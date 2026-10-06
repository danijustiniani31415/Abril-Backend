namespace Abril_Backend.Features.SsomaModule.InspeccionEquipoFeature.Application.Dtos;

// ============================================================================
// CATÁLOGO (formatos, equipos-variante, ítems con marcadores)
// ============================================================================

public class InspMarcadorDto
{
    public decimal X { get; set; }
    public decimal Y { get; set; }
    /// <summary>Punta de la flecha que señala la parte del equipo; null = sin flecha.</summary>
    public decimal? Px { get; set; }
    public decimal? Py { get; set; }
}

public class InspItemDto
{
    public int Id { get; set; }
    /// <summary>Número correlativo MOSTRADO dentro de su equipo (1..n). Se calcula por orden: no se guarda,
    /// así reordenar/agregar ítems nunca desfasa la guía de la imagen.</summary>
    public int Numero { get; set; }
    public int? VarianteId { get; set; }
    public int Orden { get; set; }
    public string? Grupo { get; set; }
    public string? NivelRiesgo { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public bool EsCritico { get; set; }
    public bool Invertido { get; set; }
    public bool Activo { get; set; }
    public List<InspMarcadorDto> Marcadores { get; set; } = [];
}

public class InspVarianteDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? ImagenUrl { get; set; }
    public int Orden { get; set; }
    public bool Activo { get; set; }
    /// <summary>Ítems propios de este equipo + los comunes del tipo al final, ya numerados 1..n.</summary>
    public List<InspItemDto> Items { get; set; } = [];
}

public class InspTipoDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string CodigoFormato { get; set; } = string.Empty;
    public string? Version { get; set; }
    public DateOnly? FechaVersion { get; set; }
    public string? Elaboracion { get; set; }
    public string? Revision { get; set; }
    public string? Aprobacion { get; set; }
    public string IdentificaPor { get; set; } = "equipo";
    public string ModoRespuesta { get; set; } = "C_NC_NA";
    public string? Instrucciones { get; set; }
    public bool RequiereTurno { get; set; }
    public bool RegistraCorreccion { get; set; }
    public bool RequierePlanificada { get; set; }
    public bool RequiereEmpresaTipo { get; set; }
    public bool PermiteVariantes { get; set; }
    public bool ReqFirmaCapataz { get; set; }
    public bool ReqFirmaResidente { get; set; }
    public bool ReqFirmaSsoma { get; set; }
    public bool Activo { get; set; }
    /// <summary>Tipos con variantes (arnés): cada una trae sus ítems. Tipos sin variantes (minicargador): vacío y se usa Items.</summary>
    public List<InspVarianteDto> Variantes { get; set; } = [];
    public List<InspItemDto> Items { get; set; } = [];
}

public class InspTipoGuardarDto
{
    public string Nombre { get; set; } = string.Empty;
    public string? Version { get; set; }
    public string? Instrucciones { get; set; }
    public bool RequiereTurno { get; set; }
    public bool RegistraCorreccion { get; set; }
    public bool RequierePlanificada { get; set; }
    public bool RequiereEmpresaTipo { get; set; }
    public bool ReqFirmaCapataz { get; set; }
    public bool ReqFirmaResidente { get; set; } = true;
    public bool ReqFirmaSsoma { get; set; } = true;
    public bool Activo { get; set; } = true;
}

public class InspVarianteGuardarDto
{
    public string Nombre { get; set; } = string.Empty;
    public int Orden { get; set; }
    public bool Activo { get; set; } = true;
}

