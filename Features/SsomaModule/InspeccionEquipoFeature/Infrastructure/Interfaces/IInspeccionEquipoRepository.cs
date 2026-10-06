using Abril_Backend.Features.SsomaModule.InspeccionEquipoFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.InspeccionEquipoFeature.Infrastructure.Models;

namespace Abril_Backend.Features.SsomaModule.InspeccionEquipoFeature.Infrastructure.Interfaces;

public interface IInspeccionEquipoRepository
{
    // ── Catálogo ─────────────────────────────────────────────────────────
    /// <summary>Tipos con variantes, ítems y marcadores cargados (sin ordenar ni numerar: eso lo arma el servicio).</summary>
    Task<List<SsInspEquipoTipo>> GetTipos(bool soloActivos);
    Task<SsInspEquipoTipo?> GetTipo(int id);
    Task ActualizarTipo(int id, InspTipoGuardarDto dto);
    Task<SsInspEquipoTipoVariante?> GetVariante(int id);
    Task<int> CrearVariante(int tipoId, InspVarianteGuardarDto dto);
    Task ActualizarVariante(int id, InspVarianteGuardarDto dto);
    Task SetImagenVariante(int id, string? url);
    Task<int> CrearItem(int tipoId, InspItemGuardarDto dto);
    Task<bool> ActualizarItem(int id, InspItemGuardarDto dto);
    Task<bool> SetMarcadores(int itemId, List<InspMarcadorDto> marcadores);

    // ── Equipos ──────────────────────────────────────────────────────────
    Task<List<InspEquipoDto>> GetEquipos(InspEquipoFiltroDto filtro);
    Task<InspEquipoDto?> GetEquipoDto(int id);
    Task<SsInspEquipo?> GetEquipoEntidad(int id);
    Task<SsInspEquipo?> GetEquipoPorQr(Guid token);
    /// <summary>Candidatos por código: los del proyecto actual y los de la casa en otros proyectos (los de contratistas no se cruzan entre proyectos). Los del proyecto primero.</summary>
    Task<List<InspEquipoDto>> BuscarCandidatos(int proyectoId, int tipoId, int? varianteId, string codigo);
    /// <summary>Mueve el equipo al proyecto indicado y deja el evento "traslado" en su historial.</summary>
    Task TrasladarEquipo(int equipoId, int nuevoProyectoId, int workerId, string detalle);
    Task<List<InspEquipoDto>> GetEquiposDeWorker(int proyectoId, int tipoId, int workerId);
    Task<int> CrearEquipo(SsInspEquipo equipo);
    Task ActualizarEquipo(int id, InspEquipoGuardarDto dto, string? fotoUrl);
    Task CambiarEstadoEquipo(int equipoId, string estado, string? motivo, int? inspeccionId, SsInspEquipoEvento evento);
    Task<List<InspEquipoEventoDto>> GetEventosEquipo(int equipoId);
    Task SetRegistradoEn(int equipoId, int inspeccionId);

    // ── Inspecciones ─────────────────────────────────────────────────────
    Task<int?> GetInspeccionIdPorClientId(Guid clientId);
    Task<bool> ExisteInspeccionDelDia(int operadorWorkerId, int tipoId, DateOnly fecha, string? turno);
    /// <summary>Genera el código correlativo "{ABREV}-INSP-0001", guarda la inspección con sus equipos y respuestas y el log de auditoría.</summary>
    Task<SsInspInspeccion> CrearInspeccion(SsInspInspeccion insp);
    Task<(List<InspInspeccionListaDto> Items, int Total)> GetInspecciones(InspInspeccionFiltroDto filtro, int page, int pageSize);
    Task<InspInspeccionDetalleDto?> GetInspeccionDetalle(int id);
    Task<SsInspInspeccion?> GetInspeccionEntidad(int id);
    Task FirmarNivel(int id, string rol, int workerId, string nombre, string? cargo, string firmaUrl, string firmaHash, DateTime hora, bool completo);
    Task Anular(int id, string motivo, int userId);
    Task<InspDashboardDto> GetDashboard(int? proyectoId);
    /// <summary>Inspecciones vigentes de un equipo entre dos fechas (registro semanal), con el nombre del operador.</summary>
    Task<List<SsInspInspeccion>> GetInspeccionesDeEquipo(int equipoId, DateOnly desde, DateOnly hasta);
    Task<string?> GetProyectoNombre(int proyectoId);
}
