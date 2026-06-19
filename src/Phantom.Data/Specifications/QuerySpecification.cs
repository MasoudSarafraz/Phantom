using Phantom.Core.Specifications;
using System.Linq.Expressions;

namespace Phantom.Data.Specifications;

public abstract class QuerySpecification<T> : Specification<T>, IQuerySpecification<T>
{
    private readonly List<Expression<Func<T, object>>> _includes = new();
    private readonly List<string> _includeStrings = new();

    public IReadOnlyList<Expression<Func<T, object>>> Includes => _includes.AsReadOnly();

    public IReadOnlyList<string> IncludeStrings => _includeStrings.AsReadOnly();

    public Expression<Func<T, object>>? OrderBy { get; private set; }

    public Expression<Func<T, object>>? OrderByDescending { get; private set; }

    public int? Skip { get; private set; }

    public int? Take { get; private set; }

    public bool AsNoTracking { get; private set; }

    protected void AddInclude(Expression<Func<T, object>> includeExpression)
    {
        ArgumentNullException.ThrowIfNull(includeExpression);
        _includes.Add(includeExpression);
    }

    protected void AddInclude(string includeString)
    {
        ArgumentNullException.ThrowIfNull(includeString);
        _includeStrings.Add(includeString);
    }

    protected void ApplyOrderBy(Expression<Func<T, object>> orderByExpression)
    {
        ArgumentNullException.ThrowIfNull(orderByExpression);
        OrderBy = orderByExpression;
    }

    protected void ApplyOrderByDescending(Expression<Func<T, object>> orderByDescendingExpression)
    {
        ArgumentNullException.ThrowIfNull(orderByDescendingExpression);
        OrderByDescending = orderByDescendingExpression;
    }

    protected void ApplyPaging(int skip, int take)
    {
        if (skip < 0) throw new ArgumentOutOfRangeException(nameof(skip), "Skip must be non-negative.");
        if (take <= 0) throw new ArgumentOutOfRangeException(nameof(take), "Take must be positive.");
        Skip = skip;
        Take = take;
    }

    protected void ApplyAsNoTracking()
    {
        AsNoTracking = true;
    }
}
