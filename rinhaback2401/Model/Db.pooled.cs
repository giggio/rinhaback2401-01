#if POOL_OBJECTS
using Npgsql;

namespace RinhaBack2401.Model;

public sealed partial class Db : IAsyncDisposable
{
    private readonly Pool<NpgsqlCommand> insertCommandPool = CreateInsertCommandPool(
#if !EXTRAOPTIMIZE
        loggerFactory.CreateLogger<Pool<NpgsqlCommand>>()
#endif
        );
    private readonly Pool<NpgsqlCommand> getClienteCommandPool = CreateGetClienteCommandPool(
#if !EXTRAOPTIMIZE
        loggerFactory.CreateLogger<Pool<NpgsqlCommand>>()
#endif
        );
    private readonly Pool<NpgsqlCommand> getTransacoesCommandPool = CreateGetTransacoesCommandPool(
#if !EXTRAOPTIMIZE
        loggerFactory.CreateLogger<Pool<NpgsqlCommand>>()
#endif
        );
#if !EXTRAOPTIMIZE
    public int QuantityInsertCommandPoolItemssAvailable => insertCommandPool.QuantityAvailable;
    public int QuantityGetClienteCommandPoolItemssAvailable => getClienteCommandPool.QuantityAvailable;
    public int QuantityGetTransacoesCommandPoolItemssAvailable => getTransacoesCommandPool.QuantityAvailable;
    public int QuantityInsertCommandPoolItemssWaiting => insertCommandPool.WaitingRenters;
    public int QuantityGetClienteCommandPoolItemssWaiting => getClienteCommandPool.WaitingRenters;
    public int QuantityGetTransacoesCommandPoolItemssWaiting => getTransacoesCommandPool.WaitingRenters;
#endif
    private static Pool<NpgsqlCommand> CreateInsertCommandPool(
#if !EXTRAOPTIMIZE
        ILogger<Pool<NpgsqlCommand>> logger
#endif
        ) =>
        CreateCommandPool(
#if !EXTRAOPTIMIZE
            logger,
#endif
            "select criartransacao($1, $2, $3)",
            1000,
            new NpgsqlParameter<int>() { NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Integer },
            new NpgsqlParameter<int>() { NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Integer },
            new NpgsqlParameter<string>() { NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Varchar });

    private static Pool<NpgsqlCommand> CreateGetClienteCommandPool(
#if !EXTRAOPTIMIZE
        ILogger<Pool<NpgsqlCommand>> logger
#endif
        ) =>
        CreateCommandPool(
#if !EXTRAOPTIMIZE
        logger,
#endif
        "SELECT saldo, limite FROM cliente WHERE id = $1",
        200,
        new NpgsqlParameter<int>() { NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Integer });

    private static Pool<NpgsqlCommand> CreateGetTransacoesCommandPool(
#if !EXTRAOPTIMIZE
        ILogger<Pool<NpgsqlCommand>> logger
#endif
        ) =>
        CreateCommandPool(
#if !EXTRAOPTIMIZE
        logger,
#endif
        "SELECT valor, descricao, realizadaem FROM transacao WHERE idcliente = $1 ORDER BY id DESC LIMIT 10",
        200,
        new NpgsqlParameter<int>() { NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Integer });

    private static Pool<NpgsqlCommand> CreateCommandPool(
#if !EXTRAOPTIMIZE
        ILogger<Pool<NpgsqlCommand>> logger,
#endif
        string commandText, int numberOfCommandsPerPool, params NpgsqlParameter[] parameters)
    {
        var command = new NpgsqlCommand(commandText);
        command.Parameters.AddRange(parameters);
        var commands = new List<NpgsqlCommand>(numberOfCommandsPerPool) { command };
        for (var k = 0; k < numberOfCommandsPerPool - 1; k++)
            commands.Add(command.Clone());
        return new(commands,
#if !EXTRAOPTIMIZE
            logger,
#endif
            command => command.Connection = null
            );
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
            return;
        disposed = true;
        if (insertCommandPool is not null)
            await insertCommandPool.DisposeAsync();
        if (getClienteCommandPool is not null)
            await getClienteCommandPool.DisposeAsync();
        if (getTransacoesCommandPool is not null)
            await getTransacoesCommandPool.DisposeAsync();
    }
}
#endif
