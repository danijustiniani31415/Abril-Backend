using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.SsomaModule.InduccionFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.InduccionFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.SsomaModule.InduccionFeature.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.SsomaModule.InduccionFeature.Infrastructure.Repositories;

public class InduccionVigenciaRepository : IInduccionVigenciaRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public InduccionVigenciaRepository(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    private static DateOnly Hoy() => DateOnly.FromDateTime(DateTime.Today);

    // ── Bloqueo por proyecto ────────────────────────────────────────────────

    public async Task<bool> GateActivo(int proyectoId)
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.SsIndGateProyecto.AsNoTracking().AnyAsync(g => g.ProyectoId == proyectoId && g.Activo);
    }

    public async Task SetGate(int proyectoId, bool activo, int? userId)
    {
        using var ctx = _factory.CreateDbContext();
        var g = await ctx.SsIndGateProyecto.FirstOrDefaultAsync(x => x.ProyectoId == proyectoId);
        if (g == null)
        {
            g = new SsIndGateProyecto { ProyectoId = proyectoId };
            ctx.SsIndGateProyecto.Add(g);
        }
        g.Activo = activo;
        g.ActualizadoEn = DateTime.UtcNow;
        g.ActualizadoPorUserId = userId;
        await ctx.SaveChangesAsync();
    }

    // ── Datos para calcular la vigencia ─────────────────────────────────────

    public async Task<DatosVigencia> CargarDatos(int proyectoId, IReadOnlyCollection<int>? workerIds, bool conRoster)
    {
        using var ctx = _factory.CreateDbContext();
        var datos = new DatosVigencia();
        var hoy = Hoy();

        if (conRoster)
        {
            var ids = await ctx.WorkerVinculacion.Where(v => v.ProyectoId == proyectoId && v.FechaFin == null)
                .Select(v => v.WorkerId).ToListAsync();
            var workers = await ctx.Worker.AsNoTracking()
                .Where(w => ids.Contains(w.Id) && w.WorkersEstadoId == Abril_Backend.Shared.Constants.WorkersEstadoIds.Activo)
                .Include(w => w.Person).ToListAsync();
            datos.Roster = workers
                .Select(w => new VigRoster(w.Id, ApellidosNombres(w.Person), w.Person?.DocumentIdentityCode, w.Categoria))
                .OrderBy(w => w.Nombre).ToList();
        }

        // Firmas de sesiones CERRADAS (una sesión anulada no cuenta) de este proyecto.
        var regs = await (from a in ctx.SsIndAsistencia.AsNoTracking()
                          join s in ctx.SsIndSesion.AsNoTracking() on a.SesionId equals s.Id
                          where s.ProyectoId == proyectoId && s.Estado == "Cerrada" && a.Estado == "Firmado"
                                && (workerIds == null || workerIds.Contains(a.WorkerId))
                          select new { a.WorkerId, AsistId = a.Id, SesionId = s.Id, s.Codigo, s.Tipo, s.Fecha, a.Aprobado })
                         .ToListAsync();

        var asistIds = regs.Select(r => r.AsistId).ToList();
        var docs = await (from d in ctx.SsIndAsistenciaDocumento.AsNoTracking()
                          join v in ctx.SsIndDocumentoVersion.AsNoTracking() on d.DocumentoVersionId equals v.Id
                          where asistIds.Contains(d.AsistenciaId)
                          select new { d.AsistenciaId, v.DocumentoId, v.Version }).ToListAsync();
        var docsPorAsist = docs.GroupBy(d => d.AsistenciaId).ToDictionary(g => g.Key, g => g.Select(x => new VigDocRecibido(x.DocumentoId, x.Version)).ToList());

        datos.Registros = regs.Select(r => new VigRegistro(
            r.WorkerId, r.AsistId, r.SesionId, r.Codigo, r.Tipo, r.Fecha, r.Aprobado,
            docsPorAsist.GetValueOrDefault(r.AsistId) ?? [])).ToList();

        datos.Catalogo = (await ctx.SsIndDocumento.AsNoTracking().Where(d => d.Activo).Include(d => d.Versiones).ToListAsync())
            .Select(d => new VigDocumento(d.Id, d.Codigo, d.Nombre, d.Obligatorio,
                d.Versiones.Select(v => new VigVersion(v.Version, v.EsCambioMaterial, v.PublicadoEn, v.Vigente)).ToList()))
            .ToList();

        datos.Exenciones = await ctx.SsIndExencion.AsNoTracking()
            .Where(e => e.ProyectoId == proyectoId && e.RevocadaEn == null && (e.Hasta == null || e.Hasta >= hoy)
                        && (workerIds == null || workerIds.Contains(e.WorkerId)))
            .ToListAsync();

        return datos;
    }

    // ── Exenciones ──────────────────────────────────────────────────────────

    public async Task<List<SsIndExencion>> ListarExenciones(int proyectoId)
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.SsIndExencion.AsNoTracking().Where(e => e.ProyectoId == proyectoId)
            .OrderByDescending(e => e.Id).ToListAsync();
    }

    public async Task<SsIndExencion> CrearExencion(IndExencionCrearDto dto, int userId)
    {
        using var ctx = _factory.CreateDbContext();
        var hoy = Hoy();
        // Una sola exención activa por trabajador y proyecto: la nueva reemplaza a la anterior.
        var vigentes = await ctx.SsIndExencion
            .Where(e => e.WorkerId == dto.WorkerId && e.ProyectoId == dto.ProyectoId && e.RevocadaEn == null && (e.Hasta == null || e.Hasta >= hoy))
            .ToListAsync();
        foreach (var v in vigentes) { v.RevocadaEn = DateTime.UtcNow; v.RevocadaPorUserId = userId; }

        var nueva = new SsIndExencion
        {
            WorkerId = dto.WorkerId,
            ProyectoId = dto.ProyectoId,
            Motivo = dto.Motivo.Trim(),
            Hasta = dto.Hasta,
            CreadoPorUserId = userId,
        };
        ctx.SsIndExencion.Add(nueva);
        await ctx.SaveChangesAsync();
        return nueva;
    }

    public async Task RevocarExencion(int id, int userId)
    {
        using var ctx = _factory.CreateDbContext();
        var e = await ctx.SsIndExencion.FirstOrDefaultAsync(x => x.Id == id) ?? throw new AbrilException("Exención no encontrada.", 404);
        if (e.RevocadaEn != null) return;
        e.RevocadaEn = DateTime.UtcNow;
        e.RevocadaPorUserId = userId;
        await ctx.SaveChangesAsync();
    }

    // ── Alertas ─────────────────────────────────────────────────────────────

    public async Task<List<int>> ProyectosConInducciones()
    {
        using var ctx = _factory.CreateDbContext();
        var desde = Hoy().AddMonths(-18);
        return await ctx.SsIndSesion.AsNoTracking().Where(s => s.Estado == "Cerrada" && s.Fecha >= desde)
            .Select(s => s.ProyectoId).Distinct().ToListAsync();
    }

    public async Task<string?> GetProyectoNombre(int proyectoId)
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.Project.AsNoTracking().Where(p => p.ProjectId == proyectoId).Select(p => p.ProjectDescription).FirstOrDefaultAsync();
    }

    public async Task<bool> RegistrarAlerta(int proyectoId, string tipo, string periodo, string? detalle)
    {
        using var ctx = _factory.CreateDbContext();
        if (await ctx.SsIndAlerta.AnyAsync(a => a.ProyectoId == proyectoId && a.Tipo == tipo && a.Periodo == periodo)) return false;
        ctx.SsIndAlerta.Add(new SsIndAlerta { ProyectoId = proyectoId, Tipo = tipo, Periodo = periodo, Detalle = detalle });
        try { await ctx.SaveChangesAsync(); return true; }
        catch (DbUpdateException) { return false; } // otro proceso la registró a la vez (UNIQUE)
    }

    // ── Expediente ──────────────────────────────────────────────────────────

    public async Task<IndExpedienteDto> GetExpediente(int workerId, int vigenciaMeses)
    {
        using var ctx = _factory.CreateDbContext();
        var w = await ctx.Worker.AsNoTracking().Include(x => x.Person).Include(x => x.PuestoCatalogo)
            .FirstOrDefaultAsync(x => x.Id == workerId) ?? throw new AbrilException("Trabajador no encontrado.", 404);

        var proyectos = await ctx.Project.AsNoTracking().Select(p => new { p.ProjectId, p.ProjectDescription }).ToDictionaryAsync(p => p.ProjectId, p => p.ProjectDescription);
        string? NombreProyecto(int id) => proyectos.GetValueOrDefault(id);

        var firmas = await (from a in ctx.SsIndAsistencia.AsNoTracking()
                            join s in ctx.SsIndSesion.AsNoTracking() on a.SesionId equals s.Id
                            where a.WorkerId == workerId && a.Estado == "Firmado"
                            orderby s.Fecha descending, s.Id descending
                            select new { a.Id, SesionId = s.Id, s.Codigo, s.Tipo, s.ProyectoId, s.Fecha, SesionEstado = s.Estado, a.Nota, a.Aprobado, a.Intentos, a.HoraServidorFirma, s.ActaPdfUrl })
                           .ToListAsync();
        var asistIds = firmas.Select(f => f.Id).ToList();
        var sesionIds = firmas.Select(f => f.SesionId).Distinct().ToList();

        var docs = await (from d in ctx.SsIndAsistenciaDocumento.AsNoTracking()
                          join v in ctx.SsIndDocumentoVersion.AsNoTracking() on d.DocumentoVersionId equals v.Id
                          join doc in ctx.SsIndDocumento.AsNoTracking() on v.DocumentoId equals doc.Id
                          where asistIds.Contains(d.AsistenciaId)
                          select new { d.AsistenciaId, doc.Codigo, v.Version }).ToListAsync();
        var sellos = await ctx.SsIndSello.AsNoTracking().Where(s => sesionIds.Contains(s.SesionId) && s.Estado == "Sellado")
            .ToDictionaryAsync(s => s.SesionId, s => s.SelladoEn);

        var expediente = new IndExpedienteDto
        {
            WorkerId = workerId,
            Nombre = ApellidosNombres(w.Person),
            DocumentoIdentidad = w.Person?.DocumentIdentityCode,
            Categoria = w.Categoria,
            Puesto = w.PuestoCatalogo?.Nombre,
            Inducciones = firmas.Select(f => new IndExpedienteInduccionDto
            {
                SesionId = f.SesionId,
                Codigo = f.Codigo,
                Tipo = f.Tipo,
                ProyectoId = f.ProyectoId,
                ProyectoNombre = NombreProyecto(f.ProyectoId),
                Fecha = f.Fecha,
                SesionEstado = f.SesionEstado,
                Nota = f.Nota,
                Aprobado = f.Aprobado,
                Intentos = f.Intentos,
                HoraServidorFirma = f.HoraServidorFirma,
                ActaPdfUrl = f.ActaPdfUrl,
                SelladoEn = sellos.GetValueOrDefault(f.SesionId),
                VigenteHasta = f.Tipo == "Actualizacion" ? null : f.Fecha.AddMonths(vigenciaMeses),
                Documentos = docs.Where(d => d.AsistenciaId == f.Id).Select(d => $"{d.Codigo} v{d.Version}").OrderBy(x => x).ToList(),
            }).ToList(),
        };

        var ats = await ctx.SsAts.AsNoTracking().Where(a => a.WorkerId == workerId && a.Estado == "Firmado")
            .OrderByDescending(a => a.Fecha).ThenByDescending(a => a.Id).Take(100)
            .Select(a => new { a.Id, a.Codigo, a.Fecha, a.ProyectoId, a.Estado }).ToListAsync();
        expediente.Ats = ats.Select(a => new IndExpedienteAtsDto
        {
            Id = a.Id, Codigo = a.Codigo, Fecha = a.Fecha, ProyectoId = a.ProyectoId, ProyectoNombre = NombreProyecto(a.ProyectoId), Estado = a.Estado,
        }).ToList();

        var exenciones = await ctx.SsIndExencion.AsNoTracking().Where(e => e.WorkerId == workerId).OrderByDescending(e => e.Id).ToListAsync();
        expediente.Exenciones = exenciones.Select(e => new IndExpedienteExencionDto
        {
            Id = e.Id, ProyectoNombre = NombreProyecto(e.ProyectoId), Motivo = e.Motivo, Hasta = e.Hasta, CreadoEn = e.CreadoEn, RevocadaEn = e.RevocadaEn,
        }).ToList();

        return expediente;
    }

    /// <summary>"Apellidos Nombres" — nunca al revés.</summary>
    private static string ApellidosNombres(Person? p)
    {
        if (p == null) return string.Empty;
        var partes = $"{p.FirstLastName} {p.SecondLastName} {p.FirstName}".Trim();
        return string.IsNullOrWhiteSpace(partes) ? (p.FullName ?? string.Empty) : System.Text.RegularExpressions.Regex.Replace(partes, @"\s+", " ");
    }
}
