using Phantom.Core.Results;
using Phantom.CQRS.Commands;

namespace ECommerce.Application.Commands;

// ─── Customer Commands ───────────────────────────────────────

/// <summary>
/// Example: Command returning Result&lt;Guid&gt; instead of throwing exceptions.
/// The handler returns Result.Success(id) or Result.Failure(message).
/// </summary>
public record CreateCustomerCommand(string FirstName, string LastName, string Email) : ICommand<Result<Guid>>;

// ─── Product Commands ────────────────────────────────────────

public record CreateProductCommand(string Name, string Description, decimal Price, string Currency, int StockQuantity) : ICommand<Guid>;

// ─── Order Commands ──────────────────────────────────────────

public record CreateOrderCommand(Guid CustomerId, string ShippingAddress, List<CreateOrderLineDto> Lines) : ICommand<Guid>;
public record CreateOrderLineDto(Guid ProductId, string ProductName, int Quantity, decimal UnitPrice, string Currency);

public record ShipOrderCommand(Guid OrderId, string TrackingNumber) : ICommand;

public record CancelOrderCommand(Guid OrderId) : ICommand;
