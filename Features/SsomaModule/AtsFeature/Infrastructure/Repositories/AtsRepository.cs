using System.Security.Cryptography;
using System.Text;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.SsomaModule.AtsFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.AtsFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.SsomaModule.AtsFeature.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Dapper;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.SsomaModule.AtsFeature.Infrastructure.Repositories;

public class AtsRepository : IAtsRepository
{
    private const int PageSize = 20;

    /// <summary>Categorías que le tocan a CUALQUIER puesto, sin importar staff/oficina central —
    /// el resto solo aparece si hay un mapeo explícito en ss_ats_paso_puesto.</summary>
    private static readonly string[] CategoriasUniversales = ["Trabajos de gabinete", "Supervisión y liberación en campo"];

    private readonly IDbContextFactory<AppDbContext> _factory;

    public AtsRepository(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<int> ResolverWorkerIdAsync(int userId)
    {
        using var ctx = _factory.CreateDbContext();

        var person = await ctx.Person.FirstOrDefaultAsync(p => p.UserId == userId)
            ?? throw new AbrilException("No tienes un perfil de persona asociado a tu usuario.", 403);

        var worker = await ctx.Worker.FirstOrDefaultAsync(w => w.PersonId == person.PersonId)
            ?? throw new AbrilException("No tienes un perfil de trabajador registrado en el sistema.", 403);

        return worker.Id;
    }

    public async Task<(int? PuestoId, int? ProyectoActualId)> GetPuestoYProyectoActual(int workerId)
    {
        using var ctx = _factory.CreateDbContext();

        var worker = await ctx.Worker.FirstOrDefaultAsync(w => w.Id == workerId);

        var vinculacionVigente = await ctx.Set<Abril_Backend.Infrastructure.Models.WorkerVinculacion>()
            .Where(v => v.WorkerId == workerId && v.FechaFin == null)
            .OrderByDescending(v => v.Id)
            .FirstOrDefaultAsync();

        return (worker?.PuestoId, vinculacionVigente?.ProyectoId);
    }

    public async Task<int?> GetPersonIdDeWorker(int workerId)
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.Worker.Where(w => w.Id == workerId).Select(w => w.PersonId).FirstOrDefaultAsync();
    }

    /// <summary>Lote de <see cref="GetListaInit"/>. Los proyectos son los de <see cref="InitSql"/>.</summary>
    private const string ListaInitSql = """
        SELECT v.proyecto_id
        FROM worker_vinculaciones v
        WHERE v.worker_id = @workerId AND v.fecha_fin IS NULL
        ORDER BY v.id DESC
        LIMIT 1;

        SELECT p.project_id AS "Id", p.project_description AS "Nombre"
        FROM project p
        WHERE p.active
        ORDER BY p.project_description;
        """;

    public async Task<AtsListaInitDto> GetListaInit(int workerId)
    {
        using var ctx = _factory.CreateDbContext();
        var conn = ctx.Database.GetDbConnection();
        await conn.OpenAsync();
        using var multi = await conn.QueryMultipleAsync(ListaInitSql, new { workerId });

        var proyectoActualId = await multi.ReadFirstOrDefaultAsync<int?>();
        var proyectos = (await multi.ReadAsync<AtsProyectoDto>()).ToList();
        return new AtsListaInitDto { Proyectos = proyectos, ProyectoActualId = proyectoActualId };
    }

    /// <summary>Peligros activos y sus riesgos activos, en dos resultados que arma
    /// <see cref="LeerPeligros"/>. Lo usan <see cref="GetInit"/> y <see cref="GetPeligrosConRiesgos"/>.</summary>
    private const string PeligrosSql = """
        SELECT pe.id AS "Id", pe.nombre AS "Nombre"
        FROM ss_ats_peligro pe
        WHERE pe.activo
        ORDER BY pe.orden, pe.id;

        SELECT r.peligro_id AS "PeligroId", r.id AS "Id", r.nombre AS "Nombre", r.requiere_petar AS "RequierePetar"
        FROM ss_ats_riesgo r
        JOIN ss_ats_peligro pe ON pe.id = r.peligro_id AND pe.activo
        WHERE r.activo
        ORDER BY r.orden, r.id;
        """;

    /// <summary>Plantillas activas y sus peligros, EPP y herramientas, en cuatro resultados que arma
    /// <see cref="LeerPlantillas"/> (antes, un Include de tres colecciones: producto cartesiano). Lo
    /// usan <see cref="GetInit"/> y <see cref="GetPlantillasActivas"/>.</summary>
    private const string PlantillasSql = """
        SELECT pl.id AS "Id", pl.nombre AS "Nombre", pl.puesto_id AS "PuestoId"
        FROM ss_ats_plantilla pl
        WHERE pl.activo
        ORDER BY pl.id;

        SELECT x.plantilla_id AS "PlantillaId", x.peligro_id AS "Id"
        FROM ss_ats_plantilla_peligro x
        JOIN ss_ats_plantilla pl ON pl.id = x.plantilla_id AND pl.activo
        ORDER BY x.id;

        SELECT x.plantilla_id AS "PlantillaId", x.epp_id AS "Id"
        FROM ss_ats_plantilla_epp x
        JOIN ss_ats_plantilla pl ON pl.id = x.plantilla_id AND pl.activo
        ORDER BY x.id;

        SELECT x.plantilla_id AS "PlantillaId", x.herramienta_id AS "Id"
        FROM ss_ats_plantilla_herramienta x
        JOIN ss_ats_plantilla pl ON pl.id = x.plantilla_id AND pl.activo
        ORDER BY x.id;
        """;

    /// <summary>Puestos activos y vigentes, por nombre. Lo usan <see cref="GetInit"/> (el selector de
    /// puesto del ATS Grupal) y <see cref="GetPuestos"/>.</summary>
    private const string PuestosSql = """
        SELECT pu.puesto_id AS "Id", pu.nombre AS "Nombre"
        FROM puesto pu
        WHERE pu.active AND pu.state
        ORDER BY pu.nombre, pu.puesto_id;
        """;

    /// <summary>
    /// Los pasos activos de las categorías activas que le tocan a un puesto: los mapeados a ese
    /// puesto en ss_ats_paso_puesto y, solo si quien llena el ATS es Staff u Oficina Central
    /// (@esStaff), también los de las categorías universales. <paramref name="puesto"/> es de dónde
    /// sale el puesto: el del trabajador en <see cref="GetInit"/>, uno elegido en
    /// <see cref="GetPasosParaPuesto"/> (sin puesto, solo los universales). Las filas las agrupa
    /// <see cref="AgruparPasos"/>.
    /// </summary>
    private static string PasosSql(string puesto) => $"""
        SELECT c.id AS "CategoriaId", c.nombre AS "CategoriaNombre",
               p.id AS "Id", p.texto AS "Texto", p.requiere_petar AS "RequierePetar"
        FROM ss_ats_categoria_paso c
        JOIN ss_ats_paso p ON p.categoria_id = c.id AND p.activo
        WHERE c.activo
          AND ((@esStaff AND c.nombre = ANY(@universales))
               OR p.id IN (SELECT pp.paso_id FROM ss_ats_paso_puesto pp WHERE pp.puesto_id = {puesto}))
        ORDER BY c.orden, c.id, p.orden, p.id;
        """;

    /// <summary>El puesto del trabajador de @workerId, dentro del lote de <see cref="GetInit"/>.</summary>
    private const string PuestoDelTrabajador =
        "(SELECT w.puesto_id FROM workers w WHERE w.id = @workerId AND w.state)";

    /// <summary>
    /// Lote de <see cref="GetInit"/>, en el orden en que se lee. Los pasos salen de
    /// <see cref="PasosSql"/> con el puesto del trabajador. La plantilla sugerida es la primera
    /// mapeada a su puesto (ss_ats_plantilla_puesto).
    /// </summary>
    private static readonly string InitSql = """
        SELECT (SELECT w.puesto_id FROM workers w WHERE w.id = @workerId AND w.state) AS "PuestoId",
               (SELECT v.proyecto_id FROM worker_vinculaciones v
                 WHERE v.worker_id = @workerId AND v.fecha_fin IS NULL
                 ORDER BY v.id DESC LIMIT 1) AS "ProyectoActualId";

        SELECT p.project_id AS "Id", p.project_description AS "Nombre"
        FROM project p
        WHERE p.active
        ORDER BY p.project_description;
        """ + "\n" + PuestosSql + "\n" + PasosSql(PuestoDelTrabajador) + "\n" + PeligrosSql + "\n" + """
        SELECT e.id AS "Id", e.nombre AS "Nombre", e.categoria AS "Categoria"
        FROM ss_ats_epp e
        WHERE e.activo
        ORDER BY e.orden, e.id;

        SELECT h.id AS "Id", h.nombre AS "Nombre", h.categoria AS "Categoria"
        FROM ss_ats_herramienta h
        WHERE h.activo
        ORDER BY h.categoria, h.orden, h.id;
        """ + "\n" + PlantillasSql + "\n" + """
        SELECT pp.plantilla_id
        FROM ss_ats_plantilla_puesto pp
        JOIN ss_ats_plantilla pl ON pl.id = pp.plantilla_id AND pl.activo
        WHERE pp.puesto_id = (SELECT w.puesto_id FROM workers w WHERE w.id = @workerId AND w.state)
        ORDER BY pp.id
        LIMIT 1;

        SELECT EXISTS (SELECT 1 FROM ss_ats_consentimiento c WHERE c.worker_id = @workerId);
        """;

    public async Task<AtsInitDto> GetInit(int workerId, bool esStaff)
    {
        using var ctx = _factory.CreateDbContext();
        var conn = ctx.Database.GetDbConnection();
        await conn.OpenAsync();
        using var multi = await conn.QueryMultipleAsync(
            InitSql, new { workerId, esStaff, universales = CategoriasUniversales });

        var trabajador = await multi.ReadSingleAsync<AtsInitTrabajadorFila>();
        var proyectos = (await multi.ReadAsync<AtsProyectoDto>()).ToList();
        var puestos = (await multi.ReadAsync<AtsPuestoDto>()).ToList();
        var pasos = AgruparPasos(await multi.ReadAsync<AtsPasoFila>());
        var peligros = await LeerPeligros(multi);
        var epps = (await multi.ReadAsync<AtsEppDto>()).ToList();
        var herramientas = (await multi.ReadAsync<AtsHerramientaDto>()).ToList();
        var plantillas = await LeerPlantillas(multi);
        var plantillaSugeridaId = await multi.ReadFirstOrDefaultAsync<int?>();
        var tieneConsentimiento = await multi.ReadSingleAsync<bool>();

        return new AtsInitDto
        {
            Proyectos = proyectos,
            ProyectoActualId = trabajador.ProyectoActualId,
            PuestoId = trabajador.PuestoId,
            Puestos = puestos,
            Pasos = pasos,
            Peligros = peligros,
            Epps = epps,
            Herramientas = herramientas,
            Plantillas = plantillas,
            PlantillaSugeridaId = plantillaSugeridaId,
            TieneConsentimiento = tieneConsentimiento,
        };
    }

    /// <summary>Pasos para un puesto cualquiera (el wizard de ATS Grupal elige el de la cuadrilla).
    /// Mismo SQL que los pasos de <see cref="GetInit"/>: una sola regla para los dos.</summary>
    public async Task<List<AtsCategoriaPasoDto>> GetPasosParaPuesto(int? puestoId, bool esStaff)
    {
        using var ctx = _factory.CreateDbContext();
        var filas = await ctx.Database.GetDbConnection().QueryAsync<AtsPasoFila>(
            PasosSql("@puestoId"), new { puestoId, esStaff, universales = CategoriasUniversales });
        return AgruparPasos(filas);
    }

    /// <summary>Las filas de <see cref="PasosSql"/> (ya ordenadas) agrupadas por categoría; una
    /// categoría sin pasos que le toquen no aparece.</summary>
    private static List<AtsCategoriaPasoDto> AgruparPasos(IEnumerable<AtsPasoFila> filas) =>
        filas
            .GroupBy(f => (f.CategoriaId, f.CategoriaNombre))
            .Select(g => new AtsCategoriaPasoDto
            {
                Id = g.Key.CategoriaId,
                Nombre = g.Key.CategoriaNombre,
                Pasos = g.Select(f => new AtsPasoDto { Id = f.Id, Texto = f.Texto, RequierePetar = f.RequierePetar }).ToList(),
            })
            .ToList();

    private static async Task<List<AtsPeligroDto>> LeerPeligros(SqlMapper.GridReader multi)
    {
        var peligros = (await multi.ReadAsync<AtsPeligroDto>()).ToList();
        var riesgos = (await multi.ReadAsync<AtsRiesgoFila>()).ToLookup(r => r.PeligroId);
        foreach (var p in peligros)
            p.Riesgos = riesgos[p.Id]
                .Select(r => new AtsRiesgoDto { Id = r.Id, Nombre = r.Nombre, RequierePetar = r.RequierePetar })
                .ToList();
        return peligros;
    }

    private static async Task<List<AtsPlantillaDto>> LeerPlantillas(SqlMapper.GridReader multi)
    {
        var plantillas = (await multi.ReadAsync<AtsPlantillaDto>()).ToList();
        var peligros = (await multi.ReadAsync<AtsPlantillaItemFila>()).ToLookup(x => x.PlantillaId, x => x.Id);
        var epps = (await multi.ReadAsync<AtsPlantillaItemFila>()).ToLookup(x => x.PlantillaId, x => x.Id);
        var herramientas = (await multi.ReadAsync<AtsPlantillaItemFila>()).ToLookup(x => x.PlantillaId, x => x.Id);
        foreach (var p in plantillas)
        {
            p.PeligroIds = peligros[p.Id].ToList();
            p.EppIds = epps[p.Id].ToList();
            p.HerramientaIds = herramientas[p.Id].ToList();
        }
        return plantillas;
    }

    /// <summary>
    /// Agrega un paso al catálogo sobre la marcha — el ATS es dinámico, así que un paso que un
    /// trabajador necesita y no está listado queda disponible para todos desde ese momento, no
    /// solo para este ATS puntual. Nace SIN mapeo de puesto (visible para nadie más hasta que
    /// alguien lo asigne en "Pasos por puesto"), salvo que su categoría sea universal.
    /// </summary>
    public async Task<AtsPasoDto> CrearPasoPersonalizado(int categoriaId, string texto)
    {
        using var ctx = _factory.CreateDbContext();
        var maxOrden = await ctx.SsAtsPaso.Where(p => p.CategoriaId == categoriaId).Select(p => (short?)p.Orden).MaxAsync() ?? 0;

        var paso = new SsAtsPaso { CategoriaId = categoriaId, Texto = texto, Orden = (short)(maxOrden + 1), Activo = true };
        ctx.SsAtsPaso.Add(paso);
        await ctx.SaveChangesAsync();

        return new AtsPasoDto { Id = paso.Id, Texto = paso.Texto };
    }

