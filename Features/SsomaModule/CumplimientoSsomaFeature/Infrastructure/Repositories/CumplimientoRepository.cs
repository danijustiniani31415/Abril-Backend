using Abril_Backend.Features.SsomaModule.CumplimientoSsomaFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.CumplimientoSsomaFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.SsomaModule.CumplimientoSsomaFeature.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Shared.Constants;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace Abril_Backend.Features.SsomaModule.CumplimientoSsomaFeature.Infrastructure.Repositories
{
    public class CumplimientoRepository : ICumplimientoRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public CumplimientoRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        // Hora de Lima (UTC-5), consistente con el resto del sistema (notificaciones, etc.)
        private static DateOnly HoyLima() => DateOnly.FromDateTime(DateTimeOffset.UtcNow.AddHours(-5).Date);

        // Periodo vigente según la frecuencia de la actividad: el día, el lunes de
        // la semana en curso, o el día 1 del mes en curso.
        public static DateOnly PeriodoActual(string frecuencia)
        {
            var hoy = HoyLima();
            return frecuencia switch
            {
                "semanal" => hoy.AddDays(-((int)hoy.DayOfWeek == 0 ? 6 : (int)hoy.DayOfWeek - 1)),
                "mensual" => new DateOnly(hoy.Year, hoy.Month, 1),
                "anual" => new DateOnly(hoy.Year, 1, 1),
                _ => hoy
            };
        }

        // ─────────────────────────────────────────────────────────────────
        // CATÁLOGO
        // ─────────────────────────────────────────────────────────────────

        public async Task<List<CumplimientoActividadDto>> GetActividadesAsync()
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.SsCumplimientoActividad
                .OrderBy(a => a.Frecuencia)
                .ThenBy(a => a.Categoria)
                .ThenBy(a => a.Orden)
                .Select(a => new CumplimientoActividadDto
                {
                    Id             = a.Id,
                    Nombre         = a.Nombre,
                    Descripcion    = a.Descripcion,
                    Categoria      = a.Categoria,
                    RolResponsable = a.RolResponsable,
                    Frecuencia     = a.Frecuencia,
                    Orden          = a.Orden,
                    Activo         = a.Activo
                })
                .ToListAsync();
        }

        public async Task<SsCumplimientoActividad> CreateActividadAsync(CumplimientoActividadUpsertDto dto)
        {
            using var ctx = _factory.CreateDbContext();
            var now = DateTimeOffset.UtcNow;
            var entity = new SsCumplimientoActividad
            {
                Nombre         = dto.Nombre,
                Descripcion    = dto.Descripcion,
                Categoria      = dto.Categoria,
                RolResponsable = dto.RolResponsable,
                Frecuencia     = dto.Frecuencia,
                Orden          = dto.Orden,
                Activo         = true,
                CreatedAt      = now,
                UpdatedAt      = now
            };
            ctx.SsCumplimientoActividad.Add(entity);
            await ctx.SaveChangesAsync();
            return entity;
        }

        public async Task UpdateActividadAsync(int actividadId, CumplimientoActividadUpsertDto dto)
        {
            using var ctx = _factory.CreateDbContext();
            var entity = await ctx.SsCumplimientoActividad.FindAsync(actividadId)
                ?? throw new KeyNotFoundException($"Actividad {actividadId} no encontrada.");

            entity.Nombre         = dto.Nombre;
            entity.Descripcion    = dto.Descripcion;
            entity.Categoria      = dto.Categoria;
            entity.RolResponsable = dto.RolResponsable;
            entity.Frecuencia     = dto.Frecuencia;
            entity.Orden          = dto.Orden;
            entity.UpdatedAt      = DateTimeOffset.UtcNow;
            await ctx.SaveChangesAsync();
        }

        // No borra si ya tiene registros de cumplimiento (evita huecos silenciosos en el
        // histórico de algún proyecto) — se desactiva en su lugar, igual que ya se puede
        // hacer desde el form de editar (Activo=false), y el registro histórico queda intacto.
        public async Task DeleteActividadAsync(int actividadId)
        {
            using var ctx = _factory.CreateDbContext();
            var entity = await ctx.SsCumplimientoActividad.FindAsync(actividadId)
                ?? throw new KeyNotFoundException($"Actividad {actividadId} no encontrada.");

            var tieneRegistros = await ctx.SsCumplimientoRegistro.AnyAsync(r => r.ActividadId == actividadId);
            if (tieneRegistros)
            {
                entity.Activo = false;
                entity.UpdatedAt = DateTimeOffset.UtcNow;
            }
            else
            {
                ctx.SsCumplimientoActividad.Remove(entity);
            }
            await ctx.SaveChangesAsync();
        }

        // ─────────────────────────────────────────────────────────────────
        // CUMPLIMIENTO POR PROYECTO
        // ─────────────────────────────────────────────────────────────────

        // Checklist de UN usuario en un proyecto: cada prevencionista/coordinador lleva el suyo,
        // los registros de otros usuarios del mismo proyecto no se mezclan.
        public async Task<CumplimientoResumenDto> GetResumenProyectoAsync(int proyectoId, int usuarioId, string? rol)
        {
            using var ctx = _factory.CreateDbContext();

            var actividades = await ctx.SsCumplimientoActividad
                .Where(a => a.Activo && (rol == null || a.RolResponsable == rol || a.RolResponsable == "ambos"))
                .OrderBy(a => a.Frecuencia)
                .ThenBy(a => a.Orden)
                .ToListAsync();

            var registros = await ctx.SsCumplimientoRegistro
                .Where(r => r.ProyectoId == proyectoId && r.UsuarioId == usuarioId)
                .ToListAsync();

            var cumplidoPorIds = registros
                .Where(r => r.CumplidoPorId.HasValue)
                .Select(r => r.CumplidoPorId!.Value)
                .Distinct()
                .ToList();

            var usuarios = await ctx.User
                .Where(u => cumplidoPorIds.Contains(u.UserId))
                .Select(u => new { u.UserId, u.Person.FullName })
                .ToDictionaryAsync(u => u.UserId, u => u.FullName);

            var items = actividades.Select(a =>
            {
                var periodo = PeriodoActual(a.Frecuencia);
                var registro = registros.FirstOrDefault(r => r.ActividadId == a.Id && r.Periodo == periodo);

                return new CumplimientoItemDto
                {
                    ActividadId       = a.Id,
                    Nombre            = a.Nombre,
                    Descripcion       = a.Descripcion,
                    Categoria         = a.Categoria,
                    RolResponsable    = a.RolResponsable,
                    Frecuencia        = a.Frecuencia,
                    Periodo           = periodo,
                    Estado            = registro?.Estado ?? "pendiente",
                    MotivoNoAplica    = registro?.MotivoNoAplica,
                    FechaCumplimiento = registro?.FechaCumplimiento,
                    CumplidoPor       = registro?.CumplidoPorId.HasValue == true && usuarios.TryGetValue(registro.CumplidoPorId!.Value, out var nombre) ? nombre : null,
                    Observacion       = registro?.Observacion
                };
            }).ToList();

            return new CumplimientoResumenDto { ProyectoId = proyectoId, Actividades = items };
        }

        public async Task<CumplimientoItemDto> MarcarAsync(int proyectoId, int actividadId, CumplimientoMarcarDto dto, int userId)
        {
            using var ctx = _factory.CreateDbContext();

            var actividad = await ctx.SsCumplimientoActividad.FindAsync(actividadId)
                ?? throw new KeyNotFoundException($"Actividad {actividadId} no encontrada.");

            var periodo = PeriodoActual(actividad.Frecuencia);
            var now = DateTimeOffset.UtcNow;
            var estado = dto.Estado is "cumplido" or "no_aplica" or "pendiente" ? dto.Estado : "pendiente";

            var registro = await ctx.SsCumplimientoRegistro
                .FirstOrDefaultAsync(r => r.ActividadId == actividadId && r.ProyectoId == proyectoId && r.Periodo == periodo && r.UsuarioId == userId);

            if (registro == null)
            {
                registro = new SsCumplimientoRegistro
                {
                    ActividadId = actividadId,
                    ProyectoId  = proyectoId,
                    UsuarioId   = userId,
                    Periodo     = periodo,
                    CreatedAt   = now
                };
                ctx.SsCumplimientoRegistro.Add(registro);
            }

            registro.Estado            = estado;
            registro.Cumplido          = estado == "cumplido";
            registro.MotivoNoAplica    = estado == "no_aplica" ? dto.MotivoNoAplica : null;
            registro.FechaCumplimiento = estado == "cumplido" ? now : null;
            registro.CumplidoPorId     = estado is "cumplido" or "no_aplica" ? userId : null;
            registro.Observacion       = dto.Observacion;
            registro.UpdatedAt         = now;

            await ctx.SaveChangesAsync();

            string? cumplidoPorNombre = null;
            if (registro.CumplidoPorId.HasValue)
            {
                cumplidoPorNombre = await ctx.User
                    .Where(u => u.UserId == registro.CumplidoPorId.Value)
                    .Select(u => u.Person.FullName)
                    .FirstOrDefaultAsync();
            }

            return new CumplimientoItemDto
            {
                ActividadId       = actividad.Id,
                Nombre            = actividad.Nombre,
                Descripcion       = actividad.Descripcion,
                Categoria         = actividad.Categoria,
                RolResponsable    = actividad.RolResponsable,
                Frecuencia        = actividad.Frecuencia,
                Periodo           = periodo,
                Estado            = registro.Estado,
                MotivoNoAplica    = registro.MotivoNoAplica,
                FechaCumplimiento = registro.FechaCumplimiento,
                CumplidoPor       = cumplidoPorNombre,
                Observacion       = registro.Observacion
            };
        }

        // ─────────────────────────────────────────────────────────────────
        // "MI" CHECKLIST — resuelve rol y proyecto actual del usuario logueado
        // ─────────────────────────────────────────────────────────────────

        // Mismo criterio de resolución que ChecklistRepository.GetProyectoActualDeUsuarioAsync:
        // asignación activa en ss_hab_worker_proyecto, si no hay ninguna se cae a la
        // última vinculación activa.
        public async Task<int?> GetProyectoActualDeUsuarioAsync(int userId)
        {
            using var ctx = _factory.CreateDbContext();

            var workerId = await ctx.Person
                .Where(p => p.UserId == userId)
                .Join(ctx.Worker, p => p.PersonId, w => w.PersonId, (p, w) => (int?)w.Id)
                .FirstOrDefaultAsync();
            if (workerId == null) return null;

            var proyectoAsignado = await ctx.WorkerProyecto
                .Where(wp => wp.WorkerId == workerId && wp.FechaFin == null)
                .OrderByDescending(wp => wp.FechaInicio).ThenByDescending(wp => wp.Id)
                .Select(wp => (int?)wp.ProyectoId)
                .FirstOrDefaultAsync();
            if (proyectoAsignado != null) return proyectoAsignado;

            return await ctx.WorkerVinculacion
                .Where(v => v.WorkerId == workerId && v.FechaFin == null)
                .OrderByDescending(v => v.CreatedAt).ThenByDescending(v => v.Id)
                .Select(v => (int?)v.ProyectoId)
                .FirstOrDefaultAsync();
        }

        // Rol para filtrar el catálogo de actividades: se lee de la categoría del puesto
        // vigente del trabajador (workers.puesto_id → puesto.categoria_id), NO del rol de
        // login — un mismo usuario puede tener varios roles de sistema pero solo un puesto.
        public async Task<string?> GetRolDeUsuarioAsync(int userId)
        {
            using var ctx = _factory.CreateDbContext();

            var categoriaId = await ctx.Person
                .Where(p => p.UserId == userId)
                .Join(ctx.Worker, p => p.PersonId, w => w.PersonId, (p, w) => w)
                .Where(w => w.State)
                .Select(w => w.PuestoCatalogo!.CategoriaId)
                .FirstOrDefaultAsync();

            return categoriaId switch
            {
                CategoriaIds.CoordinadorSsoma => "coordinador_ssoma",
                CategoriaIds.Prevencionista   => "prevencionista",
                _ => null
            };
        }

        public async Task<CumplimientoMiResumenDto> GetMiResumenAsync(int userId)
        {
            var proyectoId = await GetProyectoActualDeUsuarioAsync(userId);
            var rol = await GetRolDeUsuarioAsync(userId);

            if (proyectoId == null)
                return new CumplimientoMiResumenDto { ProyectoId = 0, ProyectoNombre = "", Rol = rol, Actividades = new() };

            using var ctx = _factory.CreateDbContext();
            var proyectoNombre = await ctx.Project
                .Where(p => p.ProjectId == proyectoId.Value)
                .Select(p => p.ProjectDescription)
                .FirstOrDefaultAsync() ?? "";

            var resumen = await GetResumenProyectoAsync(proyectoId.Value, userId, rol);

            return new CumplimientoMiResumenDto
            {
                ProyectoId     = proyectoId.Value,
                ProyectoNombre = proyectoNombre,
                Rol            = rol,
                Actividades    = resumen.Actividades
            };
        }

        // Prevencionistas y coordinadores SSOMA cuyo proyecto actual es este (misma resolución
        // que GetProyectoActualDeUsuarioAsync, pero en bloque), para que el coordinador elija
        // de quién ver el checklist en "Por Proyecto" e "Histórico".
        public async Task<List<CumplimientoUsuarioDto>> GetUsuariosProyectoAsync(int proyectoId)
        {
            using var ctx = _factory.CreateDbContext();

            var candidatos = await ctx.Person
                .Where(p => p.UserId != null)
                .Join(
                    ctx.Worker.Where(w => w.State
                        && (w.PuestoCatalogo!.CategoriaId == CategoriaIds.Prevencionista
                            || w.PuestoCatalogo!.CategoriaId == CategoriaIds.CoordinadorSsoma)),
                    p => p.PersonId, w => w.PersonId,
                    (p, w) => new
                    {
                        WorkerId = w.Id,
                        UserId = p.UserId!.Value,
                        Nombre = p.FullName,
                        CategoriaId = w.PuestoCatalogo!.CategoriaId
                    })
                .ToListAsync();

            var workerIds = candidatos.Select(c => c.WorkerId).ToList();

            var asignaciones = await ctx.WorkerProyecto
                .Where(wp => workerIds.Contains(wp.WorkerId) && wp.FechaFin == null)
                .ToListAsync();
            var vinculaciones = await ctx.WorkerVinculacion
                .Where(v => workerIds.Contains(v.WorkerId) && v.FechaFin == null)
                .ToListAsync();

            var resultado = new List<CumplimientoUsuarioDto>();
            foreach (var c in candidatos)
            {
                var proyectoActual = asignaciones
                    .Where(wp => wp.WorkerId == c.WorkerId)
                    .OrderByDescending(wp => wp.FechaInicio).ThenByDescending(wp => wp.Id)
                    .Select(wp => (int?)wp.ProyectoId)
                    .FirstOrDefault()
                    ?? vinculaciones
                        .Where(v => v.WorkerId == c.WorkerId)
                        .OrderByDescending(v => v.CreatedAt).ThenByDescending(v => v.Id)
                        .Select(v => (int?)v.ProyectoId)
                        .FirstOrDefault();

                if (proyectoActual != proyectoId) continue;

                resultado.Add(new CumplimientoUsuarioDto
                {
                    UserId = c.UserId,
                    Nombre = c.Nombre ?? "",
                    Rol = c.CategoriaId == CategoriaIds.CoordinadorSsoma ? "coordinador_ssoma" : "prevencionista"
                });
            }

            return resultado.OrderBy(u => u.Nombre).ToList();
        }

        // ─────────────────────────────────────────────────────────────────
        // HISTÓRICO / INDICADORES
        // ─────────────────────────────────────────────────────────────────

        // Foto día a día (o semana/mes según la frecuencia) de cuántas actividades había
        // vigentes y cómo terminaron. Un día sin ninguna fila creada (nadie marcó nada
        // ese día) igual aparece con Total = actividades activas de esa frecuencia y
        // Pendientes = Total, para que el histórico no tenga huecos.
        public async Task<CumplimientoHistoricoDto> GetHistoricoAsync(int proyectoId, int usuarioId, string frecuencia, DateOnly desde, DateOnly hasta)
        {
            // El total son solo las actividades que le tocan a ese usuario según su puesto.
            var rol = await GetRolDeUsuarioAsync(usuarioId);

            using var ctx = _factory.CreateDbContext();

            var totalActividades = await ctx.SsCumplimientoActividad
                .CountAsync(a => a.Activo && a.Frecuencia == frecuencia
                              && (rol == null || a.RolResponsable == rol || a.RolResponsable == "ambos"));

            var registros = await ctx.SsCumplimientoRegistro
                .Where(r => r.ProyectoId == proyectoId && r.UsuarioId == usuarioId && r.Periodo >= desde && r.Periodo <= hasta)
                .Join(ctx.SsCumplimientoActividad.Where(a => a.Frecuencia == frecuencia),
                      r => r.ActividadId, a => a.Id, (r, a) => r)
                .ToListAsync();

            var paso = frecuencia switch { "semanal" => 7, "mensual" => 0, "anual" => -1, _ => 1 };
            var dias = new List<CumplimientoHistoricoDiaDto>();

            if (paso == -1)
            {
                // Anual: un punto por cada 1 de enero dentro del rango.
                var cursor = new DateOnly(desde.Year, 1, 1);
                while (cursor <= hasta)
                {
                    dias.Add(ArmarDia(cursor, totalActividades, registros, frecuencia));
                    cursor = cursor.AddYears(1);
                }
            }
            else if (paso == 0)
            {
                // Mensual: un punto por cada día 1 de mes dentro del rango.
                var cursor = new DateOnly(desde.Year, desde.Month, 1);
                while (cursor <= hasta)
                {
                    dias.Add(ArmarDia(cursor, totalActividades, registros, frecuencia));
                    cursor = cursor.AddMonths(1);
                }
            }
            else
            {
                // Semanal: los periodos guardados son siempre el lunes de la semana
                // (ver PeriodoActual), así que el recorrido debe alinearse a lunes o
                // los registros nunca calzarían con el cursor.
                var cursor = paso == 7 ? desde.AddDays(-((int)desde.DayOfWeek == 0 ? 6 : (int)desde.DayOfWeek - 1)) : desde;
                while (cursor <= hasta)
                {
                    dias.Add(ArmarDia(cursor, totalActividades, registros, frecuencia));
                    cursor = cursor.AddDays(paso);
                }
            }

            return new CumplimientoHistoricoDto { ProyectoId = proyectoId, Frecuencia = frecuencia, Dias = dias };
        }

        private static CumplimientoHistoricoDiaDto ArmarDia(DateOnly periodo, int totalActividades, List<SsCumplimientoRegistro> registros, string frecuencia)
        {
            var delDia = registros.Where(r => r.Periodo == periodo).ToList();
            var cumplidas = delDia.Count(r => r.Estado == "cumplido");
            var noAplica = delDia.Count(r => r.Estado == "no_aplica");
            var pendientes = Math.Max(0, totalActividades - cumplidas - noAplica);
            var aplicables = totalActividades - noAplica;

            return new CumplimientoHistoricoDiaDto
            {
                Periodo = periodo,
                NumeroSemana = frecuencia == "semanal" ? ISOWeek.GetWeekOfYear(periodo.ToDateTime(TimeOnly.MinValue)) : null,
                Total = totalActividades,
                Cumplidas = cumplidas,
                NoAplica = noAplica,
                Pendientes = pendientes,
                PorcentajeCumplimiento = aplicables > 0 ? Math.Round(100.0 * cumplidas / aplicables, 1) : 0
            };
        }
    }
}
