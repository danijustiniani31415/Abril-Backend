using Abril_Backend.Features.SsomaModule.SenaleticaFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.SenaleticaFeature.Application.Interfaces;
using Abril_Backend.Features.SsomaModule.SenaleticaFeature.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Http;

namespace Abril_Backend.Features.SsomaModule.SenaleticaFeature.Application.Services
{
    public class SenaleticaService : ISenaleticaService
    {
        private static readonly string[] TiposValidos =
            { "prohibicion", "advertencia", "obligacion", "emergencia", "incendio" };

        private static readonly string[] ExtensionesImagen = { ".png", ".jpg", ".jpeg", ".svg", ".webp" };

        private readonly ISenaleticaRepository _repository;
        private readonly IFileStorageService _fileStorageService;
        private readonly IStorageContainerResolver _containerResolver;

        public SenaleticaService(
            ISenaleticaRepository repository,
            IFileStorageService fileStorageService,
            IStorageContainerResolver containerResolver)
        {
            _repository = repository;
            _fileStorageService = fileStorageService;
            _containerResolver = containerResolver;
        }

        public Task<List<SenalDto>> GetAllAsync() => _repository.GetAllAsync();

        public async Task<SenalDto> CreateAsync(SenalUpsertDto dto, int? userId)
        {
            Validar(dto);
            var entity = await _repository.CreateAsync(dto, userId);
            return new SenalDto
            {
                Id = entity.Id,
                Tipo = entity.Tipo,
                Texto = entity.Texto,
                Detalle = entity.Detalle,
                Icono = entity.Icono,
                ImagenUrl = entity.ImagenUrl,
                EsBasica = entity.EsBasica,
                Activo = entity.Activo,
            };
        }

        public Task UpdateAsync(int id, SenalUpsertDto dto, int? userId)
        {
            Validar(dto);
            return _repository.UpdateAsync(id, dto, userId);
        }

        public Task SetActivoAsync(int id, bool activo, int? userId) => _repository.SetActivoAsync(id, activo, userId);

        public async Task<string> SubirImagenAsync(int id, IFormFile archivo, int? userId)
        {
            if (archivo == null || archivo.Length == 0)
                throw new InvalidOperationException("El archivo está vacío.");
            var ext = Path.GetExtension(archivo.FileName).ToLowerInvariant();
            if (!ExtensionesImagen.Contains(ext))
                throw new InvalidOperationException("La imagen debe ser PNG, JPG, SVG o WEBP.");

            var container = _containerResolver.GetSenaleticaImagenesContainerName();
            var fileName = $"{Guid.NewGuid()}{ext}";

            await using var stream = archivo.OpenReadStream();
            var uploadedUrls = await _fileStorageService.UploadFilesAsync(
                new[] { (Stream: (Stream)stream, FileName: fileName) }, container);

            var url = uploadedUrls.First();
            await _repository.SetImagenAsync(id, url, userId);
            return url;
        }

        public Task QuitarImagenAsync(int id, int? userId) => _repository.SetImagenAsync(id, null, userId);

        public Task DeleteAsync(int id) => _repository.DeleteAsync(id);

        private static void Validar(SenalUpsertDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Texto))
                throw new InvalidOperationException("El texto de la señal es obligatorio.");
            if (!TiposValidos.Contains(dto.Tipo))
                throw new InvalidOperationException("Tipo de señal no válido.");
        }
    }
}
