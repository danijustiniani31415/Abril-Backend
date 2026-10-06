using Abril_Backend.Features.SsomaModule.SenaleticaFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.SenaleticaFeature.Infrastructure.Models;

namespace Abril_Backend.Features.SsomaModule.SenaleticaFeature.Infrastructure.Interfaces
{
    public interface ISenaleticaRepository
    {
        Task<List<SenalDto>> GetAllAsync();
        Task<SsSenal> CreateAsync(SenalUpsertDto dto, int? userId);
        Task UpdateAsync(int id, SenalUpsertDto dto, int? userId);
        Task SetActivoAsync(int id, bool activo, int? userId);
        Task SetImagenAsync(int id, string? url, int? userId);
        Task DeleteAsync(int id);
    }
}
