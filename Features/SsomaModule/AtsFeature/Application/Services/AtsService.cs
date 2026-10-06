using System.Security.Cryptography;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.AuthModule.UserFeature.Application.Dtos;
using Abril_Backend.Features.AuthModule.UserFeature.Application.Interfaces;
using Abril_Backend.Features.SsomaModule.AtsFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.AtsFeature.Application.Interfaces;
using Abril_Backend.Features.SsomaModule.AtsFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.SsomaModule.AtsFeature.Infrastructure.Models;
using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Shared.Helpers;
using Abril_Backend.Shared.Services.Notificaciones.Dtos;
using Abril_Backend.Shared.Services.Notificaciones.Interfaces;
using Abril_Backend.Shared.Services.Residentes.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Abril_Backend.Features.SsomaModule.AtsFeature.Application.Services;

public class AtsService : IAtsService
{
    private readonly IAtsRepository _repository;
    private readonly IFileStorageService _fileStorageService;
    private readonly IStorageContainerResolver _containerResolver;
    private readonly IConfiguration _configuration;
    private readonly INotificacionesService _notificacionesService;
    private readonly IResidenteProyectoResolver _residentes;
    private readonly IUserFeatureService _userFeatureService;
    private readonly string[] _logoPaths;

    public AtsService(
        IAtsRepository repository,
        IFileStorageService fileStorageService,
        IStorageContainerResolver containerResolver,
        IConfiguration configuration,
        INotificacionesService notificacionesService,
        IResidenteProyectoResolver residentes,
        IUserFeatureService userFeatureService,
        IWebHostEnvironment env)
    {
        _repository = repository;
        _fileStorageService = fileStorageService;
        _containerResolver = containerResolver;
        _configuration = configuration;
        _notificacionesService = notificacionesService;
        _residentes = residentes;
        _userFeatureService = userFeatureService;
        _logoPaths =
        [
            Path.Combine(env.WebRootPath, "images", "abril-logo.png"),
            Path.Combine(env.WebRootPath, "images", "logo-abril.jpg"),
            Path.Combine(env.ContentRootPath, "Templates", "logo-abril.jpg"),
        ];
    }

    public Task<int> ResolverWorkerId(int userId) => _repository.ResolverWorkerIdAsync(userId);

    /// <summary>Un solo lote a la BD con una sola conexión (ver AtsRepository.GetInit), más la
    /// consulta de si quien llena el ATS es obrero de obra (EsObreroDeObra): de eso depende si ve
    /// los pasos de las categorías universales, y esa regla queda en un solo lugar. Antes eran ~11
    /// consultas, 8 de ellas en paralelo con Task.WhenAll, cada una con su propio DbContext y su
    /// conexión del pool: con la BD, Task.WhenAll solo gasta conexiones.</summary>
    public async Task<AtsInitDto> GetInit(int workerId) =>
        await _repository.GetInit(workerId, esStaff: !await _repository.EsObreroDeObra(workerId));

      /// <summary>Pasos para un puesto ARBITRARIO (no el del caller) — lo usa el wizard de ATS
    /// Grupal cuando el creador elige el puesto/tipo de trabajo de la cuadrilla, que puede ser
    /// distinto al suyo propio (ver AtsInitDto.Puestos).</summary>
    public async Task<List<AtsCategoriaPasoDto>> GetPasosPorPuesto(int puestoId, int workerId)
        => await _repository.GetPasosParaPuesto(puestoId, !await _repository.EsObreroDeObra(workerId));

    public async Task<AtsPasoDto> CrearPasoPersonalizado(int categoriaId, string texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            throw new AbrilException("Describe el paso.", 400);
        return await _repository.CrearPasoPersonalizado(categoriaId, texto.Trim());
    }

    /// <summary>Todo ATS nuevo nace de una cuadrilla (un trabajador solo = cuadrilla de 1); los individuales quedan como historial.</summary>
    public Task<int> Crear(int workerId, AtsGuardarRequestDto dto)
        => throw new AbrilException("Los ATS ahora se crean como cuadrilla (incluso para un solo trabajador). Usa «Nuevo ATS» → cuadrilla.", 409);

    public async Task Editar(int id, int workerId, AtsGuardarRequestDto dto)
    {
        await ExigirAutorizacionPermiso(workerId);
        Validar(dto);
        await _repository.Editar(id, workerId, dto);
    }

    /// <summary>Sin la autorización de uso de firma digital e imagen firmada en físico (escaneada
    /// y subida por un Coordinador SSOMA) un trabajador no puede crear ni editar un ATS — mismo
    /// principio que el gate de enrolamiento de Arquitectura Comercial (SSO-FO-150), aplicado
    /// aquí a la creación del ATS en vez de al enrolamiento biométrico: antes de capturar su
    /// selfie/geolocalización/firma, el trabajador debe haber autorizado por escrito ese uso.
    /// GetInit() NO lleva este gate a propósito: esa misma acción la usa también la pantalla de
    /// administración de plantillas/pasos, que cualquier coordinador debe poder abrir sin tener
    /// él mismo esa autorización subida.</summary>
    private async Task ExigirAutorizacionPermiso(int workerId)
    {
        if (!await _repository.TieneAutorizacionPermiso(workerId))
            throw new AbrilException(
                "Aún no puedes crear un ATS: falta que tu Coordinador SSOMA suba tu autorización " +
                "firmada de uso de firma digital e imagen. Coordina con él para que la suba y podrás continuar.", 403);
    }

    public Task<bool> TieneAutorizacionPermiso(int workerId) => _repository.TieneAutorizacionPermiso(workerId);

    public Task<List<AtsAutorizacionTrabajadorDto>> GetTrabajadoresParaAutorizacion() => _repository.GetTrabajadoresParaAutorizacion();

    public async Task SubirAutorizacionPermiso(int workerId, Stream archivo, string nombreArchivo, int subidoPorUserId)
    {
        var container = _containerResolver.GetAtsContainerName();
        var fileName = $"autorizacion_{workerId}_{DateTime.UtcNow:yyyyMMddHHmmssfff}{Path.GetExtension(nombreArchivo)}";
        var urls = await _fileStorageService.UploadFilesAsync([(archivo, fileName)], container);
        await _repository.SubirAutorizacionPermiso(workerId, urls[0], subidoPorUserId);

        // Capataz/Maestro de obra con correo declarado: con el escaneado firmado ya registrado se les
        // crea la cuenta y les llega el correo para generar su contraseña.
        if (await _repository.EsCapatazOMaestro(workerId) && await _repository.GetEmailPersonalAutorizacion(workerId) != null)
            await CrearCuentaCapataz(workerId);
    }

    /// <summary>Crea el usuario (con contraseña propia, no SSO) del Capataz/Maestro de obra y le envía
    /// el enlace para generarla — requiere correo personal declarado y el escaneado firmado. Si ya tiene
    /// usuario no hace nada (para reenviar el enlace se usa "restablecer contraseña").</summary>
    public async Task CrearCuentaCapataz(int workerId)
    {
        if (!await _repository.EsCapatazOMaestro(workerId))
            throw new AbrilException("Solo el Capataz o Maestro de obra tiene cuenta propia para firmar.", 400);

        var email = await _repository.GetEmailPersonalAutorizacion(workerId)
            ?? throw new AbrilException("Registra primero el correo personal del trabajador.", 400);
        if (!await _repository.TieneAutorizacionPermiso(workerId))
            throw new AbrilException("Sube primero la autorización firmada — la cuenta se crea con ese respaldo.", 400);
        if (await _repository.TieneUsuario(workerId)) return;

        var roleId = await _repository.GetRoleIdCapataz()
            ?? throw new AbrilException("Falta crear el rol \"CAPATAZ / MAESTRO DE OBRA\" (corre la migración 2026-09-30_ats_capataz_cuenta.sql).", 500);
        var persona = await _repository.GetDatosPersonaParaCuenta(workerId)
            ?? throw new AbrilException("El trabajador no tiene DNI registrado.", 400);

        await _userFeatureService.Create(new UserFeatureCreateDto
        {
            DocumentIdentityCode = persona.Dni,
            FirstNames = persona.Nombres,
            FirstLastName = persona.ApellidoPaterno,
            SecondLastName = persona.ApellidoMaterno,
            Email = email,
            PhoneNumber = persona.Telefono,
            RoleIds = [roleId],
        }, enlaceCompletarRegistro: true);
    }

