namespace Abril_Backend.Features.SsomaModule.AtsFeature.Application.Dtos;

// ── Catálogos ────────────────────────────────────────────────────────────────

public class AtsProyectoDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
}

public class AtsPasoDto
{
    public int Id { get; set; }
    public string Texto { get; set; } = string.Empty;
    /// <summary>Marca los ítems de trabajo de alto riesgo (andamios, elevador, anclajes) para
    /// que el wizard los agrupe aparte de las verificaciones básicas de rutina.</summary>
    public bool RequierePetar { get; set; }
}

/// <summary>Un paso de las categorías NO universales (Liberación de Seguridad/Producción/Calidad)
/// con los puestos que hoy lo ven, para la pantalla de administración.</summary>
public class AtsPasoPuestoDto
{
    public int PasoId { get; set; }
    public string CategoriaNombre { get; set; } = string.Empty;
    public string Texto { get; set; } = string.Empty;
    public List<int> PuestoIds { get; set; } = [];
}

/// <summary>Fila del listado del Coordinador SSOMA para subir la autorización de uso de firma
/// digital e imagen (firmada en físico) de cada trabajador — sin esto, el trabajador no puede
/// crear/editar un ATS (ver AtsService.Crear/Editar).</summary>
public class AtsAutorizacionTrabajadorDto
{
    public int WorkerId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Dni { get; set; }
    public int? ProyectoId { get; set; }
    public string? ProyectoNombre { get; set; }
    public string? ObraOficinaStaff { get; set; }
    public bool TieneFirmaDigital { get; set; }
    public bool TieneAutorizacion { get; set; }
    public DateTime? SubidoEn { get; set; }
    public string? ArchivoUrl { get; set; }
    public bool EsCapatazOMaestro { get; set; }
    public string? EmailPersonal { get; set; }
    public bool TieneUsuario { get; set; }
}

public class AtsAutorizacionFirmaDigitalRequestDto
{
    public string FirmaBase64 { get; set; } = string.Empty;
}

/// <summary>Correo personal del Capataz/Maestro de obra + aceptación de la declaración de uso
/// personal y exclusivo (también impresa en el PDF de la autorización).</summary>
public class AtsAutorizacionEmailRequestDto
{
    public string Email { get; set; } = string.Empty;
    public bool AceptaDeclaracion { get; set; }
}

public class AtsCategoriaPasoDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public List<AtsPasoDto> Pasos { get; set; } = [];
}

public class AtsRiesgoDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public bool RequierePetar { get; set; }
}

/// <summary>Ficha resumida de un PETAR ya generado para un ATS — para mostrarlo enlazado en la
/// lista de ATS sin tener que navegar a otra pantalla.</summary>
public class AtsPetarResumenDto
{
    public int Id { get; set; }
    public string? Codigo { get; set; }
    public string? TipoNombre { get; set; }
    public string Estado { get; set; } = string.Empty;
    /// <summary>true si ya existe firma del ejecutante en este PETAR — sin ella no se puede
    /// descargar el PDF ni firmar Supervisor/SSOMA.</summary>
    public bool TieneFirmaEjecutante { get; set; }
    /// <summary>true si este PETAR ya tiene la firma del Capataz/Maestro (cuando la cadena la exige).</summary>
    public bool CapatazFirmado { get; set; }
    public bool PuedeFirmarCapataz { get; set; }
    public bool SupervisorFirmado { get; set; }
    public bool SsomaFirmado { get; set; }
    /// <summary>Mismo criterio que en PETAR → Listar: Residente del proyecto (o admin), con firma
    /// de ejecutante ya puesta y sin firmar Supervisor todavía. Se calcula acá para poder firmar
    /// el PETAR sin salir de la fila del ATS.</summary>
    public bool PuedeFirmarSupervisor { get; set; }
    public bool PuedeFirmarSsoma { get; set; }
}

public class AtsPeligroDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public List<AtsRiesgoDto> Riesgos { get; set; } = [];
}

public class AtsRiesgoRequierePetarRequestDto
{
    public bool RequierePetar { get; set; }
}

public class AtsEppDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Categoria { get; set; } = string.Empty;
}

public class AtsHerramientaDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Categoria { get; set; } = string.Empty;
}

