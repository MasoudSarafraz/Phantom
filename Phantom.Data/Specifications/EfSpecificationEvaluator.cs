using Phantom.Core.Specifications;

namespace Phantom.Data.Specifications;

public class EfSpecificationEvaluator : ISpecificationEvaluator
{
    public IQueryable<T> ApplySpecification<T>(IQueryable<T> query, ISpecification<T> specification) where T : class
        => query.Where(specification.ToExpression());
}
