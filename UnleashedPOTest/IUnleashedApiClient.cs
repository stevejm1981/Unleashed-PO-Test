using UnleashedPOTest.Models;

namespace UnleashedPOTest;

public interface IUnleashedApiClient
{
    /// <summary>
    /// GET /PurchaseOrders/1?orderNumber={orderNumber}&amp;serialBatch=true
    /// Returns the Purchase Order including PendingBatchNumbers on each line.
    /// serialBatch=true is required to include batch number data in the response.
    /// </summary>
    Task<PurchaseOrder?> GetPurchaseOrderAsync(string orderNumber, CancellationToken ct = default);

    /// <summary>
    /// PUT /PurchaseOrders/{orderGuid}/Lines/{lineGuid}
    /// Updates a specific Purchase Order line with PendingBatchNumbers.
    /// Batch numbers remain in Pending state until the PO is receipted.
    /// </summary>
    Task UpdatePurchaseOrderLineAsync(string orderGuid, string lineGuid, PurchaseOrderLineUpdate request, CancellationToken ct = default);

    /// <summary>
    /// POST /PurchaseOrders/{orderGuid}/Receipt
    /// Receipts the Purchase Order, converting all PendingBatchNumbers to receipted BatchNumbers.
    /// </summary>
    Task ReceiptPurchaseOrderAsync(string orderGuid, CancellationToken ct = default);
}
