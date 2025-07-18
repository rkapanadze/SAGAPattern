using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Microservice1;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddControllers();
        builder.Services.AddSingleton<IOrderService, OrderService>();
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
public class CreateOrderRequest
{
    public string OrderId { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public List<OrderItem> Items { get; set; } = new();
    public string TransactionId { get; set; } = string.Empty;
}

public class OrderItem
{
    public string ProductId { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Price { get; set; }
}

public class Order
{
    public string OrderId { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public List<OrderItem> Items { get; set; } = new();
    public OrderStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public string TransactionId { get; set; } = string.Empty;
}

public class CancelOrderRequest
{
    public string TransactionId { get; set; } = string.Empty;
    public object? OriginalRequest { get; set; }
}

public enum OrderStatus
{
    Created,
    Cancelled,
    Completed
}

public class OrderResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public Order? Order { get; set; }
    public string? Error { get; set; }
}

// Interfaces
public interface IOrderService
{
    Task<OrderResponse> CreateOrderAsync(CreateOrderRequest request);
    Task<OrderResponse> CancelOrderAsync(CancelOrderRequest request);
    Task<Order?> GetOrderAsync(string orderId);
}

// Services
public class OrderService : IOrderService
{
    private readonly ILogger<OrderService> _logger;
    private readonly Dictionary<string, Order> _orders = new();
    private readonly Dictionary<string, string> _transactionToOrderMapping = new();

    public OrderService(ILogger<OrderService> logger)
    {
        _logger = logger;
    }

    public async Task<OrderResponse> CreateOrderAsync(CreateOrderRequest request)
    {
        try
        {
            _logger.LogInformation($"Creating order {request.OrderId} for customer {request.CustomerId}");

            // Simulate some business logic validation
            if (string.IsNullOrEmpty(request.CustomerId))
            {
                return new OrderResponse
                {
                    Success = false,
                    Error = "CustomerId is required"
                };
            }

            if (request.Items == null || !request.Items.Any())
            {
                return new OrderResponse
                {
                    Success = false,
                    Error = "Order must contain at least one item"
                };
            }

            // Simulate potential failure (10% chance)
            var random = new Random();
            if (random.Next(1, 11) == 1) // 10% chance
            {
                _logger.LogError($"Simulated failure while creating order {request.OrderId}");
                return new OrderResponse
                {
                    Success = false,
                    Error = "Simulated order creation failure"
                };
            }

            var order = new Order
            {
                OrderId = request.OrderId,
                CustomerId = request.CustomerId,
                Items = request.Items,
                Status = OrderStatus.Created,
                CreatedAt = DateTime.UtcNow,
                TransactionId = request.TransactionId
            };

            _orders[request.OrderId] = order;
            _transactionToOrderMapping[request.TransactionId] = request.OrderId;

            _logger.LogInformation($"Order {request.OrderId} created successfully");

            // Simulate processing delay
            await Task.Delay(500);

            return new OrderResponse
            {
                Success = true,
                Message = $"Order {request.OrderId} created successfully",
                Order = order
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error creating order {request.OrderId}");
            return new OrderResponse
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    public async Task<OrderResponse> CancelOrderAsync(CancelOrderRequest request)
    {
        try
        {
            _logger.LogInformation($"Cancelling order for transaction {request.TransactionId}");

            if (!_transactionToOrderMapping.TryGetValue(request.TransactionId, out var orderId))
            {
                _logger.LogWarning($"No order found for transaction {request.TransactionId}");
                return new OrderResponse
                {
                    Success = true, // Consider this successful as there's nothing to cancel
                    Message = "No order found to cancel"
                };
            }

            if (_orders.TryGetValue(orderId, out var order))
            {
                order.Status = OrderStatus.Cancelled;
                _logger.LogInformation($"Order {orderId} cancelled successfully");

                // Simulate processing delay
                await Task.Delay(300);

                return new OrderResponse
                {
                    Success = true,
                    Message = $"Order {orderId} cancelled successfully",
                    Order = order
                };
            }

            return new OrderResponse
            {
                Success = true,
                Message = "Order not found, nothing to cancel"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error cancelling order for transaction {request.TransactionId}");
            return new OrderResponse
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    public async Task<Order?> GetOrderAsync(string orderId)
    {
        _orders.TryGetValue(orderId, out var order);
        return await Task.FromResult(order);
    }
}

// Controllers
[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;

    public OrdersController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    [HttpPost]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request)
    {
        var result = await _orderService.CreateOrderAsync(request);

        if (result.Success)
        {
            return Ok(result);
        }

        return BadRequest(result);
    }

    [HttpPost("cancel")]
    public async Task<IActionResult> CancelOrder([FromBody] CancelOrderRequest request)
    {
        var result = await _orderService.CancelOrderAsync(request);
        return Ok(result);
    }

    [HttpGet("{orderId}")]
    public async Task<IActionResult> GetOrder(string orderId)
    {
        var order = await _orderService.GetOrderAsync(orderId);

        if (order != null)
        {
            return Ok(order);
        }

        return NotFound();
    }

    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new { Status = "Healthy", Service = "Order Service", Timestamp = DateTime.UtcNow });
    }
}