# Phantom Development Environment

This directory contains Docker-based infrastructure for local development and testing.

## Services

| Service | Port | Username | Password | UI |
|---|---|---|---|---|
| PostgreSQL | 5432 | phantom | phantom | - |
| RabbitMQ | 5672 (AMQP) / 15672 (Management) | phantom | phantom | http://localhost:15672 |
| Kafka | 9092 (host) / 29092 (internal) | - | - | - |
| Kafka UI | 8080 | - | - | http://localhost:8080 |
| Redis | 6379 | - | - | - |
| Jaeger | 4317 (OTLP gRPC) / 4318 (OTLP HTTP) / 16686 (UI) | - | - | http://localhost:16686 |
| Prometheus | 9090 | - | - | http://localhost:9090 |
| Grafana | 3000 | phantom | phantom | http://localhost:3000 |

## Quick Start

```bash
# Start all services
docker compose up -d

# Check status
docker compose ps

# View logs
docker compose logs -f rabbitmq
docker compose logs -f kafka

# Stop all services
docker compose down

# Stop and remove volumes (fresh start)
docker compose down -v
```

## Individual Services

You can start specific services only:

```bash
# Just PostgreSQL and RabbitMQ
docker compose up -d postgres rabbitmq

# Just Kafka stack
docker compose up -d zookeeper kafka kafka-ui

# Just observability stack
docker compose up -d jaeger prometheus grafana
```

## Health Checks

All services have health checks configured. You can verify they are ready:

```bash
docker inspect --format='{{.State.Health.Status}}' phantom-postgres
docker inspect --format='{{.State.Health.Status}}' phantom-rabbitmq
docker inspect --format='{{.State.Health.Status}}' phantom-kafka
```

## Connection Strings

### PostgreSQL
```
Host=localhost;Port=5432;Database=phantom;Username=phantom;Password=phantom
```

### RabbitMQ
```
Host=localhost;Port=5672;Username=phantom;Password=phantom;VirtualHost=/
```

### Kafka
```
BootstrapServers=localhost:9092
```

### Redis
```
localhost:6379
```

### OpenTelemetry OTLP
```
Endpoint=http://localhost:4317 (gRPC)
Endpoint=http://localhost:4318 (HTTP)
```

## Sample appsettings.json

```json
{
  "Phantom": {
    "Database": {
      "Provider": "PostgreSQL",
      "ConnectionString": "Host=localhost;Port=5432;Database=phantom;Username=phantom;Password=phantom"
    },
    "Messaging": {
      "Channels": {
        "orders": {
          "Type": "RabbitMq",
          "RabbitMq": {
            "Host": "localhost",
            "Port": 5672,
            "Username": "phantom",
            "Password": "phantom"
          }
        }
      }
    },
    "Features": {
      "UseOutbox": true,
      "UseIdempotency": true
    }
  }
}
```

## Observability

After starting the observability stack:

1. **Jaeger UI** — View distributed traces at http://localhost:16686
2. **Prometheus** — Query metrics at http://localhost:9090
3. **Grafana** — Dashboards at http://localhost:3000 (phantom/phantom)
   - Add Prometheus as a data source: http://prometheus:9090
   - Add Jaeger as a data source: http://jaeger:16686

## Resource Requirements

- **Minimum**: 4 GB RAM, 2 CPUs
- **Recommended**: 8 GB RAM, 4 CPUs
- **Disk**: ~5 GB for images and data volumes

## Troubleshooting

### Kafka not starting
Kafka requires Zookeeper. If Kafka fails, ensure Zookeeper is running:
```bash
docker compose logs zookeeper
docker compose restart zookeeper kafka
```

### Port conflicts
If ports are already in use, modify them in `docker-compose.yml`.

### Cleaning up
```bash
# Remove all containers and volumes
docker compose down -v

# Remove images
docker compose down --rmi all
```
