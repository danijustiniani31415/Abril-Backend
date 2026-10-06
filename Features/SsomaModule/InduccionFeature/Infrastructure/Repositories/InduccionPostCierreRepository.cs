using System.Security.Cryptography;
using System.Text;
using Abril_Backend.Features.SsomaModule.InduccionFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.SsomaModule.InduccionFeature.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.SsomaModule.InduccionFeature.Infrastructure.Repositories;

public class InduccionPostCierreRepository : IInduccionPostCierreRepository
{
    private static readonly TimeSpan EsperaEntreIntentos = TimeSpan.FromMinutes(2);
    /// <summary>Si el sello tarda más que esto en salir, el respaldo no lo espera (se hace igual).</summary>
    private static readonly TimeSpan EsperaMaximaSello = TimeSpan.FromMinutes(30);

    private readonly IDbContextFactory<AppDbContext> _factory;

    public InduccionPostCierreRepository(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    // ── Sello de tiempo ─────────────────────────────────────────────────────

    public async Task<SsIndSello?> GetSello(int sesionId)
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.SsIndSello.AsNoTracking().FirstOrDefaultAsync(s => s.SesionId == sesionId);
    }

    public async Task<SsIndSello?> PrepararSello(int sesionId, string proveedor)
    {
        using var ctx = _factory.CreateDbContext();
        var existente = await ctx.SsIndSello.AsNoTracking().FirstOrDefaultAsync(s => s.SesionId == sesionId);
        if (existente != null) return existente;

        var sha = await ctx.SsIndSesion.AsNoTracking().Where(s => s.Id == sesionId).Select(s => s.ActaPdfSha256).FirstOrDefaultAsync();
        if (string.IsNullOrEmpty(sha)) return null;

        var ultimo = await ctx.SsIndAuditLog.AsNoTracking().Where(l => l.SesionId == sesionId)
            .OrderByDescending(l => l.Id).Select(l => l.Hash).FirstOrDefaultAsync() ?? string.Empty;
        var componentes = $"{sha}|{ultimo}";

        var nuevo = new SsIndSello
        {
            SesionId = sesionId,
            Componentes = componentes,
            Digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(componentes))),
            Proveedor = proveedor,
        };
        ctx.SsIndSello.Add(nuevo);
        try
        {
            await ctx.SaveChangesAsync();
            return nuevo;
        }
        catch (DbUpdateException)
        {
            // Otro proceso lo creó a la vez (UNIQUE sesion_id): se usa ese, para no cambiar nunca el digest.
            using var otro = _factory.CreateDbContext();
            return await otro.SsIndSello.AsNoTracking().FirstOrDefaultAsync(s => s.SesionId == sesionId);
        }
    }

    public async Task RegistrarIntentoSello(int selloId, bool ok, byte[]? token, DateTime? selladoEn, string? serial,
        string? emisor, string? error, int maxIntentos)
    {
        using var ctx = _factory.CreateDbContext();
        var s = await ctx.SsIndSello.FirstOrDefaultAsync(x => x.Id == selloId);
        if (s == null || s.Estado == "Sellado") return;

        s.Intentos += 1;
        s.UltimoIntento = DateTime.UtcNow;
        if (ok)
        {
            s.Estado = "Sellado";
            s.Token = token;
            s.SelladoEn = selladoEn;
            s.SerialTsa = serial;
            s.EmisorTsa = emisor;
            s.UltimoError = null;
        }
        else
        {
            s.UltimoError = error is { Length: > 500 } ? error[..500] : error;
            if (s.Intentos >= maxIntentos) s.Estado = "Error";
        }
        await ctx.SaveChangesAsync();
    }

    public async Task ReiniciarSello(int sesionId)
    {
        using var ctx = _factory.CreateDbContext();
        var s = await ctx.SsIndSello.FirstOrDefaultAsync(x => x.SesionId == sesionId);
        if (s == null || s.Estado == "Sellado") return;
        s.Estado = "Pendiente";
        s.Intentos = 0;
        s.UltimoIntento = null;
        await ctx.SaveChangesAsync();
    }

    public async Task<List<int>> SesionesPendientesSello(int max)
    {
        using var ctx = _factory.CreateDbContext();
        var limite = DateTime.UtcNow - EsperaEntreIntentos;
        return await ctx.SsIndSesion.AsNoTracking()
            .Where(s => s.CerradaEn != null && s.ActaPdfSha256 != null
                && !ctx.SsIndSello.Any(x => x.SesionId == s.Id
                    && (x.Estado == "Sellado" || x.Estado == "Error" || (x.UltimoIntento != null && x.UltimoIntento > limite))))
            .OrderBy(s => s.Id).Select(s => s.Id).Take(max).ToListAsync();
    }

    // ── Respaldo ────────────────────────────────────────────────────────────

    public async Task<SsIndRespaldo?> GetRespaldo(int sesionId)
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.SsIndRespaldo.AsNoTracking().FirstOrDefaultAsync(r => r.SesionId == sesionId);
    }

    public async Task<SsIndRespaldo> GetOCrearRespaldo(int sesionId)
    {
        using var ctx = _factory.CreateDbContext();
        var r = await ctx.SsIndRespaldo.AsNoTracking().FirstOrDefaultAsync(x => x.SesionId == sesionId);
        if (r != null) return r;

        r = new SsIndRespaldo { SesionId = sesionId };
        ctx.SsIndRespaldo.Add(r);
        try
        {
            await ctx.SaveChangesAsync();
            return r;
        }
        catch (DbUpdateException)
        {
            using var otro = _factory.CreateDbContext();
            return await otro.SsIndRespaldo.AsNoTracking().FirstAsync(x => x.SesionId == sesionId);
        }
    }

    public async Task RegistrarResultadoRespaldo(int respaldoId, bool ok, int archivos, long bytes, string? prefijo, string? error, int maxIntentos)
    {
        using var ctx = _factory.CreateDbContext();
        var r = await ctx.SsIndRespaldo.FirstOrDefaultAsync(x => x.Id == respaldoId);
        if (r == null || r.Estado == "Respaldado") return;

        r.Intentos += 1;
        r.UltimoIntento = DateTime.UtcNow;
        if (ok)
        {
            r.Estado = "Respaldado";
            r.Archivos = archivos;
            r.Bytes = bytes;
            r.Prefijo = prefijo;
            r.RespaldadoEn = DateTime.UtcNow;
            r.UltimoError = null;
        }
        else
        {
            r.UltimoError = error is { Length: > 500 } ? error[..500] : error;
            if (r.Intentos >= maxIntentos) r.Estado = "Error";
        }
        await ctx.SaveChangesAsync();
    }

    public async Task ReiniciarRespaldo(int sesionId)
    {
        using var ctx = _factory.CreateDbContext();
        var r = await ctx.SsIndRespaldo.FirstOrDefaultAsync(x => x.SesionId == sesionId);
        if (r == null || r.Estado == "Respaldado") return;
        r.Estado = "Pendiente";
        r.Intentos = 0;
        r.UltimoIntento = null;
        await ctx.SaveChangesAsync();
    }

    public async Task<List<int>> SesionesPendientesRespaldo(int max)
    {
        using var ctx = _factory.CreateDbContext();
        var ahora = DateTime.UtcNow;
        var limiteIntento = ahora - EsperaEntreIntentos;
        var limiteSello = ahora - EsperaMaximaSello;
        return await ctx.SsIndSesion.AsNoTracking()
            .Where(s => s.CerradaEn != null
                // Se espera al sello (para que el manifiesto lo incluya), pero no para siempre.
                && (s.CerradaEn < limiteSello || ctx.SsIndSello.Any(x => x.SesionId == s.Id && (x.Estado == "Sellado" || x.Estado == "Error")))
                && !ctx.SsIndRespaldo.Any(r => r.SesionId == s.Id
                    && (r.Estado == "Respaldado" || r.Estado == "Error" || (r.UltimoIntento != null && r.UltimoIntento > limiteIntento))))
            .OrderBy(s => s.Id).Select(s => s.Id).Take(max).ToListAsync();
    }
}
