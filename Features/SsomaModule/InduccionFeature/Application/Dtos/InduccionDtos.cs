namespace Abril_Backend.Features.SsomaModule.InduccionFeature.Application.Dtos;

// ── Catálogos (Coordinador SSOMA) ───────────────────────────────────────────

public class IndDocumentoDto
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string Tipo { get; set; } = "Documento";
    public bool Obligatorio { get; set; } = true;
    public int Orden { get; set; }
    public bool Activo { get; set; } = true;
    /// <summary>Versión vigente (null si todavía no se subió ningún archivo).</summary>
    public IndDocumentoVersionDto? VersionVigente { get; set; }
}

public class IndDocumentoVersionDto
{
    public int Id { get; set; }
    public int DocumentoId { get; set; }
    public int Version { get; set; }
    public string ArchivoUrl { get; set; } = string.Empty;
    public string ArchivoHash { get; set; } = string.Empty;
    public bool EsCambioMaterial { get; set; }
    public string? NotaCambio { get; set; }
    public bool Vigente { get; set; }
    public DateTime PublicadoEn { get; set; }
}

public class IndDocumentoGuardarDto
{
    public int? Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string Tipo { get; set; } = "Documento";
    public bool Obligatorio { get; set; } = true;
    public int Orden { get; set; }
    public bool Activo { get; set; } = true;
}

