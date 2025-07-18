
SAGA Pattern Microservices Setup Instructions
Overview
This solution implements the SAGA pattern using the Orchestrator approach with 4 services:

SAGA Orchestrator (Port 7000) - Coordinates the entire transaction
Order Service (Port 7001) - Handles order creation and cancellation
Payment Service (Port 7002) - Processes payments and refunds
Inventory Service (Port 7003) - Manages inventory updates and restoration


Project Structure
SAGAPattern/
├── SagaOrchestrator/           # Main orchestrator service
├── Microservice1/              # Order Service
├── Microservice2/              # Payment Service
├── Microservice3/              # Inventory Service
└── SagaTestClient/             # Test client application

Run the Services
Open 4 terminal windows and run each service:
# Terminal 1 - SAGA Orchestrator
cd SagaOrchestrator
dotnet run

# Terminal 2 - Order Service
cd Microservice1
dotnet run

# Terminal 3 - Payment Service
cd Microservice2
dotnet run

# Terminal 4 - Inventory Service
cd Microservice3
dotnet run

Run the Test Client
# Terminal 5 - Test Client
cd SagaTestClient
dotnet run


Testing Scenarios
Success Scenario

All services respond successfully
Transaction completes with Status: Completed
No compensation needed

Failure Scenario

One service fails (simulated random failures)
Orchestrator triggers compensation
All successful operations are rolled back
Transaction ends with Status: Compensated

Built-in Failure Simulation

Order Service: 10% random failure rate
Payment Service: 12.5% random failure rate
Inventory Service: 20% random failure rate + insufficient inventory checks

API Endpoints
SAGA Orchestrator (Port 7000)

POST /api/saga/execute-order - Execute SAGA transaction
GET /api/saga/status/{transactionId} - Get transaction status

Order Service (Port 7001)

POST /api/orders - Create order
POST /api/orders/cancel - Cancel order (compensation)
GET /api/orders/{orderId} - Get order details
GET /api/orders/health - Health check

Payment Service (Port 7002)

POST /api/payments - Process payment
POST /api/payments/refund - Refund payment (compensation)
GET /api/payments/{paymentId} - Get payment details
GET /api/payments/health - Health check

Inventory Service (Port 7003)

POST /api/inventory - Update inventory
POST /api/inventory/restore - Restore inventory (compensation)
GET /api/inventory/{productId} - Get product inventory
GET /api/inventory/all - Get all inventory
GET /api/inventory/health - Health check

Sample Request
json{
  "orderId": "ORDER-20241201-123456",
  "customerId": "CUST001",
  "amount": 149.97,
  "items": [
    {
      "productId": "PROD001",
      "quantity": 2,
      "price": 29.99
    },
    {
      "productId": "PROD002",
      "quantity": 1,
      "price": 49.99
    }
  ]
}
Key Features
1. Orchestrator Pattern

Centralized coordination of all services
Sequential execution of steps
Automatic compensation on failure

2. Compensation Logic

Rollback in reverse order
Idempotent compensation operations
Detailed transaction tracking

3. Error Handling

Graceful failure handling
Comprehensive logging
Timeout management

4. Monitoring

Real-time transaction status
Step-by-step execution tracking
Health check endpoints

Troubleshooting
Common Issues

Port Conflicts: Ensure ports 7000-7003 are available
SSL Certificates: Use dotnet dev-certs https --trust if needed
Service Startup: Start services in order: Orchestrator last
Network Issues: Check firewall settings for localhost connections

Logs to Monitor

Service startup logs
Transaction execution logs
Compensation operation logs
Error messages and stack traces

Production Considerations
For production deployment, consider:

Service Discovery: Replace hardcoded URLs with service discovery
Circuit Breakers: Add resilience patterns
Persistent Storage: Replace in-memory storage with databases
Message Queues: Use async messaging for better scalability
Monitoring: Add comprehensive monitoring and alerting
Security: Implement authentication and authorization
Configuration: Use configuration management for different environments