/// <summary>Quién puede firmar cada visto de un ATS, resuelto desde el proyecto (no hay tabla de
/// roles por proyecto: la responsabilidad la define la ficha de Project, igual que
/// AlertaLoginSsomaService). Se usa tanto para autorizar el POST cuanto para avisar por correo.
/// Si quien llama es el Residente no se decide acá sino con IResidenteProyectoResolver, que
/// cruza por persona: por eso no lleva la ficha del Residente.</summary>
public class AtsResponsablesDto
{
    public string ProyectoNombre { get; set; } = string.Empty;
    public string? ResidenteNombre { get; set; }
    public string? ResidenteEmail { get; set; }
    /// <summary>Coordinador SSOMA del proyecto — puede ser más de un correo (EmailCoordSsoma + CoordAdmin).</summary>
    public List<string> SsomaEmails { get; set; } = [];
}

public class AtsPuestoDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
}

public class AtsPlantillaDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public int? PuestoId { get; set; }
    public List<int> PeligroIds { get; set; } = [];
    public List<int> EppIds { get; set; } = [];
    public List<int> HerramientaIds { get; set; } = [];
}

/// <summary>Todo lo que la pantalla de "Nuevo ATS" necesita para armarse: catálogos completos,
/// los pasos que le tocan a ESE trabajador según su puesto (ya filtrados, no los 5 bloques
/// completos), la plantilla sugerida por su puesto si existe, y su proyecto actual (vigencia).</summary>
public class AtsInitDto
{
    public List<AtsProyectoDto> Proyectos { get; set; } = [];
    public int? ProyectoActualId { get; set; }
    public int? PuestoId { get; set; }
    /// <summary>Catálogo completo de puestos activos — solo lo usa el wizard de ATS Grupal (ver
    /// AtsNuevo.modoGrupal) para que el creador elija el puesto/tipo de trabajo de LA CUADRILLA
    /// (no necesariamente el suyo propio) y así traer los pasos correctos vía
    /// GetPasosPorPuesto, en vez de los pasos del puesto de quien crea el ATS.</summary>
    public List<AtsPuestoDto> Puestos { get; set; } = [];
    public List<AtsCategoriaPasoDto> Pasos { get; set; } = [];
    public List<AtsPeligroDto> Peligros { get; set; } = [];
    public List<AtsEppDto> Epps { get; set; } = [];
    public List<AtsHerramientaDto> Herramientas { get; set; } = [];
    public List<AtsPlantillaDto> Plantillas { get; set; } = [];
    public int? PlantillaSugeridaId { get; set; }
    public bool TieneConsentimiento { get; set; }
}

// ── Guardar borrador ─────────────────────────────────────────────────────────

/// <summary>Un paso marcado al llenar el ATS. <see cref="PasoId"/> presente = viene del catálogo.
/// <see cref="PasoId"/> null = paso "de una sola vez" que el trabajador escribió a mano para ESTE
/// ATS (no se guarda en el catálogo ni en la plantilla — no toda actividad se repite en otro ATS),
/// y entonces <see cref="Texto"/>/<see cref="CategoriaNombre"/> son obligatorios.</summary>
public class AtsPasoRequestDto
{
    public int? PasoId { get; set; }
    public string? Texto { get; set; }
    public string? CategoriaNombre { get; set; }
    public bool Aplica { get; set; }
}

public class AtsRiesgoDetalleRequestDto
{
    public int PeligroId { get; set; }
    public int RiesgoId { get; set; }
    /// <summary>"A" | "M" | "B" (Alto/Medio/Bajo).</summary>
    public string RiesgoBase { get; set; } = string.Empty;
    public string Controles { get; set; } = string.Empty;
    public string RiesgoResidual { get; set; } = string.Empty;
}

