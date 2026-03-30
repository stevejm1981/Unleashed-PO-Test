using System.Text.Json.Serialization;

namespace UnleashedPOTest.Models;

// ─── Shared ───────────────────────────────────────────────────────────────────

public record UnleashedProduct(
    [property: JsonPropertyName("Guid")]                       string? Guid,
    [property: JsonPropertyName("ProductCode")]                string? ProductCode,
    [property: JsonPropertyName("ProductDescription")]         string? ProductDescription,
    [property: JsonPropertyName("SupplierProductCode")]        string? SupplierProductCode,
    [property: JsonPropertyName("SupplierProductDescription")] string? SupplierProductDescription);

public record UnleashedSupplier(
    [property: JsonPropertyName("Guid")]         string? Guid,
    [property: JsonPropertyName("SupplierCode")] string? SupplierCode,
    [property: JsonPropertyName("SupplierName")] string? SupplierName);

public record UnleashedWarehouse(
    [property: JsonPropertyName("Guid")]          string? Guid,
    [property: JsonPropertyName("WarehouseCode")] string? WarehouseCode,
    [property: JsonPropertyName("WarehouseName")] string? WarehouseName);

// ─── Pagination ───────────────────────────────────────────────────────────────

public record Pagination(
    [property: JsonPropertyName("NumberOfItems")]  int NumberOfItems,
    [property: JsonPropertyName("PageSize")]       int PageSize,
    [property: JsonPropertyName("PageNumber")]     int PageNumber,
    [property: JsonPropertyName("NumberOfPages")]  int NumberOfPages);

// ─── Purchase Order (GET) ─────────────────────────────────────────────────────

public record PurchaseOrderResponse(
    [property: JsonPropertyName("Pagination")] Pagination?            Pagination,
    [property: JsonPropertyName("Items")]      List<PurchaseOrder>?   Items);

public record PurchaseOrder(
    [property: JsonPropertyName("Guid")]               string?                   Guid,
    [property: JsonPropertyName("OrderNumber")]        string?                   OrderNumber,
    [property: JsonPropertyName("OrderStatus")]        string?                   OrderStatus,
    [property: JsonPropertyName("Supplier")]           UnleashedSupplier?        Supplier,
    [property: JsonPropertyName("Warehouse")]          UnleashedWarehouse?       Warehouse,
    [property: JsonPropertyName("PurchaseOrderLines")] List<PurchaseOrderLine>?  PurchaseOrderLines);

public record PurchaseOrderLine(
    [property: JsonPropertyName("Guid")]                string?              Guid,
    [property: JsonPropertyName("LineNumber")]          int                  LineNumber,
    [property: JsonPropertyName("Product")]             UnleashedProduct?    Product,
    [property: JsonPropertyName("OrderQuantity")]       decimal              OrderQuantity,
    [property: JsonPropertyName("UnitPrice")]           decimal              UnitPrice,
    [property: JsonPropertyName("LineTotal")]           decimal              LineTotal,
    [property: JsonPropertyName("LineTax")]             decimal              LineTax,
    [property: JsonPropertyName("DiscountRate")]        decimal              DiscountRate,
    [property: JsonPropertyName("PendingBatchNumbers")] List<PendingBatch>?  PendingBatchNumbers,
    [property: JsonPropertyName("BatchNumbers")]        List<ReceiptedBatch>? BatchNumbers);

/// <summary>
/// Batch number in Pending state — set via PUT before receipting.
/// Becomes a receipted BatchNumber after POST /Receipt.
/// Uses property initialisation (not positional constructor) so IConfiguration can bind it from appsettings.json.
/// </summary>
public record PendingBatch
{
    [JsonPropertyName("Number")]
    public string? Number { get; init; }

    [JsonPropertyName("Quantity")]
    public decimal Quantity { get; init; }

    [JsonPropertyName("ExpiryDate")]
    public string? ExpiryDate { get; init; }
}

/// <summary>
/// Receipted batch number — returned on GET after the PO has been receipted.
/// </summary>
public record ReceiptedBatch(
    [property: JsonPropertyName("Guid")]       string?  Guid,
    [property: JsonPropertyName("BatchNumber")] string? BatchNumber,
    [property: JsonPropertyName("Quantity")]   decimal  Quantity,
    [property: JsonPropertyName("ExpiryDate")] string?  ExpiryDate);

// ─── Purchase Order line PUT request ─────────────────────────────────────────

/// <summary>
/// Line update payload for PUT /PurchaseOrders/{orderGuid}/Lines/{lineGuid}.
/// PendingBatchNumbers set here become receipted BatchNumbers after POST /Receipt.
/// </summary>
public record PurchaseOrderLineUpdate(
    [property: JsonPropertyName("Guid")]                string?             Guid,
    [property: JsonPropertyName("OrderQuantity")]       decimal             OrderQuantity,
    [property: JsonPropertyName("ReceiptQuantity")]     decimal             ReceiptQuantity,
    [property: JsonPropertyName("UnitPrice")]           decimal             UnitPrice,
    [property: JsonPropertyName("LineTotal")]           decimal             LineTotal,
    [property: JsonPropertyName("LineTax")]             decimal             LineTax,
    [property: JsonPropertyName("DiscountRate")]        decimal             DiscountRate,
    [property: JsonPropertyName("PendingBatchNumbers")] List<PendingBatch>? PendingBatchNumbers);
