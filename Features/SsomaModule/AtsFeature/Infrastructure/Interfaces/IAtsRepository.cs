using Abril_Backend.Features.SsomaModule.AtsFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.AtsFeature.Infrastructure.Models;

namespace Abril_Backend.Features.SsomaModule.AtsFeature.Infrastructure.Interfaces;

public interface IAtsRepository
{
    Task<int> ResolverWorkerIdAsync(int userId);
    Task<(int? PuestoId, int? ProyectoActualId)> GetPuestoYProyectoActual(int workerId);

    /// <summary>La persona de la ficha (null si la ficha no existe o está de baja).</summary>
    Task<int?> GetPersonIdDeWorker(int workerId);

    /// <summary>Carga del listado de ATS en un solo viaje: el proyecto actual del trabajador (el de
    /// su vinculación vigente, como <see cref="GetPuestoYProyectoActual"/>) y los proyectos, los
    /// mismos de <see cref="GetInit"/>.</summary>
    Task<AtsListaInitDto> GetListaInit(int workerId);

    /// <summary>Todo lo que necesita "Nuevo ATS" (y la administración de plantillas/pasos) en un
    /// solo viaje a la BD: puesto y proyecto actual del trabajador, proyectos, puestos, pasos de su
    /// puesto, peligros con riesgos, EPP, herramientas, plantillas, plantilla sugerida y
    /// consentimiento. esStaff decide los pasos igual que en <see cref="GetPasosParaPuesto"/>.</summary>
    Task<AtsInitDto> GetInit(int workerId, bool esStaff);

    /// <summary>esStaff = el trabajador que llena el ATS es Staff u Oficina Central — solo entonces
    /// se incluyen "Trabajos de gabinete" y "Supervisión y liberación en campo" (CategoriasUniversales),
    /// que son checklist de supervisión/oficina y no le corresponden a un obrero de cuadrilla.</summary>
    Task<List<AtsCategoriaPasoDto>> GetPasosParaPuesto(int? puestoId, bool esStaff);
    Task<AtsPasoDto> CrearPasoPersonalizado(int categoriaId, string texto);
    Task<List<AtsPeligroDto>> GetPeligrosConRiesgos();

    /// <summary>El Coordinador SSOMA marca manualmente qué riesgos exigen PETAR — nunca un valor
    /// por defecto del sistema, es una decisión de catálogo que le corresponde a la persona.</summary>
    Task SetRiesgoRequierePetar(int riesgoId, bool requierePetar);
    Task<bool> FaltaPetarObligatorio(int atsId);
    Task<List<AtsPlantillaDto>> GetPlantillasActivas();
    Task<List<AtsPuestoDto>> GetPuestos();
    Task<List<AtsPasoPuestoDto>> GetPasoPuestoMapeo();
    Task SetPasoPuestos(int pasoId, List<int> puestoIds);
    Task<List<AtsPlantillaPuestoDto>> GetPlantillaPuestoMapeo();
    Task SetPlantillaPuestos(int plantillaId, List<int> puestoIds);

    Task<int> Crear(int workerId, AtsGuardarRequestDto dto);
    Task Editar(int id, int workerId, AtsGuardarRequestDto dto);
    Task<SsAts?> GetEntidad(int id);
    Task<AtsResponseDto?> GetPorId(int id);

    /// <summary>Marca RequierePetar y llena Petares en cada dto — separado de ToDto porque cruza a
    /// ss_petar (otra feature) y a un chequeo de catálogo que no vale la pena repetir por fila.</summary>
    Task CompletarInfoPetar(List<AtsResponseDto> ats, Dictionary<int, (bool EsResidente, bool EsSsoma)> permisosPorAtsId);

    Task<bool> TieneConsentimiento(int workerId);
    Task RegistrarConsentimiento(int workerId, string? ipOrigen);

    // ── Autorización de uso de firma digital e imagen (firmada en físico) — gate para poder hacer ATS ────
    Task<bool> TieneAutorizacionPermiso(int workerId);
    Task SubirAutorizacionPermiso(int workerId, string archivoUrl, int? subidoPorUserId);
    Task<List<AtsAutorizacionTrabajadorDto>> GetTrabajadoresParaAutorizacion();
    Task<(string? FirmaDigitalUrl, string? Nombre, string? Dni)> GetFirmaDigitalAutorizacion(int workerId);
    Task CapturarFirmaDigitalAutorizacion(int workerId, string firmaUrl, string firmaHash, int capturadoPorUserId);

    Task Firmar(SsAts ats, string selfieUrl, string selfieHash, string firmaUrl, string firmaHash, DateTime horaServidor, AtsFirmarRequestDto body, string? ipOrigen, string? userAgent);

    /// <summary>Residente y correo(s) de Coordinador SSOMA del proyecto de un ATS — de acá salen
    /// tanto la autorización server-side de FirmarVisto cuanto los destinatarios del aviso por
    /// correo cuando el ejecutante firma.</summary>
    Task<AtsResponsablesDto> GetResponsables(int proyectoId);