public class InspItemGuardarDto
{
    public int? VarianteId { get; set; }
    public int Orden { get; set; }
    public string? Grupo { get; set; }
    public string? NivelRiesgo { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public bool EsCritico { get; set; }
    public bool Invertido { get; set; }
    public bool Activo { get; set; } = true;
}

public class InspMarcadoresGuardarDto
{
    public List<InspMarcadorDto> Marcadores { get; set; } = [];
}

public class InspImagenResponseDto
{
    public string Url { get; set; } = string.Empty;
}

// ============================================================================
// EQUIPOS
// ============================================================================

public class InspEquipoDto
{
    public int Id { get; set; }
    public int TipoId { get; set; }
    public string TipoNombre { get; set; } = string.Empty;
    public int ProyectoId { get; set; }
    public string? ProyectoNombre { get; set; }
    public int? VarianteId { get; set; }
    public string? VarianteNombre { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string? FotoUrl { get; set; }
    public int? AsignadoWorkerId { get; set; }
    public string? AsignadoNombre { get; set; }
    public string? EmpresaTipo { get; set; }
    public string? EmpresaNombre { get; set; }
    public string Estado { get; set; } = "operativo";
    public string? BloqueadoMotivo { get; set; }
    public DateTime? BloqueadoEn { get; set; }
    public Guid QrToken { get; set; }
    public bool Activo { get; set; }
    public DateOnly? UltimaInspeccion { get; set; }
}

public class InspEquipoGuardarDto
{
    public int TipoId { get; set; }
    public int ProyectoId { get; set; }
    public int? VarianteId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public int? AsignadoWorkerId { get; set; }
    public string? EmpresaTipo { get; set; }
    public string? EmpresaNombre { get; set; }
    public string? FotoBase64 { get; set; }
}

public class InspEquipoFiltroDto
{
    public int? ProyectoId { get; set; }
    public int? TipoId { get; set; }
    public string? Estado { get; set; }
    public string? Buscar { get; set; }
    /// <summary>Solo equipos operativos de formatos "por equipo" (se usan a diario) que hoy todavía no tienen su check list.</summary>
    public bool SinInspeccionHoy { get; set; }
}

public class InspEquipoEventoDto
{
    public int Id { get; set; }
    public string Evento { get; set; } = string.Empty;
    public int? InspeccionId { get; set; }
    public string? Usuario { get; set; }
    public string? Detalle { get; set; }
    public string? FotoUrl { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>Liberar (tras corregir) o dar de baja un equipo. La liberación exige detalle de qué se corrigió.</summary>
public class InspEquipoAccionDto
{
    public string Detalle { get; set; } = string.Empty;
    public string? FotoBase64 { get; set; }
}

// ============================================================================
// LLENADO (público, sin login)
// ============================================================================

public class InspWorkerOpcionDto
{
    public int WorkerId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? DniUltimos4 { get; set; }
}

/// <summary>Página pública del QR del PROYECTO (formatos por trabajador, ej. arnés).</summary>
public class InspProyectoPublicoDto
{
    public bool Valido { get; set; }
    public string? MotivoInvalido { get; set; }
    public string? ProyectoNombre { get; set; }
    public List<InspWorkerOpcionDto> Trabajadores { get; set; } = [];
    public List<InspTipoDto> Tipos { get; set; } = [];
}

/// <summary>Página pública del QR del EQUIPO (formatos por equipo, ej. minicargador).</summary>
public class InspEquipoPublicoDto
{
    public bool Valido { get; set; }
    public string? MotivoInvalido { get; set; }
    public InspEquipoDto? Equipo { get; set; }
    public InspTipoDto? Tipo { get; set; }
    public List<InspWorkerOpcionDto> Trabajadores { get; set; } = [];
}

public class InspMisEquiposRequestDto
{
    public int WorkerId { get; set; }
    public string DniConfirmacion { get; set; } = string.Empty;
    public int TipoId { get; set; }
}

/// <summary>Búsqueda por código al llenar: devuelve los equipos candidatos (con foto) para que la persona elija el suyo.</summary>
public class InspBuscarEquipoRequestDto
{
    public int WorkerId { get; set; }
    public string DniConfirmacion { get; set; } = string.Empty;
    public int TipoId { get; set; }
    public int? VarianteId { get; set; }
    public string Codigo { get; set; } = string.Empty;
}

public class InspRespuestaEnvioDto
{
    public int ItemId { get; set; }
    /// <summary>C_NC_NA: "c" | "nc" | "na". SI_NO_NA: "si" | "no" | "na" (el servidor lo normaliza según el ítem sea invertido o no).</summary>
    public string Valor { get; set; } = string.Empty;
    public string? Observacion { get; set; }
    public bool? SeCorrigio { get; set; }
    /// <summary>Obligatoria en un "no cumple" que no se corrigió en el acto — evidencia del defecto.</summary>
    public string? FotoBase64 { get; set; }
}

public class InspEquipoEnvioDto
{
    public int? VarianteId { get; set; }
    /// <summary>Si ya existe en el catálogo ("mis equipos"). Si es null se busca por código; si no existe se registra y exige FotoBase64.</summary>
    public int? EquipoId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    /// <summary>Foto del equipo con el código visible — solo la primera vez (equipo nuevo).</summary>
    public string? FotoBase64 { get; set; }
    /// <summary>La persona indicó "es otro equipo" aunque ya existan equipos con ese código (los códigos de campo se repiten): se registra uno nuevo con su foto.</summary>
    public bool EsEquipoNuevo { get; set; }
    public List<InspRespuestaEnvioDto> Respuestas { get; set; } = [];
}

public class InspInspeccionEnvioDto
{
    public int TipoId { get; set; }
    public int WorkerId { get; set; }
    public string DniConfirmacion { get; set; } = string.Empty;
    public string? Turno { get; set; }
    public bool? Planificada { get; set; }
    public string? EmpresaTipo { get; set; }
    public string? EmpresaNombre { get; set; }
    public string? Observaciones { get; set; }
    public string SelfieBase64 { get; set; } = string.Empty;
    public decimal? Lat { get; set; }
    public decimal? Lng { get; set; }
    public decimal? PrecisionMetros { get; set; }
    public DateTime? HoraDispositivo { get; set; }
    /// <summary>Armado sin conexión: idempotencia del reintento.</summary>
    public Guid? ClientId { get; set; }
    public bool OrigenOffline { get; set; }
    /// <summary>Formatos por trabajador: uno por equipo marcado ("equipo a usar hoy"). Formatos por equipo: exactamente uno, sin VarianteId.</summary>
    public List<InspEquipoEnvioDto> Equipos { get; set; } = [];
}

public class InspInspeccionCreadaDto
{
    public int Id { get; set; }
    public string? Codigo { get; set; }
    public string Resultado { get; set; } = "conforme";
    /// <summary>Códigos de los equipos que quedaron fuera de servicio con esta inspección.</summary>
    public List<string> EquiposBloqueados { get; set; } = [];
}

// ============================================================================
// LISTADO / DETALLE / FIRMAS
// ============================================================================

public class InspInspeccionFiltroDto
{
    public int? ProyectoId { get; set; }
    public int? TipoId { get; set; }
    public string? Estado { get; set; }
    public string? Resultado { get; set; }
    public DateOnly? Desde { get; set; }
    public DateOnly? Hasta { get; set; }
    public string? Buscar { get; set; }
}

public class InspInspeccionListaDto
{
    public int Id { get; set; }
    public string? Codigo { get; set; }
    public string TipoNombre { get; set; } = string.Empty;
    public string CodigoFormato { get; set; } = string.Empty;
    public int ProyectoId { get; set; }
    public string? ProyectoNombre { get; set; }
    public DateOnly Fecha { get; set; }
    public string? Turno { get; set; }
    public string OperadorNombre { get; set; } = string.Empty;
    public string? Equipos { get; set; }
    public string Estado { get; set; } = string.Empty;
    public string Resultado { get; set; } = string.Empty;
    public bool FirmadoCapataz { get; set; }
    public bool FirmadoResidente { get; set; }
    public bool FirmadoSsoma { get; set; }
}

public class InspRespuestaDto
{
    public int Id { get; set; }
    public int? InspeccionVarianteId { get; set; }
    public int Numero { get; set; }
    public string? Grupo { get; set; }
    public string? NivelRiesgo { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public bool EsCritico { get; set; }
    public bool Invertido { get; set; }
    public string Respuesta { get; set; } = string.Empty;
    public string? Observacion { get; set; }
    public bool? SeCorrigio { get; set; }
    public string? FotoUrl { get; set; }
    public List<InspMarcadorDto> Marcadores { get; set; } = [];
}

public class InspInspeccionVarianteDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? ImagenUrl { get; set; }
    public string CodigoEquipo { get; set; } = string.Empty;
    public int? EquipoId { get; set; }
}

public class InspInspeccionDetalleDto : InspInspeccionListaDto
{
    public InspTipoDto? Tipo { get; set; }
    public int? EquipoId { get; set; }
    public string? EquipoCodigo { get; set; }
    public bool? Planificada { get; set; }
    public string? EmpresaTipo { get; set; }
    public string? EmpresaNombre { get; set; }
    public string? Observaciones { get; set; }
    public DateTime HoraServidor { get; set; }
    public DateTime? HoraDispositivo { get; set; }
    public bool OrigenOffline { get; set; }
    public decimal? Lat { get; set; }
    public decimal? Lng { get; set; }
    public string? SelfieUrl { get; set; }
    public string? FirmaUrl { get; set; }
    public string? CapatazNombre { get; set; }
    public string? CapatazFirmaUrl { get; set; }
    public DateTime? CapatazHoraServidor { get; set; }
    public string? ResidenteNombre { get; set; }
    public string? ResidenteFirmaUrl { get; set; }
    public DateTime? ResidenteHoraServidor { get; set; }
    public string? SsomaNombre { get; set; }
    public string? SsomaFirmaUrl { get; set; }
    public DateTime? SsomaHoraServidor { get; set; }
    public bool RequiereCapataz { get; set; }
    public string? AnuladoMotivo { get; set; }
    public List<InspInspeccionVarianteDto> EquiposUsados { get; set; } = [];
    public List<InspRespuestaDto> Respuestas { get; set; } = [];
}

public class InspAnularDto
{
    public string Motivo { get; set; } = string.Empty;
}

/// <summary>Lo único que ve quien escanea el QR del PDF: datos mínimos para comprobar que el documento es auténtico (sin fotos ni firmas).</summary>
public class InspVerificacionDto
{
    public bool Valido { get; set; }
    public string? MotivoInvalido { get; set; }
    public string? Codigo { get; set; }
    public string? CodigoFormato { get; set; }
    public string? TipoNombre { get; set; }
    public string? ProyectoNombre { get; set; }
    public DateOnly? Fecha { get; set; }
    public string? Turno { get; set; }
    public string? Operador { get; set; }
    public string? Equipos { get; set; }
    public string? Estado { get; set; }
    public string? Resultado { get; set; }
    public List<InspVerificacionFirmaDto> Firmas { get; set; } = [];
}

public class InspVerificacionFirmaDto
{
    public string Rol { get; set; } = string.Empty;
    public string? Nombre { get; set; }
    public DateTime? Hora { get; set; }
    public bool Firmado { get; set; }
}

public class InspDashboardDto
{
    public int EquiposFueraServicio { get; set; }
    public int InspeccionesHoy { get; set; }
    public int PendientesFirma { get; set; }
    public int NoConformesSemana { get; set; }
    /// <summary>Equipos operativos de formatos "por equipo" (ej. minicargador) sin check list hoy.</summary>
    public int EquiposSinInspeccionHoy { get; set; }
}
