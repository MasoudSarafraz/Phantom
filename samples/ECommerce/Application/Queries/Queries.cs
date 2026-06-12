using Phantom.CQRS.Queries;

namespace ECommerce.Application.Queries;

public record GetCustomerByIdQuery(Guid CustomerId) : IQuery<CustomerDto>;
public record CustomerDto(Guid Id, string FirstName, string LastName, string Email, string FullName);

public record GetProductByIdQuery(Guid ProductId) : IQuery<ProductDto>;
public record ProductDto(Guid Id, string Name, decimal Price, string Currency, int StockQuantity, bool IsAvailable);

public record GetOrderByIdQuery(Guid OrderId) : IQuery<OrderDto>;
public record OrderDto(Guid Id, Guid CustomerId, string Status, string ShippingAddress, decimal TotalAmount, string Currency);
