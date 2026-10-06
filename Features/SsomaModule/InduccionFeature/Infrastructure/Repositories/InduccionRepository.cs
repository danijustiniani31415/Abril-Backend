using System.Security.Cryptography;
using System.Text;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.SsomaModule.InduccionFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.InduccionFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.SsomaModule.InduccionFeature.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.SsomaModule.InduccionFeature.Infrastructure.Repositories;

public class InduccionRepository : IInduccionRepository
{
    private const int PageSize = 25;
    private readonly IDbContextFactory<AppDbContext> _factory;

    public InduccionRepository(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    // ── Catálogo de documentos ──────────────────────────────────────────────

    public async Task<List<SsIndDocumento>> ListarDocumentos(bool soloActivos)
    {
        using var ctx = _factory.CreateDbContext();
        var q = ctx.SsIndDocumento.AsNoTracking().Include(d => d.Versiones.Where(v => v.Vigente)).AsQueryable();
        if (soloActivos) q = q.Where(d => d.Activo);
        return await q.OrderBy(d => d.Orden).ThenBy(d => d.Id).ToListAsync();
    }

    public async Task<SsIndDocumento> GuardarDocumento(IndDocumentoGuardarDto dto)
    {
        using var ctx = _factory.CreateDbContext();
        var codigo = dto.Codigo.Trim();
        if (await ctx.SsIndDocumento.AnyAsync(d => d.Codigo == codigo && d.Id != (dto.Id ?? 0)))
            throw new AbrilException("Ya existe un documento con ese código.", 409);

        SsIndDocumento d;
        if (dto.Id is int id)
        {
            d = await ctx.SsIndDocumento.FirstOrDefaultAsync(x => x.Id == id)
                ?? throw new AbrilException("Documento no encontrado.", 404);
        }
        else
        {
            d = new SsIndDocumento();
            ctx.SsIndDocumento.Add(d);
        }
        d.Codigo = codigo;
        d.Nombre = dto.Nombre.Trim();
        d.Descripcion = dto.Descripcion?.Trim();
        d.Tipo = dto.Tipo;
        d.Obligatorio = dto.Obligatorio;
        d.Orden = dto.Orden;
        d.Activo = dto.Activo;
        d.UpdatedAt = DateTime.UtcNow;
        await ctx.SaveChangesAsync();
        return d;
    }

    public async Task<SsIndDocumentoVersion> PublicarVersion(int documentoId, string archivoUrl, string archivoHash,
        bool esCambioMaterial, string? nota, int? userId)
    {
        using var ctx = _factory.CreateDbContext();
        if (!await ctx.SsIndDocumento.AnyAsync(d => d.Id == documentoId))
            throw new AbrilException("Documento no encontrado.", 404);

        await using var tx = await ctx.Database.BeginTransactionAsync();
        var anteriores = await ctx.SsIndDocumentoVersion.Where(v => v.DocumentoId == documentoId).ToListAsync();
        foreach (var a in anteriores.Where(a => a.Vigente)) a.Vigente = false;
        await ctx.SaveChangesAsync(); // libera el índice único de "vigente" antes de insertar la nueva

        var nueva = new SsIndDocumentoVersion
        {
            DocumentoId = documentoId,
            Version = (anteriores.Count == 0 ? 0 : anteriores.Max(a => a.Version)) + 1,
            ArchivoUrl = archivoUrl,
            ArchivoHash = archivoHash,
            EsCambioMaterial = esCambioMaterial,
            NotaCambio = nota,
            Vigente = true,
            PublicadoPorUserId = userId,
            PublicadoEn = DateTime.UtcNow,
        };
        ctx.SsIndDocumentoVersion.Add(nueva);
        await ctx.SaveChangesAsync();
        await tx.CommitAsync();
        return nueva;
    }

    public async Task<List<SsIndDocumentoVersion>> ListarVersiones(int documentoId)
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.SsIndDocumentoVersion.AsNoTracking()
            .Where(v => v.DocumentoId == documentoId).OrderByDescending(v => v.Version).ToListAsync();
    }