    public async Task<List<AtsPeligroDto>> GetPeligrosConRiesgos()
    {
        using var ctx = _factory.CreateDbContext();
        var conn = ctx.Database.GetDbConnection();
        await conn.OpenAsync();
        using var multi = await conn.QueryMultipleAsync(PeligrosSql);
        return await LeerPeligros(multi);
    }

    public async Task SetRiesgoRequierePetar(int riesgoId, bool requierePetar)
    {
        using var ctx = _factory.CreateDbContext();
        var riesgo = await ctx.SsAtsRiesgo.FirstOrDefaultAsync(r => r.Id == riesgoId)
            ?? throw new AbrilException("Riesgo no encontrado.", 404);
        riesgo.RequierePetar = requierePetar;
        await ctx.SaveChangesAsync();
    }

    public async Task<List<AtsPlantillaDto>> GetPlantillasActivas()
    {
        using var ctx = _factory.CreateDbContext();
        var conn = ctx.Database.GetDbConnection();
        await conn.OpenAsync();
        using var multi = await conn.QueryMultipleAsync(PlantillasSql);
        return await LeerPlantillas(multi);
    }

    public async Task<List<AtsPuestoDto>> GetPuestos()
    {
        using var ctx = _factory.CreateDbContext();
        return (await ctx.Database.GetDbConnection().QueryAsync<AtsPuestoDto>(PuestosSql)).ToList();
    }

    public async Task<List<AtsPasoPuestoDto>> GetPasoPuestoMapeo()
    {
        using var ctx = _factory.CreateDbContext();

        var pasos = await ctx.SsAtsPaso
            .Include(p => p.Categoria)
            .Where(p => p.Activo && !CategoriasUniversales.Contains(p.Categoria!.Nombre))
            .OrderBy(p => p.Categoria!.Orden).ThenBy(p => p.Orden)
            .ToListAsync();

        var mapeos = await ctx.SsAtsPasoPuesto.ToListAsync();

        return pasos.Select(p => new AtsPasoPuestoDto
        {
            PasoId = p.Id,
            CategoriaNombre = p.Categoria!.Nombre,
            Texto = p.Texto,
            PuestoIds = mapeos.Where(m => m.PasoId == p.Id).Select(m => m.PuestoId).ToList(),
        }).ToList();
    }

    public async Task SetPasoPuestos(int pasoId, List<int> puestoIds)
    {
        using var ctx = _factory.CreateDbContext();

        var existentes = await ctx.SsAtsPasoPuesto.Where(m => m.PasoId == pasoId).ToListAsync();
        ctx.SsAtsPasoPuesto.RemoveRange(existentes);
        foreach (var puestoId in puestoIds.Distinct())
            ctx.SsAtsPasoPuesto.Add(new SsAtsPasoPuesto { PasoId = pasoId, PuestoId = puestoId });

        await ctx.SaveChangesAsync();
    }

    public async Task<List<AtsPlantillaPuestoDto>> GetPlantillaPuestoMapeo()
    {
        using var ctx = _factory.CreateDbContext();
        var plantillas = await ctx.SsAtsPlantilla
            .Where(p => p.Activo)
            .OrderBy(p => p.Nombre)
            .Select(p => new { p.Id, p.Nombre })
            .ToListAsync();
        var plantillaIds = plantillas.Select(p => p.Id).ToList();
        var mapeos = await ctx.SsAtsPlantillaPuesto
            .Where(pp => plantillaIds.Contains(pp.PlantillaId))
            .ToListAsync();
        var puestosPorPlantilla = mapeos.GroupBy(m => m.PlantillaId).ToDictionary(g => g.Key, g => g.Select(m => m.PuestoId).ToList());

        return plantillas.Select(p => new AtsPlantillaPuestoDto
        {
            PlantillaId = p.Id,
            PlantillaNombre = p.Nombre,
            PuestoIds = puestosPorPlantilla.GetValueOrDefault(p.Id, []),
        }).ToList();
    }

    public async Task SetPlantillaPuestos(int plantillaId, List<int> puestoIds)
    {
        using var ctx = _factory.CreateDbContext();
        var existentes = await ctx.SsAtsPlantillaPuesto.Where(pp => pp.PlantillaId == plantillaId).ToListAsync();
        ctx.SsAtsPlantillaPuesto.RemoveRange(existentes);
        foreach (var pid in puestoIds.Distinct())
            ctx.SsAtsPlantillaPuesto.Add(new SsAtsPlantillaPuesto { PlantillaId = plantillaId, PuestoId = pid });
        await ctx.SaveChangesAsync();
    }

    public async Task<int> Crear(int workerId, AtsGuardarRequestDto dto)
    {
        using var ctx = _factory.CreateDbContext();

        var worker = await ctx.Worker.FirstOrDefaultAsync(w => w.Id == workerId);

        int? atsAnteriorId = null;
        if (dto.AtsAnteriorId.HasValue)
        {
            var anterior = await ctx.SsAts.FirstOrDefaultAsync(a => a.Id == dto.AtsAnteriorId.Value)
                ?? throw new AbrilException("El ATS que intentas corregir no existe.", 404);
            if (anterior.WorkerId != workerId)
                throw new AbrilException("Ese ATS no te pertenece.", 403);
            if (anterior.Estado != "Firmado")
                throw new AbrilException("Solo se puede corregir un ATS ya firmado.", 409);
            if (anterior.Fecha != DateOnly.FromDateTime(DateTime.Today))
                throw new AbrilException("Solo se puede corregir un ATS firmado el mismo día. Si es otro día, crea un ATS nuevo.", 409);
            atsAnteriorId = anterior.Id;
        }

        var ats = new SsAts
        {
            Codigo = await SiguienteCodigoAts(ctx, dto.ProyectoId),
            WorkerId = workerId,
            ProyectoId = dto.ProyectoId,
            PuestoId = worker?.PuestoId,
            PlantillaId = dto.PlantillaId,
            Actividad = dto.Actividad,
            TorreNombre = dto.TorreNombre,
            Pisos = dto.Pisos,
            Lugar = dto.Lugar,
            Fecha = DateOnly.FromDateTime(DateTime.Today),
            Estado = "Borrador",
            AtsAnteriorId = atsAnteriorId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        await LlenarDetalle(ctx, ats, dto);

        ctx.SsAts.Add(ats);
        await ctx.SaveChangesAsync();

        var hash = ComputeHash(null, "Creado", ats.Id, workerId);
        ctx.SsAtsAuditLog.Add(new SsAtsAuditLog
        {
            AtsId = ats.Id,
            Evento = "Creado",
            Detalle = $"proyecto_id={dto.ProyectoId}",
            HashAnterior = null,
            Hash = hash,
        });
        await ctx.SaveChangesAsync();

        return ats.Id;
    }

    public async Task Editar(int id, int workerId, AtsGuardarRequestDto dto)
    {
        using var ctx = _factory.CreateDbContext();

        var ats = await ctx.SsAts
            .Include(a => a.Pasos).Include(a => a.Epps).Include(a => a.Herramientas).Include(a => a.RiesgosDetalle)
            .FirstOrDefaultAsync(a => a.Id == id)
            ?? throw new AbrilException("ATS no encontrado.", 404);

        if (ats.WorkerId != workerId)
            throw new AbrilException("Este ATS no te pertenece.", 403);
        if (ats.Estado != "Borrador")
            throw new AbrilException("Un ATS firmado no se puede editar. Crea uno nuevo si necesitas corregirlo.", 409);

        ats.ProyectoId = dto.ProyectoId;
        ats.PlantillaId = dto.PlantillaId;
        ats.Actividad = dto.Actividad;
        ats.TorreNombre = dto.TorreNombre;
        ats.Pisos = dto.Pisos;
        ats.Lugar = dto.Lugar;
        ats.UpdatedAt = DateTime.UtcNow;

        ctx.SsAtsPasoSeleccionado.RemoveRange(ats.Pasos);
        ctx.SsAtsEppSeleccionado.RemoveRange(ats.Epps);
        ctx.SsAtsHerramientaSeleccionada.RemoveRange(ats.Herramientas);
        ctx.SsAtsRiesgoDetalle.RemoveRange(ats.RiesgosDetalle);
        ats.Pasos.Clear();
        ats.Epps.Clear();
        ats.Herramientas.Clear();
        ats.RiesgosDetalle.Clear();

        await LlenarDetalle(ctx, ats, dto);

        await ctx.SaveChangesAsync();

        var hashAnterior = await GetUltimoHashAuditLog(id);
        var hash = ComputeHash(hashAnterior, "Editado", id, workerId);
        ctx.SsAtsAuditLog.Add(new SsAtsAuditLog { AtsId = id, Evento = "Editado", HashAnterior = hashAnterior, Hash = hash });
        await ctx.SaveChangesAsync();
    }

    /// <summary>Arma el detalle (pasos/EPP/herramientas/riesgos) como snapshot del catálogo — un
    /// cambio posterior al catálogo nunca reescribe un ATS ya creado, ni siquiera en borrador.</summary>
    private static async Task LlenarDetalle(AppDbContext ctx, SsAts ats, AtsGuardarRequestDto dto)
    {
        if (dto.Pasos.Count > 0)
        {
            var pasoIds = dto.Pasos.Where(p => p.PasoId.HasValue).Select(p => p.PasoId!.Value).ToList();
            var pasosCatalogo = await ctx.SsAtsPaso.Include(p => p.Categoria)
                .Where(p => pasoIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id);

            short orden = 0;
            foreach (var p in dto.Pasos)
            {
                if (p.PasoId.HasValue)
                {
                    if (!pasosCatalogo.TryGetValue(p.PasoId.Value, out var cat)) continue;
                    ats.Pasos.Add(new SsAtsPasoSeleccionado
                    {
                        PasoId = p.PasoId,
                        CategoriaNombre = cat.Categoria?.Nombre ?? string.Empty,
                        Texto = cat.Texto,
                        Aplica = p.Aplica,
                        Orden = orden++,
                    });
                }
                else if (!string.IsNullOrWhiteSpace(p.Texto))
                {
                    // Paso "de una sola vez": escrito a mano para este ATS puntual, no toca el
                    // catálogo ni la plantilla — no toda actividad se repite en otro ATS.
                    ats.Pasos.Add(new SsAtsPasoSeleccionado
                    {
                        PasoId = null,
                        CategoriaNombre = p.CategoriaNombre ?? string.Empty,
                        Texto = p.Texto.Trim(),
                        Aplica = p.Aplica,
                        Orden = orden++,
                    });
                }
            }
        }

        if (dto.EppIds.Count > 0)
        {
            var epps = await ctx.SsAtsEpp.Where(e => dto.EppIds.Contains(e.Id)).ToListAsync();
            foreach (var e in epps)
                ats.Epps.Add(new SsAtsEppSeleccionado { EppId = e.Id, Nombre = e.Nombre });
        }

        if (dto.HerramientaIds.Count > 0)
        {
            var herramientas = await ctx.SsAtsHerramienta.Where(h => dto.HerramientaIds.Contains(h.Id)).ToListAsync();
            foreach (var h in herramientas)
                ats.Herramientas.Add(new SsAtsHerramientaSeleccionada { HerramientaId = h.Id, Nombre = h.Nombre });
        }

        foreach (var nombre in dto.HerramientasPersonalizadas)
        {
            if (string.IsNullOrWhiteSpace(nombre)) continue;
            ats.Herramientas.Add(new SsAtsHerramientaSeleccionada { HerramientaId = null, Nombre = nombre.Trim() });
        }

        if (dto.Riesgos.Count > 0)
        {
            var riesgoIds = dto.Riesgos.Select(r => r.RiesgoId).ToList();
            var riesgosCatalogo = await ctx.SsAtsRiesgo.Include(r => r.Peligro)
                .Where(r => riesgoIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id);

            short orden = 0;
            foreach (var r in dto.Riesgos)
            {
                if (!riesgosCatalogo.TryGetValue(r.RiesgoId, out var cat)) continue;
                ats.RiesgosDetalle.Add(new SsAtsRiesgoDetalle
                {
                    PeligroId = r.PeligroId,
                    RiesgoId = r.RiesgoId,
                    PeligroNombre = cat.Peligro?.Nombre ?? string.Empty,
                    RiesgoNombre = cat.Nombre,
                    RiesgoBase = r.RiesgoBase,
                    Controles = r.Controles,
                    RiesgoResidual = r.RiesgoResidual,
                    Orden = orden++,
                });
            }
        }
    }

    public async Task<SsAts?> GetEntidad(int id)
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.SsAts.FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task<AtsResponseDto?> GetPorId(int id)
    {
        using var ctx = _factory.CreateDbContext();
        var ats = await ctx.SsAts
            .Include(a => a.Worker).ThenInclude(w => w!.Person)
            .Include(a => a.Proyecto)
            .Include(a => a.Puesto)
            .Include(a => a.Plantilla)
            .Include(a => a.Pasos)
            .Include(a => a.Epps)
            .Include(a => a.Herramientas)
            .Include(a => a.RiesgosDetalle)
            .FirstOrDefaultAsync(a => a.Id == id);

        return ats == null ? null : ToDto(ats);
    }

    /// <summary>"{ABREV}-ATS-0001": abreviatura del proyecto (misma que Accidentes/Observaciones) +
    /// correlativo propio de ese proyecto. El upsert atómico evita números repetidos si dos ATS se
    /// crean a la vez.</summary>
    private static async Task<string> SiguienteCodigoAts(AppDbContext ctx, int proyectoId, string tipo = "ATS")
    {
        var abrev = await ctx.Project.Where(p => p.ProjectId == proyectoId)
            .Select(p => p.Abbreviation ?? p.Codigo).FirstOrDefaultAsync() ?? "PRY";
        var res = await ctx.Database.SqlQuery<int>(
            $"INSERT INTO ss_documento_correlativo (proyecto_id, tipo, ultimo) VALUES ({proyectoId}, {tipo}, 1) ON CONFLICT (proyecto_id, tipo) DO UPDATE SET ultimo = ss_documento_correlativo.ultimo + 1 RETURNING ultimo AS \"Value\"")
            .ToListAsync();
        return $"{abrev.Trim().ToUpperInvariant()}-{tipo}-{res[0]:D4}";
    }

    private static AtsResponseDto ToDto(SsAts ats) => new()
    {
        Id = ats.Id,
        Codigo = ats.Codigo,
        AnuladoMotivo = ats.AnuladoMotivo,
        WorkerId = ats.WorkerId,
        WorkerNombre = ats.Worker?.Person?.FullName,
        ProyectoId = ats.ProyectoId,
        ProyectoNombre = ats.Proyecto?.ProjectDescription,
        PuestoId = ats.PuestoId,
        PuestoNombre = ats.Puesto?.Nombre,
        PlantillaId = ats.PlantillaId,
        PlantillaNombre = ats.Plantilla?.Nombre,
        Actividad = ats.Actividad,
        TorreNombre = ats.TorreNombre,
        Pisos = ats.Pisos,
        Lugar = ats.Lugar,
        Fecha = ats.Fecha,
        HoraServidorFirma = ats.HoraServidorFirma,
        OrigenOffline = ats.OrigenOffline,
        HoraDispositivo = ats.HoraDispositivo,
        Lat = ats.Lat,
        Lng = ats.Lng,
        PrecisionMetros = ats.PrecisionMetros,
        SelfieUrl = ats.SelfieUrl,
        FirmaUrl = ats.FirmaUrl,
        Estado = ats.Estado,
        AtsAnteriorId = ats.AtsAnteriorId,
        PdfHash = ats.PdfHash,
        AtsGrupoId = ats.AtsGrupoId,
        CapatazNombre = ats.CapatazNombre,
        CapatazCargo = ats.CapatazCargo,
        CapatazFirmaUrl = ats.CapatazFirmaUrl,
        CapatazHoraServidor = ats.CapatazHoraServidor,
        AutorizaNombre = ats.AutorizaNombre,
        AutorizaCargo = ats.AutorizaCargo,
        AutorizaFirmaUrl = ats.AutorizaFirmaUrl,
        AutorizaHoraServidor = ats.AutorizaHoraServidor,
        SsomaNombre = ats.SsomaNombre,
        SsomaCargo = ats.SsomaCargo,
        SsomaFirmaUrl = ats.SsomaFirmaUrl,
        SsomaHoraServidor = ats.SsomaHoraServidor,
        Pasos = ats.Pasos.OrderBy(p => p.Orden).Select(p => new AtsPasoResponseDto
        {
            PasoId = p.PasoId,
            CategoriaNombre = p.CategoriaNombre,
            Texto = p.Texto,
            Aplica = p.Aplica,
        }).ToList(),
        Epps = ats.Epps.Select(e => e.Nombre).ToList(),
        Herramientas = ats.Herramientas.Select(h => h.Nombre).ToList(),
        Riesgos = ats.RiesgosDetalle.OrderBy(r => r.Orden).Select(r => new AtsRiesgoDetalleResponseDto
        {
            PeligroId = r.PeligroId,
            RiesgoId = r.RiesgoId,
            PeligroNombre = r.PeligroNombre,
            RiesgoNombre = r.RiesgoNombre,
            RiesgoBase = r.RiesgoBase,
            Controles = r.Controles,
            RiesgoResidual = r.RiesgoResidual,
        }).ToList(),
    };

    public async Task CompletarInfoPetar(List<AtsResponseDto> ats, Dictionary<int, (bool EsResidente, bool EsSsoma)> permisosPorAtsId)
    {
        if (ats.Count == 0) return;
        using var ctx = _factory.CreateDbContext();

        var riesgoIdsQueRequierenPetar = await ctx.SsAtsRiesgo.Where(r => r.RequierePetar).Select(r => r.Id).ToListAsync();

        var atsIds = ats.Select(a => a.Id).ToList();

        // dto.Riesgos viene vacío en el listado (ToDtoResumen, ver AtsRepository.Listar) — se
        // consulta aparte, liviano (solo AtsId+RiesgoId), en vez de depender del include pesado.
        var atsIdsConRiesgoPetar = (await ctx.SsAtsRiesgoDetalle
            .Where(r => atsIds.Contains(r.AtsId) && riesgoIdsQueRequierenPetar.Contains(r.RiesgoId))
            .Select(r => r.AtsId)
            .Distinct()
            .ToListAsync())
            .ToHashSet();

        var petares = await ctx.SsPetar
            .Include(p => p.Tipo)
            .Include(p => p.PetarGrupo)
            .Where(p => atsIds.Contains(p.AtsId))
            .ToListAsync();
        var petaresPorAts = petares.ToLookup(p => p.AtsId);

        foreach (var dto in ats)
        {
            dto.RequierePetar = atsIdsConRiesgoPetar.Contains(dto.Id);
            var (esResidente, esSsoma) = permisosPorAtsId.GetValueOrDefault(dto.Id);
            dto.Petares = petaresPorAts[dto.Id]
                .Select(p =>
                {
                    // Un PETAR que nació de un PETAR grupal NO se firma desde acá — Supervisor/
                    // SSOMA firman UNA vez para todo el grupo, desde el dashboard del ATS grupal
                    // (ver PetarGrupo). Acá solo se refleja si ya quedó autorizado, nunca se
                    // habilita el botón de firmar individual (evita firmar 20 veces lo mismo).
                    var supervisorFirmaUrl = p.PetarGrupoId.HasValue ? p.PetarGrupo?.SupervisorFirmaUrl : p.SupervisorFirmaUrl;
                    var ssomaFirmaUrl = p.PetarGrupoId.HasValue ? p.PetarGrupo?.SsomaFirmaUrl : p.SsomaFirmaUrl;
                    return new AtsPetarResumenDto
                    {
                        Id = p.Id,
                        Codigo = p.Codigo,
                        TipoNombre = p.Tipo?.Nombre,
                        Estado = p.Estado,
                        TieneFirmaEjecutante = p.FirmaUrl != null,
                        SupervisorFirmado = supervisorFirmaUrl != null,
                        SsomaFirmado = ssomaFirmaUrl != null,
                        PuedeFirmarSupervisor = !p.PetarGrupoId.HasValue && esResidente && p.FirmaUrl != null && p.SupervisorFirmaUrl == null && p.Estado == "Borrador",
                        PuedeFirmarSsoma = !p.PetarGrupoId.HasValue && esSsoma && p.FirmaUrl != null && p.SsomaFirmaUrl == null && p.Estado == "Borrador",
                    };
                })
                .ToList();
        }
    }

    public async Task<bool> TieneConsentimiento(int workerId)
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.SsAtsConsentimiento.AnyAsync(c => c.WorkerId == workerId);
    }

