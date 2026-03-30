using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using UnleashedPOTest.Models;

namespace UnleashedPOTest;

/// <summary>
/// Unleashed REST API client.
///
/// Authentication: HMAC-SHA256 over the query string, base64-encoded.
///   GET  — signed over the full query string (e.g. "orderNumber=PO-00000001&serialBatch=true")
///   PUT  — signed over the query string (empty string if no query params)
///   POST — signed over the query string (empty string if no query params)
///
/// Note: serialBatch=true must be included on GET requests to return PendingBatchNumbers.
/// </summary>
public sealed class UnleashedApiClient(IHttpClientFactory httpClientFactory, string apiKey) : IUnleashedApiClient
{
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    // ─── Purchase Orders ──────────────────────────────────────────────────────

    public async Task<PurchaseOrder?> GetPurchaseOrderAsync(string orderNumber, CancellationToken ct = default)
    {
        var queryString = $"orderNumber={Uri.EscapeDataString(orderNumber)}&orderStatus=Placed&serialBatch=true";
        Log($"GET /PurchaseOrders/1?{queryString}");  // Only returns the order if status is Placed

        var response = await SendAsync<PurchaseOrderResponse>(HttpMethod.Get, "PurchaseOrders/1", queryString, body: null, ct);
        return response?.Items?.FirstOrDefault();
    }

    public async Task UpdatePurchaseOrderLineAsync(string orderGuid, string lineGuid, PurchaseOrderLineUpdate request, CancellationToken ct = default)
    {
        Log($"PUT /PurchaseOrders/{orderGuid}/Lines/{lineGuid}");

        await SendAsync<object>(HttpMethod.Put, $"PurchaseOrders/{orderGuid}/Lines/{lineGuid}", queryString: string.Empty, request, ct);
    }

    public async Task ReceiptPurchaseOrderAsync(string orderGuid, CancellationToken ct = default)
    {
        Log($"POST /PurchaseOrders/{orderGuid}/Receipt");

        await SendAsync<object>(HttpMethod.Post, $"PurchaseOrders/{orderGuid}/Receipt", queryString: string.Empty, body: null, ct);
    }

    // ─── Core send ────────────────────────────────────────────────────────────

    private async Task<TResponse?> SendAsync<TResponse>(
        HttpMethod method,
        string resource,
        string queryString,
        object? body,
        CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient(nameof(UnleashedApiClient));

        var url = string.IsNullOrEmpty(queryString)
            ? resource
            : $"{resource}?{queryString}";

        using var request = new HttpRequestMessage(method, url);
        request.Headers.Add("api-auth-signature", ComputeSignature(queryString));

        if (body is not null)
        {
            var json = JsonSerializer.Serialize(body, WriteOptions);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            Log("Request body:");
            Log(json);
        }

        using var response = await client.SendAsync(request, ct);
        var responseBody   = await response.Content.ReadAsStringAsync(ct);

        Log($"Response: {(int)response.StatusCode} {response.StatusCode}");
        Log("Response body:");
        Log(responseBody);

        if (!response.IsSuccessStatusCode)
            throw new UnleashedApiException((int)response.StatusCode, responseBody);

        if (string.IsNullOrWhiteSpace(responseBody))
            return default;

        return JsonSerializer.Deserialize<TResponse>(responseBody, ReadOptions);
    }

    // ─── Auth ─────────────────────────────────────────────────────────────────

    private string ComputeSignature(string queryString)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(apiKey));
        var hash       = hmac.ComputeHash(Encoding.UTF8.GetBytes(queryString));
        return Convert.ToBase64String(hash);
    }

    // ─── Logging ──────────────────────────────────────────────────────────────

    private static void Log(string message) =>
        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}");
}
