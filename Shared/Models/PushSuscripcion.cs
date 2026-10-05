namespace Abril_Backend.Shared.Models
{
    /// <summary>Suscripción Web Push de un navegador/celular de un usuario (tabla <c>push_suscripcion</c>).
    /// Un usuario puede tener varias (celular + PC). El endpoint es único por dispositivo.</summary>
    public class PushSuscripcion
    {
        public int PushSuscripcionId { get; set; }
        public int UserId { get; set; }
        public string Endpoint { get; set; } = null!;
        public string P256dh { get; set; } = null!;
        public string Auth { get; set; } = null!;
        public string? UserAgent { get; set; }
        public DateTimeOffset CreatedDateTime { get; set; } = DateTimeOffset.UtcNow;
    }
}
