using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

public static class YandexIamHelper
{
    /// <summary>
    /// Получает IAM-токен для Yandex Cloud через JWT на основе статических ключей SA.
    /// </summary>
    public static async Task<string> GetIamTokenAsync(
        string accessKeyId,
        string secretAccessKey,
        string folderIdOrCloudId)
    {
        var jwt = CreateJwt(accessKeyId, secretAccessKey);

        var httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://iam.api.cloud.yandex.net")
        };

        // eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCIsImtpZCI6IllDQUpFWVo3cXhERm1VV2JkWG14WVJqVWgifQ.eyJpc3MiOiJZQ0FKRVlaN3F4REZtVVdiZFhteFlSalVoIiwiaWF0IjoxNzgyNzc3OTA4LCJleHAiOjE3ODI3ODE1MDh9.UQ7DPPVkM-A-YQkhXjkTn8bAs-vpdZ3YQSERPAkcSCI
        var payload = new
        {
            jwt = jwt
        };

        var content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        using var response = await httpClient.PostAsync("/iam/v1/tokens", content);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(result);
        var root = doc.RootElement;

        if (!root.TryGetProperty("iamToken", out var tokenElement))
            throw new InvalidOperationException("Ответ IAM не содержит iamToken");

        return tokenElement.GetString()!;
    }

    private static string CreateJwt(string accessKeyId, string secretAccessKey)
    {
        const int ExpirationSeconds = 3600;

        long iat = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        long exp = iat + ExpirationSeconds;

        var kid = accessKeyId;

        // Header
        var header = new
        {
            alg = "HS256",
            typ = "JWT",
            kid = kid
        };

        // Payload: aud задаём жёсткой строкой, чтобы исключить любые вариации формата
        var payloadJson = $@"{{
        ""iss"": ""{accessKeyId}"",
        ""sub"": ""{accessKeyId}"",
        ""aud"": ""iam.api.cloud.yandex.net"",
        ""iat"": {iat},
        ""exp"": {exp}
    }}";

        var headerJson = JsonSerializer.Serialize(header);
        var headerB64 = Base64UrlEncode(Encoding.UTF8.GetBytes(headerJson));
        var payloadB64 = Base64UrlEncode(Encoding.UTF8.GetBytes(payloadJson));

        var signingInput = $"{headerB64}.{payloadB64}";

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretAccessKey));
        var signatureBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(signingInput));
        var signatureB64 = Base64UrlEncode(signatureBytes);

        return $"{headerB64}.{payloadB64}.{signatureB64}";
    }


    // Base64URL без padding '=' и с заменой + -> -, / -> _
    private static string Base64UrlEncode(byte[] input)
    {
        var base64 = Convert.ToBase64String(input);
        base64 = base64.TrimEnd('=');
        base64 = base64.Replace('+', '-').Replace('/', '_');
        return base64;
    }
}
