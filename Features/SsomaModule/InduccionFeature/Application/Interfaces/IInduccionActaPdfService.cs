using Abril_Backend.Features.SsomaModule.InduccionFeature.Application.Dtos;

namespace Abril_Backend.Features.SsomaModule.InduccionFeature.Application.Interfaces;

/// <summary>Todo lo que el acta necesita, ya resuelto (imágenes descargadas) para que el generador de PDF sea puro.</summary>
public class ActaInduccionDatos
{
    public IndSesionDetalleDto Sesion { get; set; } = new();
    public string DeclaracionVersion { get; set; } = string.Empty;
    public string DeclaracionTexto { get; set; } = string.Empty;
    public string VerificacionUrl { get; set; } = string.Empty;
    public byte[]? Logo { get; set; }
    public byte[]? FirmaInstructor { get; set; }
    /// <summary>Por id de asistencia.</summary>
    public Dictionary<int, byte[]?> FirmasTrabajadores { get; set; } = [];
    public Dictionary<int, byte[]?> SelfiesTrabajadores { get; set; } = [];
}

public interface IInduccionActaPdfService
{
    byte[] Generar(ActaInduccionDatos datos);
}
