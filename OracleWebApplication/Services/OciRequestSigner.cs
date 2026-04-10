using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using OracleWebApplication.Models;

namespace OracleWebApplication.Services;

/// <summary>
/// Signs outgoing HTTP requests using OCI's HTTP Signature authentication.
/// See: https://docs.oracle.com/en-us/iaas/Content/API/Concepts/signingrequests.htm
/// </summary>
public class OciRequestSigner
{
    private readonly OciApiSettings _settings;

    public OciRequestSigner(IOptions<OciApiSettings> settings)
    {
        _settings = settings.Value;
    }

    public string KeyId =>
        $"{_settings.TenancyOcid}/{_settings.UserOcid}/{_settings.KeyFingerprint}";

    /// <summary>
    /// Signs the given <see cref="HttpRequestMessage"/> in place by setting
    /// the required date, host, content, and authorization headers.
    /// </summary>
    public void SignRequest(HttpRequestMessage request)
    {
        var now = DateTime.UtcNow.ToString("R");
        request.Headers.Date = DateTimeOffset.UtcNow;

        var host = request.RequestUri!.Host;
        var target = $"{request.Method.Method.ToLowerInvariant()} {request.RequestUri.PathAndQuery}";

        var headersToSign = new List<string> { "(request-target)", "date", "host" };
        var signingParts = new List<string>
        {
            $"(request-target): {target}",
            $"date: {request.Headers.Date.Value.ToString("R")}",
            $"host: {host}"
        };

        if (request.Content is not null)
        {
            var body = request.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
            var contentSha = SHA256.HashData(body);
            var contentHash = Convert.ToBase64String(contentSha);
            var contentLength = body.Length.ToString();

            request.Content.Headers.ContentType =
                new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
            request.Headers.TryAddWithoutValidation("x-content-sha256", contentHash);
            request.Content.Headers.ContentLength = body.Length;

            headersToSign.AddRange(["content-length", "content-type", "x-content-sha256"]);
            signingParts.Add($"content-length: {contentLength}");
            signingParts.Add($"content-type: application/json");
            signingParts.Add($"x-content-sha256: {contentHash}");
        }

        var signingString = string.Join("\n", signingParts);
        var signature = SignWithRsaSha256(signingString);

        var headersList = string.Join(" ", headersToSign);
        var authHeader = $"""Signature version="1",keyId="{KeyId}",algorithm="rsa-sha256",headers="{headersList}",signature="{signature}" """.Trim();

        request.Headers.TryAddWithoutValidation("Authorization", authHeader);
    }

    private string SignWithRsaSha256(string signingString)
    {
        using var rsa = RSA.Create();
        var keyPem = File.ReadAllText(_settings.PrivateKeyPath);
        rsa.ImportFromPem(keyPem);

        var dataBytes = Encoding.UTF8.GetBytes(signingString);
        var signatureBytes = rsa.SignData(dataBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return Convert.ToBase64String(signatureBytes);
    }
}
