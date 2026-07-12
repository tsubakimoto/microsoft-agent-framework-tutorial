using Azure.Core;
using Azure.Identity;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Http.Headers;

/// <summary>
/// HTTP-trigger proxy: accepts an API key (Functions function key) and forwards
/// the raw request body to the Foundry hosted-agent invocations endpoint,
/// inserting a Managed Identity Bearer token for Entra authentication.
/// </summary>
public class InvocationsProxyFunction(
    DefaultAzureCredential credential,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration)
{
    private static readonly TokenRequestContext FoundryScope =
        new(["https://ai.azure.com/.default"]);

    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("foundry");

    private readonly string _foundryEndpoint =
        configuration["FOUNDRY_INVOCATIONS_ENDPOINT"]
        ?? throw new InvalidOperationException("FOUNDRY_INVOCATIONS_ENDPOINT is not set.");

    [Function(nameof(InvocationsProxyFunction))]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "invocations")]
        HttpRequestData req,
        CancellationToken cancellationToken)
    {
        // Acquire Entra token via Managed Identity (DefaultAzureCredential caches internally)
        var tokenResult = await credential.GetTokenAsync(FoundryScope, cancellationToken);

        // Build upstream URL, forwarding caller query params except "code" (Functions key)
        var upstreamUrl = BuildUpstreamUrl(_foundryEndpoint, req.Url.Query);

        using var upstream = new HttpRequestMessage(HttpMethod.Post, upstreamUrl);
        upstream.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenResult.Token);
        upstream.Content = new StreamContent(req.Body);

        if (req.Headers.TryGetValues("Content-Type", out var contentTypeValues))
            upstream.Content.Headers.TryAddWithoutValidation("Content-Type", contentTypeValues);

        using var upstreamResp = await _httpClient.SendAsync(
            upstream, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        var response = req.CreateResponse((HttpStatusCode)upstreamResp.StatusCode);

        var respContentType = upstreamResp.Content.Headers.ContentType?.ToString();
        if (respContentType is not null)
        {
            response.Headers.Remove("Content-Type");
            response.Headers.Add("Content-Type", respContentType);
        }

        await upstreamResp.Content.CopyToAsync(response.Body, cancellationToken);
        return response;
    }

    // Append caller query params to the base Foundry URL, skipping "code" (Functions key).
    private static string BuildUpstreamUrl(string baseEndpoint, string callerQuery)
    {
        var extras = callerQuery
            .TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Split('=', 2))
            .Where(p => !p[0].Equals("code", StringComparison.OrdinalIgnoreCase))
            .Select(p => string.Join("=", p));

        var extraStr = string.Join("&", extras);
        if (string.IsNullOrEmpty(extraStr))
            return baseEndpoint;

        return baseEndpoint + (baseEndpoint.Contains('?') ? "&" : "?") + extraStr;
    }
}