    // ── Temario ─────────────────────────────────────────────────────────────

    public async Task<List<SsIndTema>> ListarTemas(bool soloActivos)
    {
        using var ctx = _factory.CreateDbContext();
        var q = ctx.SsIndTema.AsNoTracking().AsQueryable();
        if (soloActivos) q = q.Where(t => t.Activo);
        return await q.OrderBy(t => t.Orden).ToListAsync();
    }

    public async Task<SsIndTema> GuardarTema(IndTemaGuardarDto dto)
    {
        using var ctx = _factory.CreateDbContext();
        SsIndTema t;
        if (dto.Id is int id)
        {
            t = await ctx.SsIndTema.FirstOrDefaultAsync(x => x.Id == id) ?? throw new AbrilException("Tema no encontrado.", 404);
        }
        else
        {
            t = new SsIndTema();
            ctx.SsIndTema.Add(t);
        }
        t.Orden = dto.Orden;
        t.Descripcion = dto.Descripcion.Trim();
        t.Activo = dto.Activo;
        await ctx.SaveChangesAsync();
        return t;
    }

    // ── Banco de preguntas y evaluación publicada ───────────────────────────

    public async Task<List<SsIndPregunta>> ListarPreguntas(bool soloActivas)
    {
        using var ctx = _factory.CreateDbContext();
        var q = ctx.SsIndPregunta.AsNoTracking().Include(p => p.Opciones).AsQueryable();
        if (soloActivas) q = q.Where(p => p.Activo);
        return await q.OrderBy(p => p.Orden).ThenBy(p => p.Id).ToListAsync();
    }

    public async Task<SsIndPregunta> GuardarPregunta(IndPreguntaGuardarDto dto)
    {
        using var ctx = _factory.CreateDbContext();
        SsIndPregunta p;
        if (dto.Id is int id)
        {
            p = await ctx.SsIndPregunta.Include(x => x.Opciones).FirstOrDefaultAsync(x => x.Id == id)
                ?? throw new AbrilException("Pregunta no encontrada.", 404);
            ctx.SsIndPreguntaOpcion.RemoveRange(p.Opciones);
            p.Opciones.Clear();
        }
        else
        {
            p = new SsIndPregunta();
            ctx.SsIndPregunta.Add(p);
        }
        p.Tipo = dto.Tipo;
        p.Enunciado = dto.Enunciado.Trim();
        p.Puntos = dto.Puntos;
        p.Orden = dto.Orden;
        p.Activo = dto.Activo;
        var orden = 0;
        foreach (var o in dto.Opciones)
            p.Opciones.Add(new SsIndPreguntaOpcion
            {
                Texto = o.Texto.Trim(),
                EsCorrecta = o.EsCorrecta,
                ParTexto = o.ParTexto?.Trim(),
                Orden = o.Orden != 0 ? o.Orden : ++orden,
            });
        await ctx.SaveChangesAsync();
        return p;
    }

    public async Task<SsIndEvaluacionVersion> PublicarEvaluacion(string contenidoJson, string contenidoHash,
        decimal notaMinima, decimal notaMaxima, int duracionMin, int? userId)
    {
        using var ctx = _factory.CreateDbContext();
        await using var tx = await ctx.Database.BeginTransactionAsync();
        var anteriores = await ctx.SsIndEvaluacionVersion.ToListAsync();
        foreach (var a in anteriores.Where(a => a.Vigente)) a.Vigente = false;
        await ctx.SaveChangesAsync();

        var nueva = new SsIndEvaluacionVersion
        {
            Version = (anteriores.Count == 0 ? 0 : anteriores.Max(a => a.Version)) + 1,
            NotaMinima = notaMinima,
            NotaMaxima = notaMaxima,
            DuracionMin = duracionMin,
            ContenidoJson = contenidoJson,
            ContenidoHash = contenidoHash,
            Vigente = true,
            PublicadoPorUserId = userId,
            PublicadoEn = DateTime.UtcNow,
        };
        ctx.SsIndEvaluacionVersion.Add(nueva);
        await ctx.SaveChangesAsync();
        await tx.CommitAsync();
        return nueva;
    }

