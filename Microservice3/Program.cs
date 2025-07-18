using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Microservice3;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddControllers();
        builder.Services.AddSingleton<IInventoryService, InventoryService>();
        builder.Services.AddOpenApi();

        var app = builder.Build();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        app.UseHttpsRedirection();
        app.UseRouting();
        app.MapControllers();

        app.Run();
    }
}

// Models
public class UpdateInventoryRequest
{
    public string OrderId { get; set; } = string.Empty;
    public List<InventoryItem> Items { get; set; } = new();
    public string TransactionId { get; set; } = string.Empty;
}

public class InventoryItem
{
    public string ProductId { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Price { get; set; }
}

public class ProductInventory
{
    public string ProductId { get; set; } = string.Empty;
    public int AvailableQuantity { get; set; }
    public int ReservedQuantity { get; set; }
    public decimal Price { get; set; }
    public DateTime LastUpdated { get; set; }
}

public class InventoryTransaction
{
    public string TransactionId { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty;
    public List<InventoryItem> Items { get; set; } = new();
    public InventoryTransactionStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class RestoreInventoryRequest
{
    public string TransactionId { get; set; } = string.Empty;
    public object? OriginalRequest { get; set; }
}

public enum InventoryTransactionStatus
{
    Pending,
    Completed,
    Failed,
    Restored
}

public class InventoryResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<ProductInventory>? UpdatedInventory { get; set; }
    public string? Error { get; set; }
}

// Interfaces
public interface IInventoryService
{
    Task<InventoryResponse> UpdateInventoryAsync(UpdateInventoryRequest request);
    Task<InventoryResponse> RestoreInventoryAsync(RestoreInventoryRequest request);
    Task<ProductInventory?> GetProductInventoryAsync(string productId);
}

// Services
public class InventoryService : IInventoryService
{
    private readonly ILogger<InventoryService> _logger;
    private readonly Dictionary<string, ProductInventory> _inventory = new();
    private readonly Dictionary<string, InventoryTransaction> _transactions = new();

    public InventoryService(ILogger<InventoryService> logger)
    {
        _logger = logger;

        // Initialize some sample inventory
        InitializeSampleInventory();
    }

    private void InitializeSampleInventory()
    {
        var sampleProducts = new[]
        {
            new ProductInventory { ProductId = "PROD001", AvailableQuantity = 100, ReservedQuantity = 0, Price = 29.99m, LastUpdated = DateTime.UtcNow },
            new ProductInventory { ProductId = "PROD002", AvailableQuantity = 50, ReservedQuantity = 0, Price = 49.99m, LastUpdated = DateTime.UtcNow },
            new ProductInventory { ProductId = "PROD003", AvailableQuantity = 25, ReservedQuantity = 0, Price = 79.99m, LastUpdated = DateTime.UtcNow },
            new ProductInventory { ProductId = "PROD004", AvailableQuantity = 75, ReservedQuantity = 0, Price = 19.99m, LastUpdated = DateTime.UtcNow },
            new ProductInventory { ProductId = "PROD005", AvailableQuantity = 10, ReservedQuantity = 0, Price = 99.99m, LastUpdated = DateTime.UtcNow }
        };

        foreach (var product in sampleProducts)
        {
            _inventory[product.ProductId] = product;
        }
    }

