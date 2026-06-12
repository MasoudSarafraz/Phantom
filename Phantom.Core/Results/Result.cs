namespace Phantom.Core.Results;

/// <summary>
/// Represents the outcome of an operation without a return value.
/// A result is either successful (<see cref="IsSuccess"/> is <c>true</c>) or a failure
/// (<see cref="IsFailure"/> is <c>true</c>) with an associated error message.
/// </summary>
public class Result
{
    /// <summary>
    /// Gets a value indicating whether the operation succeeded.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Gets a value indicating whether the operation failed.
    /// </summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>
    /// Gets the error message describing why the operation failed.
    /// Contains an empty string for successful results.
    /// </summary>
    public string Error { get; }

    protected Result(bool isSuccess, string error)
    {
        if (isSuccess && error != string.Empty)
            throw new InvalidOperationException("Cannot have error on success result");
        if (!isSuccess && string.IsNullOrEmpty(error))
            throw new InvalidOperationException("Must have error on failure result");

        IsSuccess = isSuccess;
        Error = error;
    }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    /// <returns>A new successful <see cref="Result"/>.</returns>
    public static Result Success() => new(true, string.Empty);

    /// <summary>
    /// Creates a failure result with the specified error message.
    /// </summary>
    /// <param name="error">A description of why the operation failed.</param>
    /// <returns>A new failed <see cref="Result"/>.</returns>
    public static Result Failure(string error) => new(false, error);

    /// <summary>
    /// Creates a successful result containing the specified value.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="value">The value of the successful result.</param>
    /// <returns>A new successful <see cref="Result{T}"/>.</returns>
    public static Result<T> Success<T>(T value) => Result<T>.Success(value);

    /// <summary>
    /// Creates a failure result with the specified error message.
    /// </summary>
    /// <typeparam name="T">The type of the value that would have been returned on success.</typeparam>
    /// <param name="error">A description of why the operation failed.</param>
    /// <returns>A new failed <see cref="Result{T}"/>.</returns>
    public static Result<T> Failure<T>(string error) => Result<T>.Failure(error);

    /// <summary>
    /// Pattern matches on the result, executing the appropriate function based on success or failure.
    /// </summary>
    /// <typeparam name="TMatch">The return type of the match functions.</typeparam>
    /// <param name="onSuccess">The function to execute if the result is successful.</param>
    /// <param name="onFailure">The function to execute if the result is a failure. Receives the error message.</param>
    /// <returns>The result of the executed function.</returns>
    public TMatch Match<TMatch>(Func<TMatch> onSuccess, Func<string, TMatch> onFailure)
    {
        return IsSuccess ? onSuccess() : onFailure(Error);
    }

    /// <summary>
    /// Maps a successful result to a new <see cref="Result"/> using the provided mapping function.
    /// Failure results are passed through unchanged.
    /// </summary>
    /// <param name="onSuccess">The function to execute if the result is successful.</param>
    /// <returns>A new <see cref="Result"/> based on the mapping function, or the original failure.</returns>
    public Result Map(Func<Result> onSuccess)
    {
        return IsSuccess ? onSuccess() : Failure(Error);
    }
}
