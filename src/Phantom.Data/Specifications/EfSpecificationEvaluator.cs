using Microsoft.EntityFrameworkCore;
using Phantom.Core.Specifications;

namespace Phantom.Data.Specifications;

public class EfSpecificationEvaluator : ISpecificationEvaluator
{
    public IQueryable<T> ApplySpecification<T>(IQueryable<T> query, ISpecification<T> specification) where T : class
    {
        ArgumentNullException.ThrowIfNull(specification);

        var result = query.Where(specification.ToExpression());

        if (specification is IQuerySpecification<T> querySpec)
        {
            foreach (var include in querySpec.Includes)
                result = result.Include(include);

            foreach (var includeString in querySpec.IncludeStrings)
                result = result.Include(includeString);

            if (querySpec.OrderBy is not null)
                result = result.OrderBy(querySpec.OrderBy);
            else if (querySpec.OrderByDescending is not null)
                result = result.OrderByDescending(querySpec.OrderByDescending);

            if (querySpec.Skip.HasValue)
                result = result.Skip(querySpec.Skip.Value);

            if (querySpec.Take.HasValue)
                result = result.Take(querySpec.Take.Value);

            if (querySpec.AsNoTracking)
                result = result.AsNoTracking();
        }

        return result;
    }
}
