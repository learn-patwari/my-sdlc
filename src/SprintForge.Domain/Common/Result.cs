namespace SprintForge.Domain.Common;

/// <summary>Represents a success or failure outcome without throwing exceptions for expected failures.</summary>
public sealed class Result<T>
{
    private Result(T? value, string? error)
    {
        Value = value;
        Error = error;
        IsSuccess = error is null;
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public T? Value { get; }
    public string? Error { get; }

    public static Result<T> Success(T value) => new(value, null);
    public static Result<T> Failure(string error) => new(default, error);

    public Result<TNext> Map<TNext>(Func<T, TNext> map) =>
        IsSuccess ? Result<TNext>.Success(map(Value!)) : Result<TNext>.Failure(Error!);
}

public static class Result
{
    public static Result<T> Success<T>(T value) => Result<T>.Success(value);
    public static Result<T> Failure<T>(string error) => Result<T>.Failure(error);
}