    public async Task GuardarEmailPersonalAutorizacion(int workerId, AtsAutorizacionEmailRequestDto body)
    {
        if (!await _repository.EsCapatazOMaestro(workerId))
            throw new AbrilException("El correo personal solo aplica a Capataz o Maestro de obra.", 400);
        if (!body.AceptaDeclaracion)
            throw new AbrilException("El trabajador debe aceptar la declaración de uso personal y exclusivo del correo.", 400);

        var email = body.Email?.Trim() ?? string.Empty;
        if (!System.Text.RegularExpressions.Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            throw new AbrilException("El correo no es válido.", 400);

        await _repository.GuardarEmailPersonalAutorizacion(workerId, email);
    }

    public async Task CapturarFirmaDigitalAutorizacion(int workerId, AtsAutorizacionFirmaDigitalRequestDto body, int capturadoPorUserId)
    {
        if (string.IsNullOrWhiteSpace(body.FirmaBase64))
            throw new AbrilException("La firma es obligatoria.", 400);

        var firmaBytes = FirmaImagenHelper.DecodePng(body.FirmaBase64);
        var firmaHash = Convert.ToHexString(SHA256.HashData(firmaBytes));
        var firmaUrl = await SubirBytes("autorizacion-firma-digital", workerId, firmaBytes, "png");

        await _repository.CapturarFirmaDigitalAutorizacion(workerId, firmaUrl, firmaHash, capturadoPorUserId);
    }

    public Task<(string? FirmaDigitalUrl, string? Nombre, string? Dni)> GetFirmaDigitalAutorizacion(int workerId)
        => _repository.GetFirmaDigitalAutorizacion(workerId);

    /// <summary>Descarga la imagen de la firma digital autorizada YA capturada de un trabajador
    /// (misma que usa GenerarPlantillaAutorizacionPdf) para que el frontend la reutilice al firmar
    /// un ATS o al Autorizar/dar Visto Bueno — el navegador no puede traer el blob directo (storage
    /// privado), así que esto pasa por el backend con las credenciales del servidor.</summary>
    public async Task<byte[]?> GetFirmaDigitalAutorizacionImagen(int workerId)
    {
        var (firmaDigitalUrl, _, _) = await _repository.GetFirmaDigitalAutorizacion(workerId);
        return firmaDigitalUrl == null ? null : await DescargarBytes(firmaDigitalUrl);
    }

    /// <summary>TODAS las firmas del ATS/PETAR salen de la firma digital ya capturada en la autorización
    /// (SSO-FO-151): ya no se acepta una firma dibujada al momento — así la firma es siempre la misma
    /// y no puede variar entre documentos.</summary>
    public async Task<byte[]> ObtenerFirmaDigitalObligatoria(int workerId)
    {
        var bytes = await GetFirmaDigitalAutorizacionImagen(workerId);
        return bytes ?? throw new AbrilException(
            "No hay una firma digital registrada para este trabajador. El Coordinador SSOMA debe capturarla en Autorizaciones antes de poder firmar.", 400);
    }

    public async Task<byte[]> GenerarPlantillaAutorizacionPdf(int workerId)
    {
        var (firmaDigitalUrl, nombre, dni) = await _repository.GetFirmaDigitalAutorizacion(workerId);
        if (firmaDigitalUrl == null)
            throw new AbrilException("Primero debes capturar la firma digital del trabajador — recién ahí se habilita descargar la plantilla.", 400);

        byte[]? logoBytes = null;
        var logoPath = _logoPaths.FirstOrDefault(File.Exists);
        if (logoPath != null)
            logoBytes = await File.ReadAllBytesAsync(logoPath);

        byte[]? firmaDigitalBytes = await DescargarBytes(firmaDigitalUrl);

        return AtsAutorizacionPdfService.GenerarPdf(nombre ?? string.Empty, dni, logoBytes, firmaDigitalBytes, await _repository.GetEmailPersonalAutorizacion(workerId));
    }

    private static readonly HashSet<string> NivelesValidos = ["A", "M", "B"];

    private static void Validar(AtsGuardarRequestDto dto)
    {
        if (dto.ProyectoId <= 0)
            throw new AbrilException("Selecciona el proyecto.", 400);
        if (string.IsNullOrWhiteSpace(dto.Actividad))
            throw new AbrilException("Describe la actividad a realizar.", 400);
        if (dto.Riesgos.Count == 0)
            throw new AbrilException("Identifica al menos un peligro con su riesgo asociado.", 400);

        foreach (var r in dto.Riesgos)
        {
            if (!NivelesValidos.Contains(r.RiesgoBase))
                throw new AbrilException("El riesgo base debe ser Alto, Medio o Bajo.", 400);
            if (string.IsNullOrWhiteSpace(r.Controles))
                throw new AbrilException("Todo riesgo identificado debe tener sus controles definidos.", 400);
            if (!NivelesValidos.Contains(r.RiesgoResidual))
                throw new AbrilException("El riesgo residual (después de los controles) debe ser Alto, Medio o Bajo.", 400);
        }
    }

    public async Task<AtsResponseDto> GetPorId(int id, int callerUserId, int workerId, bool esAdmin)
    {
        var ats = await _repository.GetPorId(id) ?? throw new AbrilException("ATS no encontrado.", 404);
        var (esCapataz, esAutoriza, esSsoma) = await PuedeAutorizarYVistoBueno(ats, callerUserId, workerId, esAdmin);
        ats.RequiereCapataz = await _repository.EsObreroDeObra(ats.WorkerId);

        if (!esAdmin && ats.WorkerId != workerId && !esCapataz && !esAutoriza && !esSsoma)
            throw new AbrilException("Este ATS no te pertenece.", 403);

        MarcarPermisos(ats, esCapataz, esAutoriza, esSsoma);
        await _repository.CompletarInfoPetar([ats], new() { [ats.Id] = (esAutoriza, esSsoma, esCapataz) });
        return ats;
    }

    /// <summary>Capataz = Capataz/Maestro de obra vinculado actualmente al proyecto (por puesto) —
    /// solo aplica cuando el ejecutante es obrero de obra (ver EsObreroDeObra); si el ejecutante ya
    /// es Staff, su propia firma cubre este nivel y EsCapataz siempre sale false para ese ATS.
    /// Autorizar = Residente asignado al proyecto (Project.ResidenteWorkersId) O el
    /// Ingeniero/Arquitecto de Producción vinculado actualmente a ese proyecto (por puesto, no
    /// hay campo dedicado) O el Jefe/Administrador SSOMA. Visto Bueno SSOMA = el correo de
    /// Coordinador SSOMA configurado en el proyecto O cualquier prevencionista de Abril (puesto
    /// "Prevencionista", cualquier proyecto) O el Jefe/Administrador SSOMA. Nadie se autovalida su
    /// propio ATS — EXCEPTO el Jefe/Administrador SSOMA, que pidió acceso total sin bloqueo ni
    /// siquiera sobre lo suyo (es la última instancia, no hay a quién más escalar).
    /// El Residente se reconoce por persona (IResidenteProyectoResolver), no por la ficha del
    /// caller: una persona puede tener varias fichas por reingreso y el proyecto guarda una.</summary>
    private async Task<(bool EsCapataz, bool EsAutoriza, bool EsSsoma)> PuedeAutorizarYVistoBueno(AtsResponseDto ats, int callerUserId, int workerId, bool esAdmin)
    {
        if (ats.WorkerId == workerId && !esAdmin) return (false, false, false);
        if (ats.WorkerId == workerId && esAdmin) return (true, true, true);

        var responsables = await _repository.GetResponsables(ats.ProyectoId);
        var callerEmail = await _repository.GetEmailCorporativoWorker(workerId);

        var esObreroEjecutante = await _repository.EsObreroDeObra(ats.WorkerId);
        var esCapatazCaller = esObreroEjecutante && await _repository.EsCapatazDeProyecto(workerId, ats.ProyectoId);
        var esResidente = await _residentes.EsResidenteDelProyectoAsync(callerUserId, ats.ProyectoId);
        var esProduccion = !esResidente && await _repository.EsProduccionDeProyecto(workerId, ats.ProyectoId);
        var esCoordSsoma = callerEmail != null && responsables.SsomaEmails.Contains(callerEmail, StringComparer.OrdinalIgnoreCase);
        var esPrevencionista = !esCoordSsoma && await _repository.EsPrevencionistaAbril(workerId);

        return (esCapatazCaller || (esAdmin && esObreroEjecutante), esResidente || esProduccion || esAdmin, esCoordSsoma || esPrevencionista || esAdmin);
    }

    /// <summary>
    /// Un trabajador normal solo ve los ATS de SU proyecto actual (evita que vea, y sobre todo
    /// que le salgan botones sobre, ATS de otros proyectos que no le corresponden). El Jefe/
    /// Administrador SSOMA sigue viendo TODOS, sin filtrar — necesita saber qué falta aprobar en
    /// cualquier proyecto, y esAdmin también lo habilita a Autorizar/Visto Bueno en cualquiera
    /// (ver PuedeAutorizarYVistoBueno).
    /// </summary>
    /// <summary>Mismo alcance que Listar: un trabajador normal solo ve los grupos de SU proyecto actual.</summary>
    public async Task<AtsGrupoListResponseDto> ListarGrupos(AtsFiltroDto filtro, int workerId, bool esAdmin)
    {
        if (!esAdmin)
        {
            var (_, proyectoActualId) = await _repository.GetPuestoYProyectoActual(workerId);
            filtro.ProyectoId = proyectoActualId;
        }
        var res = await _repository.ListarGrupos(filtro);

        // Permisos de firma por cuadrilla (mismo criterio que GetEstadoGrupo) — se resuelven una vez por proyecto.
        var rolesPorProyecto = new Dictionary<int, (bool EsResidente, bool EsProduccion, bool EsSsoma)>();
        foreach (var g in res.Data)
        {
            if (!rolesPorProyecto.TryGetValue(g.ProyectoId, out var roles))
                rolesPorProyecto[g.ProyectoId] = roles = await RolesEnProyecto(g.ProyectoId, workerId);
            var abierto = g.Estado is not ("Anulado" or "Reemplazado");
            // La validación solo corre con la cuadrilla CERRADA (cierre manual del autor/Capataz/Residente/Producción/SSOMA).
            var validable = g.Estado == "Cerrado";
            g.PuedeAutorizar = validable && (esAdmin || roles.EsResidente || roles.EsProduccion) && g.AutorizaFirmados < g.EjecutantesFirmados;
            g.PuedeVistoBuenoSsoma = validable && (esAdmin || roles.EsSsoma) && g.SsomaFirmados < g.EjecutantesFirmados;
            if (!abierto || !g.RequiereCapataz || g.CapatazVigente) g.CapatazLinkToken = null;
        }
        return res;
    }

    public async Task<AtsListaInitDto> GetListaInit(int workerId) =>
        await _repository.GetListaInit(workerId);

    private static void MarcarPermisos(AtsResponseDto ats, bool esCapataz, bool esAutoriza, bool esSsoma)
    {
        ats.PuedeCapataz = esCapataz && ats.Estado == "Firmado" && ats.CapatazFirmaUrl == null;
        ats.PuedeAnular = (esAutoriza || esSsoma) && ats.Estado == "Firmado";
        ats.PuedeAutorizar = esAutoriza && ats.Estado == "Firmado" && ats.AutorizaFirmaUrl == null;
        ats.PuedeVistoBuenoSsoma = esSsoma && ats.Estado == "Firmado" && ats.SsomaFirmaUrl == null;
    }

    public async Task Firmar(int id, int workerId, AtsFirmarRequestDto body, string? ipOrigen, string? userAgent)
    {
        var entidad = await _repository.GetEntidad(id) ?? throw new AbrilException("ATS no encontrado.", 404);
        if (entidad.WorkerId != workerId)
            throw new AbrilException("Este ATS no te pertenece.", 403);
        if (entidad.Estado != "Borrador")
            throw new AbrilException("Este ATS ya fue firmado.", 409);

        // OJO: el PETAR solo se puede generar sobre un ATS ya Firmado (PetarService.Crear lo exige),
        // así que NO se puede bloquear la firma acá — sería un candado imposible de abrir. En vez de
        // eso, el frontend redirige a "Generar PETAR" automáticamente apenas termina de firmar
        // (ver AtsNuevo.firmar() en el frontend).
        if (string.IsNullOrWhiteSpace(body.SelfieBase64))
            throw new AbrilException("La selfie es obligatoria para firmar el ATS.", 400);
        var firmaBytes = await ObtenerFirmaDigitalObligatoria(workerId);

        if (!await _repository.TieneConsentimiento(workerId))
        {
            if (!body.AceptaConsentimiento)
                throw new AbrilException("Debes aceptar el uso de tu imagen y geolocalización para firmar el ATS digital.", 400);
            await _repository.RegistrarConsentimiento(workerId, ipOrigen);
        }

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
                "La selfie ya había sido usada en otro ATS — revisar manualmente.", hashAnterior ?? string.Empty, hash);
        }

