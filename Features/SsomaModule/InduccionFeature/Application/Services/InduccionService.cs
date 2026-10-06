using System.Security.Cryptography;
using System.Text.Json;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.SsomaModule.AtsFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.SsomaModule.InduccionFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.InduccionFeature.Application.Interfaces;
using Abril_Backend.Features.SsomaModule.InduccionFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.SsomaModule.InduccionFeature.Infrastructure.Models;
using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Shared.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Hosting;

namespace Abril_Backend.Features.SsomaModule.InduccionFeature.Application.Services;

public class InduccionService : IInduccionService
{
    public const string DeclaracionVersion = "SSO-FO-031 v01";

    /// <summary>Texto de "Declaración y compromiso" tomado del formato vigente SSO-FO-031.</summary>
    public const string DeclaracionTexto =
        "Declaro haber asistido al curso de inducción de Seguridad y Salud Ocupacional en el Trabajo, en el cual se me explicaron todos los riesgos relacionados a los mismos y sobre las Normas Básicas de Seguridad y Salud en el trabajo que debo cumplir dentro de la obra. " +
        "Declaro que me regiré por las Normas y Procedimientos especificados en este curso y por las demás Normas de Seguridad y Salud en el Trabajo dadas por ABRIL GRUPO INMOBILIARIO, adecuando mi desempeño laboral a una CONDUCTA SIEMPRE SEGURA. " +
        "Entiendo que el trabajar en forma segura, cumplir y acatar todas las Normas y Procedimientos de Seguridad, es una condición para mi permanencia en los ambientes y labores de obra. " +
        "Comprendo claramente que no se me permite el uso, posesión, presencia, compra y venta, o de estar bajo la influencia de bebidas alcohólicas y/o sustancias alucinógenas y/o controladas (drogas), en los recintos e instalaciones en las que desarrolle mis labores o en las que ésta tenga presencia (Alojamiento). " +
        "Declaro que me regiré por las Normas basadas en el Reglamento Interno de Trabajo de ABRIL GRUPO INMOBILIARIO, obedeciendo a mis Supervisores, cumpliendo con los horarios de trabajo establecidos, trabajar con el uniforme entregado por la empresa y/o contrata, y con los implementos de seguridad necesarios para los trabajos, así mismo evitar conflictos personales con cualquier trabajador de la empresa. " +
        "Declaro que no tengo enfermedades que afecten el normal desempeño de mi trabajo. " +
        "Entiendo y acepto que el incumplimiento de las Normas y Procedimientos de Seguridad y Salud establecidos por ABRIL GRUPO INMOBILIARIO, está sujeto a sanciones, por lo que me comprometo a acatar las sanciones establecidas en las Medidas Disciplinarias, las cuales conozco y acato en su totalidad.";

