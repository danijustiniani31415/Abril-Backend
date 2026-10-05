using System.Security.Claims;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.SsomaModule.AtsFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.AtsFeature.Application.Interfaces;
using Abril_Backend.Shared.Constants;
using Abril_Backend.Shared.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abril_Backend.Features.SsomaModule.AtsFeature.Presentation
{
    [ApiController]
    [Route("api/v1/ssoma/ats")]
    [Authorize]
    [RequireFeature("ssoma.gestion.ats")]
    public class AtsController : ControllerBase
    {
        private readonly IAtsService _service;
        private readonly ILogger<AtsController> _logger;

        public AtsController(IAtsService service, ILogger<AtsController> logger)
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

        /// <summary>
        /// El claim de rol lleva el ID (estable), no el nombre — comparar contra
        /// "ADMINISTRADOR DE SSOMA" (como estaba antes) nunca coincide con nada, porque el JWT
        /// nunca lleva ese texto (ver JWTService.cs). Roles.AdministradorSistema="1",
        /// Roles.AdministradorSsoma="9" (JEFE SSOMA).
        /// </summary>
        private bool EsAdmin()
        {
            var roleIds = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
            return roleIds.Contains(Roles.AdministradorSistema) || roleIds.Contains(Roles.AdministradorSsoma);
        }

        /// <summary>Liviano: solo proyectos + proyecto actual para el filtro del listado (GetInit trae
        /// además todos los catálogos del wizard, innecesario para listar).</summary>
        [HttpGet("lista-init")]
        public async Task<IActionResult> GetListaInit()
        {
            try
            {
                var workerId = await _service.ResolverWorkerId(CurrentUserId());
                return Ok(await _service.GetListaInit(workerId));
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.GetListaInit"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        /// <summary>Listado de ATS grupales (cuadrillas) — cualquier usuario con acceso al módulo ve los de su
        /// proyecto; el Jefe/Administrador SSOMA ve todos.</summary>
        [HttpGet("grupos")]
        public async Task<IActionResult> ListarGrupos([FromQuery] AtsFiltroDto filtro)
        {
            try
            {
                var workerId = await _service.ResolverWorkerId(CurrentUserId());
                return Ok(await _service.ListarGrupos(filtro, workerId, EsAdmin()));
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.ListarGrupos"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpGet("init")]
        public async Task<IActionResult> GetInit()
        {
            try
            {
                var workerId = await _service.ResolverWorkerId(CurrentUserId());
                return Ok(await _service.GetInit(workerId));
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.GetInit"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        /// <summary>Pasos para un puesto ARBITRARIO — lo usa el wizard de ATS Grupal (logueado y
        /// público) cuando el creador elige el puesto/tipo de trabajo de la cuadrilla, que puede
        /// ser distinto al suyo. [AllowAnonymous] porque el wizard público lo necesita igual.</summary>
        [HttpGet("pasos-por-puesto/{puestoId:int}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPasosPorPuesto(int puestoId, [FromQuery] int workerId)
        {
            try
            {
                // Logueado: el worker sale del JWT (el query param viene en 0). Público: lo manda
                // el wizard tras identificarse — solo decide si se incluyen Gabinete/Supervisión.
                if (workerId == 0 && User.Identity?.IsAuthenticated == true)
                    workerId = await _service.ResolverWorkerId(CurrentUserId());
                return Ok(await _service.GetPasosPorPuesto(puestoId, workerId));
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.GetPasosPorPuesto"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        public class CrearPasoRequest { public int CategoriaId { get; set; } public string Texto { get; set; } = string.Empty; }

        /// <summary>[AllowAnonymous] porque el wizard público de creación de ATS grupal (sin login,
        /// ver GetInitPublico/CrearGrupoPublico) reutiliza el mismo componente que el wizard
        /// logueado — no es sensible, solo agrega un texto de paso al catálogo compartido.</summary>
        [HttpPost("pasos")]
        [AllowAnonymous]
        public async Task<IActionResult> CrearPasoPersonalizado([FromBody] CrearPasoRequest body)
        {
            try { return Ok(await _service.CrearPasoPersonalizado(body.CategoriaId, body.Texto)); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.CrearPasoPersonalizado"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPost]
        public async Task<IActionResult> Crear([FromBody] AtsGuardarRequestDto dto)
        {
            try
            {
                var workerId = await _service.ResolverWorkerId(CurrentUserId());
                var id = await _service.Crear(workerId, dto);
                return Ok(new { id });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.Crear"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Editar(int id, [FromBody] AtsGuardarRequestDto dto)
        {
            try
            {
                var workerId = await _service.ResolverWorkerId(CurrentUserId());
                await _service.Editar(id, workerId, dto);
                return Ok(new { message = "ATS actualizado." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.Editar"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
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
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.GetPorId"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpGet]
        public async Task<IActionResult> Listar([FromQuery] AtsFiltroDto filtro)
        {
            try
            {
                var userId = CurrentUserId();
                var workerId = await _service.ResolverWorkerId(userId);
                return Ok(await _service.Listar(filtro, userId, workerId, EsAdmin()));
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.Listar"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPost("{id:int}/firmar")]
        public async Task<IActionResult> Firmar(int id, [FromBody] AtsFirmarRequestDto body)
        {
            try
            {
                var workerId = await _service.ResolverWorkerId(CurrentUserId());
                var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
                var userAgent = Request.Headers.UserAgent.ToString();
                await _service.Firmar(id, workerId, body, ip, userAgent);
                return Ok(new { message = "ATS firmado correctamente." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.Firmar"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        /// <summary>
        /// Firma de "Capataz" (Capataz / Maestro de obra) — primer nivel de la cadena, solo
        /// aplica cuando el ejecutante es obrero de obra. Adicional a la del ejecutante, que ya
        /// debe estar firmada. Sin selfie ni geo: es una validación documental.
        /// </summary>
        [HttpPost("{id:int}/firmar-capataz")]
        public async Task<IActionResult> FirmarCapataz(int id, [FromBody] AtsFirmarVistoRequestDto body)
        {
            try
            {
                await _service.FirmarCapataz(id, CurrentUserId(), EsAdmin(), body);
                return Ok(new { message = "Firmado como Capataz." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.FirmarCapataz"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        /// <summary>
        /// Firma de "Autoriza" (Residente / Ing. de Producción) — adicional a la del ejecutante,
        /// que ya debe estar firmada. Sin selfie ni geo: es una validación documental.
        /// </summary>
        [HttpPost("{id:int}/autorizar")]
        public async Task<IActionResult> FirmarAutorizacion(int id, [FromBody] AtsFirmarVistoRequestDto body)
        {
            try
            {
                await _service.FirmarAutorizacion(id, CurrentUserId(), EsAdmin(), body);
                return Ok(new { message = "Firmado como Autoriza." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.FirmarAutorizacion"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        /// <summary>Visto Bueno de SSOMA (Prevencionista / Coordinador SSOMA) — verificación técnica
        /// del análisis de riesgo, obligatoria y no removible (Art. 76 Reglamento Ley 29783).</summary>
        [HttpPost("{id:int}/visto-bueno-ssoma")]
        public async Task<IActionResult> FirmarVistoSsoma(int id, [FromBody] AtsFirmarVistoRequestDto body)
        {
            try
            {
                await _service.FirmarVistoSsoma(id, CurrentUserId(), EsAdmin(), body);
                return Ok(new { message = "Visto bueno de SSOMA registrado." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.FirmarVistoSsoma"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        /// <summary>
        /// Verificación pública del QR impreso en el PDF — sin login, para que un inspector de
        /// SUNAFIL (o cualquiera con el link) confirme que el documento corresponde exactamente
        /// a lo firmado en el sistema. [AllowAnonymous] hace que [Authorize]/[RequireFeature] de
        /// la clase no apliquen a esta acción (RequireFeatureAttribute ya no hace nada cuando el
        /// usuario no está autenticado).
        /// </summary>
        [HttpGet("{id:int}/verificar-publico")]
        [AllowAnonymous]
        public async Task<IActionResult> VerificarPublico(int id, [FromQuery] string? hash)
        {
            try { return Ok(await _service.VerificarPublico(id, hash)); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.VerificarPublico"); return StatusCode(500, new { message = "Error del servidor." }); }
        }

        [HttpGet("{id:int}/pdf")]
        public async Task<IActionResult> GetPdf(int id)
        {
            try
            {
                var bytes = await _service.GenerarPdf(id);
                return File(bytes, "application/pdf", $"ATS-{id}.pdf");
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.GetPdf"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        // ── Administración de plantillas (Coordinador SSOMA en adelante) ────

        [HttpGet("puestos")]
        public async Task<IActionResult> GetPuestos()
        {
            try { return Ok(await _service.GetPuestos()); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.GetPuestos"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpGet("pasos-puesto")]
        public async Task<IActionResult> GetPasoPuestoMapeo()
        {
            try { return Ok(await _service.GetPasoPuestoMapeo()); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.GetPasoPuestoMapeo"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPut("pasos-puesto/{pasoId:int}")]
        public async Task<IActionResult> SetPasoPuestos(int pasoId, [FromBody] List<int> puestoIds)
        {
            try
            {
                await _service.SetPasoPuestos(pasoId, puestoIds);
                return Ok(new { message = "Actualizado." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.SetPasoPuestos"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpGet("peligros")]
        public async Task<IActionResult> GetPeligros()
        {
            try { return Ok(await _service.GetPeligrosConRiesgos()); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.GetPeligros"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        /// <summary>El Coordinador SSOMA marca manualmente qué riesgos exigen PETAR — no hay un
        /// valor por defecto del sistema, es una decisión de catálogo suya.</summary>
        [HttpPut("riesgos/{id:int}/requiere-petar")]
        public async Task<IActionResult> SetRiesgoRequierePetar(int id, [FromBody] AtsRiesgoRequierePetarRequestDto dto)
        {
            try
            {
                await _service.SetRiesgoRequierePetar(id, dto.RequierePetar);
                return Ok(new { message = "Actualizado." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.SetRiesgoRequierePetar"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpGet("plantillas-puesto")]
        public async Task<IActionResult> GetPlantillaPuestoMapeo()
        {
            try { return Ok(await _service.GetPlantillaPuestoMapeo()); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.GetPlantillaPuestoMapeo"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPut("plantillas-puesto/{plantillaId:int}")]
        public async Task<IActionResult> SetPlantillaPuestos(int plantillaId, [FromBody] List<int> puestoIds)
        {
            try
            {
                await _service.SetPlantillaPuestos(plantillaId, puestoIds);
                return Ok(new { message = "Actualizado." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.SetPlantillaPuestos"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpGet("plantillas")]
        public async Task<IActionResult> GetPlantillas()
        {
            try { return Ok(await _service.GetPlantillas()); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.GetPlantillas"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPost("plantillas")]
        public async Task<IActionResult> CrearPlantilla([FromBody] AtsPlantillaGuardarRequestDto dto)
        {
            try
            {
                var id = await _service.CrearPlantilla(dto);
                return Ok(new { id });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.CrearPlantilla"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPut("plantillas/{id:int}")]
        public async Task<IActionResult> EditarPlantilla(int id, [FromBody] AtsPlantillaGuardarRequestDto dto)
        {
            try
            {
                await _service.EditarPlantilla(id, dto);
                return Ok(new { message = "Plantilla actualizada." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.EditarPlantilla"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpDelete("plantillas/{id:int}")]
        public async Task<IActionResult> DesactivarPlantilla(int id)
        {
            try
            {
                await _service.DesactivarPlantilla(id);
                return Ok(new { message = "Plantilla desactivada." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.DesactivarPlantilla"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        // ── Actividades/pasos por plantilla ──────────────────────────────────

        [HttpGet("plantillas/{plantillaId:int}/actividades")]
        public async Task<IActionResult> GetActividadesDePlantilla(int plantillaId)
        {
            try { return Ok(await _service.GetActividadesDePlantilla(plantillaId)); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.GetActividadesDePlantilla"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPost("plantillas/{plantillaId:int}/actividades")]
        public async Task<IActionResult> CrearActividad(int plantillaId, [FromBody] AtsPlantillaActividadGuardarRequestDto dto)
        {
            try
            {
                var id = await _service.CrearActividad(plantillaId, dto);
                return Ok(new { id });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.CrearActividad"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPut("actividades/{actividadId:int}")]
        public async Task<IActionResult> EditarActividad(int actividadId, [FromBody] AtsPlantillaActividadGuardarRequestDto dto)
        {
            try
            {
                await _service.EditarActividad(actividadId, dto);
                return Ok(new { message = "Actualizado." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.EditarActividad"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpDelete("actividades/{actividadId:int}")]
        public async Task<IActionResult> EliminarActividad(int actividadId)
        {
            try
            {
                await _service.EliminarActividad(actividadId);
                return Ok(new { message = "Eliminado." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.EliminarActividad"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPut("actividades/{actividadId:int}/peligros")]
        public async Task<IActionResult> SetActividadPeligros(int actividadId, [FromBody] AtsPlantillaActividadPeligrosRequestDto dto)
        {
            try
            {
                await _service.SetActividadPeligros(actividadId, dto);
                return Ok(new { message = "Actualizado." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.SetActividadPeligros"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPost("actividades/{actividadId:int}/pasos")]
        public async Task<IActionResult> CrearPaso(int actividadId, [FromBody] AtsPlantillaPasoGuardarRequestDto dto)
        {
            try
            {
                var id = await _service.CrearPaso(actividadId, dto);
                return Ok(new { id });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.CrearPaso"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPut("pasos/{pasoId:int}")]
        public async Task<IActionResult> EditarPaso(int pasoId, [FromBody] AtsPlantillaPasoGuardarRequestDto dto)
        {
            try
            {
                await _service.EditarPaso(pasoId, dto);
                return Ok(new { message = "Actualizado." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.EditarPaso"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpDelete("pasos/{pasoId:int}")]
        public async Task<IActionResult> EliminarPaso(int pasoId)
        {
            try
            {
                await _service.EliminarPaso(pasoId);
                return Ok(new { message = "Eliminado." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.EliminarPaso"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        // ── Controles sugeridos por riesgo ───────────────────────────────────

        [HttpGet("riesgos-controles")]
        public async Task<IActionResult> GetRiesgosConControles()
        {
            try { return Ok(await _service.GetRiesgosConControles()); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.GetRiesgosConControles"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPost("riesgos/{riesgoId:int}/controles")]
        public async Task<IActionResult> CrearControl(int riesgoId, [FromBody] AtsRiesgoControlGuardarRequestDto dto)
        {
            try
            {
                var id = await _service.CrearControl(riesgoId, dto);
                return Ok(new { id });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.CrearControl"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPut("controles/{controlId:int}")]
        public async Task<IActionResult> EditarControl(int controlId, [FromBody] AtsRiesgoControlGuardarRequestDto dto)
        {
            try
            {
                await _service.EditarControl(controlId, dto);
                return Ok(new { message = "Actualizado." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.EditarControl"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpDelete("controles/{controlId:int}")]
        public async Task<IActionResult> EliminarControl(int controlId)
        {
            try
            {
                await _service.EliminarControl(controlId);
                return Ok(new { message = "Eliminado." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.EliminarControl"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        // ── Autorización de uso de firma digital e imagen (firmada en físico) — gate para poder hacer ATS ──

        /// <summary>El propio trabajador consulta esto ANTES de abrir el formulario de "Nuevo
        /// ATS", para mostrarle un bloqueo claro en vez de dejarlo llenar todo el wizard y
        /// recién fallar al guardar (que además también valida esto server-side).</summary>
        [HttpGet("mi-autorizacion")]
        public async Task<IActionResult> GetMiAutorizacion()
        {
            try
            {
                var workerId = await _service.ResolverWorkerId(CurrentUserId());
                var tieneAutorizacion = await _service.TieneAutorizacionPermiso(workerId);
                return Ok(new { tieneAutorizacion });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.GetMiAutorizacion"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        /// <summary>Listado del Coordinador SSOMA: qué trabajadores ya tienen su autorización de
        /// uso de firma digital e imagen subida y cuáles faltan.</summary>
        [HttpGet("trabajadores-autorizacion")]
        public async Task<IActionResult> GetTrabajadoresParaAutorizacion()
        {
            try { return Ok(await _service.GetTrabajadoresParaAutorizacion()); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.GetTrabajadoresParaAutorizacion"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        /// <summary>El propio trabajador la consulta al llegar al paso de firmar un ATS, para
        /// ofrecerle reusar la misma firma que ya le capturó el Coordinador SSOMA para la
        /// Autorización (SSO-FO-151) en vez de dibujarla de nuevo en el lienzo.</summary>
        [HttpGet("mi-firma-digital-autorizacion")]
        public async Task<IActionResult> GetMiFirmaDigitalAutorizacion()
        {
            try
            {
                var workerId = await _service.ResolverWorkerId(CurrentUserId());
                var (firmaDigitalUrl, _, _) = await _service.GetFirmaDigitalAutorizacion(workerId);
                return Ok(new { firmaDigitalUrl });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.GetMiFirmaDigitalAutorizacion"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        /// <summary>La imagen en sí (no la URL) — el navegador no puede traer el blob del storage
        /// directo, esto pasa por el backend con sus propias credenciales.</summary>
        [HttpGet("mi-firma-digital-autorizacion/imagen")]
        public async Task<IActionResult> GetMiFirmaDigitalAutorizacionImagen()
        {
            try
            {
                var workerId = await _service.ResolverWorkerId(CurrentUserId());
                var bytes = await _service.GetFirmaDigitalAutorizacionImagen(workerId);
                if (bytes == null) return NotFound(new { message = "Todavía no tienes firma digital autorizada capturada." });
                return File(bytes, "image/png");
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.GetMiFirmaDigitalAutorizacionImagen"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        /// <summary>El Coordinador SSOMA captura en pantalla la firma del trabajador ANTES de poder
        /// descargar la plantilla — requisito previo (ver GetPlantillaAutorizacionPdf).</summary>
        [HttpPost("trabajadores/{workerId:int}/autorizacion/firma-digital")]
        public async Task<IActionResult> CapturarFirmaDigitalAutorizacion(int workerId, [FromBody] AtsAutorizacionFirmaDigitalRequestDto dto)
        {
            try
            {
                await _service.CapturarFirmaDigitalAutorizacion(workerId, dto, CurrentUserId());
                return Ok(new { message = "Firma digital registrada." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.CapturarFirmaDigitalAutorizacion"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        /// <summary>Plantilla imprimible de la autorización (SSO-FO-151) para un trabajador
        /// puntual — ya trae impresa la firma digital capturada arriba, junto a un espacio para la
        /// firma física. La descarga el Coordinador SSOMA, se la da a firmar en físico junto a la
        /// digital, y luego sube el escaneado con el endpoint de abajo.</summary>
        [HttpGet("trabajadores/{workerId:int}/autorizacion/pdf")]
        public async Task<IActionResult> GetPlantillaAutorizacionPdf(int workerId)
        {
            try
            {
                var bytes = await _service.GenerarPlantillaAutorizacionPdf(workerId);
                return File(bytes, "application/pdf", $"Autorizacion_ATS_{workerId}.pdf");
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.GetPlantillaAutorizacionPdf"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        /// <summary>Capataz/Maestro de obra: registra su correo personal + la aceptación de la declaración
        /// (impresa en el PDF). Se hace ANTES de descargar la plantilla.</summary>
        [HttpPost("trabajadores/{workerId:int}/autorizacion/email")]
        public async Task<IActionResult> GuardarEmailPersonalAutorizacion(int workerId, [FromBody] AtsAutorizacionEmailRequestDto dto)
        {
            try
            {
                await _service.GuardarEmailPersonalAutorizacion(workerId, dto);
                return Ok(new { message = "Correo registrado." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.GuardarEmailPersonalAutorizacion"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        /// <summary>Reintenta crear la cuenta del Capataz/Maestro (por si falló al subir la autorización).</summary>
        [HttpPost("trabajadores/{workerId:int}/autorizacion/crear-cuenta")]
        public async Task<IActionResult> CrearCuentaCapataz(int workerId)
        {
            try
            {
                await _service.CrearCuentaCapataz(workerId);
                return Ok(new { message = "Cuenta creada — se envió el correo para generar la contraseña." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.CrearCuentaCapataz"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPost("trabajadores/{workerId:int}/autorizacion")]
        public async Task<IActionResult> SubirAutorizacionPermiso(int workerId, IFormFile archivo)
        {
            try
            {
                if (archivo == null || archivo.Length == 0)
                    throw new AbrilException("El archivo está vacío.", 400);

                await using var stream = archivo.OpenReadStream();
                await _service.SubirAutorizacionPermiso(workerId, stream, archivo.FileName, CurrentUserId());
                return Ok(new { message = "Autorización subida correctamente." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.SubirAutorizacionPermiso"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        // ── ATS Grupal ────────────────────────────────────────────────────────

        /// <summary>Crea el contenido compartido de la cuadrilla (mismo wizard de datos/pasos/
        /// peligros/valoración) y devuelve el QR token para que cada integrante se adhiera solo.</summary>
        [HttpPost("grupo")]
        public async Task<IActionResult> CrearGrupo([FromBody] AtsGuardarRequestDto dto)
        {
            try
            {
                var workerId = await _service.ResolverWorkerId(CurrentUserId());
                return Ok(await _service.CrearGrupo(workerId, dto, EsAdmin()));
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.CrearGrupo"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        /// <summary>Panel del autor: cuántos de la cuadrilla ya firmaron.</summary>
        [HttpGet("grupo/{id:int}/estado")]
        public async Task<IActionResult> GetEstadoGrupo(int id)
        {
            try
            {
                var userId = CurrentUserId();
                var workerId = await _service.ResolverWorkerId(userId);
                return Ok(await _service.GetEstadoGrupo(id, workerId, EsAdmin()));
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.GetEstadoGrupo"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        // ── Observaciones ──
        [HttpGet("{id:int}/observaciones")]
        public async Task<IActionResult> GetObservacionesAts(int id)
        {
            try { return Ok(await _service.GetObservacionesAts(id, CurrentUserId(), EsAdmin())); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.GetObservacionesAts"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPost("{id:int}/observaciones")]
        public async Task<IActionResult> CrearObservacionAts(int id, [FromBody] AtsObservacionCrearRequestDto body)
        {
            try { await _service.CrearObservacionAts(id, body.Texto, CurrentUserId(), EsAdmin()); return Ok(new { message = "Observación registrada." }); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.CrearObservacionAts"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpGet("grupo/{id:int}/observaciones")]
        public async Task<IActionResult> GetObservacionesGrupo(int id)
        {
            try { return Ok(await _service.GetObservacionesGrupo(id, CurrentUserId(), EsAdmin())); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.GetObservacionesGrupo"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPost("grupo/{id:int}/observaciones")]
        public async Task<IActionResult> CrearObservacionGrupo(int id, [FromBody] AtsObservacionCrearRequestDto body)
        {
            try { await _service.CrearObservacionGrupo(id, body.Texto, CurrentUserId(), EsAdmin()); return Ok(new { message = "Observación registrada." }); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.CrearObservacionGrupo"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPost("observaciones/{observacionId:int}/resolver")]
        public async Task<IActionResult> ResolverObservacion(int observacionId, [FromBody] AtsObservacionResolverRequestDto body)
        {
            try { await _service.ResolverObservacion(observacionId, body.Respuesta, CurrentUserId(), EsAdmin()); return Ok(new { message = "Observación resuelta." }); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.ResolverObservacion"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        /// <summary>Contenido del grupo con forma de ATS — precarga el wizard al corregir la cuadrilla.</summary>
        [HttpGet("grupo/{id:int}/contenido")]
        public async Task<IActionResult> GetContenidoGrupo(int id)
        {
            try
            {
                var workerId = await _service.ResolverWorkerId(CurrentUserId());
                return Ok(await _service.GetContenidoGrupo(id, workerId, EsAdmin()));
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.GetContenidoGrupo"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        /// <summary>Anula un ATS firmado (no se borra: queda con motivo, quién y cuándo).</summary>
        [HttpPost("{id:int}/anular")]
        public async Task<IActionResult> AnularAts(int id, [FromBody] AtsAnularRequestDto body)
        {
            try
            {
                await _service.AnularAts(id, body.Motivo, CurrentUserId(), EsAdmin());
                return Ok(new { message = "ATS anulado." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.AnularAts"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPost("grupo/{id:int}/anular")]
        public async Task<IActionResult> AnularGrupo(int id, [FromBody] AtsAnularRequestDto body)
        {
            try
            {
                await _service.AnularGrupo(id, body.Motivo, CurrentUserId(), EsAdmin());
                return Ok(new { message = "ATS grupal anulado." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.AnularGrupo"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        /// <summary>El worker del usuario logueado — para que la pantalla de adhesión lo preseleccione sin pedir nombre ni DNI.</summary>
        [HttpGet("yo")]
        public async Task<IActionResult> GetYo()
        {
            try
            {
                var workerId = await _service.ResolverWorkerId(CurrentUserId());
                return Ok(new { workerId });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.GetYo"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        /// <summary>Adhesión de quien ya inició sesión: firma como sí mismo, sin elegir nombre ni confirmar DNI.</summary>
        [HttpPost("grupo/{token:guid}/unirse-yo")]
        public async Task<IActionResult> UnirseAGrupoLogueado(Guid token, [FromBody] AtsGrupoUnirseRequestDto body)
        {
            try
            {
                var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
                var userAgent = Request.Headers.UserAgent.ToString();
                var id = await _service.UnirseAGrupoLogueado(token, CurrentUserId(), body, ip, userAgent);
                return Ok(new { id, message = "ATS firmado correctamente." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.UnirseAGrupoLogueado"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPost("grupo/{token:guid}/mi-ats-yo")]
        public async Task<IActionResult> GetMiAtsLogueado(Guid token)
        {
            try { return Ok(new { atsId = await _service.GetMiAtsLogueado(token, CurrentUserId()) }); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.GetMiAtsLogueado"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPost("grupo/{id:int}/reabrir")]
        public async Task<IActionResult> ReabrirGrupo(int id)
        {
            try
            {
                var workerId = await _service.ResolverWorkerId(CurrentUserId());
                await _service.ReabrirGrupo(id, workerId, EsAdmin());
                return Ok(new { message = "ATS grupal reabierto." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.ReabrirGrupo"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpGet("grupo/{id:int}/candidatos")]
        public async Task<IActionResult> GetCandidatosGrupo(int id)
        {
            try
            {
                var workerId = await _service.ResolverWorkerId(CurrentUserId());
                return Ok(await _service.GetCandidatosGrupo(id, workerId, EsAdmin()));
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.GetCandidatosGrupo"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPut("grupo/{id:int}/integrantes")]
        public async Task<IActionResult> SetIntegrantesGrupo(int id, [FromBody] AtsGrupoIntegrantesRequestDto body)
        {
            try
            {
                var workerId = await _service.ResolverWorkerId(CurrentUserId());
                await _service.SetIntegrantesGrupo(id, body.WorkerIds, workerId, EsAdmin());
                return Ok(new { message = "Integrantes actualizados." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.SetIntegrantesGrupo"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        /// <summary>Quien ya firmó vuelve al link (p. ej. para un PETAR creado después): devuelve el id de su ATS.</summary>
        [HttpPost("grupo/publico/{token:guid}/mi-ats")]
        [AllowAnonymous]
        public async Task<IActionResult> GetMiAtsPublico(Guid token, [FromBody] AtsGrupoMiAtsRequestDto body)
        {
            try
            {
                var id = await _service.GetMiAtsPublico(token, body);
                return Ok(new { atsId = id });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.GetMiAtsPublico"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        /// <summary>PDF único del ATS grupal: todas las firmas de la cuadrilla + una firma de Capataz/Autoriza/SSOMA.</summary>
        [HttpGet("grupo/{id:int}/pdf")]
        public async Task<IActionResult> GetPdfGrupo(int id)
        {
            try
            {
                var workerId = await _service.ResolverWorkerId(CurrentUserId());
                var bytes = await _service.GenerarPdfGrupo(id, workerId, EsAdmin());
                return File(bytes, "application/pdf", $"ATS-GRUPAL-{id}.pdf");
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.GetPdfGrupo"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        /// <summary>Residente/Producción firman "Autoriza" a toda la cuadrilla de una vez (con su firma digital).</summary>
        [HttpPost("grupo/{id:int}/firmar-autoriza")]
        public async Task<IActionResult> FirmarAutorizaGrupo(int id)
        {
            try
            {
                var n = await _service.FirmarVistoGrupo(id, "Autoriza", CurrentUserId(), EsAdmin());
                return Ok(new { message = $"Autorizaste {n} ATS de la cuadrilla.", firmados = n });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.FirmarAutorizaGrupo"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        /// <summary>SSOMA da el Visto Bueno a toda la cuadrilla de una vez (con su firma digital).</summary>
        [HttpPost("grupo/{id:int}/firmar-ssoma")]
        public async Task<IActionResult> FirmarSsomaGrupo(int id)
        {
            try
            {
                var n = await _service.FirmarVistoGrupo(id, "Ssoma", CurrentUserId(), EsAdmin());
                return Ok(new { message = $"Diste Visto Bueno a {n} ATS de la cuadrilla.", firmados = n });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.FirmarSsomaGrupo"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        /// <summary>El autor cierra el ATS grupal manualmente (deja de aceptar adhesiones nuevas) —
        /// los ATS ya adheridos no se ven afectados.</summary>
        [HttpPost("grupo/{id:int}/cerrar")]
        public async Task<IActionResult> CerrarGrupo(int id)
        {
            try
            {
                var userId = CurrentUserId();
                var workerId = await _service.ResolverWorkerId(userId);
                await _service.CerrarGrupo(id, workerId, EsAdmin());
                return Ok(new { message = "ATS grupal cerrado." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.CerrarGrupo"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        /// <summary>El autor sin cuenta (creó la cuadrilla desde el QR de obra) la cierra con su nombre + DNI.</summary>
        [HttpPost("grupo/publico/{token:guid}/cerrar")]
        [AllowAnonymous]
        public async Task<IActionResult> CerrarGrupoPublico(Guid token, [FromBody] AtsGrupoCerrarPublicoRequestDto body)
        {
            try
            {
                await _service.CerrarGrupoPublico(token, body);
                return Ok(new { message = "ATS grupal cerrado." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.CerrarGrupoPublico"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        // ── Página pública de adhesión (QR) — SIN login, el token es el único candado ──────

        [HttpGet("grupo/publico/{token:guid}/resumen")]
        [AllowAnonymous]
        public async Task<IActionResult> GetResumenPublico(Guid token)
        {
            try { return Ok(await _service.GetResumenPublico(token)); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.GetResumenPublico"); return StatusCode(500, new { message = "Error del servidor." }); }
        }

        [HttpGet("grupo/publico/{token:guid}/trabajadores")]
        [AllowAnonymous]
        public async Task<IActionResult> GetWorkersParaAdhesion(Guid token)
        {
            try { return Ok(await _service.GetWorkersParaAdhesion(token)); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.GetWorkersParaAdhesion"); return StatusCode(500, new { message = "Error del servidor." }); }
        }

        [HttpPost("grupo/publico/{token:guid}/unirse")]
        [AllowAnonymous]
        public async Task<IActionResult> UnirseAGrupo(Guid token, [FromBody] AtsGrupoUnirseRequestDto body)
        {
            try
            {
                var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
                var userAgent = Request.Headers.UserAgent.ToString();
                var id = await _service.UnirseAGrupo(token, body, ip, userAgent);
                return Ok(new { id, message = "ATS firmado correctamente." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.UnirseAGrupo"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        // ── Firma única del Capataz por cuadrilla — SIN login, el token del grupo es el candado ──

        /// <summary>Capataz/Maestro con cuenta: firma el grupo completo (una vez por cuadrilla) desde la plataforma.</summary>
        [HttpPost("grupo/{id:int}/firmar-capataz")]
        public async Task<IActionResult> FirmarCapatazGrupoLogueado(int id, [FromBody] AtsFirmarVistoRequestDto body)
        {
            try
            {
                await _service.FirmarCapatazGrupoLogueado(id, CurrentUserId(), EsAdmin(), body);
                return Ok(new { message = "Firmado como Capataz." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.FirmarCapatazGrupoLogueado"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpGet("grupo/publico/{token:guid}/capataz")]
        [AllowAnonymous]
        public async Task<IActionResult> GetCapatazPublico(Guid token)
        {
            try { return Ok(await _service.GetCapatazPublico(token)); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.GetCapatazPublico"); return StatusCode(500, new { message = "Error del servidor." }); }
        }

        [HttpPost("grupo/publico/{token:guid}/capataz/firmar")]
        [AllowAnonymous]
        public async Task<IActionResult> FirmarCapatazPublico(Guid token, [FromBody] AtsGrupoCapatazFirmarRequestDto body)
        {
            try
            {
                await _service.FirmarCapatazPublico(token, body);
                return Ok(new { message = "Firmado como Capataz." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.FirmarCapatazPublico"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        // ── QR fijo por proyecto — crear ATS grupal SIN login (cualquier integrante de la
        // cuadrilla, incluso sin cuenta en la plataforma) ──────────────────────────────────

        /// <summary>Idempotente: SSOMA/Residente lo piden una vez por proyecto, lo imprimen/pegan
        /// en la obra. El frontend arma la URL pública con este token.</summary>
        [HttpGet("grupo/proyecto-qr/{proyectoId:int}")]
        public async Task<IActionResult> GetQrProyecto(int proyectoId)
        {
            try { return Ok(new { token = await _service.GetOrCrearQrProyecto(proyectoId) }); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.GetQrProyecto"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpGet("grupo/publico/proyecto/{token:guid}/resumen")]
        [AllowAnonymous]
        public async Task<IActionResult> GetResumenProyectoPublico(Guid token)
        {
            try { return Ok(await _service.GetResumenProyectoPublico(token)); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.GetResumenProyectoPublico"); return StatusCode(500, new { message = "Error del servidor." }); }
        }

        [HttpPost("grupo/publico/proyecto/{token:guid}/init")]
        [AllowAnonymous]
        public async Task<IActionResult> GetInitPublico(Guid token, [FromBody] AtsGrupoInitPublicoRequestDto body)
        {
            try { return Ok(await _service.GetInitPublico(token, body)); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.GetInitPublico"); return StatusCode(500, new { message = "Error del servidor." }); }
        }

        [HttpPost("grupo/publico/proyecto/{token:guid}/crear")]
        [AllowAnonymous]
        public async Task<IActionResult> CrearGrupoPublico(Guid token, [FromBody] AtsGrupoCrearPublicoRequestDto body)
        {
            try { return Ok(await _service.CrearGrupoPublico(token, body)); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en AtsController.CrearGrupoPublico"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }
    }
}
