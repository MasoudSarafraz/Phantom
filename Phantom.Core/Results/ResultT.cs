namespace Phantom.Core.Results;

public class Result<T> : Result
{
    private readonly T _value;

    public T Value => IsSuccess
        ? _value
        : throw new InvalidOperationException("Cannot access value of a failure result");

    private Result(T value, bool isSuccess, string error) : base(isSuccess, error)
    {
        _value = value;
    }

    public static Result<T> Success(T value) => new(value, true, string.Empty);

    public new static Result<T> Failure(string error) => new(default!, false, error);

    public Result<TResult> Map<TResult>(Func<T, TResult> map)
    {
        return IsSuccess ? Result<TResult>.Success(map(_value)) : Result<TResult>.Failure(Error);
    }

    public Result<TResult> Bind<TResult>(Func<T, Result<TResult>> bind)
    {
        return IsSuccess ? bind(_value) : Result<TResult>.Failure(Error);
    }

    public TMatch Match<TMatch>(Func<T, TMatch> onSuccess, Func<string, TMatch> onFailure)
    {
        return IsSuccess ? onSuccess(_value) : onFailure(Error);
    }
}
