using System.Linq.Expressions;
using ECommerce.Domain.Entities;
using Phantom.Core.Specifications;

namespace ECommerce.Domain.Specifications;

/// <summary>
/// Pure domain specification — only uses Phantom.Core's <see cref="Specification{T}"/>.
/// Domain specifications are persistence-agnostic: they only encode business rules.
/// </summary>
public class ActiveProductSpec : Specification<Product>
{
    public override bool IsSatisfiedBy(Product candidate) => candidate.IsAvailable && candidate.StockQuantity > 0;

    public override Expression<Func<Product, bool>> ToExpression() => p => p.IsAvailable && p.StockQuantity > 0;
}

/// <summary>
/// Pure domain specification for keyword-based product search.
/// </summary>
public class ProductByNameSpec : Specification<Product>
{
    private readonly string _keyword;
    public ProductByNameSpec(string keyword) => _keyword = keyword;

    public override bool IsSatisfiedBy(Product candidate) => candidate.Name.Contains(_keyword, StringComparison.OrdinalIgnoreCase);

    public override Expression<Func<Product, bool>> ToExpression() => p => p.Name.Contains(_keyword);
}

/// <summary>
/// Composed domain specification: ActiveProductSpec AND ProductByNameSpec.
/// Demonstrates the Specification combinators (And/Or/Not) provided by Phantom.Core.
/// </summary>
public class ActiveProductByNameSpec : Specification<Product>
{
    private readonly string _keyword;
    public ActiveProductByNameSpec(string keyword) => _keyword = keyword;

    public override bool IsSatisfiedBy(Product candidate)
        => candidate.IsAvailable && candidate.StockQuantity > 0
        && candidate.Name.Contains(_keyword, StringComparison.OrdinalIgnoreCase);

    public override Expression<Func<Product, bool>> ToExpression()
        => p => p.IsAvailable && p.StockQuantity > 0 && p.Name.Contains(_keyword);
}
