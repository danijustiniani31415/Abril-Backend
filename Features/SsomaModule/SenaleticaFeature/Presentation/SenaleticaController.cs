using System.Security.Claims;
using Abril_Backend.Features.SsomaModule.SenaleticaFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.SenaleticaFeature.Application.Interfaces;
using Abril_Backend.Shared.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abril_Backend.Features.SsomaModule.SenaleticaFeature.Presentation
{
    // Catálogo de señalética de seguridad (NTP 399.010-1). Solo SSOMA: la feature
    // ssoma.gestion.senaletica se asigna únicamente a los roles SSOMA (ver SQL).
    [ApiController]
    [Route("api/v1/ssoma/senaletica")]
    [Authorize]
    [RequireFeature("ssoma.gestion.senaletica")]
    public class SenaleticaController : ControllerBase
    {
        private readonly ISenaleticaService _service;

        public SenaleticaController(ISenaleticaService service)
        {
            _service = service;
        }

        private int? GetUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            return claim != null && int.TryParse(claim.Value, out var id) ? id : null;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            try { return Ok(await _service.GetAllAsync()); }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor." }); }
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] SenalUpsertDto dto)
        {
            try { return Ok(await _service.CreateAsync(dto, GetUserId())); }
            catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor." }); }
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] SenalUpsertDto dto)
        {
            try { await _service.UpdateAsync(id, dto, GetUserId()); return NoContent(); }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor." }); }
        }

        [HttpPatch("{id:int}/activo")]
        public async Task<IActionResult> SetActivo(int id, [FromQuery] bool activo)
        {
            try { await _service.SetActivoAsync(id, activo, GetUserId()); return NoContent(); }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor." }); }
        }

        [HttpPost("{id:int}/imagen")]
        [RequestSizeLimit(10_000_000)]
        public async Task<IActionResult> SubirImagen(int id, IFormFile archivo)
        {
            try { return Ok(new { imagenUrl = await _service.SubirImagenAsync(id, archivo, GetUserId()) }); }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor." }); }
        }

        [HttpDelete("{id:int}/imagen")]
        public async Task<IActionResult> QuitarImagen(int id)
        {
            try { await _service.QuitarImagenAsync(id, GetUserId()); return NoContent(); }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor." }); }
        }

        /// <summary>Elimina una señal propia. Las básicas solo se desactivan.</summary>
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            try { await _service.DeleteAsync(id); return NoContent(); }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor." }); }
        }
    }
}
