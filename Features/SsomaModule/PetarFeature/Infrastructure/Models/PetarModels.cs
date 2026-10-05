using Abril_Backend.Features.SsomaModule.AtsFeature.Infrastructure.Models;
using Abril_Backend.Infrastructure.Models;
using Abril_Backend.Shared.Models;

namespace Abril_Backend.Features.SsomaModule.PetarFeature.Infrastructure.Models;

/// <summary>Tipo de trabajo de alto riesgo (Trabajo en altura, Espacios confinados, Trabajo en
/// caliente, Izaje de cargas, Excavaciones, Trabajos eléctricos/LOTO). Catálogo editable.</summary>
public class SsPetarTipo
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    /// <summary>Código del formato SSO-FO oficial del que se transcribió el checklist (ej. "SSO-FO-039"), para trazabilidad ante SUNAFIL. Nulo si el tipo aún no tiene formato físico de respaldo.</summary>
    public string? Codigo { get; set; }
    public short Orden { get; set; }
    public bool Activo { get; set; } = true;

    public ICollection<SsPetarItem> Items { get; set; } = [];
}

/// <summary>Ítem de verificación del checklist previo (ej. "Línea de vida instalada y anclada
/// correctamente"), propio de cada tipo de PETAR.</summary>
public class SsPetarItem
{
    public int Id { get; set; }
    public int TipoId { get; set; }
    public string Texto { get; set; } = string.Empty;
    public short Orden { get; set; }
    public bool Activo { get; set; } = true;

    public SsPetarTipo? Tipo { get; set; }
}

/// <summary>
/// Permiso Escrito de Trabajo de Alto Riesgo (PETAR). SIEMPRE nace de un ATS ya firmado
/// (<see cref="AtsId"/> no nulable) — es la tarea puntual de mayor riesgo que el ejecutante
/// decidió hacer dentro de su jornada, nunca un documento suelto. Exige 3 firmas: ejecutante
/// (con selfie/geo, igual que el ATS), Supervisor/Responsable del trabajo, y SSOMA — esta última
/// es OBLIGATORIA siempre (a diferencia del Visto Bueno del ATS, que puede quedar pendiente sin
/// bloquear el PDF): un PETAR sin SSOMA no debería ni imprimirse como válido.
/// </summary>
public class SsPetar
{
    public int Id { get; set; }
    /// <summary>Código correlativo global (PETAR-000001) — lo asigna el repositorio al crear, desde ss_petar_codigo_seq.</summary>
    public string? Codigo { get; set; }
    public int AtsId { get; set; }
    public int TipoId { get; set; }
    public int WorkerId { get; set; }
    public int ProyectoId { get; set; }

    public string DescripcionTrabajo { get; set; } = string.Empty;
    public string? Lugar { get; set; }
    public DateOnly Fecha { get; set; }
    public TimeOnly? HoraInicio { get; set; }
    public TimeOnly? HoraFin { get; set; }

    // ── Firma del ejecutante (mismo patrón de trazabilidad que SsAts) ──
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

    // ── Capataz / Maestro de obra (primer nivel de validación; solo cuando el ejecutante es obrero) ──
    public int? CapatazWorkerId { get; set; }
    public string? CapatazNombre { get; set; }
    public string? CapatazCargo { get; set; }
    public string? CapatazFirmaUrl { get; set; }
    public string? CapatazFirmaHash { get; set; }
    public DateTime? CapatazHoraServidor { get; set; }

    // ── Supervisor / Responsable del trabajo (obligatorio para pasar a Firmado) ──
    public int? SupervisorWorkerId { get; set; }
    public string? SupervisorNombre { get; set; }
    public string? SupervisorCargo { get; set; }
    public string? SupervisorFirmaUrl { get; set; }
    public string? SupervisorFirmaHash { get; set; }
    public DateTime? SupervisorHoraServidor { get; set; }

    // ── SSOMA (obligatorio siempre, no removible) ──
    public int? SsomaWorkerId { get; set; }
    public string? SsomaNombre { get; set; }
    public string? SsomaCargo { get; set; }
    public string? SsomaFirmaUrl { get; set; }
    public string? SsomaFirmaHash { get; set; }
    public DateTime? SsomaHoraServidor { get; set; }

