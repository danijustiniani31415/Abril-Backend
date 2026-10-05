namespace Abril_Backend.Features.SsomaModule.PetarFeature.Application.Dtos;

// ── Catálogo ─────────────────────────────────────────────────────────────────

public class PetarItemDto
{
    public int Id { get; set; }
    public string Texto { get; set; } = string.Empty;
}

public class PetarTipoDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Codigo { get; set; }
    public List<PetarItemDto> Items { get; set; } = [];
}

public class PetarInitDto
{
    public List<PetarTipoDto> Tipos { get; set; } = [];
    /// <summary>Datos del ATS de origen, para no tener que volver a pedirlos (proyecto, lugar sugerido, etc.).</summary>
    public int AtsId { get; set; }
    public string? AtsLugar { get; set; }
    public string? AtsActividad { get; set; }
}

// ── Guardar borrador ─────────────────────────────────────────────────────────

public class PetarItemRespuestaRequestDto
{
    public int ItemId { get; set; }
    /// <summary>"SI" | "NO" | "NA".</summary>
    public string Respuesta { get; set; } = string.Empty;
}

public class PetarGuardarRequestDto
{
    public int AtsId { get; set; }
    public int TipoId { get; set; }
    public string DescripcionTrabajo { get; set; } = string.Empty;
    public string? Lugar { get; set; }
    public string? HoraInicio { get; set; } // "HH:mm"
    public string? HoraFin { get; set; }
    public List<PetarItemRespuestaRequestDto> Respuestas { get; set; } = [];
    /// <summary>Solo cuando el tipo elegido es "Izaje de cargas" (SSO-FO-043) — campos técnicos de
    /// la grúa que el formato físico exige y el checklist SI/NO no cubre.</summary>
    public PetarIzajeGruaDto? IzajeGrua { get; set; }
}

/// <summary>Campos técnicos propios del izaje con grúa (SSO-FO-043): tipo/modelo/capacidad de la
/// grúa, ángulo de pluma, peso de la carga, etc. Todos opcionales — el checklist SI/NO ya es
/// obligatorio por sí solo y no depende de que se llenen estos datos.</summary>
public class PetarIzajeGruaDto
{
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
}

// ── Firmas ───────────────────────────────────────────────────────────────────

public class PetarFirmarRequestDto
{
    public string SelfieBase64 { get; set; } = string.Empty;
    public string FirmaBase64 { get; set; } = string.Empty;
    public DateTime? HoraDispositivo { get; set; }
    public decimal? Lat { get; set; }
    public decimal? Lng { get; set; }
    public decimal? PrecisionMetros { get; set; }
}

public class PetarFirmarVistoRequestDto
{
    public string FirmaBase64 { get; set; } = string.Empty;
}

public class PetarCerrarRequestDto
{
    public string FirmaBase64 { get; set; } = string.Empty;
    public string? Observaciones { get; set; }
}

// ── Respuesta ────────────────────────────────────────────────────────────────

public class PetarItemRespuestaResponseDto
{
    public int ItemId { get; set; }
    public string Texto { get; set; } = string.Empty;
    public string Respuesta { get; set; } = string.Empty;
}

public class PetarResponseDto
{
    public int Id { get; set; }
    public string? Codigo { get; set; }
    public int AtsId { get; set; }
    /// <summary>Código del ATS del que nace este PETAR (ligados en ambos sentidos).</summary>
    public string? AtsCodigo { get; set; }
    public int TipoId { get; set; }
    public string? TipoNombre { get; set; }
    public string? TipoCodigo { get; set; }
    public int WorkerId { get; set; }
    public string? WorkerNombre { get; set; }
    public int ProyectoId { get; set; }
    public string? ProyectoNombre { get; set; }

    public string DescripcionTrabajo { get; set; } = string.Empty;
    public string? Lugar { get; set; }
    public DateOnly Fecha { get; set; }
    public string? HoraInicio { get; set; }
    public string? HoraFin { get; set; }

    public DateTime? HoraServidorFirma { get; set; }
    public decimal? Lat { get; set; }
    public decimal? Lng { get; set; }
    public decimal? PrecisionMetros { get; set; }
    public string? SelfieUrl { get; set; }
    public string? FirmaUrl { get; set; }

    public string? CapatazNombre { get; set; }
    public string? CapatazCargo { get; set; }
    public string? CapatazFirmaUrl { get; set; }
    public DateTime? CapatazHoraServidor { get; set; }
    /// <summary>true si el ejecutante es obrero: la cadena del PETAR lleva firma de Capataz antes del Supervisor.</summary>
    public bool RequiereCapataz { get; set; }
    public bool PuedeFirmarCapataz { get; set; }

    public string? SupervisorNombre { get; set; }
    public string? SupervisorCargo { get; set; }
    public string? SupervisorFirmaUrl { get; set; }
    public DateTime? SupervisorHoraServidor { get; set; }

    public string? SsomaNombre { get; set; }
    public string? SsomaCargo { get; set; }
    public string? SsomaFirmaUrl { get; set; }
    public DateTime? SsomaHoraServidor { get; set; }

    public string Estado { get; set; } = string.Empty;
    public DateTime? CierreHoraServidor { get; set; }
    public string? CierreObservaciones { get; set; }
    public string? CierreFirmaUrl { get; set; }
    public string? PdfHash { get; set; }

    public bool PuedeFirmarSupervisor { get; set; }
    public bool PuedeFirmarSsoma { get; set; }
    public bool PuedeCerrar { get; set; }

    public List<PetarItemRespuestaResponseDto> Respuestas { get; set; } = [];
    public PetarIzajeGruaDto? IzajeGrua { get; set; }
}