    public async Task<InventoryResponse> UpdateInventoryAsync(UpdateInventoryRequest request)
    {
        try
        {
            _logger.LogInformation($"Updating inventory for order {request.OrderId}");

            // Validate request
            if (request.Items == null || !request.Items.Any())
            {
                return new InventoryResponse
                {
                    Success = false,
                    Error = "No items provided for inventory update"
                };
            }

            // Check availability for all items first
            var unavailableItems = new List<string>();
            foreach (var item in request.Items)
            {
                if (_inventory.TryGetValue(item.ProductId, out var product))
                {
                    if (product.AvailableQuantity < item.Quantity)
                    {
                        unavailableItems.Add($"Product {item.ProductId} - requested: {item.Quantity}, available: {product.AvailableQuantity}");
                    }
                }
                else
                {
                    unavailableItems.Add($"Product {item.ProductId} - not found");
                }
            }

            if (unavailableItems.Any())
            {
                return new InventoryResponse
                {
                    Success = false,
                    Error = $"Insufficient inventory: {string.Join(", ", unavailableItems)}"
                };
            }

            // Simulate potential failure (20% chance)
            var random = new Random();
            if (random.Next(1, 6) == 1) // 20% chance
            {
                _logger.LogError($"Simulated inventory update failure for order {request.OrderId}");
                return new InventoryResponse
                {
                    Success = false,
                    Error = "Simulated inventory system failure"
                };
            }

            // Create transaction record
            var transaction = new InventoryTransaction
            {
                TransactionId = request.TransactionId,
                OrderId = request.OrderId,
                Items = request.Items,
                Status = InventoryTransactionStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };

            _transactions[request.TransactionId] = transaction;

            // Update inventory
            var updatedInventory = new List<ProductInventory>();
            foreach (var item in request.Items)
            {
                if (_inventory.TryGetValue(item.ProductId, out var product))
                {
                    product.AvailableQuantity -= item.Quantity;
                    product.ReservedQuantity += item.Quantity;
                    product.LastUpdated = DateTime.UtcNow;
                    updatedInventory.Add(product);
                }
            }

            transaction.Status = InventoryTransactionStatus.Completed;
            _logger.LogInformation($"Inventory updated successfully for order {request.OrderId}");

            // Simulate processing delay
            await Task.Delay(700);

            return new InventoryResponse
            {
                Success = true,
                Message = $"Inventory updated successfully for order {request.OrderId}",
                UpdatedInventory = updatedInventory
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error updating inventory for order {request.OrderId}");
            return new InventoryResponse
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    public async Task<InventoryResponse> RestoreInventoryAsync(RestoreInventoryRequest request)
    {
        try
        {
            _logger.LogInformation($"Restoring inventory for transaction {request.TransactionId}");

            if (!_transactions.TryGetValue(request.TransactionId, out var transaction))
            {
                _logger.LogWarning($"No inventory transaction found for {request.TransactionId}");
                return new InventoryResponse
                {
                    Success = true, // Consider this successful as there's nothing to restore
                    Message = "No inventory transaction found to restore"
                };
            }

            if (transaction.Status == InventoryTransactionStatus.Completed)
            {
                // Restore inventory quantities
                var restoredInventory = new List<ProductInventory>();
                foreach (var item in transaction.Items)
                {
                    if (_inventory.TryGetValue(item.ProductId, out var product))
                    {
                        product.AvailableQuantity += item.Quantity;
                        product.ReservedQuantity -= item.Quantity;
                        product.LastUpdated = DateTime.UtcNow;
                        restoredInventory.Add(product);
                    }
                }

                transaction.Status = InventoryTransactionStatus.Restored;
                _logger.LogInformation($"Inventory restored successfully for transaction {request.TransactionId}");

                // Simulate processing delay
                await Task.Delay(400);

                return new InventoryResponse
                {
                    Success = true,
                    Message = $"Inventory restored successfully for transaction {request.TransactionId}",
                    UpdatedInventory = restoredInventory
                };
            }
            else
            {
                _logger.LogInformation($"Transaction {request.TransactionId} is not in completed state, current status: {transaction.Status}");
                return new InventoryResponse
                {
                    Success = true,
                    Message = $"Transaction {request.TransactionId} was not completed, no restoration needed"
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error restoring inventory for transaction {request.TransactionId}");
            return new InventoryResponse
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    public async Task<ProductInventory?> GetProductInventoryAsync(string productId)
    {
        _inventory.TryGetValue(productId, out var product);
        return await Task.FromResult(product);
    }
}

// Controllers
[ApiController]
[Route("api/[controller]")]
public class InventoryController : ControllerBase
{
    private readonly IInventoryService _inventoryService;

    public InventoryController(IInventoryService inventoryService)
    {
        _inventoryService = inventoryService;
    }

    [HttpPost]
    public async Task<IActionResult> UpdateInventory([FromBody] UpdateInventoryRequest request)
    {
        var result = await _inventoryService.UpdateInventoryAsync(request);

        if (result.Success)
        {
            return Ok(result);
        }

        return BadRequest(result);
    }

    [HttpPost("restore")]
    public async Task<IActionResult> RestoreInventory([FromBody] RestoreInventoryRequest request)
    {
        var result = await _inventoryService.RestoreInventoryAsync(request);
        return Ok(result);
    }

    [HttpGet("{productId}")]
    public async Task<IActionResult> GetProductInventory(string productId)
    {
        var product = await _inventoryService.GetProductInventoryAsync(productId);

        if (product != null)
        {
            return Ok(product);
        }

        return NotFound();
    }

    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new { Status = "Healthy", Service = "Inventory Service", Timestamp = DateTime.UtcNow });
    }

    [HttpGet("all")]
    public async Task<IActionResult> GetAllInventory()
    {
        // This is a helper endpoint to see all inventory
        var allProducts = new List<ProductInventory>();
        var productIds = new[] { "PROD001", "PROD002", "PROD003", "PROD004", "PROD005" };

        foreach (var productId in productIds)
        {
            var product = await _inventoryService.GetProductInventoryAsync(productId);
            if (product != null)
            {
                allProducts.Add(product);
            }
        }

        return Ok(allProducts);
    }
}