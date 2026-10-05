using System.Security.Claims;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.SsomaModule.PetarFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.PetarFeature.Application.Interfaces;
using Abril_Backend.Shared.Constants;
using Abril_Backend.Shared.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abril_Backend.Features.SsomaModule.PetarFeature.Presentation
{
    [ApiController]
    [Route("api/v1/ssoma/petar")]
    [Authorize]
    [RequireFeature("ssoma.gestion.ats")]
    public class PetarController : ControllerBase
    {
        private readonly IPetarService _service;
        private readonly ILogger<PetarController> _logger;

        public PetarController(IPetarService service, ILogger<PetarController> logger)
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

        private bool EsAdmin()
        {
            var roleIds = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
            return roleIds.Contains(Roles.AdministradorSistema) || roleIds.Contains(Roles.AdministradorSsoma);
        }

        [HttpGet("tipos-catalogo")]
        public async Task<IActionResult> GetTiposCatalogo()
        {
            try { return Ok(await _service.GetTiposCatalogo()); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en PetarController.GetTiposCatalogo"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpGet("init/{atsId:int}")]
        public async Task<IActionResult> GetInit(int atsId)
        {
            try { return Ok(await _service.GetInit(atsId)); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en PetarController.GetInit"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPost]
        public async Task<IActionResult> Crear([FromBody] PetarGuardarRequestDto dto)
        {
            try
            {
                var workerId = await _service.ResolverWorkerId(CurrentUserId());
                var id = await _service.Crear(workerId, dto);
                return Ok(new { id });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en PetarController.Crear"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Editar(int id, [FromBody] PetarGuardarRequestDto dto)
        {
            try
            {
                var workerId = await _service.ResolverWorkerId(CurrentUserId());
                await _service.Editar(id, workerId, dto);
                return Ok(new { message = "PETAR actualizado." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en PetarController.Editar"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetPorId(int id)
        {
            try
            {
                var userId = CurrentUserId();
                var workerId = await _service.ResolverWorkerId(userId);
                return Ok(await _service.GetPorId(id, userId, workerId, EsAdmin()));
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en PetarController.GetPorId"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpGet]
        public async Task<IActionResult> Listar([FromQuery] PetarFiltroDto filtro)
        {
            try
            {
                var userId = CurrentUserId();
                var workerId = await _service.ResolverWorkerId(userId);
                return Ok(await _service.Listar(filtro, userId, workerId, EsAdmin()));
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en PetarController.Listar"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPost("{id:int}/firmar")]
        public async Task<IActionResult> Firmar(int id, [FromBody] PetarFirmarRequestDto body)
        {
            try
            {
                var workerId = await _service.ResolverWorkerId(CurrentUserId());
                var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
                var userAgent = Request.Headers.UserAgent.ToString();
                await _service.Firmar(id, workerId, body, ip, userAgent);
                return Ok(new { message = "PETAR firmado por el ejecutante." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en PetarController.Firmar"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPost("{id:int}/firmar-capataz")]
        public async Task<IActionResult> FirmarCapataz(int id, [FromBody] PetarFirmarVistoRequestDto body)
        {
            try
            {
                await _service.FirmarCapataz(id, CurrentUserId(), EsAdmin(), body);
                return Ok(new { message = "Firmado como Capataz/Maestro de obra." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en PetarController.FirmarCapataz"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPost("{id:int}/firmar-supervisor")]
        public async Task<IActionResult> FirmarSupervisor(int id, [FromBody] PetarFirmarVistoRequestDto body)
        {
            try
            {
                await _service.FirmarSupervisor(id, CurrentUserId(), EsAdmin(), body);
                return Ok(new { message = "Firmado como Supervisor/Responsable." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en PetarController.FirmarSupervisor"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPost("{id:int}/visto-bueno-ssoma")]
        public async Task<IActionResult> FirmarVistoSsoma(int id, [FromBody] PetarFirmarVistoRequestDto body)
        {
            try
            {
                await _service.FirmarVistoSsoma(id, CurrentUserId(), EsAdmin(), body);
                return Ok(new { message = "Visto bueno de SSOMA registrado." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en PetarController.FirmarVistoSsoma"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPost("{id:int}/cerrar")]
        public async Task<IActionResult> Cerrar(int id, [FromBody] PetarCerrarRequestDto body)
        {
            try
            {
                var workerId = await _service.ResolverWorkerId(CurrentUserId());
                await _service.Cerrar(id, workerId, body);
                return Ok(new { message = "PETAR cerrado." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en PetarController.Cerrar"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpGet("{id:int}/verificar-publico")]
        [AllowAnonymous]
        public async Task<IActionResult> VerificarPublico(int id, [FromQuery] string? hash)
        {
            try { return Ok(await _service.VerificarPublico(id, hash)); }
            catch (Exception ex) { _logger.LogError(ex, "Error en PetarController.VerificarPublico"); return StatusCode(500, new { message = "Error del servidor." }); }
        }

        [HttpGet("{id:int}/pdf")]
        public async Task<IActionResult> GetPdf(int id)
        {
            try
            {
                var bytes = await _service.GenerarPdf(id);
                return File(bytes, "application/pdf", $"PETAR-{id}.pdf");
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en PetarController.GetPdf"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        // ── PETAR Grupal ────────────────────────────────────────────────────

        [HttpPost("grupo")]
        public async Task<IActionResult> CrearGrupo([FromBody] PetarGrupoCrearRequestDto dto)
        {
            try
            {
                var workerId = await _service.ResolverWorkerId(CurrentUserId());
                return Ok(await _service.CrearGrupo(workerId, dto));
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en PetarController.CrearGrupo"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpGet("grupo/por-ats-grupo/{atsGrupoId:int}")]
        public async Task<IActionResult> GetEstadosPorAtsGrupo(int atsGrupoId)
        {
            try
            {
                var userId = CurrentUserId();
                var workerId = await _service.ResolverWorkerId(userId);
                return Ok(await _service.GetEstadosPorAtsGrupo(atsGrupoId, userId, workerId, EsAdmin()));
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en PetarController.GetEstadosPorAtsGrupo"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpGet("grupo/{id:int}/estado")]
        public async Task<IActionResult> GetEstadoGrupo(int id)
        {
            try
            {
                var userId = CurrentUserId();
                var workerId = await _service.ResolverWorkerId(userId);
                return Ok(await _service.GetEstadoGrupo(id, userId, workerId, EsAdmin()));
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en PetarController.GetEstadoGrupo"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpGet("grupo/{id:int}/verificar-publico")]
        [AllowAnonymous]
        public async Task<IActionResult> VerificarPublicoGrupo(int id, [FromQuery] string? hash)
        {
            try { return Ok(await _service.VerificarPublicoGrupo(id, hash)); }
            catch (Exception ex) { _logger.LogError(ex, "Error en PetarController.VerificarPublicoGrupo"); return StatusCode(500, new { message = "Error del servidor." }); }
        }

        [HttpGet("grupo/{id:int}/pdf")]
        public async Task<IActionResult> GetPdfGrupo(int id)
        {
            try
            {
                var bytes = await _service.GenerarPdfGrupo(id);
                return File(bytes, "application/pdf", $"PETAR-GRUPAL-{id}.pdf");
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en PetarController.GetPdfGrupo"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPost("grupo/{id:int}/cerrar")]
        public async Task<IActionResult> CerrarGrupo(int id)
        {
            try
            {
                var workerId = await _service.ResolverWorkerId(CurrentUserId());
                await _service.CerrarGrupo(id, workerId, EsAdmin());
                return Ok(new { message = "PETAR grupal cerrado." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en PetarController.CerrarGrupo"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        /// <summary>Catálogo de tipos de PETAR con su checklist, para el formulario público (QR) de generar PETAR.</summary>
        [HttpGet("grupo/publico/{atsToken:guid}/tipos")]
        [AllowAnonymous]
        public async Task<IActionResult> GetTiposPublico(Guid atsToken)
        {
            try { return Ok(await _service.GetTiposCatalogo()); }
            catch (Exception ex) { _logger.LogError(ex, "Error en PetarController.GetTiposPublico"); return StatusCode(500, new { message = "Error del servidor." }); }
        }

        /// <summary>Generar el PETAR de la cuadrilla desde el QR, sin cuenta (autor o Capataz, con nombre + DNI).</summary>
        [HttpPost("grupo/publico/{atsToken:guid}/crear")]
        [AllowAnonymous]
        public async Task<IActionResult> CrearGrupoPublico(Guid atsToken, [FromBody] PetarGrupoCrearPublicoDto body)
        {
            try { return Ok(await _service.CrearGrupoPublico(atsToken, body)); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en PetarController.CrearGrupoPublico"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        /// <summary>Página pública del Capataz (la del ATS grupal): PETAR de la cuadrilla que esperan su firma.</summary>
        [HttpGet("grupo/publico/{atsToken:guid}/capataz")]
        [AllowAnonymous]
        public async Task<IActionResult> GetCapatazPublicoPetares(Guid atsToken)
        {
            try { return Ok(await _service.GetCapatazPublicoPetares(atsToken)); }
            catch (Exception ex) { _logger.LogError(ex, "Error en PetarController.GetCapatazPublicoPetares"); return StatusCode(500, new { message = "Error del servidor." }); }
        }

        [HttpPost("grupo/{id:int}/capataz-publico/firmar")]
        [AllowAnonymous]
        public async Task<IActionResult> FirmarCapatazGrupoPublico(int id, [FromBody] PetarGrupoCapatazFirmarPublicoDto body)
        {
            try
            {
                await _service.FirmarCapatazGrupoPublico(id, body);
                return Ok(new { message = "Firmado como Capataz/Maestro de obra." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en PetarController.FirmarCapatazGrupoPublico"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPost("grupo/{id:int}/firmar-capataz")]
        public async Task<IActionResult> FirmarCapatazGrupo(int id, [FromBody] PetarFirmarVistoRequestDto body)
        {
            try
            {
                await _service.FirmarCapatazGrupo(id, CurrentUserId(), EsAdmin(), body);
                return Ok(new { message = "Firmado como Capataz/Maestro de obra para todo el grupo." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en PetarController.FirmarCapatazGrupo"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPost("grupo/{id:int}/firmar-supervisor")]
        public async Task<IActionResult> FirmarSupervisorGrupo(int id, [FromBody] PetarFirmarVistoRequestDto body)
        {
            try
            {
                await _service.FirmarSupervisorGrupo(id, CurrentUserId(), EsAdmin(), body);
                return Ok(new { message = "Firmado como Supervisor/Responsable para todo el grupo." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en PetarController.FirmarSupervisorGrupo"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPost("grupo/{id:int}/visto-bueno-ssoma")]
        public async Task<IActionResult> FirmarSsomaGrupo(int id, [FromBody] PetarFirmarVistoRequestDto body)
        {
            try
            {
                await _service.FirmarSsomaGrupo(id, CurrentUserId(), EsAdmin(), body);
                return Ok(new { message = "Visto bueno de SSOMA registrado para todo el grupo." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en PetarController.FirmarSsomaGrupo"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        // ── Página pública de adhesión (mismo QR/token del ATS grupal) ──────

        [HttpGet("grupo/publico/por-ats-token/{atsToken:guid}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetGruposPublicoPorAtsToken(Guid atsToken)
        {
            try { return Ok(await _service.GetGruposPublicoPorAtsToken(atsToken)); }
            catch (Exception ex) { _logger.LogError(ex, "Error en PetarController.GetGruposPublicoPorAtsToken"); return StatusCode(500, new { message = "Error del servidor." }); }
        }

        [HttpPost("grupo/publico/{id:int}/unirse")]
        [AllowAnonymous]
        public async Task<IActionResult> UnirseAGrupo(int id, [FromBody] PetarGrupoUnirseRequestDto body)
        {
            try
            {
                var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
                var userAgent = Request.Headers.UserAgent.ToString();
                var petarId = await _service.UnirseAGrupo(id, body, ip, userAgent);
                return Ok(new { id = petarId, message = "PETAR firmado correctamente." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en PetarController.UnirseAGrupo"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }
    }
}
