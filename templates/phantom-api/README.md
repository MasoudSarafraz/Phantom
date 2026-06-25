# MyApp

A DDD/Clean Architecture solution built with [Phantom](https://github.com/MasoudSarafraz/Phantom).

## Quick Start

```bash
# Restore and build
dotnet build

# Run database and broker infrastructure (optional, only needed for production features)
cd ../../devcontainers  # if at repo root, otherwise skip
docker compose up -d postgres rabbitmq

# Run the API
cd src/MyApp.Api
dotnet run

# Open Swagger
open https://localhost:5001/swagger
```

## Project Structure

```
MyApp/
├── src/
│   ├── MyApp.Domain/              # Domain entities, value objects, domain events
│   │   ├── Entities/
│   │   └── Events/
│   ├── MyApp.Application/         # Commands, queries, handlers, validators, DTOs
│   │   ├── Commands/
│   │   └── Handlers/
│   ├── MyApp.Infrastructure/      # EF Core DbContext, persistence concerns
│   │   └── Persistence/
│   └── MyApp.Api/                 # ASP.NET Core API, controllers, Program.cs
│       └── Controllers/
└── tests/
    └── MyApp.Tests/               # Unit and integration tests
```

## Configuration

Configuration is read from `appsettings.json` under the `Phantom` section:

```json
{
  "Phantom": {
    "Database": {
      "Provider": "PostgreSQL",
      "ConnectionString": "Host=localhost;Port=5432;Database=myapp;Username=phantom;Password=phantom"
    },
    "Messaging": {
      "Channels": {
        "default": { "Type": "InMemory" }
      }
    },
    "Features": {
      "UseOutbox": true,
      "UseIdempotency": true,
      "UseFluentValidation": true,
      "UseSoftDelete": true,
      "UseAuditable": true
    }
  }
}
```

## Features

- **CQRS** with source-generated dispatcher
- **DDD primitives** — Entity, AggregateRoot, ValueObject, Specifications
- **Event-driven** messaging with InMemory/RabbitMQ/Kafka
- **Outbox pattern** for reliable event publishing
- **Idempotency** for at-least-once message handling
- **OpenTelemetry** for distributed tracing and metrics
- **Diagnostic endpoints** at `/phantom/diagnostics/*`
- **Health checks** at `/health`
- **Prometheus metrics** at `/metrics`

## Development

For local development with PostgreSQL, RabbitMQ, Kafka, Jaeger, and Grafana, see [`devcontainers/README.md`](../../devcontainers/README.md) in the main Phantom repository.

## License

MIT