    private static readonly string[] TiposPregunta = ["VF", "Alternativa", "Relacionar"];
    private static readonly string[] TiposDocumento = ["Documento", "Procedimiento", "IPERC", "Legal"];
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };

    private readonly IInduccionRepository _repository;
    private readonly IAtsRepository _atsRepository;
    private readonly IFileStorageService _fileStorageService;
    private readonly IStorageContainerResolver _containerResolver;
    private readonly IInduccionActaPdfService _actaPdf;
    private readonly IInduccionPostCierreRepository _postCierre;
    private readonly IInduccionSelloService _sello;
    private readonly IInduccionRespaldoService _respaldo;
    private readonly IConfiguration _configuration;
    private readonly string[] _logoPaths;

    public InduccionService(
        IInduccionRepository repository,
        IAtsRepository atsRepository,
        IFileStorageService fileStorageService,
        IStorageContainerResolver containerResolver,
        IInduccionActaPdfService actaPdf,
        IInduccionPostCierreRepository postCierre,
        IInduccionSelloService sello,
        IInduccionRespaldoService respaldo,
        IConfiguration configuration,
        IWebHostEnvironment env)
    {
        _repository = repository;
        _atsRepository = atsRepository;
        _fileStorageService = fileStorageService;
        _containerResolver = containerResolver;
        _actaPdf = actaPdf;
        _postCierre = postCierre;
        _sello = sello;
        _respaldo = respaldo;
        _configuration = configuration;
        _logoPaths =
        [
            Path.Combine(env.WebRootPath ?? string.Empty, "images", "abril-logo.png"),
            Path.Combine(env.WebRootPath ?? string.Empty, "images", "logo-abril.jpg"),
            Path.Combine(env.ContentRootPath, "Templates", "logo-abril.jpg"),
        ];
    }

    // ════════════════════════════════════════════════════════════════════════
    // Catálogos
    // ════════════════════════════════════════════════════════════════════════

    public async Task<List<IndDocumentoDto>> ListarDocumentos(bool soloActivos)
        => (await _repository.ListarDocumentos(soloActivos)).Select(ToDto).ToList();

    public async Task<IndDocumentoDto> GuardarDocumento(IndDocumentoGuardarDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Codigo)) throw new AbrilException("El código es obligatorio.", 400);
        if (string.IsNullOrWhiteSpace(dto.Nombre)) throw new AbrilException("El nombre es obligatorio.", 400);
        if (!TiposDocumento.Contains(dto.Tipo)) throw new AbrilException("Tipo de documento no válido.", 400);
        var d = await _repository.GuardarDocumento(dto);
        return ToDto(d);
    }

    /// <summary>Sube el archivo, calcula su SHA-256 y publica la versión siguiente. La versión anterior queda
    /// intacta (las sesiones ya hechas siguen apuntando a ella).</summary>
    public async Task<IndDocumentoVersionDto> SubirVersionDocumento(int documentoId, Stream archivo, string nombreArchivo,
        bool esCambioMaterial, string? nota, int userId)
    {
        using var ms = new MemoryStream();
        await archivo.CopyToAsync(ms);
        var bytes = ms.ToArray();
        if (bytes.Length == 0) throw new AbrilException("El archivo está vacío.", 400);

        var hash = Convert.ToHexString(SHA256.HashData(bytes));
        var container = _containerResolver.GetAtsContainerName();
        using var stream = new MemoryStream(bytes);
        var fileName = $"induccion_doc{documentoId}_{DateTime.UtcNow:yyyyMMddHHmmssfff}{Path.GetExtension(nombreArchivo)}";
        var urls = await _fileStorageService.UploadFilesAsync([(stream, fileName)], container);

        var v = await _repository.PublicarVersion(documentoId, urls[0], hash, esCambioMaterial, nota, userId);
        return ToDto(v);
    }

    public async Task<List<IndDocumentoVersionDto>> ListarVersiones(int documentoId)
        => (await _repository.ListarVersiones(documentoId)).Select(ToDto).ToList();

    public async Task<List<IndTemaDto>> ListarTemas(bool soloActivos)
        => (await _repository.ListarTemas(soloActivos))
            .Select(t => new IndTemaDto { Id = t.Id, Orden = t.Orden, Descripcion = t.Descripcion, Activo = t.Activo }).ToList();

    public async Task<IndTemaDto> GuardarTema(IndTemaGuardarDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Descripcion)) throw new AbrilException("La descripción del tema es obligatoria.", 400);
        var t = await _repository.GuardarTema(dto);
        return new IndTemaDto { Id = t.Id, Orden = t.Orden, Descripcion = t.Descripcion, Activo = t.Activo };
    }

    public async Task<List<IndPreguntaDto>> ListarPreguntas(bool soloActivas)
        => (await _repository.ListarPreguntas(soloActivas)).Select(ToDto).ToList();

    public async Task<IndPreguntaDto> GuardarPregunta(IndPreguntaGuardarDto dto)
    {
        ValidarPregunta(dto);
        var p = await _repository.GuardarPregunta(dto);
        return ToDto(p);
    }

    /// <summary>Congela el banco activo en una versión inmutable. Los intentos y las sesiones apuntan a esa
    /// versión, así una nota siempre se puede reproducir aunque el banco cambie después.</summary>
    public async Task<IndEvaluacionVersionDto> PublicarEvaluacion(IndEvaluacionPublicarDto dto, int userId)
    {
        var preguntas = await _repository.ListarPreguntas(soloActivas: true);
        if (preguntas.Count == 0) throw new AbrilException("No hay preguntas activas para publicar la evaluación.", 400);
        foreach (var p in preguntas)
            ValidarPregunta(new IndPreguntaGuardarDto
            {
                Tipo = p.Tipo, Enunciado = p.Enunciado, Puntos = p.Puntos,
                Opciones = p.Opciones.Select(o => new IndOpcionDto { Texto = o.Texto, EsCorrecta = o.EsCorrecta, ParTexto = o.ParTexto }).ToList(),
            });

        const decimal notaMaxima = 20;
        if (dto.NotaMinima <= 0 || dto.NotaMinima > notaMaxima) throw new AbrilException("La nota mínima debe estar entre 1 y 20.", 400);

        var snapshot = preguntas.Select(p => new SnapPregunta
        {
            Id = p.Id, Tipo = p.Tipo, Enunciado = p.Enunciado, Puntos = p.Puntos,
            Opciones = p.Opciones.OrderBy(o => o.Orden).Select(o => new SnapOpcion
            {
                Id = o.Id, Texto = o.Texto, EsCorrecta = o.EsCorrecta, ParTexto = o.ParTexto, Orden = o.Orden,
            }).ToList(),
        }).ToList();
        var json = JsonSerializer.Serialize(snapshot);
        var hash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(json)));

        var v = await _repository.PublicarEvaluacion(json, hash, dto.NotaMinima, notaMaxima, dto.DuracionMin, userId);
        return ToDto(v, snapshot.Count);
    }

    public async Task<List<IndEvaluacionVersionDto>> ListarEvaluaciones()
        => (await _repository.ListarEvaluaciones()).Select(e => ToDto(e, Deserializar(e.ContenidoJson).Count)).ToList();

    // ════════════════════════════════════════════════════════════════════════
    // Sesión (instructor)
    // ════════════════════════════════════════════════════════════════════════

    public Task<List<IndWorkerOpcionDto>> GetWorkersProyecto(int proyectoId) => _repository.GetWorkersParaInduccion(proyectoId);

    public async Task<IndSesionDetalleDto> CrearSesion(int userId, IndSesionCrearDto dto, string? ip)
    {
        var evaluacion = await _repository.GetEvaluacionVigente()
            ?? throw new AbrilException("Primero publica la evaluación (Catálogos → Evaluación).", 409);
        if (await _repository.GetProyectoNombre(dto.ProyectoId) is null)
            throw new AbrilException("Proyecto no encontrado.", 404);

        // Documentos: los elegidos, o todos los obligatorios activos. Cada uno debe tener una versión vigente.
        var documentos = await _repository.ListarDocumentos(soloActivos: true);
        var elegidos = dto.DocumentoIds is { Count: > 0 }
            ? documentos.Where(d => dto.DocumentoIds.Contains(d.Id)).ToList()
            : documentos.Where(d => d.Obligatorio).ToList();
        if (elegidos.Count == 0) throw new AbrilException("No hay documentos para entregar en esta inducción.", 400);

        var sinArchivo = elegidos.Where(d => d.Versiones.All(v => !v.Vigente)).Select(d => d.Codigo).ToList();
        if (sinArchivo.Count > 0)
            throw new AbrilException($"Estos documentos todavía no tienen archivo cargado: {string.Join(", ", sinArchivo)}.", 409);
        var versionIds = elegidos.Select(d => d.Versiones.First(v => v.Vigente).Id).ToList();

        var tipo = dto.Tipo is "InduccionHombreNuevo" or "ReInduccion" or "Actualizacion"
            ? dto.Tipo
            : throw new AbrilException("Tipo de sesión no válido.", 400);
        if (tipo == "Actualizacion" && (dto.DocumentoIds is not { Count: > 0 } || dto.WorkerIdsEsperados is not { Count: > 0 }))
            throw new AbrilException("Una actualización necesita los documentos a entregar y los trabajadores que deben recibirlos.", 400);

        // La actualización entrega solo documentos: no lleva temario.
        var temas = tipo == "Actualizacion" ? new List<SsIndTema>() : await _repository.ListarTemas(soloActivos: true);
        if (tipo != "Actualizacion" && dto.TemaIds is { Count: > 0 }) temas = temas.Where(t => dto.TemaIds.Contains(t.Id)).ToList();

        var instructorWorkerId = await _atsRepository.ResolverWorkerIdAsync(userId);
        var (nombre, cargo) = await _repository.GetNombreYCargo(instructorWorkerId);

        var fecha = dto.Fecha ?? DateOnly.FromDateTime(DateTime.Today);
        var sesion = new SsIndSesion
        {
            ProyectoId = dto.ProyectoId,
            Fecha = fecha,
            Tipo = tipo,
            Horas = dto.Horas <= 0 ? 2 : dto.Horas,
            InstructorWorkerId = instructorWorkerId,
            InstructorNombre = nombre,
            InstructorCargo = cargo,
            EvaluacionVersionId = evaluacion.Id,
            // Vence al terminar el día en Lima (UTC-5): una inducción es para esa jornada, no queda reutilizable.
            QrExpiraEn = fecha.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc).AddHours(5),
            CreatedByUserId = userId,
        };

        var creada = await _repository.CrearSesion(sesion, versionIds, temas, dto.WorkerIdsEsperados ?? [], userId, ip);
        return await GetSesion(creada.Id);
    }

    public async Task<IndSesionDetalleDto> GetSesion(int id)
    {
        var s = await _repository.GetSesion(id) ?? throw new AbrilException("Sesión no encontrada.", 404);
        var detalle = ToDetalle(s);

        var repetidas = await _repository.GetAsistenciasConSelfieRepetida(id);
        foreach (var a in detalle.Asistencias) a.SelfieRepetida = repetidas.Contains(a.Id);

        if (s.CerradaEn != null)
        {
            var sello = await _postCierre.GetSello(id);
            detalle.Sello = sello is null
                ? new IndSelloDto { Estado = "Pendiente" }
                : new IndSelloDto { Estado = sello.Estado, SelladoEn = sello.SelladoEn, Proveedor = sello.Proveedor, Intentos = sello.Intentos, UltimoError = sello.UltimoError };

            var respaldo = await _postCierre.GetRespaldo(id);
            detalle.Respaldo = respaldo is null
                ? new IndRespaldoDto { Estado = _respaldo.Configurado ? "Pendiente" : "NoConfigurado" }
                : new IndRespaldoDto { Estado = respaldo.Estado, Archivos = respaldo.Archivos, RespaldadoEn = respaldo.RespaldadoEn, UltimoError = respaldo.UltimoError };
        }
        return detalle;
    }

    /// <summary>Reintento manual del sello de tiempo (cuando la TSA falló demasiadas veces).</summary>
    public async Task<IndSelloDto> ReintentarSello(int sesionId)
    {
        var s = await _repository.GetSesion(sesionId) ?? throw new AbrilException("Sesión no encontrada.", 404);
        if (s.CerradaEn is null) throw new AbrilException("Solo se sella una inducción ya cerrada.", 409);

        await _postCierre.ReiniciarSello(sesionId);
        await _sello.SellarAsync(sesionId);
        var sello = await _postCierre.GetSello(sesionId);
        return sello is null
            ? new IndSelloDto { Estado = "Pendiente" }
            : new IndSelloDto { Estado = sello.Estado, SelladoEn = sello.SelladoEn, Proveedor = sello.Proveedor, Intentos = sello.Intentos, UltimoError = sello.UltimoError };
    }

    /// <summary>Reintento manual del respaldo.</summary>
    public async Task<IndRespaldoDto> ReintentarRespaldo(int sesionId)
    {
        if (!_respaldo.Configurado)
            throw new AbrilException("No hay una cuenta de respaldo configurada (Induccion:Respaldo:ConnectionString).", 409);
        var s = await _repository.GetSesion(sesionId) ?? throw new AbrilException("Sesión no encontrada.", 404);
        if (s.CerradaEn is null) throw new AbrilException("Solo se respalda una inducción ya cerrada.", 409);

        await _postCierre.GetOCrearRespaldo(sesionId);
        await _postCierre.ReiniciarRespaldo(sesionId);
        await _respaldo.RespaldarAsync(sesionId);
        var r = await _postCierre.GetRespaldo(sesionId);
        return r is null
            ? new IndRespaldoDto { Estado = "Pendiente" }
            : new IndRespaldoDto { Estado = r.Estado, Archivos = r.Archivos, RespaldadoEn = r.RespaldadoEn, UltimoError = r.UltimoError };
    }

    /// <summary>El token RFC 3161 (.tsr) para entregárselo a un auditor: se verifica con OpenSSL sin depender de este sistema.</summary>
    public async Task<(byte[] Bytes, string Nombre)> DescargarSello(int sesionId)
    {
        var sello = await _postCierre.GetSello(sesionId);
        if (sello?.Token is null) throw new AbrilException("Esta sesión todavía no tiene sello de tiempo.", 404);
        var s = await _repository.GetSesion(sesionId);
        return (sello.Token, $"sello-{s?.Codigo ?? sesionId.ToString()}.tsr");
    }

    public Task<IndSesionListaDto> ListarSesiones(IndSesionFiltroDto filtro) => _repository.ListarSesiones(filtro);

    /// <summary>Cierra la sesión: firma del instructor + acta PDF con hash. Desde acá la sesión es inmutable.</summary>
    public async Task CerrarSesion(int id, int userId, IndCerrarSesionDto body, string? ip)
    {
        var s = await _repository.GetSesion(id) ?? throw new AbrilException("Sesión no encontrada.", 404);
        if (s.Estado != "Abierta") throw new AbrilException("Esta sesión ya fue cerrada o anulada.", 409);
        if (!s.Asistencias.Any(a => a.Estado == "Firmado"))
            throw new AbrilException("No se puede cerrar una inducción sin ningún trabajador firmado.", 409);

        // Revisión de selfie obligatoria: cada firma debe haber sido validada (o rechazada) por una persona antes de
        // que el acta quede inmutable. Es la defensa contra «firmó otro por él».
        var sinValidar = s.Asistencias.Count(a => a.Estado == "Firmado" && a.ValidadoEn == null);
        if (sinValidar > 0)
            throw new AbrilException(
                $"Faltan validar {sinValidar} {(sinValidar == 1 ? "selfie" : "selfies")}. Revísalas (validar o rechazar) antes de cerrar la inducción.", 409);

        var firmaBytes = FirmaImagenHelper.DecodePng(body.FirmaBase64);
        var firmaHash = Convert.ToHexString(SHA256.HashData(firmaBytes));
        var firmaUrl = await SubirBytes("instructor", s.InstructorWorkerId, firmaBytes, "png");

        // El acta se arma con la sesión TAL COMO queda al cerrar (solo se cambia la firma del instructor).
        var detalle = ToDetalle(s);
        detalle.Estado = "Cerrada";
        detalle.CerradaEn = DateTime.UtcNow;

        var datos = new ActaInduccionDatos
        {
            Sesion = detalle,
            DeclaracionVersion = DeclaracionVersion,
            DeclaracionTexto = DeclaracionTexto,
            Logo = await LeerLogo(),
            FirmaInstructor = firmaBytes,
        };
        foreach (var a in s.Asistencias.Where(a => a.Estado == "Firmado"))
        {
            datos.FirmasTrabajadores[a.Id] = a.FirmaUrl != null ? await DescargarBytes(a.FirmaUrl) : null;
            datos.SelfiesTrabajadores[a.Id] = a.SelfieUrl != null ? await DescargarBytes(a.SelfieUrl) : null;
        }

        // Igual que ATS: el código del QR es un hash ESTABLE del contenido firmado (no del PDF, que lo lleva impreso
        // y sería circular). Cambiar un trabajador, una firma o la sesión cambia este código.
        var hashVerificacion = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(
            $"{s.Id}|{s.Codigo}|{firmaHash}|{string.Join(",", s.Asistencias.Where(a => a.Estado == "Firmado").OrderBy(a => a.Id).Select(a => a.HashVerificacion))}")));
        var baseUrl = _configuration["Frontend:BaseUrl"]?.TrimEnd('/') ?? "https://intranet.abril.pe";
        datos.VerificacionUrl = $"{baseUrl}/induccion-verificar/{s.Id}?hash={hashVerificacion[..12]}";
        detalle.ActaPdfHash = hashVerificacion; // para que el pie del PDF imprima el mismo código que guarda la BD

        var pdf = _actaPdf.Generar(datos);
        var pdfHash = hashVerificacion;
        var pdfSha256 = Convert.ToHexString(SHA256.HashData(pdf)); // hash REAL del archivo: es lo que se sella
        var container = _containerResolver.GetAtsContainerName();
        using var stream = new MemoryStream(pdf);
        var urls = await _fileStorageService.UploadFilesAsync([(stream, $"induccion_acta_{s.Id}_{DateTime.UtcNow:yyyyMMddHHmmssfff}.pdf")], container);

        await _repository.CerrarSesion(id, firmaUrl, firmaHash, urls[0], pdfHash, pdfSha256, userId, ip);

        // Sello de tiempo externo: un intento inmediato, con tope de 12 s para no hacer esperar al instructor. Si la TSA
        // no responde, la sesión igual queda cerrada y el segundo plano reintenta (ver InduccionPostCierreService).
        try
        {
            using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(12));
            await _sello.SellarAsync(id, limite.Token);
        }
        catch
        {
            // best-effort: no debe impedir ni deshacer el cierre
        }
    }

    public async Task AnularSesion(int id, int userId, string motivo, string? ip)
    {
        if (string.IsNullOrWhiteSpace(motivo)) throw new AbrilException("El motivo de anulación es obligatorio.", 400);
        await _repository.AnularSesion(id, motivo.Trim(), userId, ip);
    }

    public async Task ValidarAsistencia(int sesionId, int asistenciaId, int userId, string? ip)
    {
        await ExigirAsistenciaEnSesionAbierta(sesionId, asistenciaId);
        await _repository.ValidarAsistencia(asistenciaId, userId, ip);
    }

    public async Task RechazarAsistencia(int sesionId, int asistenciaId, int userId, string motivo, string? ip)
    {
        if (string.IsNullOrWhiteSpace(motivo)) throw new AbrilException("Indica el motivo del rechazo.", 400);
        await ExigirAsistenciaEnSesionAbierta(sesionId, asistenciaId);
        await _repository.RechazarAsistencia(asistenciaId, motivo.Trim(), userId, ip);
    }

    private async Task ExigirAsistenciaEnSesionAbierta(int sesionId, int asistenciaId)
    {
        var s = await _repository.GetSesion(sesionId) ?? throw new AbrilException("Sesión no encontrada.", 404);
        if (s.Estado != "Abierta") throw new AbrilException("La sesión ya no admite cambios.", 409);
        if (!s.Asistencias.Any(a => a.Id == asistenciaId)) throw new AbrilException("La asistencia no pertenece a esta sesión.", 404);
    }

    // ════════════════════════════════════════════════════════════════════════
    // Público (trabajador)
    // ════════════════════════════════════════════════════════════════════════

    public async Task<IndPublicoResumenDto> GetResumenPublico(Guid token)
    {
        var s = await _repository.GetSesionPorToken(token);
        if (s is null)
            return new IndPublicoResumenDto { Valido = false, MotivoInvalido = "Este enlace no corresponde a ninguna inducción." };
        var motivo = MotivoNoDisponible(s);
        if (motivo != null)
            return new IndPublicoResumenDto { Valido = false, MotivoInvalido = motivo };

        var det = ToDetalle(s);
        return new IndPublicoResumenDto
        {
            Valido = true,
            Codigo = s.Codigo,
            ProyectoNombre = s.Proyecto?.ProjectDescription,
            Fecha = s.Fecha,
            Horas = s.Horas,
            InstructorNombre = s.InstructorNombre,
            NotaMinima = s.EvaluacionVersion?.NotaMinima ?? 14,
            DuracionMin = s.EvaluacionVersion?.DuracionMin ?? 15,
            DeclaracionVersion = DeclaracionVersion,
            DeclaracionTexto = DeclaracionTexto,
            Tipo = s.Tipo,
            RequiereEvaluacion = s.Tipo != "Actualizacion",
            Documentos = det.Documentos,
            Temas = det.Temas,
        };
    }

    public async Task<List<IndWorkerOpcionDto>> GetTrabajadoresPublico(Guid token)
    {
        var s = await SesionAbiertaPorToken(token);
        var roster = await _repository.GetWorkersParaInduccion(s.ProyectoId);
        var firmaron = s.Asistencias.Where(a => a.Estado == "Firmado").Select(a => a.WorkerId).ToHashSet();
        // Quien fue marcado como esperado pero ya no figura vinculado al proyecto igual debe poder entrar.
        foreach (var a in s.Asistencias.Where(a => roster.All(r => r.WorkerId != a.WorkerId)))
            roster.Add(new IndWorkerOpcionDto { WorkerId = a.WorkerId, Nombre = a.NombreCompleto ?? $"Trabajador {a.WorkerId}" });
        foreach (var r in roster) r.YaFirmo = firmaron.Contains(r.WorkerId);
        return roster.OrderBy(r => r.Nombre).ToList();
    }

    public async Task<IndEstadoTrabajadorDto> GetEstadoPublico(Guid token, IndIdentidadDto body)
    {
        var s = await SesionAbiertaPorToken(token);
        await ValidarIdentidad(s, body);
        var a = s.Asistencias.FirstOrDefault(x => x.WorkerId == body.WorkerId);
        return new IndEstadoTrabajadorDto
        {
            AsistenciaId = a?.Id,
            Firmado = a?.Estado == "Firmado",
            // En una Actualización de documentos no hay evaluación: recibir y firmar es todo.
            Aprobado = (a?.Aprobado ?? false) || s.Tipo == "Actualizacion",
            Intentos = a?.Intentos ?? 0,
            Nota = a?.Nota,
            DocumentosRecibidos = a?.Documentos.Select(d => d.DocumentoVersionId).ToList() ?? [],
        };
    }

    public async Task RegistrarDocumentoRecibido(Guid token, IndDocumentoRecibidoDto body)
    {
        var s = await SesionAbiertaPorToken(token);
        await ValidarIdentidad(s, body);
        if (s.Documentos.All(d => d.DocumentoVersionId != body.DocumentoVersionId))
            throw new AbrilException("Ese documento no forma parte de esta inducción.", 400);

        var a = await _repository.GetOCrearAsistencia(s.Id, body.WorkerId);
        if (a.Estado == "Firmado") throw new AbrilException("Ya firmaste esta inducción.", 409);
        await _repository.RegistrarDocumentoRecibido(a.Id, body.DocumentoVersionId, body.SegundosAbierto);
    }

    public async Task<IndEvaluacionPublicaDto> GetEvaluacionPublica(Guid token, IndIdentidadDto body)
    {
        var s = await SesionAbiertaPorToken(token);
        if (s.Tipo == "Actualizacion") throw new AbrilException("Esta actualización de documentos no lleva evaluación.", 409);
        await ValidarIdentidad(s, body);
        var a = await _repository.GetOCrearAsistencia(s.Id, body.WorkerId);
        if (a.Estado == "Firmado") throw new AbrilException("Ya firmaste esta inducción.", 409);
        await ExigirDocumentosRecibidos(s, a.Id);

        var ev = s.EvaluacionVersion ?? throw new AbrilException("La sesión no tiene evaluación.", 409);
        var preguntas = Deserializar(ev.ContenidoJson);
        return new IndEvaluacionPublicaDto
        {
            EvaluacionVersionId = ev.Id,
            NotaMinima = ev.NotaMinima,
            NotaMaxima = ev.NotaMaxima,
            DuracionMin = ev.DuracionMin,
            // Sin claves: nunca sale EsCorrecta ni el ParTexto emparejado con su ítem.
            Preguntas = preguntas.Select(p => new IndPreguntaPublicaDto
            {
                Id = p.Id,
                Tipo = p.Tipo,
                Enunciado = p.Enunciado,
                Opciones = p.Tipo == "VF" ? [] : p.Opciones.Select(o => new IndOpcionPublicaDto { Id = o.Id, Texto = o.Texto }).ToList(),
                Pares = p.Tipo == "Relacionar"
                    ? p.Opciones.Select(o => o.ParTexto ?? string.Empty).OrderBy(_ => Random.Shared.Next()).ToList()
                    : [],
            }).ToList(),
        };
    }

    public async Task<IndResultadoEvaluacionDto> ResponderEvaluacion(Guid token, IndResponderEvaluacionDto body)
    {
        var s = await SesionAbiertaPorToken(token);
        if (s.Tipo == "Actualizacion") throw new AbrilException("Esta actualización de documentos no lleva evaluación.", 409);
        await ValidarIdentidad(s, body);
        var ev = s.EvaluacionVersion ?? throw new AbrilException("La sesión no tiene evaluación.", 409);
        if (body.EvaluacionVersionId != ev.Id) throw new AbrilException("La evaluación cambió, recarga la página.", 409);

        var a = await _repository.GetOCrearAsistencia(s.Id, body.WorkerId);
        if (a.Estado == "Firmado") throw new AbrilException("Ya firmaste esta inducción.", 409);
        await ExigirDocumentosRecibidos(s, a.Id);

        var nota = Calificar(Deserializar(ev.ContenidoJson), body.Respuestas, ev.NotaMaxima);
        var aprobado = nota >= ev.NotaMinima;
        await _repository.GuardarIntento(a.Id, ev.Id, JsonSerializer.Serialize(body.Respuestas), nota, aprobado, body.IniciadoEn?.ToUniversalTime());

        return new IndResultadoEvaluacionDto
        {
            Nota = nota,
            NotaMinima = ev.NotaMinima,
            NotaMaxima = ev.NotaMaxima,
            Aprobado = aprobado,
            Intentos = a.Intentos + 1,
        };
    }

    public async Task Firmar(Guid token, IndFirmarDto body, string? ip, string? userAgent)
    {
        var s = await SesionAbiertaPorToken(token);
        await ValidarIdentidad(s, body);

        if (!body.AceptaDeclaracion)
            throw new AbrilException("Debes aceptar la declaración y compromiso para firmar.", 400);
        if (string.IsNullOrWhiteSpace(body.SelfieBase64))
            throw new AbrilException("La selfie es obligatoria para firmar.", 400);
        if (string.IsNullOrWhiteSpace(body.FirmaBase64))
            throw new AbrilException("Debes dibujar tu firma.", 400);

        var a = await _repository.GetOCrearAsistencia(s.Id, body.WorkerId);
        if (a.Estado == "Firmado") throw new AbrilException("Ya firmaste esta inducción.", 409);
        await ExigirDocumentosRecibidos(s, a.Id);
        if (s.Tipo != "Actualizacion" && !a.Aprobado)
            throw new AbrilException($"Debes aprobar la evaluación (nota mínima {s.EvaluacionVersion?.NotaMinima:0.##}) antes de firmar.", 409);

        if (!await _atsRepository.TieneConsentimiento(body.WorkerId))
        {
            if (!body.AceptaConsentimiento)
                throw new AbrilException("Debes aceptar el uso de tu imagen y geolocalización para firmar.", 400);
            await _atsRepository.RegistrarConsentimiento(body.WorkerId, ip);
        }

        var firmaBytes = FirmaImagenHelper.DecodePng(body.FirmaBase64);
        var (selfieUrl, selfieHash) = await SubirImagenConHash("selfie", body.WorkerId, body.SelfieBase64);
        var selfieDuplicada = await _repository.ExisteSelfieHash(selfieHash);
        var firmaHash = Convert.ToHexString(SHA256.HashData(firmaBytes));
        var firmaUrl = await SubirBytes("firma", body.WorkerId, firmaBytes, "png");

        await _repository.Firmar(a.Id, selfieUrl, selfieHash, firmaUrl, firmaHash, DateTime.UtcNow, body, ip, userAgent, DeclaracionVersion);

        if (selfieDuplicada)
            await _repository.AgregarAudit(s.Id, "SelfieDuplicada", null, ip,
                $"worker={body.WorkerId};la selfie ya se usó en otra firma — revisar manualmente");
    }

    public async Task<IndVerificacionDto> Verificar(int sesionId, string? hash)
    {
        var s = await _repository.GetSesion(sesionId);
        if (s is null) return new IndVerificacionDto { Valido = false, Motivo = "No existe esta acta." };
        if (s.Estado != "Cerrada") return new IndVerificacionDto { Valido = false, Motivo = s.Estado == "Anulada" ? "Esta inducción fue anulada." : "Esta inducción aún no está cerrada." };
        if (!string.IsNullOrWhiteSpace(hash) && (s.ActaPdfHash is null || !s.ActaPdfHash.StartsWith(hash, StringComparison.OrdinalIgnoreCase)))
            return new IndVerificacionDto { Valido = false, Motivo = "El código del acta no coincide: el documento pudo haber sido alterado." };

        var sello = await _postCierre.GetSello(sesionId);
        return new IndVerificacionDto
        {
            Valido = true,
            Codigo = s.Codigo,
            ProyectoNombre = s.Proyecto?.ProjectDescription,
            Fecha = s.Fecha,
            InstructorNombre = s.InstructorNombre,
            ActaPdfHash = s.ActaPdfHash,
            CerradaEn = s.CerradaEn,
            SelladoEn = sello?.Estado == "Sellado" ? sello.SelladoEn : null,
            SelloProveedor = sello?.Estado == "Sellado" ? sello.Proveedor : null,
            SelloEmisor = sello?.Estado == "Sellado" ? sello.EmisorTsa : null,
            Trabajadores = s.Asistencias.Where(a => a.Estado == "Firmado").OrderBy(a => a.NombreCompleto)
                .Select(a => new IndVerificacionTrabajadorDto
                {
                    NombreCompleto = a.NombreCompleto,
                    DocumentoIdentidadEnmascarado = EnmascararDni(a.DocumentoIdentidad),
                    Nota = a.Nota,
                    Aprobado = a.Aprobado,
                    HoraServidorFirma = a.HoraServidorFirma,
                    HashVerificacion = a.HashVerificacion,
                }).ToList(),
        };
    }

    // ════════════════════════════════════════════════════════════════════════
    // Reglas internas
    // ════════════════════════════════════════════════════════════════════════

    private static string? MotivoNoDisponible(SsIndSesion s)
    {
        if (s.Estado == "Anulada") return "Esta inducción fue anulada.";
        if (s.Estado != "Abierta") return "Esta inducción ya fue cerrada por el instructor.";
        if (s.QrExpiraEn < DateTime.UtcNow) return "Este enlace venció — la inducción era para otra jornada.";
        return null;
    }

    private async Task<SsIndSesion> SesionAbiertaPorToken(Guid token)
    {
        var s = await _repository.GetSesionPorToken(token) ?? throw new AbrilException("Enlace inválido.", 404);
        var motivo = MotivoNoDisponible(s);
        if (motivo != null) throw new AbrilException(motivo, 409);
        return s;
    }

    private async Task ValidarIdentidad(SsIndSesion s, IndIdentidadDto body)
    {
        if (!await _atsRepository.DniCoincide(body.WorkerId, body.DniConfirmacion))
            throw new AbrilException("Los dígitos de DNI no coinciden con el trabajador seleccionado.", 400);

        var esperado = s.Asistencias.Any(a => a.WorkerId == body.WorkerId);
        if (!esperado && (await _repository.GetWorkersParaInduccion(s.ProyectoId)).All(w => w.WorkerId != body.WorkerId))
            throw new AbrilException("Este trabajador no está habilitado en este proyecto.", 403);
    }

    private async Task ExigirDocumentosRecibidos(SsIndSesion s, int asistenciaId)
    {
        var recibidos = (await _repository.GetDocumentosRecibidos(asistenciaId)).ToHashSet();
        if (s.Documentos.Any(d => !recibidos.Contains(d.DocumentoVersionId)))
            throw new AbrilException("Primero confirma la recepción de todos los documentos.", 409);
    }

    private static void ValidarPregunta(IndPreguntaGuardarDto dto)
    {
        if (!TiposPregunta.Contains(dto.Tipo)) throw new AbrilException("Tipo de pregunta no válido.", 400);
        if (string.IsNullOrWhiteSpace(dto.Enunciado)) throw new AbrilException("El enunciado es obligatorio.", 400);
        if (dto.Puntos <= 0) throw new AbrilException("Los puntos deben ser mayores a cero.", 400);
        if (dto.Opciones.Any(o => string.IsNullOrWhiteSpace(o.Texto))) throw new AbrilException("Hay opciones vacías.", 400);

        switch (dto.Tipo)
        {
            case "VF" when dto.Opciones.Count != 1:
                throw new AbrilException("Verdadero/Falso lleva una sola opción (marca si la afirmación es verdadera).", 400);
            case "Alternativa" when dto.Opciones.Count < 2 || dto.Opciones.Count(o => o.EsCorrecta) != 1:
                throw new AbrilException("Alternativa necesita al menos 2 opciones y exactamente 1 correcta.", 400);
            case "Relacionar" when dto.Opciones.Count < 2 || dto.Opciones.Any(o => string.IsNullOrWhiteSpace(o.ParTexto)):
                throw new AbrilException("Relacionar necesita al menos 2 ítems, cada uno con su pareja.", 400);
        }
    }

    /// <summary>Nota sobre NotaMaxima, proporcional a los puntos. Pregunta sin responder = incorrecta; Relacionar es todo o nada.</summary>
    private static decimal Calificar(List<SnapPregunta> preguntas, List<IndRespuestaDto> respuestas, decimal notaMaxima)
    {
        var total = preguntas.Sum(p => p.Puntos);
        if (total <= 0) return 0;

        decimal obtenido = 0;
        foreach (var p in preguntas)
        {
            var r = respuestas.FirstOrDefault(x => x.PreguntaId == p.Id);
            if (r is null) continue;
            var ok = p.Tipo switch
            {
                "VF" => r.Verdadero.HasValue && p.Opciones.Count > 0 && r.Verdadero.Value == p.Opciones[0].EsCorrecta,
                "Alternativa" => r.OpcionId.HasValue && p.Opciones.Any(o => o.EsCorrecta && o.Id == r.OpcionId.Value),
                "Relacionar" => r.Pares is { Count: > 0 } && r.Pares.Count == p.Opciones.Count && p.Opciones.All(o =>
                    r.Pares.Any(x => Norm(x.Texto) == Norm(o.Texto) && Norm(x.ParTexto) == Norm(o.ParTexto))),
                _ => false,
            };
            if (ok) obtenido += p.Puntos;
        }
        return Math.Round(obtenido / total * notaMaxima, 2);
    }

    private static string Norm(string? s) => (s ?? string.Empty).Trim().ToLowerInvariant();

    private static List<SnapPregunta> Deserializar(string json)
        => JsonSerializer.Deserialize<List<SnapPregunta>>(json) ?? [];

    private static string? EnmascararDni(string? dni)
        => string.IsNullOrWhiteSpace(dni) || dni.Length < 5 ? dni : $"{dni[..2]}****{dni[^2..]}";

    // ── Archivos ────────────────────────────────────────────────────────────

    private async Task<(string Url, string Hash)> SubirImagenConHash(string prefijo, int workerId, string base64)
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

        return (await SubirBytes(prefijo, workerId, bytes, "jpg"), Convert.ToHexString(SHA256.HashData(bytes)));
    }

    private async Task<string> SubirBytes(string prefijo, int workerId, byte[] bytes, string ext)
    {
        var container = _containerResolver.GetAtsContainerName();
        using var stream = new MemoryStream(bytes);
        var fileName = $"induccion_{prefijo}_{workerId}_{DateTime.UtcNow:yyyyMMddHHmmssfff}.{ext}";
        var urls = await _fileStorageService.UploadFilesAsync([(stream, fileName)], container);
        return urls[0];
    }

    private static async Task<byte[]?> DescargarBytes(string url)
    {
        try { return await Http.GetByteArrayAsync(url); }
        catch { return null; } // una imagen que no baja no debe impedir cerrar la sesión
    }

    private async Task<byte[]?> LeerLogo()
    {
        var path = _logoPaths.FirstOrDefault(File.Exists);
        return path == null ? null : await File.ReadAllBytesAsync(path);
    }

    // ── Mapeos ──────────────────────────────────────────────────────────────

    private static IndDocumentoDto ToDto(SsIndDocumento d)
    {
        var v = d.Versiones.FirstOrDefault(x => x.Vigente);
        return new IndDocumentoDto
        {
            Id = d.Id, Codigo = d.Codigo, Nombre = d.Nombre, Descripcion = d.Descripcion, Tipo = d.Tipo,
            Obligatorio = d.Obligatorio, Orden = d.Orden, Activo = d.Activo,
            VersionVigente = v == null ? null : ToDto(v),
        };
    }

    private static IndDocumentoVersionDto ToDto(SsIndDocumentoVersion v) => new()
    {
        Id = v.Id, DocumentoId = v.DocumentoId, Version = v.Version, ArchivoUrl = v.ArchivoUrl, ArchivoHash = v.ArchivoHash,
        EsCambioMaterial = v.EsCambioMaterial, NotaCambio = v.NotaCambio, Vigente = v.Vigente, PublicadoEn = v.PublicadoEn,
    };

    private static IndPreguntaDto ToDto(SsIndPregunta p) => new()
    {
        Id = p.Id, Tipo = p.Tipo, Enunciado = p.Enunciado, Puntos = p.Puntos, Orden = p.Orden, Activo = p.Activo,
        Opciones = p.Opciones.OrderBy(o => o.Orden).Select(o => new IndOpcionDto
        {
            Id = o.Id, Texto = o.Texto, EsCorrecta = o.EsCorrecta, ParTexto = o.ParTexto, Orden = o.Orden,
        }).ToList(),
    };

    private static IndEvaluacionVersionDto ToDto(SsIndEvaluacionVersion e, int totalPreguntas) => new()
    {
        Id = e.Id, Version = e.Version, NotaMinima = e.NotaMinima, NotaMaxima = e.NotaMaxima, DuracionMin = e.DuracionMin,
        TotalPreguntas = totalPreguntas, Vigente = e.Vigente, PublicadoEn = e.PublicadoEn,
    };

    private static IndSesionDetalleDto ToDetalle(SsIndSesion s) => new()
    {
        Id = s.Id,
        Codigo = s.Codigo,
        ProyectoId = s.ProyectoId,
        ProyectoNombre = s.Proyecto?.ProjectDescription,
        Fecha = s.Fecha,
        Horas = s.Horas,
        Estado = s.Estado,
        InstructorNombre = s.InstructorNombre,
        InstructorCargo = s.InstructorCargo,
        Tipo = s.Tipo,
        QrToken = s.QrToken,
        QrExpiraEn = s.QrExpiraEn,
        EvaluacionVersion = s.EvaluacionVersion?.Version ?? 0,
        NotaMinima = s.EvaluacionVersion?.NotaMinima ?? 14,
        CerradaEn = s.CerradaEn,
        ActaPdfUrl = s.ActaPdfUrl,
        ActaPdfHash = s.ActaPdfHash,
        ActaPdfSha256 = s.ActaPdfSha256,
        AnuladoMotivo = s.AnuladoMotivo,
        Documentos = s.Documentos.Where(d => d.DocumentoVersion != null)
            .OrderBy(d => d.DocumentoVersion!.Documento?.Orden).ThenBy(d => d.Id)
            .Select(d => new IndSesionDocumentoDto
            {
                DocumentoVersionId = d.DocumentoVersionId,
                DocumentoId = d.DocumentoVersion!.DocumentoId,
                Codigo = d.DocumentoVersion.Documento?.Codigo ?? string.Empty,
                Nombre = d.DocumentoVersion.Documento?.Nombre ?? string.Empty,
                Version = d.DocumentoVersion.Version,
                ArchivoUrl = d.DocumentoVersion.ArchivoUrl,
                ArchivoHash = d.DocumentoVersion.ArchivoHash,
            }).ToList(),
        Temas = s.Temas.OrderBy(t => t.Orden).Select(t => new IndSesionTemaDto { Orden = t.Orden, Descripcion = t.Descripcion }).ToList(),
        Asistencias = s.Asistencias.OrderBy(a => a.NombreCompleto).Select(a => new IndAsistenciaDto
        {
            Id = a.Id, WorkerId = a.WorkerId, NombreCompleto = a.NombreCompleto, DocumentoIdentidad = a.DocumentoIdentidad,
            Categoria = a.Categoria, ContratistaNombre = a.ContratistaNombre, Estado = a.Estado, Nota = a.Nota,
            Aprobado = a.Aprobado, Intentos = a.Intentos, HoraServidorFirma = a.HoraServidorFirma,
            SelfieUrl = a.SelfieUrl, FirmaUrl = a.FirmaUrl, OrigenOffline = a.OrigenOffline,
            DocumentosRecibidos = a.Documentos.Count, Validado = a.ValidadoEn != null && a.Estado == "Firmado",
            RechazoMotivo = a.RechazoMotivo,
        }).ToList(),
    };

    // ── Snapshot de la evaluación (lo que se guarda en contenido_json) ──────

    private class SnapPregunta
    {
        public int Id { get; set; }
        public string Tipo { get; set; } = string.Empty;
        public string Enunciado { get; set; } = string.Empty;
        public decimal Puntos { get; set; }
        public List<SnapOpcion> Opciones { get; set; } = [];
    }

    private class SnapOpcion
    {
        public int Id { get; set; }
        public string Texto { get; set; } = string.Empty;
        public bool EsCorrecta { get; set; }
        public string? ParTexto { get; set; }
        public int Orden { get; set; }
    }
}
