namespace Abril_Backend.Features.SsomaModule.SenaleticaFeature.Application.Dtos
{
    public class SenalDto
    {
        public int Id { get; set; }
        public string Tipo { get; set; } = null!;
        public string Texto { get; set; } = null!;
        public string? Detalle { get; set; }
        public string? Icono { get; set; }
        public string? ImagenUrl { get; set; }
        public bool EsBasica { get; set; }
        public bool Activo { get; set; }
    }

    public class SenalUpsertDto
    {
        public string Tipo { get; set; } = null!;
        public string Texto { get; set; } = null!;
        public string? Detalle { get; set; }
        public string? Icono { get; set; }
    }
}
