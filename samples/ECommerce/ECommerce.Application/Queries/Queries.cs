using Phantom.CQRS.Queries;

namespace ECommerce.Application.Queries;

// ─── Customer Queries ────────────────────────────────────────

public record GetCustomerByIdQuery(Guid CustomerId) : IQuery<CustomerDto>;
public record CustomerDto(Guid Id, string FirstName, string LastName, string Email, string FullName);

// ─── Product Queries ─────────────────────────────────────────

public record GetProductByIdQuery(Guid ProductId) : IQuery<ProductDto>;
public record ProductDto(Guid Id, string Name, decimal Price, string Currency, int StockQuantity, bool IsAvailable);

/// <summary>
/// Example: Paged query with search keyword.
/// Demonstrates how to combine CQRS query with Phantom's Specification pattern.
/// </summary>
public record SearchProductsQuery(string? Keyword, int Page, int PageSize) : IQuery<PagedResult<ProductDto>>;

/// <summary>
/// Generic paged result wrapper — reusable across all queries.
/// </summary>
public record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize)
{
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;
}

// ─── Order Queries ───────────────────────────────────────────

public record GetOrderByIdQuery(Guid OrderId) : IQuery<OrderDto>;
public record OrderDto(Guid Id, Guid CustomerId, string Status, string ShippingAddress, decimal TotalAmount, string Currency);

/// <summary>
/// Example: List orders for a specific customer with paging.
/// </summary>
public record GetCustomerOrdersQuery(Guid CustomerId, int Page, int PageSize) : IQuery<PagedResult<OrderDto>>;
