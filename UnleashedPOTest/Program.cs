using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UnleashedPOTest;
using UnleashedPOTest.Configuration;

// ─── Configuration ────────────────────────────────────────────────────────────

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: false)
    .Build();

var unleashedSettings = configuration.GetSection("Unleashed").Get<UnleashedSettings>()
    ?? throw new InvalidOperationException("Unleashed configuration section is missing from appsettings.json.");

var receiptSettings = configuration.GetSection("Receipt").Get<ReceiptSettings>()
    ?? throw new InvalidOperationException("Receipt configuration section is missing from appsettings.json.");

// ─── Placeholder validation ───────────────────────────────────────────────────

if (unleashedSettings.ApiId == "YOUR_API_ID" || unleashedSettings.ApiKey == "YOUR_API_KEY")
    throw new InvalidOperationException("API credentials have not been set. Update the Unleashed section in appsettings.json before running.");

if (receiptSettings.PurchaseOrderNumber == "YOUR_PURCHASE_ORDER_NUMBER")
    throw new InvalidOperationException("Purchase order number has not been set. Update the Receipt section in appsettings.json before running.");

if (receiptSettings.BatchAllocations.ContainsKey("YOUR_PRODUCT_CODE"))
    throw new InvalidOperationException("Batch allocations have not been configured. Update the Receipt section in appsettings.json before running.");

// ─── Services ─────────────────────────────────────────────────────────────────

var services = new ServiceCollection();

services
    .AddHttpClient(nameof(UnleashedApiClient), client =>
    {
        client.BaseAddress = new Uri("https://api.unleashedsoftware.com");
        client.DefaultRequestHeaders.Add("api-auth-id", unleashedSettings.ApiId);
        client.DefaultRequestHeaders.Add("Accept", "application/json");
    });

services.AddSingleton<IUnleashedApiClient>(sp =>
    new UnleashedApiClient(sp.GetRequiredService<IHttpClientFactory>(), unleashedSettings.ApiKey));

services.AddSingleton<ReceiptService>();

var serviceProvider = services.BuildServiceProvider();

// ─── Run ──────────────────────────────────────────────────────────────────────

using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));

try
{
    var receiptService = serviceProvider.GetRequiredService<ReceiptService>();
    await receiptService.RunAsync(receiptSettings.PurchaseOrderNumber, receiptSettings.BatchAllocations, cts.Token);
}
catch (Exception ex)
{
    Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ERROR: {ex.Message}");
    Environment.Exit(1);
}