public class PetarVerificacionPublicaDto
{
    public bool Encontrado { get; set; }
    public bool Valido { get; set; }
    public string? WorkerNombre { get; set; }
    public string? ProyectoNombre { get; set; }
    public string? TipoNombre { get; set; }
    public DateOnly? Fecha { get; set; }
    public string? Estado { get; set; }
}

public class PetarFiltroDto
{
    public int? ProyectoId { get; set; }
    public int? WorkerId { get; set; }
    public int? AtsId { get; set; }
    public DateOnly? FechaDesde { get; set; }
    public DateOnly? FechaHasta { get; set; }
    public string? Estado { get; set; }
    public int Page { get; set; } = 1;
}

public class PetarListResponseDto
{
    public List<PetarResponseDto> Data { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalRecords { get; set; }
    public int TotalPages { get; set; }
}

// ── PETAR Grupal ─────────────────────────────────────────────────────────────

/// <summary>Mismo checklist que PetarGuardarRequestDto, pero para toda la cuadrilla — nace de un
/// ATS grupal (no de un AtsId individual) y puede haber varios por ATS grupal (ej. Altura Y
/// Espacio Confinado el mismo día).</summary>
public class PetarGrupoCrearRequestDto
{
    public int AtsGrupoId { get; set; }
    public int TipoId { get; set; }
    public string DescripcionTrabajo { get; set; } = string.Empty;
    public string? Lugar { get; set; }
    public string? HoraInicio { get; set; }
    public string? HoraFin { get; set; }
    public List<PetarItemRespuestaRequestDto> Respuestas { get; set; } = [];
}

/// <summary>Generar el PETAR de la cuadrilla desde el QR (sin cuenta): quien lo pide se identifica con nombre + DNI.</summary>
public class PetarGrupoCrearPublicoDto : PetarGrupoCrearRequestDto
{
    public int WorkerId { get; set; }
    public string DniConfirmacion { get; set; } = string.Empty;
}

public class PetarGrupoCrearResponseDto
{
    public int Id { get; set; }
}

/// <summary>Panel del autor: estado del checklist grupal y cuántos ejecutantes ya se adhirieron.</summary>
/// <summary>Evidencia de presencia (selfie + geo) que acompaña una firma hecha por link público.</summary>
public record PetarEvidenciaPresencia(string? SelfieUrl, string? SelfieHash, decimal? Lat, decimal? Lng, decimal? PrecisionMetros, DateTime? HoraDispositivo);

/// <summary>PETAR de la cuadrilla que espera la firma del Capataz, visto desde su página pública.</summary>
public class PetarGrupoCapatazPublicoDto
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string? TipoNombre { get; set; }
    public string DescripcionTrabajo { get; set; } = string.Empty;
    public int TotalAdhesiones { get; set; }
    public bool CapatazFirmado { get; set; }
}

public class PetarGrupoCapatazFirmarPublicoDto
{
    public string AtsToken { get; set; } = string.Empty;
    public int WorkerId { get; set; }
    public string DniConfirmacion { get; set; } = string.Empty;
    public string SelfieBase64 { get; set; } = string.Empty;
    public DateTime? HoraDispositivo { get; set; }
    public decimal? Lat { get; set; }
    public decimal? Lng { get; set; }
    public decimal? PrecisionMetros { get; set; }
}

public class PetarGrupoEstadoDto
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string? AtsCodigo { get; set; }
    public int AtsGrupoId { get; set; }
    public int ProyectoId { get; set; }
    public string? TipoNombre { get; set; }
    public string DescripcionTrabajo { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public bool RequiereCapataz { get; set; }
    public bool CapatazFirmado { get; set; }
    public bool PuedeFirmarCapataz { get; set; }
    public bool SupervisorFirmado { get; set; }
    public bool SsomaFirmado { get; set; }
    public bool PuedeFirmarSupervisor { get; set; }
    public bool PuedeFirmarSsoma { get; set; }
    public int TotalAdhesiones { get; set; }
    public List<string> TrabajadoresAdheridos { get; set; } = [];
}

/// <summary>Lo que ve el trabajador en la página pública del QR (la misma del ATS grupal) para
/// elegir cuáles PETAR le aplican a él — no todos en la cuadrilla hacen la misma tarea de alto
/// riesgo.</summary>
public class PetarGrupoResumenPublicoDto
{
    public int Id { get; set; }
    public string? TipoNombre { get; set; }
    public string DescripcionTrabajo { get; set; } = string.Empty;
    public string? Lugar { get; set; }
    public string? HoraInicio { get; set; }
    public string? HoraFin { get; set; }
    public List<PetarItemRespuestaResponseDto> Respuestas { get; set; } = [];
}

/// <summary>Adhesión liviana — mismo criterio que AtsGrupoUnirseRequestDto: sin login, el token
/// del ATS grupal (ya validado en ese paso) es lo que habilita esto; acá solo se repite para
/// confirmar que este PETAR puntual de verdad pertenece a ese mismo ATS grupal.</summary>
public class PetarGrupoUnirseRequestDto
{
    public string AtsToken { get; set; } = string.Empty;
    public int WorkerId { get; set; }
    /// <summary>El id del propio SsAts de este trabajador, devuelto al adherirse al ATS grupal —
    /// el PETAR individual se ancla ahí, igual que un PETAR nacido de un ATS normal.</summary>
    public int AtsIdPropio { get; set; }
    public string SelfieBase64 { get; set; } = string.Empty;
    public string FirmaBase64 { get; set; } = string.Empty;
    public DateTime? HoraDispositivo { get; set; }
    public decimal? Lat { get; set; }
    public decimal? Lng { get; set; }
    public decimal? PrecisionMetros { get; set; }
}
