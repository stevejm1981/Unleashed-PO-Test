# SKILL: Unleashed API Reference Project

## Overview

This skill encodes the architecture and patterns used to build Unleashed API reference
console applications for the SupplyLens developer series. Each project demonstrates a
specific Unleashed API flow with full logging, typed models, and production-quality
structure.

Reference projects in this series:
- `Unleashed-Shipment-Test` — Sales Shipment batch allocation and dispatch
- `Unleashed-PO-Test` — Purchase Order batch allocation and receipt

---

## When to Use This Skill

Use this skill when asked to:
- Create a new Unleashed API reference project
- Add a new flow to an existing reference project
- Ensure consistency across projects in the series

---

## Project Structure

Every project follows this layout:

```
{ProjectName}/
  Configuration/
    Settings.cs               — Typed settings records bound from appsettings.json
  Models/
    UnleashedModels.cs        — Immutable record types for all API shapes
  appsettings.json            — Credentials and flow-specific config (placeholders only)
  I{Name}ApiClient.cs         — Interface for the Unleashed HTTP client
  UnleashedApiClient.cs       — HTTP client implementation
  UnleashedApiException.cs    — Typed exception for non-success API responses
  {Flow}Service.cs            — Service that orchestrates the flow (e.g. DispatchService, ReceiptService)
  Program.cs                  — Composition root only: config, DI, entry point
  README.md                   — Full documentation including API notes
```

---

## Mandatory Design Patterns (.NET 9)

### Models — immutable records
All request and response shapes use positional `record` types with `[JsonPropertyName]`
on each property. Never use mutable classes for DTOs.

```csharp
public record PurchaseOrder(
    [property: JsonPropertyName("Guid")]        string?                  Guid,
    [property: JsonPropertyName("OrderNumber")] string?                  OrderNumber,
    [property: JsonPropertyName("OrderStatus")] string?                  OrderStatus,
    [property: JsonPropertyName("PurchaseOrderLines")] List<PurchaseOrderLine>? PurchaseOrderLines);
```

### HTTP client — IHttpClientFactory
Never instantiate `HttpClient` directly. Always use `IHttpClientFactory` with a named
client registered in DI. Base address and auth headers are set at registration time.

```csharp
services.AddHttpClient(nameof(UnleashedApiClient), client =>
{
    client.BaseAddress = new Uri("https://api.unleashedsoftware.com");
    client.DefaultRequestHeaders.Add("api-auth-id", unleashedSettings.ApiId);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});
```

### Interface
Every client is behind an interface to support testing and substitution.

```csharp
public interface IUnleashedApiClient
{
    Task<PurchaseOrder?> GetPurchaseOrderAsync(string orderNumber, CancellationToken ct = default);
    Task<PurchaseOrder?> UpdatePurchaseOrderAsync(string orderGuid, PurchaseOrderUpdateRequest request, CancellationToken ct = default);
    Task ReceiptPurchaseOrderAsync(string orderGuid, CancellationToken ct = default);
}
```

### Primary constructors
Use C# 12 primary constructors on services and the API client.

```csharp
public sealed class ReceiptService(IUnleashedApiClient apiClient)
public sealed class UnleashedApiClient(IHttpClientFactory httpClientFactory, string apiKey)
```

### Typed exception

```csharp
public sealed class UnleashedApiException : Exception
{
    public int StatusCode { get; }
    public string? ResponseBody { get; }
}
```

### Settings — typed records from appsettings.json

```csharp
public sealed record UnleashedSettings
{
    public string ApiId  { get; init; } = string.Empty;
    public string ApiKey { get; init; } = string.Empty;
}
```

### Single SendAsync method
All HTTP verbs are handled by one generic `SendAsync` method in the client. Never write
separate `GetAsync`, `PostAsync`, `PutAsync` helpers — consolidate into one.

---

## Unleashed HMAC-SHA256 Signing Rules

This is the most commonly misunderstood part of the Unleashed API.

| Verb | Sign over |
|------|-----------|
| GET  | The full query string (e.g. `orderNumber=PO-00001&serialBatch=true`) |
| PUT  | The query string — empty string `""` if no query params |
| POST | The query string — empty string `""` if no query params |

**Common mistakes:**
- Signing over empty string on GET → 403 Forbidden
- Appending query params to the URL without including them in the signed string → 403 Forbidden
- Signing over the full URL instead of just the query string → 403 Forbidden

```csharp
private string ComputeSignature(string queryString)
{
    using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(apiKey));
    var hash       = hmac.ComputeHash(Encoding.UTF8.GetBytes(queryString));
    return Convert.ToBase64String(hash);
}
```

---

## Unleashed API Behaviour — Key Rules by Endpoint

### Sales Shipments
- POST to `/SalesShipments` (Guid in body, not URL)
- `serialBatch=true` required on POST and PUT — without it batches are silently discarded
- PUT requires the **complete payload** — omitted fields are cleared
- Create in `Parked` status first, then PUT with batch numbers and `Dispatched`

### Purchase Orders
- GET requires `serialBatch=true` to return `PendingBatchNumbers` and `BatchNumbers`
- PUT is lenient — only send fields being updated plus `Supplier` and `OrderStatus`
- `PendingBatchNumbers` on PUT lines remain pending until POST `/Receipt` is called
- POST `/Receipt` has no request body — Guid in URL is sufficient
- After receipting, GET the order again to confirm `BatchNumbers` (not `PendingBatchNumbers`) are populated

---

## Configuration Pattern

Every project uses `appsettings.json` with placeholder values. Credentials are never
hardcoded in source files.

```json
{
  "Unleashed": {
    "ApiId":  "YOUR_API_ID",
    "ApiKey": "YOUR_API_KEY"
  }
}
```

Placeholder validation runs at startup before any API calls are made:

```csharp
if (unleashedSettings.ApiId == "YOUR_API_ID" || unleashedSettings.ApiKey == "YOUR_API_KEY")
    throw new InvalidOperationException("API credentials have not been set.");
```

---

## Logging Pattern

All log output uses a shared static `Log` helper with a timestamp prefix. No colour,
no emojis. Step labels use `Step N/N -` format. Field values are indented with consistent
label alignment.

```csharp
private static void Log(string message) =>
    Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}");
```

Output format:
```
[2026-03-29 14:32:01] Step 1/3 - Retrieving Purchase Order PO-00001234...
[2026-03-29 14:32:01]          Order Number : PO-00001234
[2026-03-29 14:32:01]          Order Guid   : 6b35620c-9a43-44a3-b156-f10656b02710
[2026-03-29 14:32:01]          Status       : Placed
```

---

## README Structure

Every project README must include:
1. One-line description + series attribution (link to supplylens.co.uk)
2. Overview — numbered list of the steps the flow executes
3. Prerequisites
4. Configuration — full `appsettings.json` example with multi-batch example
5. Running the application
6. Project structure — file tree with one-line description per file
7. API Notes — the non-obvious behaviours that catch developers out
8. Sandbox vs Production — always the same: credentials determine environment, URL never changes

---

## Sandbox vs Production Note (use verbatim in every README)

> The API endpoint is always `https://api.unleashedsoftware.com`. Whether the request
> is routed to your sandbox or production account is determined by the API ID and Key you
> supply. Use your sandbox credentials to test and your production credentials to go live —
> no other configuration change is required.
