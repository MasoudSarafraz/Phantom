using Phantom.CQRS.Queries;

namespace ECommerce.Application.Queries;

public record GetCustomerByIdQuery(Guid CustomerId) : IQuery<CustomerDto>;
public record CustomerDto(Guid Id, string FirstName, string LastName, string Email, string FullName);

public record GetProductByIdQuery(Guid ProductId) : IQuery<ProductDto>;
public record ProductDto(Guid Id, string Name, decimal Price, string Currency, int StockQuantity, bool IsAvailable);

public record SearchProductsQuery(string? Keyword, int Page, int PageSize) : IQuery<PagedResult<ProductDto>>;

public record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize)
{
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;
}

public record GetOrderByIdQuery(Guid OrderId) : IQuery<OrderDto>;
public record OrderDto(Guid Id, Guid CustomerId, string Status, string ShippingAddress, decimal TotalAmount, string Currency);

public record GetCustomerOrdersQuery(Guid CustomerId, int Page, int PageSize) : IQuery<PagedResult<OrderDto>>;