    /// <summary>Ingeniero o Arquitecto de Producción vinculado ACTUALMENTE a ese proyecto (por
    /// puesto, no hay un campo dedicado como el de Residente) — también puede Autorizar.</summary>
    Task<bool> EsProduccionDeProyecto(int workerId, int proyectoId);

    /// <summary>Capataz o Maestro de obra vinculado ACTUALMENTE a ese proyecto (por puesto, mismo
    /// patrón que EsProduccionDeProyecto) — puede firmar el primer nivel de la cadena.</summary>
    Task<bool> EsCapatazDeProyecto(int workerId, int proyectoId);

    /// <summary>true si el ejecutante del ATS es obrero de obra (no Staff/Oficina Central) — de
    /// esto depende si se exige la firma de Capataz.</summary>
    Task<bool> EsObreroDeObra(int workerId);

    /// <summary>Cualquier prevencionista de Abril (puesto "Prevencionista", ContrataCasa="Casa"),
    /// sin importar el proyecto — puede dar Visto Bueno SSOMA en cualquier ATS que no sea el suyo.</summary>
    Task<bool> EsPrevencionistaAbril(int workerId);

    // ── Cuenta propia para Capataz / Maestro de obra ────────────────────────
    Task<bool> EsCapatazOMaestro(int workerId);
    Task<string?> GetEmailPersonalAutorizacion(int workerId);
    Task GuardarEmailPersonalAutorizacion(int workerId, string email);
    Task<bool> TieneUsuario(int workerId);
    Task<int?> GetRoleIdCapataz();
    Task<(string Dni, string Nombres, string ApellidoPaterno, string ApellidoMaterno, int? Telefono)?> GetDatosPersonaParaCuenta(int workerId);

    Task<string?> GetEmailDeUsuario(int userId);
    Task<string?> GetEmailCorporativoWorker(int workerId);
    /// <summary>Correos de quienes deben enterarse (popup al celular) de un ATS/PETAR pendiente de firma: todos los
    /// prevencionistas de Abril + Ing./Arq. de Producción vigentes del proyecto.</summary>
    Task<List<string>> GetEmailsAvisoCadena(int proyectoId);

    Task<(string Nombre, string? Dni)> GetNombreYDni(int workerId);

    /// <summary>Nombre completo y puesto (cargo) de un worker — usado para snapshotear quién firmó
    /// como "Capataz", "Autoriza" o "Visto Bueno SSOMA".</summary>
    Task<(string Nombre, string? Cargo)> GetNombreYCargo(int workerId);

    /// <summary>"Capataz", "Autoriza" o "Ssoma" — cuál de las firmas adicionales se está registrando.</summary>
    Task FirmarVisto(int atsId, string rol, int workerId, string nombre, string? cargo, string firmaUrl, string firmaHash, DateTime horaServidor);

    Task GuardarPdf(int id, string pdfUrl, string pdfHash);

    Task<string?> GetUltimoHashAuditLog(int atsId);
    Task AgregarAuditLog(int atsId, string evento, int? userId, string? ipOrigen, string? detalle, string hashAnterior, string hash);

    Task<bool> ExisteSelfieHash(string selfieHash);

    Task<AtsListResponseDto> Listar(AtsFiltroDto filtro);
    Task<AtsGrupoListResponseDto> ListarGrupos(AtsFiltroDto filtro);

    /// <summary>true = obrero de obra (no Staff/Oficina Central, no Capataz/Maestro) por cada worker — una
    /// sola consulta para toda la página del listado, en vez de una por trabajador distinto.</summary>
    Task<Dictionary<int, bool>> GetEsObreroDeObraBatch(IEnumerable<int> workerIds);

    // ── Administración de plantillas ────────────────────────────────────
    Task<int> CrearPlantilla(AtsPlantillaGuardarRequestDto dto);
    Task EditarPlantilla(int id, AtsPlantillaGuardarRequestDto dto);
    Task DesactivarPlantilla(int id);

    // ── Actividades/pasos por plantilla ──────────────────────────────────
    Task<List<AtsPlantillaActividadDto>> GetActividadesDePlantilla(int plantillaId);
    Task<int> CrearActividad(int plantillaId, string texto);
    Task EditarActividad(int actividadId, string texto);
    Task EliminarActividad(int actividadId);
    Task<int> CrearPaso(int actividadId, string texto);
    Task EditarPaso(int pasoId, string texto);
    Task EliminarPaso(int pasoId);
    Task SetActividadPeligros(int actividadId, List<int> peligroIds);

    // ── Controles sugeridos por riesgo ───────────────────────────────────
    Task<List<AtsRiesgoConControlesDto>> GetRiesgosConControles();
    Task<int> CrearControl(int riesgoId, string texto, string tipo);
    Task EditarControl(int controlId, string texto, string tipo);
    Task EliminarControl(int controlId);

