using Phantom.Core.Exceptions;

namespace Phantom.Core.Results;

public class Result
{
    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

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

    public static Result Success() => new(true, string.Empty);

    public static Result Failure(string error) => new(false, error);

    public static Result<T> Success<T>(T value) => Result<T>.Success(value);

    public static Result<T> Failure<T>(string error) => Result<T>.Failure(error);

    public TMatch Match<TMatch>(Func<TMatch> onSuccess, Func<string, TMatch> onFailure)
    {
        return IsSuccess ? onSuccess() : onFailure(Error);
    }

    public Result Map(Func<Result> onSuccess)
    {
        return IsSuccess ? onSuccess() : Failure(Error);
    }

    /// <summary>
    /// Throws a <see cref="BusinessRuleException"/> if this result represents a failure.
    /// Use this to convert a Result-based invariant check into an exception when the calling
    /// context prefers exception-based flow (e.g., inside ASP.NET Core controllers that
    /// delegate to <c>ExceptionHandlingMiddleware</c> to render RFC 7807 Problem Details).
    ///
    /// This bridges the Result-first and Exception-first programming styles so application
    /// code can choose the style that fits the call site without forcing the aggregate to
    /// commit to one or the other.
    /// </summary>
    /// <returns>This same <see cref="Result"/> if it represents success.</returns>
    public Result ThrowIfFailure()
    {
        if (IsFailure)
            throw new BusinessRuleException(Error);
        return this;
    }
}

