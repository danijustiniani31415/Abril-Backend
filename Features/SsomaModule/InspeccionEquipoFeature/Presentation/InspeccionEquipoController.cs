using System.Security.Claims;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.SsomaModule.InspeccionEquipoFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.InspeccionEquipoFeature.Application.Interfaces;
using Abril_Backend.Shared.Constants;
using Abril_Backend.Shared.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abril_Backend.Features.SsomaModule.InspeccionEquipoFeature.Presentation;

/// <summary>Check list digital de inspección pre-uso de equipos (SSO-FO-049 arnés, SSO-FO-056 minicargador…).
/// Las rutas "publico/…" son sin login: el token del QR (proyecto o equipo) + DNI del trabajador son el candado.</summary>
[ApiController]
[Route("api/v1/ssoma/inspeccion-equipos")]
[Authorize]
[RequireFeature("ssoma.gestion.inspeccion-equipos")]
public class InspeccionEquipoController : ControllerBase
{
    private readonly IInspeccionEquipoService _service;
    private readonly ILogger<InspeccionEquipoController> _logger;

    public InspeccionEquipoController(IInspeccionEquipoService service, ILogger<InspeccionEquipoController> logger)
    {
        _service = service;
        _logger = logger;
    }

    private int CurrentUserId()
    {
        var val = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(val, out var id))
            throw new AbrilException("No se pudo identificar al usuario.", 401);
        return id;
    }

    private IReadOnlyList<string> RoleIds() => User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();

    private bool EsAdmin() => RoleIds().Any(r => r is Roles.AdministradorSistema or Roles.AdministradorSsoma);

    /// <summary>Equipo SSOMA: administra catálogo, equipos, liberaciones y anulaciones. Residentes y capataces solo ven y firman.</summary>
    private bool EsSsoma() => RoleIds().Any(r => r is Roles.AdministradorSistema or Roles.AdministradorSsoma or Roles.CoordinadorSsoma or Roles.Prevencionista);

    private IActionResult Fallo(Exception ex, string contexto)
    {
        if (ex is AbrilException ab) return StatusCode(ab.StatusCode, new { message = ab.Message });
        _logger.LogError(ex, "Error en InspeccionEquipoController.{Contexto}", contexto);
        return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
    }

    private IActionResult? SoloSsoma() =>
        EsSsoma() ? null : StatusCode(403, new { message = "Solo el equipo SSOMA puede realizar esta acción." });

    // ═════════════════════════════════════════════════════════════════════
    // CATÁLOGO
    // ═════════════════════════════════════════════════════════════════════

    [HttpGet("tipos")]
    public async Task<IActionResult> GetTipos()
    {
        try { return Ok(await _service.GetTipos()); }
        catch (Exception ex) { return Fallo(ex, nameof(GetTipos)); }
    }

    [HttpGet("tipos/{id:int}")]
    public async Task<IActionResult> GetTipo(int id)
    {
        try { return Ok(await _service.GetTipo(id)); }
        catch (Exception ex) { return Fallo(ex, nameof(GetTipo)); }
    }

    [HttpPut("tipos/{id:int}")]
    public async Task<IActionResult> ActualizarTipo(int id, [FromBody] InspTipoGuardarDto dto)
    {
        if (SoloSsoma() is { } no) return no;
        try { await _service.ActualizarTipo(id, dto); return NoContent(); }
        catch (Exception ex) { return Fallo(ex, nameof(ActualizarTipo)); }
    }

    [HttpPost("tipos/{tipoId:int}/variantes")]
    public async Task<IActionResult> CrearVariante(int tipoId, [FromBody] InspVarianteGuardarDto dto)
    {
        if (SoloSsoma() is { } no) return no;
        try { return Ok(new { id = await _service.CrearVariante(tipoId, dto) }); }
        catch (Exception ex) { return Fallo(ex, nameof(CrearVariante)); }
    }

    [HttpPut("variantes/{id:int}")]
    public async Task<IActionResult> ActualizarVariante(int id, [FromBody] InspVarianteGuardarDto dto)
    {
        if (SoloSsoma() is { } no) return no;
        try { await _service.ActualizarVariante(id, dto); return NoContent(); }
        catch (Exception ex) { return Fallo(ex, nameof(ActualizarVariante)); }
    }

    /// <summary>Imagen LIMPIA del equipo (sin círculos numerados): los números se dibujan encima con los marcadores.</summary>
    [HttpPost("variantes/{id:int}/imagen")]
    [RequestSizeLimit(15 * 1024 * 1024)]
    public async Task<IActionResult> SubirImagenVariante(int id, [FromForm] IFormFile file)
    {
        if (SoloSsoma() is { } no) return no;
        try
        {
            using var stream = file.OpenReadStream();
            return Ok(await _service.SubirImagenVariante(id, stream, file.FileName));
        }
        catch (Exception ex) { return Fallo(ex, nameof(SubirImagenVariante)); }
    }

    [HttpPost("tipos/{tipoId:int}/items")]
    public async Task<IActionResult> CrearItem(int tipoId, [FromBody] InspItemGuardarDto dto)
    {
        if (SoloSsoma() is { } no) return no;
        try { return Ok(new { id = await _service.CrearItem(tipoId, dto) }); }
        catch (Exception ex) { return Fallo(ex, nameof(CrearItem)); }
    }

    [HttpPut("items/{id:int}")]
    public async Task<IActionResult> ActualizarItem(int id, [FromBody] InspItemGuardarDto dto)
    {
        if (SoloSsoma() is { } no) return no;
        try { await _service.ActualizarItem(id, dto); return NoContent(); }
        catch (Exception ex) { return Fallo(ex, nameof(ActualizarItem)); }
    }

    /// <summary>Reemplaza los marcadores (posición en % sobre la imagen) de un ítem. Un ítem puede tener varios.</summary>
    [HttpPut("items/{id:int}/marcadores")]
    public async Task<IActionResult> SetMarcadores(int id, [FromBody] InspMarcadoresGuardarDto dto)
    {
        if (SoloSsoma() is { } no) return no;
        try { await _service.SetMarcadores(id, dto); return NoContent(); }
        catch (Exception ex) { return Fallo(ex, nameof(SetMarcadores)); }
    }

    // ═════════════════════════════════════════════════════════════════════
    // EQUIPOS
    // ═════════════════════════════════════════════════════════════════════

    [HttpGet("equipos")]
    public async Task<IActionResult> GetEquipos([FromQuery] InspEquipoFiltroDto filtro)
    {
        try { return Ok(await _service.GetEquipos(filtro)); }
        catch (Exception ex) { return Fallo(ex, nameof(GetEquipos)); }
    }

    [HttpGet("equipos/{id:int}")]
    public async Task<IActionResult> GetEquipo(int id)
    {
        try { return Ok(await _service.GetEquipo(id)); }
        catch (Exception ex) { return Fallo(ex, nameof(GetEquipo)); }
    }

    [HttpPost("equipos")]
    public async Task<IActionResult> CrearEquipo([FromBody] InspEquipoGuardarDto dto)
    {
        if (SoloSsoma() is { } no) return no;
        try { return Ok(await _service.CrearEquipo(dto, CurrentUserId())); }
        catch (Exception ex) { return Fallo(ex, nameof(CrearEquipo)); }
    }

    [HttpPut("equipos/{id:int}")]
    public async Task<IActionResult> ActualizarEquipo(int id, [FromBody] InspEquipoGuardarDto dto)
    {
        if (SoloSsoma() is { } no) return no;
        try { await _service.ActualizarEquipo(id, dto); return NoContent(); }
        catch (Exception ex) { return Fallo(ex, nameof(ActualizarEquipo)); }
    }

    [HttpGet("equipos/{id:int}/eventos")]
    public async Task<IActionResult> GetEventosEquipo(int id)
    {
        try { return Ok(await _service.GetEventosEquipo(id)); }
        catch (Exception ex) { return Fallo(ex, nameof(GetEventosEquipo)); }
    }

    /// <summary>Libera un equipo bloqueado por una inspección: exige detalle de lo corregido (queda en el historial).</summary>
    [HttpPost("equipos/{id:int}/liberar")]
    public async Task<IActionResult> LiberarEquipo(int id, [FromBody] InspEquipoAccionDto dto)
    {
        if (SoloSsoma() is { } no) return no;
        try { await _service.LiberarEquipo(id, dto, CurrentUserId()); return NoContent(); }
        catch (Exception ex) { return Fallo(ex, nameof(LiberarEquipo)); }
    }

    [HttpPost("equipos/{id:int}/baja")]
    public async Task<IActionResult> DarBajaEquipo(int id, [FromBody] InspEquipoAccionDto dto)
    {
        if (SoloSsoma() is { } no) return no;
        try { await _service.DarBajaEquipo(id, dto, CurrentUserId()); return NoContent(); }
        catch (Exception ex) { return Fallo(ex, nameof(DarBajaEquipo)); }
    }

    /// <summary>Token del QR fijo del proyecto (idempotente). El frontend arma la URL pública con él.</summary>
    [HttpGet("proyecto-qr/{proyectoId:int}")]
    public async Task<IActionResult> GetQrProyecto(int proyectoId)
    {
        try { return Ok(new { token = await _service.GetOrCrearQrProyecto(proyectoId) }); }
        catch (Exception ex) { return Fallo(ex, nameof(GetQrProyecto)); }
    }

    // ═════════════════════════════════════════════════════════════════════
    // INSPECCIONES
    // ═════════════════════════════════════════════════════════════════════

    [HttpGet]
    public async Task<IActionResult> GetInspecciones([FromQuery] InspInspeccionFiltroDto filtro, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        try
        {
            var (items, total) = await _service.GetInspecciones(filtro, page, pageSize);
            return Ok(new { page, pageSize, totalRecords = total, totalPages = (int)Math.Ceiling(total / (double)Math.Max(pageSize, 1)), data = items });
        }
        catch (Exception ex) { return Fallo(ex, nameof(GetInspecciones)); }
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard([FromQuery] int? proyectoId)
    {
        try { return Ok(await _service.GetDashboard(proyectoId)); }
        catch (Exception ex) { return Fallo(ex, nameof(GetDashboard)); }
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetDetalle(int id)
    {
        try { return Ok(await _service.GetDetalle(id)); }
        catch (Exception ex) { return Fallo(ex, nameof(GetDetalle)); }
    }

    [HttpGet("{id:int}/pdf")]
    public async Task<IActionResult> GetPdf(int id)
    {
        try
        {
            var bytes = await _service.GenerarPdf(id);
            var detalle = await _service.GetDetalle(id);
            return File(bytes, "application/pdf", $"{detalle.Codigo ?? ("CHECKLIST-" + id)}.pdf");
        }
        catch (Exception ex) { return Fallo(ex, nameof(GetPdf)); }
    }

    /// <summary>Registro de seguimiento e inspección diaria de un equipo (Lunes–Sábado de la semana de la fecha; hoy por defecto).</summary>
    [HttpGet("equipos/{id:int}/registro-semanal/pdf")]
    public async Task<IActionResult> GetRegistroSemanal(int id, [FromQuery] DateOnly? fecha)
    {
        try
        {
            var dia = fecha ?? DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-5));
            var bytes = await _service.GenerarRegistroSemanalPdf(id, dia);
            return File(bytes, "application/pdf", $"registro-semanal-{id}-{dia:yyyyMMdd}.pdf");
        }
        catch (Exception ex) { return Fallo(ex, nameof(GetRegistroSemanal)); }
    }

    /// <summary>Verificación pública del QR impreso en el PDF: sin login, el hash es el único candado.</summary>
    [HttpGet("{id:int}/verificar-publico")]
    [AllowAnonymous]
    public async Task<IActionResult> VerificarPublico(int id, [FromQuery] string? hash)
    {
        try { return Ok(await _service.VerificarPublico(id, hash)); }
        catch (Exception ex) { return Fallo(ex, nameof(VerificarPublico)); }
    }

    /// <summary>nivel: capataz | residente | ssoma. La firma sale de la firma digital registrada de quien firma.</summary>
    [HttpPost("{id:int}/firmar/{nivel}")]
    public async Task<IActionResult> Firmar(int id, string nivel)
    {
        try
        {
            var rol = nivel.ToLowerInvariant() switch
            {
                "capataz" => "Capataz",
                "residente" => "Residente",
                "ssoma" => "Ssoma",
                _ => throw new AbrilException("Nivel de firma inválido.", 400),
            };
            await _service.Firmar(id, rol, CurrentUserId(), EsAdmin());
            return Ok(new { message = "Firmado correctamente." });
        }
        catch (Exception ex) { return Fallo(ex, nameof(Firmar)); }
    }

    [HttpPost("{id:int}/anular")]
    public async Task<IActionResult> Anular(int id, [FromBody] InspAnularDto dto)
    {
        if (SoloSsoma() is { } no) return no;
        try { await _service.Anular(id, dto.Motivo, CurrentUserId()); return NoContent(); }
        catch (Exception ex) { return Fallo(ex, nameof(Anular)); }
    }

    // ═════════════════════════════════════════════════════════════════════
    // LLENADO PÚBLICO — sin login
    // ═════════════════════════════════════════════════════════════════════

    /// <summary>Página del QR del proyecto: trabajadores a elegir + formatos "por trabajador" con sus equipos, imágenes y marcadores.</summary>
    [HttpGet("publico/proyecto/{token:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetProyectoPublico(Guid token)
    {
        try { return Ok(await _service.GetProyectoPublico(token)); }
        catch (Exception ex) { return Fallo(ex, nameof(GetProyectoPublico)); }
    }

    /// <summary>"Mis equipos": equipos que esa persona ya registró (con foto), para no volver a escribir el código ni subir foto.</summary>
    [HttpPost("publico/proyecto/{token:guid}/mis-equipos")]
    [AllowAnonymous]
    public async Task<IActionResult> GetMisEquipos(Guid token, [FromBody] InspMisEquiposRequestDto body)
    {
        try { return Ok(await _service.GetMisEquipos(token, body)); }
        catch (Exception ex) { return Fallo(ex, nameof(GetMisEquipos)); }
    }

    /// <summary>Al escribir un código ("281") devuelve los equipos candidatos con su foto, para que la persona elija el suyo.</summary>
    [HttpPost("publico/proyecto/{token:guid}/buscar-equipo")]
    [AllowAnonymous]
    public async Task<IActionResult> BuscarEquipos(Guid token, [FromBody] InspBuscarEquipoRequestDto body)
    {
        try { return Ok(await _service.BuscarEquipos(token, body)); }
        catch (Exception ex) { return Fallo(ex, nameof(BuscarEquipos)); }
    }

    [HttpPost("publico/proyecto/{token:guid}/inspeccion")]
    [AllowAnonymous]
    public async Task<IActionResult> RegistrarPorProyecto(Guid token, [FromBody] InspInspeccionEnvioDto body)
    {
        try
        {
            var res = await _service.RegistrarPorProyecto(token, body, HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString());
            return Ok(res);
        }
        catch (Exception ex) { return Fallo(ex, nameof(RegistrarPorProyecto)); }
    }

    /// <summary>Página del QR pegado al equipo (formatos "por equipo": minicargador…).</summary>
    [HttpGet("publico/equipo/{qrToken:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetEquipoPublico(Guid qrToken)
    {
        try { return Ok(await _service.GetEquipoPublico(qrToken)); }
        catch (Exception ex) { return Fallo(ex, nameof(GetEquipoPublico)); }
    }

    [HttpPost("publico/equipo/{qrToken:guid}/inspeccion")]
    [AllowAnonymous]
    public async Task<IActionResult> RegistrarPorEquipo(Guid qrToken, [FromBody] InspInspeccionEnvioDto body)
    {
        try
        {
            var res = await _service.RegistrarPorEquipo(qrToken, body, HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString());
            return Ok(res);
        }
        catch (Exception ex) { return Fallo(ex, nameof(RegistrarPorEquipo)); }
    }
}
