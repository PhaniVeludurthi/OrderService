# 🎫 Event Ticketing System - Order Service

A production-ready microservice for managing orders and tickets in an event ticketing platform. Built with .NET 8, PostgreSQL, and Docker.

## 📋 Table of Contents

- [Overview](#overview)
- [Features](#features)
- [Prerequisites](#prerequisites)
- [Quick Start](#quick-start)
- [Project Structure](#project-structure)
- [API Documentation](#api-documentation)
- [Configuration](#configuration)
- [Database](#database)
- [Testing](#testing)
- [Troubleshooting](#troubleshooting)

---

## 🎯 Overview

The Order Service orchestrates the complete ticket booking workflow including:
- Order creation and management
- Ticket generation
- Payment coordination
- Seat reservation orchestration

**Architecture:** Clean Architecture with Domain, Infrastructure, and API layers  
**Database:** PostgreSQL 16 with Entity Framework Core  
**API:** RESTful with OpenAPI/Swagger documentation  

---

## ✨ Features

- ✅ Order Management (Create, View, Cancel)
- ✅ Ticket Generation with unique IDs
- ✅ Mock External Service Clients (Catalog, Seating, Payment)
- ✅ Automatic CSV Data Seeding (400 orders, 740 tickets)
- ✅ Health Checks (Liveness & Readiness)
- ✅ Prometheus Metrics
- ✅ Swagger UI Documentation
- ✅ Docker & Docker Compose Support
- ✅ Structured Logging with Serilog

---

## 📦 Prerequisites

- **Docker Desktop** 24.0+ ([Download](https://www.docker.com/products/docker-desktop/))
- **Docker Compose** 2.20+ (included with Docker Desktop)
- **.NET 9 SDK** (optional, for local development) ([Download](https://dotnet.microsoft.com/download/dotnet/9.0))

### Verify Installation

```bash

docker --version
docker-compose --version
```

---

## 🚀 Quick Start

```bash

# 1. Clone the repository
git clone https://github.com/PhaniVeludurthi/OrderService
cd OrderService

# 2. Start the service and database
docker-compose up -d

# 3. Verify services are running
docker-compose ps

# 4. View logs
docker-compose logs -f order-service

# 5. Access the application
Swagger UI: http://localhost:5004/swagger
API: http://localhost:5004/api/v1/orders
Health: http://localhost:5004/health/live
```

### Option 2: Local Development

```bash
# 1. Start PostgreSQL
docker-compose up -d orderdb

# 2. Update connection string in appsettings.json
"ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5435;Database=orderdb;Username=postgres;Password=postgres123"
}

# 3. Run the application
cd OrderService.API
dotnet run

# 4. Access at http://localhost:5004/swagger
```
---

## 📁 Project Structure

```

OrderService/
├── OrderService.API/              \# API Layer
│   ├── Controllers/              \# REST API Controllers
│   ├── Middleware/               \# Custom middleware
│   ├── SeedData/                 \# CSV seed files
│   └── Program.cs                \# Application entry point
├── OrderService.Core/            \# Domain Layer
│   ├── Entities/                 \# Domain entities
│   ├── Interfaces/               \# Repository interfaces
│   ├── DTOs/                     \# Data transfer objects
│   └── Enums/                    \# Enumerations
├── OrderService.Infrastructure/  \# Infrastructure Layer
│   ├── Data/                     \# Database context
│   ├── Repositories/             \# Data access
│   ├── ExternalClients/          \# HTTP clients
│   └── Services/                 \# Business logic
├── docker-compose.yml            \# Docker orchestration
└── README.md                     \# This file

```

---

## 📚 API Documentation

### Base URL
```
http://localhost:5004
```

### Swagger UI
```
http://localhost:5004/swagger
```

### Key Endpoints

#### Orders
- `GET /api/v1/orders` - List all orders (paginated)
- `GET /api/v1/orders/{id}` - Get order by ID
- `GET /api/v1/orders/user/{userId}` - Get user's orders
- `POST /api/v1/orders` - Create new order
- `POST /api/v1/orders/{id}/cancel` - Cancel order
- `GET /api/v1/orders/statistics` - Order statistics

#### Tickets
- `GET /api/v1/tickets/{id}` - Get ticket by ID
- `GET /api/v1/tickets/order/{orderId}` - Get tickets by order
- `GET /api/v1/tickets/event/{eventId}` - Get tickets by event
- `GET /api/v1/tickets/event/{eventId}/statistics` - Ticket statistics

#### Health & Monitoring
- `GET /health/live` - Liveness probe
- `GET /health/ready` - Readiness probe
- `GET /metrics` - Prometheus metrics

### Sample API Call

```
Create an order

curl -X POST http://localhost:5004/api/v1/orders \
-H "Content-Type: application/json" \
-d '{
"userId": 1,
"eventId": 25,
"seatIds":
}'
```

---

## ⚙️ Configuration

### Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `ASPNETCORE_ENVIRONMENT` | Environment (Development/Production) | `Development` |
| `ConnectionStrings__DefaultConnection` | PostgreSQL connection string | See appsettings.json |

### appsettings.json

```json

{
    "ConnectionStrings": {
        "DefaultConnection": "Host=orderdb;Database=orderdb;Username=postgres;Password=postgres123"
    },
    "Services": {
        "CatalogUrl": "http://catalog-service:8080",
        "SeatingUrl": "http://seating-service:8080",
        "PaymentUrl": "http://payment-service:8080"
    }
}

```

---

## 🗄️ Database

### Automatic Seeding

The service automatically seeds the database on startup with:
- **400 Orders** from `etsr_orders.csv`
- **740 Tickets** from `etsr_tickets.csv`

### Manual Database Access

```bash

# Connect to PostgreSQL

docker exec -it orderdb psql -U postgres -d orderdb

# Check seeded data

SELECT COUNT(*) FROM orders;   -- Should be 400
SELECT COUNT(*) FROM tickets;  -- Should be 740

# View sample order

SELECT * FROM orders LIMIT 5;

```

### Reset Database

```bash

# Stop and remove containers with volumes
docker-compose down -v

# Start fresh
docker-compose up -d
```

---

## 🐳 Docker Commands

### Start Services
```bash
docker-compose up -d
```

### Stop Services
```bash
docker-compose down
```

### View Logs
```bash
# All services
docker-compose logs -f

# Order service only
docker-compose logs -f order-service

# Database only
docker-compose logs -f orderdb

```

### Check Status
```bash
docker-compose ps
```

### Rebuild Services
```bash
docker-compose up -d --build
```

### Clean Up
```bash

# Stop and remove containers, networks, volumes
docker-compose down -v

# Remove all unused Docker resources
docker system prune -a
```

---

## 🧪 Testing

### Health Checks
```bash

# Liveness
curl http://localhost:5004/health/live

# Readiness
curl http://localhost:5004/health/ready
```

### API Testing
```bash

# Get all orders
curl http://localhost:5004/api/v1/orders

# Get specific order
curl http://localhost:5004/api/v1/orders/1

# Get order statistics
curl http://localhost:5004/api/v1/orders/statistics
```

### Using Swagger UI
1. Open http://localhost:5004/swagger
2. Click "Try it out" on any endpoint
3. Fill in parameters
4. Click "Execute"

---

## 🔧 Troubleshooting

### Port Already in Use
```bash

# Find process using port 5004
lsof -i :5004  \# macOS/Linux
netstat -ano | findstr :5004  \# Windows

# Kill the process or change port in docker-compose.yml
```

### Database Connection Issues
```bash

# Check if database is running
docker-compose ps orderdb

# View database logs
docker-compose logs orderdb

# Verify connection
docker exec -it orderdb pg_isready -U postgres

```

### Seeding Errors
```bash

# Check CSV files exist
ls -la OrderService.API/SeedData/

# View application logs
docker-compose logs -f order-service | grep "Seed"
```

### Swagger Not Loading
```bash

# Verify service is running
curl http://localhost:5004/health/live

# Check correct URL
http://localhost:5004/swagger  \# Not /swagger/index.html
```

---
## 📊 Monitoring

### Prometheus Metrics
```bash

http://localhost:5004/metrics
```

**Available Metrics:**
- `http_requests_received_total` - Total HTTP requests
- `http_request_duration_seconds` - Request latency
- `process_cpu_seconds_total` - CPU usage
- `dotnet_total_memory_bytes` - Memory usage
### Logs
```bash
# Stream logs
docker-compose logs -f order-service

# Export logs to file
docker-compose logs order-service > logs.txt
```

---
## 🤝 Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit changes (`git commit -m 'Add amazing feature'`)
4. Push to branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request
---

## 📝 License

This project is licensed under the MIT License.

---

## 📞 Support

- **Issues**: [GitHub Issues](https://github.com/PhaniVeludurthi/OrderService/issues)
- **Documentation**: [Wiki](https://github.com/PhaniVeludurthi/OrderService/wiki)
- **Email**: phaniveludurthi@gmail.com
---

## 🙏 Acknowledgments

- Built with [ASP.NET Core 9](https://dotnet.microsoft.com/)
- Database: [PostgreSQL 16](https://www.postgresql.org/)
- Containerization: [Docker](https://www.docker.com/)
- API Documentation: [Swagger/OpenAPI](https://swagger.io/)
---

⭐ Star this repository if you find it helpful!
