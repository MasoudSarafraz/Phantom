using Phantom.Core.Specifications;

namespace Phantom.Data.Specifications;

/// <summary>
/// EF Core specification evaluator that translates <see cref="ISpecification{T}"/>
/// into queryable expressions using <see cref="ISpecification{T}.ToExpression"/>,
/// which EF Core can translate to SQL.
/// </summary>
public class EfSpecificationEvaluator : ISpecificationEvaluator
{
    /// <summary>
    /// Applies a specification to a queryable by converting it to an expression
    /// that EF Core can translate to SQL.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="query">The source queryable.</param>
    /// <param name="specification">The specification to apply.</param>
    /// <returns>The filtered queryable.</returns>
    public IQueryable<T> ApplySpecification<T>(IQueryable<T> query, ISpecification<T> specification) where T : class
        => query.Where(specification.ToExpression());
}
