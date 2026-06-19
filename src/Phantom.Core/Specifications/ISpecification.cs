using System.Linq.Expressions;

namespace Phantom.Core.Specifications;

/// <summary>
/// Pure domain specification interface — contains only domain-level concepts.
/// Infrastructure concerns (includes, tracking, ordering, paging) have been
/// moved to <see cref="IQuerySpecification{T}"/> in the Data layer.
/// </summary>
public interface ISpecification<T>
{
    bool IsSatisfiedBy(T candidate);

    Expression<Func<T, bool>> ToExpression();

    ISpecification<T> And(ISpecification<T> other);

    ISpecification<T> Or(ISpecification<T> other);

    ISpecification<T> Not();
}
