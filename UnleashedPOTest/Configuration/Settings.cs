using UnleashedPOTest.Models;

namespace UnleashedPOTest.Configuration;

public sealed record UnleashedSettings
{
    public string ApiId  { get; init; } = string.Empty;
    public string ApiKey { get; init; } = string.Empty;
}

public sealed record ReceiptSettings
{
    public string PurchaseOrderNumber { get; init; } = string.Empty;

    /// <summary>
    /// Batch allocations keyed by ProductCode.
    /// Each entry maps to one or more pending batch numbers with quantities.
    /// These are applied to the PO lines via PUT and become receipted after POST /Receipt.
    /// </summary>
    public Dictionary<string, List<PendingBatch>> BatchAllocations { get; init; } = [];
}
