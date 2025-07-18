using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Microservice2;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddControllers();
        builder.Services.AddSingleton<IPaymentService, PaymentService>();
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
public class ProcessPaymentRequest
{
    public string OrderId { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string TransactionId { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = "CreditCard";
}

public class Payment
{
    public string PaymentId { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public PaymentStatus Status { get; set; }
    public DateTime ProcessedAt { get; set; }
    public string TransactionId { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = string.Empty;
}

public class RefundPaymentRequest
{
    public string TransactionId { get; set; } = string.Empty;
    public object? OriginalRequest { get; set; }
}

public enum PaymentStatus
{
    Pending,
    Completed,
    Failed,
    Refunded
}

public class PaymentResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public Payment? Payment { get; set; }
    public string? Error { get; set; }
}

// Interfaces
public interface IPaymentService
{
    Task<PaymentResponse> ProcessPaymentAsync(ProcessPaymentRequest request);
    Task<PaymentResponse> RefundPaymentAsync(RefundPaymentRequest request);
    Task<Payment?> GetPaymentAsync(string paymentId);
}

// Services
public class PaymentService : IPaymentService
{
    private readonly ILogger<PaymentService> _logger;
    private readonly Dictionary<string, Payment> _payments = new();
    private readonly Dictionary<string, string> _transactionToPaymentMapping = new();

    public PaymentService(ILogger<PaymentService> logger)
    {
        _logger = logger;
    }

    public async Task<PaymentResponse> ProcessPaymentAsync(ProcessPaymentRequest request)
    {
        try
        {
            _logger.LogInformation($"Processing payment for order {request.OrderId}, amount ${request.Amount}");

            // Simulate business logic validation
            if (request.Amount <= 0)
            {
                return new PaymentResponse
                {
                    Success = false,
                    Error = "Payment amount must be greater than zero"
                };
            }

            if (request.Amount > 10000) // Simulate max payment limit
            {
                return new PaymentResponse
                {
                    Success = false,
                    Error = "Payment amount exceeds maximum limit of $10,000"
                };
            }

            // Simulate potential failure (15% chance)
            var random = new Random();
            if (random.Next(1, 8) == 1) // ~12.5% chance
            {
                _logger.LogError($"Simulated payment failure for order {request.OrderId}");
                return new PaymentResponse
                {
                    Success = false,
                    Error = "Payment processing failed - insufficient funds"
                };
            }

            var paymentId = Guid.NewGuid().ToString();
            var payment = new Payment
            {
                PaymentId = paymentId,
                OrderId = request.OrderId,
                CustomerId = request.CustomerId,
                Amount = request.Amount,
                Status = PaymentStatus.Completed,
                ProcessedAt = DateTime.UtcNow,
                TransactionId = request.TransactionId,
                PaymentMethod = request.PaymentMethod
            };

            _payments[paymentId] = payment;
            _transactionToPaymentMapping[request.TransactionId] = paymentId;

            _logger.LogInformation($"Payment {paymentId} processed successfully for order {request.OrderId}");

            // Simulate processing delay
            await Task.Delay(800);

            return new PaymentResponse
            {
                Success = true,
                Message = $"Payment processed successfully",
                Payment = payment
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error processing payment for order {request.OrderId}");
            return new PaymentResponse
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    public async Task<PaymentResponse> RefundPaymentAsync(RefundPaymentRequest request)
    {
        try
        {
            _logger.LogInformation($"Processing refund for transaction {request.TransactionId}");

            if (!_transactionToPaymentMapping.TryGetValue(request.TransactionId, out var paymentId))
            {
                _logger.LogWarning($"No payment found for transaction {request.TransactionId}");
                return new PaymentResponse
                {
                    Success = true, // Consider this successful as there's nothing to refund
                    Message = "No payment found to refund"
                };
            }

            if (_payments.TryGetValue(paymentId, out var payment))
            {
                if (payment.Status == PaymentStatus.Completed)
                {
                    payment.Status = PaymentStatus.Refunded;
                    _logger.LogInformation($"Payment {paymentId} refunded successfully");

                    // Simulate processing delay
                    await Task.Delay(600);

                    return new PaymentResponse
                    {
                        Success = true,
                        Message = $"Payment {paymentId} refunded successfully",
                        Payment = payment
                    };
                }
                else
                {
                    _logger.LogInformation($"Payment {paymentId} is not in completed state, current status: {payment.Status}");
                    return new PaymentResponse
                    {
                        Success = true,
                        Message = $"Payment {paymentId} was not completed, no refund needed"
                    };
                }
            }

            return new PaymentResponse
            {
                Success = true,
                Message = "Payment not found, nothing to refund"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error refunding payment for transaction {request.TransactionId}");
            return new PaymentResponse
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    public async Task<Payment?> GetPaymentAsync(string paymentId)
    {
        _payments.TryGetValue(paymentId, out var payment);
        return await Task.FromResult(payment);
    }
}

// Controllers
[ApiController]
[Route("api/[controller]")]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentService _paymentService;

    public PaymentsController(IPaymentService paymentService)
    {
        _paymentService = paymentService;
    }

    [HttpPost]
    public async Task<IActionResult> ProcessPayment([FromBody] ProcessPaymentRequest request)
    {
        var result = await _paymentService.ProcessPaymentAsync(request);

        if (result.Success)
        {
            return Ok(result);
        }

        return BadRequest(result);
    }

    [HttpPost("refund")]
    public async Task<IActionResult> RefundPayment([FromBody] RefundPaymentRequest request)
    {
        var result = await _paymentService.RefundPaymentAsync(request);
        return Ok(result);
    }

    [HttpGet("{paymentId}")]
    public async Task<IActionResult> GetPayment(string paymentId)
    {
        var payment = await _paymentService.GetPaymentAsync(paymentId);

        if (payment != null)
        {
            return Ok(payment);
        }

        return NotFound();
    }

    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new { Status = "Healthy", Service = "Payment Service", Timestamp = DateTime.UtcNow });
    }
}