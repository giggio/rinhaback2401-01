using RinhaBack2401.Model;

namespace RinhaBack2401;

#if !EXTRAOPTIMIZE
public static partial class Logs
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "Got unhandled exception at url {url}:\n{exceptionMessage}.")]
    public static partial void AppError(this ILogger logger, string url, string exceptionMessage);

    [LoggerMessage(EventId = 2, Level = LogLevel.Trace, Message = "Inserted {rowsCount} rows into database. Details:\n{details}")]
    public static partial void DbInserted(this ILogger logger, int rowsCount, string? details);

    [LoggerMessage(EventId = 3, Level = LogLevel.Trace, Message = "Pool of {typeName} rented an item, has {itemsCount} before renting.")]
    public static partial void PoolRentingItem(this ILogger logger, string typeName, int itemsCount);

    [LoggerMessage(EventId = 4, Level = LogLevel.Trace, Message = "Pool of {typeName} returned an item, had {itemsCount} after return.")]
    public static partial void PoolReturnedItem(this ILogger logger, string typeName, int itemsCount);

    [LoggerMessage(EventId = 5, Level = LogLevel.Trace, Message = "Pool of {typeName} returning all items, had {itemsCount}.")]
    public static partial void PoolReturningAllItems(this ILogger logger, string typeName, int itemsCount);

    [LoggerMessage(EventId = 6, Level = LogLevel.Trace, Message = "Pool of {typeName} created with {itemsCount}.")]
    public static partial void PoolCreated(this ILogger logger, string typeName, int itemsCount);

    [LoggerMessage(EventId = 7, Level = LogLevel.Trace, Message = "Inserted transacao rows into database for client {idCliente}, tipo: {tipo}, valor: {valor}")]
    public static partial void DbInserted(this ILogger logger, int idCliente, int valor, TipoTransacao tipo);
}

public sealed class AppLogs { }
#endif
