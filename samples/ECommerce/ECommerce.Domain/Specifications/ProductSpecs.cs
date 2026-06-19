using System.Linq.Expressions;
using ECommerce.Domain.Entities;
using Phantom.Core.Specifications;

namespace ECommerce.Domain.Specifications;

public class ActiveProductSpec : Specification<Product>
{
    public override bool IsSatisfiedBy(Product candidate) => candidate.IsAvailable && candidate.StockQuantity > 0;

    public override Expression<Func<Product, bool>> ToExpression() => p => p.IsAvailable && p.StockQuantity > 0;
}

public class ProductByNameSpec : Specification<Product>
{
    private readonly string _keyword;
    public ProductByNameSpec(string keyword) => _keyword = keyword;

    public override bool IsSatisfiedBy(Product candidate) => candidate.Name.Contains(_keyword, StringComparison.OrdinalIgnoreCase);

    public override Expression<Func<Product, bool>> ToExpression() => p => p.Name.Contains(_keyword);
}

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
