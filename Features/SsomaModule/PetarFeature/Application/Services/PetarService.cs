using System.Security.Cryptography;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.SsomaModule.AtsFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.SsomaModule.PetarFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.PetarFeature.Application.Interfaces;
using Abril_Backend.Features.SsomaModule.PetarFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.SsomaModule.PetarFeature.Infrastructure.Models;
using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Shared.Helpers;
using Abril_Backend.Shared.Services.Notificaciones.Dtos;
using Abril_Backend.Shared.Services.Notificaciones.Interfaces;
using Abril_Backend.Shared.Services.Residentes.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Abril_Backend.Features.SsomaModule.PetarFeature.Application.Services;

public class PetarService : IPetarService
{
    private readonly IPetarRepository _repository;
    // Reusa la resolución de Residente/SSOMA por proyecto y utilidades de identidad ya construidas
    // para ATS — un PETAR nace de un ATS y comparte exactamente los mismos responsables.
    private readonly IAtsRepository _atsRepository;
    private readonly IFileStorageService _fileStorageService;
    private readonly IStorageContainerResolver _containerResolver;
    private readonly IConfiguration _configuration;
    private readonly INotificacionesService _notificacionesService;
    // Si quien llama es el Residente del proyecto: por persona, igual que en ATS.
    private readonly IResidenteProyectoResolver _residentes;
    private readonly string[] _logoPaths;

    public PetarService(
        IPetarRepository repository,
        IAtsRepository atsRepository,
        IFileStorageService fileStorageService,
        IStorageContainerResolver containerResolver,
        IConfiguration configuration,
        INotificacionesService notificacionesService,
        IResidenteProyectoResolver residentes,
        IWebHostEnvironment env)
    {
        _repository = repository;
        _atsRepository = atsRepository;
        _fileStorageService = fileStorageService;
        _containerResolver = containerResolver;
        _configuration = configuration;
        _notificacionesService = notificacionesService;
        _residentes = residentes;
        _logoPaths =
        [
            Path.Combine(env.WebRootPath, "images", "abril-logo.png"),
            Path.Combine(env.WebRootPath, "images", "logo-abril.jpg"),
            Path.Combine(env.ContentRootPath, "Templates", "logo-abril.jpg"),
        ];
    }

    public Task<int> ResolverWorkerId(int userId) => _atsRepository.ResolverWorkerIdAsync(userId);

    public Task<List<PetarTipoDto>> GetTiposCatalogo() => _repository.GetTiposConItems();

    public async Task<PetarInitDto> GetInit(int atsId)
    {
        var (_, lugar, actividad, estadoAts) = await _repository.GetDatosAts(atsId);
        if (estadoAts != "Firmado")
            throw new AbrilException("El ATS debe estar firmado antes de generar un PETAR.", 409);

        return new PetarInitDto
        {
            AtsId = atsId,
            AtsLugar = lugar,
            AtsActividad = actividad,
            Tipos = await _repository.GetTiposConItems(),
        };
    }

    public async Task<int> Crear(int workerId, PetarGuardarRequestDto dto)
    {
        Validar(dto);
        return await _repository.Crear(workerId, dto);
    }

    public async Task Editar(int id, int workerId, PetarGuardarRequestDto dto)
    {
        Validar(dto);
        await _repository.Editar(id, workerId, dto);
    }

    private static void Validar(PetarGuardarRequestDto dto)
    {
        if (dto.AtsId <= 0)
            throw new AbrilException("Falta el ATS de origen.", 400);
        if (dto.TipoId <= 0)
            throw new AbrilException("Selecciona el tipo de trabajo de alto riesgo.", 400);
        if (string.IsNullOrWhiteSpace(dto.DescripcionTrabajo))
            throw new AbrilException("Describe el trabajo a realizar.", 400);
        if (dto.Respuestas.Count == 0)
            throw new AbrilException("Completa el checklist de verificación.", 400);
        if (dto.Respuestas.Any(r => r.Respuesta == "NO"))
            throw new AbrilException(
                "Hay ítems del checklist marcados como NO cumplidos — el trabajo no puede iniciar hasta corregirlos.", 400);
    }

    public async Task<PetarResponseDto> GetPorId(int id, int callerUserId, int workerId, bool esAdmin)
    {
        var petar = await _repository.GetPorId(id) ?? throw new AbrilException("PETAR no encontrado.", 404);
        var responsables = await _atsRepository.GetResponsables(petar.ProyectoId);
        var callerEmail = await _atsRepository.GetEmailCorporativoWorker(workerId);

        var esResidente = await EsSupervisorDelProyecto(callerUserId, workerId, petar.ProyectoId);
        var esSsoma = callerEmail != null && responsables.SsomaEmails.Contains(callerEmail, StringComparer.OrdinalIgnoreCase);

        var esCapataz = await _atsRepository.EsCapatazDeProyecto(workerId, petar.ProyectoId);
        if (!esAdmin && petar.WorkerId != workerId && !esResidente && !esSsoma && !esCapataz)
            throw new AbrilException("Este PETAR no te pertenece.", 403);

        MarcarPermisos(petar, workerId, esResidente || esAdmin, esSsoma || esAdmin, esCapataz || esAdmin, await _atsRepository.EsObreroDeObra(petar.WorkerId));
        return petar;
    }

    /// <summary>Quién firma como «Supervisor» del PETAR: el Residente de la obra o el Ing./Arq. de Producción
    /// de esa obra (igual que el «Autoriza» del ATS).</summary>
    private async Task<bool> EsSupervisorDelProyecto(int callerUserId, int workerId, int proyectoId)
        => await _residentes.EsResidenteDelProyectoAsync(callerUserId, proyectoId)
           || await _atsRepository.EsProduccionDeProyecto(workerId, proyectoId);

    public async Task<PetarListResponseDto> Listar(PetarFiltroDto filtro, int callerUserId, int workerId, bool esAdmin)
    {
        var res = await _repository.Listar(filtro);
        var callerEmail = await _atsRepository.GetEmailCorporativoWorker(workerId);
        var obrasDondeEsResidente = (await _residentes.ProyectosDelResidenteAsync(callerUserId)).ToHashSet();

        var cache = new Dictionary<int, Abril_Backend.Features.SsomaModule.AtsFeature.Application.Dtos.AtsResponsablesDto>();
        var capatazPorProyecto = new Dictionary<int, bool>();
        var obreroPorWorker = new Dictionary<int, bool>();
        foreach (var petar in res.Data)
        {
            if (!cache.TryGetValue(petar.ProyectoId, out var responsables))
            {
                responsables = await _atsRepository.GetResponsables(petar.ProyectoId);
                cache[petar.ProyectoId] = responsables;
            }

            var esResidente = obrasDondeEsResidente.Contains(petar.ProyectoId)
                || await _atsRepository.EsProduccionDeProyecto(workerId, petar.ProyectoId);
            var esSsoma = callerEmail != null && responsables.SsomaEmails.Contains(callerEmail, StringComparer.OrdinalIgnoreCase);
            if (!capatazPorProyecto.TryGetValue(petar.ProyectoId, out var esCapataz))
                capatazPorProyecto[petar.ProyectoId] = esCapataz = await _atsRepository.EsCapatazDeProyecto(workerId, petar.ProyectoId);
            if (!obreroPorWorker.TryGetValue(petar.WorkerId, out var esObrero))
                obreroPorWorker[petar.WorkerId] = esObrero = await _atsRepository.EsObreroDeObra(petar.WorkerId);
            MarcarPermisos(petar, workerId, esResidente || esAdmin, esSsoma || esAdmin, esCapataz || esAdmin, esObrero);
        }

        return res;
    }

