using Abril_Backend.Features.SsomaModule.SenaleticaFeature.Application.Dtos;
using Microsoft.AspNetCore.Http;

namespace Abril_Backend.Features.SsomaModule.SenaleticaFeature.Application.Interfaces
{
    public interface ISenaleticaService
    {
        Task<List<SenalDto>> GetAllAsync();
        Task<SenalDto> CreateAsync(SenalUpsertDto dto, int? userId);
        Task UpdateAsync(int id, SenalUpsertDto dto, int? userId);
        Task SetActivoAsync(int id, bool activo, int? userId);
        Task<string> SubirImagenAsync(int id, IFormFile archivo, int? userId);
        Task QuitarImagenAsync(int id, int? userId);
        Task DeleteAsync(int id);
    }
}
