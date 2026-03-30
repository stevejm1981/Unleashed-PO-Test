# Unleashed Purchase Order - Batch Receipt Test

A self-contained C# (.NET 9) console application demonstrating the correct API flow for
applying batch number allocations to a Purchase Order and receipting it via the Unleashed
REST API.

Part of the [SupplyLens](https://supplylens.co.uk) Unleashed API reference series.

---

## Overview

Unleashed uses a two-stage process for batch number allocation on Purchase Orders. Batch
numbers are first set as "Pending" on the order lines, then confirmed when the order is
receipted. The application executes the following steps:

1. **GET** the Purchase Order to retrieve the order Guid, supplier, warehouse, and line
   details needed to construct the PUT payload (`serialBatch=true` is included so any
   existing batch data is also returned, though batch numbers are not read from the GET)
2. **PUT** the Purchase Order with `PendingBatchNumbers` applied to each line — batch
   numbers come from your configuration, not from the GET response, and remain in Pending
   state until the PO is receipted
3. **POST** `/PurchaseOrders/{orderGuid}/Receipt` to receipt the order, converting
   all pending batch numbers to fully receipted batch numbers in Unleashed stock
4. **GET** the Purchase Order again to confirm the batch numbers are now receipted

---

## Prerequisites

- .NET 9 SDK
- Unleashed account with API access enabled
- API credentials (API ID and API Key) from your Unleashed account settings
- A Purchase Order in `Placed` status with at least one batch-tracked product line

---

## Configuration

All configuration is held in `appsettings.json`. Replace the placeholder values before running.

```json
{
  "Unleashed": {
    "ApiId":  "YOUR_API_ID",
    "ApiKey": "YOUR_API_KEY"
  },
  "Receipt": {
    "PurchaseOrderNumber": "YOUR_PURCHASE_ORDER_NUMBER",
    "BatchAllocations": {
      "YOUR_PRODUCT_CODE": [
        {
          "Number":   "YOUR_BATCH_NUMBER",
          "Quantity": 0
        }
      ]
    }
  }
}
```

`BatchAllocations` is keyed by `ProductCode`. Multiple batch entries per product are
supported if stock is split across batches:

```json
"BatchAllocations": {
  "PRODUCT-A": [
    { "Number": "BATCH-001", "Quantity": 10 }
  ],
  "PRODUCT-B": [
    { "Number": "BATCH-002", "Quantity": 5 },
    { "Number": "BATCH-003", "Quantity": 5 }
  ]
}
```

---

## Running the Application

Open the solution in JetBrains Rider (or Visual Studio) and run the project directly,
or from the command line:

```bash
cd UnleashedPOTest
dotnet run
```

All output is written to the console with a timestamp prefix. Request and response bodies
are logged in full to assist with debugging.

---

## Project Structure

```
UnleashedPOTest/
  Configuration/
    Settings.cs               — Typed settings records bound from appsettings.json
  Models/
    UnleashedModels.cs        — Immutable record types for all API request and response shapes
  appsettings.json            — Configuration file (credentials and order details go here)
  IUnleashedApiClient.cs      — Interface for the Unleashed API client
  UnleashedApiClient.cs       — HTTP client with HMAC-SHA256 signing
  UnleashedApiException.cs    — Typed exception for non-success API responses
  ReceiptService.cs           — Orchestrates the three-step batch receipt flow
  Program.cs                  — Composition root: configuration, DI setup, entry point
```

---

## API Notes

**`serialBatch=true` is included on the GET request.** Without it, `PendingBatchNumbers`
and `BatchNumbers` will not be returned in the response. The GET is used to retrieve order
structure (Guid, supplier, warehouse, lines) — batch numbers are sourced from configuration
and applied on the PUT, not read from the GET response.

**Batch numbers are Pending until the PO is receipted.** Setting `PendingBatchNumbers`
on the PUT does not immediately allocate stock. The POST `/Receipt` call is what confirms
the allocation and makes the stock available in Unleashed.

**The PUT payload for a Purchase Order is more lenient than Sales Shipments.** You do not
need to send the complete order payload — only the fields being updated, plus `Supplier`
and `OrderStatus` which are always required on PUT.

**The Receipt POST has no request body.** The order Guid in the URL is sufficient.
Unleashed receipts the order using the `ReceiptQuantity` values already on the lines.

---

## Sandbox vs Production

The API endpoint is always `https://api.unleashedsoftware.com`. Whether the request
is routed to your sandbox or production account is determined by the API ID and Key you
supply. Use your sandbox credentials to test and your production credentials to go live —
no other configuration change is required.