    /// <summary>"Borrador" (checklist + firma ejecutante en curso) → "Firmado" (las 3 firmas
    /// completas, trabajo autorizado a iniciar) → "Cerrado" (trabajo terminado, área verificada
    /// segura por el ejecutante).</summary>
    public string Estado { get; set; } = "Borrador";

    public DateTime? CierreHoraServidor { get; set; }
    public string? CierreObservaciones { get; set; }
    public string? CierreFirmaUrl { get; set; }
    public string? CierreFirmaHash { get; set; }

    public string? PdfUrl { get; set; }
    public string? PdfHash { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Presente cuando este PETAR nació de "adherirse" a un PETAR grupal (mismo QR del
    /// ATS grupal) — el contenido (Respuestas de abajo) es una copia tomada del grupo, y las
    /// firmas de Supervisor/SSOMA NO viven en este registro individual sino en el grupal
    /// (SupervisorFirmaUrl/SsomaFirmaUrl de arriba quedan sin usar en ese caso — ver
    /// PetarRepository.ToDto, que las rellena desde PetarGrupo al armar la respuesta).</summary>
    public int? PetarGrupoId { get; set; }

    public SsAts? Ats { get; set; }
    public SsPetarTipo? Tipo { get; set; }
    public Worker? Worker { get; set; }
    public Worker? SupervisorWorker { get; set; }
    public Worker? SsomaWorker { get; set; }
    public Project? Proyecto { get; set; }
    public SsPetarGrupo? PetarGrupo { get; set; }

    public ICollection<SsPetarItemRespuesta> Respuestas { get; set; } = [];
    public SsPetarIzajeGrua? IzajeGrua { get; set; }
}

/// <summary>
/// PETAR GRUPAL: el checklist (tipo de trabajo de alto riesgo, descripción, verificación SI/NO/NA)
/// se llena UNA sola vez para toda la cuadrilla expuesta a ese riesgo puntual — nace ligado a un
/// ATS grupal, y puede haber MÁS DE UNO por ATS grupal (ej. Altura Y Espacio Confinado el mismo
/// día). Supervisor y SSOMA firman UNA vez acá, no una por trabajador. Cada integrante que se
/// adhiere (mismo QR del ATS, sin escanear nada nuevo) crea su propia fila en <see cref="SsPetar"/>
/// (vía <see cref="SsPetar.PetarGrupoId"/>) con su propia selfie+geo+firma de ejecutante.
/// </summary>
public class SsPetarGrupo
{
    public int Id { get; set; }
    public int AtsGrupoId { get; set; }
    /// <summary>Código por proyecto, igual que el del ATS grupal: "{ABREV}-PETG-0001". Se imprime junto al del ATS vinculado.</summary>
    public string? Codigo { get; set; }
    public int TipoId { get; set; }
    public int ProyectoId { get; set; }
    public int CreadoPorWorkerId { get; set; }

    public string DescripcionTrabajo { get; set; } = string.Empty;
    public string? Lugar { get; set; }
    public DateOnly Fecha { get; set; }
    public TimeOnly? HoraInicio { get; set; }
    public TimeOnly? HoraFin { get; set; }

    /// <summary>"Activo" acepta adhesiones nuevas; "Cerrado" deja de aceptarlas — los PETAR ya
    /// adheridos no se ven afectados.</summary>
    public string Estado { get; set; } = "Activo";

    // ── Capataz / Maestro de obra (primer nivel de validación; solo cuando el ejecutante es obrero) ──
    public int? CapatazWorkerId { get; set; }
    public string? CapatazNombre { get; set; }
    public string? CapatazCargo { get; set; }
    public string? CapatazFirmaUrl { get; set; }
    public string? CapatazFirmaHash { get; set; }
    public DateTime? CapatazHoraServidor { get; set; }
    /// <summary>Evidencia de presencia del Capataz cuando firma por el link público (no tiene cuenta): selfie + geo.</summary>
    public string? CapatazSelfieUrl { get; set; }
    public string? CapatazSelfieHash { get; set; }
    public decimal? CapatazLat { get; set; }
    public decimal? CapatazLng { get; set; }
    public decimal? CapatazPrecisionMetros { get; set; }
    public DateTime? CapatazHoraDispositivo { get; set; }

    public int? SupervisorWorkerId { get; set; }
    public string? SupervisorNombre { get; set; }
    public string? SupervisorCargo { get; set; }
    public string? SupervisorFirmaUrl { get; set; }
    public string? SupervisorFirmaHash { get; set; }
    public DateTime? SupervisorHoraServidor { get; set; }

    public int? SsomaWorkerId { get; set; }
    public string? SsomaNombre { get; set; }
    public string? SsomaCargo { get; set; }
    public string? SsomaFirmaUrl { get; set; }
    public string? SsomaFirmaHash { get; set; }
    public DateTime? SsomaHoraServidor { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public SsAtsGrupo? AtsGrupo { get; set; }
    public SsPetarTipo? Tipo { get; set; }
    public Project? Proyecto { get; set; }
    public Worker? CreadoPorWorker { get; set; }
    public Worker? SupervisorWorker { get; set; }
    public Worker? SsomaWorker { get; set; }

    public ICollection<SsPetarGrupoItemRespuesta> Respuestas { get; set; } = [];
    public ICollection<SsPetar> Adhesiones { get; set; } = [];
}

/// <summary>Mismo shape que <see cref="SsPetarItemRespuesta"/>, a nivel de grupo.</summary>
public class SsPetarGrupoItemRespuesta
{
    public int Id { get; set; }
    public int PetarGrupoId { get; set; }
    public int ItemId { get; set; }
    public string Texto { get; set; } = string.Empty;
    public string Respuesta { get; set; } = string.Empty;
    public short Orden { get; set; }

    public SsPetarGrupo? PetarGrupo { get; set; }
}

/// <summary>Campos técnicos propios del izaje con grúa (SSO-FO-043) — tipo/modelo/capacidad de la
/// grúa, ángulo de pluma, peso de la carga, etc. No aplican a los demás tipos de PETAR (por eso
/// viven en tabla aparte, 1:1 con ss_petar, en vez de columnas nulas en la tabla genérica) ni al
/// nuevo SSO-FO-153 (equipo no convencional/tecles), que usa su propio checklist genérico.</summary>
public class SsPetarIzajeGrua
{
    public int PetarId { get; set; }

    /// <summary>"TorreGrua" | "GruaMovil".</summary>
    public string? TipoGrua { get; set; }
    public string? FabricanteOMarca { get; set; }
    public string? ModeloOPlaca { get; set; }
    public string? SerieOTarjetaCirculacion { get; set; }
    public decimal? LongitudPlumaBrazoM { get; set; }
    public decimal? RadioMaximoGiroM { get; set; }
    public string? DireccionGradoGiro { get; set; }
    public decimal? ElevacionM { get; set; }
    public decimal? AnguloPluma { get; set; }
    public decimal? CapacidadCertificadaTon { get; set; }
    public decimal? PesoCargaTotalTon { get; set; }
    public decimal? PorcentajeCapacidad { get; set; }
    public string? TamanoEstrobo { get; set; }
    public string? Observaciones { get; set; }

    public SsPetar? Petar { get; set; }
}

/// <summary>Snapshot de la respuesta a un ítem del checklist (SI/NO/NA) — texto copiado del
/// catálogo al crear, mismo criterio de inmutabilidad que SsAtsPasoSeleccionado.</summary>
public class SsPetarItemRespuesta
{
    public int Id { get; set; }
    public int PetarId { get; set; }
    public int ItemId { get; set; }
    public string Texto { get; set; } = string.Empty;
    public string Respuesta { get; set; } = string.Empty; // SI | NO | NA
    public short Orden { get; set; }

    public SsPetar? Petar { get; set; }
}

/// <summary>Log de auditoría INSERT-ONLY, hash encadenado — igual mecanismo que SsAtsAuditLog.</summary>
public class SsPetarAuditLog
{
    public long Id { get; set; }
    public int PetarId { get; set; }
    public string Evento { get; set; } = string.Empty;
    public int? UserId { get; set; }
    public string? IpOrigen { get; set; }
    public string? Detalle { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? HashAnterior { get; set; }
    public string Hash { get; set; } = string.Empty;

    public SsPetar? Petar { get; set; }
}