    private static void MarcarPermisos(PetarResponseDto p, int workerId, bool esResidente, bool esSsoma, bool esCapataz, bool requiereCapataz)
    {
        // Cadena: Capataz (solo si el ejecutante es obrero) → Supervisor (Residente/Producción) → SSOMA.
        p.RequiereCapataz = requiereCapataz;
        p.PuedeFirmarCapataz = esCapataz && requiereCapataz && p.FirmaUrl != null && p.CapatazFirmaUrl == null && p.Estado == "Borrador";
        p.PuedeFirmarSupervisor = esResidente && p.FirmaUrl != null && p.SupervisorFirmaUrl == null && p.Estado == "Borrador" && (!requiereCapataz || p.CapatazFirmaUrl != null);
        p.PuedeFirmarSsoma = esSsoma && p.FirmaUrl != null && p.SupervisorFirmaUrl != null && p.SsomaFirmaUrl == null && p.Estado == "Borrador";
        p.PuedeCerrar = p.WorkerId == workerId && p.Estado == "Firmado";
    }

    public async Task Firmar(int id, int workerId, PetarFirmarRequestDto body, string? ipOrigen, string? userAgent)
    {
        var entidad = await _repository.GetEntidad(id) ?? throw new AbrilException("PETAR no encontrado.", 404);
        if (entidad.WorkerId != workerId)
            throw new AbrilException("Este PETAR no te pertenece.", 403);
        if (entidad.Estado != "Borrador")
            throw new AbrilException("Este PETAR ya fue firmado.", 409);

        if (string.IsNullOrWhiteSpace(body.SelfieBase64))
            throw new AbrilException("La selfie es obligatoria para firmar el PETAR.", 400);
        var firmaBytes = await ObtenerFirmaDigitalObligatoria(workerId);

        // El consentimiento de uso de imagen/geolocalización ya se dio al firmar el primer ATS —
        // no se vuelve a pedir acá, es el mismo trabajador con la misma autorización vigente.

        var (selfieUrl, selfieHash) = await SubirImagenConHash("selfie", workerId, body.SelfieBase64);
        var selfieDuplicada = await _repository.ExisteSelfieHash(selfieHash);

        var firmaHash = Convert.ToHexString(SHA256.HashData(firmaBytes));
        var firmaUrl = await SubirBytes("firma", workerId, firmaBytes, "png");

        var horaServidor = DateTime.UtcNow;
        await _repository.Firmar(entidad, selfieUrl, selfieHash, firmaUrl, firmaHash, horaServidor, body, ipOrigen, userAgent);

        if (selfieDuplicada)
        {
            var hashAnterior = await _repository.GetUltimoHashAuditLog(id);
            var hash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"{hashAnterior}|SelfieDuplicada|{id}|{DateTime.UtcNow:O}")));
            await _repository.AgregarAuditLog(id, "SelfieDuplicada", null, ipOrigen,
                "La selfie ya había sido usada en otro PETAR/ATS — revisar manualmente.", hashAnterior ?? string.Empty, hash);
        }

        // PETAR de cuadrilla: el aviso sale al cerrar el ATS grupal, no por cada adherido.
        if (entidad.PetarGrupoId == null)
            await AvisarResponsables(entidad.ProyectoId, await _atsRepository.EsObreroDeObra(entidad.WorkerId));
    }

    private async Task AvisarResponsables(int proyectoId, bool esObrero)
    {
        var responsables = await _atsRepository.GetResponsables(proyectoId);
        // Cadena: Capataz (si es obrero) → Supervisor (Residente/Producción) → SSOMA; cada uno recibe el aviso cuando le toca.
        var destinatarios = await _atsRepository.GetEmailsNivel(proyectoId, esObrero ? "Capataz" : "Autoriza");
        if (destinatarios.Count == 0) return;
        try
        {
            await _notificacionesService.CrearPorCorreosAsync(
                "ATS_PENDIENTE_FIRMA",
                destinatarios,
                origenUserId: null,
                items: [new NuevaNotificacionDto
                {
                    Titulo = "PETAR de alto riesgo pendiente de tu firma",
                    Subtitulo = responsables.ProyectoNombre,
                    Descripcion = esObrero
                        ? "Un PETAR ya fue firmado por el ejecutante y espera tu firma como Capataz/Maestro de obra."
                        : "Un PETAR ya fue firmado por el ejecutante y espera tu firma como Supervisor (Residente/Producción) antes de poder iniciar el trabajo.",
                    Referencia = "/ssoma/gestion/ats",
                }]);
        }
        catch { /* best-effort */ }
    }

    private async Task AvisarSupervisor(int proyectoId)
    {
        try
        {
            var responsables = await _atsRepository.GetResponsables(proyectoId);
            var destinatarios = await _atsRepository.GetEmailsNivel(proyectoId, "Autoriza");
            if (destinatarios.Count == 0) return;
            await _notificacionesService.CrearPorCorreosAsync(
                "ATS_PENDIENTE_FIRMA",
                destinatarios,
                origenUserId: null,
                items: [new NuevaNotificacionDto
                {
                    Titulo = "PETAR pendiente de tu firma (Supervisor)",
                    Subtitulo = responsables.ProyectoNombre,
                    Descripcion = "El Capataz/Maestro de obra ya firmó un PETAR de alto riesgo y espera tu firma como Supervisor (Residente/Producción).",
                    Referencia = "/ssoma/gestion/ats",
                }]);
        }
        catch { /* best-effort */ }
    }

    private async Task AvisarSsoma(int proyectoId)
    {
        try
        {
            var responsables = await _atsRepository.GetResponsables(proyectoId);
            if (responsables.SsomaEmails.Count == 0) return;
            await _notificacionesService.CrearPorCorreosAsync(
                "ATS_PENDIENTE_FIRMA",
                responsables.SsomaEmails,
                origenUserId: null,
                items: [new NuevaNotificacionDto
                {
                    Titulo = "PETAR pendiente de Visto Bueno SSOMA",
                    Subtitulo = responsables.ProyectoNombre,
                    Descripcion = "El Supervisor ya firmó un PETAR de alto riesgo y espera tu Visto Bueno.",
                    Referencia = "/ssoma/gestion/ats",
                }]);
        }
        catch { /* best-effort */ }
    }

    public async Task FirmarCapataz(int id, int callerUserId, bool esAdmin, PetarFirmarVistoRequestDto body)
        => await FirmarVistoComun(id, callerUserId, esAdmin, body, "Capataz");

    public async Task FirmarSupervisor(int id, int callerUserId, bool esAdmin, PetarFirmarVistoRequestDto body)
        => await FirmarVistoComun(id, callerUserId, esAdmin, body, "Supervisor");

    public async Task FirmarVistoSsoma(int id, int callerUserId, bool esAdmin, PetarFirmarVistoRequestDto body)
        => await FirmarVistoComun(id, callerUserId, esAdmin, body, "Ssoma");

    private async Task FirmarVistoComun(int id, int callerUserId, bool esAdmin, PetarFirmarVistoRequestDto body, string rol)
    {
        var entidad = await _repository.GetEntidad(id) ?? throw new AbrilException("PETAR no encontrado.", 404);
        var workerId = await _atsRepository.ResolverWorkerIdAsync(callerUserId);
        var firmaBytes = await ObtenerFirmaDigitalObligatoria(workerId);
        var responsables = await _atsRepository.GetResponsables(entidad.ProyectoId);

        // Cuadrilla cerrada: mientras esté abierta todavía puede sumarse gente.
        var ats = await _atsRepository.GetEntidad(entidad.AtsId);
        if (ats?.AtsGrupoId != null)
        {
            var atsGrupo = await _atsRepository.GetGrupoEntidad(ats.AtsGrupoId.Value);
            if (atsGrupo?.Estado != "Cerrado")
                throw new AbrilException("Cierra la cuadrilla del ATS antes de validar el PETAR: todavía puede sumarse gente.", 409);
        }

        // Orden de la cadena: Capataz (solo si el ejecutante es obrero) → Residente/Producción (Supervisor) → SSOMA.
        var requiereCapataz = await _atsRepository.EsObreroDeObra(entidad.WorkerId);
        var capatazFirmaUrl = entidad.CapatazFirmaUrl;
        if (entidad.PetarGrupoId != null)
            capatazFirmaUrl = (await _repository.GetGrupoEntidad(entidad.PetarGrupoId.Value))?.CapatazFirmaUrl;
        if (rol == "Capataz")
        {
            if (!requiereCapataz)
                throw new AbrilException("Este PETAR es de un Staff/supervisor, no requiere firma de Capataz.", 409);
        }
        else if (rol == "Supervisor")
        {
            if (requiereCapataz && capatazFirmaUrl == null)
                throw new AbrilException("Falta la firma del Capataz/Maestro de obra. Debe firmar antes del Residente/Producción.", 409);
        }
        else
        {
            if (entidad.SupervisorFirmaUrl == null)
                throw new AbrilException("Falta la firma del Residente/Producción. Debe firmar antes del Visto Bueno SSOMA.", 409);
            // SSOMA firma AL FINAL: el ATS de origen debe tener ya Capataz (obreros) y Autoriza.
            if (ats?.AtsGrupoId != null)
            {
                var estadoAtsPrev = await _atsRepository.GetEstadoGrupo(ats.AtsGrupoId.Value);
                if (estadoAtsPrev != null && (estadoAtsPrev.AutorizaFirmados < estadoAtsPrev.EjecutantesFirmados || (estadoAtsPrev.RequiereCapataz && !estadoAtsPrev.CapatazVigente)))
                    throw new AbrilException("Faltan firmas en el ATS (Capataz y/o Residente/Producción). SSOMA firma al final: primero debe completarse el ATS.", 409);
            }
            else if (ats != null && (ats.AutorizaFirmaUrl == null || (requiereCapataz && ats.CapatazFirmaUrl == null)))
                throw new AbrilException("Faltan firmas en el ATS (Capataz y/o Residente/Producción). SSOMA firma al final: primero debe completarse el ATS.", 409);
        }

        if (!esAdmin)
        {
            if (rol == "Capataz")
            {
                if (!await _atsRepository.EsCapatazDeProyecto(workerId, entidad.ProyectoId))
                    throw new AbrilException("Solo el Capataz o Maestro de obra de este proyecto puede firmar este nivel.", 403);
            }
            else if (rol == "Supervisor")
            {
                if (!await EsSupervisorDelProyecto(callerUserId, workerId, entidad.ProyectoId))
                    throw new AbrilException("Solo el Residente o el Ing./Arq. de Producción de este proyecto pueden firmar como Supervisor.", 403);
            }
            else
            {
                var email = await _atsRepository.GetEmailCorporativoWorker(workerId);
                if (email == null || !responsables.SsomaEmails.Contains(email, StringComparer.OrdinalIgnoreCase))
                    throw new AbrilException("Solo el Coordinador SSOMA de este proyecto puede dar el Visto Bueno.", 403);
            }
        }

        var (nombre, cargo) = await _atsRepository.GetNombreYCargo(workerId);

        var firmaHash = Convert.ToHexString(SHA256.HashData(firmaBytes));
        var firmaUrl = await SubirBytes(rol.ToLower(), workerId, firmaBytes, "png");

        // Si la otra firma ya existía, esta es la que completa las 3 y autoriza el inicio del
        // trabajo — el ejecutante es quien más necesita saberlo (está esperando para empezar).
        var quedaAutorizado = rol == "Capataz" ? false : rol == "Supervisor" ? entidad.SsomaFirmaUrl != null : entidad.SupervisorFirmaUrl != null;

        await _repository.FirmarVisto(id, rol, workerId, nombre, cargo, firmaUrl, firmaHash, DateTime.UtcNow);

        if (rol == "Capataz")
            await AvisarSupervisor(entidad.ProyectoId);
        if (rol == "Supervisor")
            await AvisarSsoma(entidad.ProyectoId);
        if (quedaAutorizado)
            await AvisarEjecutanteAutorizado(entidad);
    }

    private async Task AvisarEjecutanteAutorizado(SsPetar entidad)
    {
        var email = await _atsRepository.GetEmailCorporativoWorker(entidad.WorkerId);
        if (string.IsNullOrWhiteSpace(email)) return;

        try
        {
            await _notificacionesService.CrearPorCorreosAsync(
                "PETAR_AUTORIZADO",
                [email],
                origenUserId: null,
                items: [new NuevaNotificacionDto
                {
                    Titulo = "Tu PETAR fue autorizado",
                    Subtitulo = entidad.DescripcionTrabajo,
                    Descripcion = "Supervisor y SSOMA ya firmaron — ya puedes iniciar el trabajo de alto riesgo.",
                    Referencia = "/ssoma/gestion/ats",
                }]);
        }
        catch { /* best-effort */ }
    }

    public async Task Cerrar(int id, int workerId, PetarCerrarRequestDto body)
    {
        var entidad = await _repository.GetEntidad(id) ?? throw new AbrilException("PETAR no encontrado.", 404);

        var firmaBytes = await ObtenerFirmaDigitalObligatoria(workerId);
        var firmaHash = Convert.ToHexString(SHA256.HashData(firmaBytes));
        var firmaUrl = await SubirBytes("cierre", workerId, firmaBytes, "png");

        await _repository.Cerrar(id, workerId, firmaUrl, firmaHash, body.Observaciones, DateTime.UtcNow);
        await AvisarCierre(entidad.ProyectoId);
    }

    private async Task AvisarCierre(int proyectoId)
    {
        var responsables = await _atsRepository.GetResponsables(proyectoId);
        var destinatarios = new List<string>();
        if (!string.IsNullOrWhiteSpace(responsables.ResidenteEmail)) destinatarios.Add(responsables.ResidenteEmail);
        destinatarios.AddRange(responsables.SsomaEmails);
        if (destinatarios.Count == 0) return;

        try
        {
            await _notificacionesService.CrearPorCorreosAsync(
                "PETAR_CERRADO",
                destinatarios,
                origenUserId: null,
                items: [new NuevaNotificacionDto
                {
                    Titulo = "PETAR cerrado",
                    Subtitulo = responsables.ProyectoNombre,
                    Descripcion = "El ejecutante cerró el PETAR — el trabajo de alto riesgo asociado ha finalizado.",
                    Referencia = "/ssoma/gestion/ats",
                }]);
        }
        catch { /* best-effort */ }
    }

    public async Task<byte[]> GenerarPdf(int id)
    {
        var petar = await _repository.GetPorId(id) ?? throw new AbrilException("PETAR no encontrado.", 404);
        // OJO: Estado se queda en "Borrador" hasta que Supervisor Y SSOMA también firman (ver
        // FirmarVisto) — NO cambia solo porque el ejecutante ya firmó. El gate real es si ya
        // existe la firma del ejecutante, no el Estado.
        if (petar.FirmaUrl == null)
            throw new AbrilException("Solo se puede exportar un PETAR con al menos la firma del ejecutante.", 409);

        byte[]? selfieBytes = petar.SelfieUrl != null ? await DescargarBytes(petar.SelfieUrl) : null;
        byte[]? firmaBytes = petar.FirmaUrl != null ? await DescargarBytes(petar.FirmaUrl) : null;
        byte[]? firmaCapatazBytes = petar.CapatazFirmaUrl != null ? await DescargarBytes(petar.CapatazFirmaUrl) : null;
        petar.RequiereCapataz = await _atsRepository.EsObreroDeObra(petar.WorkerId);
        byte[]? firmaSupervisorBytes = petar.SupervisorFirmaUrl != null ? await DescargarBytes(petar.SupervisorFirmaUrl) : null;
        byte[]? firmaSsomaBytes = petar.SsomaFirmaUrl != null ? await DescargarBytes(petar.SsomaFirmaUrl) : null;

        var baseUrl = _configuration["Frontend:BaseUrl"]?.TrimEnd('/') ?? "https://intranet.abril.pe";
        var verificacionUrl = $"{baseUrl}/petar-verificar/{petar.Id}?hash={(petar.PdfHash != null ? petar.PdfHash[..Math.Min(12, petar.PdfHash.Length)] : "")}";

        byte[]? logoBytes = null;
        var logoPath = _logoPaths.FirstOrDefault(File.Exists);
        if (logoPath != null)
            logoBytes = await File.ReadAllBytesAsync(logoPath);

        var pdfBytes = PetarPdfService.Generar(petar, selfieBytes, firmaBytes, verificacionUrl, firmaSupervisorBytes, firmaSsomaBytes, logoBytes, firmaCapatazBytes);
        var pdfHash = Convert.ToHexString(SHA256.HashData(pdfBytes));

        var container = _containerResolver.GetAtsContainerName();
        using var stream = new MemoryStream(pdfBytes);
        var fileName = $"petar_{petar.Id}_{DateTime.UtcNow:yyyyMMddHHmmssfff}.pdf";
        var urls = await _fileStorageService.UploadFilesAsync([(stream, fileName)], container);

        await _repository.GuardarPdf(id, urls[0], pdfHash);
        return pdfBytes;
    }

    /// <summary>PDF del PETAR grupal: un solo documento por cuadrilla con la firma de cada trabajador adherido y la
    /// firma única de Supervisor y SSOMA (las que falten salen como PENDIENTE).</summary>
    public async Task<byte[]> GenerarPdfGrupo(int petarGrupoId)
    {
        var g = await _repository.GetGrupoParaPdf(petarGrupoId) ?? throw new AbrilException("PETAR grupal no encontrado.", 404);
        var firmantes = g.Adhesiones.Where(a => a.FirmaUrl != null).OrderBy(a => a.Worker?.Person?.FullName).ToList();
        if (firmantes.Count == 0)
            throw new AbrilException("Todavía no firmó ningún trabajador de la cuadrilla en este PETAR.", 409);

        var filas = new List<PetarGrupalFila>();
        foreach (var a in firmantes)
            filas.Add(new PetarGrupalFila(
                a.Worker?.Person?.FullName ?? "—", a.HoraServidorFirma, a.Lat, a.Lng,
                a.SelfieUrl != null ? await DescargarBytes(a.SelfieUrl) : null,
                await DescargarBytes(a.FirmaUrl!)));

        var requiereCapatazGrupo = (await _atsRepository.GetEstadoGrupo(g.AtsGrupoId))?.RequiereCapataz ?? false;
        var capataz = requiereCapatazGrupo
            ? new PetarGrupalFirma(g.CapatazNombre, g.CapatazCargo, g.CapatazHoraServidor,
                g.CapatazFirmaUrl != null ? await DescargarBytes(g.CapatazFirmaUrl) : null)
            : null;
        var supervisor = new PetarGrupalFirma(g.SupervisorNombre, g.SupervisorCargo, g.SupervisorHoraServidor,
            g.SupervisorFirmaUrl != null ? await DescargarBytes(g.SupervisorFirmaUrl) : null);
        var ssoma = new PetarGrupalFirma(g.SsomaNombre, g.SsomaCargo, g.SsomaHoraServidor,
            g.SsomaFirmaUrl != null ? await DescargarBytes(g.SsomaFirmaUrl) : null);

        var hashCorto = HashVerificacionGrupo(g);
        var baseUrl = _configuration["Frontend:BaseUrl"]?.TrimEnd('/') ?? "https://intranet.abril.pe";
        var verificacionUrl = $"{baseUrl}/petar-verificar/{g.Id}?grupo=1&hash={hashCorto}";

        var datos = new PetarGrupalDatos(
            g.Tipo?.Codigo, g.Tipo?.Nombre, g.Codigo ?? $"PETG-{g.Id:D4}", g.AtsGrupo?.Codigo, g.Proyecto?.ProjectDescription,
            g.DescripcionTrabajo, g.Lugar, g.Fecha, g.HoraInicio, g.HoraFin,
            g.Respuestas.OrderBy(r => r.Orden).Select(r => (r.Texto, r.Respuesta)).ToList());

        byte[]? logoBytes = null;
        var logoPath = _logoPaths.FirstOrDefault(File.Exists);
        if (logoPath != null)
            logoBytes = await File.ReadAllBytesAsync(logoPath);

        return PetarPdfService.GenerarGrupal(datos, filas, capataz, supervisor, ssoma, logoBytes, verificacionUrl, hashCorto);
    }

    /// <summary>Huella (12 hex) del contenido firmado del PETAR grupal: hashes de las firmas de cada trabajador +
    /// Supervisor + SSOMA. Se recalcula al verificar, así no depende de una columna y el QR del PDF solo deja de
    /// coincidir si cambió algo firmado (p. ej. se sumó un trabajador o firmó otro nivel).</summary>
    private static string HashVerificacionGrupo(SsPetarGrupo g)
    {
        var partes = g.Adhesiones.Where(a => a.FirmaUrl != null).OrderBy(a => a.Id).Select(a => a.FirmaHash ?? string.Empty)
            .Append(g.CapatazFirmaHash ?? string.Empty).Append(g.SupervisorFirmaHash ?? string.Empty).Append(g.SsomaFirmaHash ?? string.Empty);
        var texto = $"petar-grupo|{g.Id}|{string.Join("|", partes)}";
        return Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(texto)))[..12];
    }

    public async Task<PetarVerificacionPublicaDto> VerificarPublicoGrupo(int petarGrupoId, string? hash)
    {
        var g = await _repository.GetGrupoParaPdf(petarGrupoId);
        if (g == null) return new PetarVerificacionPublicaDto { Encontrado = false, Valido = false };

        var firmantes = g.Adhesiones.Count(a => a.FirmaUrl != null);
        return new PetarVerificacionPublicaDto
        {
            Encontrado = true,
            Valido = !string.IsNullOrWhiteSpace(hash) && string.Equals(hash, HashVerificacionGrupo(g), StringComparison.OrdinalIgnoreCase),
            WorkerNombre = $"Cuadrilla de {firmantes} trabajador(es) — {g.Codigo}",
            ProyectoNombre = g.Proyecto?.ProjectDescription,
            TipoNombre = g.Tipo?.Nombre,
            Fecha = g.Fecha,
            Estado = g.Estado,
        };
    }

    public async Task<PetarVerificacionPublicaDto> VerificarPublico(int id, string? hash)
    {
        var petar = await _repository.GetPorId(id);
        if (petar == null || petar.Estado == "Borrador")
            return new PetarVerificacionPublicaDto { Encontrado = false, Valido = false };

        var prefijoEsperado = petar.PdfHash?[..Math.Min(12, petar.PdfHash.Length)];
        var valido = !string.IsNullOrWhiteSpace(hash) && string.Equals(hash, prefijoEsperado, StringComparison.OrdinalIgnoreCase);

        return new PetarVerificacionPublicaDto
        {
            Encontrado = true,
            Valido = valido,
            WorkerNombre = petar.WorkerNombre,
            ProyectoNombre = petar.ProyectoNombre,
            TipoNombre = petar.TipoNombre,
            Fecha = petar.Fecha,
            Estado = petar.Estado,
        };
    }

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

        if (bytes.Length == 0)
            throw new AbrilException("La imagen enviada está vacía.", 400);

        var hash = Convert.ToHexString(SHA256.HashData(bytes));
        var url = await SubirBytes(prefijo, workerId, bytes, "jpg");
        return (url, hash);
    }

    private async Task<string> SubirBytes(string prefijo, int workerId, byte[] bytes, string ext)
    {
        var container = _containerResolver.GetAtsContainerName();
        using var stream = new MemoryStream(bytes);
        var fileName = $"petar_{prefijo}_{workerId}_{DateTime.UtcNow:yyyyMMddHHmmssfff}.{ext}";
        var urls = await _fileStorageService.UploadFilesAsync([(stream, fileName)], container);
        return urls[0];
    }

    /// <summary>Toda firma del PETAR sale de la firma digital ya capturada en la autorización
    /// (SSO-FO-151) — ya no se acepta una firma dibujada al momento.</summary>
    private async Task<byte[]> ObtenerFirmaDigitalObligatoria(int workerId)
    {
        var (url, _, _) = await _atsRepository.GetFirmaDigitalAutorizacion(workerId);
        var bytes = url == null ? null : await DescargarBytes(url);
        return bytes ?? throw new AbrilException(
            "No hay una firma digital registrada para este trabajador. El Coordinador SSOMA debe capturarla en Autorizaciones antes de poder firmar.", 400);
    }

    private static async Task<byte[]?> DescargarBytes(string url)
    {
        try
        {
            using var http = new HttpClient();
            return await http.GetByteArrayAsync(url);
        }
        catch { return null; }
    }

    // ── PETAR Grupal ──────────────────────────────────────────────────────

    /// <summary>Cualquier trabajador puede crear el checklist grupal (mismo criterio que el ATS
    /// grupal) — nace ligado a un ATS grupal ya existente, del que hereda el proyecto.</summary>
    /// <summary>Genera el PETAR de la cuadrilla desde el QR, SIN cuenta: lo pide el autor de la cuadrilla o el Capataz/Maestro
    /// del proyecto, identificándose con su nombre (lista acotada) + dígitos de DNI.</summary>
    public async Task<PetarGrupoCrearResponseDto> CrearGrupoPublico(Guid atsToken, PetarGrupoCrearPublicoDto body)
    {
        var atsGrupo = await _atsRepository.GetGrupoPorToken(atsToken) ?? throw new AbrilException("Enlace inválido.", 404);
        if (atsGrupo.QrExpiraEn < DateTime.UtcNow)
            throw new AbrilException("Este enlace venció — el ATS grupal era para otra jornada.", 409);
        if (atsGrupo.Estado is "Anulado" or "Reemplazado")
            throw new AbrilException($"Este ATS grupal está {atsGrupo.Estado.ToLower()} — no se puede generar PETAR.", 409);
        if (!await _atsRepository.DniCoincide(body.WorkerId, body.DniConfirmacion))
            throw new AbrilException("Los dígitos de DNI no coinciden con el trabajador seleccionado.", 400);
        if (atsGrupo.CreadoPorWorkerId != body.WorkerId
            && !(await _atsRepository.GetCapatacesDeProyecto(atsGrupo.ProyectoId)).Any(c => c.WorkerId == body.WorkerId))
            throw new AbrilException("Solo quien creó la cuadrilla o el Capataz/Maestro de obra del proyecto pueden generar el PETAR desde aquí.", 403);

        body.AtsGrupoId = atsGrupo.Id;
        return await CrearGrupo(body.WorkerId, body);
    }

    public async Task<PetarGrupoCrearResponseDto> CrearGrupo(int workerId, PetarGrupoCrearRequestDto dto)
    {
        if (dto.TipoId <= 0)
            throw new AbrilException("Selecciona el tipo de trabajo de alto riesgo.", 400);
        if (string.IsNullOrWhiteSpace(dto.DescripcionTrabajo))
            throw new AbrilException("Describe el trabajo a realizar.", 400);
        if (dto.Respuestas.Count == 0)
            throw new AbrilException("Completa el checklist de verificación.", 400);
        if (dto.Respuestas.Any(r => r.Respuesta == "NO"))
            throw new AbrilException("Hay ítems del checklist marcados como NO cumplidos — el trabajo no puede iniciar hasta corregirlos.", 400);

        var atsGrupo = await _atsRepository.GetGrupoEntidad(dto.AtsGrupoId)
            ?? throw new AbrilException("El ATS grupal de origen no existe.", 404);

        var grupo = await _repository.CrearGrupo(workerId, atsGrupo.ProyectoId, dto);
        return new PetarGrupoCrearResponseDto { Id = grupo.Id };
    }

    /// <summary>Lo que ve el trabajador en la misma página pública del QR del ATS grupal, para
    /// elegir cuáles PETAR le aplican a él (no todos en la cuadrilla hacen la misma tarea de alto
    /// riesgo).</summary>
    public async Task<List<PetarGrupoResumenPublicoDto>> GetGruposPublicoPorAtsToken(Guid atsToken)
    {
        var atsGrupo = await _atsRepository.GetGrupoPorToken(atsToken);
        if (atsGrupo is null) return [];
        return await _repository.GetGruposActivosPorAtsGrupo(atsGrupo.Id);
    }

    /// <summary>A diferencia de CerrarGrupo (que sí exige ser el autor o admin), VER el estado no
    /// se restringe al autor — el Residente/SSOMA que debe firmar normalmente NO es quien creó el
    /// ATS grupal, y necesita poder abrir este mismo panel para hacerlo. El control real de quién
    /// puede firmar sigue viviendo en FirmarVistoGrupoComun, no acá.</summary>
    public async Task<List<PetarGrupoEstadoDto>> GetEstadosPorAtsGrupo(int atsGrupoId, int callerUserId, int workerId, bool esAdmin)
    {
        var obrasT = _residentes.ProyectosDelResidenteAsync(callerUserId);
        var emailT = _atsRepository.GetEmailCorporativoWorker(workerId);
        await Task.WhenAll(obrasT, emailT);
        return await EstadosDeAtsGrupo(atsGrupoId, workerId, esAdmin, obrasT.Result.ToHashSet(), emailT.Result);
    }

    public async Task<Dictionary<int, List<PetarGrupoEstadoDto>>> GetEstadosPorAtsGrupos(IReadOnlyCollection<int> atsGrupoIds, int callerUserId, int workerId, bool esAdmin)
    {
        var ids = atsGrupoIds.Distinct().Take(60).ToList();
        if (ids.Count == 0) return [];

        // Lo de quien llama (obras donde es Residente, correo) se consulta UNA vez para todas las cuadrillas de la página.
        var obrasT = _residentes.ProyectosDelResidenteAsync(callerUserId);
        var emailT = _atsRepository.GetEmailCorporativoWorker(workerId);
        await Task.WhenAll(obrasT, emailT);
        var obras = obrasT.Result.ToHashSet();

        // Cada cuadrilla usa sus propios DbContext: se resuelven en paralelo, con tope para no saturar el pool de conexiones.
        using var compuerta = new SemaphoreSlim(6);
        var tareas = ids.ToDictionary(id => id, async id =>
        {
            await compuerta.WaitAsync();
            try { return await EstadosDeAtsGrupo(id, workerId, esAdmin, obras, emailT.Result); }
            finally { compuerta.Release(); }
        });
        await Task.WhenAll(tareas.Values);
        return tareas.ToDictionary(k => k.Key, k => k.Value.Result);
    }

    /// <summary>PETAR de UN ATS grupal con los permisos de quien llama. Todo lo que es igual para todos los PETAR de la cuadrilla
    /// (estado del ATS grupal, responsables, roles en el proyecto) se consulta una vez, no una por PETAR. Mismas reglas que
    /// <see cref="MarcarPermisosGrupo"/>.</summary>
    private async Task<List<PetarGrupoEstadoDto>> EstadosDeAtsGrupo(int atsGrupoId, int workerId, bool esAdmin, HashSet<int> obrasDondeEsResidente, string? email)
    {
        var lista = await _repository.GetEstadosPorAtsGrupo(atsGrupoId);
        if (lista.Count == 0) return lista;

        var estadoAts = await _atsRepository.GetEstadoGrupo(atsGrupoId);
        foreach (var d in lista) d.RequiereCapataz = estadoAts?.RequiereCapataz ?? false;
        if (estadoAts?.Estado != "Cerrado") return lista;

        var activos = lista.Where(d => d.Estado == "Activo").ToList();
        if (activos.Count == 0) return lista;

        var atsCompleto = estadoAts.AutorizaFirmados >= estadoAts.EjecutantesFirmados && (!estadoAts.RequiereCapataz || estadoAts.CapatazVigente);

        var proyectos = activos.Select(d => d.ProyectoId).Distinct().ToList();
        var permisos = proyectos.ToDictionary(id => id, async id =>
        {
            if (esAdmin) return (EsResidente: true, EsSsoma: true, EsCapataz: true);
            var responsablesT = _atsRepository.GetResponsables(id);
            var produccionT = obrasDondeEsResidente.Contains(id) ? Task.FromResult(true) : _atsRepository.EsProduccionDeProyecto(workerId, id);
            var capatazT = _atsRepository.EsCapatazDeProyecto(workerId, id);
            await Task.WhenAll(responsablesT, produccionT, capatazT);
            var esSsoma = email != null && responsablesT.Result.SsomaEmails.Contains(email, StringComparer.OrdinalIgnoreCase);
            return (EsResidente: produccionT.Result, EsSsoma: esSsoma, EsCapataz: capatazT.Result);
        });
        await Task.WhenAll(permisos.Values);

        foreach (var dto in activos)
        {
            var p = permisos[dto.ProyectoId].Result;
            dto.PuedeFirmarCapataz = p.EsCapataz && dto.RequiereCapataz && !dto.CapatazFirmado && dto.TotalAdhesiones > 0;
            dto.PuedeFirmarSupervisor = p.EsResidente && !dto.SupervisorFirmado && (!dto.RequiereCapataz || dto.CapatazFirmado);
            dto.PuedeFirmarSsoma = p.EsSsoma && dto.SupervisorFirmado && !dto.SsomaFirmado && atsCompleto;
        }
        return lista;
    }

    public async Task<PetarGrupoEstadoDto> GetEstadoGrupo(int id, int callerUserId, int workerId, bool esAdmin)
    {
        var dto = await _repository.GetEstadoGrupo(id) ?? throw new AbrilException("PETAR grupal no encontrado.", 404);
        await MarcarPermisosGrupo(dto, workerId, esAdmin,
            await _residentes.EsResidenteDelProyectoAsync(callerUserId, dto.ProyectoId));
        return dto;
    }

    /// <summary>Mismo criterio de permisos que FirmarVistoGrupoComun (Residente=Supervisor,
    /// Coordinador SSOMA=Ssoma) — acá solo decide qué botón mostrar, la firma real se valida de
    /// nuevo server-side al firmar. El Residente llega resuelto por persona, igual que en el PETAR
    /// individual.</summary>
    private async Task MarcarPermisosGrupo(PetarGrupoEstadoDto dto, int workerId, bool esAdmin, bool esResidenteDelProyecto)
    {
        var estadoAts = await _atsRepository.GetEstadoGrupo(dto.AtsGrupoId);
        dto.RequiereCapataz = estadoAts?.RequiereCapataz ?? false;
        if (dto.Estado != "Activo") return;
        if (estadoAts?.Estado != "Cerrado") return;
        var responsables = await _atsRepository.GetResponsables(dto.ProyectoId);
        var esResidente = esAdmin || esResidenteDelProyecto || await _atsRepository.EsProduccionDeProyecto(workerId, dto.ProyectoId);
        var email = await _atsRepository.GetEmailCorporativoWorker(workerId);
        var esSsoma = esAdmin || (email != null && responsables.SsomaEmails.Contains(email, StringComparer.OrdinalIgnoreCase));
        var esCapataz = esAdmin || await _atsRepository.EsCapatazDeProyecto(workerId, dto.ProyectoId);
        dto.PuedeFirmarCapataz = esCapataz && dto.RequiereCapataz && !dto.CapatazFirmado && dto.TotalAdhesiones > 0;
        dto.PuedeFirmarSupervisor = esResidente && !dto.SupervisorFirmado && (!dto.RequiereCapataz || dto.CapatazFirmado);
        var atsCompleto = estadoAts != null && estadoAts.AutorizaFirmados >= estadoAts.EjecutantesFirmados && (!estadoAts.RequiereCapataz || estadoAts.CapatazVigente);
        dto.PuedeFirmarSsoma = esSsoma && dto.SupervisorFirmado && !dto.SsomaFirmado && atsCompleto;
    }

    public async Task CerrarGrupo(int id, int workerId, bool esAdmin)
    {
        if (!esAdmin && !await _repository.EsAutorDeGrupo(id, workerId))
            throw new AbrilException("Este PETAR grupal no te pertenece.", 403);
        await _repository.CerrarGrupo(id);
    }

    /// <summary>PETAR de la cuadrilla que esperan la firma del Capataz, para su página pública (la misma del ATS grupal).
    /// Solo con la cuadrilla cerrada y si es de obreros; vacío en cualquier otro caso.</summary>
    public async Task<List<PetarGrupoCapatazPublicoDto>> GetCapatazPublicoPetares(Guid atsToken)
    {
        var atsGrupo = await _atsRepository.GetGrupoPorToken(atsToken);
        if (atsGrupo == null || atsGrupo.QrExpiraEn < DateTime.UtcNow || atsGrupo.Estado != "Cerrado") return [];
        var estado = await _atsRepository.GetEstadoGrupo(atsGrupo.Id);
        if (estado == null || !estado.RequiereCapataz) return [];

        var lista = await _repository.GetEstadosPorAtsGrupo(atsGrupo.Id);
        return lista.Where(p => p.TotalAdhesiones > 0).Select(p => new PetarGrupoCapatazPublicoDto
        {
            Id = p.Id, Codigo = p.Codigo, TipoNombre = p.TipoNombre, DescripcionTrabajo = p.DescripcionTrabajo,
            TotalAdhesiones = p.TotalAdhesiones, CapatazFirmado = p.CapatazFirmado,
        }).ToList();
    }

    /// <summary>El Capataz/Maestro SIN cuenta firma el PETAR de la cuadrilla desde el link público del ATS grupal: se
    /// identifica con su nombre (lista acotada del proyecto) + dígitos del DNI y deja selfie + geolocalización, igual
    /// que al firmar el ATS grupal.</summary>
    public async Task FirmarCapatazGrupoPublico(int petarGrupoId, PetarGrupoCapatazFirmarPublicoDto body)
    {
        if (string.IsNullOrWhiteSpace(body.SelfieBase64))
            throw new AbrilException("La selfie es obligatoria — es la prueba de que estás en obra.", 400);
        if (!Guid.TryParse(body.AtsToken, out var atsToken))
            throw new AbrilException("Enlace inválido.", 400);

        var atsGrupo = await _atsRepository.GetGrupoPorToken(atsToken) ?? throw new AbrilException("Enlace inválido.", 404);
        if (atsGrupo.QrExpiraEn < DateTime.UtcNow)
            throw new AbrilException("Este enlace venció — el ATS grupal era para otra jornada.", 409);
        var grupo = await _repository.GetGrupoEntidad(petarGrupoId) ?? throw new AbrilException("PETAR grupal no encontrado.", 404);
        if (grupo.AtsGrupoId != atsGrupo.Id)
            throw new AbrilException("Este PETAR no corresponde a este ATS.", 400);
        if (atsGrupo.Estado != "Cerrado")
            throw new AbrilException("La cuadrilla todavía está abierta: el autor debe cerrarla antes de que firmes.", 409);

        var estadoAts = await _atsRepository.GetEstadoGrupo(atsGrupo.Id);
        if (estadoAts == null || !estadoAts.RequiereCapataz)
            throw new AbrilException("Esta cuadrilla es de Staff/supervisores — no requiere firma de Capataz.", 409);
        var estadoPetar = await _repository.GetEstadoGrupo(petarGrupoId);
        if (estadoPetar == null || estadoPetar.TotalAdhesiones == 0)
            throw new AbrilException("Todavía no firmó ningún trabajador de la cuadrilla en este PETAR.", 409);
        if (grupo.CapatazFirmaUrl != null)
            throw new AbrilException("Este PETAR ya tiene la firma del Capataz/Maestro de obra.", 409);

        if (!await _atsRepository.DniCoincide(body.WorkerId, body.DniConfirmacion))
            throw new AbrilException("Los dígitos de DNI no coinciden con el trabajador seleccionado.", 400);
        var capataces = await _atsRepository.GetCapatacesDeProyecto(grupo.ProyectoId);
        if (!capataces.Any(c => c.WorkerId == body.WorkerId))
            throw new AbrilException("Solo el Capataz o Maestro de obra de este proyecto puede firmar este nivel.", 403);

        var (nombre, cargo) = await _atsRepository.GetNombreYCargo(body.WorkerId);
        var firmaBytes = await ObtenerFirmaDigitalObligatoria(body.WorkerId);
        var firmaHash = Convert.ToHexString(SHA256.HashData(firmaBytes));
        var firmaUrl = await SubirBytes("capataz-grupo", body.WorkerId, firmaBytes, "png");
        var (selfieUrl, selfieHash) = await SubirImagenConHash("capatazselfie", body.WorkerId, body.SelfieBase64);

        await _repository.FirmarVistoGrupo(petarGrupoId, "Capataz", body.WorkerId, nombre, cargo, firmaUrl, firmaHash, DateTime.UtcNow,
            new PetarEvidenciaPresencia(selfieUrl, selfieHash, body.Lat, body.Lng, body.PrecisionMetros, body.HoraDispositivo));
        await AvisarSupervisor(grupo.ProyectoId);
    }

    public Task FirmarCapatazGrupo(int id, int callerUserId, bool esAdmin, PetarFirmarVistoRequestDto body)
        => FirmarVistoGrupoComun(id, callerUserId, esAdmin, body, "Capataz");

    public Task FirmarSupervisorGrupo(int id, int callerUserId, bool esAdmin, PetarFirmarVistoRequestDto body)
        => FirmarVistoGrupoComun(id, callerUserId, esAdmin, body, "Supervisor");

    public Task FirmarSsomaGrupo(int id, int callerUserId, bool esAdmin, PetarFirmarVistoRequestDto body)
        => FirmarVistoGrupoComun(id, callerUserId, esAdmin, body, "Ssoma");

    private async Task FirmarVistoGrupoComun(int id, int callerUserId, bool esAdmin, PetarFirmarVistoRequestDto body, string rol)
    {
        var grupo = await _repository.GetGrupoEntidad(id) ?? throw new AbrilException("PETAR grupal no encontrado.", 404);
        var workerId = await _atsRepository.ResolverWorkerIdAsync(callerUserId);
        var firmaBytes = await ObtenerFirmaDigitalObligatoria(workerId);

        var atsGrupoPadre = await _atsRepository.GetGrupoEntidad(grupo.AtsGrupoId);
        if (atsGrupoPadre?.Estado != "Cerrado")
            throw new AbrilException("Cierra la cuadrilla del ATS antes de validar el PETAR: todavía puede sumarse gente.", 409);

        var estadoPetar = await _repository.GetEstadoGrupo(id);
        if (estadoPetar == null || estadoPetar.TotalAdhesiones == 0)
            throw new AbrilException("Todavía no firmó ningún trabajador de la cuadrilla en este PETAR.", 409);

        // Orden de la cadena: Capataz (solo cuadrillas de obreros) → Residente/Producción (Supervisor) → SSOMA.
        var estadoAtsPadre = await _atsRepository.GetEstadoGrupo(grupo.AtsGrupoId);
        var requiereCapataz = estadoAtsPadre?.RequiereCapataz ?? false;
        if (rol == "Capataz")
        {
            if (!requiereCapataz)
                throw new AbrilException("Esta cuadrilla es de Staff/supervisores — no requiere firma de Capataz.", 409);
        }
        else if (rol == "Supervisor")
        {
            if (requiereCapataz && grupo.CapatazFirmaUrl == null)
                throw new AbrilException("Falta la firma del Capataz/Maestro de obra. Debe firmar antes del Residente/Producción.", 409);
        }
        else
        {
            if (grupo.SupervisorFirmaUrl == null)
                throw new AbrilException("Falta la firma del Residente/Producción. Debe firmar antes del Visto Bueno SSOMA.", 409);
            // SSOMA firma AL FINAL: el ATS de la cuadrilla debe tener ya Capataz (obreros) y Autoriza sobre todos.
            if (estadoAtsPadre != null && (estadoAtsPadre.AutorizaFirmados < estadoAtsPadre.EjecutantesFirmados || (estadoAtsPadre.RequiereCapataz && !estadoAtsPadre.CapatazVigente)))
                throw new AbrilException("Faltan firmas en el ATS (Capataz y/o Residente/Producción). SSOMA firma al final: primero debe completarse el ATS.", 409);
        }

        if (!esAdmin)
        {
            var responsables = await _atsRepository.GetResponsables(grupo.ProyectoId);
            if (rol == "Capataz")
            {
                if (!await _atsRepository.EsCapatazDeProyecto(workerId, grupo.ProyectoId))
                    throw new AbrilException("Solo el Capataz o Maestro de obra de este proyecto puede firmar este nivel.", 403);
            }
            else if (rol == "Supervisor")
            {
                if (!await EsSupervisorDelProyecto(callerUserId, workerId, grupo.ProyectoId))
                    throw new AbrilException("Solo el Residente o el Ing./Arq. de Producción de este proyecto pueden firmar como Supervisor.", 403);
            }
            else
            {
                var email = await _atsRepository.GetEmailCorporativoWorker(workerId);
                if (email == null || !responsables.SsomaEmails.Contains(email, StringComparer.OrdinalIgnoreCase))
                    throw new AbrilException("Solo el Coordinador SSOMA de este proyecto puede dar el Visto Bueno.", 403);
            }
        }

        var (nombre, cargo) = await _atsRepository.GetNombreYCargo(workerId);

        var firmaHash = Convert.ToHexString(SHA256.HashData(firmaBytes));
        var firmaUrl = await SubirBytes(rol.ToLower() + "-grupo", workerId, firmaBytes, "png");

        await _repository.FirmarVistoGrupo(id, rol, workerId, nombre, cargo, firmaUrl, firmaHash, DateTime.UtcNow);
        if (rol == "Capataz")
            await AvisarSupervisor(grupo.ProyectoId);
        if (rol == "Supervisor")
            await AvisarSsoma(grupo.ProyectoId);
    }

    /// <summary>Adhesión liviana a un PETAR grupal — mismo QR/token que el ATS grupal del que
    /// nace. Reusa Firmar() (que ya existe para el flujo individual) para no duplicar la lógica
    /// de selfie/hash/auditoría.</summary>
    public async Task<int> UnirseAGrupo(int petarGrupoId, PetarGrupoUnirseRequestDto body, string? ipOrigen, string? userAgent)
    {
        if (!Guid.TryParse(body.AtsToken, out var atsToken))
            throw new AbrilException("Enlace inválido.", 400);

        var atsGrupo = await _atsRepository.GetGrupoPorToken(atsToken) ?? throw new AbrilException("Enlace inválido.", 404);
        var petarGrupo = await _repository.GetGrupoEntidad(petarGrupoId) ?? throw new AbrilException("Este PETAR ya no está disponible.", 404);
        if (petarGrupo.AtsGrupoId != atsGrupo.Id)
            throw new AbrilException("Este PETAR no corresponde a este ATS.", 400);
        if (petarGrupo.Estado != "Activo")
            throw new AbrilException("Este PETAR grupal ya fue cerrado.", 409);

        var atsPropio = await _atsRepository.GetEntidad(body.AtsIdPropio)
            ?? throw new AbrilException("Primero debes firmar tu ATS antes de firmar el PETAR.", 400);
        if (atsPropio.WorkerId != body.WorkerId || atsPropio.AtsGrupoId != atsGrupo.Id)
            throw new AbrilException("Este ATS no corresponde a este trabajador/grupo.", 403);

        if (await _repository.YaFirmoPetarGrupo(petarGrupo.Id, body.WorkerId))
            throw new AbrilException("Ya firmaste este PETAR.", 409);

        await ObtenerFirmaDigitalObligatoria(body.WorkerId); // antes de crear el borrador, para no dejarlo huérfano

        var petarId = await _repository.CrearDesdeGrupo(body.WorkerId, body.AtsIdPropio, petarGrupo);

        var firmarDto = new PetarFirmarRequestDto
        {
            SelfieBase64 = body.SelfieBase64,
            FirmaBase64 = body.FirmaBase64,
            HoraDispositivo = body.HoraDispositivo,
            Lat = body.Lat,
            Lng = body.Lng,
            PrecisionMetros = body.PrecisionMetros,
        };
        await Firmar(petarId, body.WorkerId, firmarDto, ipOrigen, userAgent);

        return petarId;
    }
}
