using Abril_Backend.Features.SsomaModule.CumplimientoSsomaFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.CumplimientoSsomaFeature.Application.Interfaces;
using Abril_Backend.Features.SsomaModule.CumplimientoSsomaFeature.Infrastructure.Interfaces;

namespace Abril_Backend.Features.SsomaModule.CumplimientoSsomaFeature.Application.Services
{
    public class CumplimientoService : ICumplimientoService
    {
        private readonly ICumplimientoRepository _repo;

        public CumplimientoService(ICumplimientoRepository repo)
        {
            _repo = repo;
        }

        public Task<List<CumplimientoActividadDto>> GetActividadesAsync()
            => _repo.GetActividadesAsync();

        public async Task<CumplimientoActividadDto> CreateActividadAsync(CumplimientoActividadUpsertDto dto)
        {
            var entity = await _repo.CreateActividadAsync(dto);
            return new CumplimientoActividadDto
            {
                Id = entity.Id,
                Nombre = entity.Nombre,
                Descripcion = entity.Descripcion,
                Categoria = entity.Categoria,
                RolResponsable = entity.RolResponsable,
                Frecuencia = entity.Frecuencia,
                Orden = entity.Orden,
                Activo = entity.Activo
            };
        }

        public Task UpdateActividadAsync(int actividadId, CumplimientoActividadUpsertDto dto)
            => _repo.UpdateActividadAsync(actividadId, dto);

        public Task DeleteActividadAsync(int actividadId)
            => _repo.DeleteActividadAsync(actividadId);

        // El rol se deduce del puesto del usuario consultado, no se recibe del cliente.
        public async Task<CumplimientoResumenDto> GetResumenProyectoAsync(int proyectoId, int usuarioId)
        {
            var rol = await _repo.GetRolDeUsuarioAsync(usuarioId);
            return await _repo.GetResumenProyectoAsync(proyectoId, usuarioId, rol);
        }

        public Task<CumplimientoItemDto> MarcarAsync(int proyectoId, int actividadId, CumplimientoMarcarDto dto, int userId)
            => _repo.MarcarAsync(proyectoId, actividadId, dto, userId);

        public Task<List<CumplimientoUsuarioDto>> GetUsuariosProyectoAsync(int proyectoId)
            => _repo.GetUsuariosProyectoAsync(proyectoId);

        public Task<CumplimientoMiResumenDto> GetMiResumenAsync(int userId)
            => _repo.GetMiResumenAsync(userId);

        public Task<CumplimientoHistoricoDto> GetHistoricoAsync(int proyectoId, int usuarioId, string frecuencia, DateOnly desde, DateOnly hasta)
            => _repo.GetHistoricoAsync(proyectoId, usuarioId, frecuencia, desde, hasta);
    }
}