        await AvisarResponsables(entidad);
    }

    /// <summary>
    /// Avisa al Residente y al/los Coordinador(es) SSOMA del proyecto apenas el ejecutante firma
    /// — por la campanita de notificaciones, NO por correo (un correo por cada ATS del día satura
    /// la bandeja; la campanita ya trae su propio contador de no leídas y no genera spam). El
    /// botón "Autorizar"/"Visto Bueno SSOMA" solo se habilita para ELLOS en la lista de ATS (ver
    /// MarcarPermisos), nunca para cualquier otro usuario aunque tenga acceso al módulo.
    /// Si el proyecto no tiene Residente o correo SSOMA cargado, o esos correos no tienen usuario
    /// en el sistema, CrearPorCorreosAsync simplemente no crea nada — no bloquea la firma del
    /// ejecutante, que ya quedó registrada.
    /// </summary>
    private async Task AvisarResponsables(SsAts entidad)
    {
        try
        {
            var esObrero = await _repository.EsObreroDeObra(entidad.WorkerId);
            var referencia = "/ssoma/gestion/ats";
            // Cuadrilla: no se avisa por cada adherido; el aviso sale una sola vez, cuando se CIERRA (CerrarGrupo).
            if (entidad.AtsGrupoId != null) return;
            // La cadena es Capataz → Residente/Producción → SSOMA: se avisa solo a quien le toca firmar ahora.
            await AvisarNivel(entidad.ProyectoId, esObrero ? "Capataz" : "Autoriza", referencia);
        }
        catch { /* best-effort: la firma del ejecutante ya quedó guardada, el aviso es una comodidad */ }
    }

    /// <summary>Avisa (campanita + push al celular) a quienes firman ese nivel de la cadena en el proyecto.</summary>
    private async Task AvisarNivel(int proyectoId, string nivel, string referencia)
    {
        try
        {
            var destinatarios = await _repository.GetEmailsNivel(proyectoId, nivel);
            if (destinatarios.Count == 0) return;
            var responsables = await _repository.GetResponsables(proyectoId);
            var (titulo, detalle) = nivel switch
            {
                "Capataz" => ("ATS pendiente de tu firma (Capataz)", "Un ATS ya fue firmado por el ejecutante y espera tu firma como Capataz/Maestro de obra."),
                "Autoriza" => ("ATS pendiente de tu firma (Autoriza)", "Un ATS está listo para tu Autorización como Residente/Producción."),
                _ => ("ATS pendiente de Visto Bueno SSOMA", "Un ATS ya tiene la firma del Residente/Producción y espera tu Visto Bueno."),
            };
            await _notificacionesService.CrearPorCorreosAsync(
                "ATS_PENDIENTE_FIRMA",
                destinatarios,
                origenUserId: null,
                items: [new NuevaNotificacionDto { Titulo = titulo, Subtitulo = responsables.ProyectoNombre, Descripcion = detalle, Referencia = referencia }]);
        }
        catch { /* best-effort */ }
    }

    private async Task FirmarVistoComun(int id, int callerUserId, bool esAdmin, AtsFirmarVistoRequestDto body, string rol)
    {
        var entidad = await _repository.GetEntidad(id) ?? throw new AbrilException("ATS no encontrado.", 404);
        if (entidad.Estado == "Anulado") throw new AbrilException("Este ATS está anulado.", 409);
        if (entidad.AtsGrupoId != null) await ExigirGrupoCerrado(entidad.AtsGrupoId.Value);
        await ExigirSinObservacionesAbiertas(id, entidad.AtsGrupoId);
        var workerId = await _repository.ResolverWorkerIdAsync(callerUserId);
        var firmaBytes = await ObtenerFirmaDigitalObligatoria(workerId);

        if (entidad.AtsGrupoId == null && await _repository.FaltaPetarObligatorio(id))
            throw new AbrilException("Este ATS tiene un riesgo que exige PETAR y todavía no se generó. Genera el PETAR antes de validar.", 409);

        // Orden de la cadena: Capataz → Autoriza → SSOMA (el Capataz solo aplica a obreros).
        if (rol == "Autoriza" && entidad.CapatazFirmaUrl == null && await _repository.EsObreroDeObra(entidad.WorkerId))
            throw new AbrilException("Falta la firma del Capataz/Maestro de obra. Debe firmar antes del Residente/Producción.", 409);
        if (rol == "Ssoma" && entidad.AutorizaFirmaUrl == null)
            throw new AbrilException("Falta la firma del Residente/Producción. Debe firmar antes del Visto Bueno SSOMA.", 409);
        // SSOMA firma AL FINAL: si el ATS tiene PETAR, antes deben estar firmados por el Capataz (obreros) y el Supervisor.
        if (rol == "Ssoma")
        {
            var requiereCapatazPetar = entidad.AtsGrupoId != null
                ? (await _repository.GetEstadoGrupo(entidad.AtsGrupoId.Value))?.RequiereCapataz ?? false
                : await _repository.EsObreroDeObra(entidad.WorkerId);
            await ExigirPetaresFirmadosParaSsoma(entidad.AtsGrupoId == null ? id : null, entidad.AtsGrupoId, requiereCapatazPetar);
        }

        // Nadie se autovalida su propio ATS — excepto el Jefe/Administrador SSOMA (ver
        // PuedeAutorizarYVistoBueno): pidió acceso total, incluido sobre lo suyo.
        if (entidad.WorkerId == workerId && !esAdmin)
            throw new AbrilException("No puedes dar visto bueno a tu propio ATS.", 403);

        // El Jefe/Administrador SSOMA puede firmar cualquiera de los dos vistos en cualquier
        // proyecto — es quien pidió tener acceso total para destrabar lo que falte aprobar.
        if (!esAdmin)
        {
            var responsables = await _repository.GetResponsables(entidad.ProyectoId);
            if (rol == "Capataz")
            {
                if (!await _repository.EsObreroDeObra(entidad.WorkerId))
                    throw new AbrilException("Este ATS es de un Staff/supervisor, no requiere firma de Capataz.", 409);
                if (!await _repository.EsCapatazDeProyecto(workerId, entidad.ProyectoId))
                    throw new AbrilException("Solo el Capataz o Maestro de obra de este proyecto puede firmar este nivel.", 403);
            }
            else if (rol == "Autoriza")
            {
                var esResidente = await _residentes.EsResidenteDelProyectoAsync(callerUserId, entidad.ProyectoId);
                var esProduccion = !esResidente && await _repository.EsProduccionDeProyecto(workerId, entidad.ProyectoId);
                if (!esResidente && !esProduccion)
                    throw new AbrilException("Solo el Residente o el Ing./Arq. de Producción de este proyecto pueden firmar como Autoriza.", 403);
            }
            else
            {
                var email = await _repository.GetEmailCorporativoWorker(workerId);
                var esCoordSsoma = email != null && responsables.SsomaEmails.Contains(email, StringComparer.OrdinalIgnoreCase);
                var esPrevencionista = !esCoordSsoma && await _repository.EsPrevencionistaAbril(workerId);
                if (!esCoordSsoma && !esPrevencionista)
                    throw new AbrilException("Solo el Coordinador SSOMA de este proyecto o un prevencionista de Abril pueden dar el Visto Bueno.", 403);
            }
        }

        var (nombre, cargo) = await _repository.GetNombreYCargo(workerId);

        var firmaHash = Convert.ToHexString(SHA256.HashData(firmaBytes));
        var firmaUrl = await SubirBytes(rol.ToLower(), workerId, firmaBytes, "png");

        await _repository.FirmarVisto(id, rol, workerId, nombre, cargo, firmaUrl, firmaHash, DateTime.UtcNow);

        // Pasa el turno al siguiente nivel. En cuadrilla lo avisa FirmarVistoGrupo/Capataz una sola vez.
        if (entidad.AtsGrupoId == null && rol != "Ssoma")
            await AvisarNivel(entidad.ProyectoId, rol == "Capataz" ? "Autoriza" : "Ssoma", "/ssoma/gestion/ats");
    }

    public async Task<byte[]> GenerarPdf(int id)
    {
        var ats = await _repository.GetPorId(id) ?? throw new AbrilException("ATS no encontrado.", 404);
        if (ats.Estado != "Firmado")
            throw new AbrilException("Solo se puede exportar un ATS ya firmado.", 409);

        byte[]? selfieBytes = ats.SelfieUrl != null ? await DescargarBytes(ats.SelfieUrl) : null;
        byte[]? firmaBytes = ats.FirmaUrl != null ? await DescargarBytes(ats.FirmaUrl) : null;
        byte[]? firmaCapatazBytes = ats.CapatazFirmaUrl != null ? await DescargarBytes(ats.CapatazFirmaUrl) : null;
        byte[]? firmaAutorizaBytes = ats.AutorizaFirmaUrl != null ? await DescargarBytes(ats.AutorizaFirmaUrl) : null;
        byte[]? firmaSsomaBytes = ats.SsomaFirmaUrl != null ? await DescargarBytes(ats.SsomaFirmaUrl) : null;
        ats.RequiereCapataz = await _repository.EsObreroDeObra(ats.WorkerId);

        // Ruta PÚBLICA (fuera del shell logueado) — un inspector externo no tiene ni debe
        // necesitar una cuenta de Abril para verificar el documento.
        var baseUrl = _configuration["Frontend:BaseUrl"]?.TrimEnd('/') ?? "https://intranet.abril.pe";
        var verificacionUrl = $"{baseUrl}/ats-verificar/{ats.Id}?hash={(ats.PdfHash != null ? ats.PdfHash[..Math.Min(12, ats.PdfHash.Length)] : "")}";

        byte[]? logoBytes = null;
        var logoPath = _logoPaths.FirstOrDefault(File.Exists);
        if (logoPath != null)
            logoBytes = await File.ReadAllBytesAsync(logoPath);

        var pdfBytes = AtsPdfService.Generar(ats, selfieBytes, firmaBytes, verificacionUrl, firmaAutorizaBytes, firmaSsomaBytes, logoBytes, firmaCapatazBytes, await _repository.GetCodigosPetar(ats.AtsGrupoId == null ? id : null, ats.AtsGrupoId));

        var container = _containerResolver.GetAtsContainerName();
        using var stream = new MemoryStream(pdfBytes);
        var fileName = $"ats_{ats.Id}_{DateTime.UtcNow:yyyyMMddHHmmssfff}.pdf";
        var urls = await _fileStorageService.UploadFilesAsync([(stream, fileName)], container);

        // PdfHash ya quedó fijado al firmar (ver AtsRepository.Firmar/ComputeHashVerificacion) —
        // acá solo se actualiza la URL del archivo. Antes este método recalculaba el hash a
        // partir de los bytes del PDF recién generado, que ya llevaban impreso el QR con el
        // hash de la exportación ANTERIOR: la verificación pública nunca podía coincidir.
        await _repository.GuardarPdf(id, urls[0], ats.PdfHash ?? string.Empty);

        return pdfBytes;
    }

    public async Task<AtsVerificacionPublicaDto> VerificarPublico(int id, string? hash)
    {
        var ats = await _repository.GetPorId(id);
        if (ats == null || ats.Estado != "Firmado")
            return new AtsVerificacionPublicaDto { Encontrado = false, Valido = false };

        var prefijoEsperado = ats.PdfHash?[..Math.Min(12, ats.PdfHash.Length)];
        var valido = !string.IsNullOrWhiteSpace(hash) && string.Equals(hash, prefijoEsperado, StringComparison.OrdinalIgnoreCase);

        return new AtsVerificacionPublicaDto
        {
            Encontrado = true,
            Valido = valido,
            WorkerNombre = ats.WorkerNombre,
            ProyectoNombre = ats.ProyectoNombre,
            Actividad = ats.Actividad,
            Fecha = ats.Fecha,
            HoraServidorFirma = ats.HoraServidorFirma,
            Estado = ats.Estado,
        };
    }

    public Task<List<AtsPlantillaDto>> GetPlantillas() => _repository.GetPlantillasActivas();
    public Task<List<AtsPuestoDto>> GetPuestos() => _repository.GetPuestos();
    public Task<List<AtsPasoPuestoDto>> GetPasoPuestoMapeo() => _repository.GetPasoPuestoMapeo();
    public Task SetPasoPuestos(int pasoId, List<int> puestoIds) => _repository.SetPasoPuestos(pasoId, puestoIds);
    public Task<List<AtsPlantillaPuestoDto>> GetPlantillaPuestoMapeo() => _repository.GetPlantillaPuestoMapeo();
    public Task SetPlantillaPuestos(int plantillaId, List<int> puestoIds) => _repository.SetPlantillaPuestos(plantillaId, puestoIds);

    public async Task<AtsEppDto> CrearEpp(AtsCatalogoItemCrearDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Nombre))
            throw new AbrilException("El EPP necesita un nombre.", 400);
        return await _repository.CrearEpp(dto);
    }

    public async Task<AtsPeligroDto> CrearPeligro(AtsCatalogoItemCrearDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Nombre))
            throw new AbrilException("El peligro necesita un nombre.", 400);
        return await _repository.CrearPeligro(dto);
    }

    public async Task<AtsRiesgoDto> CrearRiesgo(int peligroId, AtsCatalogoItemCrearDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Nombre))
            throw new AbrilException("El riesgo necesita un nombre.", 400);
        return await _repository.CrearRiesgo(peligroId, dto);
    }

    public async Task<AtsHerramientaDto> CrearHerramienta(AtsCatalogoItemCrearDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Nombre))
            throw new AbrilException("La herramienta necesita un nombre.", 400);
        return await _repository.CrearHerramienta(dto);
    }

    public async Task<int> CrearPlantilla(AtsPlantillaGuardarRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Nombre))
            throw new AbrilException("La plantilla necesita un nombre.", 400);
        return await _repository.CrearPlantilla(dto);
    }

    public async Task EditarPlantilla(int id, AtsPlantillaGuardarRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Nombre))
            throw new AbrilException("La plantilla necesita un nombre.", 400);
        await _repository.EditarPlantilla(id, dto);
    }

    public Task DesactivarPlantilla(int id) => _repository.DesactivarPlantilla(id);

    public Task<List<AtsPeligroDto>> GetPeligrosConRiesgos() => _repository.GetPeligrosConRiesgos();

    public Task SetRiesgoRequierePetar(int riesgoId, bool requierePetar) => _repository.SetRiesgoRequierePetar(riesgoId, requierePetar);

    // ── Actividades/pasos por plantilla ──────────────────────────────────

    public Task<List<AtsPlantillaActividadDto>> GetActividadesDePlantilla(int plantillaId) => _repository.GetActividadesDePlantilla(plantillaId);

    public async Task<int> CrearActividad(int plantillaId, AtsPlantillaActividadGuardarRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Texto))
            throw new AbrilException("La actividad necesita un texto.", 400);
        return await _repository.CrearActividad(plantillaId, dto.Texto.Trim());
    }

    public async Task EditarActividad(int actividadId, AtsPlantillaActividadGuardarRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Texto))
            throw new AbrilException("La actividad necesita un texto.", 400);
        await _repository.EditarActividad(actividadId, dto.Texto.Trim());
    }

    public Task EliminarActividad(int actividadId) => _repository.EliminarActividad(actividadId);

    public async Task<int> CrearPaso(int actividadId, AtsPlantillaPasoGuardarRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Texto))
            throw new AbrilException("El paso necesita un texto.", 400);
        return await _repository.CrearPaso(actividadId, dto.Texto.Trim());
    }

    public async Task EditarPaso(int pasoId, AtsPlantillaPasoGuardarRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Texto))
            throw new AbrilException("El paso necesita un texto.", 400);
        await _repository.EditarPaso(pasoId, dto.Texto.Trim());
    }

    public Task EliminarPaso(int pasoId) => _repository.EliminarPaso(pasoId);

    public Task SetActividadPeligros(int actividadId, AtsPlantillaActividadPeligrosRequestDto dto) => _repository.SetActividadPeligros(actividadId, dto.PeligroIds);

    // ── Controles sugeridos por riesgo ───────────────────────────────────

    public Task<List<AtsRiesgoConControlesDto>> GetRiesgosConControles() => _repository.GetRiesgosConControles();

    public async Task<int> CrearControl(int riesgoId, AtsRiesgoControlGuardarRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Texto))
            throw new AbrilException("El control necesita un texto.", 400);
        return await _repository.CrearControl(riesgoId, dto.Texto.Trim(), dto.Tipo);
    }

    public async Task EditarControl(int controlId, AtsRiesgoControlGuardarRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Texto))
            throw new AbrilException("El control necesita un texto.", 400);
        await _repository.EditarControl(controlId, dto.Texto.Trim(), dto.Tipo);
    }

    public Task EliminarControl(int controlId) => _repository.EliminarControl(controlId);

    // ── ATS Grupal ──────────────────────────────────────────────────────────

    /// <summary>Cualquier trabajador puede crear un ATS grupal (decisión de Samuel 2026-09-30: en
    /// la práctica, cualquiera está en capacidad de hacerlo) — mismo gate de autorización de firma
    /// digital que el ATS individual, porque el autor también puede adherirse a su propio grupo.</summary>
    public async Task<AtsGrupoCrearResponseDto> CrearGrupo(int workerId, AtsGuardarRequestDto dto, bool esAdmin = false)
    {
        await ExigirAutorizacionPermiso(workerId);

        // Corrección con revisión: el grupo anterior queda "Reemplazado" y esta es la Rev. N+1 (se vuelve a firmar todo).
        SsAtsGrupo? anterior = null;
        if (dto.GrupoAnteriorId.HasValue)
        {
            anterior = await _repository.GetGrupoEntidad(dto.GrupoAnteriorId.Value) ?? throw new AbrilException("El ATS grupal que intentas corregir no existe.", 404);
            if (anterior.Estado is "Anulado" or "Reemplazado")
                throw new AbrilException($"Este ATS grupal ya está {anterior.Estado.ToLower()} — no se puede corregir.", 409);
            var (esResidente, esProduccion, _) = await RolesEnProyecto(anterior.ProyectoId, workerId);
            if (!esAdmin && !esResidente && !esProduccion && !await _repository.EsAutorDeGrupo(anterior.Id, workerId))
                throw new AbrilException("Solo quien creó el ATS grupal, el Residente/Producción o un administrador pueden corregirlo.", 403);
            dto.ProyectoId = anterior.ProyectoId;
        }

        Validar(dto);
        var grupo = await _repository.CrearGrupo(workerId, dto);
        // Quien arma el ATS grupal también es un ejecutante: queda como integrante esperado para que aparezca como
        // pendiente de firma (y se le avise en el panel) hasta que firme como los demás.
        await _repository.AgregarIntegrante(grupo.Id, workerId);
        if (anterior != null)
        {
            var (nombre, _) = await _repository.GetNombreYCargo(workerId);
            await _repository.ResolverObservacionesDeGrupo(anterior.Id, $"Corregido en la revisión {grupo.Revision}", workerId, nombre);
            await _repository.AgregarEventoGrupo(anterior.Id, "Reemplazado", workerId, $"Reemplazado por la revisión {grupo.Revision}");
        }
        return new AtsGrupoCrearResponseDto { Id = grupo.Id, QrToken = grupo.QrToken.ToString(), QrExpiraEn = grupo.QrExpiraEn };
    }

    public async Task<AtsGrupoEstadoDto> GetEstadoGrupo(int id, int workerId, bool esAdmin)
    {
        var grupo = await _repository.GetGrupoEntidad(id) ?? throw new AbrilException("ATS grupal no encontrado.", 404);

        // Lo ve su autor y quienes firman la cadena en ese proyecto (Residente/Producción/SSOMA) — antes solo
        // el autor, y por eso el Residente no podía abrir el panel para firmar.
        var responsables = await _repository.GetResponsables(grupo.ProyectoId);
        var email = await _repository.GetEmailCorporativoWorker(workerId);
        var esResidente = await EsResidenteDelProyecto(grupo.ProyectoId, workerId);
        var esProduccion = !esResidente && await _repository.EsProduccionDeProyecto(workerId, grupo.ProyectoId);
        var esSsoma = (email != null && responsables.SsomaEmails.Contains(email, StringComparer.OrdinalIgnoreCase))
                      || await _repository.EsPrevencionistaAbril(workerId);
        var esAutor = await _repository.EsAutorDeGrupo(id, workerId);
        var esCapatazGrupo = await _repository.EsCapatazDeProyecto(workerId, grupo.ProyectoId);
        if (!esAdmin && !esAutor && !esResidente && !esProduccion && !esSsoma && !esCapatazGrupo)
            throw new AbrilException("Este ATS grupal no te pertenece.", 403);

        var estado = await _repository.GetEstadoGrupo(id) ?? throw new AbrilException("ATS grupal no encontrado.", 404);
        estado.Esperados = await _repository.GetIntegrantes(id);
        estado.SoyAutor = esAutor;
        estado.YoYaFirme = (await _repository.GetWorkerIdsAdheridos(id)).Contains(workerId);
        estado.PuedeEditarIntegrantes = esAdmin || esAutor;
        // Reabrir: su autor, quienes validan la cadena en el proyecto (Residente/Producción/SSOMA) o un admin —
        // los que firman son justo quienes necesitan poder sumar a alguien que faltó.
        estado.PuedeReabrir = (esAdmin || esAutor || esResidente || esProduccion || esSsoma) && estado.Estado == "Cerrado" && grupo.Fecha == DateOnly.FromDateTime(DateTime.Today);
        estado.PuedeCerrar = (esAdmin || esAutor || esCapatazGrupo || esResidente || esProduccion || esSsoma) && estado.Estado == "Activo" && estado.EjecutantesFirmados > 0;
        estado.AnuladoMotivo = grupo.AnuladoMotivo;
        estado.PuedeAnular = estado.Estado != "Anulado"
            && (esAdmin || esResidente || esProduccion || esSsoma || (esAutor && !await _repository.GrupoTieneValidaciones(id)));
        if (estado.Estado is "Anulado" or "Reemplazado")
        {
            estado.PuedeAutorizar = false;
            estado.PuedeVistoBuenoSsoma = false;
            estado.PuedeReabrir = false;
        }
        estado.UltimoEvento = await _repository.GetUltimoEventoGrupo(id);
        estado.PuedeCorregir = estado.Estado is "Activo" or "Cerrado" && (esAdmin || esAutor || esResidente || esProduccion);
        estado.PuedeAutorizar = (esAdmin || esResidente || esProduccion) && estado.Estado == "Cerrado" && estado.AutorizaFirmados < estado.EjecutantesFirmados;
        estado.PuedeVistoBuenoSsoma = (esAdmin || esSsoma) && estado.Estado == "Cerrado" && estado.SsomaFirmados < estado.EjecutantesFirmados;
        if (estado.Estado is "Anulado" or "Reemplazado")
        {
            estado.PuedeAutorizar = false;
            estado.PuedeVistoBuenoSsoma = false;
        }
        return estado;
    }

    /// <summary>PDF del ATS grupal: firmas de todos los trabajadores + una sola firma de Capataz (si aplica),
    /// Autoriza y SSOMA. El contenido (pasos/IPERC) sale del primer ATS adherido, que es copia del grupo.</summary>
    public async Task<byte[]> GenerarPdfGrupo(int grupoId, int workerId, bool esAdmin)
    {
        var estado = await GetEstadoGrupo(grupoId, workerId, esAdmin); // valida acceso
        var grupo = await _repository.GetGrupoEntidad(grupoId) ?? throw new AbrilException("ATS grupal no encontrado.", 404);
        var adhesiones = await _repository.GetAdhesionesParaPdf(grupoId);
        if (adhesiones.Count == 0)
            throw new AbrilException("Todavía no hay trabajadores con el ATS firmado en esta cuadrilla.", 409);

        var contenido = await _repository.GetPorId(adhesiones[0].AtsId) ?? throw new AbrilException("ATS no encontrado.", 404);

        // Imágenes en paralelo — una cuadrilla grande son decenas de selfies/firmas.
        var selfies = await Task.WhenAll(adhesiones.Select(a => a.SelfieUrl != null ? DescargarBytes(a.SelfieUrl) : Task.FromResult<byte[]?>(null)));
        var firmas = await Task.WhenAll(adhesiones.Select(a => a.FirmaUrl != null ? DescargarBytes(a.FirmaUrl) : Task.FromResult<byte[]?>(null)));
        var filas = adhesiones.Select((a, i) => new AtsPdfService.GrupalFila(a.Nombre, a.Puesto, a.HoraServidorFirma, selfies[i], firmas[i], a.Lat, a.Lng, a.OrigenOffline, a.HoraDispositivo)).ToList();

        // Mientras el grupo sigue Activo, los esperados que aún no firmaron (incluido quien armó el ATS) salen como
        // PENDIENTE (borrador). Con el grupo Cerrado el documento muestra solo a quienes firmaron.
        if (grupo.Estado == "Activo")
        foreach (var e in (await _repository.GetIntegrantes(grupoId)).Where(x => !x.Adherido))
            filas.Add(new AtsPdfService.GrupalFila(e.Nombre + (e.WorkerId == grupo.CreadoPorWorkerId ? " (autor)" : ""), "Pendiente de firmar", null, null, null, null, null));

        // Autoriza y SSOMA se firman por cuadrilla: es la misma firma en cada ATS, se toma la primera que exista.
        var conAutoriza = adhesiones.FirstOrDefault(a => a.AutorizaFirmaUrl != null);
        var conSsoma = adhesiones.FirstOrDefault(a => a.SsomaFirmaUrl != null);
        var autoriza = new AtsPdfService.FirmaCadena(conAutoriza?.AutorizaNombre, conAutoriza?.AutorizaCargo, conAutoriza?.AutorizaHoraServidor,
            conAutoriza?.AutorizaFirmaUrl != null ? await DescargarBytes(conAutoriza.AutorizaFirmaUrl) : null);
        var ssoma = new AtsPdfService.FirmaCadena(conSsoma?.SsomaNombre, conSsoma?.SsomaCargo, conSsoma?.SsomaHoraServidor,
            conSsoma?.SsomaFirmaUrl != null ? await DescargarBytes(conSsoma.SsomaFirmaUrl) : null);
        var capataz = new AtsPdfService.FirmaCadena(grupo.CapatazNombre, grupo.CapatazCargo, grupo.CapatazHoraServidor,
            grupo.CapatazFirmaUrl != null ? await DescargarBytes(grupo.CapatazFirmaUrl) : null);

        byte[]? logoBytes = null;
        var logoPath = _logoPaths.FirstOrDefault(File.Exists);
        if (logoPath != null)
            logoBytes = await File.ReadAllBytesAsync(logoPath);

        return AtsPdfService.GenerarGrupal(contenido, grupo.Codigo ?? $"GRUPAL-{grupoId:D4}", filas, estado.RequiereCapataz, capataz, autoriza, ssoma, logoBytes, await _repository.GetCodigosPetar(null, grupoId));
    }

    /// <summary>Residente/Producción (rol "Autoriza") o SSOMA firman de una vez a TODA la cuadrilla: se aplica su
    /// firma digital a cada ATS adherido y firmado que todavía no la tenga. Cada ATS pasa por las mismas
    /// validaciones de siempre (FirmarVistoComun); el propio firmante queda fuera de su ATS, salvo admin.
    /// Devuelve cuántos ATS firmó.</summary>
    public async Task<int> FirmarVistoGrupo(int grupoId, string rol, int callerUserId, bool esAdmin)
    {
        if (rol != "Autoriza" && rol != "Ssoma")
            throw new AbrilException("Rol de firma inválido.", 400);
        var grupo = await _repository.GetGrupoEntidad(grupoId) ?? throw new AbrilException("ATS grupal no encontrado.", 404);
        var workerId = await _repository.ResolverWorkerIdAsync(callerUserId);

        if (grupo.Estado is "Anulado" or "Reemplazado") throw new AbrilException($"Este ATS grupal está {grupo.Estado.ToLower()} — ya no se puede firmar.", 409);
        if (grupo.Estado != "Cerrado") throw new AbrilException("Cierra la cuadrilla antes de validar: todavía puede sumarse gente.", 409);
        await ExigirSinObservacionesAbiertas(null, grupoId);

        // Cadena obligatoria: Capataz → Residente/Producción (Autoriza) → Visto Bueno SSOMA. Staff no lleva Capataz.
        var estado = await _repository.GetEstadoGrupo(grupoId) ?? throw new AbrilException("ATS grupal no encontrado.", 404);
        if (estado.EjecutantesFirmados == 0)
            throw new AbrilException("Todavía no firmó ningún trabajador de la cuadrilla.", 409);
        if (estado.RequierePetar && estado.TotalPetares == 0)
            throw new AbrilException("Este ATS tiene un riesgo que exige PETAR y todavía no se generó. Genera el PETAR de la cuadrilla antes de validar.", 409);
        if (estado.RequiereCapataz && !estado.CapatazVigente)
            throw new AbrilException(estado.CapatazNombre == null
                ? "Falta la firma del Capataz/Maestro de obra. Debe firmar primero."
                : $"Se sumaron {estado.CapatazNuevosSinValidar} trabajador(es) después de la firma del Capataz/Maestro de obra. Debe volver a firmar antes de continuar.", 409);
        if (rol == "Ssoma" && estado.AutorizaFirmados < estado.EjecutantesFirmados)
            throw new AbrilException("Falta la firma del Residente/Producción (Autoriza) sobre toda la cuadrilla. Debe firmar antes del Visto Bueno SSOMA.", 409);
        if (rol == "Ssoma")
            await ExigirPetaresFirmadosParaSsoma(null, grupoId, estado.RequiereCapataz);

        var ids = await _repository.GetAtsIdsPendientesDeVisto(grupoId, rol, workerId, esAdmin);
        if (ids.Count == 0)
            throw new AbrilException("No hay ATS de esta cuadrilla pendientes de tu firma.", 409);

        var body = new AtsFirmarVistoRequestDto(); // la firma sale de la firma digital registrada
        foreach (var atsId in ids)
            await FirmarVistoComun(atsId, callerUserId, esAdmin, body, rol);
        if (rol == "Autoriza")
            await AvisarNivel(grupo.ProyectoId, "Ssoma", $"/ssoma/gestion/ats/grupo/{grupoId}");
        return ids.Count;
    }

    /// <summary>Cerrar la cuadrilla = declarar que ya se unieron todos. Recién ahí corre la cadena de validación
    /// (Capataz → Residente/Producción → SSOMA). Cierran: su autor, el Capataz/Maestro, el Residente, Producción, SSOMA o un admin.
    /// Al cerrar se avisa al primer nivel que debe firmar.</summary>
    public async Task CerrarGrupo(int id, int workerId, bool esAdmin)
    {
        var grupo = await _repository.GetGrupoEntidad(id) ?? throw new AbrilException("ATS grupal no encontrado.", 404);
        if (!esAdmin && !await _repository.EsAutorDeGrupo(id, workerId))
        {
            var (esResidente, esProduccion, esSsoma) = await RolesEnProyecto(grupo.ProyectoId, workerId);
            if (!esResidente && !esProduccion && !esSsoma && !await _repository.EsCapatazDeProyecto(workerId, grupo.ProyectoId))
                throw new AbrilException("Solo quien creó el ATS grupal, el Capataz/Maestro de obra, el Residente/Producción, SSOMA o un administrador pueden cerrarlo.", 403);
        }
        await EjecutarCierreGrupo(grupo, workerId);
    }

    /// <summary>El AUTOR de la cuadrilla, que no tiene cuenta (creó el ATS desde el QR de obra), la cierra desde su
    /// página pública con el token + su nombre + dígitos de DNI; también puede cerrarla el Capataz/Maestro del proyecto. El resto cierra con su cuenta.</summary>
    public async Task CerrarGrupoPublico(Guid token, AtsGrupoCerrarPublicoRequestDto body)
    {
        var grupo = await _repository.GetGrupoPorToken(token) ?? throw new AbrilException("Enlace inválido.", 404);
        if (!await _repository.DniCoincide(body.WorkerId, body.DniConfirmacion))
            throw new AbrilException("Los dígitos de DNI no coinciden con el trabajador seleccionado.", 400);
        if (grupo.CreadoPorWorkerId != body.WorkerId
            && !(await _repository.GetCapatacesDeProyecto(grupo.ProyectoId)).Any(c => c.WorkerId == body.WorkerId))
            throw new AbrilException("Solo quien creó la cuadrilla o el Capataz/Maestro de obra del proyecto pueden cerrarla desde aquí.", 403);
        await EjecutarCierreGrupo(grupo, body.WorkerId);
    }

    private async Task EjecutarCierreGrupo(SsAtsGrupo grupo, int workerId)
    {
        var id = grupo.Id;
        if (grupo.Estado is "Anulado" or "Reemplazado")
            throw new AbrilException($"Este ATS grupal está {grupo.Estado.ToLower()} — no se puede cerrar.", 409);
        if (grupo.Estado == "Cerrado")
            throw new AbrilException("Este ATS grupal ya está cerrado.", 409);

        var estado = await _repository.GetEstadoGrupo(id);
        if (estado is null || estado.EjecutantesFirmados == 0)
            throw new AbrilException("Todavía no firmó ningún trabajador de la cuadrilla — no se puede cerrar.", 409);

        await _repository.CerrarGrupo(id);
        await _repository.AgregarEventoGrupo(id, "Cerrado", workerId, null);
        await AvisarNivel(grupo.ProyectoId, estado.RequiereCapataz ? "Capataz" : "Autoriza", $"/ssoma/gestion/ats/grupo/{id}");
    }

    /// <summary>SSOMA firma al final: no puede dar el Visto Bueno del ATS mientras falten firmas previas en sus PETAR.</summary>
    private async Task ExigirPetaresFirmadosParaSsoma(int? atsId, int? atsGrupoId, bool requiereCapataz)
    {
        var faltan = await _repository.GetPetaresSinValidacionPrevia(atsId, atsGrupoId, requiereCapataz);
        if (faltan.Count > 0)
            throw new AbrilException("No se puede dar el Visto Bueno SSOMA: faltan firmas en el PETAR — " + string.Join("; ", faltan) + ". SSOMA firma al final.", 409);
    }

    /// <summary>La validación solo corre con la cuadrilla cerrada: mientras esté abierta todavía puede sumarse gente.</summary>
    private async Task ExigirGrupoCerrado(int grupoId)
    {
        var g = await _repository.GetGrupoEntidad(grupoId) ?? throw new AbrilException("ATS grupal no encontrado.", 404);
        if (g.Estado != "Cerrado")
            throw new AbrilException("Cierra la cuadrilla antes de validar: todavía puede sumarse gente.", 409);
    }

    /// <summary>Reabre un grupo cerrado el MISMO día (para sumar a alguien que faltó) — solo su autor o el admin,
    /// y queda registrado quién lo hizo. Las firmas de VALIDACIÓN (Capataz, Autoriza, SSOMA, PETAR) se invalidan: hay que
    /// volver a firmar sobre la cuadrilla final (quién había firmado queda en el evento).</summary>
    public async Task ReabrirGrupo(int grupoId, int workerId, bool esAdmin)
    {
        var grupo = await _repository.GetGrupoEntidad(grupoId) ?? throw new AbrilException("ATS grupal no encontrado.", 404);
        if (!esAdmin && !await _repository.EsAutorDeGrupo(grupoId, workerId))
        {
            var (esResidente, esProduccion, esSsoma) = await RolesEnProyecto(grupo.ProyectoId, workerId);
            if (!esResidente && !esProduccion && !esSsoma)
                throw new AbrilException("Solo quien creó el ATS grupal, el Residente/Producción, SSOMA o un administrador pueden reabrirlo.", 403);
        }
        if (grupo.Estado == "Anulado")
            throw new AbrilException("Este ATS grupal está anulado — no se puede reabrir.", 409);
        if (grupo.Estado == "Activo")
            throw new AbrilException("Este ATS grupal ya está abierto.", 409);
        if (grupo.Fecha != DateOnly.FromDateTime(DateTime.Today))
            throw new AbrilException("Solo se puede reabrir el mismo día. Si es otra jornada, crea un ATS grupal nuevo.", 409);

        var firmasInvalidadas = await _repository.ReabrirGrupo(grupoId, grupo.Fecha.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc));
        await _repository.AgregarEventoGrupo(grupoId, "Reabierto", workerId, firmasInvalidadas);
    }

    /// <summary>Contenido del grupo con la forma de un ATS — para precargar el wizard al corregir (?corregirGrupo=id).</summary>
    public async Task<AtsResponseDto> GetContenidoGrupo(int grupoId, int workerId, bool esAdmin)
    {
        await GetEstadoGrupo(grupoId, workerId, esAdmin); // valida acceso
        var g = await _repository.GetGrupoEntidad(grupoId) ?? throw new AbrilException("ATS grupal no encontrado.", 404);
        return new AtsResponseDto
        {
            ProyectoId = g.ProyectoId,
            PlantillaId = g.PlantillaId,
            Actividad = g.Actividad,
            TorreNombre = g.TorreNombre,
            Pisos = g.Pisos,
            Lugar = g.Lugar,
            Pasos = g.Pasos.OrderBy(p => p.Orden).Select(p => new AtsPasoResponseDto { PasoId = p.PasoId, CategoriaNombre = p.CategoriaNombre, Texto = p.Texto, Aplica = p.Aplica }).ToList(),
            Epps = g.Epps.Select(e => e.Nombre).ToList(),
            Herramientas = g.Herramientas.Select(h => h.Nombre).ToList(),
            Riesgos = g.RiesgosDetalle.OrderBy(r => r.Orden).Select(r => new AtsRiesgoDetalleResponseDto
            {
                PeligroId = r.PeligroId, RiesgoId = r.RiesgoId, PeligroNombre = r.PeligroNombre, RiesgoNombre = r.RiesgoNombre,
                RiesgoBase = r.RiesgoBase, Controles = r.Controles, RiesgoResidual = r.RiesgoResidual,
            }).ToList(),
        };
    }

    // ── Observaciones ──────────────────────────────────────────────────────

    private async Task ExigirSinObservacionesAbiertas(int? atsId, int? grupoId)
    {
        var n = 0;
        if (atsId.HasValue) n += await _repository.ContarObservacionesAbiertas(atsId, null);
        if (grupoId.HasValue) n += await _repository.ContarObservacionesAbiertas(null, grupoId);
        if (n > 0)
            throw new AbrilException($"Hay {n} observación(es) abierta(s) — deben resolverse antes de firmar la validación.", 409);
    }

    /// <summary>Quién puede observar/resolver: observan Capataz del proyecto, Residente, Producción, SSOMA y admin (no
    /// sobre su propio ATS, salvo admin); resuelven además el dueño del ATS / autor del grupo.</summary>
    private async Task<(bool PuedeObservar, bool PuedeResolver, string Rol)> PermisosObservacion(int proyectoId, int ownerWorkerId, int workerId, bool esAdmin)
    {
        var (esResidente, esProduccion, esSsoma) = await RolesEnProyecto(proyectoId, workerId);
        var esCapataz = await _repository.EsCapatazDeProyecto(workerId, proyectoId);
        var esOwner = ownerWorkerId == workerId;
        var rol = esSsoma ? "Ssoma" : (esResidente || esProduccion) ? "Autoriza" : esCapataz ? "Capataz" : "Admin";
        var puedeObservar = (esAdmin || esResidente || esProduccion || esSsoma || esCapataz) && (!esOwner || esAdmin);
        var puedeResolver = esAdmin || esOwner || esResidente || esProduccion || esSsoma;
        return (puedeObservar, puedeResolver, rol);
    }

    private static AtsObservacionDto ObsDto(SsAtsObservacion o) => new()
    {
        Id = o.Id, Rol = o.Rol, AutorNombre = o.AutorNombre, Texto = o.Texto, Estado = o.Estado,
        Respuesta = o.Respuesta, ResueltaPorNombre = o.ResueltaPorNombre, CreatedAt = o.CreatedAt, ResueltaEn = o.ResueltaEn,
    };

    public async Task<AtsObservacionesDto> GetObservacionesAts(int atsId, int callerUserId, bool esAdmin)
    {
        var ats = await _repository.GetEntidad(atsId) ?? throw new AbrilException("ATS no encontrado.", 404);
        var workerId = await _repository.ResolverWorkerIdAsync(callerUserId);
        var (puedeObservar, puedeResolver, _) = await PermisosObservacion(ats.ProyectoId, ats.WorkerId, workerId, esAdmin);
        var lista = await _repository.GetObservaciones(atsId, null);
        return new AtsObservacionesDto
        {
            Observaciones = lista.Select(ObsDto).ToList(),
            PuedeObservar = puedeObservar && ats.Estado == "Firmado",
            PuedeResolver = puedeResolver,
        };
    }

    public async Task<AtsObservacionesDto> GetObservacionesGrupo(int grupoId, int callerUserId, bool esAdmin)
    {
        var grupo = await _repository.GetGrupoEntidad(grupoId) ?? throw new AbrilException("ATS grupal no encontrado.", 404);
        var workerId = await _repository.ResolverWorkerIdAsync(callerUserId);
        var (puedeObservar, puedeResolver, _) = await PermisosObservacion(grupo.ProyectoId, grupo.CreadoPorWorkerId, workerId, esAdmin);
        var lista = await _repository.GetObservaciones(null, grupoId);
        return new AtsObservacionesDto
        {
            Observaciones = lista.Select(ObsDto).ToList(),
            PuedeObservar = puedeObservar && grupo.Estado is "Activo" or "Cerrado",
            PuedeResolver = puedeResolver,
        };
    }

    private static string ValidarTextoObservacion(string? texto)
    {
        var t = texto?.Trim() ?? string.Empty;
        if (t.Length < 5) throw new AbrilException("Describe la observación (mínimo 5 caracteres).", 400);
        return t.Length > 1000 ? t[..1000] : t;
    }

    public async Task CrearObservacionAts(int atsId, string texto, int callerUserId, bool esAdmin)
    {
        var t = ValidarTextoObservacion(texto);
        var ats = await _repository.GetEntidad(atsId) ?? throw new AbrilException("ATS no encontrado.", 404);
        if (ats.Estado != "Firmado") throw new AbrilException("Solo se observa un ATS ya firmado.", 409);
        var workerId = await _repository.ResolverWorkerIdAsync(callerUserId);
        var (puedeObservar, _, rol) = await PermisosObservacion(ats.ProyectoId, ats.WorkerId, workerId, esAdmin);
        if (!puedeObservar) throw new AbrilException("No tienes permiso para observar este ATS.", 403);

        var (nombre, _) = await _repository.GetNombreYCargo(workerId);
        await _repository.AgregarObservacion(new SsAtsObservacion { AtsId = atsId, Rol = rol, AutorWorkerId = workerId, AutorNombre = nombre, Texto = t });
        await AvisarObservacion(ats.WorkerId, $"{nombre} observó tu ATS", t, "/ssoma/gestion/ats");
    }

    public async Task CrearObservacionGrupo(int grupoId, string texto, int callerUserId, bool esAdmin)
    {
        var t = ValidarTextoObservacion(texto);
        var grupo = await _repository.GetGrupoEntidad(grupoId) ?? throw new AbrilException("ATS grupal no encontrado.", 404);
        if (grupo.Estado is not ("Activo" or "Cerrado")) throw new AbrilException("Este ATS grupal ya no admite observaciones.", 409);
        var workerId = await _repository.ResolverWorkerIdAsync(callerUserId);
        var (puedeObservar, _, rol) = await PermisosObservacion(grupo.ProyectoId, grupo.CreadoPorWorkerId, workerId, esAdmin);
        if (!puedeObservar) throw new AbrilException("No tienes permiso para observar este ATS grupal.", 403);

        var (nombre, _) = await _repository.GetNombreYCargo(workerId);
        await _repository.AgregarObservacion(new SsAtsObservacion { AtsGrupoId = grupoId, Rol = rol, AutorWorkerId = workerId, AutorNombre = nombre, Texto = t });
        await AvisarObservacion(grupo.CreadoPorWorkerId, $"{nombre} observó el ATS grupal", t, $"/ssoma/gestion/ats/grupo/{grupoId}");
    }

    public async Task ResolverObservacion(int observacionId, string respuesta, int callerUserId, bool esAdmin)
    {
        var r = ValidarTextoObservacion(respuesta);
        var obs = await _repository.GetObservacion(observacionId) ?? throw new AbrilException("Observación no encontrada.", 404);
        if (obs.Estado != "Abierta") throw new AbrilException("Esta observación ya fue resuelta.", 409);

        var workerId = await _repository.ResolverWorkerIdAsync(callerUserId);
        int proyectoId, ownerWorkerId;
        if (obs.AtsId.HasValue)
        {
            var ats = await _repository.GetEntidad(obs.AtsId.Value) ?? throw new AbrilException("ATS no encontrado.", 404);
            (proyectoId, ownerWorkerId) = (ats.ProyectoId, ats.WorkerId);
        }
        else
        {
            var g = await _repository.GetGrupoEntidad(obs.AtsGrupoId!.Value) ?? throw new AbrilException("ATS grupal no encontrado.", 404);
            (proyectoId, ownerWorkerId) = (g.ProyectoId, g.CreadoPorWorkerId);
        }
        var (_, puedeResolver, _) = await PermisosObservacion(proyectoId, ownerWorkerId, workerId, esAdmin);
        if (!puedeResolver && obs.AutorWorkerId != workerId)
            throw new AbrilException("No tienes permiso para resolver esta observación.", 403);

        var (nombre, _) = await _repository.GetNombreYCargo(workerId);
        await _repository.ResolverObservacion(observacionId, r, workerId, nombre);
        await AvisarObservacion(obs.AutorWorkerId, $"{nombre} resolvió tu observación", r,
            obs.AtsGrupoId.HasValue ? $"/ssoma/gestion/ats/grupo/{obs.AtsGrupoId}" : "/ssoma/gestion/ats");
    }

    /// <summary>Aviso por la campanita al dueño del ATS / autor del grupo / autor de la observación — best-effort.</summary>
    private async Task AvisarObservacion(int destinatarioWorkerId, string titulo, string descripcion, string referencia)
    {
        try
        {
            var email = await _repository.GetEmailCorporativoWorker(destinatarioWorkerId);
            if (string.IsNullOrWhiteSpace(email)) return;
            await _notificacionesService.CrearPorCorreosAsync(
                "ATS_PENDIENTE_FIRMA",
                [email],
                origenUserId: null,
                items: [new NuevaNotificacionDto { Titulo = titulo, Subtitulo = "ATS", Descripcion = descripcion, Referencia = referencia }]);
        }
        catch { /* best-effort */ }
    }

    private async Task<(bool EsResidente, bool EsProduccion, bool EsSsoma)> RolesEnProyecto(int proyectoId, int workerId)
    {
        var responsables = await _repository.GetResponsables(proyectoId);
        var email = await _repository.GetEmailCorporativoWorker(workerId);
        var esResidente = await EsResidenteDelProyecto(proyectoId, workerId);
        var esProduccion = !esResidente && await _repository.EsProduccionDeProyecto(workerId, proyectoId);
        var esSsoma = (email != null && responsables.SsomaEmails.Contains(email, StringComparer.OrdinalIgnoreCase))
                      || await _repository.EsPrevencionistaAbril(workerId);
        return (esResidente, esProduccion, esSsoma);
    }

    /// <summary>
    /// ¿Es la persona de esta ficha el residente del proyecto en Configuración → Proyectos? Por
    /// persona y no por ficha, como el resto del ATS (PLAN-RESIDENTES, Paso 5e): una persona con
    /// dos fichas vivas por reingreso sigue siendo el residente. Para los métodos que reciben la
    /// ficha y no el usuario; los que tienen el usuario le preguntan directo a _residentes.
    /// </summary>
    private async Task<bool> EsResidenteDelProyecto(int proyectoId, int workerId)
    {
        var residente = await _residentes.ResidenteDelProyectoAsync(proyectoId);
        if (residente == null) return false;
        if (residente.WorkerId == workerId) return true;
        return residente.PersonId != null && residente.PersonId == await _repository.GetPersonIdDeWorker(workerId);
    }

    private static string ValidarMotivo(string? motivo)
    {
        var m = motivo?.Trim() ?? string.Empty;
        if (m.Length < 10)
            throw new AbrilException("Escribe el motivo de la anulación (mínimo 10 caracteres) — queda registrado.", 400);
        return m.Length > 300 ? m[..300] : m;
    }

    /// <summary>Anula UN ATS firmado (nunca se borra: queda Estado "Anulado" con motivo, quién y cuándo). Quienes
    /// validan en el proyecto (Residente/Producción/SSOMA) o un admin; si el ATS es de una cuadrilla, también
    /// su autor mientras la cadena no tenga firmas.</summary>
    public async Task AnularAts(int atsId, string motivo, int callerUserId, bool esAdmin)
    {
        var m = ValidarMotivo(motivo);
        var ats = await _repository.GetEntidad(atsId) ?? throw new AbrilException("ATS no encontrado.", 404);
        if (ats.Estado == "Anulado") throw new AbrilException("Este ATS ya está anulado.", 409);
        if (ats.Estado != "Firmado") throw new AbrilException("Solo se anula un ATS ya firmado (un borrador simplemente se descarta).", 409);

        var workerId = await _repository.ResolverWorkerIdAsync(callerUserId);
        var (esResidente, esProduccion, esSsoma) = await RolesEnProyecto(ats.ProyectoId, workerId);
        var esAutorGrupo = ats.AtsGrupoId.HasValue && await _repository.EsAutorDeGrupo(ats.AtsGrupoId.Value, workerId)
                           && !await _repository.GrupoTieneValidaciones(ats.AtsGrupoId.Value);
        if (!esAdmin && !esResidente && !esProduccion && !esSsoma && !esAutorGrupo)
            throw new AbrilException("No tienes permiso para anular este ATS.", 403);

        await _repository.AnularAts(atsId, m, workerId);

        var hashAnterior = await _repository.GetUltimoHashAuditLog(atsId);
        var hash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"{hashAnterior}|Anulado|{atsId}|{DateTime.UtcNow:O}")));
        await _repository.AgregarAuditLog(atsId, "Anulado", callerUserId, null, m, hashAnterior ?? string.Empty, hash);
    }

    public async Task AnularGrupo(int grupoId, string motivo, int callerUserId, bool esAdmin)
    {
        var m = ValidarMotivo(motivo);
        var grupo = await _repository.GetGrupoEntidad(grupoId) ?? throw new AbrilException("ATS grupal no encontrado.", 404);
        if (grupo.Estado == "Anulado") throw new AbrilException("Este ATS grupal ya está anulado.", 409);

        var workerId = await _repository.ResolverWorkerIdAsync(callerUserId);
        var (esResidente, esProduccion, esSsoma) = await RolesEnProyecto(grupo.ProyectoId, workerId);
        var esAutorSinValidar = await _repository.EsAutorDeGrupo(grupoId, workerId) && !await _repository.GrupoTieneValidaciones(grupoId);
        if (!esAdmin && !esResidente && !esProduccion && !esSsoma && !esAutorSinValidar)
            throw new AbrilException("No tienes permiso para anular este ATS grupal.", 403);

        var n = await _repository.AnularGrupo(grupoId, m, workerId);
        await _repository.AgregarEventoGrupo(grupoId, "Anulado", workerId, $"{m} ({n} ATS)");
    }

    public async Task<List<AtsGrupoWorkerOpcionDto>> GetCandidatosGrupo(int grupoId, int workerId, bool esAdmin)
    {
        var grupo = await _repository.GetGrupoEntidad(grupoId) ?? throw new AbrilException("ATS grupal no encontrado.", 404);
        if (!esAdmin && !await _repository.EsAutorDeGrupo(grupoId, workerId))
            throw new AbrilException("Solo quien creó el ATS grupal puede definir sus integrantes.", 403);
        return await _repository.GetWorkersParaAdhesion(grupo.ProyectoId);
    }

    public async Task SetIntegrantesGrupo(int grupoId, List<int> workerIds, int workerId, bool esAdmin)
    {
        var grupo = await _repository.GetGrupoEntidad(grupoId) ?? throw new AbrilException("ATS grupal no encontrado.", 404);
        if (!esAdmin && !await _repository.EsAutorDeGrupo(grupoId, workerId))
            throw new AbrilException("Solo quien creó el ATS grupal puede definir sus integrantes.", 403);

        // Solo trabajadores del proyecto del grupo habilitados para firmar.
        var validos = (await _repository.GetWorkersParaAdhesion(grupo.ProyectoId)).Select(w => w.WorkerId).ToHashSet();
        await _repository.SetIntegrantes(grupoId, workerIds.Where(validos.Contains).ToList());
    }

    /// <summary>Para quien ya firmó el ATS y vuelve a entrar al link (p. ej. para firmar un PETAR creado después):
    /// confirma identidad con el DNI y devuelve el id de SU ATS firmado en este grupo, o null si no ha firmado.</summary>
    public async Task<int?> GetMiAtsPublico(Guid token, AtsGrupoMiAtsRequestDto body)
    {
        var grupo = await _repository.GetGrupoPorToken(token) ?? throw new AbrilException("Enlace inválido.", 404);
        if (grupo.QrExpiraEn < DateTime.UtcNow)
            throw new AbrilException("Este enlace venció — el ATS grupal era para otra jornada.", 409);
        if (!await _repository.DniCoincide(body.WorkerId, body.DniConfirmacion))
            throw new AbrilException("Los dígitos de DNI no coinciden con el trabajador seleccionado.", 400);
        return await _repository.GetAtsIdDeGrupo(grupo.Id, body.WorkerId, "Firmado");
    }

    /// <summary>Página pública que abre el QR — sin login. "Válido" exige grupo existente, Activo,
    /// y dentro de la ventana de vigencia del token (no acotado a proyecto acá: eso ya lo filtra
    /// el propio token, que nace ligado a un solo grupo).</summary>
    public async Task<AtsGrupoResumenPublicoDto> GetResumenPublico(Guid token)
    {
        var grupo = await _repository.GetGrupoPorToken(token);
        if (grupo is null)
            return new AtsGrupoResumenPublicoDto { Valido = false, MotivoInvalido = "Este enlace no corresponde a ningún ATS grupal." };
        if (grupo.Estado != "Activo")
            return new AtsGrupoResumenPublicoDto { Valido = false, MotivoInvalido = "Este ATS grupal ya fue cerrado por quien lo creó." };
        if (grupo.QrExpiraEn < DateTime.UtcNow)
            return new AtsGrupoResumenPublicoDto { Valido = false, MotivoInvalido = "Este enlace venció — pide uno nuevo para el día de hoy." };

        return new AtsGrupoResumenPublicoDto
        {
            Valido = true,
            ProyectoNombre = grupo.Proyecto?.ProjectDescription,
            Actividad = grupo.Actividad,
            TorreNombre = grupo.TorreNombre,
            Pisos = grupo.Pisos,
            Lugar = grupo.Lugar,
            Fecha = grupo.Fecha,
            Epps = grupo.Epps.Select(e => e.Nombre).ToList(),
            Herramientas = grupo.Herramientas.Select(h => h.Nombre).ToList(),
            Riesgos = grupo.RiesgosDetalle.OrderBy(r => r.Orden).Select(r => new AtsRiesgoDetalleResponseDto
            {
                PeligroId = r.PeligroId,
                RiesgoId = r.RiesgoId,
                PeligroNombre = r.PeligroNombre,
                RiesgoNombre = r.RiesgoNombre,
                RiesgoBase = r.RiesgoBase,
                Controles = r.Controles,
                RiesgoResidual = r.RiesgoResidual,
            }).ToList(),
        };
    }

    public async Task<List<AtsGrupoWorkerOpcionDto>> GetWorkersParaAdhesion(Guid token)
    {
        var grupo = await _repository.GetGrupoPorToken(token) ?? throw new AbrilException("Enlace inválido.", 404);
        var opciones = await _repository.GetWorkersParaAdhesion(grupo.ProyectoId);
        var yaFirmaron = await _repository.GetWorkerIdsAdheridos(grupo.Id);
        foreach (var o in opciones) o.YaFirmo = yaFirmaron.Contains(o.WorkerId);
        return opciones;
    }

    /// <summary>Firma liviana de adhesión — sin login (el token del QR es el único candado). El
    /// DNI corto es fricción mínima contra "elegir cualquier nombre de la lista"; la selfie+geo+
    /// firma es la misma prueba de presencia física que el ATS individual. Reusa Firmar() tal cual
    /// para no duplicar la lógica de consentimiento/hash/selfie-duplicada/aviso a responsables.</summary>
    public Task<int> UnirseAGrupo(Guid token, AtsGrupoUnirseRequestDto body, string? ipOrigen, string? userAgent)
        => UnirseCore(token, body, ipOrigen, userAgent, validarDni: true);

    /// <summary>Quien YA inició sesión firma como sí mismo: su identidad la da el usuario, no hace falta elegir nombre ni
    /// confirmar el DNI (esos pasos existen solo para quien entra por el QR sin cuenta).</summary>
    public async Task<int> UnirseAGrupoLogueado(Guid token, int callerUserId, AtsGrupoUnirseRequestDto body, string? ipOrigen, string? userAgent)
    {
        body.WorkerId = await _repository.ResolverWorkerIdAsync(callerUserId);
        return await UnirseCore(token, body, ipOrigen, userAgent, validarDni: false);
    }

    public async Task<int?> GetMiAtsLogueado(Guid token, int callerUserId)
    {
        var grupo = await _repository.GetGrupoPorToken(token) ?? throw new AbrilException("Enlace inválido.", 404);
        var workerId = await _repository.ResolverWorkerIdAsync(callerUserId);
        return await _repository.GetAtsIdDeGrupo(grupo.Id, workerId, "Firmado");
    }

    private async Task<int> UnirseCore(Guid token, AtsGrupoUnirseRequestDto body, string? ipOrigen, string? userAgent, bool validarDni)
    {
        var grupo = await _repository.GetGrupoPorToken(token) ?? throw new AbrilException("Enlace inválido.", 404);
        if (grupo.Estado != "Activo")
            throw new AbrilException("Este ATS grupal ya fue cerrado por quien lo creó.", 409);
        if (grupo.QrExpiraEn < DateTime.UtcNow)
            throw new AbrilException("Este enlace venció — pide uno nuevo para el día de hoy.", 409);

        if (validarDni && !await _repository.DniCoincide(body.WorkerId, body.DniConfirmacion))
            throw new AbrilException("Los dígitos de DNI no coinciden con el trabajador seleccionado.", 400);

        var opciones = await _repository.GetWorkersParaAdhesion(grupo.ProyectoId);
        if (!opciones.Any(o => o.WorkerId == body.WorkerId))
            throw new AbrilException("Este trabajador no está habilitado para firmar en este proyecto.", 403);

        await ExigirAutorizacionPermiso(body.WorkerId);
        await ObtenerFirmaDigitalObligatoria(body.WorkerId); // antes de crear el borrador, para no dejarlo huérfano

        // Una sola adhesión por trabajador: si ya firmó, no se crea otro ATS (el flujo público lo manda a los PETAR).
        if (await _repository.GetAtsIdDeGrupo(grupo.Id, body.WorkerId, "Firmado") is not null)
            throw new AbrilException("Ya firmaste este ATS grupal.", 409);

        // ¿La cuadrilla ya tenía firmas de la cadena? Entonces este nuevo integrante las deja pendientes → se avisa.
        var teniaValidaciones = await _repository.GrupoTieneValidaciones(grupo.Id);

        // Reutiliza un borrador previo del mismo trabajador (firma que falló a medias) en vez de dejar huérfanos.
        var atsId = await _repository.GetAtsIdDeGrupo(grupo.Id, body.WorkerId, "Borrador")
                    ?? await _repository.CrearDesdeGrupo(body.WorkerId, grupo);

        var firmarDto = new AtsFirmarRequestDto
        {
            SelfieBase64 = body.SelfieBase64,
            FirmaBase64 = body.FirmaBase64,
            HoraDispositivo = body.HoraDispositivo,
            Lat = body.Lat,
            Lng = body.Lng,
            PrecisionMetros = body.PrecisionMetros,
            AceptaConsentimiento = body.AceptaConsentimiento,
        };
        await Firmar(atsId, body.WorkerId, firmarDto, ipOrigen, userAgent);

        if (teniaValidaciones)
            await AvisarNuevoEnGrupoValidado(grupo, body.WorkerId);

        return atsId;
    }

    /// <summary>Se sumó alguien a una cuadrilla cuya cadena ya estaba (en parte) firmada: Residente/SSOMA y el
    /// Capataz deben volver a validar. Aviso por la campanita; best-effort, no bloquea la firma del trabajador.</summary>
    private async Task AvisarNuevoEnGrupoValidado(SsAtsGrupo grupo, int workerIdNuevo)
    {
        try
        {
            var (nombre, _) = await _repository.GetNombreYCargo(workerIdNuevo);
            var responsables = await _repository.GetResponsables(grupo.ProyectoId);
            var destinatarios = new List<string>();
            if (!string.IsNullOrWhiteSpace(responsables.ResidenteEmail)) destinatarios.Add(responsables.ResidenteEmail);
            destinatarios.AddRange(responsables.SsomaEmails);
            destinatarios.AddRange(await _repository.GetEmailsAvisoCadena(grupo.ProyectoId));
            if (grupo.CapatazFirmaUrl != null && grupo.CapatazWorkerId.HasValue)
            {
                var emailCapataz = await _repository.GetEmailPersonalAutorizacion(grupo.CapatazWorkerId.Value);
                if (!string.IsNullOrWhiteSpace(emailCapataz)) destinatarios.Add(emailCapataz);
            }
            if (destinatarios.Count == 0) return;

            await _notificacionesService.CrearPorCorreosAsync(
                "ATS_PENDIENTE_FIRMA",
                destinatarios,
                origenUserId: null,
                items: [new NuevaNotificacionDto
                {
                    Titulo = "Se sumó un trabajador a un ATS grupal ya validado",
                    Subtitulo = responsables.ProyectoNombre,
                    Descripcion = $"{nombre} firmó el ATS grupal \"{grupo.Actividad}\" después de la validación — hay que volver a firmar para incluirlo.",
                    Referencia = $"/ssoma/gestion/ats/grupo/{grupo.Id}",
                }]);
        }
        catch { /* best-effort */ }
    }

    // ── Firma única del Capataz por cuadrilla (link público, sin login) ─────

    /// <summary>Lo que ve el Capataz al abrir el link: quiénes ya adhirieron (su firma certifica a
    /// esa lista) y si su firma anterior sigue vigente o quedó pendiente por nuevos adheridos. No
    /// exige grupo Activo: el flujo natural es firmar DESPUÉS de que el autor cerró las adhesiones.</summary>
    public async Task<AtsGrupoCapatazPublicoDto> GetCapatazPublico(Guid token)
    {
        var grupo = await _repository.GetGrupoPorToken(token);
        if (grupo is null)
            return new AtsGrupoCapatazPublicoDto { Valido = false, MotivoInvalido = "Este enlace no corresponde a ningún ATS grupal." };
        if (grupo.QrExpiraEn < DateTime.UtcNow)
            return new AtsGrupoCapatazPublicoDto { Valido = false, MotivoInvalido = "Este enlace venció — el ATS grupal era para otra jornada." };
        var estado = await _repository.GetEstadoGrupo(grupo.Id);
        return new AtsGrupoCapatazPublicoDto
        {
            Valido = true,
            Abierta = grupo.Estado == "Activo",
            ProyectoNombre = grupo.Proyecto?.ProjectDescription,
            Actividad = grupo.Actividad,
            TrabajadoresAdheridos = estado?.TrabajadoresAdheridos ?? [],
            YaFirmo = grupo.CapatazFirmaUrl != null,
            Vigente = estado?.CapatazVigente ?? false,
            NuevosSinValidar = estado?.CapatazNuevosSinValidar ?? 0,
            Capataces = await _repository.GetCapatacesDeProyecto(grupo.ProyectoId),
        };
    }

    /// <summary>Capataz/Maestro CON cuenta firma el grupo completo desde la plataforma — su usuario ya
    /// lo identifica, así que no pide selfie/geo (sí quedan el usuario y la hora de servidor). Misma
    /// regla que el link público: solo con adheridos y solo si no está ya vigente.</summary>
    public async Task FirmarCapatazGrupoLogueado(int grupoId, int callerUserId, bool esAdmin, AtsFirmarVistoRequestDto body)
    {
        var grupo = await _repository.GetGrupoEntidad(grupoId) ?? throw new AbrilException("ATS grupal no encontrado.", 404);
        var workerId = await _repository.ResolverWorkerIdAsync(callerUserId);
        var firmaBytes = await ObtenerFirmaDigitalObligatoria(workerId);

        if (!esAdmin && !await _repository.EsCapatazDeProyecto(workerId, grupo.ProyectoId))
            throw new AbrilException("Solo el Capataz o Maestro de obra de este proyecto puede firmar este nivel.", 403);

        if (grupo.Estado is "Anulado" or "Reemplazado") throw new AbrilException($"Este ATS grupal está {grupo.Estado.ToLower()}.", 409);
        if (grupo.Estado != "Cerrado") throw new AbrilException("Cierra la cuadrilla antes de validar: todavía puede sumarse gente.", 409);
        await ExigirSinObservacionesAbiertas(null, grupoId);
        var estado = await _repository.GetEstadoGrupo(grupoId);
        if (estado is not null && !estado.RequiereCapataz)
            throw new AbrilException("Esta cuadrilla es de Staff/supervisores — no requiere firma de Capataz.", 409);
        if (estado is null || estado.EjecutantesFirmados == 0)
            throw new AbrilException("Todavía no firmó ningún trabajador — firma cuando al menos uno de la cuadrilla haya firmado.", 409);
        if (estado.RequierePetar && estado.TotalPetares == 0)
            throw new AbrilException("Este ATS tiene un riesgo que exige PETAR y todavía no se generó. Genera el PETAR de la cuadrilla antes de firmar.", 409);
        if (estado.CapatazVigente)
            throw new AbrilException("La firma de Capataz de este ATS grupal ya está vigente — no se sumó nadie nuevo desde entonces.", 409);

        var (nombre, cargo) = await _repository.GetNombreYCargo(workerId);
        var firmaHash = Convert.ToHexString(SHA256.HashData(firmaBytes));
        var firmaUrl = await SubirBytes("capatazgrupo", workerId, firmaBytes, "png");

        await _repository.FirmarCapatazGrupo(grupoId, workerId, nombre, cargo, firmaUrl, firmaHash, DateTime.UtcNow, null, null, null, null, null, null);
        await AvisarNivel(grupo.ProyectoId, "Autoriza", $"/ssoma/gestion/ats/grupo/{grupoId}");
    }

    public async Task FirmarCapatazPublico(Guid token, AtsGrupoCapatazFirmarRequestDto body)
    {
        if (string.IsNullOrWhiteSpace(body.SelfieBase64))
            throw new AbrilException("La selfie es obligatoria — es la prueba de que estás en obra.", 400);

        var grupo = await _repository.GetGrupoPorToken(token) ?? throw new AbrilException("Enlace inválido.", 404);
        if (grupo.QrExpiraEn < DateTime.UtcNow)
            throw new AbrilException("Este enlace venció — el ATS grupal era para otra jornada.", 409);

        if (grupo.Estado is "Anulado" or "Reemplazado") throw new AbrilException($"Este ATS grupal está {grupo.Estado.ToLower()}.", 409);
        if (grupo.Estado != "Cerrado") throw new AbrilException("La cuadrilla todavía está abierta: el autor debe cerrarla antes de que firmes.", 409);
        await ExigirSinObservacionesAbiertas(null, grupo.Id);
        var estado = await _repository.GetEstadoGrupo(grupo.Id);
        if (estado is not null && !estado.RequiereCapataz)
            throw new AbrilException("Esta cuadrilla es de Staff/supervisores — no requiere firma de Capataz.", 409);
        if (estado is null || estado.EjecutantesFirmados == 0)
            throw new AbrilException("Todavía no firmó ningún trabajador — firma cuando al menos uno de la cuadrilla haya firmado.", 409);
        if (estado.RequierePetar && estado.TotalPetares == 0)
            throw new AbrilException("Este ATS tiene un riesgo que exige PETAR y todavía no se generó. Genera el PETAR de la cuadrilla antes de firmar.", 409);
        if (estado.CapatazVigente)
            throw new AbrilException("La firma de Capataz de este ATS grupal ya está vigente — no se sumó nadie nuevo desde entonces.", 409);

        if (!await _repository.DniCoincide(body.WorkerId, body.DniConfirmacion))
            throw new AbrilException("Los dígitos de DNI no coinciden con el trabajador seleccionado.", 400);

        var capataces = await _repository.GetCapatacesDeProyecto(grupo.ProyectoId);
        if (!capataces.Any(c => c.WorkerId == body.WorkerId))
            throw new AbrilException("Solo el Capataz o Maestro de obra de este proyecto puede firmar este nivel.", 403);

        var (nombre, cargo) = await _repository.GetNombreYCargo(body.WorkerId);
        var firmaBytes = await ObtenerFirmaDigitalObligatoria(body.WorkerId);
        var firmaHash = Convert.ToHexString(SHA256.HashData(firmaBytes));
        var firmaUrl = await SubirBytes("capatazgrupo", body.WorkerId, firmaBytes, "png");

        var (selfieUrl, selfieHash) = await SubirImagenConHash("capatazselfie", body.WorkerId, body.SelfieBase64);

        await _repository.FirmarCapatazGrupo(grupo.Id, body.WorkerId, nombre, cargo, firmaUrl, firmaHash, DateTime.UtcNow,
            selfieUrl, selfieHash, body.Lat, body.Lng, body.PrecisionMetros, body.HoraDispositivo);
        await AvisarNivel(grupo.ProyectoId, "Autoriza", $"/ssoma/gestion/ats/grupo/{grupo.Id}");
    }

    // ── QR fijo por proyecto (crear ATS Grupal sin login) ──────────────────

    /// <summary>Idempotente: si el proyecto ya tiene un QR de creación, devuelve el mismo (se
    /// imprime/pega UNA vez en la obra). Lo pide cualquier usuario logueado con acceso al módulo
    /// de ATS — no es información sensible, es solo el link que ya va a estar pegado en físico.</summary>
    public async Task<string> GetOrCrearQrProyecto(int proyectoId)
    {
        var token = await _repository.GetOrCrearTokenProyecto(proyectoId);
        return token.ToString();
    }

    /// <summary>Página pública que abre el QR de obra — sin login, el token es el único candado.
    /// A diferencia de GetResumenPublico (adhesión a un grupo YA creado), acá todavía no hay
    /// contenido: solo valida que el proyecto exista y devuelve a quién puede elegir como "yo soy".</summary>
    public async Task<AtsGrupoProyectoPublicoDto> GetResumenProyectoPublico(Guid tokenProyecto)
    {
        var proyectoId = await _repository.GetProyectoPorTokenCrear(tokenProyecto);
        if (proyectoId is null)
            return new AtsGrupoProyectoPublicoDto { Valido = false, MotivoInvalido = "Este código no corresponde a ningún proyecto." };

        return new AtsGrupoProyectoPublicoDto
        {
            Valido = true,
            ProyectoNombre = await _repository.GetProyectoNombre(proyectoId.Value),
            Trabajadores = await _repository.GetWorkersParaAdhesion(proyectoId.Value),
        };
    }

    /// <summary>Mismos catálogos que GetInit (pasos por puesto, EPP, herramientas, peligros,
    /// plantillas) para que el wizard público funcione idéntico al logueado — pero el
    /// ProyectoActualId sale SIEMPRE del token (el trabajador puede figurar vinculado a otro
    /// proyecto en el sistema; acá importa el proyecto del QR que escaneó, no el suyo).</summary>
    public async Task<AtsInitDto> GetInitPublico(Guid tokenProyecto, AtsGrupoInitPublicoRequestDto body)
    {
        var proyectoId = await _repository.GetProyectoPorTokenCrear(tokenProyecto) ?? throw new AbrilException("Enlace inválido.", 404);
        if (!await _repository.DniCoincide(body.WorkerId, body.DniConfirmacion))
            throw new AbrilException("Los dígitos de DNI no coinciden con el trabajador seleccionado.", 400);

        var opciones = await _repository.GetWorkersParaAdhesion(proyectoId);
        if (!opciones.Any(o => o.WorkerId == body.WorkerId))
            throw new AbrilException("Este trabajador no está habilitado para firmar en este proyecto.", 403);

        var init = await GetInit(body.WorkerId);
        init.ProyectoActualId = proyectoId;
        return init;
    }

    /// <summary>Crea el ATS grupal desde la página pública — mismo gate de autorización de firma
    /// digital que la creación logueada (ExigirAutorizacionPermiso, dentro de CrearGrupo). El
    /// ProyectoId que venga en el contenido se SOBRESCRIBE con el del token: nunca confiar en lo
    /// que mande el cliente para decidir en qué proyecto queda el grupo.</summary>
    public async Task<AtsGrupoCrearResponseDto> CrearGrupoPublico(Guid tokenProyecto, AtsGrupoCrearPublicoRequestDto body)
    {
        var proyectoId = await _repository.GetProyectoPorTokenCrear(tokenProyecto) ?? throw new AbrilException("Enlace inválido.", 404);
        if (!await _repository.DniCoincide(body.WorkerId, body.DniConfirmacion))
            throw new AbrilException("Los dígitos de DNI no coinciden con el trabajador seleccionado.", 400);

        var opciones = await _repository.GetWorkersParaAdhesion(proyectoId);
        if (!opciones.Any(o => o.WorkerId == body.WorkerId))
            throw new AbrilException("Este trabajador no está habilitado para firmar en este proyecto.", 403);

        body.Contenido.ProyectoId = proyectoId;
        body.Contenido.GrupoAnteriorId = null; // reemplazar un grupo exige sesión y permisos — nunca desde el link público

        // Grupo armado sin conexión: el reintento con el mismo ClientId devuelve el grupo ya creado (no duplica).
        if (body.ClientId.HasValue)
        {
            var existente = await _repository.GetGrupoIdPorClientId(body.ClientId.Value);
            if (existente.HasValue)
            {
                var g = await _repository.GetGrupoEntidad(existente.Value) ?? throw new AbrilException("ATS grupal no encontrado.", 404);
                return await ConAvisoPetar(new AtsGrupoCrearResponseDto { Id = g.Id, QrToken = g.QrToken.ToString(), QrExpiraEn = g.QrExpiraEn }, avisar: false);
            }

            if (!body.CapturadoEn.HasValue)
                throw new AbrilException("Falta la hora en que se armó el ATS sin conexión.", 400);
            var capturado = DateTime.SpecifyKind(body.CapturadoEn.Value, DateTimeKind.Utc);
            if (capturado > DateTime.UtcNow.AddMinutes(10))
                throw new AbrilException("La hora del dispositivo es posterior a la actual — revisa la fecha y hora del celular.", 400);
            if (capturado < DateTime.UtcNow.AddDays(-7))
                throw new AbrilException("Este ATS se armó hace más de 7 días y ya no se puede subir. Debe hacerse de nuevo.", 409);

            var creado = await CrearGrupo(body.WorkerId, body.Contenido);
            await _repository.MarcarGrupoOffline(creado.Id, body.ClientId.Value, capturado);
            var grupo = await _repository.GetGrupoEntidad(creado.Id) ?? throw new AbrilException("ATS grupal no encontrado.", 404);
            return await ConAvisoPetar(new AtsGrupoCrearResponseDto { Id = grupo.Id, QrToken = grupo.QrToken.ToString(), QrExpiraEn = grupo.QrExpiraEn }, avisar: true);
        }

        return await ConAvisoPetar(await CrearGrupo(body.WorkerId, body.Contenido), avisar: true);
    }

    /// <summary>Si el grupo recién creado exige PETAR, lo marca en la respuesta (la pantalla pública lo avisa) y
    /// notifica al Residente/Producción y SSOMA — desde el link público nadie es llevado a generarlo.</summary>
    private async Task<AtsGrupoCrearResponseDto> ConAvisoPetar(AtsGrupoCrearResponseDto res, bool avisar)
    {
        var estado = await _repository.GetEstadoGrupo(res.Id);
        res.RequierePetar = estado?.RequierePetar == true;
        if (!res.RequierePetar || !avisar || estado is null) return res;
        try
        {
            var grupo = await _repository.GetGrupoEntidad(res.Id);
            if (grupo is null) return res;
            var responsables = await _repository.GetResponsables(grupo.ProyectoId);
            var destinatarios = new List<string>();
            if (!string.IsNullOrWhiteSpace(responsables.ResidenteEmail)) destinatarios.Add(responsables.ResidenteEmail);
            destinatarios.AddRange(responsables.SsomaEmails);
            destinatarios.AddRange(await _repository.GetEmailsAvisoCadena(grupo.ProyectoId));
            if (destinatarios.Count > 0)
                await _notificacionesService.CrearPorCorreosAsync(
                    "ATS_PENDIENTE_FIRMA",
                    destinatarios,
                    origenUserId: null,
                    items: [new NuevaNotificacionDto
                    {
                        Titulo = "ATS grupal con riesgo que exige PETAR",
                        Subtitulo = responsables.ProyectoNombre,
                        Descripcion = $"El ATS grupal \"{grupo.Actividad}\" tiene un riesgo que exige PETAR y todavía no se generó. Sin PETAR no se puede validar.",
                        Referencia = $"/ssoma/gestion/ats/grupo/{grupo.Id}?petar=1",
                    }]);
        }
        catch { /* best-effort */ }
        return res;
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
        var fileName = $"{prefijo}_{workerId}_{DateTime.UtcNow:yyyyMMddHHmmssfff}.{ext}";
        var urls = await _fileStorageService.UploadFilesAsync([(stream, fileName)], container);
        return urls[0];
    }

    // Un solo HttpClient para todos los PDF (crear uno por imagen agotaba sockets y repetía el handshake) y caché
    // en memoria: las URLs de selfie/firma son inmutables, así el PDF de una cuadrilla grande no vuelve a bajar
    // y reducir decenas de imágenes cada vez que se abre la vista previa.
    private static readonly HttpClient _httpImagenes = new() { Timeout = TimeSpan.FromSeconds(20) };
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte[]> _cacheImagenes = new();
    private const int MaxImagenesEnCache = 400;
    private const int LadoMaxImagenPdf = 640;

    private static async Task<byte[]?> DescargarBytes(string url)
    {
        if (_cacheImagenes.TryGetValue(url, out var cacheado)) return cacheado;
        try
        {
            var bytes = await _httpImagenes.GetByteArrayAsync(url);
            bytes = ReducirImagen(bytes);
            if (_cacheImagenes.Count >= MaxImagenesEnCache) _cacheImagenes.Clear();
            _cacheImagenes[url] = bytes;
            return bytes;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Las selfies llegan del celular a resolución completa (varios MB): en el PDF se ven igual a 640 px y
    /// QuestPDF tarda mucho menos en incrustarlas. Si no se puede procesar, se usa la imagen original.</summary>
    private static byte[] ReducirImagen(byte[] original)
    {
        try
        {
            if (original.Length < 60_000) return original;
            using var img = SixLabors.ImageSharp.Image.Load(original);
            var formato = img.Metadata.DecodedImageFormat;
            if (img.Width <= LadoMaxImagenPdf && img.Height <= LadoMaxImagenPdf) return original;
            img.Mutate(x => x.Resize(new SixLabors.ImageSharp.Processing.ResizeOptions
            {
                Size = new SixLabors.ImageSharp.Size(LadoMaxImagenPdf, LadoMaxImagenPdf),
                Mode = SixLabors.ImageSharp.Processing.ResizeMode.Max,
            }));
            using var ms = new MemoryStream();
            if (formato is SixLabors.ImageSharp.Formats.Png.PngFormat) img.SaveAsPng(ms);
            else img.SaveAsJpeg(ms, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder { Quality = 80 });
            return ms.ToArray();
        }
        catch
        {
            return original;
        }
    }
}
