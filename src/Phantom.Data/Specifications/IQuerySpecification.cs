using Phantom.Core.Specifications;
using System.Linq.Expressions;

namespace Phantom.Data.Specifications;

public interface IQuerySpecification<T> : ISpecification<T>
{
    IReadOnlyList<Expression<Func<T, object>>> Includes { get; }

    IReadOnlyList<string> IncludeStrings { get; }

    Expression<Func<T, object>>? OrderBy { get; }

    Expression<Func<T, object>>? OrderByDescending { get; }

    int? Skip { get; }

    int? Take { get; }

    bool AsNoTracking { get; }
}
