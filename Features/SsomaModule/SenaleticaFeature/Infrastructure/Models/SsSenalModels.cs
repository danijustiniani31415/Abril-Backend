namespace Abril_Backend.Features.SsomaModule.SenaleticaFeature.Infrastructure.Models
{
    // Señal del catálogo de señalética (NTP 399.010-1). Tipo: prohibicion | advertencia |
    // obligacion | emergencia | incendio. El pictograma es un ícono de la biblioteca (Icono)
    // o una imagen propia subida por SSOMA (ImagenUrl, que tiene prioridad). Las básicas
    // (EsBasica) vienen sembradas por SQL y no se pueden eliminar, solo desactivar.
    public class SsSenal
    {
        public int Id { get; set; }
        public string Tipo { get; set; } = null!;
        public string Texto { get; set; } = null!;
        public string? Detalle { get; set; }
        public string? Icono { get; set; }
        public string? ImagenUrl { get; set; }
        public bool EsBasica { get; set; }
        public bool Activo { get; set; }
        public int Orden { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public int? CreatedById { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }
        public int? UpdatedById { get; set; }
    }
}
