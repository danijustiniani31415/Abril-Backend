using Abril_Backend.Features.SsomaModule.CumplimientoSsomaFeature.Application.Dtos;

namespace Abril_Backend.Features.SsomaModule.CumplimientoSsomaFeature.Application.Interfaces
{
    public interface ICumplimientoService
    {
        Task<List<CumplimientoActividadDto>> GetActividadesAsync();
        Task<CumplimientoActividadDto> CreateActividadAsync(CumplimientoActividadUpsertDto dto);
        Task UpdateActividadAsync(int actividadId, CumplimientoActividadUpsertDto dto);
        Task DeleteActividadAsync(int actividadId);

        Task<CumplimientoResumenDto> GetResumenProyectoAsync(int proyectoId, int usuarioId);
        Task<CumplimientoItemDto> MarcarAsync(int proyectoId, int actividadId, CumplimientoMarcarDto dto, int userId);
        Task<List<CumplimientoUsuarioDto>> GetUsuariosProyectoAsync(int proyectoId);

        Task<CumplimientoMiResumenDto> GetMiResumenAsync(int userId);
        Task<CumplimientoHistoricoDto> GetHistoricoAsync(int proyectoId, int usuarioId, string frecuencia, DateOnly desde, DateOnly hasta);
    }
}
