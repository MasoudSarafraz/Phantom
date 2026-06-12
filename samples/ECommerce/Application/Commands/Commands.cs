using Phantom.CQRS.Commands;

namespace ECommerce.Application.Commands;

public record CreateCustomerCommand(string FirstName, string LastName, string Email) : ICommand<Guid>;

public record CreateProductCommand(string Name, string Description, decimal Price, string Currency, int StockQuantity) : ICommand<Guid>;

public record CreateOrderCommand(Guid CustomerId, string ShippingAddress, List<CreateOrderLineDto> Lines) : ICommand<Guid>;
public record CreateOrderLineDto(Guid ProductId, string ProductName, int Quantity, decimal UnitPrice, string Currency);

public record ShipOrderCommand(Guid OrderId, string TrackingNumber) : ICommand;

public record CancelOrderCommand(Guid OrderId) : ICommand;
