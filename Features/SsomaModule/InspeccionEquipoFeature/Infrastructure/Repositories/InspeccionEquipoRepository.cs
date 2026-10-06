using System.Security.Cryptography;
using System.Text;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.SsomaModule.InspeccionEquipoFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.InspeccionEquipoFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.SsomaModule.InspeccionEquipoFeature.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.SsomaModule.InspeccionEquipoFeature.Infrastructure.Repositories;

public class InspeccionEquipoRepository : IInspeccionEquipoRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public InspeccionEquipoRepository(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
    }

    // ═════════════════════════════════════════════════════════════════════
    // CATÁLOGO
    // ═════════════════════════════════════════════════════════════════════

    public async Task<List<SsInspEquipoTipo>> GetTipos(bool soloActivos)
    {
        using var ctx = _factory.CreateDbContext();
        var q = ctx.SsInspEquipoTipo.AsNoTracking()
            .Include(t => t.Variantes)
            .Include(t => t.Items).ThenInclude(i => i.Marcadores)
            .AsSplitQuery();
        if (soloActivos) q = q.Where(t => t.Activo);
        return await q.OrderBy(t => t.CodigoFormato).ToListAsync();
    }

    public async Task<SsInspEquipoTipo?> GetTipo(int id)
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.SsInspEquipoTipo.AsNoTracking()
            .Include(t => t.Variantes)
            .Include(t => t.Items).ThenInclude(i => i.Marcadores)
            .AsSplitQuery()
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task ActualizarTipo(int id, InspTipoGuardarDto dto)
    {
        using var ctx = _factory.CreateDbContext();
        var t = await ctx.SsInspEquipoTipo.FirstOrDefaultAsync(x => x.Id == id)
            ?? throw new AbrilException("Formato no encontrado.", 404);
        t.Nombre = dto.Nombre.Trim();
        t.Version = dto.Version?.Trim();
        t.Instrucciones = dto.Instrucciones;
        t.RequiereTurno = dto.RequiereTurno;
        t.RegistraCorreccion = dto.RegistraCorreccion;
        t.RequierePlanificada = dto.RequierePlanificada;
        t.RequiereEmpresaTipo = dto.RequiereEmpresaTipo;
        t.ReqFirmaCapataz = dto.ReqFirmaCapataz;
        t.ReqFirmaResidente = dto.ReqFirmaResidente;
        t.ReqFirmaSsoma = dto.ReqFirmaSsoma;
        t.Activo = dto.Activo;
        t.UpdatedAt = DateTime.UtcNow;
        await ctx.SaveChangesAsync();
    }

    public async Task<SsInspEquipoTipoVariante?> GetVariante(int id)
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.SsInspEquipoTipoVariante.AsNoTracking().FirstOrDefaultAsync(v => v.Id == id);
    }

    public async Task<int> CrearVariante(int tipoId, InspVarianteGuardarDto dto)
    {
        using var ctx = _factory.CreateDbContext();
        var tipo = await ctx.SsInspEquipoTipo.FirstOrDefaultAsync(t => t.Id == tipoId)
            ?? throw new AbrilException("Formato no encontrado.", 404);
        if (!tipo.PermiteVariantes)
            throw new AbrilException("Este formato no maneja equipos distintos (variantes).", 409);
        var nombre = dto.Nombre.Trim();
        if (await ctx.SsInspEquipoTipoVariante.AnyAsync(v => v.TipoId == tipoId && v.Nombre == nombre))
            throw new AbrilException("Ya existe un equipo con ese nombre en este formato.", 409);
        var v = new SsInspEquipoTipoVariante { TipoId = tipoId, Nombre = nombre, Orden = dto.Orden, Activo = dto.Activo };
        ctx.SsInspEquipoTipoVariante.Add(v);
        await ctx.SaveChangesAsync();
        return v.Id;
    }

    public async Task ActualizarVariante(int id, InspVarianteGuardarDto dto)
    {
        using var ctx = _factory.CreateDbContext();
        var v = await ctx.SsInspEquipoTipoVariante.FirstOrDefaultAsync(x => x.Id == id)
            ?? throw new AbrilException("Equipo no encontrado.", 404);
        var nombre = dto.Nombre.Trim();
        if (await ctx.SsInspEquipoTipoVariante.AnyAsync(x => x.TipoId == v.TipoId && x.Nombre == nombre && x.Id != id))
            throw new AbrilException("Ya existe un equipo con ese nombre en este formato.", 409);
        v.Nombre = nombre;
        v.Orden = dto.Orden;
        v.Activo = dto.Activo;
        await ctx.SaveChangesAsync();
    }

    public async Task SetImagenVariante(int id, string? url)
    {
        using var ctx = _factory.CreateDbContext();
        var v = await ctx.SsInspEquipoTipoVariante.FirstOrDefaultAsync(x => x.Id == id)
            ?? throw new AbrilException("Equipo no encontrado.", 404);
        v.ImagenUrl = url;
        await ctx.SaveChangesAsync();
    }

    public async Task<int> CrearItem(int tipoId, InspItemGuardarDto dto)
    {
        using var ctx = _factory.CreateDbContext();
        if (!await ctx.SsInspEquipoTipo.AnyAsync(t => t.Id == tipoId))
            throw new AbrilException("Formato no encontrado.", 404);
        if (dto.VarianteId.HasValue && !await ctx.SsInspEquipoTipoVariante.AnyAsync(v => v.Id == dto.VarianteId && v.TipoId == tipoId))
            throw new AbrilException("El equipo no pertenece a este formato.", 400);
        var item = new SsInspPlantillaItem
        {
            TipoId = tipoId,
            VarianteId = dto.VarianteId,
            Orden = dto.Orden,
            Grupo = dto.Grupo,
            NivelRiesgo = dto.NivelRiesgo,
            Descripcion = dto.Descripcion.Trim(),
            EsCritico = dto.EsCritico,
            Invertido = dto.Invertido,
            Activo = dto.Activo,
        };
        ctx.SsInspPlantillaItem.Add(item);
        await ctx.SaveChangesAsync();
        return item.Id;
    }

    public async Task<bool> ActualizarItem(int id, InspItemGuardarDto dto)
    {
        using var ctx = _factory.CreateDbContext();
        var item = await ctx.SsInspPlantillaItem.FirstOrDefaultAsync(i => i.Id == id);
        if (item is null) return false;
        item.Orden = dto.Orden;
        item.Grupo = dto.Grupo;
        item.NivelRiesgo = dto.NivelRiesgo;
        item.Descripcion = dto.Descripcion.Trim();
        item.EsCritico = dto.EsCritico;
        item.Invertido = dto.Invertido;
        item.Activo = dto.Activo;
        item.UpdatedAt = DateTime.UtcNow;
        await ctx.SaveChangesAsync();
        return true;
    }

    public async Task<bool> SetMarcadores(int itemId, List<InspMarcadorDto> marcadores)
    {
        using var ctx = _factory.CreateDbContext();
        if (!await ctx.SsInspPlantillaItem.AnyAsync(i => i.Id == itemId)) return false;
        await ctx.SsInspItemMarcador.Where(m => m.ItemId == itemId).ExecuteDeleteAsync();
        ctx.SsInspItemMarcador.AddRange(marcadores.Select(m => new SsInspItemMarcador
        {
            ItemId = itemId,
            XPct = Math.Clamp(m.X, 0, 100),
            YPct = Math.Clamp(m.Y, 0, 100),
            // La flecha se define completa o no se define.
            PxPct = m.Px.HasValue && m.Py.HasValue ? Math.Clamp(m.Px.Value, 0, 100) : null,
            PyPct = m.Px.HasValue && m.Py.HasValue ? Math.Clamp(m.Py.Value, 0, 100) : null,
        }));
        await ctx.SaveChangesAsync();
        return true;
    }

    // ═════════════════════════════════════════════════════════════════════
    // EQUIPOS
    // ═════════════════════════════════════════════════════════════════════

    private static IQueryable<InspEquipoDto> ProyectarEquipos(AppDbContext ctx, IQueryable<SsInspEquipo> q) =>
        q.Select(e => new InspEquipoDto
        {
            Id = e.Id,
            TipoId = e.TipoId,
            TipoNombre = e.Tipo!.Nombre,
            ProyectoId = e.ProyectoId,
            ProyectoNombre = e.Proyecto!.ProjectDescription,
            VarianteId = e.VarianteId,
            VarianteNombre = e.Variante != null ? e.Variante.Nombre : null,
            Codigo = e.Codigo,
            Descripcion = e.Descripcion,
            FotoUrl = e.FotoUrl,
            AsignadoWorkerId = e.AsignadoWorkerId,
            AsignadoNombre = e.AsignadoWorker != null && e.AsignadoWorker.Person != null ? e.AsignadoWorker.Person.FullName : null,
            EmpresaTipo = e.EmpresaTipo,
            EmpresaNombre = e.EmpresaNombre,
            Estado = e.Estado,
            BloqueadoMotivo = e.BloqueadoMotivo,
            BloqueadoEn = e.BloqueadoEn,
            QrToken = e.QrToken,
            Activo = e.Activo,
            UltimaInspeccion = ctx.SsInspInspeccion
                .Where(i => i.Estado != "Anulado" &&
                    (i.EquipoId == e.Id || ctx.SsInspInspeccionVariante.Any(iv => iv.InspeccionId == i.Id && iv.EquipoId == e.Id)))
                .OrderByDescending(i => i.Fecha)
                .Select(i => (DateOnly?)i.Fecha)
                .FirstOrDefault(),
        });

    public async Task<List<InspEquipoDto>> GetEquipos(InspEquipoFiltroDto f)
    {
        using var ctx = _factory.CreateDbContext();
        var q = ctx.SsInspEquipo.AsNoTracking().Where(e => e.Activo);
        if (f.ProyectoId.HasValue) q = q.Where(e => e.ProyectoId == f.ProyectoId);
        if (f.TipoId.HasValue) q = q.Where(e => e.TipoId == f.TipoId);
        if (!string.IsNullOrWhiteSpace(f.Estado)) q = q.Where(e => e.Estado == f.Estado);
        if (f.SinInspeccionHoy)
        {
            var hoy = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-5));
            q = q.Where(e => e.Estado == "operativo" && e.Tipo!.IdentificaPor == "equipo"
                && !ctx.SsInspInspeccion.Any(i => i.EquipoId == e.Id && i.Fecha == hoy && i.Estado != "Anulado"));
        }
        if (!string.IsNullOrWhiteSpace(f.Buscar))
        {
            var b = f.Buscar.Trim().ToUpper();
            q = q.Where(e => e.Codigo.ToUpper().Contains(b) || (e.Descripcion != null && e.Descripcion.ToUpper().Contains(b)));
        }
        return await ProyectarEquipos(ctx, q.OrderBy(e => e.Codigo)).ToListAsync();
    }

    public async Task<InspEquipoDto?> GetEquipoDto(int id)
    {
        using var ctx = _factory.CreateDbContext();
        return await ProyectarEquipos(ctx, ctx.SsInspEquipo.AsNoTracking().Where(e => e.Id == id)).FirstOrDefaultAsync();
    }

    public async Task<SsInspEquipo?> GetEquipoEntidad(int id)
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.SsInspEquipo.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id);
    }

    public async Task<SsInspEquipo?> GetEquipoPorQr(Guid token)
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.SsInspEquipo.AsNoTracking().FirstOrDefaultAsync(e => e.QrToken == token && e.Activo);
    }

    public async Task<List<InspEquipoDto>> BuscarCandidatos(int proyectoId, int tipoId, int? varianteId, string codigo)
    {
        using var ctx = _factory.CreateDbContext();
        var q = ctx.SsInspEquipo.AsNoTracking()
            .Where(e => e.Activo && e.TipoId == tipoId && e.VarianteId == varianteId && e.Codigo == codigo
                && (e.ProyectoId == proyectoId || e.EmpresaTipo == "casa"))
            .OrderByDescending(e => e.ProyectoId == proyectoId).ThenBy(e => e.Id);
        return await ProyectarEquipos(ctx, q).ToListAsync();
    }

    public async Task TrasladarEquipo(int equipoId, int nuevoProyectoId, int workerId, string detalle)
    {
        using var ctx = _factory.CreateDbContext();
        var e = await ctx.SsInspEquipo.FirstOrDefaultAsync(x => x.Id == equipoId)
            ?? throw new AbrilException("Equipo no encontrado.", 404);
        e.ProyectoId = nuevoProyectoId;
        e.UpdatedAt = DateTime.UtcNow;
        ctx.SsInspEquipoEvento.Add(new SsInspEquipoEvento { EquipoId = equipoId, Evento = "traslado", WorkerId = workerId, Detalle = detalle });
        await ctx.SaveChangesAsync();
    }

    public async Task<List<InspEquipoDto>> GetEquiposDeWorker(int proyectoId, int tipoId, int workerId)
    {
        using var ctx = _factory.CreateDbContext();
        var q = ctx.SsInspEquipo.AsNoTracking()
            .Where(e => e.Activo && e.TipoId == tipoId && e.AsignadoWorkerId == workerId)
            .OrderByDescending(e => e.ProyectoId == proyectoId).ThenBy(e => e.VarianteId).ThenBy(e => e.Codigo);
        return await ProyectarEquipos(ctx, q).ToListAsync();
    }

    public async Task<int> CrearEquipo(SsInspEquipo equipo)
    {
        using var ctx = _factory.CreateDbContext();
        ctx.SsInspEquipo.Add(equipo);
        await ctx.SaveChangesAsync();
        return equipo.Id;
    }

    public async Task ActualizarEquipo(int id, InspEquipoGuardarDto dto, string? fotoUrl)
    {
        using var ctx = _factory.CreateDbContext();
        var e = await ctx.SsInspEquipo.FirstOrDefaultAsync(x => x.Id == id)
            ?? throw new AbrilException("Equipo no encontrado.", 404);
        var codigo = dto.Codigo.Trim().ToUpperInvariant();
        e.Codigo = codigo;
        e.VarianteId = dto.VarianteId;
        e.Descripcion = dto.Descripcion;
        e.AsignadoWorkerId = dto.AsignadoWorkerId;
        e.EmpresaTipo = dto.EmpresaTipo;
        e.EmpresaNombre = dto.EmpresaNombre;
        if (fotoUrl != null) e.FotoUrl = fotoUrl;
        e.UpdatedAt = DateTime.UtcNow;
        await ctx.SaveChangesAsync();
    }

    public async Task CambiarEstadoEquipo(int equipoId, string estado, string? motivo, int? inspeccionId, SsInspEquipoEvento evento)
    {
        using var ctx = _factory.CreateDbContext();
        var e = await ctx.SsInspEquipo.FirstOrDefaultAsync(x => x.Id == equipoId)
            ?? throw new AbrilException("Equipo no encontrado.", 404);
        e.Estado = estado;
        if (estado == "fuera_servicio")
        {
            e.BloqueadoMotivo = motivo;
            e.BloqueadoEn = DateTime.UtcNow;
            e.BloqueadoInspeccionId = inspeccionId;
        }
        else if (estado == "operativo")
        {
            e.BloqueadoMotivo = null;
            e.BloqueadoEn = null;
            e.BloqueadoInspeccionId = null;
        }
        else if (estado == "baja")
        {
            e.Activo = false;
        }
        e.UpdatedAt = DateTime.UtcNow;
        evento.EquipoId = equipoId;
        ctx.SsInspEquipoEvento.Add(evento);
        await ctx.SaveChangesAsync();
    }

    public async Task<List<InspEquipoEventoDto>> GetEventosEquipo(int equipoId)
    {
        using var ctx = _factory.CreateDbContext();
        return await (from ev in ctx.SsInspEquipoEvento.AsNoTracking()
                      where ev.EquipoId == equipoId
                      join u in ctx.Person on ev.UserId equals u.UserId into pj
                      from p in pj.DefaultIfEmpty()
                      orderby ev.CreatedAt descending
                      select new InspEquipoEventoDto
                      {
                          Id = ev.Id,
                          Evento = ev.Evento,
                          InspeccionId = ev.InspeccionId,
                          Usuario = p != null ? p.FullName : null,
                          Detalle = ev.Detalle,
                          FotoUrl = ev.FotoUrl,
                          CreatedAt = ev.CreatedAt,
                      }).ToListAsync();
    }

    public async Task SetRegistradoEn(int equipoId, int inspeccionId)
    {
        using var ctx = _factory.CreateDbContext();
        await ctx.SsInspEquipo.Where(e => e.Id == equipoId && e.RegistradoEnInspeccionId == null)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.RegistradoEnInspeccionId, inspeccionId));
    }

    // ═════════════════════════════════════════════════════════════════════
    // INSPECCIONES
    // ═════════════════════════════════════════════════════════════════════

    public async Task<int?> GetInspeccionIdPorClientId(Guid clientId)
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.SsInspInspeccion.AsNoTracking().Where(i => i.ClientId == clientId).Select(i => (int?)i.Id).FirstOrDefaultAsync();
    }

    public async Task<bool> ExisteInspeccionDelDia(int operadorWorkerId, int tipoId, DateOnly fecha, string? turno)
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.SsInspInspeccion.AnyAsync(i => i.OperadorWorkerId == operadorWorkerId && i.TipoId == tipoId
            && i.Fecha == fecha && i.Turno == turno && i.Estado != "Anulado" && i.EquipoId == null);
    }

    /// <summary>"{ABREV}-INSP-0001": misma abreviatura y misma tabla de correlativos que ATS/PETAR (ss_documento_correlativo).</summary>
    private static async Task<string> SiguienteCodigo(AppDbContext ctx, int proyectoId)
    {
        const string tipo = "INSP";
        var abrev = await ctx.Project.Where(p => p.ProjectId == proyectoId)
            .Select(p => p.Abbreviation ?? p.Codigo).FirstOrDefaultAsync() ?? "PRY";
        var res = await ctx.Database.SqlQuery<int>(
            $"INSERT INTO ss_documento_correlativo (proyecto_id, tipo, ultimo) VALUES ({proyectoId}, {tipo}, 1) ON CONFLICT (proyecto_id, tipo) DO UPDATE SET ultimo = ss_documento_correlativo.ultimo + 1 RETURNING ultimo AS \"Value\"")
            .ToListAsync();
        return $"{abrev.Trim().ToUpperInvariant()}-{tipo}-{res[0]:D4}";
    }

    public async Task<SsInspInspeccion> CrearInspeccion(SsInspInspeccion insp)
    {
        using var ctx = _factory.CreateDbContext();
        await using var tx = await ctx.Database.BeginTransactionAsync();
        insp.Codigo = await SiguienteCodigo(ctx, insp.ProyectoId);
        ctx.SsInspInspeccion.Add(insp);
        await ctx.SaveChangesAsync();
        ctx.SsInspAuditLog.Add(new SsInspAuditLog
        {
            InspeccionId = insp.Id,
            Evento = "Creado",
            Detalle = $"operador_worker_id={insp.OperadorWorkerId}; resultado={insp.Resultado}",
            IpOrigen = insp.IpOrigen,
            HashAnterior = null,
            Hash = ComputeHash(null, "Creado", insp.Id, insp.OperadorWorkerId, insp.FirmaHash),
        });
        await ctx.SaveChangesAsync();
        await tx.CommitAsync();
        return insp;
    }

    private static string ComputeHash(string? anterior, string evento, int inspeccionId, int? actorId, string? extra = null)
    {
        var raw = $"{anterior}|{evento}|{inspeccionId}|{actorId}|{extra}|{DateTime.UtcNow.Ticks}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
    }

    private static async Task AuditarAsync(AppDbContext ctx, int inspeccionId, string evento, int? actorId, string? detalle, string? extra = null)
    {
        var anterior = await ctx.SsInspAuditLog.Where(a => a.InspeccionId == inspeccionId)
            .OrderByDescending(a => a.Id).Select(a => a.Hash).FirstOrDefaultAsync();
        ctx.SsInspAuditLog.Add(new SsInspAuditLog
        {
            InspeccionId = inspeccionId,
            Evento = evento,
            UserId = actorId,
            Detalle = detalle,
            HashAnterior = anterior,
            Hash = ComputeHash(anterior, evento, inspeccionId, actorId, extra),
        });
    }

    public async Task<(List<InspInspeccionListaDto> Items, int Total)> GetInspecciones(InspInspeccionFiltroDto f, int page, int pageSize)
    {
        using var ctx = _factory.CreateDbContext();
        var q = ctx.SsInspInspeccion.AsNoTracking().AsQueryable();
        if (f.ProyectoId.HasValue) q = q.Where(i => i.ProyectoId == f.ProyectoId);
        if (f.TipoId.HasValue) q = q.Where(i => i.TipoId == f.TipoId);
        if (!string.IsNullOrWhiteSpace(f.Estado)) q = q.Where(i => i.Estado == f.Estado);
        if (!string.IsNullOrWhiteSpace(f.Resultado)) q = q.Where(i => i.Resultado == f.Resultado);
        if (f.Desde.HasValue) q = q.Where(i => i.Fecha >= f.Desde);
        if (f.Hasta.HasValue) q = q.Where(i => i.Fecha <= f.Hasta);
        if (!string.IsNullOrWhiteSpace(f.Buscar))
        {
            var b = f.Buscar.Trim().ToUpper();
            q = q.Where(i => (i.Codigo != null && i.Codigo.ToUpper().Contains(b))
                || (i.OperadorWorker!.Person != null && i.OperadorWorker.Person.FullName.ToUpper().Contains(b))
                || ctx.SsInspInspeccionVariante.Any(v => v.InspeccionId == i.Id && v.CodigoEquipo.ToUpper().Contains(b))
                || (i.Equipo != null && i.Equipo.Codigo.ToUpper().Contains(b)));
        }

        var total = await q.CountAsync();
        var items = await q.OrderByDescending(i => i.Fecha).ThenByDescending(i => i.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(i => new InspInspeccionListaDto
            {
                Id = i.Id,
                Codigo = i.Codigo,
                TipoNombre = i.Tipo!.Nombre,
                CodigoFormato = i.Tipo.CodigoFormato,
                ProyectoId = i.ProyectoId,
                ProyectoNombre = i.Proyecto!.ProjectDescription,
                Fecha = i.Fecha,
                Turno = i.Turno,
                OperadorNombre = i.OperadorWorker!.Person != null ? i.OperadorWorker.Person.FullName : string.Empty,
                Equipos = i.Equipo != null ? i.Equipo.Codigo : null,
                Estado = i.Estado,
                Resultado = i.Resultado,
                FirmadoCapataz = i.CapatazFirmaUrl != null,
                FirmadoResidente = i.ResidenteFirmaUrl != null,
                FirmadoSsoma = i.SsomaFirmaUrl != null,
            }).ToListAsync();

        var ids = items.Select(x => x.Id).ToList();
        var usados = await ctx.SsInspInspeccionVariante.AsNoTracking()
            .Where(v => ids.Contains(v.InspeccionId)).OrderBy(v => v.Orden).ToListAsync();
        foreach (var it in items.Where(x => x.Equipos == null))
            it.Equipos = string.Join(", ", usados.Where(v => v.InspeccionId == it.Id).Select(v => $"{v.Nombre} {v.CodigoEquipo}"));
        return (items, total);
    }

    public async Task<InspInspeccionDetalleDto?> GetInspeccionDetalle(int id)
    {
        using var ctx = _factory.CreateDbContext();
        var i = await ctx.SsInspInspeccion.AsNoTracking()
            .Include(x => x.Tipo).Include(x => x.Proyecto).Include(x => x.Equipo)
            .Include(x => x.OperadorWorker).ThenInclude(w => w!.Person)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (i is null) return null;

        var variantes = await ctx.SsInspInspeccionVariante.AsNoTracking()
            .Where(v => v.InspeccionId == id).OrderBy(v => v.Orden).ToListAsync();
        var respuestas = await ctx.SsInspRespuesta.AsNoTracking()
            .Where(r => r.InspeccionId == id).OrderBy(r => r.Orden).ToListAsync();
        var itemIds = respuestas.Where(r => r.ItemId.HasValue).Select(r => r.ItemId!.Value).ToList();
        var marcadores = await ctx.SsInspItemMarcador.AsNoTracking()
            .Where(m => itemIds.Contains(m.ItemId)).ToListAsync();

        return new InspInspeccionDetalleDto
        {
            Id = i.Id,
            Codigo = i.Codigo,
            TipoNombre = i.Tipo!.Nombre,
            CodigoFormato = i.Tipo.CodigoFormato,
            ProyectoId = i.ProyectoId,
            ProyectoNombre = i.Proyecto?.ProjectDescription,
            Fecha = i.Fecha,
            Turno = i.Turno,
            OperadorNombre = i.OperadorWorker?.Person?.FullName ?? string.Empty,
            Equipos = i.Equipo?.Codigo ?? string.Join(", ", variantes.Select(v => $"{v.Nombre} {v.CodigoEquipo}")),
            Estado = i.Estado,
            Resultado = i.Resultado,
            FirmadoCapataz = i.CapatazFirmaUrl != null,
            FirmadoResidente = i.ResidenteFirmaUrl != null,
            FirmadoSsoma = i.SsomaFirmaUrl != null,
            EquipoId = i.EquipoId,
            EquipoCodigo = i.Equipo?.Codigo,
            Planificada = i.Planificada,
            EmpresaTipo = i.EmpresaTipo,
            EmpresaNombre = i.EmpresaNombre,
            Observaciones = i.Observaciones,
            HoraServidor = i.HoraServidor,
            HoraDispositivo = i.HoraDispositivo,
            OrigenOffline = i.OrigenOffline,
            Lat = i.Lat,
            Lng = i.Lng,
            SelfieUrl = i.SelfieUrl,
            FirmaUrl = i.FirmaUrl,
            CapatazNombre = i.CapatazNombre,
            CapatazFirmaUrl = i.CapatazFirmaUrl,
            CapatazHoraServidor = i.CapatazHoraServidor,
            ResidenteNombre = i.ResidenteNombre,
            ResidenteFirmaUrl = i.ResidenteFirmaUrl,
            ResidenteHoraServidor = i.ResidenteHoraServidor,
            SsomaNombre = i.SsomaNombre,
            SsomaFirmaUrl = i.SsomaFirmaUrl,
            SsomaHoraServidor = i.SsomaHoraServidor,
            AnuladoMotivo = i.AnuladoMotivo,
            EquiposUsados = variantes.Select(v => new InspInspeccionVarianteDto
            {
                Id = v.Id, Nombre = v.Nombre, ImagenUrl = v.ImagenUrl, CodigoEquipo = v.CodigoEquipo, EquipoId = v.EquipoId,
            }).ToList(),
            Respuestas = respuestas.Select(r => new InspRespuestaDto
            {
                Id = r.Id,
                InspeccionVarianteId = r.InspeccionVarianteId,
                Numero = r.Numero,
                Grupo = r.Grupo,
                NivelRiesgo = r.NivelRiesgo,
                Descripcion = r.Descripcion,
                EsCritico = r.EsCritico,
                Invertido = r.Invertido,
                Respuesta = r.Respuesta,
                Observacion = r.Observacion,
                SeCorrigio = r.SeCorrigio,
                FotoUrl = r.FotoUrl,
                Marcadores = r.ItemId.HasValue
                    ? marcadores.Where(m => m.ItemId == r.ItemId).Select(m => new InspMarcadorDto { X = m.XPct, Y = m.YPct, Px = m.PxPct, Py = m.PyPct }).ToList()
                    : [],
            }).ToList(),
        };
    }

    public async Task<SsInspInspeccion?> GetInspeccionEntidad(int id)
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.SsInspInspeccion.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id);
    }

    public async Task FirmarNivel(int id, string rol, int workerId, string nombre, string? cargo, string firmaUrl, string firmaHash, DateTime hora, bool completo)
    {
        using var ctx = _factory.CreateDbContext();
        var i = await ctx.SsInspInspeccion.FirstOrDefaultAsync(x => x.Id == id)
            ?? throw new AbrilException("Check list no encontrado.", 404);
        switch (rol)
        {
            case "Capataz":
                i.CapatazWorkerId = workerId; i.CapatazNombre = nombre; i.CapatazCargo = cargo;
                i.CapatazFirmaUrl = firmaUrl; i.CapatazFirmaHash = firmaHash; i.CapatazHoraServidor = hora;
                break;
            case "Residente":
                i.ResidenteWorkerId = workerId; i.ResidenteNombre = nombre; i.ResidenteCargo = cargo;
                i.ResidenteFirmaUrl = firmaUrl; i.ResidenteFirmaHash = firmaHash; i.ResidenteHoraServidor = hora;
                break;
            default:
                i.SsomaWorkerId = workerId; i.SsomaNombre = nombre; i.SsomaCargo = cargo;
                i.SsomaFirmaUrl = firmaUrl; i.SsomaFirmaHash = firmaHash; i.SsomaHoraServidor = hora;
                break;
        }
        if (completo) i.Estado = "Completo";
        i.UpdatedAt = DateTime.UtcNow;
        await AuditarAsync(ctx, id, $"Firmado_{rol}", workerId, null, firmaHash);
        await ctx.SaveChangesAsync();
    }

    public async Task Anular(int id, string motivo, int userId)
    {
        using var ctx = _factory.CreateDbContext();
        var i = await ctx.SsInspInspeccion.FirstOrDefaultAsync(x => x.Id == id)
            ?? throw new AbrilException("Check list no encontrado.", 404);
        i.Estado = "Anulado";
        i.AnuladoMotivo = motivo;
        i.AnuladoPorUserId = userId;
        i.AnuladoEn = DateTime.UtcNow;
        i.UpdatedAt = DateTime.UtcNow;
        await AuditarAsync(ctx, id, "Anulado", userId, motivo);
        await ctx.SaveChangesAsync();
    }

    public async Task<InspDashboardDto> GetDashboard(int? proyectoId)
    {
        using var ctx = _factory.CreateDbContext();
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-5));
        var semana = hoy.AddDays(-6);
        var inspecciones = ctx.SsInspInspeccion.AsNoTracking().Where(i => i.Estado != "Anulado");
        var equipos = ctx.SsInspEquipo.AsNoTracking().Where(e => e.Activo);
        if (proyectoId.HasValue)
        {
            inspecciones = inspecciones.Where(i => i.ProyectoId == proyectoId);
            equipos = equipos.Where(e => e.ProyectoId == proyectoId);
        }
        return new InspDashboardDto
        {
            EquiposFueraServicio = await equipos.CountAsync(e => e.Estado == "fuera_servicio"),
            InspeccionesHoy = await inspecciones.CountAsync(i => i.Fecha == hoy),
            PendientesFirma = await inspecciones.CountAsync(i => i.Estado == "Pendiente_Firmas"),
            NoConformesSemana = await inspecciones.CountAsync(i => i.Fecha >= semana && i.Resultado == "no_conforme_critico"),
            EquiposSinInspeccionHoy = await equipos.CountAsync(e => e.Estado == "operativo" && e.Tipo!.IdentificaPor == "equipo"
                && !ctx.SsInspInspeccion.Any(i => i.EquipoId == e.Id && i.Fecha == hoy && i.Estado != "Anulado")),
        };
    }

    public async Task<List<SsInspInspeccion>> GetInspeccionesDeEquipo(int equipoId, DateOnly desde, DateOnly hasta)
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.SsInspInspeccion.AsNoTracking()
            .Include(i => i.OperadorWorker).ThenInclude(w => w!.Person)
            .Where(i => i.EquipoId == equipoId && i.Fecha >= desde && i.Fecha <= hasta && i.Estado != "Anulado")
            .OrderBy(i => i.Fecha).ThenBy(i => i.Id)
            .ToListAsync();
    }

    public async Task<string?> GetProyectoNombre(int proyectoId)
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.Project.AsNoTracking().Where(p => p.ProjectId == proyectoId).Select(p => p.ProjectDescription).FirstOrDefaultAsync();
    }
}
