using System.Linq.Expressions;

namespace Phantom.Core.Specifications;

/// <summary>
/// Defines a specification pattern for querying and filtering entities.
/// Provides both in-memory evaluation via <see cref="IsSatisfiedBy"/> and
/// expression-based evaluation via <see cref="ToExpression"/> for SQL translation
/// by ORMs such as Entity Framework Core.
/// </summary>
/// <typeparam name="T">The type of entity this specification applies to.</typeparam>
public interface ISpecification<T>
{
    /// <summary>
    /// Determines whether the specified candidate satisfies this specification.
    /// Used for in-memory evaluation.
    /// </summary>
    /// <param name="candidate">The candidate entity to evaluate.</param>
    /// <returns><c>true</c> if the candidate satisfies the specification; otherwise, <c>false</c>.</returns>
    bool IsSatisfiedBy(T candidate);

    /// <summary>
    /// Converts this specification into an expression tree that can be translated to SQL
    /// by ORMs such as Entity Framework Core. This is critical for ensuring that filtering
    /// is performed in the database rather than in memory.
    /// </summary>
    /// <returns>An <see cref="Expression{TDelegate}"/> representing this specification.</returns>
    Expression<Func<T, bool>> ToExpression();

    /// <summary>
    /// Creates a new specification that is satisfied only when both this specification
    /// and the other specification are satisfied (logical AND).
    /// </summary>
    /// <param name="other">The specification to combine with this one.</param>
    /// <returns>A new combined specification.</returns>
    ISpecification<T> And(ISpecification<T> other);

    /// <summary>
    /// Creates a new specification that is satisfied when either this specification
    /// or the other specification is satisfied (logical OR).
    /// </summary>
    /// <param name="other">The specification to combine with this one.</param>
    /// <returns>A new combined specification.</returns>
    ISpecification<T> Or(ISpecification<T> other);

    /// <summary>
    /// Creates a new specification that is satisfied when this specification is not satisfied (logical NOT).
    /// </summary>
    /// <returns>A new negated specification.</returns>
    ISpecification<T> Not();
}
