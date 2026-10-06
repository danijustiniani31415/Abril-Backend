using Abril_Backend.Infrastructure.Models;
using Abril_Backend.Shared.Models;

namespace Abril_Backend.Features.SsomaModule.InduccionFeature.Infrastructure.Models;

// ============================================================================
// INDUCCIÓN DIGITAL — reemplaza SSO-FO-118/119 (entrega y difusión de documentos),
// SSO-FO-031 (acta derecho a saber), SSO-FO-091 (evaluación) y GTH-FO-17 (registro).
// Mismo patrón que ATS: QR con token, DNI parcial + selfie + GPS + firma, auditoría
// con hash encadenado, y una vez Cerrada la sesión es inmutable.
// ============================================================================

/// <summary>Documento que se entrega en la inducción (RISST, IPERC línea base, PETS…).
/// Lo que se entrega siempre es una <see cref="SsIndDocumentoVersion"/>, nunca el documento "vivo".</summary>
public class SsIndDocumento
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    /// <summary>Documento | Procedimiento | IPERC | Legal</summary>
    public string Tipo { get; set; } = "Documento";
    public bool Obligatorio { get; set; } = true;
    public int Orden { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<SsIndDocumentoVersion> Versiones { get; set; } = [];
}

/// <summary>Versión publicada de un documento. INMUTABLE: un cambio crea la versión siguiente.
/// <see cref="ArchivoHash"/> es el SHA-256 del archivo — prueba de que no se alteró.</summary>
public class SsIndDocumentoVersion
{
    public int Id { get; set; }
    public int DocumentoId { get; set; }
    public int Version { get; set; }
    public string ArchivoUrl { get; set; } = string.Empty;
    public string ArchivoHash { get; set; } = string.Empty;
    /// <summary>true = obliga a re-aceptación de los trabajadores ya inducidos (Fase 3).</summary>
    public bool EsCambioMaterial { get; set; }
    public string? NotaCambio { get; set; }
    public bool Vigente { get; set; } = true;
    public int? PublicadoPorUserId { get; set; }
    public DateTime PublicadoEn { get; set; } = DateTime.UtcNow;

    public SsIndDocumento? Documento { get; set; }
}

/// <summary>Tema del temario (SSO-FO-031, 28 temas) — editable desde el front.</summary>
public class SsIndTema
{
    public int Id { get; set; }
    public int Orden { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;
}

/// <summary>Pregunta del banco (borrador editable). Tipos: VF, Alternativa, Relacionar.</summary>
public class SsIndPregunta
{
    public int Id { get; set; }
    public string Tipo { get; set; } = "VF";
    public string Enunciado { get; set; } = string.Empty;
    public decimal Puntos { get; set; } = 1;
    public int Orden { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<SsIndPreguntaOpcion> Opciones { get; set; } = [];
}

/// <summary>VF: una sola opción con EsCorrecta = "la afirmación es verdadera". Alternativa: EsCorrecta
/// marca la correcta. Relacionar: Texto es el ítem y ParTexto su pareja correcta.</summary>
public class SsIndPreguntaOpcion
{
    public int Id { get; set; }
    public int PreguntaId { get; set; }
    public string Texto { get; set; } = string.Empty;
    public bool EsCorrecta { get; set; }
    public string? ParTexto { get; set; }
    public int Orden { get; set; }

    public SsIndPregunta? Pregunta { get; set; }
}

/// <summary>Evaluación publicada: foto inmutable del banco (preguntas + claves) al momento de publicar.
/// Los intentos y las sesiones apuntan a esta versión, así la nota siempre es reproducible.</summary>
public class SsIndEvaluacionVersion
{
    public int Id { get; set; }
    public int Version { get; set; }
    public decimal NotaMinima { get; set; } = 14;
    public decimal NotaMaxima { get; set; } = 20;
    public int DuracionMin { get; set; } = 15;
    /// <summary>jsonb con las preguntas y claves (ver InduccionService al publicar).</summary>
    public string ContenidoJson { get; set; } = "[]";
    public string ContenidoHash { get; set; } = string.Empty;
    public bool Vigente { get; set; } = true;
    public int? PublicadoPorUserId { get; set; }
    public DateTime PublicadoEn { get; set; } = DateTime.UtcNow;
}

/// <summary>La "sala": una inducción en un proyecto y fecha. El instructor la abre, se proyecta el QR,
/// los trabajadores se adhieren, y al cerrarla (con la firma del instructor) queda inmutable y se genera el acta.</summary>
public class SsIndSesion
{
    public int Id { get; set; }
    /// <summary>"{ABREV}-IND-0001" — correlativo por proyecto en ss_documento_correlativo (tipo IND).</summary>
    public string? Codigo { get; set; }
    public int ProyectoId { get; set; }
    public DateOnly Fecha { get; set; }
    public string Tipo { get; set; } = "InduccionHombreNuevo";
    public decimal Horas { get; set; } = 2;

