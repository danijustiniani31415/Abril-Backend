using System.Security.Cryptography;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.SsomaModule.AtsFeature.Application.Interfaces;
using Abril_Backend.Features.SsomaModule.AtsFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.SsomaModule.InspeccionEquipoFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.InspeccionEquipoFeature.Application.Interfaces;
using Abril_Backend.Features.SsomaModule.InspeccionEquipoFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.SsomaModule.InspeccionEquipoFeature.Infrastructure.Models;
using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Shared.Services.Notificaciones.Dtos;
using Abril_Backend.Shared.Services.Notificaciones.Interfaces;
using Abril_Backend.Shared.Services.Residentes.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SixLabors.ImageSharp;

namespace Abril_Backend.Features.SsomaModule.InspeccionEquipoFeature.Application.Services;

public class InspeccionEquipoService : IInspeccionEquipoService
{
    private readonly IInspeccionEquipoRepository _repo;
    // Personas, DNI, firma digital y cadena de firmas salen del ATS: mismas reglas, una sola fuente.
    private readonly IAtsRepository _atsRepo;
    private readonly IAtsService _ats;
    private readonly IFileStorageService _storage;
    private readonly IStorageContainerResolver _containers;
    private readonly INotificacionesService _notificaciones;
    private readonly IResidenteProyectoResolver _residentes;
    private readonly string[] _logoPaths;
    private readonly IConfiguration _configuration;

    public InspeccionEquipoService(
        IInspeccionEquipoRepository repo,
        IAtsRepository atsRepo,
        IAtsService ats,
        IFileStorageService storage,
        IStorageContainerResolver containers,
        INotificacionesService notificaciones,
        IResidenteProyectoResolver residentes,
        IConfiguration configuration,
        IWebHostEnvironment env)
    {
        _configuration = configuration;
        _logoPaths =
        [
            Path.Combine(env.WebRootPath, "images", "abril-logo.png"),
            Path.Combine(env.WebRootPath, "images", "logo-abril.jpg"),
            Path.Combine(env.ContentRootPath, "Templates", "logo-abril.jpg"),
        ];
        _repo = repo;
        _atsRepo = atsRepo;
        _ats = ats;
        _storage = storage;
        _containers = containers;
        _notificaciones = notificaciones;
        _residentes = residentes;
    }

    private static DateTime AhoraLima => DateTime.UtcNow.AddHours(-5);

    // ═════════════════════════════════════════════════════════════════════
    // CATÁLOGO
    // ═════════════════════════════════════════════════════════════════════

    /// <summary>Ítems de un equipo (variante) + los comunes del tipo al final, ya numerados 1..n. El número NO se guarda
    /// en el catálogo: sale del orden, así reordenar o agregar ítems nunca desfasa la guía de la imagen.</summary>
    private static List<InspItemDto> ItemsDe(SsInspEquipoTipo t, int? varianteId, bool soloActivos)
    {
        var elegibles = t.Items.Where(i => (!soloActivos || i.Activo) && (i.VarianteId == null || i.VarianteId == varianteId)).ToList();
        var propios = elegibles.Where(i => i.VarianteId != null).OrderBy(i => i.Orden).ThenBy(i => i.Id);
        var comunes = elegibles.Where(i => i.VarianteId == null).OrderBy(i => i.Orden).ThenBy(i => i.Id);
        var n = 0;
        return propios.Concat(comunes).Select(i => new InspItemDto
        {
            Id = i.Id,
            Numero = ++n,
            VarianteId = i.VarianteId,
            Orden = i.Orden,
            Grupo = i.Grupo,
            NivelRiesgo = i.NivelRiesgo,
            Descripcion = i.Descripcion,
            EsCritico = i.EsCritico,
            Invertido = i.Invertido,
            Activo = i.Activo,
            Marcadores = i.Marcadores.Select(m => new InspMarcadorDto { X = m.XPct, Y = m.YPct, Px = m.PxPct, Py = m.PyPct }).ToList(),
        }).ToList();
    }

    private static InspTipoDto ToTipoDto(SsInspEquipoTipo t, bool soloActivos) => new()
    {
        Id = t.Id,
        Nombre = t.Nombre,
        CodigoFormato = t.CodigoFormato,
        Version = t.Version,
        FechaVersion = t.FechaVersion,
        Elaboracion = t.Elaboracion,
        Revision = t.Revision,
        Aprobacion = t.Aprobacion,
        IdentificaPor = t.IdentificaPor,
        ModoRespuesta = t.ModoRespuesta,
        Instrucciones = t.Instrucciones,
        RequiereTurno = t.RequiereTurno,
        RegistraCorreccion = t.RegistraCorreccion,
        RequierePlanificada = t.RequierePlanificada,
        RequiereEmpresaTipo = t.RequiereEmpresaTipo,
        PermiteVariantes = t.PermiteVariantes,
        ReqFirmaCapataz = t.ReqFirmaCapataz,
        ReqFirmaResidente = t.ReqFirmaResidente,
        ReqFirmaSsoma = t.ReqFirmaSsoma,
        Activo = t.Activo,
        Variantes = t.PermiteVariantes
            ? t.Variantes.Where(v => !soloActivos || v.Activo).OrderBy(v => v.Orden).ThenBy(v => v.Id)
                .Select(v => new InspVarianteDto
                {
                    Id = v.Id, Nombre = v.Nombre, ImagenUrl = v.ImagenUrl, Orden = v.Orden, Activo = v.Activo,
                    Items = ItemsDe(t, v.Id, soloActivos),
                }).ToList()
            : [],
        Items = t.PermiteVariantes ? [] : ItemsDe(t, null, soloActivos),
    };

    public async Task<List<InspTipoDto>> GetTipos() =>
        (await _repo.GetTipos(soloActivos: false)).Select(t => ToTipoDto(t, soloActivos: false)).ToList();

    public async Task<InspTipoDto> GetTipo(int id) =>
        ToTipoDto(await _repo.GetTipo(id) ?? throw new AbrilException("Formato no encontrado.", 404), soloActivos: false);

