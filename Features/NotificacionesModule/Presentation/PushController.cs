using Abril_Backend.Application.Exceptions;
using Abril_Backend.Shared.Services.Push;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Abril_Backend.Features.NotificacionesModule.Presentation
{
    /// <summary>Suscripción de los celulares/PC del usuario a los avisos push (Web Push, gratuito).</summary>
    [ApiController]
    [Route("api/v1/push")]
    [Authorize]
    public class PushController : ControllerBase
    {
        private readonly IPushService _push;
        private readonly ILogger<PushController> _logger;

        public PushController(IPushService push, ILogger<PushController> logger) { _push = push; _logger = logger; }

        private int? CurrentUserId =>
            int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;

        /// <summary>Clave pública VAPID; 204 si el servidor no tiene push configurado.</summary>
        [HttpGet("clave-publica")]
        public IActionResult ClavePublica() =>
            _push.ClavePublica is { } k ? Ok(new { clave = k }) : NoContent();

        [HttpPost("suscribir")]
        public async Task<IActionResult> Suscribir([FromBody] PushSuscripcionDto dto)
        {
            try
            {
                if (CurrentUserId is not { } uid) return Unauthorized();
                await _push.Suscribir(uid, dto, Request.Headers.UserAgent.ToString());
                return Ok(new { message = "Avisos activados." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en PushController.Suscribir"); return StatusCode(500, new { message = "Error del servidor." }); }
        }

        [HttpPost("desuscribir")]
        public async Task<IActionResult> Desuscribir([FromBody] PushSuscripcionDto dto)
        {
            try
            {
                if (CurrentUserId is not { } uid) return Unauthorized();
                await _push.Desuscribir(uid, dto.Endpoint);
                return Ok();
            }
            catch (Exception ex) { _logger.LogError(ex, "Error en PushController.Desuscribir"); return StatusCode(500, new { message = "Error del servidor." }); }
        }
    }
}
