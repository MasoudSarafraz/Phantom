namespace Phantom.Core.Results;

/// <summary>
/// Represents the outcome of an operation that returns a value of type <typeparamref name="T"/>.
/// A result is either successful with a value or a failure with an associated error message.
/// </summary>
/// <typeparam name="T">The type of the value returned on success.</typeparam>
public class Result<T> : Result
{
    private readonly T _value;

    /// <summary>
    /// Gets the value of a successful result.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when accessing the value of a failure result.</exception>
    public T Value => IsSuccess
        ? _value
        : throw new InvalidOperationException("Cannot access value of a failure result");

    private Result(T value, bool isSuccess, string error) : base(isSuccess, error)
    {
        _value = value;
    }

    /// <summary>
    /// Creates a successful result containing the specified value.
    /// </summary>
    /// <param name="value">The value of the successful result.</param>
    /// <returns>A new successful <see cref="Result{T}"/>.</returns>
    public static Result<T> Success(T value) => new(value, true, string.Empty);

    /// <summary>
    /// Creates a failure result with the specified error message.
    /// </summary>
    /// <param name="error">A description of why the operation failed.</param>
    /// <returns>A new failed <see cref="Result{T}"/>.</returns>
    public new static Result<T> Failure(string error) => new(default!, false, error);

    /// <summary>
    /// Transforms the value of a successful result using the provided mapping function.
    /// Failure results are passed through unchanged.
    /// </summary>
    /// <typeparam name="TResult">The type of the mapped value.</typeparam>
    /// <param name="map">The function to apply to the value if the result is successful.</param>
    /// <returns>A new <see cref="Result{TResult}"/> containing the mapped value, or the original failure.</returns>
    public Result<TResult> Map<TResult>(Func<T, TResult> map)
    {
        return IsSuccess ? Result<TResult>.Success(map(_value)) : Result<TResult>.Failure(Error);
    }

    /// <summary>
    /// Binds the value of a successful result to a new result-producing function (monadic bind).
    /// Failure results are passed through unchanged.
    /// </summary>
    /// <typeparam name="TResult">The type of the bound result value.</typeparam>
    /// <param name="bind">The function to apply to the value if the result is successful. Must return a <see cref="Result{TResult}"/>.</param>
    /// <returns>The result of the bind function, or the original failure.</returns>
    public Result<TResult> Bind<TResult>(Func<T, Result<TResult>> bind)
    {
        return IsSuccess ? bind(_value) : Result<TResult>.Failure(Error);
    }

    /// <summary>
    /// Pattern matches on the result, executing the appropriate function based on success or failure.
    /// </summary>
    /// <typeparam name="TMatch">The return type of the match functions.</typeparam>
    /// <param name="onSuccess">The function to execute if the result is successful. Receives the value.</param>
    /// <param name="onFailure">The function to execute if the result is a failure. Receives the error message.</param>
    /// <returns>The result of the executed function.</returns>
    public TMatch Match<TMatch>(Func<T, TMatch> onSuccess, Func<string, TMatch> onFailure)
    {
        return IsSuccess ? onSuccess(_value) : onFailure(Error);
    }
}
