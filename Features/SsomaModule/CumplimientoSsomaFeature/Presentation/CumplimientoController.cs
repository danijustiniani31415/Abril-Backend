using System.Security.Claims;
using Abril_Backend.Features.SsomaModule.CumplimientoSsomaFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.CumplimientoSsomaFeature.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Abril_Backend.Shared.Filters;

namespace Abril_Backend.Features.SsomaModule.CumplimientoSsomaFeature.Presentation
{
    [ApiController]
    [Route("api/v1/ssoma/cumplimiento")]
    [Authorize]
    [RequireFeature("ssoma.gestion.cumplimiento")]
    public class CumplimientoController : ControllerBase
    {
        private readonly ICumplimientoService _service;

        public CumplimientoController(ICumplimientoService service)
        {
            _service = service;
        }

        private int? GetUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            return claim != null && int.TryParse(claim.Value, out var id) ? id : null;
        }

        // ─────────────────────────────────────────────────────────────────
        // CATÁLOGO DE ACTIVIDADES
        // ─────────────────────────────────────────────────────────────────

        [HttpGet("actividades")]
        public async Task<IActionResult> GetActividades()
        {
            try { return Ok(await _service.GetActividadesAsync()); }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor." }); }
        }

        [HttpPost("actividades")]
        public async Task<IActionResult> CreateActividad([FromBody] CumplimientoActividadUpsertDto dto)
        {
            try { return Ok(await _service.CreateActividadAsync(dto)); }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor." }); }
        }

        [HttpPut("actividades/{actividadId:int}")]
        public async Task<IActionResult> UpdateActividad(int actividadId, [FromBody] CumplimientoActividadUpsertDto dto)
        {
            try
            {
                await _service.UpdateActividadAsync(actividadId, dto);
                return NoContent();
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor." }); }
        }

        [HttpDelete("actividades/{actividadId:int}")]
        public async Task<IActionResult> DeleteActividad(int actividadId)
        {
            try
            {
                await _service.DeleteActividadAsync(actividadId);
                return NoContent();
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor." }); }
        }

        // ─────────────────────────────────────────────────────────────────
        // CUMPLIMIENTO POR PROYECTO
        // ─────────────────────────────────────────────────────────────────

        /// <summary>Prevencionistas y coordinadores SSOMA del proyecto, para elegir de quién ver el checklist.</summary>
        [HttpGet("proyecto/{proyectoId:int}/usuarios")]
        public async Task<IActionResult> GetUsuarios(int proyectoId)
        {
            try { return Ok(await _service.GetUsuariosProyectoAsync(proyectoId)); }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor." }); }
        }

        /// <summary>Estado de cumplimiento del periodo vigente (día/semana/mes según cada actividad) del checklist de un usuario en un proyecto.</summary>
        [HttpGet("proyecto/{proyectoId:int}/resumen")]
        public async Task<IActionResult> GetResumen(int proyectoId, [FromQuery] int usuarioId)
        {
            try { return Ok(await _service.GetResumenProyectoAsync(proyectoId, usuarioId)); }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor." }); }
        }

        /// <summary>Marca (cumplido/no aplica) o desmarca (pendiente) una actividad en el periodo vigente, siempre en el checklist del usuario logueado.</summary>
        [HttpPatch("proyecto/{proyectoId:int}/actividades/{actividadId:int}")]
        public async Task<IActionResult> Marcar(int proyectoId, int actividadId, [FromBody] CumplimientoMarcarDto dto)
        {
            try
            {
                var userId = GetUserId();
                if (userId == null) return Unauthorized();
                var result = await _service.MarcarAsync(proyectoId, actividadId, dto, userId.Value);
                return Ok(result);
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor." }); }
        }

        /// <summary>Checklist del usuario logueado: resuelve su rol y su proyecto actual solo, sin que tenga que elegir nada (pensado para celular).</summary>
        [HttpGet("mi-resumen")]
        public async Task<IActionResult> GetMiResumen()
        {
            try
            {
                var userId = GetUserId();
                if (userId == null) return Unauthorized();
                return Ok(await _service.GetMiResumenAsync(userId.Value));
            }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor." }); }
        }

        /// <summary>Histórico de cumplimiento (% por día/semana/mes) de un proyecto, para indicadores.</summary>
        [HttpGet("proyecto/{proyectoId:int}/historico")]
        public async Task<IActionResult> GetHistorico(int proyectoId, [FromQuery] int usuarioId, [FromQuery] string frecuencia, [FromQuery] DateOnly desde, [FromQuery] DateOnly hasta)
        {
            try { return Ok(await _service.GetHistoricoAsync(proyectoId, usuarioId, frecuencia, desde, hasta)); }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor." }); }
        }
    }
}