public class AtsGuardarRequestDto
{
    public int ProyectoId { get; set; }
    public int? PlantillaId { get; set; }
    public string Actividad { get; set; } = string.Empty;
    public string? TorreNombre { get; set; }
    public string? Pisos { get; set; }
    public string? Lugar { get; set; }
    public List<AtsPasoRequestDto> Pasos { get; set; } = [];
    public List<int> EppIds { get; set; } = [];
    public List<int> HerramientaIds { get; set; } = [];
    /// <summary>Herramientas "otros" escritas a mano para este ATS puntual — no están en el
    /// catálogo ni lo tocan (ver SsAtsHerramientaSeleccionada.HerramientaId).</summary>
    public List<string> HerramientasPersonalizadas { get; set; } = [];
    public List<AtsRiesgoDetalleRequestDto> Riesgos { get; set; } = [];
    /// <summary>Presente solo cuando este ATS nace como corrección de uno ya FIRMADO el mismo
    /// día (condición de campo distinta a la evaluada) — enlaza con el original vía AtsAnteriorId,
    /// nunca lo modifica (un ATS firmado es inmutable, Art. 76 del Reglamento de la Ley 29783).</summary>
    public int? AtsAnteriorId { get; set; }
    /// <summary>Solo al corregir un ATS grupal desde el panel: el grupo se reemplaza por una revisión nueva
    /// (nunca se acepta desde la página pública).</summary>
    public int? GrupoAnteriorId { get; set; }
}

// ── Firmas adicionales (Autoriza / Visto Bueno SSOMA) ───────────────────────

/// <summary>Firma de Residente/Ing. Producción o de Prevencionista/Coordinador SSOMA sobre un ATS
/// que el ejecutante ya firmó. Sin selfie ni geolocalización: no es evidencia de presencia física
/// en el lugar de la tarea, es una validación documental que puede darse desde oficina.</summary>
public class AtsFirmarVistoRequestDto
{
    public string FirmaBase64 { get; set; } = string.Empty;
}

// ── Firmar ───────────────────────────────────────────────────────────────────

public class AtsFirmarRequestDto
{
    public string SelfieBase64 { get; set; } = string.Empty;
    public string FirmaBase64 { get; set; } = string.Empty;
    public DateTime? HoraDispositivo { get; set; }
    public decimal? Lat { get; set; }
    public decimal? Lng { get; set; }
    public decimal? PrecisionMetros { get; set; }
    public bool AceptaConsentimiento { get; set; }
}

// ── Respuesta ────────────────────────────────────────────────────────────────

public class AtsPasoResponseDto
{
    public int? PasoId { get; set; }
    public string CategoriaNombre { get; set; } = string.Empty;
    public string Texto { get; set; } = string.Empty;
    public bool Aplica { get; set; }
}

public class AtsRiesgoDetalleResponseDto
{
    public int PeligroId { get; set; }
    public int RiesgoId { get; set; }
    public string PeligroNombre { get; set; } = string.Empty;
    public string RiesgoNombre { get; set; } = string.Empty;
    public string RiesgoBase { get; set; } = string.Empty;
    public string Controles { get; set; } = string.Empty;
    public string RiesgoResidual { get; set; } = string.Empty;
}

