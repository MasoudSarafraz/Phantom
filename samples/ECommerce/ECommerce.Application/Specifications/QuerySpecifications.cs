using System.Linq.Expressions;
using ECommerce.Domain.Entities;
using Phantom.Core.Specifications;
using Phantom.Data.Specifications;

namespace ECommerce.Application.Specifications;

/// <summary>
/// Infrastructure-layer query specification for paginated product search.
/// Lives in Infrastructure (not Domain) because it uses <see cref="QuerySpecification{T}"/>
/// which depends on EF Core concepts (Includes, AsNoTracking, paging, ordering).
///
/// Usage in handler:
///   var spec = new PagedProductSpec(keyword: "laptop", page: 1, pageSize: 20);
///   var products = await _repository.FindAsync(spec, ct);
/// </summary>
public class PagedProductSpec : QuerySpecification<Product>
{
    public PagedProductSpec(string? keyword = null, int page = 1, int pageSize = 20)
    {
        _keyword = keyword;

        ApplyOrderBy(p => p.Name);
        ApplyPaging(skip: (page - 1) * pageSize, take: pageSize);
        ApplyAsNoTracking();
    }

    private readonly string? _keyword;

    public override bool IsSatisfiedBy(Product candidate)
    {
        if (_keyword is not null && !candidate.Name.Contains(_keyword, StringComparison.OrdinalIgnoreCase))
            return false;
        return candidate.IsAvailable;
    }

    public override Expression<Func<Product, bool>> ToExpression()
    {
        if (_keyword is not null)
            return p => p.Name.Contains(_keyword) && p.IsAvailable;
        return p => p.IsAvailable;
    }
}

/// <summary>
/// Infrastructure-layer query specification for retrieving an Order with its OrderLines
/// eagerly loaded in a single SQL query.
/// </summary>
public class OrderWithLinesSpec : QuerySpecification<Order>
{
    private readonly Guid? _orderId;

    public OrderWithLinesSpec(Guid? orderId = null)
    {
        _orderId = orderId;

        AddInclude("Lines");
        ApplyAsNoTracking();
        ApplyOrderByDescending(o => o.Id);
    }

    public override bool IsSatisfiedBy(Order candidate)
        => _orderId is null || candidate.Id == _orderId.Value;

    public override Expression<Func<Order, bool>> ToExpression()
        => _orderId.HasValue ? o => o.Id == _orderId.Value : o => true;
}