public class IndTemaDto
{
    public int Id { get; set; }
    public int Orden { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;
}

public class IndTemaGuardarDto
{
    public int? Id { get; set; }
    public int Orden { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;
}

public class IndOpcionDto
{
    public int Id { get; set; }
    public string Texto { get; set; } = string.Empty;
    public bool EsCorrecta { get; set; }
    public string? ParTexto { get; set; }
    public int Orden { get; set; }
}

public class IndPreguntaDto
{
    public int Id { get; set; }
    /// <summary>VF | Alternativa | Relacionar</summary>
    public string Tipo { get; set; } = "VF";
    public string Enunciado { get; set; } = string.Empty;
    public decimal Puntos { get; set; } = 1;
    public int Orden { get; set; }
    public bool Activo { get; set; } = true;
    public List<IndOpcionDto> Opciones { get; set; } = [];
}

public class IndPreguntaGuardarDto
{
    public int? Id { get; set; }
    public string Tipo { get; set; } = "VF";
    public string Enunciado { get; set; } = string.Empty;
    public decimal Puntos { get; set; } = 1;
    public int Orden { get; set; }
    public bool Activo { get; set; } = true;
    public List<IndOpcionDto> Opciones { get; set; } = [];
}

public class IndEvaluacionVersionDto
{
    public int Id { get; set; }
    public int Version { get; set; }
    public decimal NotaMinima { get; set; }
    public decimal NotaMaxima { get; set; }
    public int DuracionMin { get; set; }
    public int TotalPreguntas { get; set; }
    public bool Vigente { get; set; }
    public DateTime PublicadoEn { get; set; }
}

public class IndEvaluacionPublicarDto
{
    public decimal NotaMinima { get; set; } = 14;
    public int DuracionMin { get; set; } = 15;
}

// ── Sesión (instructor) ─────────────────────────────────────────────────────

public class IndSesionCrearDto
{
    public int ProyectoId { get; set; }
    public DateOnly? Fecha { get; set; }
    public decimal Horas { get; set; } = 2;
    /// <summary>Vacío = todos los documentos obligatorios y activos con versión vigente.</summary>
    public List<int>? DocumentoIds { get; set; }
    /// <summary>Vacío = todos los temas activos.</summary>
    public List<int>? TemaIds { get; set; }
    /// <summary>Asistentes esperados (opcional): quedan "Pendiente" para ver quién falta firmar.</summary>
    public List<int>? WorkerIdsEsperados { get; set; }
    /// <summary>InduccionHombreNuevo | ReInduccion | Actualizacion. La Actualización entrega solo los documentos
    /// indicados y NO exige evaluación.</summary>
    public string Tipo { get; set; } = "InduccionHombreNuevo";
}

public class IndSesionFiltroDto
{
    public int? ProyectoId { get; set; }
    public DateOnly? FechaDesde { get; set; }
    public DateOnly? FechaHasta { get; set; }
    public string? Estado { get; set; }
    public int Page { get; set; } = 1;
}

public class IndSesionListaItemDto
{
    public int Id { get; set; }
    public string? Codigo { get; set; }
    public int ProyectoId { get; set; }
    public string? ProyectoNombre { get; set; }
    public DateOnly Fecha { get; set; }
    public string Tipo { get; set; } = "InduccionHombreNuevo";
    public string Estado { get; set; } = string.Empty;
    public string? InstructorNombre { get; set; }
    public int Firmados { get; set; }
    public int Esperados { get; set; }
}

public class IndSesionListaDto
{
    public List<IndSesionListaItemDto> Items { get; set; } = [];
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class IndAsistenciaDto
{
    public int Id { get; set; }
    public int WorkerId { get; set; }
    public string? NombreCompleto { get; set; }
    public string? DocumentoIdentidad { get; set; }
    public string? Categoria { get; set; }
    public string? ContratistaNombre { get; set; }
    /// <summary>Pendiente | Firmado | Rechazado</summary>
    public string Estado { get; set; } = string.Empty;
    public decimal? Nota { get; set; }
    public bool Aprobado { get; set; }
    public int Intentos { get; set; }
    public DateTime? HoraServidorFirma { get; set; }
    public string? SelfieUrl { get; set; }
    public string? FirmaUrl { get; set; }
    public bool OrigenOffline { get; set; }
    public int DocumentosRecibidos { get; set; }
    public bool Validado { get; set; }
    public string? RechazoMotivo { get; set; }
    /// <summary>La selfie ya se usó en otra firma (de esta u otra sesión, o de un ATS): el instructor debe revisarla.</summary>
    public bool SelfieRepetida { get; set; }
}

/// <summary>Sello de tiempo externo de la sesión (RFC 3161).</summary>
public class IndSelloDto
{
    /// <summary>Pendiente | Sellado | Error</summary>
    public string Estado { get; set; } = "Pendiente";
    public DateTime? SelladoEn { get; set; }
    public string? Proveedor { get; set; }
    public int Intentos { get; set; }
    public string? UltimoError { get; set; }
}

/// <summary>Respaldo de los archivos de la sesión en otra cuenta de almacenamiento.</summary>
public class IndRespaldoDto
{
    /// <summary>Pendiente | Respaldado | Error | NoConfigurado</summary>
    public string Estado { get; set; } = "Pendiente";
    public int Archivos { get; set; }
    public DateTime? RespaldadoEn { get; set; }
    public string? UltimoError { get; set; }
}

public class IndSesionDocumentoDto
{
    public int DocumentoVersionId { get; set; }
    public int DocumentoId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public int Version { get; set; }
    public string ArchivoUrl { get; set; } = string.Empty;
    public string ArchivoHash { get; set; } = string.Empty;
}

public class IndSesionTemaDto
{
    public int Orden { get; set; }
    public string Descripcion { get; set; } = string.Empty;
}

public class IndSesionDetalleDto
{
    public int Id { get; set; }
    public string? Codigo { get; set; }
    public int ProyectoId { get; set; }
    public string? ProyectoNombre { get; set; }
    public DateOnly Fecha { get; set; }
    public decimal Horas { get; set; }
    public string Estado { get; set; } = string.Empty;
    public string? InstructorNombre { get; set; }
    public string? InstructorCargo { get; set; }
    public string Tipo { get; set; } = "InduccionHombreNuevo";
    public Guid QrToken { get; set; }
    public DateTime QrExpiraEn { get; set; }
    public int EvaluacionVersion { get; set; }
    public decimal NotaMinima { get; set; }
    public DateTime? CerradaEn { get; set; }
    public string? ActaPdfUrl { get; set; }
    public string? ActaPdfHash { get; set; }
    public string? AnuladoMotivo { get; set; }
    public string? ActaPdfSha256 { get; set; }
    /// <summary>Solo se llena en el detalle de una sesión cerrada.</summary>
    public IndSelloDto? Sello { get; set; }
    public IndRespaldoDto? Respaldo { get; set; }
    public List<IndSesionDocumentoDto> Documentos { get; set; } = [];
    public List<IndSesionTemaDto> Temas { get; set; } = [];
    public List<IndAsistenciaDto> Asistencias { get; set; } = [];
}

public class IndCerrarSesionDto
{
    public string FirmaBase64 { get; set; } = string.Empty;
}

public class IndAnularDto
{
    public string Motivo { get; set; } = string.Empty;
}

public class IndRechazarAsistenciaDto
{
    public string Motivo { get; set; } = string.Empty;
}

// ── Página pública del trabajador (sin login, el token del QR es el candado) ─

public class IndPublicoResumenDto
{
    public bool Valido { get; set; }
    public string? MotivoInvalido { get; set; }
    public string? Codigo { get; set; }
    public string? ProyectoNombre { get; set; }
    public DateOnly Fecha { get; set; }
    public decimal Horas { get; set; }
    public string? InstructorNombre { get; set; }
    public decimal NotaMinima { get; set; }
    public int DuracionMin { get; set; }
    public string DeclaracionVersion { get; set; } = string.Empty;
    public string DeclaracionTexto { get; set; } = string.Empty;
    public string Tipo { get; set; } = "InduccionHombreNuevo";
    /// <summary>false en una Actualización de documentos: el trabajador recibe, confirma y firma, sin evaluación.</summary>
    public bool RequiereEvaluacion { get; set; } = true;
    public List<IndSesionDocumentoDto> Documentos { get; set; } = [];
    public List<IndSesionTemaDto> Temas { get; set; } = [];
}

public class IndWorkerOpcionDto
{
    public int WorkerId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public bool YaFirmo { get; set; }
}

/// <summary>Cuerpo base de todo POST público: quién es + los últimos dígitos del DNI.</summary>
public class IndIdentidadDto
{
    public int WorkerId { get; set; }
    public string DniConfirmacion { get; set; } = string.Empty;
}

public class IndEstadoTrabajadorDto
{
    public bool Firmado { get; set; }
    public bool Aprobado { get; set; }
    public int Intentos { get; set; }
    public decimal? Nota { get; set; }
    public List<int> DocumentosRecibidos { get; set; } = [];
    public int? AsistenciaId { get; set; }
}

public class IndDocumentoRecibidoDto : IndIdentidadDto
{
    public int DocumentoVersionId { get; set; }
    public int? SegundosAbierto { get; set; }
}

public class IndPreguntaPublicaDto
{
    public int Id { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public string Enunciado { get; set; } = string.Empty;
    /// <summary>Alternativa: opciones. VF: vacío. Relacionar: ítems (lado izquierdo).</summary>
    public List<IndOpcionPublicaDto> Opciones { get; set; } = [];
    /// <summary>Relacionar: lado derecho, ya mezclado.</summary>
    public List<string> Pares { get; set; } = [];
}

public class IndOpcionPublicaDto
{
    public int Id { get; set; }
    public string Texto { get; set; } = string.Empty;
}

public class IndEvaluacionPublicaDto
{
    public int EvaluacionVersionId { get; set; }
    public decimal NotaMinima { get; set; }
    public decimal NotaMaxima { get; set; }
    public int DuracionMin { get; set; }
    public List<IndPreguntaPublicaDto> Preguntas { get; set; } = [];
}

public class IndParRespuestaDto
{
    public string Texto { get; set; } = string.Empty;
    public string ParTexto { get; set; } = string.Empty;
}

public class IndRespuestaDto
{
    public int PreguntaId { get; set; }
    /// <summary>VF: la respuesta del trabajador (true = Verdadero).</summary>
    public bool? Verdadero { get; set; }
    /// <summary>Alternativa: id de la opción elegida.</summary>
    public int? OpcionId { get; set; }
    /// <summary>Relacionar: cada ítem con la pareja que eligió.</summary>
    public List<IndParRespuestaDto>? Pares { get; set; }
}

public class IndResponderEvaluacionDto : IndIdentidadDto
{
    public int EvaluacionVersionId { get; set; }
    public DateTime? IniciadoEn { get; set; }
    public List<IndRespuestaDto> Respuestas { get; set; } = [];
}

public class IndResultadoEvaluacionDto
{
    public decimal Nota { get; set; }
    public decimal NotaMinima { get; set; }
    public decimal NotaMaxima { get; set; }
    public bool Aprobado { get; set; }
    public int Intentos { get; set; }
}

public class IndFirmarDto : IndIdentidadDto
{
    public string SelfieBase64 { get; set; } = string.Empty;
    public string FirmaBase64 { get; set; } = string.Empty;
    public decimal? Lat { get; set; }
    public decimal? Lng { get; set; }
    public decimal? PrecisionMetros { get; set; }
    public DateTime? HoraDispositivo { get; set; }
    public bool AceptaConsentimiento { get; set; }
    /// <summary>"Experiencia del trabajador en la actividad a realizar" (SSO-FO-031).</summary>
    public string? ExperienciaActividad { get; set; }
    public bool AceptaDeclaracion { get; set; }
}

// ── Verificación pública del acta (QR) ──────────────────────────────────────

public class IndVerificacionDto
{
    public bool Valido { get; set; }
    public string? Motivo { get; set; }
    public string? Codigo { get; set; }
    public string? ProyectoNombre { get; set; }
    public DateOnly Fecha { get; set; }
    public string? InstructorNombre { get; set; }
    public string? ActaPdfHash { get; set; }
    public DateTime? CerradaEn { get; set; }
    /// <summary>Hora certificada por la TSA externa, si la sesión ya está sellada.</summary>
    public DateTime? SelladoEn { get; set; }
    public string? SelloProveedor { get; set; }
    public string? SelloEmisor { get; set; }
    public List<IndVerificacionTrabajadorDto> Trabajadores { get; set; } = [];
}

public class IndVerificacionTrabajadorDto
{
    public string? NombreCompleto { get; set; }
    public string? DocumentoIdentidadEnmascarado { get; set; }
    public decimal? Nota { get; set; }
    public bool Aprobado { get; set; }
    public DateTime? HoraServidorFirma { get; set; }
    public string? HashVerificacion { get; set; }
}

// ── Vigencia, cumplimiento y expediente (Fase 3) ────────────────────────────

/// <summary>Un documento que el trabajador debe volver a recibir.</summary>
public class IndPendienteDocDto
{
    public int DocumentoId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public int VersionVigente { get; set; }
    public int? VersionRecibida { get; set; }
    /// <summary>«Cambio material» | «Documento nuevo»</summary>
    public string Motivo { get; set; } = string.Empty;
}

public class IndCumplimientoItemDto
{
    public int WorkerId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? DocumentoIdentidad { get; set; }
    public string? Categoria { get; set; }
    /// <summary>Vigente | PorVencer | Vencida | SinInduccion | ActualizacionPendiente</summary>
    public string Estado { get; set; } = "SinInduccion";
    public DateOnly? UltimaInduccionFecha { get; set; }
    public DateOnly? VigenteHasta { get; set; }
    public int? SesionId { get; set; }
    public string? SesionCodigo { get; set; }
    public List<IndPendienteDocDto> Pendientes { get; set; } = [];
    public bool Exento { get; set; }
    public int? ExencionId { get; set; }
    public string? ExencionMotivo { get; set; }
    public DateOnly? ExencionHasta { get; set; }
    /// <summary>Puede firmar ATS: tiene inducción al día o está exento.</summary>
    public bool Habilitado { get; set; }
}

public class IndCumplimientoResumenDto
{
    public int Total { get; set; }
    public int Vigentes { get; set; }
    public int PorVencer { get; set; }
    public int Vencidas { get; set; }
    public int SinInduccion { get; set; }
    public int ActualizacionPendiente { get; set; }
    public int Exentos { get; set; }
}

public class IndCumplimientoDto
{
    public int ProyectoId { get; set; }
    public string? ProyectoNombre { get; set; }
    public bool GateActivo { get; set; }
    public int VigenciaMeses { get; set; }
    public IndCumplimientoResumenDto Resumen { get; set; } = new();
    public List<IndCumplimientoItemDto> Items { get; set; } = [];
}

public class IndGateDto
{
    public bool Activo { get; set; }
}

public class IndExencionCrearDto
{
    public int WorkerId { get; set; }
    public int ProyectoId { get; set; }
    public string Motivo { get; set; } = string.Empty;
    public DateOnly? Hasta { get; set; }
}

public class IndExpedienteInduccionDto
{
    public int SesionId { get; set; }
    public string? Codigo { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public int ProyectoId { get; set; }
    public string? ProyectoNombre { get; set; }
    public DateOnly Fecha { get; set; }
    public string SesionEstado { get; set; } = string.Empty;
    public decimal? Nota { get; set; }
    public bool Aprobado { get; set; }
    public int Intentos { get; set; }
    public DateTime? HoraServidorFirma { get; set; }
    public string? ActaPdfUrl { get; set; }
    public DateTime? SelladoEn { get; set; }
    public DateOnly? VigenteHasta { get; set; }
    public List<string> Documentos { get; set; } = [];
}

public class IndExpedienteAtsDto
{
    public int Id { get; set; }
    public string? Codigo { get; set; }
    public DateOnly Fecha { get; set; }
    public int ProyectoId { get; set; }
    public string? ProyectoNombre { get; set; }
    public string Estado { get; set; } = string.Empty;
}

public class IndExpedienteExencionDto
{
    public int Id { get; set; }
    public string? ProyectoNombre { get; set; }
    public string Motivo { get; set; } = string.Empty;
    public DateOnly? Hasta { get; set; }
    public DateTime CreadoEn { get; set; }
    public DateTime? RevocadaEn { get; set; }
}

public class IndExpedienteDto
{
    public int WorkerId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? DocumentoIdentidad { get; set; }
    public string? Categoria { get; set; }
    public string? Puesto { get; set; }
    public DateTime GeneradoEn { get; set; } = DateTime.UtcNow;
    public List<IndExpedienteInduccionDto> Inducciones { get; set; } = [];
    public List<IndExpedienteAtsDto> Ats { get; set; } = [];
    public List<IndExpedienteExencionDto> Exenciones { get; set; } = [];
}
