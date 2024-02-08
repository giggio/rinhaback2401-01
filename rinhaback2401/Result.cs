namespace RinhaBack2401;

public abstract record class Result<T, TError>;

public record class Ok<T, TError>(T Value) : Result<T, TError>;

public record class Error<T, TError>(TError Value) : Result<T, TError>;
