using System.Linq.Expressions;

namespace Phantom.Core.Specifications;

public interface ISpecification<T>
{
    bool IsSatisfiedBy(T candidate);

    Expression<Func<T, bool>> ToExpression();

    ISpecification<T> And(ISpecification<T> other);

    ISpecification<T> Or(ISpecification<T> other);

    ISpecification<T> Not();
}
