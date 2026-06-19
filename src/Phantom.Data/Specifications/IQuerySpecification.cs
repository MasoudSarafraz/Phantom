using Phantom.Core.Specifications;
using System.Linq.Expressions;

namespace Phantom.Data.Specifications;

/// <summary>
/// Extended specification interface for infrastructure/query concerns.
/// Inherits domain-level filtering from <see cref="ISpecification{T}"/>
/// and adds EF Core-specific concerns: includes, tracking, ordering, paging.
/// </summary>
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
