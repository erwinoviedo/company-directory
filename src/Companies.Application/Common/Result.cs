using System.Diagnostics.CodeAnalysis;

namespace Companies.Application.Common;

public enum ErrorKind
{
    Validation,
    NotFound,
    Conflict,
}

/// <summary>
/// An expected failure, described without any reference to HTTP. The API layer decides the status code.
/// </summary>
public sealed record Error(ErrorKind Kind, string Message, IReadOnlyDictionary<string, string[]>? FieldErrors = null)
{
    public static Error Validation(IReadOnlyDictionary<string, string[]> fieldErrors) =>
        new(ErrorKind.Validation, string.Join(" ", fieldErrors.Values.SelectMany(messages => messages)), fieldErrors);

    public static Error NotFound(string message) => new(ErrorKind.NotFound, message);

    public static Error Conflict(string message) => new(ErrorKind.Conflict, message);
}

/// <summary>Outcome of an operation that returns nothing on success.</summary>
public sealed class Result
{
    private Result(Error? error) => Error = error;

    public Error? Error { get; }

    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    public static Result Success() => new(null);

    public static implicit operator Result(Error error) => new(error);
}

/// <summary>Outcome of an operation that returns a value on success.</summary>
public sealed class Result<T>
{
    private Result(T? value, Error? error)
    {
        Value = value;
        Error = error;
    }

    public T? Value { get; }

    public Error? Error { get; }

    [MemberNotNullWhen(true, nameof(Value))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    public static implicit operator Result<T>(T value) => new(value, null);

    public static implicit operator Result<T>(Error error) => new(default, error);
}
