using System.ComponentModel.DataAnnotations.Schema;
using Abril_Backend.Infrastructure.Models;
using Abril_Backend.Shared.Models;

namespace Abril_Backend.Features.SsomaModule.InspeccionEquipoFeature.Infrastructure.Models;

/// <summary>Un tipo = un formato SSO-FO-xxx (arnés, minicargador…). Define cómo se responde y qué firmas exige.</summary>
[Table("ss_insp_equipo_tipo")]
public class SsInspEquipoTipo
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string CodigoFormato { get; set; } = string.Empty;
    public string? Version { get; set; }
    public DateOnly? FechaVersion { get; set; }
    public string? Elaboracion { get; set; }
    public string? Revision { get; set; }
    public string? Aprobacion { get; set; }
    /// <summary>"trabajador" = un check list por persona (QR del proyecto) | "equipo" = check list de un equipo registrado (QR del equipo).</summary>
    public string IdentificaPor { get; set; } = "equipo";
    /// <summary>"C_NC_NA" | "SI_NO_NA".</summary>
    public string ModoRespuesta { get; set; } = "C_NC_NA";
    public string? Instrucciones { get; set; }
    public bool RequiereTurno { get; set; }
    public bool RegistraCorreccion { get; set; }
    public bool RequierePlanificada { get; set; }
    public bool RequiereEmpresaTipo { get; set; }
    public bool PermiteVariantes { get; set; }
    public bool ReqFirmaCapataz { get; set; }
    public bool ReqFirmaResidente { get; set; } = true;
    public bool ReqFirmaSsoma { get; set; } = true;
    public bool Activo { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<SsInspEquipoTipoVariante> Variantes { get; set; } = [];
    [ForeignKey(nameof(SsInspPlantillaItem.TipoId))]
    public ICollection<SsInspPlantillaItem> Items { get; set; } = [];
}

/// <summary>Equipo concreto dentro de un tipo (arnés, línea de anclaje doble, tambor retráctil…) con su propia imagen.</summary>
[Table("ss_insp_equipo_tipo_variante")]
public class SsInspEquipoTipoVariante
{
    public int Id { get; set; }
    public int TipoId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? ImagenUrl { get; set; }
    public int Orden { get; set; }
    public bool Activo { get; set; } = true;

    public SsInspEquipoTipo? Tipo { get; set; }
}

[Table("ss_insp_plantilla_item")]
public class SsInspPlantillaItem
{
    public int Id { get; set; }
    public int TipoId { get; set; }
    /// <summary>null = ítem común a todos los equipos del tipo (se agrega al final de cada equipo).</summary>
    public int? VarianteId { get; set; }
    public int Orden { get; set; }
    public string? Grupo { get; set; }
    public string? NivelRiesgo { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public bool EsCritico { get; set; }
    /// <summary>La pregunta está redactada en negativo ("¿Existen fugas?"): "SI" es lo malo.</summary>
    public bool Invertido { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(SsInspItemMarcador.ItemId))]
    public ICollection<SsInspItemMarcador> Marcadores { get; set; } = [];
}

[Table("ss_insp_item_marcador")]
public class SsInspItemMarcador
{
    public int Id { get; set; }
    public int ItemId { get; set; }
    public decimal XPct { get; set; }
    public decimal YPct { get; set; }
    /// <summary>Punta de la flecha (en % de la imagen). Null = el marcador no lleva flecha.</summary>
    public decimal? PxPct { get; set; }
    public decimal? PyPct { get; set; }
}

[Table("ss_insp_equipo")]
public class SsInspEquipo
{
    public int Id { get; set; }
    public int TipoId { get; set; }
    public int ProyectoId { get; set; }
    public int? VarianteId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string? FotoUrl { get; set; }
    public int? AsignadoWorkerId { get; set; }
    public int? RegistradoEnInspeccionId { get; set; }
    public string? EmpresaTipo { get; set; }
    public string? EmpresaNombre { get; set; }
    public Guid QrToken { get; set; } = Guid.NewGuid();
    /// <summary>"operativo" | "fuera_servicio" | "baja".</summary>
    public string Estado { get; set; } = "operativo";
    public string? BloqueadoMotivo { get; set; }
    public DateTime? BloqueadoEn { get; set; }
    public int? BloqueadoInspeccionId { get; set; }
    public bool Activo { get; set; } = true;
    public int? CreadoPorUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public SsInspEquipoTipo? Tipo { get; set; }
    public SsInspEquipoTipoVariante? Variante { get; set; }
    public Project? Proyecto { get; set; }
    public Worker? AsignadoWorker { get; set; }
}

