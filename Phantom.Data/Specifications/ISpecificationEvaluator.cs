using Phantom.Core.Specifications;

namespace Phantom.Data.Specifications;

public interface ISpecificationEvaluator
{
    IQueryable<T> ApplySpecification<T>(IQueryable<T> query, ISpecification<T> specification) where T : class;
}
