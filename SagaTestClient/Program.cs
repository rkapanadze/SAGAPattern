using System.Text;
using System.Text.Json;

namespace SagaTestClient;

public class Program
{
    private static readonly HttpClient _httpClient = new();

    public static async Task Main(string[] args)
    {
        Console.WriteLine("=== SAGA Pattern Test Client ===\n");

        // Configure HttpClient
        _httpClient.Timeout = TimeSpan.FromSeconds(30);

        while (true)
        {
            ShowMenu();
            var choice = Console.ReadLine();

            switch (choice)
            {
                case "1":
                    await TestSuccessfulSaga();
                    break;
                case "2":
                    await TestFailingSaga();
                    break;
                case "3":
                    await TestMultipleScenarios();
                    break;
                case "4":
                    await CheckServiceHealth();
                    break;
                case "5":
                    await ViewInventoryStatus();
                    break;
                case "6":
                    Environment.Exit(0);
                    break;
                default:
                    Console.WriteLine("Invalid choice. Please try again.\n");
                    break;
            }
        }
    }

    private static void ShowMenu()
    {
        Console.WriteLine("Choose an option:");
        Console.WriteLine("1. Test Successful SAGA (all services succeed)");
        Console.WriteLine("2. Test Failing SAGA (with compensation)");
        Console.WriteLine("3. Test Multiple Scenarios");
        Console.WriteLine("4. Check Service Health");
        Console.WriteLine("5. View Inventory Status");
        Console.WriteLine("6. Exit");
        Console.Write("Enter your choice (1-6): ");
    }

    private static async Task TestSuccessfulSaga()
    {
        Console.WriteLine("\n=== Testing Successful SAGA ===");

        var orderRequest = new
        {
            OrderId = $"ORDER-{DateTime.Now:yyyyMMdd-HHmmss}",
            CustomerId = "CUST001",
            Amount = 149.97m,
            Items = new[]
            {
                new { ProductId = "PROD001", Quantity = 2, Price = 29.99m },
                new { ProductId = "PROD002", Quantity = 1, Price = 49.99m },
                new { ProductId = "PROD003", Quantity = 1, Price = 79.99m }
            }
        };

        await ExecuteSagaTest(orderRequest, "Successful SAGA");
    }

    private static async Task TestFailingSaga()
    {
        Console.WriteLine("\n=== Testing Failing SAGA (High Quantity to Trigger Failure) ===");

        var orderRequest = new
        {
            OrderId = $"ORDER-{DateTime.Now:yyyyMMdd-HHmmss}",
            CustomerId = "CUST002",
            Amount = 2999.75m,
            Items = new[]
            {
                new { ProductId = "PROD001", Quantity = 150, Price = 29.99m }, // This will likely fail due to insufficient inventory
                new { ProductId = "PROD002", Quantity = 100, Price = 49.99m }
            }
        };

        await ExecuteSagaTest(orderRequest, "Failing SAGA");
    }

    private static async Task TestMultipleScenarios()
    {
        Console.WriteLine("\n=== Testing Multiple Scenarios ===");

        var scenarios = new[]
        {
            new
            {
                Name = "Small Order",
                Request = new
                {
                    OrderId = $"ORDER-SMALL-{DateTime.Now:yyyyMMdd-HHmmss}",
                    CustomerId = "CUST003",
                    Amount = 29.99m,
                    Items = new[] { new { ProductId = "PROD001", Quantity = 1, Price = 29.99m } }
                }
            },
            new
            {
                Name = "Medium Order",
                Request = new
                {
                    OrderId = $"ORDER-MEDIUM-{DateTime.Now:yyyyMMdd-HHmmss}",
                    CustomerId = "CUST004",
                    Amount = 199.96m,
                    Items = new[]
                    {
                        new { ProductId = "PROD002", Quantity = 2, Price = 49.99m },
                        new { ProductId = "PROD004", Quantity = 5, Price = 19.99m }
                    }
                }
            },
            new
            {
                Name = "Large Order (might fail)",
                Request = new
                {
                    OrderId = $"ORDER-LARGE-{DateTime.Now:yyyyMMdd-HHmmss}",
                    CustomerId = "CUST005",
                    Amount = 999.90m,
                    Items = new[]
                    {
                        new { ProductId = "PROD005", Quantity = 10, Price = 99.99m } // This will likely fail due to exact inventory limit
                    }
                }
            }
        };

        foreach (var scenario in scenarios)
        {
            Console.WriteLine($"\n--- {scenario.Name} ---");
            await ExecuteSagaTest(scenario.Request, scenario.Name);
            await Task.Delay(2000); // Wait between tests
        }
    }

