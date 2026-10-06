using System.Security.Claims;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.SsomaModule.InduccionFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.InduccionFeature.Application.Interfaces;
using Abril_Backend.Shared.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abril_Backend.Features.SsomaModule.InduccionFeature.Presentation
{
    /// <summary>
    /// Inducción digital. Lo del instructor/coordinador exige login + feature; lo del trabajador
    /// (<c>publico/{token}</c>) es [AllowAnonymous]: el token del QR de la sesión es el único candado,
    /// igual que el ATS grupal (DNI parcial + selfie + GPS + firma dentro del flujo).
    /// </summary>
    [ApiController]
    [Route("api/v1/ssoma/induccion")]
    [Authorize]
    [RequireFeature("ssoma.gestion.induccion")]
    public class InduccionController : ControllerBase
    {
        private readonly IInduccionService _service;
        private readonly IInduccionVigenciaService _vigencia;
        private readonly ILogger<InduccionController> _logger;

        public InduccionController(IInduccionService service, IInduccionVigenciaService vigencia, ILogger<InduccionController> logger)
        {
            _service = service;
            _vigencia = vigencia;
            _logger = logger;
        }

        private int CurrentUserId()
        {
            var val = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(val, out var id))
                throw new AbrilException("No se pudo identificar al usuario.", 401);
            return id;
        }

        private string? Ip() => HttpContext.Connection.RemoteIpAddress?.ToString();

        /// <summary>Misma forma de manejo de errores que el resto de controladores: AbrilException → su status;
        /// cualquier otra cosa → 500 con mensaje fijo.</summary>
        private async Task<IActionResult> Ejecutar(string accion, Func<Task<IActionResult>> cuerpo)
        {
            try { return await cuerpo(); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en InduccionController.{Accion}", accion);
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        // ── Catálogo de documentos ──────────────────────────────────────────

        [HttpGet("documentos")]
        public Task<IActionResult> ListarDocumentos([FromQuery] bool soloActivos = false)
            => Ejecutar(nameof(ListarDocumentos), async () => Ok(await _service.ListarDocumentos(soloActivos)));

        [HttpPost("documentos")]
        public Task<IActionResult> GuardarDocumento([FromBody] IndDocumentoGuardarDto dto)
            => Ejecutar(nameof(GuardarDocumento), async () => Ok(await _service.GuardarDocumento(dto)));

        /// <summary>Sube una versión nueva del documento (la anterior queda intacta). multipart: archivo, esCambioMaterial, nota.</summary>
        [HttpPost("documentos/{id:int}/versiones")]
        public Task<IActionResult> SubirVersion(int id, IFormFile archivo, [FromForm] bool esCambioMaterial = false, [FromForm] string? nota = null)
            => Ejecutar(nameof(SubirVersion), async () =>
            {
                if (archivo == null || archivo.Length == 0)
                    throw new AbrilException("El archivo está vacío.", 400);
                await using var stream = archivo.OpenReadStream();
                return Ok(await _service.SubirVersionDocumento(id, stream, archivo.FileName, esCambioMaterial, nota, CurrentUserId()));
            });

        [HttpGet("documentos/{id:int}/versiones")]
        public Task<IActionResult> ListarVersiones(int id)
            => Ejecutar(nameof(ListarVersiones), async () => Ok(await _service.ListarVersiones(id)));

        // ── Temario ─────────────────────────────────────────────────────────

        [HttpGet("temas")]
        public Task<IActionResult> ListarTemas([FromQuery] bool soloActivos = false)
            => Ejecutar(nameof(ListarTemas), async () => Ok(await _service.ListarTemas(soloActivos)));

        [HttpPost("temas")]
        public Task<IActionResult> GuardarTema([FromBody] IndTemaGuardarDto dto)
            => Ejecutar(nameof(GuardarTema), async () => Ok(await _service.GuardarTema(dto)));

        // ── Banco de preguntas y evaluación ─────────────────────────────────

        [HttpGet("preguntas")]
        public Task<IActionResult> ListarPreguntas([FromQuery] bool soloActivas = false)
            => Ejecutar(nameof(ListarPreguntas), async () => Ok(await _service.ListarPreguntas(soloActivas)));

        [HttpPost("preguntas")]
        public Task<IActionResult> GuardarPregunta([FromBody] IndPreguntaGuardarDto dto)
            => Ejecutar(nameof(GuardarPregunta), async () => Ok(await _service.GuardarPregunta(dto)));

        [HttpGet("evaluaciones")]
        public Task<IActionResult> ListarEvaluaciones()
            => Ejecutar(nameof(ListarEvaluaciones), async () => Ok(await _service.ListarEvaluaciones()));

        /// <summary>Congela el banco de preguntas activas en una versión nueva de la evaluación.</summary>
        [HttpPost("evaluaciones/publicar")]
        public Task<IActionResult> PublicarEvaluacion([FromBody] IndEvaluacionPublicarDto dto)
            => Ejecutar(nameof(PublicarEvaluacion), async () => Ok(await _service.PublicarEvaluacion(dto, CurrentUserId())));

        // ── Sesión (instructor) ─────────────────────────────────────────────

        [HttpGet("proyectos/{proyectoId:int}/trabajadores")]
        public Task<IActionResult> GetTrabajadoresProyecto(int proyectoId)
            => Ejecutar(nameof(GetTrabajadoresProyecto), async () => Ok(await _service.GetWorkersProyecto(proyectoId)));

        [HttpPost("sesiones")]
        public Task<IActionResult> CrearSesion([FromBody] IndSesionCrearDto dto)
            => Ejecutar(nameof(CrearSesion), async () => Ok(await _service.CrearSesion(CurrentUserId(), dto, Ip())));

        [HttpGet("sesiones")]
        public Task<IActionResult> ListarSesiones([FromQuery] IndSesionFiltroDto filtro)
            => Ejecutar(nameof(ListarSesiones), async () => Ok(await _service.ListarSesiones(filtro)));

        /// <summary>Detalle en vivo: el instructor lo consulta cada pocos segundos para ver quién ya firmó.</summary>
        [HttpGet("sesiones/{id:int}")]
        public Task<IActionResult> GetSesion(int id)
            => Ejecutar(nameof(GetSesion), async () => Ok(await _service.GetSesion(id)));

        [HttpPost("sesiones/{id:int}/cerrar")]
        public Task<IActionResult> CerrarSesion(int id, [FromBody] IndCerrarSesionDto body)
            => Ejecutar(nameof(CerrarSesion), async () =>
            {
                await _service.CerrarSesion(id, CurrentUserId(), body, Ip());
                return Ok(new { message = "Inducción cerrada. El acta quedó generada." });
            });

        [HttpPost("sesiones/{id:int}/anular")]
        public Task<IActionResult> AnularSesion(int id, [FromBody] IndAnularDto body)
            => Ejecutar(nameof(AnularSesion), async () =>
            {
                await _service.AnularSesion(id, CurrentUserId(), body.Motivo, Ip());
                return Ok(new { message = "Inducción anulada." });
            });

        /// <summary>Reintenta a mano el sello de tiempo externo (cuando la TSA falló demasiadas veces).</summary>
        [HttpPost("sesiones/{id:int}/sellar")]
        public Task<IActionResult> ReintentarSello(int id)
            => Ejecutar(nameof(ReintentarSello), async () => Ok(await _service.ReintentarSello(id)));

        /// <summary>Reintenta a mano el respaldo en la cuenta de almacenamiento secundaria.</summary>
        [HttpPost("sesiones/{id:int}/respaldar")]
        public Task<IActionResult> ReintentarRespaldo(int id)
            => Ejecutar(nameof(ReintentarRespaldo), async () => Ok(await _service.ReintentarRespaldo(id)));

        /// <summary>Descarga el token RFC 3161 (.tsr) para dárselo a un auditor.</summary>
        [HttpGet("sesiones/{id:int}/sello/archivo")]
        public Task<IActionResult> DescargarSello(int id)
            => Ejecutar(nameof(DescargarSello), async () =>
            {
                var (bytes, nombre) = await _service.DescargarSello(id);
                return File(bytes, "application/timestamp-reply", nombre);
            });

        [HttpPost("sesiones/{id:int}/asistencias/{asistenciaId:int}/validar")]
        public Task<IActionResult> ValidarAsistencia(int id, int asistenciaId)
            => Ejecutar(nameof(ValidarAsistencia), async () =>
            {
                await _service.ValidarAsistencia(id, asistenciaId, CurrentUserId(), Ip());
                return Ok(new { message = "Asistencia validada." });
            });

        [HttpPost("sesiones/{id:int}/asistencias/{asistenciaId:int}/rechazar")]
        public Task<IActionResult> RechazarAsistencia(int id, int asistenciaId, [FromBody] IndRechazarAsistenciaDto body)
            => Ejecutar(nameof(RechazarAsistencia), async () =>
            {
                await _service.RechazarAsistencia(id, asistenciaId, CurrentUserId(), body.Motivo, Ip());
                return Ok(new { message = "Asistencia rechazada: el trabajador puede volver a firmar." });
            });

        // ── Cumplimiento: vigencia por proyecto, bloqueo del ATS, exenciones, expediente ──

        [HttpGet("cumplimiento/{proyectoId:int}")]
        public Task<IActionResult> GetCumplimiento(int proyectoId)
            => Ejecutar(nameof(GetCumplimiento), async () => Ok(await _vigencia.GetCumplimiento(proyectoId)));

        /// <summary>Enciende o apaga el bloqueo «sin inducción vigente no se firma ATS» de este proyecto.</summary>
        [HttpPost("cumplimiento/{proyectoId:int}/bloqueo")]
        public Task<IActionResult> SetBloqueo(int proyectoId, [FromBody] IndGateDto body)
            => Ejecutar(nameof(SetBloqueo), async () =>
            {
                await _vigencia.SetGate(proyectoId, body.Activo, CurrentUserId());
                return Ok(new { message = body.Activo ? "Bloqueo del ATS encendido." : "Bloqueo del ATS apagado." });
            });

        [HttpPost("exenciones")]
        public Task<IActionResult> CrearExencion([FromBody] IndExencionCrearDto dto)
            => Ejecutar(nameof(CrearExencion), async () =>
            {
                await _vigencia.CrearExencion(dto, CurrentUserId());
                return Ok(new { message = "Exención registrada." });
            });

        [HttpPost("exenciones/{id:int}/revocar")]
        public Task<IActionResult> RevocarExencion(int id)
            => Ejecutar(nameof(RevocarExencion), async () =>
            {
                await _vigencia.RevocarExencion(id, CurrentUserId());
                return Ok(new { message = "Exención revocada." });
            });

        [HttpGet("trabajadores/{workerId:int}/expediente")]
        public Task<IActionResult> GetExpediente(int workerId)
            => Ejecutar(nameof(GetExpediente), async () => Ok(await _vigencia.GetExpediente(workerId)));

        // ── Público: el trabajador desde su celular (sin login) ─────────────

        [HttpGet("publico/{token:guid}/resumen")]
        [AllowAnonymous]
        public Task<IActionResult> GetResumenPublico(Guid token)
            => Ejecutar(nameof(GetResumenPublico), async () => Ok(await _service.GetResumenPublico(token)));

        [HttpGet("publico/{token:guid}/trabajadores")]
        [AllowAnonymous]
        public Task<IActionResult> GetTrabajadoresPublico(Guid token)
            => Ejecutar(nameof(GetTrabajadoresPublico), async () => Ok(await _service.GetTrabajadoresPublico(token)));

        [HttpPost("publico/{token:guid}/estado")]
        [AllowAnonymous]
        public Task<IActionResult> GetEstadoPublico(Guid token, [FromBody] IndIdentidadDto body)
            => Ejecutar(nameof(GetEstadoPublico), async () => Ok(await _service.GetEstadoPublico(token, body)));

        [HttpPost("publico/{token:guid}/documento-recibido")]
        [AllowAnonymous]
        public Task<IActionResult> RegistrarDocumentoRecibido(Guid token, [FromBody] IndDocumentoRecibidoDto body)
            => Ejecutar(nameof(RegistrarDocumentoRecibido), async () =>
            {
                await _service.RegistrarDocumentoRecibido(token, body);
                return Ok(new { message = "Recepción registrada." });
            });

        [HttpPost("publico/{token:guid}/evaluacion")]
        [AllowAnonymous]
        public Task<IActionResult> GetEvaluacionPublica(Guid token, [FromBody] IndIdentidadDto body)
            => Ejecutar(nameof(GetEvaluacionPublica), async () => Ok(await _service.GetEvaluacionPublica(token, body)));

        [HttpPost("publico/{token:guid}/evaluacion/responder")]
        [AllowAnonymous]
        public Task<IActionResult> ResponderEvaluacion(Guid token, [FromBody] IndResponderEvaluacionDto body)
            => Ejecutar(nameof(ResponderEvaluacion), async () => Ok(await _service.ResponderEvaluacion(token, body)));

        [HttpPost("publico/{token:guid}/firmar")]
        [AllowAnonymous]
        public Task<IActionResult> Firmar(Guid token, [FromBody] IndFirmarDto body)
            => Ejecutar(nameof(Firmar), async () =>
            {
                await _service.Firmar(token, body, Ip(), Request.Headers.UserAgent.ToString());
                return Ok(new { message = "Inducción firmada correctamente." });
            });

        // ── Verificación pública del acta (QR del PDF) ──────────────────────

        [HttpGet("{id:int}/verificar-publico")]
        [AllowAnonymous]
        public Task<IActionResult> Verificar(int id, [FromQuery] string? hash)
            => Ejecutar(nameof(Verificar), async () => Ok(await _service.Verificar(id, hash)));
    }
}