public class AtsObservacionDto
{
    public int Id { get; set; }
    public string Rol { get; set; } = string.Empty;
    public string AutorNombre { get; set; } = string.Empty;
    public string Texto { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public string? Respuesta { get; set; }
    public string? ResueltaPorNombre { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ResueltaEn { get; set; }
}

public class AtsObservacionesDto
{
    public List<AtsObservacionDto> Observaciones { get; set; } = [];
    public bool PuedeObservar { get; set; }
    public bool PuedeResolver { get; set; }
}

public class AtsObservacionCrearRequestDto
{
    public string Texto { get; set; } = string.Empty;
}

public class AtsObservacionResolverRequestDto
{
    public string Respuesta { get; set; } = string.Empty;
}

public class AtsAnularRequestDto
{
    public string Motivo { get; set; } = string.Empty;
}

public class AtsResponseDto
{
    public int Id { get; set; }
    public string? AnuladoMotivo { get; set; }
    public int ObservacionesAbiertas { get; set; }
    public bool OrigenOffline { get; set; }
    public DateTime? HoraDispositivo { get; set; }
    /// <summary>Residente/Producción/SSOMA/admin pueden anular un ATS firmado (queda registrado, no se borra).</summary>
    public bool PuedeAnular { get; set; }
    public string? Codigo { get; set; }
    public int WorkerId { get; set; }
    public string? WorkerNombre { get; set; }
    public int ProyectoId { get; set; }
    public string? ProyectoNombre { get; set; }
    public int? PuestoId { get; set; }
    public string? PuestoNombre { get; set; }
    public int? PlantillaId { get; set; }
    public string? PlantillaNombre { get; set; }

    public string Actividad { get; set; } = string.Empty;
    public string? TorreNombre { get; set; }
    public string? Pisos { get; set; }
    public string? Lugar { get; set; }
    public DateOnly Fecha { get; set; }
    public DateTime? HoraServidorFirma { get; set; }
    public decimal? Lat { get; set; }
    public decimal? Lng { get; set; }
    public decimal? PrecisionMetros { get; set; }
    public string? SelfieUrl { get; set; }
    public string? FirmaUrl { get; set; }
    public string Estado { get; set; } = string.Empty;
    public int? AtsAnteriorId { get; set; }
    public string? PdfHash { get; set; }

    /// <summary>true si el ejecutante es obrero de obra — de esto depende si el PDF y el
    /// wizard piden/muestran la firma de Capataz (ver AtsRepository.EsObreroDeObra).</summary>
    public bool RequiereCapataz { get; set; }
    /// <summary>Presente si este ATS nació de una cuadrilla (QR) — el Capataz firma UNA vez por grupo,
    /// no por cada ATS (ver POST grupo/{id}/firmar-capataz).</summary>
    public int? AtsGrupoId { get; set; }
    public string? CapatazNombre { get; set; }
    public string? CapatazCargo { get; set; }
    public string? CapatazFirmaUrl { get; set; }
    public DateTime? CapatazHoraServidor { get; set; }

    public string? AutorizaNombre { get; set; }
    public string? AutorizaCargo { get; set; }
    public string? AutorizaFirmaUrl { get; set; }
    public DateTime? AutorizaHoraServidor { get; set; }

    public string? SsomaNombre { get; set; }
    public string? SsomaCargo { get; set; }
    public string? SsomaFirmaUrl { get; set; }
    public DateTime? SsomaHoraServidor { get; set; }

    /// <summary>Se completan solo cuando GetPorId/Listar los pide para el usuario logueado
    /// (ver AtsService) — el frontend los usa para mostrar u ocultar los botones de firma.
    /// PuedeCapataz solo puede ser true cuando el ejecutante es obrero de obra (ver
    /// AtsService.PuedeAutorizarYVistoBueno) — si el ejecutante ya es Staff, este nivel no aplica
    /// y el botón nunca se muestra.</summary>
    public bool PuedeCapataz { get; set; }
    public bool PuedeAutorizar { get; set; }
    public bool PuedeVistoBuenoSsoma { get; set; }

    /// <summary>true si algún riesgo marcado en este ATS exige PETAR (ver SsAtsRiesgo.RequierePetar).</summary>
    public bool RequierePetar { get; set; }
    public List<AtsPetarResumenDto> Petares { get; set; } = [];

    public List<AtsPasoResponseDto> Pasos { get; set; } = [];
    public List<string> Epps { get; set; } = [];
    public List<string> Herramientas { get; set; } = [];
    public List<AtsRiesgoDetalleResponseDto> Riesgos { get; set; } = [];
}

/// <summary>Lo mínimo que se muestra públicamente al escanear el QR del PDF — sin datos que
/// identifiquen de más al trabajador (nombre completo sí, pero nada de contacto/DNI).</summary>
public class AtsVerificacionPublicaDto
{
    public bool Valido { get; set; }
    public bool Encontrado { get; set; }
    public string? WorkerNombre { get; set; }
    public string? ProyectoNombre { get; set; }
    public string? Actividad { get; set; }
    public DateOnly? Fecha { get; set; }
    public DateTime? HoraServidorFirma { get; set; }
    public string? Estado { get; set; }
}

public class AtsFiltroDto
{
    public int? ProyectoId { get; set; }
    public int? WorkerId { get; set; }
    public DateOnly? FechaDesde { get; set; }
    public DateOnly? FechaHasta { get; set; }
    public string? Estado { get; set; }
    public int Page { get; set; } = 1;
    /// <summary>Primera carga del listado: el servidor usa el proyecto actual de quien consulta (así el front no espera a conocerlo
    /// para pedir el listado y ambas llamadas salen en paralelo). Se ignora si ya viene ProyectoId.</summary>
    public bool ProyectoActual { get; set; }
}


/// <summary>Lo mínimo que necesita el listado de ATS para poblar el filtro de proyecto — en vez de
/// traer GetInit completo (catálogos de pasos/peligros/EPP/plantillas) solo para sacar proyectos.</summary>
public class AtsListaInitDto
{
    public List<AtsProyectoDto> Proyectos { get; set; } = [];
    public int? ProyectoActualId { get; set; }
}

/// <summary>Fila del listado de ATS grupales (cuadrillas) — una por grupo, no por trabajador.</summary>
public class AtsGrupoListaItemDto
{
    public int Id { get; set; }
    /// <summary>Algún riesgo de la cuadrilla exige PETAR (catálogo de Riesgos) — mismo criterio que el ATS individual.</summary>
    public bool RequierePetar { get; set; }
    public int TotalPetares { get; set; }
    public int Revision { get; set; } = 1;
    public int ObservacionesAbiertas { get; set; }
    public string? Codigo { get; set; }
    public string? ProyectoNombre { get; set; }
    public string Actividad { get; set; } = string.Empty;
    public string? TorreNombre { get; set; }
    public string? Pisos { get; set; }
    public string? Lugar { get; set; }
    public DateOnly Fecha { get; set; }
    public string Estado { get; set; } = string.Empty;
    public string? CreadoPorNombre { get; set; }
    public int TotalAdhesiones { get; set; }
    public string? CapatazNombre { get; set; }
    public bool CapatazFirmado { get; set; }
    public bool CapatazVigente { get; set; }
    public int CapatazNuevosSinValidar { get; set; }
    /// <summary>false en cuadrillas de Staff/supervisores: ahí no firma el Capataz.</summary>
    public bool RequiereCapataz { get; set; }
    public int EjecutantesFirmados { get; set; }
    public int AutorizaFirmados { get; set; }
    public int SsomaFirmados { get; set; }
    public int ProyectoId { get; set; }
    /// <summary>PETAR de la cuadrilla a los que todavía les falta la firma de Supervisor o SSOMA.</summary>
    public int PetaresPendientesFirma { get; set; }
    /// <summary>Permisos del usuario que abre el listado (se calculan en el servicio) — mismo criterio que el panel de la cuadrilla.</summary>
    public bool PuedeAutorizar { get; set; }
    public bool PuedeVistoBuenoSsoma { get; set; }
    /// <summary>Token del link del Capataz; solo se envía a quien puede ver la cuadrilla y mientras falte su firma.</summary>
    public string? CapatazLinkToken { get; set; }
}

public class AtsGrupoListResponseDto
{
    public List<AtsGrupoListaItemDto> Data { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalRecords { get; set; }
    public int TotalPages { get; set; }
}

// ── Administración de plantillas ────────────────────────────────────────────

/// <summary>Fila del mapeo "Plantillas por puesto" — misma idea que AtsPasoPuestoDto pero para
/// autosugerir la plantilla de ATS según el puesto del trabajador.</summary>
public class AtsPlantillaPuestoDto
{
    public int PlantillaId { get; set; }
    public string PlantillaNombre { get; set; } = string.Empty;
    public List<int> PuestoIds { get; set; } = [];
}

public class AtsPlantillaGuardarRequestDto
{
    public string Nombre { get; set; } = string.Empty;
    public int? PuestoId { get; set; }
    public List<int> PeligroIds { get; set; } = [];
    public List<int> EppIds { get; set; } = [];
    public List<int> HerramientaIds { get; set; } = [];
}

// ── Actividades/pasos por plantilla ─────────────────────────────────────────

public class AtsPlantillaPasoDto
{
    public int Id { get; set; }
    public string Texto { get; set; } = string.Empty;
    public short Orden { get; set; }
}

public class AtsPlantillaActividadDto
{
    public int Id { get; set; }
    public int PlantillaId { get; set; }
    public string Texto { get; set; } = string.Empty;
    public short Orden { get; set; }
    public List<AtsPlantillaPasoDto> Pasos { get; set; } = [];
    public List<int> PeligroIds { get; set; } = [];
}

public class AtsPlantillaActividadGuardarRequestDto
{
    public string Texto { get; set; } = string.Empty;
}

public class AtsPlantillaPasoGuardarRequestDto
{
    public string Texto { get; set; } = string.Empty;
}

public class AtsPlantillaActividadPeligrosRequestDto
{
    public List<int> PeligroIds { get; set; } = [];
}

// ── Controles sugeridos por riesgo ──────────────────────────────────────────

public class AtsRiesgoControlDto
{
    public int Id { get; set; }
    public string Texto { get; set; } = string.Empty;
    public short Orden { get; set; }
    /// <summary>Uno de: Eliminacion, Sustitucion, Ingenieria, Administrativo, Epp (jerarquía de
    /// controles de la norma). El Coordinador SSOMA puede cambiarlo en cualquier momento.</summary>
    public string Tipo { get; set; } = "Administrativo";
}

public class AtsRiesgoConControlesDto
{
    public int RiesgoId { get; set; }
    public string RiesgoNombre { get; set; } = string.Empty;
    public int PeligroId { get; set; }
    public string PeligroNombre { get; set; } = string.Empty;
    public List<AtsRiesgoControlDto> Controles { get; set; } = [];
}

public class AtsRiesgoControlGuardarRequestDto
{
    public string Texto { get; set; } = string.Empty;
    public string Tipo { get; set; } = "Administrativo";
}

// ── ATS Grupal (cuadrilla) ───────────────────────────────────────────────────

/// <summary>Respuesta al crear un ATS grupal: el link/QR para que la cuadrilla se adhiera.</summary>
public class AtsGrupoCrearResponseDto
{
    public int Id { get; set; }
    public string QrToken { get; set; } = string.Empty;
    public DateTime QrExpiraEn { get; set; }
    /// <summary>true si algún riesgo del grupo exige PETAR — la pantalla pública avisa que falta generarlo.</summary>
    public bool RequierePetar { get; set; }
}

/// <summary>Panel del autor: cuántos ya firmaron, para saber si falta alguien de la cuadrilla.</summary>
/// <summary>Datos de cada adhesión necesarios para armar el PDF grupal (evidencia + firmas de la cadena).</summary>
public class AtsGrupoPdfAdhesionDto
{
    public bool OrigenOffline { get; set; }
    public DateTime? HoraDispositivo { get; set; }
    public int AtsId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Puesto { get; set; }
    public DateTime? HoraServidorFirma { get; set; }
    public string? SelfieUrl { get; set; }
    public string? FirmaUrl { get; set; }
    public decimal? Lat { get; set; }
    public decimal? Lng { get; set; }
    public string? AutorizaNombre { get; set; }
    public string? AutorizaCargo { get; set; }
    public string? AutorizaFirmaUrl { get; set; }
    public DateTime? AutorizaHoraServidor { get; set; }
    public string? SsomaNombre { get; set; }
    public string? SsomaCargo { get; set; }
    public string? SsomaFirmaUrl { get; set; }
    public DateTime? SsomaHoraServidor { get; set; }
}

public class AtsGrupoIntegranteDto
{
    public int WorkerId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    /// <summary>true si ya tiene su ATS firmado en esta cuadrilla.</summary>
    public bool Adherido { get; set; }
}

public class AtsGrupoIntegrantesRequestDto
{
    public List<int> WorkerIds { get; set; } = [];
}

public class AtsGrupoMiAtsRequestDto
{
    public int WorkerId { get; set; }
    public string DniConfirmacion { get; set; } = string.Empty;
}

public class AtsGrupoAdheridoDto
{
    /// <summary>true si este ATS ya tiene la firma de ese nivel — falso = firmó después de la validación.</summary>
    public bool AutorizaFirmado { get; set; }
    public bool SsomaFirmado { get; set; }
    public int AtsId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
}

public class AtsGrupoEstadoDto
{
    /// <summary>PETARes grupales ya generados para esta cuadrilla.</summary>
    public int TotalPetares { get; set; }
    public int Id { get; set; }
    public string? Codigo { get; set; }
    public string Actividad { get; set; } = string.Empty;
    public string? ProyectoNombre { get; set; }
    public string? TorreNombre { get; set; }
    public string? Pisos { get; set; }
    public DateOnly Fecha { get; set; }
    public string Estado { get; set; } = string.Empty;
    public string QrToken { get; set; } = string.Empty;
    public DateTime QrExpiraEn { get; set; }
    public int TotalAdhesiones { get; set; }
    public List<string> TrabajadoresAdheridos { get; set; } = [];
    /// <summary>Mismo listado con el id del ATS individual de cada uno — el PDF de cada trabajador sigue
    /// existiendo (evidencia propia: su selfie, geo y firma), se abre desde el panel de la cuadrilla.</summary>
    public List<AtsGrupoAdheridoDto> Adheridos { get; set; } = [];

    public string? CapatazNombre { get; set; }
    public DateTime? CapatazHoraServidor { get; set; }
    /// <summary>true = el Capataz firmó Y no se sumó nadie después. Si firmó pero hay más
    /// adhesiones que las que había al firmar, queda false y CapatazNuevosSinValidar > 0.</summary>
    public bool CapatazVigente { get; set; }
    public int CapatazNuevosSinValidar { get; set; }

    /// <summary>false en cuadrillas de Staff/supervisores (p. ej. "Supervisión"): ahí NO firma el Capataz,
    /// la cadena es Autoriza (Residente/Producción) + SSOMA.</summary>
    public bool RequiereCapataz { get; set; }
    /// <summary>Cuántos de los ATS adheridos ya firmó cada nivel (sobre TotalAdhesiones).</summary>
    public int EjecutantesFirmados { get; set; }
    public int AutorizaFirmados { get; set; }
    public int SsomaFirmados { get; set; }
    /// <summary>Permisos del usuario que abre el panel (se calculan en el servicio).</summary>
    public bool PuedeAutorizar { get; set; }
    public bool PuedeVistoBuenoSsoma { get; set; }
    /// <summary>Integrantes esperados (lista previa) con marca de quién ya firmó; vacío si no se definió.</summary>
    public List<AtsGrupoIntegranteDto> Esperados { get; set; } = [];
    public bool PuedeReabrir { get; set; }
    /// <summary>Autor, Residente, Producción, SSOMA o admin, con la cuadrilla abierta y al menos un trabajador firmado.</summary>
    public bool PuedeCerrar { get; set; }
    public bool PuedeEditarIntegrantes { get; set; }
    /// <summary>Quien abre el panel creó este grupo / ya firmó su propio ATS dentro de él — el autor también es
    /// un ejecutante y debe firmar como los demás.</summary>
    public bool SoyAutor { get; set; }
    public bool YoYaFirme { get; set; }
    public bool PuedeAnular { get; set; }
    public string? AnuladoMotivo { get; set; }
    public int ObservacionesAbiertas { get; set; }
    public bool RequierePetar { get; set; }
    public int Revision { get; set; } = 1;
    public int? GrupoAnteriorId { get; set; }
    /// <summary>Id del grupo que reemplazó a este (si fue corregido).</summary>
    public int? ReemplazadoPorId { get; set; }
    public bool PuedeCorregir { get; set; }
    /// <summary>Último evento de cierre/reapertura (quién y cuándo), para trazabilidad en el panel.</summary>
    public string? UltimoEvento { get; set; }
}

/// <summary>Página pública del Capataz (sin login, el token del grupo es el candado): ve cuántos y
/// quiénes ya adhirieron antes de firmar — su firma certifica a esa lista, no a futuros.</summary>
public class AtsGrupoCapatazPublicoDto
{
    public bool Valido { get; set; }
    public string? MotivoInvalido { get; set; }
    public string? ProyectoNombre { get; set; }
    public string? Actividad { get; set; }
    public List<string> TrabajadoresAdheridos { get; set; } = [];
    public bool YaFirmo { get; set; }
    public bool Vigente { get; set; }
    /// <summary>La cuadrilla sigue abierta: todavía no se puede firmar, pero el Capataz puede cerrarla desde aquí.</summary>
    public bool Abierta { get; set; }
    public int NuevosSinValidar { get; set; }
    public List<AtsGrupoWorkerOpcionDto> Capataces { get; set; } = [];
}

/// <summary>El autor de una cuadrilla (sin cuenta) cierra desde su página pública: nombre + dígitos de su DNI.</summary>
public class AtsGrupoCerrarPublicoRequestDto
{
    public int WorkerId { get; set; }
    public string DniConfirmacion { get; set; } = string.Empty;
}

public class AtsGrupoCapatazFirmarRequestDto
{
    public int WorkerId { get; set; }
    public string DniConfirmacion { get; set; } = string.Empty;
    public string FirmaBase64 { get; set; } = string.Empty;
    public string SelfieBase64 { get; set; } = string.Empty;
    public DateTime? HoraDispositivo { get; set; }
    public decimal? Lat { get; set; }
    public decimal? Lng { get; set; }
    public decimal? PrecisionMetros { get; set; }
}

/// <summary>Lo que ve el trabajador al escanear el QR, ANTES de identificarse — sin login, por
/// eso no lleva nada sensible (ver AtsVerificacionPublicaDto, mismo criterio de exposición mínima).</summary>
public class AtsGrupoResumenPublicoDto
{
    public bool Valido { get; set; }
    public string? MotivoInvalido { get; set; }
    public string? ProyectoNombre { get; set; }
    public string? Actividad { get; set; }
    public string? TorreNombre { get; set; }
    public string? Pisos { get; set; }
    public string? Lugar { get; set; }
    public DateOnly? Fecha { get; set; }
    public List<string> Epps { get; set; } = [];
    public List<string> Herramientas { get; set; } = [];
    public List<AtsRiesgoDetalleResponseDto> Riesgos { get; set; } = [];
}

/// <summary>Trabajador seleccionable en la página de adhesión — acotado al proyecto del grupo y
/// a quienes ya tienen la autorización de firma digital (ver ExigirAutorizacionPermiso), para no
/// dejar elegir a alguien que de todas formas no podría firmar.</summary>
public class AtsGrupoWorkerOpcionDto
{
    public int WorkerId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    /// <summary>true si ya firmó su ATS en esta cuadrilla — la pantalla pública lo manda directo a los PETAR
    /// en vez de pedirle firmar otra vez.</summary>
    public bool YaFirmo { get; set; }
    /// <summary>Últimos 4 dígitos únicamente — nunca se manda el DNI completo a esta pantalla sin
    /// login, solo lo necesario para que el trabajador reconozca su propio nombre en la lista.</summary>
    public string? DniUltimos4 { get; set; }
}

/// <summary>El trabajador confirma su identidad con los últimos dígitos de SU PROPIO DNI (no es
/// una contraseña, es fricción mínima contra "elegir cualquier nombre de la lista") y firma en el
/// mismo paso — no hay Borrador intermedio, entra directo Firmado.</summary>
public class AtsGrupoUnirseRequestDto
{
    public int WorkerId { get; set; }
    public string DniConfirmacion { get; set; } = string.Empty;
    public string SelfieBase64 { get; set; } = string.Empty;
    public string FirmaBase64 { get; set; } = string.Empty;
    public DateTime? HoraDispositivo { get; set; }
    public decimal? Lat { get; set; }
    public decimal? Lng { get; set; }
    public decimal? PrecisionMetros { get; set; }
    public bool AceptaConsentimiento { get; set; }
}

// ── QR fijo por proyecto (crear ATS Grupal sin login) ────────────────────────

/// <summary>Página pública que abre el QR de obra — sin login. A diferencia del resumen de
/// adhesión, no hay contenido todavía (este QR es para CREAR un grupo, no para unirse a uno
/// existente): solo valida el token y devuelve la lista de trabajadores del proyecto.</summary>
public class AtsGrupoProyectoPublicoDto
{
    public bool Valido { get; set; }
    public string? MotivoInvalido { get; set; }
    public string? ProyectoNombre { get; set; }
    public List<AtsGrupoWorkerOpcionDto> Trabajadores { get; set; } = [];
}

/// <summary>Identificación mínima (igual patrón que la adhesión) antes de pedir los catálogos del
/// wizard — sin esto cualquiera podría ver pasos/EPP/herramientas de la obra sin ser parte de ella.</summary>
public class AtsGrupoInitPublicoRequestDto
{
    public int WorkerId { get; set; }
    public string DniConfirmacion { get; set; } = string.Empty;
}

/// <summary>Mismo candado (worker + DNI) envolviendo el contenido ya armado por el wizard — el
/// ProyectoId que venga en Contenido se IGNORA, el servidor siempre usa el del token.</summary>
public class AtsGrupoCrearPublicoRequestDto
{
    /// <summary>Solo para grupos armados sin conexión: id generado en el teléfono (idempotencia) y hora del dispositivo.</summary>
    public Guid? ClientId { get; set; }
    public DateTime? CapturadoEn { get; set; }
    public int WorkerId { get; set; }
    public string DniConfirmacion { get; set; } = string.Empty;
    public AtsGuardarRequestDto Contenido { get; set; } = new();
}

/// <summary>Alta de un EPP o herramienta en el catálogo del ATS desde el detalle de plantilla.
/// Categoria es opcional: EPP → "Específico" por defecto; herramienta → "Varios".</summary>
public class AtsCatalogoItemCrearDto
{
    public string Nombre { get; set; } = string.Empty;
    public string? Categoria { get; set; }
}