    private static async Task ExecuteSagaTest(object orderRequest, string testName)
    {
        try
        {
            Console.WriteLine($"Executing {testName}...");

            var json = JsonSerializer.Serialize(orderRequest, new JsonSerializerOptions { WriteIndented = true });
            Console.WriteLine($"Request: {json}");

            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("https://localhost:7000/api/saga/execute-order", content);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var sagaResult = JsonSerializer.Deserialize<JsonElement>(responseContent);

                Console.WriteLine($"SAGA Status: {sagaResult.GetProperty("status").GetString()}");
                Console.WriteLine($"Transaction ID: {sagaResult.GetProperty("transactionId").GetString()}");

                if (sagaResult.TryGetProperty("errorMessage", out var errorMessage) && errorMessage.ValueKind != JsonValueKind.Null)
                {
                    Console.WriteLine($"Error: {errorMessage.GetString()}");
                }

                Console.WriteLine("Steps executed:");
                if (sagaResult.TryGetProperty("steps", out var steps))
                {
                    foreach (var step in steps.EnumerateArray())
                    {
                        var stepName = step.GetProperty("stepName").GetString();
                        var stepStatus = step.GetProperty("status").GetString();
                        Console.WriteLine($"  - {stepName}: {stepStatus}");

                        if (step.TryGetProperty("errorMessage", out var stepError) && stepError.ValueKind != JsonValueKind.Null)
                        {
                            Console.WriteLine($"    Error: {stepError.GetString()}");
                        }
                    }
                }
            }
            else
            {
                Console.WriteLine($"Request failed: {response.StatusCode}");
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Error: {errorContent}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception during {testName}: {ex.Message}");
        }

        Console.WriteLine(new string('=', 50));
    }

    private static async Task CheckServiceHealth()
    {
        Console.WriteLine("\n=== Checking Service Health ===");

        var services = new[]
        {
            new { Name = "Orchestrator", Url = "https://localhost:7000/api/saga/status/test" },
            new { Name = "Order Service", Url = "https://localhost:7001/api/orders/health" },
            new { Name = "Payment Service", Url = "https://localhost:7002/api/payments/health" },
            new { Name = "Inventory Service", Url = "https://localhost:7003/api/inventory/health" }
        };

        foreach (var service in services)
        {
            try
            {
                var response = await _httpClient.GetAsync(service.Url);
                var status = response.IsSuccessStatusCode ? "✅ Healthy" : "❌ Unhealthy";
                Console.WriteLine($"{service.Name}: {status}");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    if (content.Contains("Status"))
                    {
                        Console.WriteLine($"  Response: {content}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{service.Name}: ❌ Error - {ex.Message}");
            }
        }

        Console.WriteLine();
    }

    private static async Task ViewInventoryStatus()
    {
        Console.WriteLine("\n=== Current Inventory Status ===");

        try
        {
            var response = await _httpClient.GetAsync("https://localhost:7003/api/inventory/all");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var inventory = JsonSerializer.Deserialize<JsonElement[]>(content);

                Console.WriteLine("Product ID | Available | Reserved | Price");
                Console.WriteLine(new string('-', 45));

                foreach (var product in inventory)
                {
                    var productId = product.GetProperty("productId").GetString();
                    var available = product.GetProperty("availableQuantity").GetInt32();
                    var reserved = product.GetProperty("reservedQuantity").GetInt32();
                    var price = product.GetProperty("price").GetDecimal();

                    Console.WriteLine($"{productId,-10} | {available,9} | {reserved,8} | ${price,6:F2}");
                }
            }
            else
            {
                Console.WriteLine("Could not retrieve inventory status");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error retrieving inventory: {ex.Message}");
        }

        Console.WriteLine();
    }
}