using System.Security.Cryptography;
using System.Text;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.SsomaModule.PetarFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.PetarFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.SsomaModule.PetarFeature.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.SsomaModule.PetarFeature.Infrastructure.Repositories;

public class PetarRepository : IPetarRepository
{
    private const int PageSize = 20;
    private readonly IDbContextFactory<AppDbContext> _factory;

    public PetarRepository(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<List<PetarTipoDto>> GetTiposConItems()
    {
        using var ctx = _factory.CreateDbContext();
        var tipos = await ctx.SsPetarTipo
            .Where(t => t.Activo)
            .OrderBy(t => t.Orden)
            .Include(t => t.Items.Where(i => i.Activo))
            .ToListAsync();

        return tipos.Select(t => new PetarTipoDto
        {
            Id = t.Id,
            Nombre = t.Nombre,
            Codigo = t.Codigo,
            Items = t.Items.OrderBy(i => i.Orden).Select(i => new PetarItemDto { Id = i.Id, Texto = i.Texto }).ToList(),
        }).ToList();
    }

    public async Task<(int ProyectoId, string? Lugar, string Actividad, string Estado)> GetDatosAts(int atsId)
    {
        using var ctx = _factory.CreateDbContext();
        var ats = await ctx.SsAts.FirstOrDefaultAsync(a => a.Id == atsId)
            ?? throw new AbrilException("ATS de origen no encontrado.", 404);
        return (ats.ProyectoId, ats.Lugar, ats.Actividad, ats.Estado);
    }

    public async Task<int> Crear(int workerId, PetarGuardarRequestDto dto)
    {
        using var ctx = _factory.CreateDbContext();

        var ats = await ctx.SsAts.FirstOrDefaultAsync(a => a.Id == dto.AtsId)
            ?? throw new AbrilException("ATS de origen no encontrado.", 404);
        if (ats.Estado != "Firmado")
            throw new AbrilException("El ATS de origen debe estar firmado antes de generar un PETAR.", 409);

        var petar = new SsPetar
        {
            Codigo = await SiguienteCodigoPetar(ctx, ats.ProyectoId),
            AtsId = dto.AtsId,
            TipoId = dto.TipoId,
            WorkerId = workerId,
            ProyectoId = ats.ProyectoId,
            DescripcionTrabajo = dto.DescripcionTrabajo,
            Lugar = dto.Lugar,
            Fecha = DateOnly.FromDateTime(DateTime.Today),
            HoraInicio = ParseHora(dto.HoraInicio),
            HoraFin = ParseHora(dto.HoraFin),
            Estado = "Borrador",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        await LlenarRespuestas(ctx, petar, dto);
        petar.IzajeGrua = MapIzajeGrua(dto.IzajeGrua);

        ctx.SsPetar.Add(petar);
        await ctx.SaveChangesAsync();

        var hash = ComputeHash(null, "Creado", petar.Id, workerId);
        ctx.SsPetarAuditLog.Add(new SsPetarAuditLog { PetarId = petar.Id, Evento = "Creado", HashAnterior = null, Hash = hash });
        await ctx.SaveChangesAsync();

        return petar.Id;
    }

    public async Task Editar(int id, int workerId, PetarGuardarRequestDto dto)
    {
        using var ctx = _factory.CreateDbContext();

        var petar = await ctx.SsPetar.Include(p => p.Respuestas).Include(p => p.IzajeGrua).FirstOrDefaultAsync(p => p.Id == id)
            ?? throw new AbrilException("PETAR no encontrado.", 404);
        if (petar.WorkerId != workerId)
            throw new AbrilException("Este PETAR no te pertenece.", 403);
        if (petar.Estado != "Borrador")
            throw new AbrilException("Un PETAR firmado no se puede editar.", 409);

        petar.TipoId = dto.TipoId;
        petar.DescripcionTrabajo = dto.DescripcionTrabajo;
        petar.Lugar = dto.Lugar;
        petar.HoraInicio = ParseHora(dto.HoraInicio);
        petar.HoraFin = ParseHora(dto.HoraFin);
        petar.UpdatedAt = DateTime.UtcNow;

        ctx.SsPetarItemRespuesta.RemoveRange(petar.Respuestas);
        petar.Respuestas.Clear();
        await LlenarRespuestas(ctx, petar, dto);

        if (petar.IzajeGrua != null) ctx.Remove(petar.IzajeGrua);
        petar.IzajeGrua = MapIzajeGrua(dto.IzajeGrua);

        await ctx.SaveChangesAsync();

        var hashAnterior = await GetUltimoHashAuditLog(id);
        var hash = ComputeHash(hashAnterior, "Editado", id, workerId);
        ctx.SsPetarAuditLog.Add(new SsPetarAuditLog { PetarId = id, Evento = "Editado", HashAnterior = hashAnterior, Hash = hash });
        await ctx.SaveChangesAsync();
    }

    private static SsPetarIzajeGrua? MapIzajeGrua(PetarIzajeGruaDto? dto)
    {
        if (dto == null) return null;
        return new SsPetarIzajeGrua
        {
            TipoGrua = dto.TipoGrua,
            FabricanteOMarca = dto.FabricanteOMarca,
            ModeloOPlaca = dto.ModeloOPlaca,
            SerieOTarjetaCirculacion = dto.SerieOTarjetaCirculacion,
            LongitudPlumaBrazoM = dto.LongitudPlumaBrazoM,
            RadioMaximoGiroM = dto.RadioMaximoGiroM,
            DireccionGradoGiro = dto.DireccionGradoGiro,
            ElevacionM = dto.ElevacionM,
            AnguloPluma = dto.AnguloPluma,
            CapacidadCertificadaTon = dto.CapacidadCertificadaTon,
            PesoCargaTotalTon = dto.PesoCargaTotalTon,
            PorcentajeCapacidad = dto.PorcentajeCapacidad,
            TamanoEstrobo = dto.TamanoEstrobo,
            Observaciones = dto.Observaciones,
        };
    }

    private static PetarIzajeGruaDto? MapIzajeGruaDto(SsPetarIzajeGrua? m)
    {
        if (m == null) return null;
        return new PetarIzajeGruaDto
        {
            TipoGrua = m.TipoGrua,
            FabricanteOMarca = m.FabricanteOMarca,
            ModeloOPlaca = m.ModeloOPlaca,
            SerieOTarjetaCirculacion = m.SerieOTarjetaCirculacion,
            LongitudPlumaBrazoM = m.LongitudPlumaBrazoM,
            RadioMaximoGiroM = m.RadioMaximoGiroM,
            DireccionGradoGiro = m.DireccionGradoGiro,
            ElevacionM = m.ElevacionM,
            AnguloPluma = m.AnguloPluma,
            CapacidadCertificadaTon = m.CapacidadCertificadaTon,
            PesoCargaTotalTon = m.PesoCargaTotalTon,
            PorcentajeCapacidad = m.PorcentajeCapacidad,
            TamanoEstrobo = m.TamanoEstrobo,
            Observaciones = m.Observaciones,
        };
    }

    private static async Task LlenarRespuestas(AppDbContext ctx, SsPetar petar, PetarGuardarRequestDto dto)
    {
        if (dto.Respuestas.Count == 0) return;

        var itemIds = dto.Respuestas.Select(r => r.ItemId).ToList();
        var catalogo = await ctx.SsPetarItem.Where(i => itemIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id);

        short orden = 0;
        foreach (var r in dto.Respuestas)
        {
            if (!catalogo.TryGetValue(r.ItemId, out var item)) continue;
            petar.Respuestas.Add(new SsPetarItemRespuesta
            {
                ItemId = r.ItemId,
                Texto = item.Texto,
                Respuesta = r.Respuesta,
                Orden = orden++,
            });
        }
    }

    public async Task<SsPetar?> GetEntidad(int id)
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.SsPetar.FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<PetarResponseDto?> GetPorId(int id)
    {
        using var ctx = _factory.CreateDbContext();
        var petar = await ctx.SsPetar
            .Include(p => p.Worker).ThenInclude(w => w!.Person)
            .Include(p => p.Proyecto)
            .Include(p => p.Tipo)
            .Include(p => p.Respuestas)
            .Include(p => p.IzajeGrua)
            .Include(p => p.PetarGrupo)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (petar == null) return null;
        var dto = ToDto(petar);
        dto.AtsCodigo = await ctx.SsAts.Where(a => a.Id == petar.AtsId).Select(a => a.Codigo).FirstOrDefaultAsync();
        return dto;
    }

    /// <summary>"{ABREV}-PETAR-0001": abreviatura del proyecto + correlativo propio de ese proyecto
    /// (upsert atómico, sin números repetidos).</summary>
    private static async Task<string> SiguienteCodigoPetar(AppDbContext ctx, int proyectoId, string tipo = "PETAR")
    {
        var abrev = await ctx.Project.Where(p => p.ProjectId == proyectoId)
            .Select(p => p.Abbreviation ?? p.Codigo).FirstOrDefaultAsync() ?? "PRY";
        var res = await ctx.Database.SqlQuery<int>(
            $"INSERT INTO ss_documento_correlativo (proyecto_id, tipo, ultimo) VALUES ({proyectoId}, {tipo}, 1) ON CONFLICT (proyecto_id, tipo) DO UPDATE SET ultimo = ss_documento_correlativo.ultimo + 1 RETURNING ultimo AS \"Value\"")
            .ToListAsync();
        return $"{abrev.Trim().ToUpperInvariant()}-{tipo}-{res[0]:D4}";
    }

    /// <summary>Cuando este PETAR nació de un PETAR grupal, Supervisor/SSOMA no viven en la fila
    /// individual (serían un solo "Borrador" nunca alcanzando "Firmado" per-persona) sino en
    /// <see cref="SsPetar.PetarGrupo"/> — se rellenan acá para que el resto del sistema (permisos,
    /// PDF) siga leyendo los mismos campos de siempre sin enterarse de la diferencia.</summary>
    private static PetarResponseDto ToDto(SsPetar p)
    {
        var dto = ToDtoBase(p);
        if (p.PetarGrupoId.HasValue && p.PetarGrupo != null)
        {
            dto.CapatazNombre = p.PetarGrupo.CapatazNombre;
            dto.CapatazCargo = p.PetarGrupo.CapatazCargo;
            dto.CapatazFirmaUrl = p.PetarGrupo.CapatazFirmaUrl;
            dto.CapatazHoraServidor = p.PetarGrupo.CapatazHoraServidor;
            dto.SupervisorNombre = p.PetarGrupo.SupervisorNombre;
            dto.SupervisorCargo = p.PetarGrupo.SupervisorCargo;
            dto.SupervisorFirmaUrl = p.PetarGrupo.SupervisorFirmaUrl;
            dto.SupervisorHoraServidor = p.PetarGrupo.SupervisorHoraServidor;
            dto.SsomaNombre = p.PetarGrupo.SsomaNombre;
            dto.SsomaCargo = p.PetarGrupo.SsomaCargo;
            dto.SsomaFirmaUrl = p.PetarGrupo.SsomaFirmaUrl;
            dto.SsomaHoraServidor = p.PetarGrupo.SsomaHoraServidor;
            if (dto.SupervisorFirmaUrl != null && dto.SsomaFirmaUrl != null && dto.Estado == "Borrador")
                dto.Estado = "Firmado";
        }
        return dto;
    }

    private static PetarResponseDto ToDtoBase(SsPetar p) => new()
    {
        Id = p.Id,
        Codigo = p.Codigo,
        AtsId = p.AtsId,
        TipoId = p.TipoId,
        TipoNombre = p.Tipo?.Nombre,
        TipoCodigo = p.Tipo?.Codigo,
        WorkerId = p.WorkerId,
        WorkerNombre = p.Worker?.Person?.FullName,
        ProyectoId = p.ProyectoId,
        ProyectoNombre = p.Proyecto?.ProjectDescription,
        DescripcionTrabajo = p.DescripcionTrabajo,
        Lugar = p.Lugar,
        Fecha = p.Fecha,
        HoraInicio = p.HoraInicio?.ToString("HH:mm"),
        HoraFin = p.HoraFin?.ToString("HH:mm"),
        HoraServidorFirma = p.HoraServidorFirma,
        Lat = p.Lat,
        Lng = p.Lng,
        PrecisionMetros = p.PrecisionMetros,
        SelfieUrl = p.SelfieUrl,
        FirmaUrl = p.FirmaUrl,
        CapatazNombre = p.CapatazNombre,
        CapatazCargo = p.CapatazCargo,
        CapatazFirmaUrl = p.CapatazFirmaUrl,
        CapatazHoraServidor = p.CapatazHoraServidor,
        SupervisorNombre = p.SupervisorNombre,
        SupervisorCargo = p.SupervisorCargo,
        SupervisorFirmaUrl = p.SupervisorFirmaUrl,
        SupervisorHoraServidor = p.SupervisorHoraServidor,
        SsomaNombre = p.SsomaNombre,
        SsomaCargo = p.SsomaCargo,
        SsomaFirmaUrl = p.SsomaFirmaUrl,
        SsomaHoraServidor = p.SsomaHoraServidor,
        Estado = p.Estado,
        CierreHoraServidor = p.CierreHoraServidor,
        CierreObservaciones = p.CierreObservaciones,
        CierreFirmaUrl = p.CierreFirmaUrl,
        PdfHash = p.PdfHash,
        Respuestas = p.Respuestas.OrderBy(r => r.Orden).Select(r => new PetarItemRespuestaResponseDto
        {
            ItemId = r.ItemId,
            Texto = r.Texto,
            Respuesta = r.Respuesta,
        }).ToList(),
        IzajeGrua = MapIzajeGruaDto(p.IzajeGrua),
    };

    public async Task<PetarListResponseDto> Listar(PetarFiltroDto filtro)
    {
        using var ctx = _factory.CreateDbContext();

        var query = ctx.SsPetar
            .Include(p => p.Worker).ThenInclude(w => w!.Person)
            .Include(p => p.Proyecto)
            .Include(p => p.Tipo)
            .Include(p => p.Respuestas)
            .Include(p => p.PetarGrupo)
            .AsQueryable();

        if (filtro.ProyectoId.HasValue) query = query.Where(p => p.ProyectoId == filtro.ProyectoId);
        if (filtro.WorkerId.HasValue) query = query.Where(p => p.WorkerId == filtro.WorkerId);
        if (filtro.AtsId.HasValue) query = query.Where(p => p.AtsId == filtro.AtsId);
        if (filtro.FechaDesde.HasValue) query = query.Where(p => p.Fecha >= filtro.FechaDesde);
        if (filtro.FechaHasta.HasValue) query = query.Where(p => p.Fecha <= filtro.FechaHasta);
        if (!string.IsNullOrWhiteSpace(filtro.Estado)) query = query.Where(p => p.Estado == filtro.Estado);

        query = query.OrderByDescending(p => p.Fecha).ThenByDescending(p => p.Id);

        var total = await query.CountAsync();
        var page = Math.Max(1, filtro.Page);
        var data = await query.Skip((page - 1) * PageSize).Take(PageSize).ToListAsync();

        var dtos = data.Select(ToDto).ToList();
        var atsIds = data.Select(p => p.AtsId).Distinct().ToList();
        var atsCodigos = await ctx.SsAts.Where(a => atsIds.Contains(a.Id)).Select(a => new { a.Id, a.Codigo }).ToDictionaryAsync(a => a.Id, a => a.Codigo);
        foreach (var dto in dtos)
            dto.AtsCodigo = atsCodigos.GetValueOrDefault(dto.AtsId);

        return new PetarListResponseDto
        {
            Data = dtos,
            Page = page,
            PageSize = PageSize,
            TotalRecords = total,
            TotalPages = (int)Math.Ceiling(total / (double)PageSize),
        };
    }

    public async Task Firmar(
        SsPetar petar, string selfieUrl, string selfieHash, string firmaUrl, string firmaHash,
        DateTime horaServidor, PetarFirmarRequestDto body, string? ipOrigen, string? userAgent)
    {
        using var ctx = _factory.CreateDbContext();
        var entidad = await ctx.SsPetar.FirstOrDefaultAsync(p => p.Id == petar.Id)
            ?? throw new AbrilException("PETAR no encontrado.", 404);
        if (entidad.Estado != "Borrador")
            throw new AbrilException("Este PETAR ya fue firmado.", 409);

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
        entidad.UpdatedAt = DateTime.UtcNow;
        // El estado pasa a "Firmado" recién cuando Supervisor Y SSOMA también firmaron — ver
        // FirmarVisto, que es quien de verdad cierra el estado a "Firmado" (autorización a iniciar).

        await ctx.SaveChangesAsync();

        var hashAnterior = await GetUltimoHashAuditLog(petar.Id);
        var hash = ComputeHash(hashAnterior, "FirmadoEjecutante", petar.Id, entidad.WorkerId, firmaHash);
        ctx.SsPetarAuditLog.Add(new SsPetarAuditLog { PetarId = petar.Id, Evento = "FirmadoEjecutante", IpOrigen = ipOrigen, Detalle = $"firma_hash={firmaHash}", HashAnterior = hashAnterior, Hash = hash });
        await ctx.SaveChangesAsync();
    }

    public async Task FirmarVisto(int petarId, string rol, int workerId, string nombre, string? cargo, string firmaUrl, string firmaHash, DateTime horaServidor)
    {
        using var ctx = _factory.CreateDbContext();
        var petar = await ctx.SsPetar.FirstOrDefaultAsync(p => p.Id == petarId) ?? throw new AbrilException("PETAR no encontrado.", 404);

        if (petar.FirmaUrl == null)
            throw new AbrilException("El ejecutante debe firmar el PETAR antes de que se agreguen las demás firmas.", 409);
        if (petar.Estado != "Borrador")
            throw new AbrilException("Este PETAR ya está firmado/cerrado.", 409);

        if (rol == "Capataz")
        {
            if (petar.CapatazFirmaUrl != null)
                throw new AbrilException("Este PETAR ya tiene la firma del Capataz/Maestro de obra.", 409);
            petar.CapatazWorkerId = workerId;
            petar.CapatazNombre = nombre;
            petar.CapatazCargo = cargo;
            petar.CapatazFirmaUrl = firmaUrl;
            petar.CapatazFirmaHash = firmaHash;
            petar.CapatazHoraServidor = horaServidor;
        }
        else if (rol == "Supervisor")
        {
            if (petar.SupervisorFirmaUrl != null)
                throw new AbrilException("Este PETAR ya tiene la firma del Supervisor.", 409);
            petar.SupervisorWorkerId = workerId;
            petar.SupervisorNombre = nombre;
            petar.SupervisorCargo = cargo;
            petar.SupervisorFirmaUrl = firmaUrl;
            petar.SupervisorFirmaHash = firmaHash;
            petar.SupervisorHoraServidor = horaServidor;
        }
        else
        {
            if (petar.SsomaFirmaUrl != null)
                throw new AbrilException("Este PETAR ya tiene el Visto Bueno de SSOMA.", 409);
            petar.SsomaWorkerId = workerId;
            petar.SsomaNombre = nombre;
            petar.SsomaCargo = cargo;
            petar.SsomaFirmaUrl = firmaUrl;
            petar.SsomaFirmaHash = firmaHash;
            petar.SsomaHoraServidor = horaServidor;
        }

        // Las 3 firmas completas = autorizado a iniciar el trabajo de alto riesgo.
        if (petar.SupervisorFirmaUrl != null && petar.SsomaFirmaUrl != null)
            petar.Estado = "Firmado";

        petar.UpdatedAt = DateTime.UtcNow;
        await ctx.SaveChangesAsync();

        var hashAnterior = await GetUltimoHashAuditLog(petarId);
        var hash = ComputeHash(hashAnterior, $"Firmado{rol}", petarId, workerId, firmaHash);
        ctx.SsPetarAuditLog.Add(new SsPetarAuditLog { PetarId = petarId, Evento = $"Firmado{rol}", Detalle = $"firma_hash={firmaHash}", HashAnterior = hashAnterior, Hash = hash });
        await ctx.SaveChangesAsync();
    }

    public async Task Cerrar(int petarId, int workerId, string firmaUrl, string firmaHash, string? observaciones, DateTime horaServidor)
    {
        using var ctx = _factory.CreateDbContext();
        var petar = await ctx.SsPetar.FirstOrDefaultAsync(p => p.Id == petarId) ?? throw new AbrilException("PETAR no encontrado.", 404);

        if (petar.Estado != "Firmado")
            throw new AbrilException("Solo se puede cerrar un PETAR con las 3 firmas completas (trabajo autorizado).", 409);
        if (petar.WorkerId != workerId)
            throw new AbrilException("Solo el ejecutante puede cerrar este PETAR.", 403);

        petar.Estado = "Cerrado";
        petar.CierreHoraServidor = horaServidor;
        petar.CierreObservaciones = observaciones;
        petar.CierreFirmaUrl = firmaUrl;
        petar.CierreFirmaHash = firmaHash;
        petar.UpdatedAt = DateTime.UtcNow;
        await ctx.SaveChangesAsync();

        var hashAnterior = await GetUltimoHashAuditLog(petarId);
        var hash = ComputeHash(hashAnterior, "Cerrado", petarId, workerId, firmaHash);
        ctx.SsPetarAuditLog.Add(new SsPetarAuditLog { PetarId = petarId, Evento = "Cerrado", Detalle = observaciones, HashAnterior = hashAnterior, Hash = hash });
        await ctx.SaveChangesAsync();
    }

    public async Task GuardarPdf(int id, string pdfUrl, string pdfHash)
    {
        using var ctx = _factory.CreateDbContext();
        var petar = await ctx.SsPetar.FirstOrDefaultAsync(p => p.Id == id) ?? throw new AbrilException("PETAR no encontrado.", 404);
        petar.PdfUrl = pdfUrl;
        petar.PdfHash = pdfHash;
        await ctx.SaveChangesAsync();

        var hashAnterior = await GetUltimoHashAuditLog(id);
        var hash = ComputeHash(hashAnterior, "PdfExportado", id, petar.WorkerId, pdfHash);
        ctx.SsPetarAuditLog.Add(new SsPetarAuditLog { PetarId = id, Evento = "PdfExportado", Detalle = $"pdf_hash={pdfHash}", HashAnterior = hashAnterior, Hash = hash });
        await ctx.SaveChangesAsync();
    }

    public async Task<string?> GetUltimoHashAuditLog(int petarId)
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.SsPetarAuditLog.Where(l => l.PetarId == petarId).OrderByDescending(l => l.Id).Select(l => l.Hash).FirstOrDefaultAsync();
    }

    public async Task AgregarAuditLog(int petarId, string evento, int? userId, string? ipOrigen, string? detalle, string hashAnterior, string hash)
    {
        using var ctx = _factory.CreateDbContext();
        ctx.SsPetarAuditLog.Add(new SsPetarAuditLog { PetarId = petarId, Evento = evento, UserId = userId, IpOrigen = ipOrigen, Detalle = detalle, HashAnterior = hashAnterior, Hash = hash });
        await ctx.SaveChangesAsync();
    }

    public async Task<bool> ExisteSelfieHash(string selfieHash)
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.SsPetar.AnyAsync(p => p.SelfieHash == selfieHash);
    }

