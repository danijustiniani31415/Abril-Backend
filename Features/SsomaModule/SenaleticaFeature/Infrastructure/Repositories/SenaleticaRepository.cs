using Abril_Backend.Features.SsomaModule.SenaleticaFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.SenaleticaFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.SsomaModule.SenaleticaFeature.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.SsomaModule.SenaleticaFeature.Infrastructure.Repositories
{
    public class SenaleticaRepository : ISenaleticaRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public SenaleticaRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<List<SenalDto>> GetAllAsync()
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.SsSenal.AsNoTracking()
                .OrderBy(s => s.Tipo).ThenBy(s => s.Orden).ThenBy(s => s.Texto)
                .Select(s => new SenalDto
                {
                    Id = s.Id,
                    Tipo = s.Tipo,
                    Texto = s.Texto,
                    Detalle = s.Detalle,
                    Icono = s.Icono,
                    ImagenUrl = s.ImagenUrl,
                    EsBasica = s.EsBasica,
                    Activo = s.Activo,
                })
                .ToListAsync();
        }

        public async Task<SsSenal> CreateAsync(SenalUpsertDto dto, int? userId)
        {
            using var ctx = _factory.CreateDbContext();
            var entity = new SsSenal
            {
                Tipo = dto.Tipo,
                Texto = dto.Texto.Trim(),
                Detalle = string.IsNullOrWhiteSpace(dto.Detalle) ? null : dto.Detalle.Trim(),
                Icono = string.IsNullOrWhiteSpace(dto.Icono) ? null : dto.Icono.Trim(),
                EsBasica = false,
                Activo = true,
                Orden = 1000,
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedById = userId,
            };
            ctx.SsSenal.Add(entity);
            await ctx.SaveChangesAsync();
            return entity;
        }

        public async Task UpdateAsync(int id, SenalUpsertDto dto, int? userId)
        {
            using var ctx = _factory.CreateDbContext();
            var entity = await ctx.SsSenal.FirstOrDefaultAsync(s => s.Id == id)
                ?? throw new KeyNotFoundException("Señal no encontrada.");
            entity.Tipo = dto.Tipo;
            entity.Texto = dto.Texto.Trim();
            entity.Detalle = string.IsNullOrWhiteSpace(dto.Detalle) ? null : dto.Detalle.Trim();
            entity.Icono = string.IsNullOrWhiteSpace(dto.Icono) ? null : dto.Icono.Trim();
            entity.UpdatedAt = DateTimeOffset.UtcNow;
            entity.UpdatedById = userId;
            await ctx.SaveChangesAsync();
        }

        public async Task SetActivoAsync(int id, bool activo, int? userId)
        {
            using var ctx = _factory.CreateDbContext();
            var entity = await ctx.SsSenal.FirstOrDefaultAsync(s => s.Id == id)
                ?? throw new KeyNotFoundException("Señal no encontrada.");
            entity.Activo = activo;
            entity.UpdatedAt = DateTimeOffset.UtcNow;
            entity.UpdatedById = userId;
            await ctx.SaveChangesAsync();
        }

        public async Task SetImagenAsync(int id, string? url, int? userId)
        {
            using var ctx = _factory.CreateDbContext();
            var entity = await ctx.SsSenal.FirstOrDefaultAsync(s => s.Id == id)
                ?? throw new KeyNotFoundException("Señal no encontrada.");
            entity.ImagenUrl = url;
            entity.UpdatedAt = DateTimeOffset.UtcNow;
            entity.UpdatedById = userId;
            await ctx.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            using var ctx = _factory.CreateDbContext();
            var entity = await ctx.SsSenal.FirstOrDefaultAsync(s => s.Id == id)
                ?? throw new KeyNotFoundException("Señal no encontrada.");
            if (entity.EsBasica)
                throw new InvalidOperationException("Las señales básicas no se pueden eliminar, solo desactivar.");
            ctx.SsSenal.Remove(entity);
            await ctx.SaveChangesAsync();
        }
    }
}
