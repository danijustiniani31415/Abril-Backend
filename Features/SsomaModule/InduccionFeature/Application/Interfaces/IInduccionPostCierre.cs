namespace Abril_Backend.Features.SsomaModule.InduccionFeature.Application.Interfaces;

/// <summary>Sello de tiempo externo (RFC 3161) de una sesión cerrada. Falla en silencio hacia el llamador: un
/// sello pendiente no impide cerrar la inducción, se reintenta solo en segundo plano.</summary>
public interface IInduccionSelloService
{
    /// <summary>true si la sesión quedó sellada (ahora o antes). false si sigue pendiente o ya se rindió.</summary>
    Task<bool> SellarAsync(int sesionId, CancellationToken ct = default);
}

/// <summary>Copia de todos los archivos de una sesión cerrada (acta, firmas, selfies, versiones de documentos y
/// un manifiesto con hashes) en una cuenta de almacenamiento distinta a la de producción.</summary>
public interface IInduccionRespaldoService
{
    /// <summary>false si no hay cuenta de respaldo configurada (Induccion:Respaldo:ConnectionString).</summary>
    bool Configurado { get; }

    Task<bool> RespaldarAsync(int sesionId, CancellationToken ct = default);
}