    public int InstructorWorkerId { get; set; }
    public string? InstructorNombre { get; set; }
    public string? InstructorCargo { get; set; }

    public int EvaluacionVersionId { get; set; }

    /// <summary>Token embebido en el QR — sin login, acotado a esta sesión y expira solo.</summary>
    public Guid QrToken { get; set; } = Guid.NewGuid();
    public DateTime QrExpiraEn { get; set; }

    /// <summary>Abierta | Cerrada | Anulada. Cerrada es inmutable; Anulada conserva motivo/quién/cuándo (nunca se borra).</summary>
    public string Estado { get; set; } = "Abierta";

    public string? InstructorFirmaUrl { get; set; }
    public string? InstructorFirmaHash { get; set; }
    public DateTime? InstructorHoraServidor { get; set; }
    public DateTime? CerradaEn { get; set; }

    public string? ActaPdfUrl { get; set; }
    /// <summary>Código estable del QR (hash del contenido firmado, no del archivo).</summary>
    public string? ActaPdfHash { get; set; }
    /// <summary>SHA-256 real de los bytes del PDF — es lo que se sella con la TSA.</summary>
    public string? ActaPdfSha256 { get; set; }

    public string? AnuladoMotivo { get; set; }
    public int? AnuladoPorUserId { get; set; }
    public DateTime? AnuladoEn { get; set; }

    public int? CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Project? Proyecto { get; set; }
    public SsIndEvaluacionVersion? EvaluacionVersion { get; set; }

    public ICollection<SsIndSesionDocumento> Documentos { get; set; } = [];
    public ICollection<SsIndSesionTema> Temas { get; set; } = [];
    public ICollection<SsIndAsistencia> Asistencias { get; set; } = [];
}

/// <summary>Documento + versión exacta que se entrega en ESTA sesión (foto, no referencia viva).</summary>
public class SsIndSesionDocumento
{
    public int Id { get; set; }
    public int SesionId { get; set; }
    public int DocumentoVersionId { get; set; }

    public SsIndSesion? Sesion { get; set; }
    public SsIndDocumentoVersion? DocumentoVersion { get; set; }
}

/// <summary>Tema tratado en la sesión; se copia el texto (snapshot) por si el catálogo cambia.</summary>
public class SsIndSesionTema
{
    public int Id { get; set; }
    public int SesionId { get; set; }
    public int? TemaId { get; set; }
    public int Orden { get; set; }
    public string Descripcion { get; set; } = string.Empty;

    public SsIndSesion? Sesion { get; set; }
}

/// <summary>Adhesión y firma de un trabajador en la sesión. Los campos de identidad son snapshot
/// (si luego cambia de categoría o contratista, esta acta no cambia).</summary>
public class SsIndAsistencia
{
    public int Id { get; set; }
    public int SesionId { get; set; }
    public int WorkerId { get; set; }

    public string? NombreCompleto { get; set; }
    public string? DocumentoIdentidad { get; set; }
    public string? Categoria { get; set; }
    public string? Especialidad { get; set; }
    public string? ContratistaNombre { get; set; }
    public string? ExperienciaActividad { get; set; }

    /// <summary>Pendiente | Firmado | Rechazado (selfie no válida u otro motivo, lo marca el instructor).</summary>
    public string Estado { get; set; } = "Pendiente";
    public bool OrigenOffline { get; set; }
    public DateTime? HoraServidorFirma { get; set; }
    public DateTime? HoraDispositivo { get; set; }
    public decimal? Lat { get; set; }
    public decimal? Lng { get; set; }
    public decimal? PrecisionMetros { get; set; }

    public string? SelfieUrl { get; set; }
    public string? SelfieHash { get; set; }
    public string? FirmaUrl { get; set; }
    public string? FirmaHash { get; set; }
    public string? IpOrigen { get; set; }
    public string? UserAgent { get; set; }

    public string? DeclaracionVersion { get; set; }
    public int Intentos { get; set; }
    public decimal? Nota { get; set; }
    public bool Aprobado { get; set; }

    /// <summary>Hash estable fijado una sola vez al firmar (base del QR de verificación del acta).</summary>
    public string? HashVerificacion { get; set; }

