using Abril_Backend.Features.SsomaModule.InspeccionEquipoFeature.Application.Dtos;

namespace Abril_Backend.Features.SsomaModule.InspeccionEquipoFeature.Application.Interfaces;

public interface IInspeccionEquipoService
{
    // ── Catálogo (admin) ─────────────────────────────────────────────────
    Task<List<InspTipoDto>> GetTipos();
    Task<InspTipoDto> GetTipo(int id);
    Task ActualizarTipo(int id, InspTipoGuardarDto dto);
    Task<int> CrearVariante(int tipoId, InspVarianteGuardarDto dto);
    Task ActualizarVariante(int id, InspVarianteGuardarDto dto);
    Task<InspImagenResponseDto> SubirImagenVariante(int varianteId, Stream stream, string fileName);
    Task<int> CrearItem(int tipoId, InspItemGuardarDto dto);
    Task ActualizarItem(int id, InspItemGuardarDto dto);
    Task SetMarcadores(int itemId, InspMarcadoresGuardarDto dto);

    // ── Equipos (admin) ──────────────────────────────────────────────────
    Task<List<InspEquipoDto>> GetEquipos(InspEquipoFiltroDto filtro);
    Task<InspEquipoDto> GetEquipo(int id);
    Task<InspEquipoDto> CrearEquipo(InspEquipoGuardarDto dto, int userId);
    Task ActualizarEquipo(int id, InspEquipoGuardarDto dto);
    Task LiberarEquipo(int id, InspEquipoAccionDto dto, int userId);
    Task DarBajaEquipo(int id, InspEquipoAccionDto dto, int userId);
    Task<List<InspEquipoEventoDto>> GetEventosEquipo(int id);
    /// <summary>QR fijo del proyecto (mismo token que el QR de obra del ATS: se imprime una sola vez).</summary>
    Task<string> GetOrCrearQrProyecto(int proyectoId);

    // ── Llenado público (sin login) ──────────────────────────────────────
    Task<InspProyectoPublicoDto> GetProyectoPublico(Guid tokenProyecto);
    Task<List<InspEquipoDto>> GetMisEquipos(Guid tokenProyecto, InspMisEquiposRequestDto body);
    /// <summary>Candidatos (con foto) para un código escrito: el proyecto actual, los propios de la persona y los de la casa en otros proyectos.</summary>
    Task<List<InspEquipoDto>> BuscarEquipos(Guid tokenProyecto, InspBuscarEquipoRequestDto body);
    Task<InspEquipoPublicoDto> GetEquipoPublico(Guid qrToken);
    Task<InspInspeccionCreadaDto> RegistrarPorProyecto(Guid tokenProyecto, InspInspeccionEnvioDto body, string? ip, string? userAgent);
    Task<InspInspeccionCreadaDto> RegistrarPorEquipo(Guid qrToken, InspInspeccionEnvioDto body, string? ip, string? userAgent);

    // ── Inspecciones (logueado) ──────────────────────────────────────────
    Task<(List<InspInspeccionListaDto> Items, int Total)> GetInspecciones(InspInspeccionFiltroDto filtro, int page, int pageSize);
    Task<InspInspeccionDetalleDto> GetDetalle(int id);
    /// <summary>rol: "Capataz" | "Residente" | "Ssoma".</summary>
    Task Firmar(int id, string rol, int callerUserId, bool esAdmin);
    Task Anular(int id, string motivo, int userId);
    Task<InspDashboardDto> GetDashboard(int? proyectoId);
    Task<byte[]> GenerarPdf(int id);
    /// <summary>Registro de seguimiento e inspección diaria de UN equipo (Lunes–Sábado de la semana de esa fecha) con las firmas de cada día.</summary>
    Task<byte[]> GenerarRegistroSemanalPdf(int equipoId, DateOnly fecha);
    /// <summary>Verificación pública (sin login): el hash del QR impreso en el PDF es el único candado.</summary>
    Task<InspVerificacionDto> VerificarPublico(int id, string? hash);
}
