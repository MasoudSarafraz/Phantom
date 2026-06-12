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
}
