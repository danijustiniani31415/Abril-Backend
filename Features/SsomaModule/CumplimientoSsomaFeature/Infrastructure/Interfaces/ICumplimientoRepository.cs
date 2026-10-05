using Abril_Backend.Features.SsomaModule.CumplimientoSsomaFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.CumplimientoSsomaFeature.Infrastructure.Models;

namespace Abril_Backend.Features.SsomaModule.CumplimientoSsomaFeature.Infrastructure.Interfaces
{
    public interface ICumplimientoRepository
    {
        // --- Catálogo ---
        Task<List<CumplimientoActividadDto>> GetActividadesAsync();
        Task<SsCumplimientoActividad> CreateActividadAsync(CumplimientoActividadUpsertDto dto);
        Task UpdateActividadAsync(int actividadId, CumplimientoActividadUpsertDto dto);
        Task DeleteActividadAsync(int actividadId);

        // --- Cumplimiento por proyecto ---
        Task<CumplimientoResumenDto> GetResumenProyectoAsync(int proyectoId, int usuarioId, string? rol);
        Task<CumplimientoItemDto> MarcarAsync(int proyectoId, int actividadId, CumplimientoMarcarDto dto, int userId);
        Task<List<CumplimientoUsuarioDto>> GetUsuariosProyectoAsync(int proyectoId);

        // --- "Mi" checklist: resuelve rol + proyecto actual del usuario logueado ---
        Task<int?> GetProyectoActualDeUsuarioAsync(int userId);
        Task<string?> GetRolDeUsuarioAsync(int userId);
        Task<CumplimientoMiResumenDto> GetMiResumenAsync(int userId);

        // --- Histórico / indicadores ---
        Task<CumplimientoHistoricoDto> GetHistoricoAsync(int proyectoId, int usuarioId, string frecuencia, DateOnly desde, DateOnly hasta);
    }
}