[Table("ss_insp_inspeccion")]
public class SsInspInspeccion
{
    public int Id { get; set; }
    public string? Codigo { get; set; }
    public int? EquipoId { get; set; }
    public int TipoId { get; set; }
    public int ProyectoId { get; set; }
    public DateOnly Fecha { get; set; }
    public string? Turno { get; set; }
    public bool? Planificada { get; set; }
    public string? EmpresaTipo { get; set; }
    public string? EmpresaNombre { get; set; }
    /// <summary>"Pendiente_Firmas" | "Completo" | "Anulado".</summary>
    public string Estado { get; set; } = "Pendiente_Firmas";
    /// <summary>"conforme" | "con_observaciones" | "no_conforme_critico".</summary>
    public string Resultado { get; set; } = "conforme";
    public string? Observaciones { get; set; }

    public int OperadorWorkerId { get; set; }
    public DateTime HoraServidor { get; set; } = DateTime.UtcNow;
    public DateTime? HoraDispositivo { get; set; }
    public bool OrigenOffline { get; set; }
    public Guid? ClientId { get; set; }
    public decimal? Lat { get; set; }
    public decimal? Lng { get; set; }
    public decimal? PrecisionMetros { get; set; }
    public string? SelfieUrl { get; set; }
    public string? SelfieHash { get; set; }
    public string? FirmaUrl { get; set; }
    public string? FirmaHash { get; set; }
    public string? IpOrigen { get; set; }
    public string? UserAgent { get; set; }

    public int? CapatazWorkerId { get; set; }
    public string? CapatazNombre { get; set; }
    public string? CapatazCargo { get; set; }
    public string? CapatazFirmaUrl { get; set; }
    public string? CapatazFirmaHash { get; set; }
    public DateTime? CapatazHoraServidor { get; set; }

    public int? ResidenteWorkerId { get; set; }
    public string? ResidenteNombre { get; set; }
    public string? ResidenteCargo { get; set; }
    public string? ResidenteFirmaUrl { get; set; }
    public string? ResidenteFirmaHash { get; set; }
    public DateTime? ResidenteHoraServidor { get; set; }

    public int? SsomaWorkerId { get; set; }
    public string? SsomaNombre { get; set; }
    public string? SsomaCargo { get; set; }
    public string? SsomaFirmaUrl { get; set; }
    public string? SsomaFirmaHash { get; set; }
    public DateTime? SsomaHoraServidor { get; set; }

    public string? AnuladoMotivo { get; set; }
    public int? AnuladoPorUserId { get; set; }
    public DateTime? AnuladoEn { get; set; }

    public string? PdfUrl { get; set; }
    public string? PdfHash { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public SsInspEquipoTipo? Tipo { get; set; }
    public SsInspEquipo? Equipo { get; set; }
    public Project? Proyecto { get; set; }
    public Worker? OperadorWorker { get; set; }
    public ICollection<SsInspInspeccionVariante> Variantes { get; set; } = [];
    [ForeignKey(nameof(SsInspRespuesta.InspeccionId))]
    public ICollection<SsInspRespuesta> Respuestas { get; set; } = [];
}

[Table("ss_insp_inspeccion_variante")]
public class SsInspInspeccionVariante
{
    public int Id { get; set; }
    public int InspeccionId { get; set; }
    public int? VarianteId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? ImagenUrl { get; set; }
    public string CodigoEquipo { get; set; } = string.Empty;
    public int? EquipoId { get; set; }
    public int Orden { get; set; }

    public SsInspInspeccion? Inspeccion { get; set; }
    [ForeignKey(nameof(SsInspRespuesta.InspeccionVarianteId))]
    public ICollection<SsInspRespuesta> Respuestas { get; set; } = [];
}

[Table("ss_insp_respuesta")]
public class SsInspRespuesta
{
    public int Id { get; set; }
    public int InspeccionId { get; set; }
    public int? InspeccionVarianteId { get; set; }
    public int? ItemId { get; set; }
    public int Numero { get; set; }
    public int Orden { get; set; }
    public string? Grupo { get; set; }
    public string? NivelRiesgo { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public bool EsCritico { get; set; }
    public bool Invertido { get; set; }
    /// <summary>"cumple" | "no_cumple" | "na" (ya normalizado considerando Invertido).</summary>
    public string Respuesta { get; set; } = "cumple";
    public string? Observacion { get; set; }
    public bool? SeCorrigio { get; set; }
    public string? FotoUrl { get; set; }
}

[Table("ss_insp_equipo_evento")]
public class SsInspEquipoEvento
{
    public int Id { get; set; }
    public int EquipoId { get; set; }
    /// <summary>"bloqueo" | "liberacion" | "baja" | "reactivacion".</summary>
    public string Evento { get; set; } = string.Empty;
    public int? InspeccionId { get; set; }
    public int? UserId { get; set; }
    public int? WorkerId { get; set; }
    public string? Detalle { get; set; }
    public string? FotoUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

[Table("ss_insp_audit_log")]
public class SsInspAuditLog
{
    public long Id { get; set; }
    public int InspeccionId { get; set; }
    public string Evento { get; set; } = string.Empty;
    public int? UserId { get; set; }
    public string? IpOrigen { get; set; }
    public string? Detalle { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? HashAnterior { get; set; }
    public string Hash { get; set; } = string.Empty;
}