    public async Task RegistrarConsentimiento(int workerId, string? ipOrigen)
    {
        using var ctx = _factory.CreateDbContext();
        ctx.SsAtsConsentimiento.Add(new SsAtsConsentimiento { WorkerId = workerId, IpOrigen = ipOrigen, AceptadoEn = DateTime.UtcNow });
        await ctx.SaveChangesAsync();
    }

    public async Task<bool> TieneAutorizacionPermiso(int workerId)
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.SsAtsAutorizacionPermiso.AnyAsync(a => a.WorkerId == workerId && a.ArchivoUrl != null);
    }

    public async Task<(string? FirmaDigitalUrl, string? Nombre, string? Dni)> GetFirmaDigitalAutorizacion(int workerId)
    {
        using var ctx = _factory.CreateDbContext();
        var permiso = await ctx.SsAtsAutorizacionPermiso.FirstOrDefaultAsync(a => a.WorkerId == workerId);
        var (nombre, dni) = await GetNombreYDni(workerId);
        return (permiso?.FirmaDigitalUrl, nombre, dni);
    }

    private static bool EsNombreCapataz(string? puesto) =>
        puesto != null && (puesto.Contains("Capataz", StringComparison.OrdinalIgnoreCase)
            || puesto.Contains("Maestro de Obra", StringComparison.OrdinalIgnoreCase));

    public async Task<bool> EsCapatazOMaestro(int workerId)
    {
        using var ctx = _factory.CreateDbContext();
        var puesto = await ctx.Worker.Where(w => w.Id == workerId).Select(w => w.PuestoCatalogo != null ? w.PuestoCatalogo.Nombre : null).FirstOrDefaultAsync();
        return EsNombreCapataz(puesto);
    }

    public async Task<string?> GetEmailPersonalAutorizacion(int workerId)
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.SsAtsAutorizacionPermiso.Where(a => a.WorkerId == workerId).Select(a => a.EmailPersonal).FirstOrDefaultAsync();
    }

    /// <summary>Registra el correo personal declarado (solo Capataz/Maestro de obra). Crea la fila de
    /// permiso si todavía no existe — el correo se pide ANTES de imprimir la plantilla, que lo lleva
    /// impreso en la declaración.</summary>
    public async Task GuardarEmailPersonalAutorizacion(int workerId, string email)
    {
        using var ctx = _factory.CreateDbContext();
        var permiso = await ctx.SsAtsAutorizacionPermiso.FirstOrDefaultAsync(a => a.WorkerId == workerId);
        if (permiso == null)
        {
            permiso = new SsAtsAutorizacionPermiso { WorkerId = workerId };
            ctx.SsAtsAutorizacionPermiso.Add(permiso);
        }
        permiso.EmailPersonal = email;
        permiso.EmailDeclaradoEn = DateTime.UtcNow;
        await ctx.SaveChangesAsync();
    }

    public async Task<bool> TieneUsuario(int workerId)
    {
        using var ctx = _factory.CreateDbContext();
        var userId = await ctx.Worker.Where(w => w.Id == workerId).Select(w => w.Person != null ? w.Person.UserId : null).FirstOrDefaultAsync();
        return userId != null && await ctx.User.AnyAsync(u => u.UserId == userId && u.State);
    }

    public async Task<int?> GetRoleIdCapataz()
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.Role.Where(r => r.RoleDescription.ToUpper() == "CAPATAZ / MAESTRO DE OBRA").Select(r => (int?)r.RoleId).FirstOrDefaultAsync();
    }

    public async Task<(string Dni, string Nombres, string ApellidoPaterno, string ApellidoMaterno, int? Telefono)?> GetDatosPersonaParaCuenta(int workerId)
    {
        using var ctx = _factory.CreateDbContext();
        var p = await ctx.Worker.Where(w => w.Id == workerId).Select(w => w.Person).FirstOrDefaultAsync();
        if (p == null || string.IsNullOrEmpty(p.DocumentIdentityCode)) return null;
        return (p.DocumentIdentityCode, p.FirstNames ?? string.Empty, p.FirstLastName ?? string.Empty, p.SecondLastName ?? string.Empty, p.PhoneNumber);
    }

    public async Task CapturarFirmaDigitalAutorizacion(int workerId, string firmaUrl, string firmaHash, int capturadoPorUserId)
    {
        using var ctx = _factory.CreateDbContext();
        var existente = await ctx.SsAtsAutorizacionPermiso.FirstOrDefaultAsync(a => a.WorkerId == workerId);

        if (existente != null)
        {
            existente.FirmaDigitalUrl = firmaUrl;
            existente.FirmaDigitalHash = firmaHash;
            existente.FirmadoDigitalEn = DateTime.UtcNow;
            existente.FirmadoDigitalPorUserId = capturadoPorUserId;
        }
        else
        {
            ctx.SsAtsAutorizacionPermiso.Add(new SsAtsAutorizacionPermiso
            {
                WorkerId = workerId,
                FirmaDigitalUrl = firmaUrl,
                FirmaDigitalHash = firmaHash,
                FirmadoDigitalEn = DateTime.UtcNow,
                FirmadoDigitalPorUserId = capturadoPorUserId,
            });
        }

        await ctx.SaveChangesAsync();
    }

    public async Task SubirAutorizacionPermiso(int workerId, string archivoUrl, int? subidoPorUserId)
    {
        using var ctx = _factory.CreateDbContext();
        var existente = await ctx.SsAtsAutorizacionPermiso.FirstOrDefaultAsync(a => a.WorkerId == workerId);

        if (existente != null)
        {
            existente.ArchivoUrl = archivoUrl;
            existente.SubidoPorUserId = subidoPorUserId;
            existente.SubidoEn = DateTime.UtcNow;
        }
        else
        {
            ctx.SsAtsAutorizacionPermiso.Add(new SsAtsAutorizacionPermiso
            {
                WorkerId = workerId,
                ArchivoUrl = archivoUrl,
                SubidoPorUserId = subidoPorUserId,
                SubidoEn = DateTime.UtcNow,
            });
        }

        await ctx.SaveChangesAsync();
    }

    /// <summary>Universo: trabajadores activos de Staff/Oficina Central — los obreros de obra
    /// llenan su ATS en físico y no necesitan esta autorización de firma digital.</summary>
    public async Task<List<AtsAutorizacionTrabajadorDto>> GetTrabajadoresParaAutorizacion()
    {
        using var ctx = _factory.CreateDbContext();

        var workers = await ctx.Worker
            .Where(w => w.WorkersEstadoId == Abril_Backend.Shared.Constants.WorkersEstadoIds.Activo
                && ((w.ObraOficinaStaffId != null
                        && Abril_Backend.Shared.Constants.ObraOficinaStaffIds.StaffUOficinaCentral.Contains(w.ObraOficinaStaffId.Value))
                    // Capataz / Maestro de obra: no son Staff pero firman por toda la cuadrilla, así
                    // que también pasan por esta autorización (con correo personal para su cuenta).
                    || (w.PuestoCatalogo != null
                        && (w.PuestoCatalogo.Nombre.ToLower().Contains("capataz")
                            || w.PuestoCatalogo.Nombre.ToLower().Contains("maestro de obra")))))
            .OrderBy(w => w.Person != null ? w.Person.FullName : null)
            .Select(w => new
            {
                w.Id,
                Nombre = w.Person != null ? w.Person.FullName : null,
                Dni = w.Person != null ? w.Person.DocumentIdentityCode : null,
                ObraOficinaStaffId = w.ObraOficinaStaffId,
                PuestoNombre = w.PuestoCatalogo != null ? w.PuestoCatalogo.Nombre : null,
                UserId = w.Person != null ? w.Person.UserId : null,
            })
            .ToListAsync();

        var userIdsDeWorkers = workers.Where(x => x.UserId != null).Select(x => x.UserId!.Value).ToList();
        var userIdsConCuenta = (await ctx.User
            .Where(u => u.State && userIdsDeWorkers.Contains(u.UserId))
            .Select(u => u.UserId)
            .ToListAsync()).ToHashSet();

        var workerIds = workers.Select(w => w.Id).ToList();

        var autorizaciones = await ctx.SsAtsAutorizacionPermiso
            .Where(a => workerIds.Contains(a.WorkerId))
            .ToListAsync();
        var autorizacionMap = autorizaciones.ToDictionary(a => a.WorkerId, a => a);

        // El proyecto vigente de un worker no vive en workers (no hay workers.project_id): se lee
        // de worker_vinculaciones sin fecha_fin, mismo criterio que EvGestionSsomaRepository.
        var proyectoPorWorker = await ctx.WorkerVinculacion
            .Where(v => workerIds.Contains(v.WorkerId) && v.FechaFin == null && v.ProyectoId != null)
            .Select(v => new { v.WorkerId, v.ProyectoId, ProyectoNombre = v.Proyecto != null ? v.Proyecto.ProjectDescription : null })
            .ToListAsync();
        var proyectoMap = proyectoPorWorker
            .GroupBy(v => v.WorkerId)
            .ToDictionary(g => g.Key, g => g.First());

        return workers.Select(w =>
        {
            proyectoMap.TryGetValue(w.Id, out var proy);
            return new AtsAutorizacionTrabajadorDto
            {
                WorkerId = w.Id,
                Nombre = w.Nombre ?? $"Worker {w.Id}",
                Dni = w.Dni,
                ProyectoId = proy?.ProyectoId,
                ProyectoNombre = proy?.ProyectoNombre,
                ObraOficinaStaff = Abril_Backend.Shared.Constants.ObraOficinaStaffIds.Nombre(w.ObraOficinaStaffId),
                TieneFirmaDigital = autorizacionMap.GetValueOrDefault(w.Id)?.FirmaDigitalUrl != null,
                TieneAutorizacion = autorizacionMap.GetValueOrDefault(w.Id)?.ArchivoUrl != null,
                SubidoEn = autorizacionMap.GetValueOrDefault(w.Id)?.SubidoEn,
                ArchivoUrl = autorizacionMap.GetValueOrDefault(w.Id)?.ArchivoUrl,
                EsCapatazOMaestro = EsNombreCapataz(w.PuestoNombre),
                EmailPersonal = autorizacionMap.GetValueOrDefault(w.Id)?.EmailPersonal,
                TieneUsuario = w.UserId != null && userIdsConCuenta.Contains(w.UserId.Value),
            };
        }).ToList();
    }

    public async Task Firmar(
        SsAts ats, string selfieUrl, string selfieHash, string firmaUrl, string firmaHash,
        DateTime horaServidor, AtsFirmarRequestDto body, string? ipOrigen, string? userAgent)
    {
        using var ctx = _factory.CreateDbContext();

        var entidad = await ctx.SsAts.FirstOrDefaultAsync(a => a.Id == ats.Id)
            ?? throw new AbrilException("ATS no encontrado.", 404);
        if (entidad.Estado != "Borrador")
            throw new AbrilException("Este ATS ya fue firmado.", 409);

        entidad.SelfieUrl = selfieUrl;
        entidad.SelfieHash = selfieHash;
        entidad.FirmaUrl = firmaUrl;
        entidad.FirmaHash = firmaHash;
        entidad.HoraServidorFirma = horaServidor;
        entidad.HoraDispositivo = body.HoraDispositivo;
        entidad.Lat = body.Lat;
        entidad.Lng = body.Lng;
        entidad.PrecisionMetros = body.PrecisionMetros;
        entidad.IpOrigen = ipOrigen;
        entidad.UserAgent = userAgent;
        entidad.Estado = "Firmado";
        entidad.UpdatedAt = DateTime.UtcNow;

        // Hash de verificación pública (QR del PDF) — se fija ACÁ, al firmar, y nunca se
        // vuelve a tocar. Antes se recalculaba en AtsService.GenerarPdf a partir de los bytes
        // del PDF ya generado, después de que esos mismos bytes ya llevaban impreso el QR con
        // el hash ANTERIOR (o vacío, la primera vez) — un huevo-y-gallina que garantizaba que
        // el hash del QR nunca coincidiera con el que terminaba guardado. Al depender solo de
        // datos que quedan fijos desde la firma (no de la renderización del PDF, que puede
        // variar entre exportaciones del mismo documento), el QR y el valor guardado siempre
        // coinciden, sin importar cuántas veces se vuelva a exportar el PDF.
        entidad.PdfHash = ComputeHashVerificacion(ats.Id, entidad.WorkerId, firmaHash, selfieHash, horaServidor);

        await ctx.SaveChangesAsync();

        var hashAnterior = await GetUltimoHashAuditLog(ats.Id);
        var hash = ComputeHash(hashAnterior, "Firmado", ats.Id, entidad.WorkerId, firmaHash);
        ctx.SsAtsAuditLog.Add(new SsAtsAuditLog
        {
            AtsId = ats.Id,
            Evento = "Firmado",
            IpOrigen = ipOrigen,
            Detalle = $"lat={body.Lat};lng={body.Lng};precision={body.PrecisionMetros};firma_hash={firmaHash}",
            HashAnterior = hashAnterior,
            Hash = hash,
        });
        await ctx.SaveChangesAsync();
    }

    public async Task GuardarPdf(int id, string pdfUrl, string pdfHash)
    {
        using var ctx = _factory.CreateDbContext();
        var ats = await ctx.SsAts.FirstOrDefaultAsync(a => a.Id == id) ?? throw new AbrilException("ATS no encontrado.", 404);

        ats.PdfUrl = pdfUrl;
        ats.PdfHash = pdfHash;
        await ctx.SaveChangesAsync();

        var hashAnterior = await GetUltimoHashAuditLog(id);
        var hash = ComputeHash(hashAnterior, "PdfExportado", id, ats.WorkerId, pdfHash);
        ctx.SsAtsAuditLog.Add(new SsAtsAuditLog { AtsId = id, Evento = "PdfExportado", Detalle = $"pdf_hash={pdfHash}", HashAnterior = hashAnterior, Hash = hash });
        await ctx.SaveChangesAsync();
    }

    public async Task<AtsResponsablesDto> GetResponsables(int proyectoId)
    {
        using var ctx = _factory.CreateDbContext();

        var proyecto = await ctx.Project.Include(p => p.CoordAdmin)
            .FirstOrDefaultAsync(p => p.ProjectId == proyectoId)
            ?? throw new AbrilException("Proyecto no encontrado.", 404);

        Abril_Backend.Infrastructure.Models.Worker? residente = proyecto.ResidenteWorkersId.HasValue
            ? await ctx.Worker.Include(w => w.Person).FirstOrDefaultAsync(w => w.Id == proyecto.ResidenteWorkersId)
            : null;

        var ssomaEmails = new List<string>();
        if (!string.IsNullOrWhiteSpace(proyecto.EmailCoordSsoma)) ssomaEmails.Add(proyecto.EmailCoordSsoma.Trim());
        if (!string.IsNullOrWhiteSpace(proyecto.CoordAdmin?.EmailCorporativo)) ssomaEmails.Add(proyecto.CoordAdmin.EmailCorporativo.Trim());

        return new AtsResponsablesDto
        {
            ProyectoNombre = proyecto.ProjectDescription,
            ResidenteNombre = residente?.Person?.FullName,
            ResidenteEmail = residente?.EmailCorporativo,
            SsomaEmails = ssomaEmails.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
        };
    }

    public async Task<bool> EsProduccionDeProyecto(int workerId, int proyectoId)
    {
        using var ctx = _factory.CreateDbContext();

        var vinculacionVigente = await ctx.Set<Abril_Backend.Infrastructure.Models.WorkerVinculacion>()
            .Include(v => v.PuestoCatalogo)
            .Where(v => v.WorkerId == workerId && v.FechaFin == null)
            .OrderByDescending(v => v.Id)
            .FirstOrDefaultAsync();

        if (vinculacionVigente?.ProyectoId != proyectoId || vinculacionVigente.PuestoCatalogo == null) return false;

        var nombre = vinculacionVigente.PuestoCatalogo.Nombre;
        return nombre.Contains("Ingeniero de Producción", StringComparison.OrdinalIgnoreCase)
            || nombre.Contains("Arquitecto de Producción", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Capataz/Maestro de obra vinculado actualmente a ese proyecto (por puesto, igual
    /// patrón que EsProduccionDeProyecto — no hay campo dedicado en Project).</summary>
    public async Task<bool> EsCapatazDeProyecto(int workerId, int proyectoId)
    {
        using var ctx = _factory.CreateDbContext();

        var vinculacionVigente = await ctx.Set<Abril_Backend.Infrastructure.Models.WorkerVinculacion>()
            .Include(v => v.PuestoCatalogo)
            .Where(v => v.WorkerId == workerId && v.FechaFin == null)
            .OrderByDescending(v => v.Id)
            .FirstOrDefaultAsync();

        if (vinculacionVigente?.ProyectoId != proyectoId || vinculacionVigente.PuestoCatalogo == null) return false;

        var nombre = vinculacionVigente.PuestoCatalogo.Nombre;
        return nombre.Contains("Capataz", StringComparison.OrdinalIgnoreCase)
            || nombre.Contains("Maestro de Obra", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>true si el ATS de este ejecutante REQUIERE firma de Capataz: obrero de obra (no
    /// Staff/Oficina Central) y que además no sea él mismo capataz/maestro de obra — el capataz
    /// ejecutando su propio ATS ya cubre ese nivel con su firma de ejecutante (ver SsAts.CapatazWorkerId).</summary>
    public async Task<bool> EsObreroDeObra(int workerId)
    {
        using var ctx = _factory.CreateDbContext();
        var worker = await ctx.Worker.Include(w => w.PuestoCatalogo).FirstOrDefaultAsync(w => w.Id == workerId);
        var puesto = worker?.PuestoCatalogo?.Nombre;
        if (puesto != null
            && (puesto.Contains("Capataz", StringComparison.OrdinalIgnoreCase)
                || puesto.Contains("Maestro de Obra", StringComparison.OrdinalIgnoreCase)))
            return false;
        if (worker?.ObraOficinaStaffId == null) return true;
        return !Abril_Backend.Shared.Constants.ObraOficinaStaffIds.StaffUOficinaCentral.Contains(worker.ObraOficinaStaffId.Value);
    }

    public async Task<bool> EsPrevencionistaAbril(int workerId)
    {
        using var ctx = _factory.CreateDbContext();

        var worker = await ctx.Worker.Include(w => w.PuestoCatalogo).FirstOrDefaultAsync(w => w.Id == workerId);
        if (worker?.ContrataCasa != "Casa" || worker.PuestoCatalogo == null) return false;

        return worker.PuestoCatalogo.Nombre.Contains("Prevencionista", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<string?> GetEmailDeUsuario(int userId)
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.User.Where(u => u.UserId == userId).Select(u => u.Email).FirstOrDefaultAsync();
    }

    public async Task<List<string>> GetEmailsAvisoCadena(int proyectoId)
    {
        using var ctx = _factory.CreateDbContext();

        var prevencionistas = await ctx.Worker
            .Where(w => w.ContrataCasa == "Casa" && w.EmailCorporativo != null && w.PuestoCatalogo != null
                        && w.PuestoCatalogo.Nombre.ToLower().Contains("prevencionista"))
            .Select(w => w.EmailCorporativo!).ToListAsync();

        var produccion = await (from v in ctx.Set<Abril_Backend.Infrastructure.Models.WorkerVinculacion>()
                                join w in ctx.Worker on v.WorkerId equals w.Id
                                where v.FechaFin == null && v.ProyectoId == proyectoId && v.PuestoCatalogo != null && w.EmailCorporativo != null
                                      && (v.PuestoCatalogo.Nombre.ToLower().Contains("ingeniero de producción")
                                          || v.PuestoCatalogo.Nombre.ToLower().Contains("arquitecto de producción"))
                                select w.EmailCorporativo).ToListAsync();

        return prevencionistas.Concat(produccion.Where(e => e != null).Select(e => e!)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    public async Task<string?> GetEmailCorporativoWorker(int workerId)
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.Worker.Where(w => w.Id == workerId).Select(w => w.EmailCorporativo).FirstOrDefaultAsync();
    }

    public async Task<(string Nombre, string? Dni)> GetNombreYDni(int workerId)
    {
        using var ctx = _factory.CreateDbContext();
        var worker = await ctx.Worker.Include(w => w.Person).FirstOrDefaultAsync(w => w.Id == workerId)
            ?? throw new AbrilException("Trabajador no encontrado.", 404);
        return (worker.Person?.FullName ?? $"Worker {workerId}", worker.Person?.DocumentIdentityCode);
    }

    public async Task<(string Nombre, string? Cargo)> GetNombreYCargo(int workerId)
    {
        using var ctx = _factory.CreateDbContext();
        var worker = await ctx.Worker.Include(w => w.Person).Include(w => w.PuestoCatalogo).FirstOrDefaultAsync(w => w.Id == workerId)
            ?? throw new AbrilException("Trabajador no encontrado.", 404);

        return (worker.Person?.FullName ?? $"Worker {workerId}", worker.PuestoCatalogo?.Nombre);
    }

    public async Task FirmarVisto(int atsId, string rol, int workerId, string nombre, string? cargo, string firmaUrl, string firmaHash, DateTime horaServidor)
    {
        using var ctx = _factory.CreateDbContext();
        var ats = await ctx.SsAts.FirstOrDefaultAsync(a => a.Id == atsId) ?? throw new AbrilException("ATS no encontrado.", 404);

        if (ats.Estado != "Firmado")
            throw new AbrilException("El ejecutante debe firmar el ATS antes de que se agreguen las demás firmas.", 409);

        if (rol == "Capataz")
        {
            if (ats.CapatazFirmaUrl != null)
                throw new AbrilException("Este ATS ya tiene la firma de Capataz/Maestro de obra.", 409);
            ats.CapatazWorkerId = workerId;
            ats.CapatazNombre = nombre;
            ats.CapatazCargo = cargo;
            ats.CapatazFirmaUrl = firmaUrl;
            ats.CapatazFirmaHash = firmaHash;
            ats.CapatazHoraServidor = horaServidor;
        }
        else if (rol == "Autoriza")
        {
            if (ats.AutorizaFirmaUrl != null)
                throw new AbrilException("Este ATS ya tiene la firma de Autoriza (Residente/Ing. Producción).", 409);
            ats.AutorizaWorkerId = workerId;
            ats.AutorizaNombre = nombre;
            ats.AutorizaCargo = cargo;
            ats.AutorizaFirmaUrl = firmaUrl;
            ats.AutorizaFirmaHash = firmaHash;
            ats.AutorizaHoraServidor = horaServidor;
        }
        else
        {
            if (ats.SsomaFirmaUrl != null)
                throw new AbrilException("Este ATS ya tiene el Visto Bueno de SSOMA.", 409);
            ats.SsomaWorkerId = workerId;
            ats.SsomaNombre = nombre;
            ats.SsomaCargo = cargo;
            ats.SsomaFirmaUrl = firmaUrl;
            ats.SsomaFirmaHash = firmaHash;
            ats.SsomaHoraServidor = horaServidor;
        }

        ats.UpdatedAt = DateTime.UtcNow;
        await ctx.SaveChangesAsync();

        var hashAnterior = await GetUltimoHashAuditLog(atsId);
        var hash = ComputeHash(hashAnterior, $"Firmado{rol}", atsId, workerId, firmaHash);
        ctx.SsAtsAuditLog.Add(new SsAtsAuditLog { AtsId = atsId, Evento = $"Firmado{rol}", UserId = null, Detalle = $"firma_hash={firmaHash}", HashAnterior = hashAnterior, Hash = hash });
        await ctx.SaveChangesAsync();
    }

    public async Task<string?> GetUltimoHashAuditLog(int atsId)
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.SsAtsAuditLog.Where(l => l.AtsId == atsId).OrderByDescending(l => l.Id).Select(l => l.Hash).FirstOrDefaultAsync();
    }

    public async Task AgregarAuditLog(int atsId, string evento, int? userId, string? ipOrigen, string? detalle, string hashAnterior, string hash)
    {
        using var ctx = _factory.CreateDbContext();
        ctx.SsAtsAuditLog.Add(new SsAtsAuditLog { AtsId = atsId, Evento = evento, UserId = userId, IpOrigen = ipOrigen, Detalle = detalle, HashAnterior = hashAnterior, Hash = hash });
        await ctx.SaveChangesAsync();
    }

    public async Task<bool> ExisteSelfieHash(string selfieHash)
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.SsAts.AnyAsync(a => a.SelfieHash == selfieHash);
    }

    /// <summary>Listado paginado — a propósito NO trae Pasos/Epps/Herramientas/RiesgosDetalle
    /// (eran includes pesados repetidos en cada una de las 20 filas de la página, aun cuando la
    /// fila no está expandida): eso se pide aparte por ATS individual (GetPorId) recién cuando el
    /// usuario hace clic en "Ver detalle" o abre el modal de firma. Con cientos de ATS/día y ~100
    /// usuarios en simultáneo, ese detalle de más era el grueso de la lentitud del listado.</summary>
    public async Task<AtsListResponseDto> Listar(AtsFiltroDto filtro)
    {
        using var ctx = _factory.CreateDbContext();

        var query = ctx.SsAts
            .AsNoTracking()
            .Include(a => a.Worker).ThenInclude(w => w!.Person)
            .Include(a => a.Proyecto)
            .Include(a => a.Puesto)
            .AsQueryable();

        if (filtro.ProyectoId.HasValue) query = query.Where(a => a.ProyectoId == filtro.ProyectoId);
        if (filtro.WorkerId.HasValue) query = query.Where(a => a.WorkerId == filtro.WorkerId);
        if (filtro.FechaDesde.HasValue) query = query.Where(a => a.Fecha >= filtro.FechaDesde);
        if (filtro.FechaHasta.HasValue) query = query.Where(a => a.Fecha <= filtro.FechaHasta);
        if (!string.IsNullOrWhiteSpace(filtro.Estado)) query = query.Where(a => a.Estado == filtro.Estado);
        if (filtro.SoloIndividuales) query = query.Where(a => a.AtsGrupoId == null);

        query = query.OrderByDescending(a => a.Fecha).ThenByDescending(a => a.Id);

        var total = await query.CountAsync();
        var page = Math.Max(1, filtro.Page);
        var data = await query.Skip((page - 1) * PageSize).Take(PageSize).ToListAsync();

        return new AtsListResponseDto
        {
            Data = data.Select(ToDtoResumen).ToList(),
            Page = page,
            PageSize = PageSize,
            TotalRecords = total,
            TotalPages = (int)Math.Ceiling(total / (double)PageSize),
        };
    }

    public async Task<AtsGrupoListResponseDto> ListarGrupos(AtsFiltroDto filtro)
    {
        using var ctx = _factory.CreateDbContext();

        var query = ctx.SsAtsGrupo.AsNoTracking().AsQueryable();
        if (filtro.ProyectoId.HasValue) query = query.Where(g => g.ProyectoId == filtro.ProyectoId);
        if (filtro.FechaDesde.HasValue) query = query.Where(g => g.Fecha >= filtro.FechaDesde);
        if (filtro.FechaHasta.HasValue) query = query.Where(g => g.Fecha <= filtro.FechaHasta);
        if (!string.IsNullOrWhiteSpace(filtro.Estado)) query = query.Where(g => g.Estado == filtro.Estado);

        var total = await query.CountAsync();
        var page = Math.Max(1, filtro.Page);

        // Una sola consulta con el conteo de adhesiones como subconsulta — no se traen las filas de SsAts.
        var filas = await query
            .OrderByDescending(g => g.Fecha).ThenByDescending(g => g.Id)
            .Skip((page - 1) * PageSize).Take(PageSize)
            .Select(g => new
            {
                g.Id,
                g.Codigo,
                g.Revision,
                g.ProyectoId,
                g.QrToken,
                ObsAbiertas = ctx.SsAtsObservacion.Count(o => o.AtsGrupoId == g.Id && o.Estado == "Abierta"),
                ReqPetar = ctx.SsAtsGrupoRiesgoDetalle.Any(r => r.AtsGrupoId == g.Id && ctx.SsAtsRiesgo.Any(c => c.Id == r.RiesgoId && c.RequierePetar)),
                NPetares = ctx.SsPetarGrupo.Count(p => p.AtsGrupoId == g.Id),
                NPetaresPend = ctx.SsPetarGrupo.Count(p => p.AtsGrupoId == g.Id && (p.SupervisorFirmaUrl == null || p.SsomaFirmaUrl == null)),
                ProyectoNombre = g.Proyecto != null ? g.Proyecto.ProjectDescription : null,
                g.Actividad, g.TorreNombre, g.Pisos, g.Lugar, g.Fecha, g.Estado,
                CreadoPorNombre = g.CreadoPorWorker != null && g.CreadoPorWorker.Person != null ? g.CreadoPorWorker.Person.FullName : null,
                Total = ctx.SsAts.Count(a => a.AtsGrupoId == g.Id),
                g.CapatazNombre,
                CapatazFirmado = g.CapatazFirmaUrl != null,
                g.CapatazAdhesionesAlFirmar,
            })
            .ToListAsync();

        // Avance de la cadena de firmas de cada cuadrilla de la página — una consulta para todas.
        var grupoIds = filas.Select(f => f.Id).ToList();
        var adhesiones = await ctx.SsAts.AsNoTracking()
            .Where(a => a.AtsGrupoId != null && grupoIds.Contains(a.AtsGrupoId.Value))
            .Select(a => new { GrupoId = a.AtsGrupoId!.Value, a.WorkerId, a.Estado, Autoriza = a.AutorizaFirmaUrl != null, Ssoma = a.SsomaFirmaUrl != null })
            .ToListAsync();
        var flagsObrero = await GetEsObreroDeObraBatch(adhesiones.Select(a => a.WorkerId));
        var adhesionesPorGrupo = adhesiones.ToLookup(a => a.GrupoId);

        return new AtsGrupoListResponseDto
        {
            Data = filas.Select(f =>
            {
                var alFirmar = f.CapatazAdhesionesAlFirmar ?? 0;
                var adh = adhesionesPorGrupo[f.Id].ToList();
                var firmadas = adh.Where(a => a.Estado == "Firmado").ToList();
                return new AtsGrupoListaItemDto
                {
                    // Sin adheridos todavía se asume que requiere Capataz solo si ya hubo firma de uno.
                    RequiereCapataz = adh.Count > 0 ? adh.Any(a => flagsObrero.GetValueOrDefault(a.WorkerId)) : f.CapatazFirmado,
                    EjecutantesFirmados = firmadas.Count,
                    AutorizaFirmados = firmadas.Count(a => a.Autoriza),
                    SsomaFirmados = firmadas.Count(a => a.Ssoma),
                    Id = f.Id,
                    ProyectoId = f.ProyectoId,
                    CapatazLinkToken = f.QrToken.ToString(),
                    Codigo = f.Codigo,
                    Revision = f.Revision,
                    RequierePetar = f.ReqPetar,
                    TotalPetares = f.NPetares,
                    PetaresPendientesFirma = f.NPetaresPend,
                    ObservacionesAbiertas = f.ObsAbiertas,
                    ProyectoNombre = f.ProyectoNombre,
                    Actividad = f.Actividad,
                    TorreNombre = f.TorreNombre,
                    Pisos = f.Pisos,
                    Lugar = f.Lugar,
                    Fecha = f.Fecha,
                    Estado = f.Estado,
                    CreadoPorNombre = f.CreadoPorNombre,
                    TotalAdhesiones = f.Total,
                    CapatazNombre = f.CapatazNombre,
                    CapatazFirmado = f.CapatazFirmado,
                    CapatazVigente = f.CapatazFirmado && f.Total <= alFirmar,
                    CapatazNuevosSinValidar = f.CapatazFirmado ? Math.Max(0, f.Total - alFirmar) : 0,
                };
            }).ToList(),
            Page = page,
            PageSize = PageSize,
            TotalRecords = total,
            TotalPages = (int)Math.Ceiling(total / (double)PageSize),
        };
    }

    public async Task<Dictionary<int, bool>> GetEsObreroDeObraBatch(IEnumerable<int> workerIds)
    {
        var ids = workerIds.Distinct().ToList();
        if (ids.Count == 0) return [];
        using var ctx = _factory.CreateDbContext();
        var workers = await ctx.Worker.AsNoTracking()
            .Where(w => ids.Contains(w.Id))
            .Select(w => new { w.Id, w.ObraOficinaStaffId, Puesto = w.PuestoCatalogo != null ? w.PuestoCatalogo.Nombre : null })
            .ToListAsync();

        // Misma regla que EsObreroDeObra.
        return workers.ToDictionary(w => w.Id, w =>
        {
            if (w.Puesto != null
                && (w.Puesto.Contains("Capataz", StringComparison.OrdinalIgnoreCase)
                    || w.Puesto.Contains("Maestro de Obra", StringComparison.OrdinalIgnoreCase)))
                return false;
            if (w.ObraOficinaStaffId == null) return true;
            return !Abril_Backend.Shared.Constants.ObraOficinaStaffIds.StaffUOficinaCentral.Contains(w.ObraOficinaStaffId.Value);
        });
    }

    /// <summary>Misma forma que ToDto pero sin Pasos/Epps/Herramientas/Riesgos (listas vacías) —
    /// ver comentario en Listar. No requiere que esas colecciones de navegación vengan cargadas.</summary>
    private static AtsResponseDto ToDtoResumen(SsAts ats) => new()
    {
        Id = ats.Id,
        Codigo = ats.Codigo,
        AnuladoMotivo = ats.AnuladoMotivo,
        WorkerId = ats.WorkerId,
        WorkerNombre = ats.Worker?.Person?.FullName,
        ProyectoId = ats.ProyectoId,
        ProyectoNombre = ats.Proyecto?.ProjectDescription,
        PuestoId = ats.PuestoId,
        PuestoNombre = ats.Puesto?.Nombre,
        PlantillaId = ats.PlantillaId,
        PlantillaNombre = ats.Plantilla?.Nombre,
        Actividad = ats.Actividad,
        TorreNombre = ats.TorreNombre,
        Pisos = ats.Pisos,
        Lugar = ats.Lugar,
        Fecha = ats.Fecha,
        HoraServidorFirma = ats.HoraServidorFirma,
        OrigenOffline = ats.OrigenOffline,
        HoraDispositivo = ats.HoraDispositivo,
        Lat = ats.Lat,
        Lng = ats.Lng,
        PrecisionMetros = ats.PrecisionMetros,
        SelfieUrl = ats.SelfieUrl,
        FirmaUrl = ats.FirmaUrl,
        Estado = ats.Estado,
        AtsAnteriorId = ats.AtsAnteriorId,
        PdfHash = ats.PdfHash,
        AtsGrupoId = ats.AtsGrupoId,
        CapatazNombre = ats.CapatazNombre,
        CapatazCargo = ats.CapatazCargo,
        CapatazFirmaUrl = ats.CapatazFirmaUrl,
        CapatazHoraServidor = ats.CapatazHoraServidor,
        AutorizaNombre = ats.AutorizaNombre,
        AutorizaCargo = ats.AutorizaCargo,
        AutorizaFirmaUrl = ats.AutorizaFirmaUrl,
        AutorizaHoraServidor = ats.AutorizaHoraServidor,
        SsomaNombre = ats.SsomaNombre,
        SsomaCargo = ats.SsomaCargo,
        SsomaFirmaUrl = ats.SsomaFirmaUrl,
        SsomaHoraServidor = ats.SsomaHoraServidor,
        Pasos = [],
        Epps = [],
        Herramientas = [],
        Riesgos = [],
    };

    // ── Administración de plantillas ────────────────────────────────────

    public async Task<int> CrearPlantilla(AtsPlantillaGuardarRequestDto dto)
    {
        using var ctx = _factory.CreateDbContext();
        var plantilla = new SsAtsPlantilla { Nombre = dto.Nombre, PuestoId = dto.PuestoId, Activo = true, CreatedAt = DateTime.UtcNow };
        AsignarPlantillaDetalle(plantilla, dto);
        ctx.SsAtsPlantilla.Add(plantilla);
        await ctx.SaveChangesAsync();
        return plantilla.Id;
    }

    public async Task EditarPlantilla(int id, AtsPlantillaGuardarRequestDto dto)
    {
        using var ctx = _factory.CreateDbContext();
        var plantilla = await ctx.SsAtsPlantilla
            .Include(p => p.Peligros).Include(p => p.Epps).Include(p => p.Herramientas)
            .FirstOrDefaultAsync(p => p.Id == id) ?? throw new AbrilException("Plantilla no encontrada.", 404);

        plantilla.Nombre = dto.Nombre;
        plantilla.PuestoId = dto.PuestoId;
        ctx.SsAtsPlantillaPeligro.RemoveRange(plantilla.Peligros);
        ctx.SsAtsPlantillaEpp.RemoveRange(plantilla.Epps);
        ctx.SsAtsPlantillaHerramienta.RemoveRange(plantilla.Herramientas);
        plantilla.Peligros.Clear();
        plantilla.Epps.Clear();
        plantilla.Herramientas.Clear();
        AsignarPlantillaDetalle(plantilla, dto);

        await ctx.SaveChangesAsync();
    }

    private static void AsignarPlantillaDetalle(SsAtsPlantilla plantilla, AtsPlantillaGuardarRequestDto dto)
    {
        foreach (var pid in dto.PeligroIds) plantilla.Peligros.Add(new SsAtsPlantillaPeligro { PeligroId = pid });
        foreach (var eid in dto.EppIds) plantilla.Epps.Add(new SsAtsPlantillaEpp { EppId = eid });
        foreach (var hid in dto.HerramientaIds) plantilla.Herramientas.Add(new SsAtsPlantillaHerramienta { HerramientaId = hid });
    }

    public async Task DesactivarPlantilla(int id)
    {
        using var ctx = _factory.CreateDbContext();
        var plantilla = await ctx.SsAtsPlantilla.FirstOrDefaultAsync(p => p.Id == id) ?? throw new AbrilException("Plantilla no encontrada.", 404);
        plantilla.Activo = false;
        await ctx.SaveChangesAsync();
    }

    // ── Actividades/pasos por plantilla ──────────────────────────────────

    public async Task<List<AtsPlantillaActividadDto>> GetActividadesDePlantilla(int plantillaId)
    {
        using var ctx = _factory.CreateDbContext();
        var actividades = await ctx.SsAtsPlantillaActividad
            .Where(a => a.PlantillaId == plantillaId && a.Activo)
            .Include(a => a.Pasos.Where(p => p.Activo))
            .Include(a => a.Peligros)
            .OrderBy(a => a.Orden)
            .ToListAsync();

        return actividades.Select(a => new AtsPlantillaActividadDto
        {
            Id = a.Id,
            PlantillaId = a.PlantillaId,
            Texto = a.Texto,
            Orden = a.Orden,
            Pasos = a.Pasos.OrderBy(p => p.Orden)
                .Select(p => new AtsPlantillaPasoDto { Id = p.Id, Texto = p.Texto, Orden = p.Orden }).ToList(),
            PeligroIds = a.Peligros.Select(p => p.PeligroId).ToList(),
        }).ToList();
    }

    public async Task<int> CrearActividad(int plantillaId, string texto)
    {
        using var ctx = _factory.CreateDbContext();
        var maxOrden = await ctx.SsAtsPlantillaActividad
            .Where(a => a.PlantillaId == plantillaId)
            .Select(a => (short?)a.Orden)
            .MaxAsync() ?? 0;
        var actividad = new SsAtsPlantillaActividad { PlantillaId = plantillaId, Texto = texto, Orden = (short)(maxOrden + 1), Activo = true };
        ctx.SsAtsPlantillaActividad.Add(actividad);
        await ctx.SaveChangesAsync();
        return actividad.Id;
    }

    public async Task EditarActividad(int actividadId, string texto)
    {
        using var ctx = _factory.CreateDbContext();
        var actividad = await ctx.SsAtsPlantillaActividad.FirstOrDefaultAsync(a => a.Id == actividadId) ?? throw new AbrilException("Actividad no encontrada.", 404);
        actividad.Texto = texto;
        await ctx.SaveChangesAsync();
    }

    public async Task EliminarActividad(int actividadId)
    {
        using var ctx = _factory.CreateDbContext();
        var actividad = await ctx.SsAtsPlantillaActividad.FirstOrDefaultAsync(a => a.Id == actividadId) ?? throw new AbrilException("Actividad no encontrada.", 404);
        actividad.Activo = false;
        await ctx.SaveChangesAsync();
    }

    public async Task<int> CrearPaso(int actividadId, string texto)
    {
        using var ctx = _factory.CreateDbContext();
        var maxOrden = await ctx.SsAtsPlantillaPaso
            .Where(p => p.ActividadId == actividadId)
            .Select(p => (short?)p.Orden)
            .MaxAsync() ?? 0;
        var paso = new SsAtsPlantillaPaso { ActividadId = actividadId, Texto = texto, Orden = (short)(maxOrden + 1), Activo = true };
        ctx.SsAtsPlantillaPaso.Add(paso);
        await ctx.SaveChangesAsync();
        return paso.Id;
    }

    public async Task EditarPaso(int pasoId, string texto)
    {
        using var ctx = _factory.CreateDbContext();
        var paso = await ctx.SsAtsPlantillaPaso.FirstOrDefaultAsync(p => p.Id == pasoId) ?? throw new AbrilException("Paso no encontrado.", 404);
        paso.Texto = texto;
        await ctx.SaveChangesAsync();
    }

    public async Task EliminarPaso(int pasoId)
    {
        using var ctx = _factory.CreateDbContext();
        var paso = await ctx.SsAtsPlantillaPaso.FirstOrDefaultAsync(p => p.Id == pasoId) ?? throw new AbrilException("Paso no encontrado.", 404);
        paso.Activo = false;
        await ctx.SaveChangesAsync();
    }

    public async Task SetActividadPeligros(int actividadId, List<int> peligroIds)
    {
        using var ctx = _factory.CreateDbContext();
        var existentes = await ctx.SsAtsPlantillaActividadPeligro.Where(x => x.ActividadId == actividadId).ToListAsync();
        ctx.SsAtsPlantillaActividadPeligro.RemoveRange(existentes);
        foreach (var pid in peligroIds.Distinct())
            ctx.SsAtsPlantillaActividadPeligro.Add(new SsAtsPlantillaActividadPeligro { ActividadId = actividadId, PeligroId = pid });
        await ctx.SaveChangesAsync();
    }

    // ── Controles sugeridos por riesgo ───────────────────────────────────

    public async Task<List<AtsRiesgoConControlesDto>> GetRiesgosConControles()
    {
        using var ctx = _factory.CreateDbContext();
        var riesgos = await ctx.SsAtsRiesgo
            .Where(r => r.Activo)
            .Include(r => r.Peligro)
            .OrderBy(r => r.Peligro!.Orden).ThenBy(r => r.Orden)
            .ToListAsync();
        var riesgoIds = riesgos.Select(r => r.Id).ToList();
        var controles = await ctx.SsAtsRiesgoControl
            .Where(c => riesgoIds.Contains(c.RiesgoId) && c.Activo)
            .OrderBy(c => c.Orden)
            .ToListAsync();
        var controlesPorRiesgo = controles.GroupBy(c => c.RiesgoId).ToDictionary(g => g.Key, g => g.ToList());

        return riesgos.Select(r => new AtsRiesgoConControlesDto
        {
            RiesgoId = r.Id,
            RiesgoNombre = r.Nombre,
            PeligroId = r.PeligroId,
            PeligroNombre = r.Peligro?.Nombre ?? string.Empty,
            Controles = controlesPorRiesgo.GetValueOrDefault(r.Id, [])
                .Select(c => new AtsRiesgoControlDto { Id = c.Id, Texto = c.Texto, Orden = c.Orden, Tipo = c.Tipo }).ToList(),
        }).ToList();
    }

    public async Task<int> CrearControl(int riesgoId, string texto, string tipo)
    {
        using var ctx = _factory.CreateDbContext();
        var maxOrden = await ctx.SsAtsRiesgoControl
            .Where(c => c.RiesgoId == riesgoId)
            .Select(c => (short?)c.Orden)
            .MaxAsync() ?? 0;
        var control = new SsAtsRiesgoControl { RiesgoId = riesgoId, Texto = texto, Tipo = tipo, Orden = (short)(maxOrden + 1), Activo = true };
        ctx.SsAtsRiesgoControl.Add(control);
        await ctx.SaveChangesAsync();
        return control.Id;
    }

    public async Task EditarControl(int controlId, string texto, string tipo)
    {
        using var ctx = _factory.CreateDbContext();
        var control = await ctx.SsAtsRiesgoControl.FirstOrDefaultAsync(c => c.Id == controlId) ?? throw new AbrilException("Control no encontrado.", 404);
        control.Texto = texto;
        control.Tipo = tipo;
        await ctx.SaveChangesAsync();
    }

    public async Task EliminarControl(int controlId)
    {
        using var ctx = _factory.CreateDbContext();
        var control = await ctx.SsAtsRiesgoControl.FirstOrDefaultAsync(c => c.Id == controlId) ?? throw new AbrilException("Control no encontrado.", 404);
        control.Activo = false;
        await ctx.SaveChangesAsync();
    }

    /// <summary>Hash que encadena la fila anterior de <see cref="SsAtsAuditLog"/> con el evento
    /// actual — ver el mismo mecanismo documentado en AtsModels.cs.</summary>
    private static string ComputeHash(string? hashAnterior, string evento, int atsId, int workerId, string? extra = null)
    {
        var payload = $"{hashAnterior}|{evento}|{atsId}|{workerId}|{DateTime.UtcNow:O}|{extra}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }

    /// <summary>Hash estable del contenido firmado (para el QR de verificación pública) — a
    /// diferencia de <see cref="ComputeHash"/>, NO incluye la hora actual ni se recalcula
    /// nunca: se fija una sola vez al firmar, a partir de datos que no cambian sin importar
    /// cuántas veces se re-exporte el PDF.</summary>
    private static string ComputeHashVerificacion(int atsId, int workerId, string firmaHash, string selfieHash, DateTime horaServidorFirma)
    {
        var payload = $"{atsId}|{workerId}|{firmaHash}|{selfieHash}|{horaServidorFirma:O}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }

    // ── ATS Grupal ──────────────────────────────────────────────────────────

    public async Task<SsAtsGrupo> CrearGrupo(int creadoPorWorkerId, AtsGuardarRequestDto dto)
    {
        using var ctx = _factory.CreateDbContext();

        var hoy = DateOnly.FromDateTime(DateTime.Today);
        var anterior = dto.GrupoAnteriorId.HasValue
            ? await ctx.SsAtsGrupo.FirstOrDefaultAsync(g => g.Id == dto.GrupoAnteriorId.Value)
            : null;
        var grupo = new SsAtsGrupo
        {
            Revision = (anterior?.Revision ?? 0) + 1,
            GrupoAnteriorId = anterior?.Id,
            Codigo = await SiguienteCodigoAts(ctx, dto.ProyectoId, "ATSG"),
            CreadoPorWorkerId = creadoPorWorkerId,
            ProyectoId = dto.ProyectoId,
            PlantillaId = dto.PlantillaId,
            Actividad = dto.Actividad,
            TorreNombre = dto.TorreNombre,
            Pisos = dto.Pisos,
            Lugar = dto.Lugar,
            Fecha = hoy,
            QrToken = Guid.NewGuid(),
            // Vence a medianoche del mismo día — un ATS grupal es para la jornada de hoy, no queda
            // reutilizable después (igual criterio que el ATS individual, que es por día).
            QrExpiraEn = hoy.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc),
            Estado = "Activo",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        if (dto.Pasos.Count > 0)
        {
            var pasoIds = dto.Pasos.Where(p => p.PasoId.HasValue).Select(p => p.PasoId!.Value).ToList();
            var pasosCatalogo = await ctx.SsAtsPaso.Include(p => p.Categoria)
                .Where(p => pasoIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id);

            short orden = 0;
            foreach (var p in dto.Pasos)
            {
                if (p.PasoId.HasValue)
                {
                    if (!pasosCatalogo.TryGetValue(p.PasoId.Value, out var cat)) continue;
                    grupo.Pasos.Add(new SsAtsGrupoPasoSeleccionado
                    {
                        PasoId = p.PasoId,
                        CategoriaNombre = cat.Categoria?.Nombre ?? string.Empty,
                        Texto = cat.Texto,
                        Aplica = p.Aplica,
                        Orden = orden++,
                    });
                }
                else if (!string.IsNullOrWhiteSpace(p.Texto))
                {
                    grupo.Pasos.Add(new SsAtsGrupoPasoSeleccionado
                    {
                        PasoId = null,
                        CategoriaNombre = p.CategoriaNombre ?? string.Empty,
                        Texto = p.Texto.Trim(),
                        Aplica = p.Aplica,
                        Orden = orden++,
                    });
                }
            }
        }

        if (dto.EppIds.Count > 0)
        {
            var epps = await ctx.SsAtsEpp.Where(e => dto.EppIds.Contains(e.Id)).ToListAsync();
            foreach (var e in epps)
                grupo.Epps.Add(new SsAtsGrupoEppSeleccionado { EppId = e.Id, Nombre = e.Nombre });
        }

        if (dto.HerramientaIds.Count > 0)
        {
            var herramientas = await ctx.SsAtsHerramienta.Where(h => dto.HerramientaIds.Contains(h.Id)).ToListAsync();
            foreach (var h in herramientas)
                grupo.Herramientas.Add(new SsAtsGrupoHerramientaSeleccionada { HerramientaId = h.Id, Nombre = h.Nombre });
        }

        foreach (var nombre in dto.HerramientasPersonalizadas)
        {
            if (string.IsNullOrWhiteSpace(nombre)) continue;
            grupo.Herramientas.Add(new SsAtsGrupoHerramientaSeleccionada { HerramientaId = null, Nombre = nombre.Trim() });
        }

        if (dto.Riesgos.Count > 0)
        {
            var riesgoIds = dto.Riesgos.Select(r => r.RiesgoId).ToList();
            var riesgosCatalogo = await ctx.SsAtsRiesgo.Include(r => r.Peligro)
                .Where(r => riesgoIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id);

            short orden = 0;
            foreach (var r in dto.Riesgos)
            {
                if (!riesgosCatalogo.TryGetValue(r.RiesgoId, out var cat)) continue;
                grupo.RiesgosDetalle.Add(new SsAtsGrupoRiesgoDetalle
                {
                    PeligroId = r.PeligroId,
                    RiesgoId = r.RiesgoId,
                    PeligroNombre = cat.Peligro?.Nombre ?? string.Empty,
                    RiesgoNombre = cat.Nombre,
                    RiesgoBase = r.RiesgoBase,
                    Controles = r.Controles,
                    RiesgoResidual = r.RiesgoResidual,
                    Orden = orden++,
                });
            }
        }

        ctx.SsAtsGrupo.Add(grupo);
        await ctx.SaveChangesAsync();

        if (anterior != null)
        {
            // La revisión reemplaza a la anterior (queda en el historial como "Reemplazado") y hereda a quienes
            // debían firmar: los integrantes esperados + quienes ya habían firmado, que ahora deben re-firmar.
            if (anterior.Estado != "Anulado") anterior.Estado = "Reemplazado";
            anterior.UpdatedAt = DateTime.UtcNow;

            var previos = await ctx.SsAtsGrupoIntegrante.Where(i => i.AtsGrupoId == anterior.Id).Select(i => i.WorkerId).ToListAsync();
            var firmaron = await ctx.SsAts.Where(a => a.AtsGrupoId == anterior.Id && a.Estado == "Firmado").Select(a => a.WorkerId).ToListAsync();
            foreach (var wid in previos.Concat(firmaron).Distinct())
                ctx.SsAtsGrupoIntegrante.Add(new SsAtsGrupoIntegrante { AtsGrupoId = grupo.Id, WorkerId = wid });
            await ctx.SaveChangesAsync();
        }
        return grupo;
    }

    public async Task<SsAtsGrupo?> GetGrupoPorToken(Guid token)
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.SsAtsGrupo
            .Include(g => g.Proyecto)
            .Include(g => g.Pasos)
            .Include(g => g.Epps)
            .Include(g => g.Herramientas)
            .Include(g => g.RiesgosDetalle)
            .FirstOrDefaultAsync(g => g.QrToken == token);
    }

    public async Task<SsAtsGrupo?> GetGrupoEntidad(int id)
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.SsAtsGrupo
            .Include(g => g.Pasos)
            .Include(g => g.Epps)
            .Include(g => g.Herramientas)
            .Include(g => g.RiesgosDetalle)
            .FirstOrDefaultAsync(g => g.Id == id);
    }

    public async Task<AtsGrupoEstadoDto?> GetEstadoGrupo(int id)
    {
        using var ctx = _factory.CreateDbContext();
        var grupo = await ctx.SsAtsGrupo.Include(g => g.Proyecto).FirstOrDefaultAsync(g => g.Id == id);
        if (grupo is null) return null;

        var adheridos = await ctx.SsAts
            .Where(a => a.AtsGrupoId == id)
            .Include(a => a.Worker).ThenInclude(w => w!.Person)
            .OrderBy(a => a.Worker!.Person!.FullName)
            .ToListAsync();

        // ¿La cuadrilla es de obreros (requiere Capataz) o de Staff/supervisores (no)? Se decide por quienes
        // ya se adhirieron; si todavía no hay nadie, por quien la creó (un Capataz/Maestro que arma la
        // cuadrilla cuenta como cuadrilla de obreros).
        bool requiereCapataz;
        if (adheridos.Count > 0)
        {
            var flags = await GetEsObreroDeObraBatch(adheridos.Select(a => a.WorkerId));
            requiereCapataz = flags.Values.Any(v => v);
        }
        else
        {
            var creador = await GetEsObreroDeObraBatch([grupo.CreadoPorWorkerId]);
            requiereCapataz = creador.GetValueOrDefault(grupo.CreadoPorWorkerId) || await EsCapatazOMaestro(grupo.CreadoPorWorkerId);
        }
        var firmados = adheridos.Where(a => a.Estado == "Firmado").ToList();

        return new AtsGrupoEstadoDto
        {
            Codigo = grupo.Codigo,
            Revision = grupo.Revision,
            RequierePetar = await ctx.SsAtsGrupoRiesgoDetalle.AnyAsync(r => r.AtsGrupoId == id && ctx.SsAtsRiesgo.Any(c => c.Id == r.RiesgoId && c.RequierePetar)),
            TotalPetares = await ctx.SsPetarGrupo.CountAsync(p => p.AtsGrupoId == id),
            GrupoAnteriorId = grupo.GrupoAnteriorId,
            ReemplazadoPorId = await ctx.SsAtsGrupo.Where(g => g.GrupoAnteriorId == id).Select(g => (int?)g.Id).FirstOrDefaultAsync(),
            ObservacionesAbiertas = await ctx.SsAtsObservacion.CountAsync(o => o.AtsGrupoId == id && o.Estado == "Abierta"),
            RequiereCapataz = requiereCapataz,
            EjecutantesFirmados = firmados.Count,
            AutorizaFirmados = firmados.Count(a => a.AutorizaFirmaUrl != null),
            SsomaFirmados = firmados.Count(a => a.SsomaFirmaUrl != null),
            Id = grupo.Id,
            Actividad = grupo.Actividad,
            ProyectoNombre = grupo.Proyecto?.ProjectDescription,
            TorreNombre = grupo.TorreNombre,
            Pisos = grupo.Pisos,
            Fecha = grupo.Fecha,
            Estado = grupo.Estado,
            QrToken = grupo.QrToken.ToString(),
            QrExpiraEn = grupo.QrExpiraEn,
            TotalAdhesiones = adheridos.Count,
            TrabajadoresAdheridos = adheridos.Select(a => a.Worker?.Person?.FullName ?? "—").ToList(),
            Adheridos = adheridos.Select(a => new AtsGrupoAdheridoDto { AtsId = a.Id, Nombre = a.Worker?.Person?.FullName ?? "—", Estado = a.Estado, AutorizaFirmado = a.AutorizaFirmaUrl != null, SsomaFirmado = a.SsomaFirmaUrl != null }).ToList(),
            CapatazNombre = grupo.CapatazNombre,
            CapatazHoraServidor = grupo.CapatazHoraServidor,
            CapatazVigente = grupo.CapatazFirmaUrl != null && adheridos.Count <= (grupo.CapatazAdhesionesAlFirmar ?? 0),
            CapatazNuevosSinValidar = grupo.CapatazFirmaUrl != null ? Math.Max(0, adheridos.Count - (grupo.CapatazAdhesionesAlFirmar ?? 0)) : 0,
        };
    }

    /// <summary>true si el ATS individual tiene un riesgo que exige PETAR y todavía no se generó ninguno.</summary>
    public async Task<bool> FaltaPetarObligatorio(int atsId)
    {
        using var ctx = _factory.CreateDbContext();
        var exige = await ctx.SsAtsRiesgoDetalle.AnyAsync(r => r.AtsId == atsId && ctx.SsAtsRiesgo.Any(c => c.Id == r.RiesgoId && c.RequierePetar));
        return exige && !await ctx.SsPetar.AnyAsync(p => p.AtsId == atsId);
    }

    public async Task<List<int>> GetAtsIdsPendientesDeVisto(int atsGrupoId, string rol, int callerWorkerId, bool incluirPropios)
    {
        using var ctx = _factory.CreateDbContext();
        var q = ctx.SsAts.AsNoTracking().Where(a => a.AtsGrupoId == atsGrupoId && a.Estado == "Firmado");
        q = rol == "Autoriza" ? q.Where(a => a.AutorizaFirmaUrl == null) : q.Where(a => a.SsomaFirmaUrl == null);
        if (!incluirPropios) q = q.Where(a => a.WorkerId != callerWorkerId);
        return await q.Select(a => a.Id).ToListAsync();
    }

    public async Task<List<SsAtsObservacion>> GetObservaciones(int? atsId, int? grupoId)
    {
        using var ctx = _factory.CreateDbContext();
        var q = ctx.SsAtsObservacion.AsNoTracking().AsQueryable();
        q = atsId.HasValue ? q.Where(o => o.AtsId == atsId) : q.Where(o => o.AtsGrupoId == grupoId);
        return await q.OrderBy(o => o.Estado == "Abierta" ? 0 : 1).ThenByDescending(o => o.CreatedAt).ToListAsync();
    }

    public async Task<SsAtsObservacion?> GetObservacion(int id)
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.SsAtsObservacion.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id);
    }

    public async Task AgregarObservacion(SsAtsObservacion o)
    {
        using var ctx = _factory.CreateDbContext();
        ctx.SsAtsObservacion.Add(o);
        await ctx.SaveChangesAsync();
    }

    public async Task ResolverObservacion(int id, string respuesta, int workerId, string nombre)
    {
        using var ctx = _factory.CreateDbContext();
        var o = await ctx.SsAtsObservacion.FirstOrDefaultAsync(x => x.Id == id) ?? throw new AbrilException("Observación no encontrada.", 404);
        o.Estado = "Resuelta";
        o.Respuesta = respuesta;
        o.ResueltaPorWorkerId = workerId;
        o.ResueltaPorNombre = nombre;
        o.ResueltaEn = DateTime.UtcNow;
        await ctx.SaveChangesAsync();
    }

    public async Task<int> ContarObservacionesAbiertas(int? atsId, int? grupoId)
    {
        using var ctx = _factory.CreateDbContext();
        var q = ctx.SsAtsObservacion.Where(o => o.Estado == "Abierta");
        q = atsId.HasValue ? q.Where(o => o.AtsId == atsId) : q.Where(o => o.AtsGrupoId == grupoId);
        return await q.CountAsync();
    }

    public async Task<Dictionary<int, int>> ContarObservacionesAbiertasPorAts(IEnumerable<int> atsIds)
    {
        var ids = atsIds.Distinct().ToList();
        if (ids.Count == 0) return [];
        using var ctx = _factory.CreateDbContext();
        return await ctx.SsAtsObservacion.AsNoTracking()
            .Where(o => o.Estado == "Abierta" && o.AtsId != null && ids.Contains(o.AtsId.Value))
            .GroupBy(o => o.AtsId!.Value)
            .Select(g => new { Id = g.Key, N = g.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.N);
    }

    public async Task ResolverObservacionesDeGrupo(int grupoId, string respuesta, int workerId, string nombre)
    {
        using var ctx = _factory.CreateDbContext();
        var abiertas = await ctx.SsAtsObservacion.Where(o => o.AtsGrupoId == grupoId && o.Estado == "Abierta").ToListAsync();
        foreach (var o in abiertas)
        {
            o.Estado = "Resuelta";
            o.Respuesta = respuesta;
            o.ResueltaPorWorkerId = workerId;
            o.ResueltaPorNombre = nombre;
            o.ResueltaEn = DateTime.UtcNow;
        }
        await ctx.SaveChangesAsync();
    }

    public async Task AnularAts(int atsId, string motivo, int workerId)
    {
        using var ctx = _factory.CreateDbContext();
        var a = await ctx.SsAts.FirstOrDefaultAsync(x => x.Id == atsId) ?? throw new AbrilException("ATS no encontrado.", 404);
        a.Estado = "Anulado";
        a.AnuladoMotivo = motivo;
        a.AnuladoPorWorkerId = workerId;
        a.AnuladoEn = DateTime.UtcNow;
        a.UpdatedAt = DateTime.UtcNow;
        await ctx.SaveChangesAsync();
    }

    public async Task<int> AnularGrupo(int atsGrupoId, string motivo, int workerId)
    {
        using var ctx = _factory.CreateDbContext();
        var g = await ctx.SsAtsGrupo.FirstOrDefaultAsync(x => x.Id == atsGrupoId) ?? throw new AbrilException("ATS grupal no encontrado.", 404);
        var ahora = DateTime.UtcNow;
        g.Estado = "Anulado";
        g.AnuladoMotivo = motivo;
        g.AnuladoPorWorkerId = workerId;
        g.AnuladoEn = ahora;
        g.UpdatedAt = ahora;

        var ats = await ctx.SsAts.Where(a => a.AtsGrupoId == atsGrupoId && a.Estado != "Anulado").ToListAsync();
        foreach (var a in ats)
        {
            a.Estado = "Anulado";
            a.AnuladoMotivo = motivo;
            a.AnuladoPorWorkerId = workerId;
            a.AnuladoEn = ahora;
            a.UpdatedAt = ahora;
        }
        await ctx.SaveChangesAsync();
        return ats.Count;
    }

    public async Task<HashSet<int>> GetWorkerIdsAdheridos(int atsGrupoId)
    {
        using var ctx = _factory.CreateDbContext();
        return (await ctx.SsAts.AsNoTracking()
            .Where(a => a.AtsGrupoId == atsGrupoId && a.Estado == "Firmado")
            .Select(a => a.WorkerId).ToListAsync()).ToHashSet();
    }

    public async Task<int?> GetAtsIdDeGrupo(int atsGrupoId, int workerId, string estado)
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.SsAts.AsNoTracking()
            .Where(a => a.AtsGrupoId == atsGrupoId && a.WorkerId == workerId && a.Estado == estado)
            .OrderByDescending(a => a.Id)
            .Select(a => (int?)a.Id)
            .FirstOrDefaultAsync();
    }

    public async Task<bool> GrupoTieneValidaciones(int atsGrupoId)
    {
        using var ctx = _factory.CreateDbContext();
        if (await ctx.SsAtsGrupo.AnyAsync(g => g.Id == atsGrupoId && g.CapatazFirmaUrl != null)) return true;
        return await ctx.SsAts.AnyAsync(a => a.AtsGrupoId == atsGrupoId && (a.AutorizaFirmaUrl != null || a.SsomaFirmaUrl != null));
    }

    public async Task ReabrirGrupo(int atsGrupoId, DateTime nuevaExpiracionUtc)
    {
        using var ctx = _factory.CreateDbContext();
        var g = await ctx.SsAtsGrupo.FirstOrDefaultAsync(x => x.Id == atsGrupoId) ?? throw new AbrilException("ATS grupal no encontrado.", 404);
        g.Estado = "Activo";
        g.QrExpiraEn = nuevaExpiracionUtc;
        g.UpdatedAt = DateTime.UtcNow;
        await ctx.SaveChangesAsync();
    }

    public async Task AgregarEventoGrupo(int atsGrupoId, string evento, int? workerId, string? detalle)
    {
        using var ctx = _factory.CreateDbContext();
        ctx.SsAtsGrupoEvento.Add(new SsAtsGrupoEvento { AtsGrupoId = atsGrupoId, Evento = evento, WorkerId = workerId, Detalle = detalle, CreatedAt = DateTime.UtcNow });
        await ctx.SaveChangesAsync();
    }

    public async Task<string?> GetUltimoEventoGrupo(int atsGrupoId)
    {
        using var ctx = _factory.CreateDbContext();
        var ev = await ctx.SsAtsGrupoEvento.AsNoTracking()
            .Where(e => e.AtsGrupoId == atsGrupoId)
            .OrderByDescending(e => e.CreatedAt).FirstOrDefaultAsync();
        if (ev is null) return null;
        var quien = ev.WorkerId.HasValue
            ? await ctx.Worker.AsNoTracking().Where(w => w.Id == ev.WorkerId).Select(w => w.Person!.FullName).FirstOrDefaultAsync()
            : null;
        var peru = new DateTimeOffset(DateTime.SpecifyKind(ev.CreatedAt, DateTimeKind.Utc)).ToOffset(TimeSpan.FromHours(-5));
        return $"{ev.Evento} por {quien ?? "—"} el {peru:dd/MM/yyyy HH:mm}";
    }

    public async Task<List<AtsGrupoIntegranteDto>> GetIntegrantes(int atsGrupoId)
    {
        using var ctx = _factory.CreateDbContext();
        var ids = await ctx.SsAtsGrupoIntegrante.AsNoTracking().Where(i => i.AtsGrupoId == atsGrupoId).Select(i => i.WorkerId).ToListAsync();
        if (ids.Count == 0) return [];
        var adheridos = await GetWorkerIdsAdheridos(atsGrupoId);
        var nombres = await ctx.Worker.AsNoTracking().Where(w => ids.Contains(w.Id))
            .Select(w => new { w.Id, Nombre = w.Person != null ? w.Person.FullName : "—" }).ToListAsync();
        return nombres
            .Select(n => new AtsGrupoIntegranteDto { WorkerId = n.Id, Nombre = n.Nombre, Adherido = adheridos.Contains(n.Id) })
            .OrderBy(n => n.Adherido).ThenBy(n => n.Nombre)
            .ToList();
    }

    public async Task<int?> GetGrupoIdPorClientId(Guid clientId)
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.SsAtsGrupo.AsNoTracking().Where(g => g.ClientId == clientId).Select(g => (int?)g.Id).FirstOrDefaultAsync();
    }

    public async Task MarcarGrupoOffline(int atsGrupoId, Guid clientId, DateTime capturadoEnUtc)
    {
        using var ctx = _factory.CreateDbContext();
        var g = await ctx.SsAtsGrupo.FirstOrDefaultAsync(x => x.Id == atsGrupoId) ?? throw new AbrilException("ATS grupal no encontrado.", 404);
        g.OrigenOffline = true;
        g.ClientId = clientId;
        g.CapturadoEn = capturadoEnUtc;
        // La jornada es la del día en que se trabajó (Perú, UTC-5), no la del día de la sincronización.
        g.Fecha = DateOnly.FromDateTime(capturadoEnUtc.AddHours(-5));
        g.QrExpiraEn = capturadoEnUtc.AddHours(72);
        g.UpdatedAt = DateTime.UtcNow;
        await ctx.SaveChangesAsync();
    }

    public async Task AgregarIntegrante(int atsGrupoId, int workerId)
    {
        using var ctx = _factory.CreateDbContext();
        if (await ctx.SsAtsGrupoIntegrante.AnyAsync(i => i.AtsGrupoId == atsGrupoId && i.WorkerId == workerId)) return;
        ctx.SsAtsGrupoIntegrante.Add(new SsAtsGrupoIntegrante { AtsGrupoId = atsGrupoId, WorkerId = workerId });
        await ctx.SaveChangesAsync();
    }

    public async Task SetIntegrantes(int atsGrupoId, List<int> workerIds)
    {
        using var ctx = _factory.CreateDbContext();
        var nuevos = workerIds.Distinct().ToHashSet();
        var actuales = await ctx.SsAtsGrupoIntegrante.Where(i => i.AtsGrupoId == atsGrupoId).ToListAsync();
        ctx.SsAtsGrupoIntegrante.RemoveRange(actuales.Where(a => !nuevos.Contains(a.WorkerId)));
        var existentes = actuales.Select(a => a.WorkerId).ToHashSet();
        foreach (var id in nuevos.Where(id => !existentes.Contains(id)))
            ctx.SsAtsGrupoIntegrante.Add(new SsAtsGrupoIntegrante { AtsGrupoId = atsGrupoId, WorkerId = id });
        await ctx.SaveChangesAsync();
    }

    public async Task<List<AtsGrupoPdfAdhesionDto>> GetAdhesionesParaPdf(int atsGrupoId)
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.SsAts.AsNoTracking()
            .Where(a => a.AtsGrupoId == atsGrupoId && a.Estado == "Firmado")
            .OrderBy(a => a.Worker!.Person!.FullName)
            .Select(a => new AtsGrupoPdfAdhesionDto
            {
                AtsId = a.Id,
                Nombre = a.Worker != null && a.Worker.Person != null ? a.Worker.Person.FullName : "—",
                Puesto = a.Puesto != null ? a.Puesto.Nombre : null,
                HoraServidorFirma = a.HoraServidorFirma,
                OrigenOffline = a.OrigenOffline,
                HoraDispositivo = a.HoraDispositivo,
                SelfieUrl = a.SelfieUrl,
                FirmaUrl = a.FirmaUrl,
                Lat = a.Lat,
                Lng = a.Lng,
                AutorizaNombre = a.AutorizaNombre, AutorizaCargo = a.AutorizaCargo,
                AutorizaFirmaUrl = a.AutorizaFirmaUrl, AutorizaHoraServidor = a.AutorizaHoraServidor,
                SsomaNombre = a.SsomaNombre, SsomaCargo = a.SsomaCargo,
                SsomaFirmaUrl = a.SsomaFirmaUrl, SsomaHoraServidor = a.SsomaHoraServidor,
            })
            .ToListAsync();
    }

    public async Task<bool> EsAutorDeGrupo(int atsGrupoId, int workerId)
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.SsAtsGrupo.AnyAsync(g => g.Id == atsGrupoId && g.CreadoPorWorkerId == workerId);
    }

    public async Task CerrarGrupo(int atsGrupoId)
    {
        using var ctx = _factory.CreateDbContext();
        var grupo = await ctx.SsAtsGrupo.FirstOrDefaultAsync(g => g.Id == atsGrupoId) ?? throw new AbrilException("ATS grupal no encontrado.", 404);
        grupo.Estado = "Cerrado";
        grupo.UpdatedAt = DateTime.UtcNow;
        await ctx.SaveChangesAsync();
    }

    /// <summary>Acotado al proyecto del grupo y a trabajadores Activos que ya tienen la
    /// autorización de firma digital — no tiene sentido ofrecerle la opción a alguien que de
    /// todas formas el backend va a rechazar al intentar firmar (ExigirAutorizacionPermiso).</summary>
    public async Task<List<AtsGrupoWorkerOpcionDto>> GetWorkersParaAdhesion(int proyectoId)
    {
        using var ctx = _factory.CreateDbContext();

        var workerIdsDelProyecto = await ctx.WorkerVinculacion
            .Where(v => v.ProyectoId == proyectoId && v.FechaFin == null)
            .Select(v => v.WorkerId)
            .ToListAsync();

        var workers = await ctx.Worker
            .Where(w => workerIdsDelProyecto.Contains(w.Id)
                && w.WorkersEstadoId == Abril_Backend.Shared.Constants.WorkersEstadoIds.Activo)
            .Include(w => w.Person)
            .ToListAsync();

        var workerIds = workers.Select(w => w.Id).ToList();
        var autorizados = await ctx.SsAtsAutorizacionPermiso
            .Where(a => workerIds.Contains(a.WorkerId) && a.ArchivoUrl != null)
            .Select(a => a.WorkerId)
            .ToHashSetAsync();

        return workers
            .Where(w => autorizados.Contains(w.Id))
            .Select(w => new AtsGrupoWorkerOpcionDto
            {
                WorkerId = w.Id,
                Nombre = w.Person?.FullName ?? string.Empty,
                DniUltimos4 = w.Person?.DocumentIdentityCode is { Length: >= 4 } dni ? dni[^4..] : null,
            })
            .OrderBy(w => w.Nombre)
            .ToList();
    }

    public async Task<bool> DniCoincide(int workerId, string ultimosDigitos)
    {
        if (string.IsNullOrWhiteSpace(ultimosDigitos)) return false;
        using var ctx = _factory.CreateDbContext();
        var worker = await ctx.Worker.Include(w => w.Person).FirstOrDefaultAsync(w => w.Id == workerId);
        var dni = worker?.Person?.DocumentIdentityCode;
        if (string.IsNullOrEmpty(dni) || dni.Length < ultimosDigitos.Length) return false;
        return dni[^ultimosDigitos.Length..] == ultimosDigitos.Trim();
    }

    /// <summary>QR fijo por proyecto para crear ATS Grupales sin login — idempotente: si el
    /// proyecto ya tiene uno, lo devuelve en vez de generar otro (un solo QR por proyecto, se
    /// imprime una vez).</summary>
    public async Task<Guid> GetOrCrearTokenProyecto(int proyectoId)
    {
        using var ctx = _factory.CreateDbContext();
        var existente = await ctx.SsAtsProyectoQr.FirstOrDefaultAsync(q => q.ProyectoId == proyectoId);
        if (existente != null) return existente.Token;

        var nuevo = new SsAtsProyectoQr { ProyectoId = proyectoId };
        ctx.SsAtsProyectoQr.Add(nuevo);
        await ctx.SaveChangesAsync();
        return nuevo.Token;
    }

    public async Task<int?> GetProyectoPorTokenCrear(Guid token)
    {
        using var ctx = _factory.CreateDbContext();
        return (await ctx.SsAtsProyectoQr.FirstOrDefaultAsync(q => q.Token == token))?.ProyectoId;
    }

    /// <summary>Capataces/Maestros de obra activos vinculados HOY al proyecto — los únicos que
    /// la página pública del Capataz deja elegir como "yo soy".</summary>
    public async Task<List<AtsGrupoWorkerOpcionDto>> GetCapatacesDeProyecto(int proyectoId)
    {
        using var ctx = _factory.CreateDbContext();

        var vinculaciones = await ctx.WorkerVinculacion
            .Include(v => v.PuestoCatalogo)
            .Where(v => v.ProyectoId == proyectoId && v.FechaFin == null)
            .ToListAsync();

        var workerIds = vinculaciones
            .Where(v => v.PuestoCatalogo != null
                && (v.PuestoCatalogo.Nombre.Contains("Capataz", StringComparison.OrdinalIgnoreCase)
                    || v.PuestoCatalogo.Nombre.Contains("Maestro de Obra", StringComparison.OrdinalIgnoreCase)))
            .Select(v => v.WorkerId)
            .Distinct()
            .ToList();

        var workers = await ctx.Worker
            .Where(w => workerIds.Contains(w.Id) && w.WorkersEstadoId == Abril_Backend.Shared.Constants.WorkersEstadoIds.Activo)
            .Include(w => w.Person)
            .ToListAsync();

        return workers
            .Select(w => new AtsGrupoWorkerOpcionDto
            {
                WorkerId = w.Id,
                Nombre = w.Person?.FullName ?? string.Empty,
                DniUltimos4 = w.Person?.DocumentIdentityCode is { Length: >= 4 } dni ? dni[^4..] : null,
            })
            .OrderBy(w => w.Nombre)
            .ToList();
    }

    /// <summary>Firma única del Capataz sobre el grupo + copia a los SsAts adheridos que todavía no
    /// tienen firma de Capataz. Los que ya la tenían (firma anterior, antes de que se sumaran
    /// nuevos) NO se reescriben: conservan su firma y hora originales.</summary>
    public async Task FirmarCapatazGrupo(int grupoId, int workerId, string nombre, string? cargo, string firmaUrl, string firmaHash, DateTime horaServidor,
        string? selfieUrl, string? selfieHash, decimal? lat, decimal? lng, decimal? precisionMetros, DateTime? horaDispositivo)
    {
        using var ctx = _factory.CreateDbContext();
        var grupo = await ctx.SsAtsGrupo.FirstOrDefaultAsync(g => g.Id == grupoId) ?? throw new AbrilException("ATS grupal no encontrado.", 404);

        var adheridos = await ctx.SsAts.Where(a => a.AtsGrupoId == grupoId && a.Estado == "Firmado").ToListAsync();

        grupo.CapatazWorkerId = workerId;
        grupo.CapatazNombre = nombre;
        grupo.CapatazCargo = cargo;
        grupo.CapatazFirmaUrl = firmaUrl;
        grupo.CapatazFirmaHash = firmaHash;
        grupo.CapatazHoraServidor = horaServidor;
        grupo.CapatazAdhesionesAlFirmar = adheridos.Count;
        grupo.CapatazSelfieUrl = selfieUrl;
        grupo.CapatazSelfieHash = selfieHash;
        grupo.CapatazLat = lat;
        grupo.CapatazLng = lng;
        grupo.CapatazPrecisionMetros = precisionMetros;
        grupo.CapatazHoraDispositivo = horaDispositivo;
        grupo.UpdatedAt = DateTime.UtcNow;

        foreach (var ats in adheridos.Where(a => a.CapatazFirmaUrl == null))
        {
            ats.CapatazWorkerId = workerId;
            ats.CapatazNombre = nombre;
            ats.CapatazCargo = cargo;
            ats.CapatazFirmaUrl = firmaUrl;
            ats.CapatazFirmaHash = firmaHash;
            ats.CapatazHoraServidor = horaServidor;
            ats.UpdatedAt = DateTime.UtcNow;
        }
        await ctx.SaveChangesAsync();

        foreach (var ats in adheridos.Where(a => a.CapatazHoraServidor == horaServidor))
        {
            var hashAnterior = await ctx.SsAtsAuditLog.Where(l => l.AtsId == ats.Id).OrderByDescending(l => l.Id).Select(l => l.Hash).FirstOrDefaultAsync();
            var hash = ComputeHash(hashAnterior, "FirmadoCapatazGrupo", ats.Id, workerId, firmaHash);
            ctx.SsAtsAuditLog.Add(new SsAtsAuditLog { AtsId = ats.Id, Evento = "FirmadoCapatazGrupo", UserId = null, Detalle = $"firma_hash={firmaHash}; ats_grupo_id={grupoId}", HashAnterior = hashAnterior, Hash = hash });
        }
        await ctx.SaveChangesAsync();
    }

    public async Task<string?> GetProyectoNombre(int proyectoId)
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.Project.Where(p => p.ProjectId == proyectoId).Select(p => p.ProjectDescription).FirstOrDefaultAsync();
    }

    public async Task<int> CrearDesdeGrupo(int workerId, SsAtsGrupo grupo)
    {
        using var ctx = _factory.CreateDbContext();
        var worker = await ctx.Worker.FirstOrDefaultAsync(w => w.Id == workerId);

        var ats = new SsAts
        {
            Codigo = await SiguienteCodigoAts(ctx, grupo.ProyectoId),
            WorkerId = workerId,
            ProyectoId = grupo.ProyectoId,
            PuestoId = worker?.PuestoId,
            PlantillaId = grupo.PlantillaId,
            Actividad = grupo.Actividad,
            TorreNombre = grupo.TorreNombre,
            Pisos = grupo.Pisos,
            Lugar = grupo.Lugar,
            Fecha = grupo.Fecha,
            Estado = "Borrador",
            OrigenOffline = grupo.OrigenOffline,
            AtsGrupoId = grupo.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        short ordenPaso = 0;
        foreach (var p in grupo.Pasos)
        {
            ats.Pasos.Add(new SsAtsPasoSeleccionado
            {
                PasoId = p.PasoId,
                CategoriaNombre = p.CategoriaNombre,
                Texto = p.Texto,
                Aplica = p.Aplica,
                Orden = ordenPaso++,
            });
        }
        foreach (var e in grupo.Epps)
            ats.Epps.Add(new SsAtsEppSeleccionado { EppId = e.EppId, Nombre = e.Nombre });
        foreach (var h in grupo.Herramientas)
            ats.Herramientas.Add(new SsAtsHerramientaSeleccionada { HerramientaId = h.HerramientaId, Nombre = h.Nombre });

        short ordenRiesgo = 0;
        foreach (var r in grupo.RiesgosDetalle)
        {
            ats.RiesgosDetalle.Add(new SsAtsRiesgoDetalle
            {
                PeligroId = r.PeligroId,
                RiesgoId = r.RiesgoId,
                PeligroNombre = r.PeligroNombre,
                RiesgoNombre = r.RiesgoNombre,
                RiesgoBase = r.RiesgoBase,
                Controles = r.Controles,
                RiesgoResidual = r.RiesgoResidual,
                Orden = ordenRiesgo++,
            });
        }

        ctx.SsAts.Add(ats);
        await ctx.SaveChangesAsync();

        var hash = ComputeHash(null, "CreadoDesdeGrupo", ats.Id, workerId, $"ats_grupo_id={grupo.Id}");
        ctx.SsAtsAuditLog.Add(new SsAtsAuditLog
        {
            AtsId = ats.Id,
            Evento = "CreadoDesdeGrupo",
            Detalle = $"ats_grupo_id={grupo.Id}",
            HashAnterior = null,
            Hash = hash,
        });
        await ctx.SaveChangesAsync();

        return ats.Id;
    }
}

/// <summary>Primera fila de <see cref="AtsRepository.GetInit"/>: puesto y proyecto actual del trabajador.</summary>
internal sealed class AtsInitTrabajadorFila
{
    public int? PuestoId { get; set; }
    public int? ProyectoActualId { get; set; }
}

/// <summary>Un paso con su categoría, antes de agruparlos en <see cref="AtsRepository.GetInit"/>.</summary>
internal sealed class AtsPasoFila
{
    public int CategoriaId { get; set; }
    public string CategoriaNombre { get; set; } = string.Empty;
    public int Id { get; set; }
    public string Texto { get; set; } = string.Empty;
    public bool RequierePetar { get; set; }
}

/// <summary>Un riesgo con su peligro, antes de agruparlos por peligro.</summary>
internal sealed class AtsRiesgoFila
{
    public int PeligroId { get; set; }
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public bool RequierePetar { get; set; }
}

/// <summary>Un peligro, EPP o herramienta de una plantilla, antes de agruparlos por plantilla.</summary>
internal sealed class AtsPlantillaItemFila
{
    public int PlantillaId { get; set; }
    public int Id { get; set; }
}