    public async Task<SsIndEvaluacionVersion?> GetEvaluacionVigente()
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.SsIndEvaluacionVersion.AsNoTracking().FirstOrDefaultAsync(e => e.Vigente);
    }

    public async Task<SsIndEvaluacionVersion?> GetEvaluacionVersion(int id)
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.SsIndEvaluacionVersion.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id);
    }

    public async Task<List<SsIndEvaluacionVersion>> ListarEvaluaciones()
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.SsIndEvaluacionVersion.AsNoTracking().OrderByDescending(e => e.Version).ToListAsync();
    }

    // ── Sesión ──────────────────────────────────────────────────────────────

    public async Task<SsIndSesion> CrearSesion(SsIndSesion sesion, List<int> documentoVersionIds, List<SsIndTema> temas,
        List<int> workerIdsEsperados, int? userId, string? ip)
    {
        using var ctx = _factory.CreateDbContext();
        await using var tx = await ctx.Database.BeginTransactionAsync();

        sesion.Codigo = await SiguienteCodigo(ctx, sesion.ProyectoId, "IND");
        foreach (var dvId in documentoVersionIds.Distinct())
            sesion.Documentos.Add(new SsIndSesionDocumento { DocumentoVersionId = dvId });
        foreach (var t in temas)
            sesion.Temas.Add(new SsIndSesionTema { TemaId = t.Id, Orden = t.Orden, Descripcion = t.Descripcion });

        ctx.SsIndSesion.Add(sesion);
        await ctx.SaveChangesAsync();

        foreach (var wid in workerIdsEsperados.Distinct())
            ctx.SsIndAsistencia.Add(await NuevaAsistencia(ctx, sesion.Id, wid));

        await AddAudit(ctx, sesion.Id, "Creada", userId, ip, $"codigo={sesion.Codigo};docs={documentoVersionIds.Count};esperados={workerIdsEsperados.Count}");
        await ctx.SaveChangesAsync();
        await tx.CommitAsync();
        return sesion;
    }

    public async Task<SsIndSesion?> GetSesion(int id)
    {
        using var ctx = _factory.CreateDbContext();
        return await ConIncludes(ctx).FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<SsIndSesion?> GetSesionPorToken(Guid token)
    {
        using var ctx = _factory.CreateDbContext();
        return await ConIncludes(ctx).FirstOrDefaultAsync(s => s.QrToken == token);
    }

    private static IQueryable<SsIndSesion> ConIncludes(AppDbContext ctx) => ctx.SsIndSesion.AsNoTracking()
        .AsSplitQuery()
        .Include(s => s.Proyecto)
        .Include(s => s.EvaluacionVersion)
        .Include(s => s.Documentos).ThenInclude(d => d.DocumentoVersion).ThenInclude(v => v!.Documento)
        .Include(s => s.Temas)
        .Include(s => s.Asistencias).ThenInclude(a => a.Documentos);

    public async Task<IndSesionListaDto> ListarSesiones(IndSesionFiltroDto filtro)
    {
        using var ctx = _factory.CreateDbContext();
        var q = ctx.SsIndSesion.AsNoTracking().AsQueryable();
        if (filtro.ProyectoId.HasValue) q = q.Where(s => s.ProyectoId == filtro.ProyectoId);
        if (filtro.FechaDesde.HasValue) q = q.Where(s => s.Fecha >= filtro.FechaDesde);
        if (filtro.FechaHasta.HasValue) q = q.Where(s => s.Fecha <= filtro.FechaHasta);
        if (!string.IsNullOrWhiteSpace(filtro.Estado)) q = q.Where(s => s.Estado == filtro.Estado);

        var total = await q.CountAsync();
        var page = Math.Max(1, filtro.Page);
        var items = await q.OrderByDescending(s => s.Fecha).ThenByDescending(s => s.Id)
            .Skip((page - 1) * PageSize).Take(PageSize)
            .Select(s => new IndSesionListaItemDto
            {
                Id = s.Id,
                Codigo = s.Codigo,
                ProyectoId = s.ProyectoId,
                ProyectoNombre = s.Proyecto!.ProjectDescription,
                Fecha = s.Fecha,
                Tipo = s.Tipo,
                Estado = s.Estado,
                InstructorNombre = s.InstructorNombre,
                Firmados = s.Asistencias.Count(a => a.Estado == "Firmado"),
                Esperados = s.Asistencias.Count(),
            }).ToListAsync();
        return new IndSesionListaDto { Items = items, Total = total, Page = page, PageSize = PageSize };
    }

    public async Task<string?> GetProyectoNombre(int proyectoId)
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.Project.Where(p => p.ProjectId == proyectoId).Select(p => p.ProjectDescription).FirstOrDefaultAsync();
    }

    /// <summary>Trabajadores activos con vinculación vigente al proyecto. A diferencia del ATS NO exige la
    /// autorización de firma digital: un trabajador nuevo se induce ANTES de tener ese permiso.</summary>
    public async Task<List<IndWorkerOpcionDto>> GetWorkersParaInduccion(int proyectoId)
    {
        using var ctx = _factory.CreateDbContext();
        var ids = await ctx.WorkerVinculacion
            .Where(v => v.ProyectoId == proyectoId && v.FechaFin == null)
            .Select(v => v.WorkerId).ToListAsync();

        var workers = await ctx.Worker.AsNoTracking()
            .Where(w => ids.Contains(w.Id) && w.WorkersEstadoId == Abril_Backend.Shared.Constants.WorkersEstadoIds.Activo)
            .Include(w => w.Person).ToListAsync();

        return workers
            .Select(w => new IndWorkerOpcionDto { WorkerId = w.Id, Nombre = ApellidosNombres(w.Person) })
            .OrderBy(w => w.Nombre).ToList();
    }

    // ── Trabajador ──────────────────────────────────────────────────────────

    public async Task<SsIndAsistencia?> GetAsistencia(int sesionId, int workerId)
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.SsIndAsistencia.AsNoTracking().Include(a => a.Documentos)
            .FirstOrDefaultAsync(a => a.SesionId == sesionId && a.WorkerId == workerId);
    }

    public async Task<SsIndAsistencia> GetOCrearAsistencia(int sesionId, int workerId)
    {
        using var ctx = _factory.CreateDbContext();
        var existente = await ctx.SsIndAsistencia.AsNoTracking().FirstOrDefaultAsync(a => a.SesionId == sesionId && a.WorkerId == workerId);
        if (existente != null) return existente;

        var nueva = await NuevaAsistencia(ctx, sesionId, workerId);
        ctx.SsIndAsistencia.Add(nueva);
        await ctx.SaveChangesAsync();
        return nueva;
    }

    public async Task RegistrarDocumentoRecibido(int asistenciaId, int documentoVersionId, int? segundosAbierto)
    {
        using var ctx = _factory.CreateDbContext();
        var fila = await ctx.SsIndAsistenciaDocumento
            .FirstOrDefaultAsync(d => d.AsistenciaId == asistenciaId && d.DocumentoVersionId == documentoVersionId);
        if (fila == null)
        {
            ctx.SsIndAsistenciaDocumento.Add(new SsIndAsistenciaDocumento
            {
                AsistenciaId = asistenciaId,
                DocumentoVersionId = documentoVersionId,
                SegundosAbierto = segundosAbierto,
            });
        }
        else if (segundosAbierto.HasValue && segundosAbierto > (fila.SegundosAbierto ?? 0))
        {
            fila.SegundosAbierto = segundosAbierto; // la fecha de recibido original no se pisa
        }
        await ctx.SaveChangesAsync();
    }

    public async Task<List<int>> GetDocumentosRecibidos(int asistenciaId)
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.SsIndAsistenciaDocumento.AsNoTracking()
            .Where(d => d.AsistenciaId == asistenciaId).Select(d => d.DocumentoVersionId).ToListAsync();
    }

    public async Task<SsIndIntento> GuardarIntento(int asistenciaId, int evaluacionVersionId, string respuestasJson,
        decimal nota, bool aprobado, DateTime? iniciadoEn)
    {
        using var ctx = _factory.CreateDbContext();
        var a = await ctx.SsIndAsistencia.FirstOrDefaultAsync(x => x.Id == asistenciaId)
            ?? throw new AbrilException("Asistencia no encontrada.", 404);

        var intento = new SsIndIntento
        {
            AsistenciaId = asistenciaId,
            EvaluacionVersionId = evaluacionVersionId,
            RespuestasJson = respuestasJson,
            Nota = nota,
            Aprobado = aprobado,
            IniciadoEn = iniciadoEn,
        };
        ctx.SsIndIntento.Add(intento);
        a.Intentos += 1;
        // Queda la mejor nota: un intento posterior más bajo no baja una nota ya aprobada.
        if (a.Nota == null || nota > a.Nota) a.Nota = nota;
        if (aprobado) a.Aprobado = true;
        await ctx.SaveChangesAsync();
        return intento;
    }

    public async Task Firmar(int asistenciaId, string selfieUrl, string selfieHash, string firmaUrl, string firmaHash,
        DateTime horaServidor, IndFirmarDto body, string? ip, string? userAgent, string declaracionVersion)
    {
        using var ctx = _factory.CreateDbContext();
        var a = await ctx.SsIndAsistencia.FirstOrDefaultAsync(x => x.Id == asistenciaId)
            ?? throw new AbrilException("Asistencia no encontrada.", 404);

        a.Estado = "Firmado";
        a.RechazoMotivo = null;
        a.ValidadoPorUserId = null;
        a.ValidadoEn = null;
        a.OrigenOffline = false; // la firma sin conexión no entra en Fase 1
        a.HoraServidorFirma = horaServidor;
        a.HoraDispositivo = body.HoraDispositivo?.ToUniversalTime();
        a.Lat = body.Lat;
        a.Lng = body.Lng;
        a.PrecisionMetros = body.PrecisionMetros;
        a.SelfieUrl = selfieUrl;
        a.SelfieHash = selfieHash;
        a.FirmaUrl = firmaUrl;
        a.FirmaHash = firmaHash;
        a.IpOrigen = ip;
        a.UserAgent = userAgent;
        a.DeclaracionVersion = declaracionVersion;
        a.ExperienciaActividad = string.IsNullOrWhiteSpace(body.ExperienciaActividad) ? a.ExperienciaActividad : body.ExperienciaActividad.Trim();
        // Fijo una sola vez: no incluye la hora actual, no se recalcula al re-exportar el acta.
        a.HashVerificacion = Hash($"{a.Id}|{a.SesionId}|{a.WorkerId}|{firmaHash}|{selfieHash}|{horaServidor:O}");

        await AddAudit(ctx, a.SesionId, "Firmado", null, ip,
            $"worker={a.WorkerId};lat={body.Lat};lng={body.Lng};precision={body.PrecisionMetros};firma_hash={firmaHash};selfie_hash={selfieHash};nota={a.Nota}");
        await ctx.SaveChangesAsync();
    }

    public async Task<bool> ExisteSelfieHash(string selfieHash)
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.SsIndAsistencia.AnyAsync(a => a.SelfieHash == selfieHash)
            || await ctx.SsAts.AnyAsync(a => a.SelfieHash == selfieHash);
    }

    // ── Instructor / cierre ─────────────────────────────────────────────────

    public async Task<HashSet<int>> GetAsistenciasConSelfieRepetida(int sesionId)
    {
        using var ctx = _factory.CreateDbContext();
        var ids = await ctx.SsIndAsistencia.AsNoTracking()
            .Where(a => a.SesionId == sesionId && a.SelfieHash != null
                && (ctx.SsIndAsistencia.Any(o => o.Id != a.Id && o.SelfieHash == a.SelfieHash)
                    || ctx.SsAts.Any(t => t.SelfieHash == a.SelfieHash)))
            .Select(a => a.Id).ToListAsync();
        return ids.ToHashSet();
    }

    public async Task CerrarSesion(int sesionId, string firmaUrl, string firmaHash, string actaPdfUrl, string actaPdfHash, string actaPdfSha256, int? userId, string? ip)
    {
        using var ctx = _factory.CreateDbContext();
        var s = await ctx.SsIndSesion.FirstOrDefaultAsync(x => x.Id == sesionId) ?? throw new AbrilException("Sesión no encontrada.", 404);
        if (s.Estado != "Abierta") throw new AbrilException("Esta sesión ya fue cerrada o anulada.", 409);

        var ahora = DateTime.UtcNow;
        s.InstructorFirmaUrl = firmaUrl;
        s.InstructorFirmaHash = firmaHash;
        s.InstructorHoraServidor = ahora;
        s.ActaPdfUrl = actaPdfUrl;
        s.ActaPdfHash = actaPdfHash;
        s.ActaPdfSha256 = actaPdfSha256;
        s.CerradaEn = ahora;
        s.Estado = "Cerrada";
        s.UpdatedAt = ahora;
        await AddAudit(ctx, sesionId, "Cerrada", userId, ip, $"firma_hash={firmaHash};acta_hash={actaPdfHash};acta_sha256={actaPdfSha256}");
        await ctx.SaveChangesAsync();
    }

    public async Task AnularSesion(int sesionId, string motivo, int? userId, string? ip)
    {
        using var ctx = _factory.CreateDbContext();
        var s = await ctx.SsIndSesion.FirstOrDefaultAsync(x => x.Id == sesionId) ?? throw new AbrilException("Sesión no encontrada.", 404);
        if (s.Estado == "Anulada") throw new AbrilException("La sesión ya está anulada.", 409);

        s.Estado = "Anulada"; // nunca se borra: queda el motivo, quién y cuándo
        s.AnuladoMotivo = motivo;
        s.AnuladoPorUserId = userId;
        s.AnuladoEn = DateTime.UtcNow;
        s.UpdatedAt = DateTime.UtcNow;
        await AddAudit(ctx, sesionId, "Anulada", userId, ip, motivo);
        await ctx.SaveChangesAsync();
    }

    public async Task ValidarAsistencia(int asistenciaId, int userId, string? ip)
    {
        using var ctx = _factory.CreateDbContext();
        var a = await ctx.SsIndAsistencia.FirstOrDefaultAsync(x => x.Id == asistenciaId) ?? throw new AbrilException("Asistencia no encontrada.", 404);
        a.ValidadoPorUserId = userId;
        a.ValidadoEn = DateTime.UtcNow;
        a.RechazoMotivo = null;
        await AddAudit(ctx, a.SesionId, "Validada", userId, ip, $"worker={a.WorkerId}");
        await ctx.SaveChangesAsync();
    }

    public async Task RechazarAsistencia(int asistenciaId, string motivo, int userId, string? ip)
    {
        using var ctx = _factory.CreateDbContext();
        var a = await ctx.SsIndAsistencia.FirstOrDefaultAsync(x => x.Id == asistenciaId) ?? throw new AbrilException("Asistencia no encontrada.", 404);
        a.Estado = "Rechazado"; // el trabajador puede volver a firmar mientras la sesión siga abierta
        a.RechazoMotivo = motivo;
        a.ValidadoPorUserId = userId;
        a.ValidadoEn = DateTime.UtcNow;
        await AddAudit(ctx, a.SesionId, "Rechazada", userId, ip, $"worker={a.WorkerId};motivo={motivo}");
        await ctx.SaveChangesAsync();
    }

    public async Task SetInstructorInfo(int sesionId, string? nombre, string? cargo)
    {
        using var ctx = _factory.CreateDbContext();
        var s = await ctx.SsIndSesion.FirstOrDefaultAsync(x => x.Id == sesionId);
        if (s == null) return;
        s.InstructorNombre = nombre;
        s.InstructorCargo = cargo;
        await ctx.SaveChangesAsync();
    }

    // ── Datos de apoyo ──────────────────────────────────────────────────────

    public async Task<(string? Nombre, string? Cargo)> GetNombreYCargo(int workerId)
    {
        using var ctx = _factory.CreateDbContext();
        var w = await ctx.Worker.AsNoTracking().Include(x => x.Person).Include(x => x.PuestoCatalogo)
            .FirstOrDefaultAsync(x => x.Id == workerId) ?? throw new AbrilException("Trabajador no encontrado.", 404);
        return (ApellidosNombres(w.Person), w.PuestoCatalogo?.Nombre);
    }

    // ── Auditoría ───────────────────────────────────────────────────────────

    public async Task AgregarAudit(int sesionId, string evento, int? userId, string? ip, string? detalle)
    {
        using var ctx = _factory.CreateDbContext();
        await AddAudit(ctx, sesionId, evento, userId, ip, detalle);
        await ctx.SaveChangesAsync();
    }

    /// <summary>Agrega (sin guardar) un evento encadenado al último hash de la sesión.</summary>
    private static async Task AddAudit(AppDbContext ctx, int sesionId, string evento, int? userId, string? ip, string? detalle)
    {
        var anterior = await ctx.SsIndAuditLog.Where(l => l.SesionId == sesionId)
            .OrderByDescending(l => l.Id).Select(l => l.Hash).FirstOrDefaultAsync();
        var ahora = DateTime.UtcNow;
        ctx.SsIndAuditLog.Add(new SsIndAuditLog
        {
            SesionId = sesionId,
            Evento = evento,
            UserId = userId,
            IpOrigen = ip,
            Detalle = detalle,
            CreatedAt = ahora,
            HashAnterior = anterior,
            Hash = Hash($"{anterior}|{evento}|{sesionId}|{userId}|{ahora:O}|{detalle}"),
        });
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static string Hash(string payload) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));

    /// <summary>"Apellidos Nombres" — nunca al revés.</summary>
    private static string ApellidosNombres(Person? p)
    {
        if (p == null) return string.Empty;
        var partes = $"{p.FirstLastName} {p.SecondLastName} {p.FirstName}".Trim();
        return string.IsNullOrWhiteSpace(partes) ? (p.FullName ?? string.Empty) : System.Text.RegularExpressions.Regex.Replace(partes, @"\s+", " ");
    }

    private static async Task<SsIndAsistencia> NuevaAsistencia(AppDbContext ctx, int sesionId, int workerId)
    {
        var w = await ctx.Worker.AsNoTracking().Include(x => x.Person).FirstOrDefaultAsync(x => x.Id == workerId)
            ?? throw new AbrilException("Trabajador no encontrado.", 404);
        return new SsIndAsistencia
        {
            SesionId = sesionId,
            WorkerId = workerId,
            NombreCompleto = ApellidosNombres(w.Person),
            DocumentoIdentidad = w.Person?.DocumentIdentityCode,
            Categoria = w.Categoria,
            Especialidad = w.Ocupacion ?? w.Puesto,
            ContratistaNombre = w.ContrataCasa,
        };
    }

    private static async Task<string> SiguienteCodigo(AppDbContext ctx, int proyectoId, string tipo)
    {
        var abrev = await ctx.Project.Where(p => p.ProjectId == proyectoId)
            .Select(p => p.Abbreviation ?? p.Codigo).FirstOrDefaultAsync() ?? "PRY";
        var res = await ctx.Database.SqlQuery<int>(
            $"INSERT INTO ss_documento_correlativo (proyecto_id, tipo, ultimo) VALUES ({proyectoId}, {tipo}, 1) ON CONFLICT (proyecto_id, tipo) DO UPDATE SET ultimo = ss_documento_correlativo.ultimo + 1 RETURNING ultimo AS \"Value\"")
            .ToListAsync();
        return $"{abrev.Trim().ToUpperInvariant()}-{tipo}-{res[0]:D4}";
    }
}
