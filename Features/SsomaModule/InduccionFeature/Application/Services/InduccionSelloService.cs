using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using Abril_Backend.Features.SsomaModule.InduccionFeature.Application.Interfaces;
using Abril_Backend.Features.SsomaModule.InduccionFeature.Infrastructure.Interfaces;

namespace Abril_Backend.Features.SsomaModule.InduccionFeature.Application.Services;

/// <summary>
/// Sellado de tiempo RFC 3161. Una Autoridad de Sellado de Tiempo (TSA) externa firma «este hash existía en esta
/// fecha», así la fecha del acta no depende solo de nuestra base de datos. Se sella
/// SHA-256( SHA-256 del PDF | último hash de la auditoría ): queda sellado el archivo Y la cadena de eventos.
/// La TSA es configurable (<c>Induccion:Tsa:Url</c>, por defecto la de DigiCert, gratuita y estándar).
/// El token (.tsr) se guarda completo: cualquiera puede verificarlo con OpenSSL sin depender de este sistema.
/// </summary>
public class InduccionSelloService : IInduccionSelloService
{
    public const string TsaPorDefecto = "http://timestamp.digicert.com";
    private const int MaxIntentos = 10;
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };

    private readonly IInduccionPostCierreRepository _repository;
    private readonly IConfiguration _config;
    private readonly ILogger<InduccionSelloService> _log;

    public InduccionSelloService(IInduccionPostCierreRepository repository, IConfiguration config, ILogger<InduccionSelloService> log)
    {
        _repository = repository;
        _config = config;
        _log = log;
    }

    public async Task<bool> SellarAsync(int sesionId, CancellationToken ct = default)
    {
        var url = _config["Induccion:Tsa:Url"];
        if (string.IsNullOrWhiteSpace(url)) url = TsaPorDefecto;

        var sello = await _repository.PrepararSello(sesionId, url);
        if (sello is null) return false;                 // la sesión todavía no tiene acta
        if (sello.Estado == "Sellado") return true;
        if (sello.Estado == "Error") return false;       // se rindió: solo se reintenta a mano

        try
        {
            var digest = Convert.FromHexString(sello.Digest);
            var request = Rfc3161TimestampRequest.CreateFromHash(
                digest, HashAlgorithmName.SHA256,
                nonce: RandomNumberGenerator.GetBytes(8),
                requestSignerCertificates: true);

            using var content = new ByteArrayContent(request.Encode());
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/timestamp-query");
            using var respuesta = await Http.PostAsync(sello.Proveedor, content, ct);
            respuesta.EnsureSuccessStatusCode();
            var bytes = await respuesta.Content.ReadAsByteArrayAsync(ct);

            // ProcessResponse valida que la respuesta corresponda a ESTA solicitud (hash y nonce) y que la TSA la concedió.
            var token = request.ProcessResponse(bytes, out _);
            if (!token.VerifySignatureForHash(digest, HashAlgorithmName.SHA256, out var firmante, null))
                throw new CryptographicException("La firma del sello de tiempo no es válida.");

            var info = token.TokenInfo;
            await _repository.RegistrarIntentoSello(sello.Id, true, bytes, info.Timestamp.UtcDateTime,
                Convert.ToHexString(info.GetSerialNumber().Span), firmante?.Subject, null, MaxIntentos);
            _log.LogInformation("[Inducción] Sesión {Id} sellada por {Tsa} ({Hora:O})", sesionId, sello.Proveedor, info.Timestamp);
            return true;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "[Inducción] No se pudo sellar la sesión {Id} (intento {N})", sesionId, sello.Intentos + 1);
            await _repository.RegistrarIntentoSello(sello.Id, false, null, null, null, null, ex.Message, MaxIntentos);
            return false;
        }
    }
}
