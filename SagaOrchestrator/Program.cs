using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace SagaOrchestrator;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
            });
        builder.Services.AddHttpClient();
        builder.Services.AddSingleton<ISagaOrchestrator, SagaOrchestratorService>();
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
public class OrderRequest
{
    public string OrderId { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public List<OrderItem> Items { get; set; } = new();
}

public class OrderItem
{
    public string ProductId { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Price { get; set; }
}

public class SagaTransaction
{
    public string TransactionId { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty;
    public SagaStatus Status { get; set; }
    public List<SagaStep> Steps { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public string? ErrorMessage { get; set; }
}

public class SagaStep
{
    public string StepName { get; set; } = string.Empty;
    public string ServiceUrl { get; set; } = string.Empty;
    public string CompensationUrl { get; set; } = string.Empty;
    public StepStatus Status { get; set; }
    public object? Request { get; set; }
    public object? Response { get; set; }
    public DateTime ExecutedAt { get; set; }
    public string? ErrorMessage { get; set; }
}

public enum SagaStatus
{
    Started,
    InProgress,
    Completed,
    Failed,
    Compensating,
    Compensated
}

public enum StepStatus
{
    Pending,
    Completed,
    Failed,
    Compensated
}

// Interfaces
public interface ISagaOrchestrator
{
    Task<SagaTransaction> ExecuteOrderSagaAsync(OrderRequest request);
    Task<SagaTransaction> GetSagaStatusAsync(string transactionId);
}

// Services
public class SagaOrchestratorService : ISagaOrchestrator
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SagaOrchestratorService> _logger;
    private readonly Dictionary<string, SagaTransaction> _sagaTransactions = new();

    public SagaOrchestratorService(HttpClient httpClient, ILogger<SagaOrchestratorService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<SagaTransaction> ExecuteOrderSagaAsync(OrderRequest request)
    {
        var transactionId = Guid.NewGuid().ToString();
        var saga = new SagaTransaction
        {
            TransactionId = transactionId,
            OrderId = request.OrderId,
            Status = SagaStatus.Started,
            CreatedAt = DateTime.UtcNow,
            Steps = new List<SagaStep>
            {
                new SagaStep
                {
                    StepName = "CreateOrder",
                    ServiceUrl = "https://localhost:7001/api/orders",
                    CompensationUrl = "https://localhost:7001/api/orders/cancel",
                    Status = StepStatus.Pending
                },
                new SagaStep
                {
                    StepName = "ProcessPayment",
                    ServiceUrl = "https://localhost:7002/api/payments",
                    CompensationUrl = "https://localhost:7002/api/payments/refund",
                    Status = StepStatus.Pending
                },
                new SagaStep
                {
                    StepName = "UpdateInventory",
                    ServiceUrl = "https://localhost:7003/api/inventory",
                    CompensationUrl = "https://localhost:7003/api/inventory/restore",
                    Status = StepStatus.Pending
                }
            }
        };

        _sagaTransactions[transactionId] = saga;

        try
        {
            saga.Status = SagaStatus.InProgress;

            // Execute steps sequentially
            for (int i = 0; i < saga.Steps.Count; i++)
            {
                var step = saga.Steps[i];
                _logger.LogInformation($"Executing step {i + 1}: {step.StepName}");

                var success = await ExecuteStepAsync(step, request, transactionId);

                if (!success)
                {
                    _logger.LogError($"Step {step.StepName} failed. Starting compensation.");
                    saga.Status = SagaStatus.Failed;
                    saga.ErrorMessage = step.ErrorMessage;

                    // Compensate all successful steps in reverse order
                    await CompensateAsync(saga, i - 1);
                    return saga;
                }
            }

            saga.Status = SagaStatus.Completed;
            _logger.LogInformation($"Saga {transactionId} completed successfully");
        }
        catch (Exception ex)
        {
            saga.Status = SagaStatus.Failed;
            saga.ErrorMessage = ex.Message;
            _logger.LogError(ex, $"Saga {transactionId} failed with exception");
        }

        return saga;
    }

    private async Task<bool> ExecuteStepAsync(SagaStep step, OrderRequest request, string transactionId)
    {
        try
        {
            var requestData = step.StepName switch
            {
                "CreateOrder" => (object)new { OrderId = request.OrderId, CustomerId = request.CustomerId, Items = request.Items, TransactionId = transactionId },
                "ProcessPayment" => (object)new { OrderId = request.OrderId, CustomerId = request.CustomerId, Amount = request.Amount, TransactionId = transactionId },
                "UpdateInventory" => (object)new { OrderId = request.OrderId, Items = request.Items, TransactionId = transactionId },
                _ => request
            };

            step.Request = requestData;
            step.ExecutedAt = DateTime.UtcNow;

            var json = JsonSerializer.Serialize(requestData);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(step.ServiceUrl, content);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                step.Response = JsonSerializer.Deserialize<object>(responseContent);
                step.Status = StepStatus.Completed;
                return true;
            }
            else
            {
                step.Status = StepStatus.Failed;
                step.ErrorMessage = $"HTTP {response.StatusCode}: {await response.Content.ReadAsStringAsync()}";
                return false;
            }
        }
        catch (Exception ex)
        {
            step.Status = StepStatus.Failed;
            step.ErrorMessage = ex.Message;
            return false;
        }
    }

    private async Task CompensateAsync(SagaTransaction saga, int lastSuccessfulStepIndex)
    {
        saga.Status = SagaStatus.Compensating;

        // Compensate in reverse order
        for (int i = lastSuccessfulStepIndex; i >= 0; i--)
        {
            var step = saga.Steps[i];
            if (step.Status == StepStatus.Completed)
            {
                _logger.LogInformation($"Compensating step: {step.StepName}");
                await CompensateStepAsync(step);
            }
        }

        saga.Status = SagaStatus.Compensated;
    }

    private async Task CompensateStepAsync(SagaStep step)
    {
        try
        {
            var compensationData = new { TransactionId = step.Request, OriginalRequest = step.Request };
            var json = JsonSerializer.Serialize(compensationData);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(step.CompensationUrl, content);

            if (response.IsSuccessStatusCode)
            {
                step.Status = StepStatus.Compensated;
            }
            else
            {
                _logger.LogError($"Compensation failed for step {step.StepName}: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Compensation failed for step {step.StepName}");
        }
    }

    public async Task<SagaTransaction> GetSagaStatusAsync(string transactionId)
    {
        _sagaTransactions.TryGetValue(transactionId, out var saga);
        return saga ?? throw new KeyNotFoundException($"Saga transaction {transactionId} not found");
    }
}

// Controllers
[ApiController]
[Route("api/[controller]")]
public class SagaController : ControllerBase
{
    private readonly ISagaOrchestrator _orchestrator;

    public SagaController(ISagaOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    [HttpPost("execute-order")]
    public async Task<IActionResult> ExecuteOrder([FromBody] OrderRequest request)
    {
        var result = await _orchestrator.ExecuteOrderSagaAsync(request);
        return Ok(result);
    }

    [HttpGet("status/{transactionId}")]
    public async Task<IActionResult> GetStatus(string transactionId)
    {
        try
        {
            var result = await _orchestrator.GetSagaStatusAsync(transactionId);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}