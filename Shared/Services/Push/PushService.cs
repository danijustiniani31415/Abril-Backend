using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Shared.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using WebPush;

namespace Abril_Backend.Shared.Services.Push
{
    public class PushSuscripcionDto
    {
        public string Endpoint { get; set; } = string.Empty;
        public string P256dh { get; set; } = string.Empty;
        public string Auth { get; set; } = string.Empty;
    }

    public interface IPushService
    {
        string? ClavePublica { get; }
        Task Suscribir(int userId, PushSuscripcionDto dto, string? userAgent);
        Task Desuscribir(int userId, string endpoint);
        /// <summary>Envía el aviso a todos los dispositivos de esos usuarios. Best-effort: nunca lanza.</summary>
        Task EnviarAsync(IReadOnlyCollection<int> userIds, string titulo, string cuerpo, string? url);
    }

    /// <summary>Web Push gratuito (estándar VAPID, sin servicios de pago). Si no hay claves en la configuración
    /// (Vapid:PublicKey / Vapid:PrivateKey / Vapid:Subject) queda desactivado y no hace nada.</summary>
    public class PushService : IPushService
    {
        private readonly IDbContextFactory<AppDbContext> _factory;
        private readonly ILogger<PushService> _logger;
        private readonly string? _publica, _privada, _subject;

        public PushService(IDbContextFactory<AppDbContext> factory, IConfiguration config, ILogger<PushService> logger)
        {
            _factory = factory;
            _logger = logger;
            _publica = config["Vapid:PublicKey"];
            _privada = config["Vapid:PrivateKey"];
            _subject = config["Vapid:Subject"] ?? "mailto:soporte@abril.pe";
        }

        private bool Activo => !string.IsNullOrWhiteSpace(_publica) && !string.IsNullOrWhiteSpace(_privada);
        public string? ClavePublica => Activo ? _publica : null;

        public async Task Suscribir(int userId, PushSuscripcionDto dto, string? userAgent)
        {
            if (string.IsNullOrWhiteSpace(dto.Endpoint) || string.IsNullOrWhiteSpace(dto.P256dh) || string.IsNullOrWhiteSpace(dto.Auth))
                throw new Abril_Backend.Application.Exceptions.AbrilException("Suscripción inválida.", 400);
            using var ctx = _factory.CreateDbContext();
            var s = await ctx.PushSuscripcion.FirstOrDefaultAsync(x => x.Endpoint == dto.Endpoint);
            if (s == null)
            {
                s = new PushSuscripcion { Endpoint = dto.Endpoint };
                ctx.PushSuscripcion.Add(s);
            }
            s.UserId = userId; // el dispositivo pasa al usuario que acaba de iniciar sesión en él
            s.P256dh = dto.P256dh;
            s.Auth = dto.Auth;
            s.UserAgent = userAgent?.Length > 400 ? userAgent[..400] : userAgent;
            await ctx.SaveChangesAsync();
        }

        public async Task Desuscribir(int userId, string endpoint)
        {
            using var ctx = _factory.CreateDbContext();
            await ctx.PushSuscripcion.Where(x => x.UserId == userId && x.Endpoint == endpoint).ExecuteDeleteAsync();
        }

        public async Task EnviarAsync(IReadOnlyCollection<int> userIds, string titulo, string cuerpo, string? url)
        {
            if (!Activo || userIds.Count == 0) return;
            try
            {
                using var ctx = _factory.CreateDbContext();
                var subs = await ctx.PushSuscripcion.AsNoTracking().Where(x => userIds.Contains(x.UserId)).ToListAsync();
                if (subs.Count == 0) return;

                var payload = JsonSerializer.Serialize(new
                {
                    notification = new
                    {
                        title = titulo,
                        body = cuerpo,
                        icon = "/icons/icon-192.png",
                        badge = "/icons/icon-192.png",
                        vibrate = new[] { 200, 100, 200 },
                        data = new { onActionClick = new { @default = new { operation = "navigateLastFocusedOrOpen", url = url ?? "/" } } },
                    },
                });
                var vapid = new VapidDetails(_subject, _publica, _privada);
                var client = new WebPushClient();
                var caducadas = new List<int>();

                await Task.WhenAll(subs.Select(async s =>
                {
                    try { await client.SendNotificationAsync(new WebPush.PushSubscription(s.Endpoint, s.P256dh, s.Auth), payload, vapid); }
                    catch (WebPushException ex) when (ex.StatusCode is System.Net.HttpStatusCode.Gone or System.Net.HttpStatusCode.NotFound)
                    { lock (caducadas) caducadas.Add(s.PushSuscripcionId); }
                    catch (Exception ex) { _logger.LogWarning(ex, "No se pudo enviar el push a la suscripción {Id}", s.PushSuscripcionId); }
                }));

                if (caducadas.Count > 0)
                    await ctx.PushSuscripcion.Where(x => caducadas.Contains(x.PushSuscripcionId)).ExecuteDeleteAsync();
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Error enviando notificaciones push"); }
        }
    }
}
