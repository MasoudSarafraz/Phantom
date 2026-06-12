using System.Linq.Expressions;

namespace Phantom.Core.Specifications;

/// <summary>
/// Abstract base class for specifications. Provides default implementations for
/// combining specifications using logical AND, OR, and NOT operators, with proper
/// expression tree composition for SQL translation.
/// </summary>
/// <typeparam name="T">The type of entity this specification applies to.</typeparam>
public abstract class Specification<T> : ISpecification<T>
{
    /// <summary>
    /// Determines whether the specified candidate satisfies this specification.
    /// Used for in-memory evaluation.
    /// </summary>
    /// <param name="candidate">The candidate entity to evaluate.</param>
    /// <returns><c>true</c> if the candidate satisfies the specification; otherwise, <c>false</c>.</returns>
    public abstract bool IsSatisfiedBy(T candidate);

    /// <summary>
    /// Converts this specification into an expression tree for SQL translation by ORMs.
    /// </summary>
    /// <returns>An <see cref="Expression{TDelegate}"/> representing this specification.</returns>
    public abstract Expression<Func<T, bool>> ToExpression();

    /// <summary>
    /// Creates a new specification that is satisfied only when both this specification
    /// and the other specification are satisfied (logical AND).
    /// </summary>
    /// <param name="other">The specification to combine with this one. Must not be <c>null</c>.</param>
    /// <returns>A new <see cref="AndSpecification{T}"/> combining both specifications.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="other"/> is <c>null</c>.</exception>
    public ISpecification<T> And(ISpecification<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return new AndSpecification<T>(this, other);
    }

    /// <summary>
    /// Creates a new specification that is satisfied when either this specification
    /// or the other specification is satisfied (logical OR).
    /// </summary>
    /// <param name="other">The specification to combine with this one. Must not be <c>null</c>.</param>
    /// <returns>A new <see cref="OrSpecification{T}"/> combining both specifications.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="other"/> is <c>null</c>.</exception>
    public ISpecification<T> Or(ISpecification<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return new OrSpecification<T>(this, other);
    }

    /// <summary>
    /// Creates a new specification that is satisfied when this specification is not satisfied (logical NOT).
    /// </summary>
    /// <returns>A new <see cref="NotSpecification{T}"/> negating this specification.</returns>
    public ISpecification<T> Not() => new NotSpecification<T>(this);
}

internal sealed class AndSpecification<T> : Specification<T>
{
    private readonly ISpecification<T> _left;
    private readonly ISpecification<T> _right;

    public AndSpecification(ISpecification<T> left, ISpecification<T> right)
    {
        _left = left;
        _right = right;
    }

    public override bool IsSatisfiedBy(T candidate) => _left.IsSatisfiedBy(candidate) && _right.IsSatisfiedBy(candidate);

    public override Expression<Func<T, bool>> ToExpression()
    {
        var leftExpr = _left.ToExpression();
        var rightExpr = _right.ToExpression();
        var parameter = Expression.Parameter(typeof(T));

        var leftBody = new ParameterReplacer(leftExpr.Parameters[0], parameter).Visit(leftExpr.Body);
        var rightBody = new ParameterReplacer(rightExpr.Parameters[0], parameter).Visit(rightExpr.Body);

        return Expression.Lambda<Func<T, bool>>(Expression.AndAlso(leftBody, rightBody), parameter);
    }
}

internal sealed class OrSpecification<T> : Specification<T>
{
    private readonly ISpecification<T> _left;
    private readonly ISpecification<T> _right;

    public OrSpecification(ISpecification<T> left, ISpecification<T> right)
    {
        _left = left;
        _right = right;
    }

    public override bool IsSatisfiedBy(T candidate) => _left.IsSatisfiedBy(candidate) || _right.IsSatisfiedBy(candidate);

    public override Expression<Func<T, bool>> ToExpression()
    {
        var leftExpr = _left.ToExpression();
        var rightExpr = _right.ToExpression();
        var parameter = Expression.Parameter(typeof(T));

        var leftBody = new ParameterReplacer(leftExpr.Parameters[0], parameter).Visit(leftExpr.Body);
        var rightBody = new ParameterReplacer(rightExpr.Parameters[0], parameter).Visit(rightExpr.Body);

        return Expression.Lambda<Func<T, bool>>(Expression.OrElse(leftBody, rightBody), parameter);
    }
}

internal sealed class NotSpecification<T> : Specification<T>
{
    private readonly ISpecification<T> _specification;

    public NotSpecification(ISpecification<T> specification)
    {
        _specification = specification;
    }

    public override bool IsSatisfiedBy(T candidate) => !_specification.IsSatisfiedBy(candidate);

    public override Expression<Func<T, bool>> ToExpression()
    {
        var expr = _specification.ToExpression();
        var parameter = Expression.Parameter(typeof(T));
        var body = new ParameterReplacer(expr.Parameters[0], parameter).Visit(expr.Body);

        return Expression.Lambda<Func<T, bool>>(Expression.Not(body), parameter);
    }
}

/// <summary>
/// Expression visitor that replaces parameter expressions to unify parameters
/// when combining multiple expression trees.
/// </summary>
internal sealed class ParameterReplacer : ExpressionVisitor
{
    private readonly ParameterExpression _oldParameter;
    private readonly ParameterExpression _newParameter;

    public ParameterReplacer(ParameterExpression oldParameter, ParameterExpression newParameter)
    {
        _oldParameter = oldParameter;
        _newParameter = newParameter;
    }

    protected override Expression VisitParameter(ParameterExpression node)
    {
        return node == _oldParameter ? _newParameter : node;
    }
}