    public int? ValidadoPorUserId { get; set; }
    public DateTime? ValidadoEn { get; set; }
    public string? RechazoMotivo { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public SsIndSesion? Sesion { get; set; }
    public Worker? Worker { get; set; }
    public ICollection<SsIndAsistenciaDocumento> Documentos { get; set; } = [];
    public ICollection<SsIndIntento> IntentosEvaluacion { get; set; } = [];
}

/// <summary>Confirmación de recepción por documento (reemplaza la "firma recibido" de SSO-FO-118/119).</summary>
public class SsIndAsistenciaDocumento
{
    public int Id { get; set; }
    public int AsistenciaId { get; set; }
    public int DocumentoVersionId { get; set; }
    public DateTime RecibidoEn { get; set; } = DateTime.UtcNow;
    public int? SegundosAbierto { get; set; }

    public SsIndAsistencia? Asistencia { get; set; }
    public SsIndDocumentoVersion? DocumentoVersion { get; set; }
}

/// <summary>Cada intento de evaluación queda guardado (intentos ilimitados, nota mínima 14).</summary>
public class SsIndIntento
{
    public int Id { get; set; }
    public int AsistenciaId { get; set; }
    public int EvaluacionVersionId { get; set; }
    public string RespuestasJson { get; set; } = "[]";
    public decimal Nota { get; set; }
    public bool Aprobado { get; set; }
    public DateTime? IniciadoEn { get; set; }
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;

    public SsIndAsistencia? Asistencia { get; set; }
    public SsIndEvaluacionVersion? EvaluacionVersion { get; set; }
}

/// <summary>Log de auditoría INSERT-ONLY, hash encadenado por sesión — igual que SsAtsAuditLog.</summary>
public class SsIndAuditLog
{
    public long Id { get; set; }
    public int SesionId { get; set; }
    public string Evento { get; set; } = string.Empty;
    public int? UserId { get; set; }
    public string? IpOrigen { get; set; }
    public string? Detalle { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? HashAnterior { get; set; }
    public string Hash { get; set; } = string.Empty;

    public SsIndSesion? Sesion { get; set; }
}

/// <summary>Sello de tiempo RFC 3161 de una sesión cerrada: una TSA externa certifica que el digest (acta PDF +
/// cadena de auditoría) existía en esa fecha, así el registro no depende solo de nuestra base de datos.</summary>
public class SsIndSello
{
    public int Id { get; set; }
    public int SesionId { get; set; }
    public string Digest { get; set; } = string.Empty;
    public string Componentes { get; set; } = string.Empty;
    public string Proveedor { get; set; } = string.Empty;
    /// <summary>Pendiente | Sellado | Error (tras demasiados intentos; se puede reintentar a mano).</summary>
    public string Estado { get; set; } = "Pendiente";
    public byte[]? Token { get; set; }
    public DateTime? SelladoEn { get; set; }
    public string? SerialTsa { get; set; }
    public string? EmisorTsa { get; set; }
    public int Intentos { get; set; }
    public string? UltimoError { get; set; }
    public DateTime? UltimoIntento { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public SsIndSesion? Sesion { get; set; }
}

/// <summary>Enciende o apaga, por proyecto, el bloqueo «sin inducción vigente no se firma ATS». Apagado por defecto.</summary>
public class SsIndGateProyecto
{
    public int Id { get; set; }
    public int ProyectoId { get; set; }
    public bool Activo { get; set; }
    public DateTime ActualizadoEn { get; set; } = DateTime.UtcNow;
    public int? ActualizadoPorUserId { get; set; }
}

/// <summary>Excepción al bloqueo para un trabajador en un proyecto (p. ej. inducido en papel). Se revoca, no se borra.</summary>
public class SsIndExencion
{
    public int Id { get; set; }
    public int WorkerId { get; set; }
    public int ProyectoId { get; set; }
    public string Motivo { get; set; } = string.Empty;
    public DateOnly? Hasta { get; set; }
    public int? CreadoPorUserId { get; set; }
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
    public DateTime? RevocadaEn { get; set; }
    public int? RevocadaPorUserId { get; set; }
}

/// <summary>Marca de «ya avisé esto esta semana» para el resumen de inducciones por renovar.</summary>
public class SsIndAlerta
{
    public int Id { get; set; }
    public int ProyectoId { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public string Periodo { get; set; } = string.Empty;
    public string? Detalle { get; set; }
    public DateTime CreadaEn { get; set; } = DateTime.UtcNow;
}

/// <summary>Respaldo de los archivos de una sesión en una cuenta de almacenamiento distinta a la de producción.</summary>
public class SsIndRespaldo
{
    public int Id { get; set; }
    public int SesionId { get; set; }
    /// <summary>Pendiente | Respaldado | Error | NoConfigurado</summary>
    public string Estado { get; set; } = "Pendiente";
    public int Archivos { get; set; }
    public long Bytes { get; set; }
    public string? Prefijo { get; set; }
    public DateTime? RespaldadoEn { get; set; }
    public int Intentos { get; set; }
    public string? UltimoError { get; set; }
    public DateTime? UltimoIntento { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public SsIndSesion? Sesion { get; set; }
}