    // ── ATS Grupal ────────────────────────────────────────────────────────
    Task<SsAtsGrupo> CrearGrupo(int creadoPorWorkerId, AtsGuardarRequestDto dto);
    Task<SsAtsGrupo?> GetGrupoPorToken(Guid token);
    Task<SsAtsGrupo?> GetGrupoEntidad(int id);
    Task<AtsGrupoEstadoDto?> GetEstadoGrupo(int id);
    Task<bool> EsAutorDeGrupo(int atsGrupoId, int workerId);
    Task<List<AtsGrupoPdfAdhesionDto>> GetAdhesionesParaPdf(int atsGrupoId);

    // ── Observaciones ──
    Task<List<SsAtsObservacion>> GetObservaciones(int? atsId, int? grupoId);
    Task<SsAtsObservacion?> GetObservacion(int id);
    Task AgregarObservacion(SsAtsObservacion o);
    Task ResolverObservacion(int id, string respuesta, int workerId, string nombre);
    Task<int> ContarObservacionesAbiertas(int? atsId, int? grupoId);
    Task<Dictionary<int, int>> ContarObservacionesAbiertasPorAts(IEnumerable<int> atsIds);
    Task ResolverObservacionesDeGrupo(int grupoId, string respuesta, int workerId, string nombre);

    Task AnularAts(int atsId, string motivo, int workerId);
    /// <summary>Anula el grupo y todos sus ATS adheridos (no borra nada). Devuelve cuántos ATS anuló.</summary>
    Task<int> AnularGrupo(int atsGrupoId, string motivo, int workerId);

    // ── Seguimiento de la cuadrilla ──
    Task<HashSet<int>> GetWorkerIdsAdheridos(int atsGrupoId);
    Task<int?> GetAtsIdDeGrupo(int atsGrupoId, int workerId, string estado);
    /// <summary>true si ya hay alguna validación de la cadena (Capataz/Autoriza/SSOMA) en la cuadrilla.</summary>
    Task<bool> GrupoTieneValidaciones(int atsGrupoId);
    Task ReabrirGrupo(int atsGrupoId, DateTime nuevaExpiracionUtc);
    Task AgregarEventoGrupo(int atsGrupoId, string evento, int? workerId, string? detalle);
    Task<string?> GetUltimoEventoGrupo(int atsGrupoId);
    Task<List<AtsGrupoIntegranteDto>> GetIntegrantes(int atsGrupoId);
    Task SetIntegrantes(int atsGrupoId, List<int> workerIds);
    /// <summary>Agrega un integrante esperado si todavía no está (idempotente).</summary>
    Task AgregarIntegrante(int atsGrupoId, int workerId);

    // ── Sin conexión ──
    Task<int?> GetGrupoIdPorClientId(Guid clientId);
    /// <summary>Marca el grupo como armado sin conexión: guarda el ClientId, la hora del dispositivo, fija la fecha de
    /// la jornada según esa hora y abre una ventana de adhesión de 72 h para sincronizar las firmas.</summary>
    Task MarcarGrupoOffline(int atsGrupoId, Guid clientId, DateTime capturadoEnUtc);

    /// <summary>Ids de los ATS Firmados del grupo a los que todavía les falta la firma de ese nivel
    /// ("Autoriza" o "Ssoma"), sin incluir los del propio firmante salvo que sea admin.</summary>
    Task<List<int>> GetAtsIdsPendientesDeVisto(int atsGrupoId, string rol, int callerWorkerId, bool incluirPropios);
    Task CerrarGrupo(int atsGrupoId);
    Task<List<AtsGrupoWorkerOpcionDto>> GetWorkersParaAdhesion(int proyectoId);
    Task<bool> DniCoincide(int workerId, string ultimosDigitos);

    /// <summary>Crea el ATS individual de este trabajador COPIANDO el contenido ya snapshoteado
    /// del grupo (no vuelve a resolver el catálogo) — deja Estado="Borrador", listo para que el
    /// servicio llame a Firmar() inmediatamente después, igual que el flujo individual normal.</summary>
    Task<int> CrearDesdeGrupo(int workerId, SsAtsGrupo grupo);

    // ── QR fijo por proyecto (crear ATS Grupal sin login) ───────────────────
    Task<Guid> GetOrCrearTokenProyecto(int proyectoId);
    Task<int?> GetProyectoPorTokenCrear(Guid token);
    Task<string?> GetProyectoNombre(int proyectoId);

    // ── Firma única del Capataz por cuadrilla (link público) ────────────────
    Task<List<AtsGrupoWorkerOpcionDto>> GetCapatacesDeProyecto(int proyectoId);
    Task FirmarCapatazGrupo(int grupoId, int workerId, string nombre, string? cargo, string firmaUrl, string firmaHash, DateTime horaServidor,
        string? selfieUrl, string? selfieHash, decimal? lat, decimal? lng, decimal? precisionMetros, DateTime? horaDispositivo);
}
