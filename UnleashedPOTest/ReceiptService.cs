using UnleashedPOTest.Models;

namespace UnleashedPOTest;

/// <summary>
/// Orchestrates the Purchase Order batch allocation and receipt flow against the Unleashed API.
/// </summary>
public sealed class ReceiptService(IUnleashedApiClient apiClient)
{
    /// <summary>
    /// Executes the three-step receipt flow for the given purchase order:
    ///   1. GET the Purchase Order to retrieve line details and the order Guid
    ///   2. PUT the Purchase Order with PendingBatchNumbers applied to each line
    ///   3. POST /Receipt to receipt the order, converting pending batches to receipted batches
    /// </summary>
    public async Task RunAsync(
        string purchaseOrderNumber,
        Dictionary<string, List<PendingBatch>> batchAllocations,
        CancellationToken ct = default)
    {
        Log("---------------------------------------------------");
        Log("  Unleashed Purchase Order - Batch Receipt Test");
        Log($"  Order: {purchaseOrderNumber}");
        Log("---------------------------------------------------");

        // Step 1: GET the Purchase Order.
        // Retrieves the order Guid, supplier, warehouse, and line details needed to build the PUT payload.
        // Batch numbers are not read from the GET response — they come from configuration and are
        // applied as PendingBatchNumbers on the PUT in Step 2.
        // serialBatch=true is included so the GET response also returns any existing batch data for reference.
        Log($"Step 1/3 - Retrieving Purchase Order {purchaseOrderNumber}...");

        var purchaseOrder = await apiClient.GetPurchaseOrderAsync(purchaseOrderNumber, ct)
            ?? throw new InvalidOperationException(
                $"Purchase Order {purchaseOrderNumber} was not found or is not in Placed status. " +
                $"Only Placed orders can have batch numbers applied and be receipted.");

        if (string.IsNullOrWhiteSpace(purchaseOrder.Guid) || purchaseOrder.Guid == "00000000-0000-0000-0000-000000000000")
            throw new InvalidOperationException($"Purchase Order {purchaseOrderNumber} returned an invalid Guid.");

        Log($"         Order Number : {purchaseOrder.OrderNumber}");
        Log($"         Order Guid   : {purchaseOrder.Guid}");
        Log($"         Status       : {purchaseOrder.OrderStatus}");
        Log($"         Supplier     : {purchaseOrder.Supplier?.SupplierCode}");
        Log($"         Lines        : {purchaseOrder.PurchaseOrderLines?.Count ?? 0}");

        // Step 2: PUT each line individually via the line-level endpoint.
        // PendingBatchNumbers must be set via PUT /PurchaseOrders/{orderGuid}/Lines/{lineGuid}
        // — the order-level PUT ignores batch data.
        // Batch numbers remain in Pending state until receipted in Step 3.
        Log("Step 2/3 - Applying pending batch allocations to lines via PUT...");

        var lineUpdates = BuildLineUpdates(purchaseOrder, batchAllocations);
        foreach (var (lineUpdate, originalLine) in lineUpdates.Zip(purchaseOrder.PurchaseOrderLines ?? []))
        {
            var lineGuid = originalLine.Guid
                ?? throw new InvalidOperationException($"Line {originalLine.LineNumber} has no Guid.");

            Log($"         Updating line {originalLine.LineNumber} ({originalLine.Product?.ProductCode})...");
            await apiClient.UpdatePurchaseOrderLineAsync(purchaseOrder.Guid, lineGuid, lineUpdate, ct);
        }

        LogPendingBatches(purchaseOrder, batchAllocations);

        // Step 3: POST /Receipt — receipts the PO and converts all PendingBatchNumbers
        // to fully receipted BatchNumbers visible in Unleashed stock.
        Log("Step 3/3 - Receipting Purchase Order...");

        await apiClient.ReceiptPurchaseOrderAsync(purchaseOrder.Guid, ct);

        // Step 4: GET the order again to confirm batch numbers are now receipted.
        Log("Confirming receipted batch allocations...");

        var receiptedOrder = await apiClient.GetPurchaseOrderAsync(purchaseOrderNumber, ct)
            ?? throw new InvalidOperationException("Could not retrieve Purchase Order after receipting.");

        Log($"         Status : {receiptedOrder.OrderStatus}");
        LogReceiptedBatches(receiptedOrder, batchAllocations);

        Log("---------------------------------------------------");
        Log("  Receipt flow completed successfully.");
        Log("---------------------------------------------------");
    }

    // ─── Private helpers ──────────────────────────────────────────────────────

    private static List<PurchaseOrderLineUpdate> BuildLineUpdates(
        PurchaseOrder purchaseOrder,
        Dictionary<string, List<PendingBatch>> batchAllocations) =>
        (purchaseOrder.PurchaseOrderLines ?? [])
            .Select(line =>
            {
                batchAllocations.TryGetValue(line.Product?.ProductCode ?? string.Empty, out var batches);

                // ReceiptQuantity must match the total PendingBatchNumber quantity
                // or Unleashed will reject the Receipt call with a 400.
                var receiptQty = batches?.Sum(b => b.Quantity) ?? line.OrderQuantity;

                return new PurchaseOrderLineUpdate(
                    Guid:                line.Guid,
                    OrderQuantity:       line.OrderQuantity,
                    ReceiptQuantity:     receiptQty,
                    UnitPrice:           line.UnitPrice,
                    LineTotal:           line.LineTotal,
                    LineTax:             line.LineTax,
                    DiscountRate:        line.DiscountRate,
                    PendingBatchNumbers: batches?.Count > 0 ? [.. batches] : null);
            })
            .ToList();

    private static void LogPendingBatches(
        PurchaseOrder order,
        Dictionary<string, List<PendingBatch>> expectedBatches)
    {
        Log("Pending Batch Allocation Results:");

        foreach (var line in order.PurchaseOrderLines ?? [])
        {
            var productCode = line.Product?.ProductCode ?? "(unknown)";

            if (line.PendingBatchNumbers?.Count > 0)
            {
                foreach (var batch in line.PendingBatchNumbers)
                    Log($"         {productCode} - Pending Batch: {batch.Number}, Qty: {batch.Quantity} [PENDING]");
            }
            else if (expectedBatches.ContainsKey(productCode))
            {
                Log($"         {productCode} - Pending batch not applied. PendingBatchNumbers returned null.");
            }
            else
            {
                Log($"         {productCode} - No batch allocation expected for this line.");
            }
        }
    }

    private static void LogReceiptedBatches(
        PurchaseOrder order,
        Dictionary<string, List<PendingBatch>> expectedBatches)
    {
        Log("Receipted Batch Results:");

        foreach (var line in order.PurchaseOrderLines ?? [])
        {
            var productCode = line.Product?.ProductCode ?? "(unknown)";

            if (line.BatchNumbers?.Count > 0)
            {
                foreach (var batch in line.BatchNumbers)
                    Log($"         {productCode} - Batch: {batch.BatchNumber}, Qty: {batch.Quantity} [RECEIPTED]");
            }
            else if (expectedBatches.ContainsKey(productCode))
            {
                Log($"         {productCode} - Batch not found on receipted order. Verify the receipt completed successfully.");
            }
            else
            {
                Log($"         {productCode} - No batch allocation expected for this line.");
            }
        }
    }

    private static void Log(string message) =>
        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}");
}