    private static TimeOnly? ParseHora(string? hhmm)
        => TimeOnly.TryParse(hhmm, out var t) ? t : null;

    private static string ComputeHash(string? hashAnterior, string evento, int petarId, int workerId, string? extra = null)
    {
        var payload = $"{hashAnterior}|{evento}|{petarId}|{workerId}|{DateTime.UtcNow:O}|{extra}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }

    // ── PETAR Grupal ──────────────────────────────────────────────────────

    public async Task<SsPetarGrupo> CrearGrupo(int creadoPorWorkerId, int proyectoId, PetarGrupoCrearRequestDto dto)
    {
        using var ctx = _factory.CreateDbContext();

        var grupo = new SsPetarGrupo
        {
            Codigo = await SiguienteCodigoPetar(ctx, proyectoId, "PETG"),
            AtsGrupoId = dto.AtsGrupoId,
            TipoId = dto.TipoId,
            ProyectoId = proyectoId,
            CreadoPorWorkerId = creadoPorWorkerId,
            DescripcionTrabajo = dto.DescripcionTrabajo,
            Lugar = dto.Lugar,
            Fecha = DateOnly.FromDateTime(DateTime.Today),
            HoraInicio = ParseHora(dto.HoraInicio),
            HoraFin = ParseHora(dto.HoraFin),
            Estado = "Activo",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        if (dto.Respuestas.Count > 0)
        {
            var itemIds = dto.Respuestas.Select(r => r.ItemId).ToList();
            var catalogo = await ctx.SsPetarItem.Where(i => itemIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id);

            short orden = 0;
            foreach (var r in dto.Respuestas)
            {
                if (!catalogo.TryGetValue(r.ItemId, out var item)) continue;
                grupo.Respuestas.Add(new SsPetarGrupoItemRespuesta
                {
                    ItemId = r.ItemId,
                    Texto = item.Texto,
                    Respuesta = r.Respuesta,
                    Orden = orden++,
                });
            }
        }

        ctx.SsPetarGrupo.Add(grupo);
        await ctx.SaveChangesAsync();
        return grupo;
    }

    public async Task<List<PetarGrupoResumenPublicoDto>> GetGruposActivosPorAtsGrupo(int atsGrupoId)
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.SsPetarGrupo
            .Where(g => g.AtsGrupoId == atsGrupoId && g.Estado == "Activo")
            .Include(g => g.Tipo)
            .Include(g => g.Respuestas)
            .Select(g => new PetarGrupoResumenPublicoDto
            {
                Id = g.Id,
                TipoNombre = g.Tipo!.Nombre,
                DescripcionTrabajo = g.DescripcionTrabajo,
                Lugar = g.Lugar,
                HoraInicio = g.HoraInicio.HasValue ? g.HoraInicio.Value.ToString("HH:mm") : null,
                HoraFin = g.HoraFin.HasValue ? g.HoraFin.Value.ToString("HH:mm") : null,
                Respuestas = g.Respuestas.OrderBy(r => r.Orden).Select(r => new PetarItemRespuestaResponseDto
                {
                    ItemId = r.ItemId,
                    Texto = r.Texto,
                    Respuesta = r.Respuesta,
                }).ToList(),
            })
            .ToListAsync();
    }

    public async Task<List<PetarGrupoEstadoDto>> GetEstadosPorAtsGrupo(int atsGrupoId)
    {
        using var ctx = _factory.CreateDbContext();
        var grupos = await ctx.SsPetarGrupo
            .Where(g => g.AtsGrupoId == atsGrupoId)
            .Include(g => g.Tipo)
            .OrderByDescending(g => g.CreatedAt)
            .ToListAsync();

        var ids = grupos.Select(g => g.Id).ToList();
        var adheridos = await ctx.SsPetar
            .Where(p => p.PetarGrupoId.HasValue && ids.Contains(p.PetarGrupoId.Value))
            .Include(p => p.Worker).ThenInclude(w => w!.Person)
            .ToListAsync();
        var adheridosPorGrupo = adheridos.ToLookup(p => p.PetarGrupoId!.Value);
        var atsCodigo = await ctx.SsAtsGrupo.Where(a => a.Id == atsGrupoId).Select(a => a.Codigo).FirstOrDefaultAsync();

        return grupos.Select(g => new PetarGrupoEstadoDto
        {
            Id = g.Id,
            Codigo = g.Codigo ?? $"PETG-{g.Id:D4}",
            AtsCodigo = atsCodigo,
            AtsGrupoId = atsGrupoId,
            ProyectoId = g.ProyectoId,
            TipoNombre = g.Tipo?.Nombre,
            DescripcionTrabajo = g.DescripcionTrabajo,
            Estado = g.Estado,
            CapatazFirmado = g.CapatazFirmaUrl != null,
            SupervisorFirmado = g.SupervisorFirmaUrl != null,
            SsomaFirmado = g.SsomaFirmaUrl != null,
            TotalAdhesiones = adheridosPorGrupo[g.Id].Count(),
            TrabajadoresAdheridos = adheridosPorGrupo[g.Id].Select(p => p.Worker?.Person?.FullName ?? "—").ToList(),
        }).ToList();
    }

    public async Task<SsPetarGrupo?> GetGrupoEntidad(int id)
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.SsPetarGrupo.Include(g => g.Respuestas).FirstOrDefaultAsync(g => g.Id == id);
    }