    public Task ActualizarTipo(int id, InspTipoGuardarDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Nombre)) throw new AbrilException("El nombre del formato es obligatorio.", 400);
        return _repo.ActualizarTipo(id, dto);
    }

    public Task<int> CrearVariante(int tipoId, InspVarianteGuardarDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Nombre)) throw new AbrilException("El nombre del equipo es obligatorio.", 400);
        return _repo.CrearVariante(tipoId, dto);
    }

    public Task ActualizarVariante(int id, InspVarianteGuardarDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Nombre)) throw new AbrilException("El nombre del equipo es obligatorio.", 400);
        return _repo.ActualizarVariante(id, dto);
    }

    public async Task<InspImagenResponseDto> SubirImagenVariante(int varianteId, Stream stream, string fileName)
    {
        _ = await _repo.GetVariante(varianteId) ?? throw new AbrilException("Equipo no encontrado.", 404);
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (ext is not (".png" or ".jpg" or ".jpeg" or ".webp"))
            throw new AbrilException("La imagen debe ser PNG, JPG o WEBP.", 400);
        var nombre = $"insp-variante_{varianteId}_{DateTime.UtcNow:yyyyMMddHHmmssfff}{ext}";
        var urls = await _storage.UploadFilesAsync([(stream, nombre)], _containers.GetAtsContainerName());
        await _repo.SetImagenVariante(varianteId, urls[0]);
        return new InspImagenResponseDto { Url = urls[0] };
    }

    public Task<int> CrearItem(int tipoId, InspItemGuardarDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Descripcion)) throw new AbrilException("La descripción del ítem es obligatoria.", 400);
        return _repo.CrearItem(tipoId, dto);
    }

    public async Task ActualizarItem(int id, InspItemGuardarDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Descripcion)) throw new AbrilException("La descripción del ítem es obligatoria.", 400);
        if (!await _repo.ActualizarItem(id, dto)) throw new AbrilException("Ítem no encontrado.", 404);
    }

    public async Task SetMarcadores(int itemId, InspMarcadoresGuardarDto dto)
    {
        if (!await _repo.SetMarcadores(itemId, dto.Marcadores)) throw new AbrilException("Ítem no encontrado.", 404);
    }

    // ═════════════════════════════════════════════════════════════════════
    // EQUIPOS
    // ═════════════════════════════════════════════════════════════════════

    public Task<List<InspEquipoDto>> GetEquipos(InspEquipoFiltroDto filtro) => _repo.GetEquipos(filtro);

    public async Task<InspEquipoDto> GetEquipo(int id) =>
        await _repo.GetEquipoDto(id) ?? throw new AbrilException("Equipo no encontrado.", 404);

    public async Task<InspEquipoDto> CrearEquipo(InspEquipoGuardarDto dto, int userId)
    {
        var codigo = dto.Codigo.Trim().ToUpperInvariant();
        if (codigo.Length == 0) throw new AbrilException("El código del equipo es obligatorio.", 400);
        var tipo = await _repo.GetTipo(dto.TipoId) ?? throw new AbrilException("Formato no encontrado.", 404);
        if (tipo.PermiteVariantes && !dto.VarianteId.HasValue)
            throw new AbrilException("Indica qué equipo es (arnés, línea de anclaje, etc.).", 400);
        if (dto.VarianteId.HasValue && tipo.Variantes.All(v => v.Id != dto.VarianteId))
            throw new AbrilException("El equipo no pertenece a este formato.", 400);

        string? fotoUrl = null;
        if (!string.IsNullOrWhiteSpace(dto.FotoBase64))
            (fotoUrl, _) = await SubirImagenBase64("insp-equipo", dto.FotoBase64);

        var id = await _repo.CrearEquipo(new SsInspEquipo
        {
            TipoId = dto.TipoId,
            ProyectoId = dto.ProyectoId,
            VarianteId = dto.VarianteId,
            Codigo = codigo,
            Descripcion = dto.Descripcion,
            FotoUrl = fotoUrl,
            AsignadoWorkerId = dto.AsignadoWorkerId,
            EmpresaTipo = dto.EmpresaTipo,
            EmpresaNombre = dto.EmpresaNombre,
            CreadoPorUserId = userId,
        });
        return await GetEquipo(id);
    }

    public async Task ActualizarEquipo(int id, InspEquipoGuardarDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Codigo)) throw new AbrilException("El código del equipo es obligatorio.", 400);
        string? fotoUrl = null;
        if (!string.IsNullOrWhiteSpace(dto.FotoBase64))
            (fotoUrl, _) = await SubirImagenBase64("insp-equipo", dto.FotoBase64);
        await _repo.ActualizarEquipo(id, dto, fotoUrl);
    }

    public async Task LiberarEquipo(int id, InspEquipoAccionDto dto, int userId)
    {
        var e = await _repo.GetEquipoEntidad(id) ?? throw new AbrilException("Equipo no encontrado.", 404);
        if (e.Estado != "fuera_servicio")
            throw new AbrilException("Este equipo no está fuera de servicio.", 409);
        if (string.IsNullOrWhiteSpace(dto.Detalle))
            throw new AbrilException("Indica qué se corrigió para poder liberar el equipo.", 400);
        string? foto = string.IsNullOrWhiteSpace(dto.FotoBase64) ? null : (await SubirImagenBase64("insp-liberacion", dto.FotoBase64)).Url;
        await _repo.CambiarEstadoEquipo(id, "operativo", null, null,
            new SsInspEquipoEvento { Evento = "liberacion", UserId = userId, Detalle = dto.Detalle.Trim(), FotoUrl = foto });
    }

    public async Task DarBajaEquipo(int id, InspEquipoAccionDto dto, int userId)
    {
        _ = await _repo.GetEquipoEntidad(id) ?? throw new AbrilException("Equipo no encontrado.", 404);
        if (string.IsNullOrWhiteSpace(dto.Detalle))
            throw new AbrilException("Indica el motivo de la baja.", 400);
        string? foto = string.IsNullOrWhiteSpace(dto.FotoBase64) ? null : (await SubirImagenBase64("insp-baja", dto.FotoBase64)).Url;
        await _repo.CambiarEstadoEquipo(id, "baja", dto.Detalle.Trim(), null,
            new SsInspEquipoEvento { Evento = "baja", UserId = userId, Detalle = dto.Detalle.Trim(), FotoUrl = foto });
    }

    public Task<List<InspEquipoEventoDto>> GetEventosEquipo(int id) => _repo.GetEventosEquipo(id);

    public async Task<string> GetOrCrearQrProyecto(int proyectoId) =>
        (await _atsRepo.GetOrCrearTokenProyecto(proyectoId)).ToString();

    // ═════════════════════════════════════════════════════════════════════
    // LLENADO PÚBLICO
    // ═════════════════════════════════════════════════════════════════════

    public async Task<InspProyectoPublicoDto> GetProyectoPublico(Guid tokenProyecto)
    {
        var proyectoId = await _atsRepo.GetProyectoPorTokenCrear(tokenProyecto);
        if (proyectoId is null)
            return new InspProyectoPublicoDto { Valido = false, MotivoInvalido = "Este código no corresponde a ningún proyecto." };

        var tipos = (await _repo.GetTipos(soloActivos: true))
            .Where(t => t.IdentificaPor == "trabajador")
            .Select(t => ToTipoDto(t, soloActivos: true))
            .ToList();

        return new InspProyectoPublicoDto
        {
            Valido = true,
            ProyectoNombre = await _repo.GetProyectoNombre(proyectoId.Value),
            Trabajadores = (await _atsRepo.GetWorkersParaAdhesion(proyectoId.Value))
                .Select(w => new InspWorkerOpcionDto { WorkerId = w.WorkerId, Nombre = w.Nombre, DniUltimos4 = w.DniUltimos4 }).ToList(),
            Tipos = tipos,
        };
    }

    public async Task<List<InspEquipoDto>> GetMisEquipos(Guid tokenProyecto, InspMisEquiposRequestDto body)
    {
        var proyectoId = await _atsRepo.GetProyectoPorTokenCrear(tokenProyecto) ?? throw new AbrilException("Enlace inválido.", 404);
        await ExigirTrabajadorHabilitado(proyectoId, body.WorkerId, body.DniConfirmacion);
        return await _repo.GetEquiposDeWorker(proyectoId, body.TipoId, body.WorkerId);
    }

    public async Task<List<InspEquipoDto>> BuscarEquipos(Guid tokenProyecto, InspBuscarEquipoRequestDto body)
    {
        var proyectoId = await _atsRepo.GetProyectoPorTokenCrear(tokenProyecto) ?? throw new AbrilException("Enlace inválido.", 404);
        await ExigirTrabajadorHabilitado(proyectoId, body.WorkerId, body.DniConfirmacion);
        var codigo = (body.Codigo ?? string.Empty).Trim().ToUpperInvariant();
        if (codigo.Length == 0) return [];
        return await _repo.BuscarCandidatos(proyectoId, body.TipoId, body.VarianteId, codigo);
    }

    public async Task<InspEquipoPublicoDto> GetEquipoPublico(Guid qrToken)
    {
        var e = await _repo.GetEquipoPorQr(qrToken);
        if (e is null)
            return new InspEquipoPublicoDto { Valido = false, MotivoInvalido = "Este código QR no corresponde a ningún equipo." };
        var tipo = await _repo.GetTipo(e.TipoId);
        if (tipo is null || !tipo.Activo || tipo.IdentificaPor != "equipo")
            return new InspEquipoPublicoDto { Valido = false, MotivoInvalido = "Este equipo no tiene un check list digital habilitado." };

        return new InspEquipoPublicoDto
        {
            Valido = true,
            Equipo = await _repo.GetEquipoDto(e.Id),
            Tipo = ToTipoDto(tipo, soloActivos: true),
            Trabajadores = (await _atsRepo.GetWorkersParaAdhesion(e.ProyectoId))
                .Select(w => new InspWorkerOpcionDto { WorkerId = w.WorkerId, Nombre = w.Nombre, DniUltimos4 = w.DniUltimos4 }).ToList(),
        };
    }

    public async Task<InspInspeccionCreadaDto> RegistrarPorProyecto(Guid tokenProyecto, InspInspeccionEnvioDto body, string? ip, string? userAgent)
    {
        var proyectoId = await _atsRepo.GetProyectoPorTokenCrear(tokenProyecto) ?? throw new AbrilException("Enlace inválido.", 404);
        return await RegistrarCore(proyectoId, null, body, ip, userAgent);
    }

    public async Task<InspInspeccionCreadaDto> RegistrarPorEquipo(Guid qrToken, InspInspeccionEnvioDto body, string? ip, string? userAgent)
    {
        var e = await _repo.GetEquipoPorQr(qrToken) ?? throw new AbrilException("Enlace inválido.", 404);
        return await RegistrarCore(e.ProyectoId, e, body, ip, userAgent);
    }

    private async Task ExigirTrabajadorHabilitado(int proyectoId, int workerId, string dni)
    {
        if (!await _atsRepo.DniCoincide(workerId, dni))
            throw new AbrilException("Los dígitos de DNI no coinciden con el trabajador seleccionado.", 400);
        var opciones = await _atsRepo.GetWorkersParaAdhesion(proyectoId);
        if (!opciones.Any(o => o.WorkerId == workerId))
            throw new AbrilException("Este trabajador no está habilitado para firmar en este proyecto.", 403);
    }

    /// <summary>"C"/"NC"/"NA" o "SI"/"NO"/"NA" → cumple / no_cumple / na. Si la pregunta está redactada en negativo
    /// ("¿Existen fugas?"), el SI es lo malo.</summary>
    private static string Normalizar(string modo, string valor, bool invertido)
    {
        var v = (valor ?? string.Empty).Trim().ToLowerInvariant();
        if (v is "na" or "n/a") return "na";
        if (modo == "C_NC_NA")
            return v switch { "c" => "cumple", "nc" => "no_cumple", _ => throw new AbrilException("Respuesta inválida.", 400) };
        return v switch
        {
            "si" or "sí" => invertido ? "no_cumple" : "cumple",
            "no" => invertido ? "cumple" : "no_cumple",
            _ => throw new AbrilException("Respuesta inválida.", 400),
        };
    }

    private async Task<InspInspeccionCreadaDto> RegistrarCore(int proyectoId, SsInspEquipo? equipoFijo, InspInspeccionEnvioDto body, string? ip, string? userAgent)
    {
        // Reintento de un check list armado sin conexión: devuelve el ya creado, no duplica.
        if (body.ClientId.HasValue)
        {
            var existente = await _repo.GetInspeccionIdPorClientId(body.ClientId.Value);
            if (existente.HasValue)
            {
                var d = await _repo.GetInspeccionDetalle(existente.Value);
                return new InspInspeccionCreadaDto { Id = existente.Value, Codigo = d?.Codigo, Resultado = d?.Resultado ?? "conforme" };
            }
        }

        var tipo = await _repo.GetTipo(body.TipoId) ?? throw new AbrilException("Formato no encontrado.", 404);
        if (!tipo.Activo) throw new AbrilException("Este formato está deshabilitado.", 409);
        var porEquipo = equipoFijo != null;
        if (porEquipo != (tipo.IdentificaPor == "equipo") || (porEquipo && equipoFijo!.TipoId != tipo.Id))
            throw new AbrilException("Este formato no corresponde a este código QR.", 400);

        await ExigirTrabajadorHabilitado(proyectoId, body.WorkerId, body.DniConfirmacion);
        if (string.IsNullOrWhiteSpace(body.SelfieBase64))
            throw new AbrilException("La selfie es obligatoria — es la prueba de que estás en obra.", 400);

        var turno = body.Turno?.Trim().ToLowerInvariant();
        if (tipo.RequiereTurno && turno is not ("dia" or "noche")) throw new AbrilException("Indica el turno (día o noche).", 400);
        if (!tipo.RequiereTurno) turno = null;
        if (tipo.RequierePlanificada && body.Planificada is null) throw new AbrilException("Indica si la inspección es planificada o no.", 400);
        var empresaTipo = body.EmpresaTipo?.Trim().ToLowerInvariant() ?? equipoFijo?.EmpresaTipo;
        if (tipo.RequiereEmpresaTipo && empresaTipo is not ("casa" or "contratista"))
            throw new AbrilException("Indica si el equipo es de la casa o de un contratista.", 400);

        var fecha = DateOnly.FromDateTime(AhoraLima);
        DateTime? capturado = null;
        if (body.OrigenOffline)
        {
            if (!body.HoraDispositivo.HasValue) throw new AbrilException("Falta la hora en que se llenó el check list sin conexión.", 400);
            capturado = DateTime.SpecifyKind(body.HoraDispositivo.Value, DateTimeKind.Utc);
            if (capturado > DateTime.UtcNow.AddMinutes(10))
                throw new AbrilException("La hora del dispositivo es posterior a la actual — revisa la fecha y hora del celular.", 400);
            if (capturado < DateTime.UtcNow.AddDays(-2))
                throw new AbrilException("Este check list se llenó hace más de 2 días y ya no se puede subir. Debe hacerse de nuevo.", 409);
            fecha = DateOnly.FromDateTime(capturado.Value.AddHours(-5));
        }

        if (!porEquipo && await _repo.ExisteInspeccionDelDia(body.WorkerId, tipo.Id, fecha, turno))
            throw new AbrilException("Ya registraste tu check list de este formato para hoy. Solo se permite uno por persona.", 409);

        var firmaBytes = await _ats.GetFirmaDigitalAutorizacionImagen(body.WorkerId)
            ?? throw new AbrilException("No hay una firma digital registrada para este trabajador. El Coordinador SSOMA debe capturarla en Autorizaciones antes de poder firmar.", 400);

        // ── Equipos y respuestas ──────────────────────────────────────────
        var entradas = new List<(InspEquipoEnvioDto Envio, int? VarianteId, string NombreVariante, string? ImagenUrl)>();
        if (porEquipo)
        {
            if (equipoFijo!.Estado != "operativo")
                throw new AbrilException("Este equipo está fuera de servicio: no puede usarse hasta que SSOMA lo libere.", 409);
            var envio = body.Equipos.FirstOrDefault() ?? new InspEquipoEnvioDto();
            entradas.Add((envio, null, string.Empty, null));
        }
        else
        {
            if (body.Equipos.Count == 0) throw new AbrilException("Marca al menos un equipo a usar hoy.", 400);
            if (body.Equipos.Select(e => e.VarianteId).Distinct().Count() != body.Equipos.Count)
                throw new AbrilException("Cada equipo solo puede marcarse una vez.", 400);
            foreach (var envio in body.Equipos)
            {
                var variante = tipo.Variantes.FirstOrDefault(v => v.Id == envio.VarianteId && v.Activo)
                    ?? throw new AbrilException("Uno de los equipos marcados no es válido.", 400);
                entradas.Add((envio, variante.Id, variante.Nombre, variante.ImagenUrl));
            }
        }

        var (selfieUrl, selfieHash) = await SubirImagenBase64("insp-selfie", body.SelfieBase64);
        var firmaHash = Convert.ToHexString(SHA256.HashData(firmaBytes));
        var firmaUrl = await SubirBytes("insp-firma", firmaBytes, "png");

        var insp = new SsInspInspeccion
        {
            TipoId = tipo.Id,
            ProyectoId = proyectoId,
            EquipoId = equipoFijo?.Id,
            Fecha = fecha,
            Turno = turno,
            Planificada = tipo.RequierePlanificada ? body.Planificada : null,
            EmpresaTipo = tipo.RequiereEmpresaTipo ? empresaTipo : null,
            EmpresaNombre = tipo.RequiereEmpresaTipo ? (body.EmpresaNombre ?? equipoFijo?.EmpresaNombre) : null,
            Observaciones = body.Observaciones,
            OperadorWorkerId = body.WorkerId,
            HoraServidor = DateTime.UtcNow,
            HoraDispositivo = body.HoraDispositivo,
            OrigenOffline = body.OrigenOffline,
            ClientId = body.ClientId,
            Lat = body.Lat,
            Lng = body.Lng,
            PrecisionMetros = body.PrecisionMetros,
            SelfieUrl = selfieUrl,
            SelfieHash = selfieHash,
            FirmaUrl = firmaUrl,
            FirmaHash = firmaHash,
            IpOrigen = ip,
            UserAgent = userAgent,
        };

        var equiposNuevos = new List<int>();
        var bloqueos = new List<(int EquipoId, string Codigo, string Motivo)>();
        var orden = 0;
        var hayNc = false;

        foreach (var (envio, varianteId, nombreVariante, imagenUrl) in entradas)
        {
            // Equipo: del QR, de "mis equipos" o por el código escrito (si es nuevo se registra con su foto, una sola vez).
            SsInspEquipo equipo;
            var esNuevo = false;
            if (porEquipo)
            {
                equipo = equipoFijo!;
            }
            else
            {
                var codigo = (envio.Codigo ?? string.Empty).Trim().ToUpperInvariant();
                if (codigo.Length == 0) throw new AbrilException($"Escribe el código del equipo «{nombreVariante}».", 400);

                // Los códigos de campo son cortos y se repiten ("281"): la identidad es el id. La persona elige su equipo entre los
                // candidatos (con foto) o indica que es otro; solo entonces se registra uno nuevo.
                SsInspEquipo? encontrado = null;
                if (envio.EquipoId.HasValue)
                {
                    encontrado = await _repo.GetEquipoEntidad(envio.EquipoId.Value);
                    if (encontrado is null || !encontrado.Activo || encontrado.TipoId != tipo.Id || encontrado.VarianteId != varianteId)
                        throw new AbrilException($"El equipo seleccionado para «{nombreVariante}» no es válido.", 400);
                    // Equipo de otro proyecto: solo si es de la casa o es el propio de la persona (el arnés personal viaja con ella).
                    if (encontrado.ProyectoId != proyectoId && encontrado.AsignadoWorkerId != body.WorkerId && encontrado.EmpresaTipo != "casa")
                        throw new AbrilException($"El equipo «{nombreVariante} {encontrado.Codigo}» no está disponible en este proyecto.", 403);
                }
                else if (!envio.EsEquipoNuevo || body.OrigenOffline)
                {
                    var candidatos = await _repo.BuscarCandidatos(proyectoId, tipo.Id, varianteId, codigo);
                    if (candidatos.Count > 0)
                    {
                        // Armado SIN conexión: el celular no pudo mostrar los candidatos. Se vincula solo si no hay duda: el equipo propio de
                        // esa persona o, si no, uno solo en el proyecto. Si es ambiguo, se rechaza y se corrige con conexión.
                        var propios = candidatos.Where(c => c.AsignadoWorkerId == body.WorkerId).ToList();
                        var delProyecto = candidatos.Where(c => c.ProyectoId == proyectoId).ToList();
                        var elegido = !body.OrigenOffline ? null
                            : propios.Count == 1 ? propios[0]
                            : propios.Count == 0 && delProyecto.Count == 1 ? delProyecto[0]
                            : null;
                        if (elegido is not null)
                            encontrado = await _repo.GetEquipoEntidad(elegido.Id);
                        else if (!(body.OrigenOffline && envio.EsEquipoNuevo))
                            throw new AbrilException($"Ya hay equipos con el código {codigo} ({nombreVariante}): elige el tuyo en la lista o marca «es otro equipo».", 409);
                    }
                }

                if (encontrado is null)
                {
                    if (string.IsNullOrWhiteSpace(envio.FotoBase64))
                        throw new AbrilException($"El equipo «{nombreVariante} {codigo}» es nuevo: sube una foto donde se vea el código (solo se pide esta primera vez).", 400);
                    var (fotoUrl, _) = await SubirImagenBase64("insp-equipo", envio.FotoBase64);
                    var nuevoId = await _repo.CrearEquipo(new SsInspEquipo
                    {
                        TipoId = tipo.Id,
                        ProyectoId = proyectoId,
                        VarianteId = varianteId,
                        Codigo = codigo,
                        FotoUrl = fotoUrl,
                        AsignadoWorkerId = body.WorkerId,
                    });
                    equipo = (await _repo.GetEquipoEntidad(nuevoId))!;
                    esNuevo = true;
                    equiposNuevos.Add(nuevoId);
                }
                else
                {
                    equipo = encontrado;
                }

                // Un equipo bloqueado en cualquier proyecto sigue bloqueado donde esté: no se mueve ni se usa hasta que SSOMA lo libere.
                if (equipo.Estado != "operativo")
                    throw new AbrilException($"El equipo «{nombreVariante} {equipo.Codigo}» está fuera de servicio: no puede usarse hasta que SSOMA lo libere.", 409);

                if (equipo.ProyectoId != proyectoId)
                {
                    var origen = await _repo.GetProyectoNombre(equipo.ProyectoId) ?? $"proyecto {equipo.ProyectoId}";
                    var destino = await _repo.GetProyectoNombre(proyectoId) ?? $"proyecto {proyectoId}";
                    await _repo.TrasladarEquipo(equipo.Id, proyectoId, body.WorkerId, $"De {origen} a {destino}");
                }
            }

            var variante = new SsInspInspeccionVariante
            {
                VarianteId = varianteId,
                Nombre = nombreVariante,
                ImagenUrl = imagenUrl,
                CodigoEquipo = equipo.Codigo,
                EquipoId = equipo.Id,
                Orden = insp.Variantes.Count,
            };
            if (!porEquipo) insp.Variantes.Add(variante);

            var items = ItemsDe(tipo, varianteId, soloActivos: true);
            if (items.Count == 0) throw new AbrilException("Este formato no tiene ítems configurados.", 409);
            var porItem = envio.Respuestas.GroupBy(r => r.ItemId).ToDictionary(g => g.Key, g => g.Last());

            foreach (var item in items)
            {
                if (!porItem.TryGetValue(item.Id, out var r))
                    throw new AbrilException($"Falta responder el ítem {item.Numero}{(porEquipo ? "" : $" de «{nombreVariante}»")}.", 400);

                var resultado = Normalizar(tipo.ModoRespuesta, r.Valor, item.Invertido);
                string? fotoItem = null;
                bool? seCorrigio = null;
                if (resultado == "no_cumple")
                {
                    hayNc = true;
                    seCorrigio = tipo.RegistraCorreccion ? r.SeCorrigio : null;
                    if (seCorrigio != true)
                    {
                        if (string.IsNullOrWhiteSpace(r.FotoBase64))
                            throw new AbrilException($"El ítem {item.Numero} no cumple: adjunta una foto como evidencia.", 400);
                        fotoItem = (await SubirImagenBase64("insp-nc", r.FotoBase64)).Url;
                        if (item.EsCritico)
                            bloqueos.Add((equipo.Id, equipo.Codigo, $"Ítem {item.Numero}: {item.Descripcion}"));
                    }
                    else if (!string.IsNullOrWhiteSpace(r.FotoBase64))
                    {
                        fotoItem = (await SubirImagenBase64("insp-nc", r.FotoBase64)).Url;
                    }
                }

                var resp = new SsInspRespuesta
                {
                    ItemId = item.Id,
                    Numero = item.Numero,
                    Orden = ++orden,
                    Grupo = item.Grupo,
                    NivelRiesgo = item.NivelRiesgo,
                    Descripcion = item.Descripcion,
                    EsCritico = item.EsCritico,
                    Invertido = item.Invertido,
                    Respuesta = resultado,
                    Observacion = string.IsNullOrWhiteSpace(r.Observacion) ? null : r.Observacion.Trim(),
                    SeCorrigio = seCorrigio,
                    FotoUrl = fotoItem,
                };
                insp.Respuestas.Add(resp);
                if (!porEquipo) variante.Respuestas.Add(resp);
            }
            _ = esNuevo;
        }

        insp.Resultado = bloqueos.Count > 0 ? "no_conforme_critico" : hayNc ? "con_observaciones" : "conforme";

        // ¿Quién falta firmar? El Capataz solo aplica si el operador es obrero de obra (si es Staff, su propia firma cubre ese nivel).
        var requiereCapataz = tipo.ReqFirmaCapataz && await _atsRepo.EsObreroDeObra(body.WorkerId);
        insp.Estado = requiereCapataz || tipo.ReqFirmaResidente || tipo.ReqFirmaSsoma ? "Pendiente_Firmas" : "Completo";

        try
        {
            await _repo.CrearInspeccion(insp);
        }
        catch (DbUpdateException)
        {
            throw new AbrilException("Ya existe un check list vigente para este equipo en la misma fecha y turno.", 409);
        }

        foreach (var id in equiposNuevos) await _repo.SetRegistradoEn(id, insp.Id);

        var bloqueados = new List<string>();
        foreach (var grupo in bloqueos.GroupBy(b => b.EquipoId))
        {
            var motivo = string.Join("; ", grupo.Select(g => g.Motivo));
            await _repo.CambiarEstadoEquipo(grupo.Key, "fuera_servicio", motivo, insp.Id,
                new SsInspEquipoEvento { Evento = "bloqueo", InspeccionId = insp.Id, WorkerId = body.WorkerId, Detalle = motivo });
            bloqueados.Add(grupo.First().Codigo);
        }

        if (bloqueados.Count > 0)
            await AvisarBloqueo(proyectoId, insp, bloqueados);
        if (insp.Estado == "Pendiente_Firmas")
            await AvisarNivel(proyectoId, requiereCapataz ? "Capataz" : tipo.ReqFirmaResidente ? "Autoriza" : "Ssoma", tipo.CodigoFormato, insp.Id);

        return new InspInspeccionCreadaDto { Id = insp.Id, Codigo = insp.Codigo, Resultado = insp.Resultado, EquiposBloqueados = bloqueados };
    }

    // ═════════════════════════════════════════════════════════════════════
    // INSPECCIONES (logueado)
    // ═════════════════════════════════════════════════════════════════════

    public Task<(List<InspInspeccionListaDto> Items, int Total)> GetInspecciones(InspInspeccionFiltroDto filtro, int page, int pageSize) =>
        _repo.GetInspecciones(filtro, Math.Max(page, 1), Math.Clamp(pageSize, 1, 100));

    public async Task<InspInspeccionDetalleDto> GetDetalle(int id)
    {
        var d = await _repo.GetInspeccionDetalle(id) ?? throw new AbrilException("Check list no encontrado.", 404);
        var entidad = await _repo.GetInspeccionEntidad(id);
        if (entidad is not null)
        {
            var tipo = await _repo.GetTipo(entidad.TipoId);
            if (tipo is not null) d.Tipo = ToTipoDto(tipo, soloActivos: false);
            d.RequiereCapataz = (d.Tipo?.ReqFirmaCapataz ?? false) && await _atsRepo.EsObreroDeObra(entidad.OperadorWorkerId);
        }
        return d;
    }

    public async Task Firmar(int id, string rol, int callerUserId, bool esAdmin)
    {
        if (rol is not ("Capataz" or "Residente" or "Ssoma")) throw new AbrilException("Nivel de firma inválido.", 400);
        var i = await _repo.GetInspeccionEntidad(id) ?? throw new AbrilException("Check list no encontrado.", 404);
        if (i.Estado == "Anulado") throw new AbrilException("Este check list está anulado.", 409);
        var tipo = await _repo.GetTipo(i.TipoId) ?? throw new AbrilException("Formato no encontrado.", 404);

        var requiereCapataz = tipo.ReqFirmaCapataz && await _atsRepo.EsObreroDeObra(i.OperadorWorkerId);
        var yaFirmado = rol switch { "Capataz" => i.CapatazFirmaUrl != null, "Residente" => i.ResidenteFirmaUrl != null, _ => i.SsomaFirmaUrl != null };
        if (yaFirmado) throw new AbrilException("Este nivel ya firmó.", 409);
        if (rol == "Capataz" && !requiereCapataz) throw new AbrilException("Este check list no requiere firma de Capataz.", 409);
        if (rol == "Residente" && !tipo.ReqFirmaResidente) throw new AbrilException("Este formato no requiere firma del Residente.", 409);
        if (rol == "Ssoma" && !tipo.ReqFirmaSsoma) throw new AbrilException("Este formato no requiere firma de SSOMA.", 409);
        // Orden de la cadena: Capataz → Residente/Producción → SSOMA (SSOMA firma al final, igual que el ATS).
        if (rol == "Residente" && requiereCapataz && i.CapatazFirmaUrl == null)
            throw new AbrilException("Falta la firma del Capataz/Maestro de obra. Debe firmar antes del Residente.", 409);
        if (rol == "Ssoma" && tipo.ReqFirmaResidente && i.ResidenteFirmaUrl == null)
            throw new AbrilException("Falta la firma del Residente/Producción. Debe firmar antes de SSOMA.", 409);

        var workerId = await _atsRepo.ResolverWorkerIdAsync(callerUserId);
        if (i.OperadorWorkerId == workerId && !esAdmin)
            throw new AbrilException("No puedes firmar un check list que tú mismo llenaste.", 403);

        if (!esAdmin)
        {
            if (rol == "Capataz")
            {
                if (!await _atsRepo.EsCapatazDeProyecto(workerId, i.ProyectoId))
                    throw new AbrilException("Solo el Capataz o Maestro de obra de este proyecto puede firmar este nivel.", 403);
            }
            else if (rol == "Residente")
            {
                var esResidente = await _residentes.EsResidenteDelProyectoAsync(callerUserId, i.ProyectoId);
                if (!esResidente && !await _atsRepo.EsProduccionDeProyecto(workerId, i.ProyectoId))
                    throw new AbrilException("Solo el Residente o el Ing./Arq. de Producción de este proyecto pueden firmar este nivel.", 403);
            }
            else
            {
                var responsables = await _atsRepo.GetResponsables(i.ProyectoId);
                var email = await _atsRepo.GetEmailCorporativoWorker(workerId);
                var esCoord = email != null && responsables.SsomaEmails.Contains(email, StringComparer.OrdinalIgnoreCase);
                if (!esCoord && !await _atsRepo.EsPrevencionistaAbril(workerId))
                    throw new AbrilException("Solo el Coordinador SSOMA de este proyecto o un prevencionista de Abril pueden firmar este nivel.", 403);
            }
        }

        var firmaBytes = await _ats.GetFirmaDigitalAutorizacionImagen(workerId)
            ?? throw new AbrilException("No hay una firma digital registrada para ti. El Coordinador SSOMA debe capturarla en Autorizaciones antes de poder firmar.", 400);
        var (nombre, cargo) = await _atsRepo.GetNombreYCargo(workerId);
        var firmaHash = Convert.ToHexString(SHA256.HashData(firmaBytes));
        var firmaUrl = await SubirBytes($"insp-{rol.ToLower()}", firmaBytes, "png");

        // ¿Con esta firma ya está todo? (las que el formato exige y todavía faltaban)
        var faltaCapataz = requiereCapataz && rol != "Capataz" && i.CapatazFirmaUrl == null;
        var faltaResidente = tipo.ReqFirmaResidente && rol != "Residente" && i.ResidenteFirmaUrl == null;
        var faltaSsoma = tipo.ReqFirmaSsoma && rol != "Ssoma" && i.SsomaFirmaUrl == null;
        var completo = !faltaCapataz && !faltaResidente && !faltaSsoma;

        await _repo.FirmarNivel(id, rol, workerId, nombre, cargo, firmaUrl, firmaHash, DateTime.UtcNow, completo);

        if (!completo)
        {
            var siguiente = rol == "Capataz" ? (tipo.ReqFirmaResidente ? "Autoriza" : "Ssoma") : "Ssoma";
            await AvisarNivel(i.ProyectoId, siguiente, tipo.CodigoFormato, id);
        }
    }

    public Task Anular(int id, string motivo, int userId)
    {
        if (string.IsNullOrWhiteSpace(motivo)) throw new AbrilException("Indica el motivo de la anulación.", 400);
        return _repo.Anular(id, motivo.Trim(), userId);
    }

    public Task<InspDashboardDto> GetDashboard(int? proyectoId) => _repo.GetDashboard(proyectoId);

    // ═════════════════════════════════════════════════════════════════════
    // PDF
    // ═════════════════════════════════════════════════════════════════════

    // Un solo HttpClient para todos los PDF (mismo criterio que ATS: no agotar sockets bajando imagen por imagen).
    private static readonly HttpClient _httpImagenes = new() { Timeout = TimeSpan.FromSeconds(20) };

    private static async Task<byte[]?> Bajar(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        try { return await _httpImagenes.GetByteArrayAsync(url); }
        catch { return null; }
    }

    /// <summary>PDF del check list: encabezado corporativo del formato, una sección por equipo (imagen guía con sus números ubicados
    /// por los marcadores + criterios marcados tal cual el papel), evidencias y la cadena de firmas.</summary>
    public async Task<byte[]> GenerarPdf(int id)
    {
        var d = await GetDetalle(id);
        var tipo = d.Tipo ?? throw new AbrilException("Formato no encontrado.", 404);

        var equipos = new List<InspPdfEquipo>();
        var evidenciasBajadas = 0;
        async Task<Dictionary<int, byte[]>> EvidenciasDe(IEnumerable<InspRespuestaDto> rs)
        {
            var res = new Dictionary<int, byte[]>();
            foreach (var r in rs.Where(x => !string.IsNullOrWhiteSpace(x.FotoUrl)))
            {
                if (evidenciasBajadas >= 20) break;
                var b = await Bajar(r.FotoUrl);
                if (b is null) continue;
                res[r.Id] = b;
                evidenciasBajadas++;
            }
            return res;
        }

        if (d.EquiposUsados.Count == 0)
        {
            var titulo = string.IsNullOrWhiteSpace(d.EquipoCodigo) ? tipo.Nombre : $"{tipo.Nombre} — {d.EquipoCodigo}";
            equipos.Add(new InspPdfEquipo(titulo, null, 0, 0, d.Respuestas, await EvidenciasDe(d.Respuestas)));
        }
        else
        {
            foreach (var e in d.EquiposUsados)
            {
                var rs = d.Respuestas.Where(r => r.InspeccionVarianteId == e.Id).ToList();
                var img = await Bajar(e.ImagenUrl);
                int w = 0, h = 0;
                if (img is not null)
                {
                    try { var info = Image.Identify(img); w = info.Width; h = info.Height; }
                    catch { img = null; }
                }
                equipos.Add(new InspPdfEquipo($"{e.Nombre} — Código {e.CodigoEquipo}", img, w, h, rs, await EvidenciasDe(rs)));
            }
        }

        var equiposDelFormato = tipo.PermiteVariantes
            ? tipo.Variantes.Where(v => v.Activo).Select(v => (v.Nombre, d.EquiposUsados.Any(e => e.Nombre == v.Nombre))).ToList()
            : [];

        var selfie = await Bajar(d.SelfieUrl);
        var operador = new InspPdfFirma(d.OperadorNombre, null, d.HoraServidor, await Bajar(d.FirmaUrl));
        var capataz = d.RequiereCapataz ? new InspPdfFirma(d.CapatazNombre, null, d.CapatazHoraServidor, await Bajar(d.CapatazFirmaUrl)) : null;
        var residente = tipo.ReqFirmaResidente ? new InspPdfFirma(d.ResidenteNombre, null, d.ResidenteHoraServidor, await Bajar(d.ResidenteFirmaUrl)) : null;
        var ssoma = tipo.ReqFirmaSsoma ? new InspPdfFirma(d.SsomaNombre, null, d.SsomaHoraServidor, await Bajar(d.SsomaFirmaUrl)) : null;

        byte[]? logoBytes = null;
        var logoPath = _logoPaths.FirstOrDefault(File.Exists);
        if (logoPath != null) logoBytes = await File.ReadAllBytesAsync(logoPath);

        var entidad = await _repo.GetInspeccionEntidad(id) ?? throw new AbrilException("Check list no encontrado.", 404);
        var hash = HashVerificacion(entidad);
        var baseUrl = _configuration["Frontend:BaseUrl"]?.TrimEnd('/') ?? "https://intranet.abril.pe";
        var verificacionUrl = $"{baseUrl}/inspeccion-equipos/verificar/{id}?hash={hash}";

        return InspeccionEquipoPdfService.Generar(d, equipos, equiposDelFormato, selfie, operador, capataz, residente, ssoma, logoBytes, verificacionUrl, hash);
    }

    /// <summary>Huella corta que va en el QR del PDF: ata el documento a su id, código, firma del operador y fecha. Sin ella (o con otra)
    /// la verificación pública no devuelve nada, así que no se pueden recorrer ids para curiosear.</summary>
    private static string HashVerificacion(SsInspInspeccion i) =>
        Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"{i.Id}|{i.Codigo}|{i.FirmaHash}|{i.Fecha:yyyyMMdd}")))[..12].ToLowerInvariant();

    public async Task<InspVerificacionDto> VerificarPublico(int id, string? hash)
    {
        var i = await _repo.GetInspeccionEntidad(id);
        if (i is null || string.IsNullOrWhiteSpace(hash) || !string.Equals(hash.Trim(), HashVerificacion(i), StringComparison.OrdinalIgnoreCase))
            return new InspVerificacionDto { Valido = false, MotivoInvalido = "El código de verificación no coincide con ningún check list." };

        var d = await GetDetalle(id);
        var firmas = new List<InspVerificacionFirmaDto>();
        if (d.RequiereCapataz) firmas.Add(new InspVerificacionFirmaDto { Rol = "Capataz / Maestro de obra", Nombre = d.CapatazNombre, Hora = d.CapatazHoraServidor, Firmado = d.FirmadoCapataz });
        if (d.Tipo?.ReqFirmaResidente == true) firmas.Add(new InspVerificacionFirmaDto { Rol = "Residente / Producción", Nombre = d.ResidenteNombre, Hora = d.ResidenteHoraServidor, Firmado = d.FirmadoResidente });
        if (d.Tipo?.ReqFirmaSsoma == true) firmas.Add(new InspVerificacionFirmaDto { Rol = "Responsable SSOMA", Nombre = d.SsomaNombre, Hora = d.SsomaHoraServidor, Firmado = d.FirmadoSsoma });

        return new InspVerificacionDto
        {
            Valido = true,
            Codigo = d.Codigo,
            CodigoFormato = d.CodigoFormato,
            TipoNombre = d.TipoNombre,
            ProyectoNombre = d.ProyectoNombre,
            Fecha = d.Fecha,
            Turno = d.Turno,
            Operador = d.OperadorNombre,
            Equipos = d.Equipos,
            Estado = d.Estado,
            Resultado = d.Resultado,
            Firmas = firmas,
        };
    }

    public async Task<byte[]> GenerarRegistroSemanalPdf(int equipoId, DateOnly fecha)
    {
        var equipo = await _repo.GetEquipoDto(equipoId) ?? throw new AbrilException("Equipo no encontrado.", 404);
        var tipoEnt = await _repo.GetTipo(equipo.TipoId) ?? throw new AbrilException("Formato no encontrado.", 404);
        if (tipoEnt.IdentificaPor != "equipo")
            throw new AbrilException("El registro semanal aplica a los formatos que se llenan por equipo (ej. minicargador).", 400);
        var tipo = ToTipoDto(tipoEnt, soloActivos: true);

        var lunes = fecha.AddDays(-(((int)fecha.DayOfWeek + 6) % 7));
        var inspecciones = await _repo.GetInspeccionesDeEquipo(equipoId, lunes, lunes.AddDays(5));

        var dias = new List<InspPdfDia>();
        for (var n = 0; n < 6; n++)
        {
            var dia = lunes.AddDays(n);
            var i = inspecciones.Where(x => x.Fecha == dia).OrderByDescending(x => x.Id).FirstOrDefault();
            dias.Add(i is null
                ? new InspPdfDia(dia, null, null, null, null, null)
                : new InspPdfDia(dia, i.Resultado, i.OperadorWorker?.Person?.FullName,
                    await Bajar(i.FirmaUrl), await Bajar(i.ResidenteFirmaUrl), await Bajar(i.SsomaFirmaUrl)));
        }

        byte[]? logoBytes = null;
        var logoPath = _logoPaths.FirstOrDefault(File.Exists);
        if (logoPath != null) logoBytes = await File.ReadAllBytesAsync(logoPath);

        var semana = System.Globalization.ISOWeek.GetWeekOfYear(lunes.ToDateTime(TimeOnly.MinValue));
        return InspeccionEquipoPdfService.GenerarRegistroSemanal(tipo, equipo, lunes, semana, dias, logoBytes);
    }

    // ═════════════════════════════════════════════════════════════════════
    // AVISOS (campanita, best-effort) Y ALMACENAMIENTO
    // ═════════════════════════════════════════════════════════════════════

    private async Task AvisarNivel(int proyectoId, string nivel, string codigoFormato, int inspeccionId)
    {
        try
        {
            var destinatarios = await _atsRepo.GetEmailsNivel(proyectoId, nivel);
            if (destinatarios.Count == 0) return;
            var responsables = await _atsRepo.GetResponsables(proyectoId);
            await _notificaciones.CrearPorCorreosAsync(
                "INSPECCION_EQUIPO_PENDIENTE_FIRMA",
                destinatarios,
                origenUserId: null,
                items: [new NuevaNotificacionDto
                {
                    Titulo = $"Check list {codigoFormato} pendiente de tu firma",
                    Subtitulo = responsables.ProyectoNombre,
                    Descripcion = "Una inspección pre-uso de equipo espera tu firma.",
                    Referencia = $"/ssoma/gestion/inspeccion-equipos/{inspeccionId}",
                }]);
        }
        catch { /* best-effort */ }
    }

    private async Task AvisarBloqueo(int proyectoId, SsInspInspeccion insp, List<string> codigos)
    {
        try
        {
            var destinatarios = new List<string>();
            destinatarios.AddRange(await _atsRepo.GetEmailsNivel(proyectoId, "Ssoma"));
            destinatarios.AddRange(await _atsRepo.GetEmailsNivel(proyectoId, "Autoriza"));
            if (destinatarios.Count == 0) return;
            var responsables = await _atsRepo.GetResponsables(proyectoId);
            await _notificaciones.CrearPorCorreosAsync(
                "INSPECCION_EQUIPO_BLOQUEADO",
                destinatarios.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                origenUserId: null,
                items: [new NuevaNotificacionDto
                {
                    Titulo = "Equipo fuera de servicio",
                    Subtitulo = responsables.ProyectoNombre,
                    Descripcion = $"La inspección {insp.Codigo} dejó fuera de servicio: {string.Join(", ", codigos)}. No puede usarse hasta que SSOMA lo libere.",
                    Referencia = $"/ssoma/gestion/inspeccion-equipos/{insp.Id}",
                }]);
        }
        catch { /* best-effort */ }
    }

    private async Task<(string Url, string Hash)> SubirImagenBase64(string prefijo, string base64)
    {
        byte[] bytes;
        try
        {
            var payload = base64.Contains(',') ? base64[(base64.IndexOf(',') + 1)..] : base64;
            bytes = Convert.FromBase64String(payload);
        }
        catch (FormatException)
        {
            throw new AbrilException("La imagen enviada no es válida.", 400);
        }
        if (bytes.Length == 0) throw new AbrilException("La imagen enviada está vacía.", 400);
        if (bytes.Length > 12 * 1024 * 1024) throw new AbrilException("La imagen es demasiado grande (máximo 12 MB).", 400);
        return (await SubirBytes(prefijo, bytes, "jpg"), Convert.ToHexString(SHA256.HashData(bytes)));
    }

    private async Task<string> SubirBytes(string prefijo, byte[] bytes, string ext)
    {
        using var stream = new MemoryStream(bytes);
        var nombre = $"{prefijo}_{DateTime.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid().ToString("N")[..6]}.{ext}";
        var urls = await _storage.UploadFilesAsync([(stream, nombre)], _containers.GetAtsContainerName());
        return urls[0];
    }
}
