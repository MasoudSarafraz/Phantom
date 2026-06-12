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

    public new static Result<T> Success(T value) => new(value, true, string.Empty);
    public new static Result<T> Failure(string error) => new(default!, false, error);

    public static implicit operator Result<T>(T value) => Success(value);
}