    /// <summary>Grupo con lo que necesita el PDF: tipo, checklist, ATS grupal (código) y adhesiones con su trabajador.</summary>
    public async Task<SsPetarGrupo?> GetGrupoParaPdf(int id)
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.SsPetarGrupo.AsNoTracking()
            .Include(g => g.Tipo)
            .Include(g => g.Respuestas)
            .Include(g => g.AtsGrupo)
            .Include(g => g.Proyecto)
            .Include(g => g.Adhesiones).ThenInclude(p => p.Worker).ThenInclude(w => w!.Person)
            .AsSplitQuery()
            .FirstOrDefaultAsync(g => g.Id == id);
    }

    public async Task<PetarGrupoEstadoDto?> GetEstadoGrupo(int id)
    {
        using var ctx = _factory.CreateDbContext();
        var grupo = await ctx.SsPetarGrupo.Include(g => g.Tipo).FirstOrDefaultAsync(g => g.Id == id);
        if (grupo is null) return null;

        var adheridos = await ctx.SsPetar
            .Where(p => p.PetarGrupoId == id)
            .Include(p => p.Worker).ThenInclude(w => w!.Person)
            .OrderBy(p => p.Worker!.Person!.FullName)
            .ToListAsync();

        return new PetarGrupoEstadoDto
        {
            Id = grupo.Id,
            Codigo = grupo.Codigo ?? $"PETG-{grupo.Id:D4}",
            AtsGrupoId = grupo.AtsGrupoId,
            AtsCodigo = await ctx.SsAtsGrupo.Where(a => a.Id == grupo.AtsGrupoId).Select(a => a.Codigo).FirstOrDefaultAsync(),
            ProyectoId = grupo.ProyectoId,
            TipoNombre = grupo.Tipo?.Nombre,
            DescripcionTrabajo = grupo.DescripcionTrabajo,
            Estado = grupo.Estado,
            CapatazFirmado = grupo.CapatazFirmaUrl != null,
            SupervisorFirmado = grupo.SupervisorFirmaUrl != null,
            SsomaFirmado = grupo.SsomaFirmaUrl != null,
            TotalAdhesiones = adheridos.Count,
            TrabajadoresAdheridos = adheridos.Select(p => p.Worker?.Person?.FullName ?? "—").ToList(),
        };
    }

    public async Task<bool> EsAutorDeGrupo(int petarGrupoId, int workerId)
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.SsPetarGrupo.AnyAsync(g => g.Id == petarGrupoId && g.CreadoPorWorkerId == workerId);
    }

    public async Task CerrarGrupo(int petarGrupoId)
    {
        using var ctx = _factory.CreateDbContext();
        var grupo = await ctx.SsPetarGrupo.FirstOrDefaultAsync(g => g.Id == petarGrupoId) ?? throw new AbrilException("PETAR grupal no encontrado.", 404);
        grupo.Estado = "Cerrado";
        grupo.UpdatedAt = DateTime.UtcNow;
        await ctx.SaveChangesAsync();
    }

    public async Task FirmarVistoGrupo(int petarGrupoId, string rol, int workerId, string nombre, string? cargo, string firmaUrl, string firmaHash, DateTime horaServidor, PetarEvidenciaPresencia? evidencia = null)
    {
        using var ctx = _factory.CreateDbContext();
        var grupo = await ctx.SsPetarGrupo.FirstOrDefaultAsync(g => g.Id == petarGrupoId) ?? throw new AbrilException("PETAR grupal no encontrado.", 404);

        if (rol == "Capataz")
        {
            if (grupo.CapatazFirmaUrl != null)
                throw new AbrilException("Este PETAR grupal ya tiene la firma del Capataz/Maestro de obra.", 409);
            grupo.CapatazWorkerId = workerId;
            grupo.CapatazNombre = nombre;
            grupo.CapatazCargo = cargo;
            grupo.CapatazFirmaUrl = firmaUrl;
            grupo.CapatazFirmaHash = firmaHash;
            grupo.CapatazHoraServidor = horaServidor;
            if (evidencia != null)
            {
                grupo.CapatazSelfieUrl = evidencia.SelfieUrl;
                grupo.CapatazSelfieHash = evidencia.SelfieHash;
                grupo.CapatazLat = evidencia.Lat;
                grupo.CapatazLng = evidencia.Lng;
                grupo.CapatazPrecisionMetros = evidencia.PrecisionMetros;
                grupo.CapatazHoraDispositivo = evidencia.HoraDispositivo;
            }
        }
        else if (rol == "Supervisor")
        {
            if (grupo.SupervisorFirmaUrl != null)
                throw new AbrilException("Este PETAR grupal ya tiene la firma del Supervisor.", 409);
            grupo.SupervisorWorkerId = workerId;
            grupo.SupervisorNombre = nombre;
            grupo.SupervisorCargo = cargo;
            grupo.SupervisorFirmaUrl = firmaUrl;
            grupo.SupervisorFirmaHash = firmaHash;
            grupo.SupervisorHoraServidor = horaServidor;
        }
        else
        {
            if (grupo.SsomaFirmaUrl != null)
                throw new AbrilException("Este PETAR grupal ya tiene el Visto Bueno de SSOMA.", 409);
            grupo.SsomaWorkerId = workerId;
            grupo.SsomaNombre = nombre;
            grupo.SsomaCargo = cargo;
            grupo.SsomaFirmaUrl = firmaUrl;
            grupo.SsomaFirmaHash = firmaHash;
            grupo.SsomaHoraServidor = horaServidor;
        }

        grupo.UpdatedAt = DateTime.UtcNow;
        await ctx.SaveChangesAsync();
    }

    public async Task<bool> YaFirmoPetarGrupo(int petarGrupoId, int workerId)
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.SsPetar.AnyAsync(p => p.PetarGrupoId == petarGrupoId && p.WorkerId == workerId && p.FirmaUrl != null);
    }

    public async Task<int> CrearDesdeGrupo(int workerId, int atsIdPropio, SsPetarGrupo grupo)
    {
        using var ctx = _factory.CreateDbContext();

        var petar = new SsPetar
        {
            Codigo = await SiguienteCodigoPetar(ctx, grupo.ProyectoId),
            AtsId = atsIdPropio,
            TipoId = grupo.TipoId,
            WorkerId = workerId,
            ProyectoId = grupo.ProyectoId,
            DescripcionTrabajo = grupo.DescripcionTrabajo,
            Lugar = grupo.Lugar,
            Fecha = grupo.Fecha,
            HoraInicio = grupo.HoraInicio,
            HoraFin = grupo.HoraFin,
            Estado = "Borrador",
            PetarGrupoId = grupo.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        short orden = 0;
        foreach (var r in grupo.Respuestas)
        {
            petar.Respuestas.Add(new SsPetarItemRespuesta
            {
                ItemId = r.ItemId,
                Texto = r.Texto,
                Respuesta = r.Respuesta,
                Orden = orden++,
            });
        }

        ctx.SsPetar.Add(petar);
        await ctx.SaveChangesAsync();

        var hash = ComputeHash(null, "CreadoDesdeGrupo", petar.Id, workerId, $"petar_grupo_id={grupo.Id}");
        ctx.SsPetarAuditLog.Add(new SsPetarAuditLog { PetarId = petar.Id, Evento = "CreadoDesdeGrupo", Detalle = $"petar_grupo_id={grupo.Id}", HashAnterior = null, Hash = hash });
        await ctx.SaveChangesAsync();

        return petar.Id;
    }
}
