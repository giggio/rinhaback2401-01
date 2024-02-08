using Microsoft.Extensions.Options;
using Npgsql;
using System.Data;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace RinhaBack2401.Model;

#if DEBUG
public sealed class Db(IOptions<DbConfig> configOption, ILogger<Db> logger, ILoggerFactory loggerFactory) : IAsyncDisposable
#else
public sealed class Db(IOptions<DbConfig> configOption, ILoggerFactory loggerFactory) : IAsyncDisposable
#endif
{
    private readonly Pool<NpgsqlConnection> connectionPool = CreateConnections(configOption.Value, loggerFactory.CreateLogger<Pool<NpgsqlConnection>>());
    private readonly Pool<NpgsqlCommand> insertCommandPool = CreateInsertCommandPool(loggerFactory.CreateLogger<Pool<NpgsqlCommand>>());
    private readonly Pool<NpgsqlCommand> getClienteCommandPool = CreateGetClienteCommandPool(loggerFactory.CreateLogger<Pool<NpgsqlCommand>>());
    private readonly Pool<NpgsqlCommand> getTransacoesCommandPool = CreateGetTransacoesCommandPool(loggerFactory.CreateLogger<Pool<NpgsqlCommand>>());
    private bool disposed;

    private static Pool<NpgsqlConnection> CreateConnections(DbConfig config, ILogger<Pool<NpgsqlConnection>> logger)
    {
        var connections = new List<NpgsqlConnection>(config.PoolSize);
        for (var i = 0; i < config.PoolSize; i++)
        {
            var conn = new NpgsqlConnection(config.ConnectionString);
            conn.Open();
            connections.Add(conn);
        }
        return new(connections, logger);
    }

    private static Pool<NpgsqlCommand> CreateInsertCommandPool(ILogger<Pool<NpgsqlCommand>> logger) =>
        CreateCommandPool(logger,
        "select criartransacao($1, $2, $3)",
        20,
        new NpgsqlParameter<int>() { NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Integer },
        new NpgsqlParameter<int>() { NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Integer },
        new NpgsqlParameter<string>() { NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Varchar });

    private static Pool<NpgsqlCommand> CreateGetClienteCommandPool(ILogger<Pool<NpgsqlCommand>> logger) =>
        CreateCommandPool(logger,
        """SELECT saldo, limite FROM cliente WHERE id = $1""",
        20,
        new NpgsqlParameter<int>() { NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Integer });

    private static Pool<NpgsqlCommand> CreateGetTransacoesCommandPool(ILogger<Pool<NpgsqlCommand>> logger) =>
        CreateCommandPool(logger,
        """SELECT valor, descricao, realizadaem FROM transacao WHERE idcliente = $1 ORDER BY id DESC""",
        20,
        new NpgsqlParameter<int>() { NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Integer });

    private static Pool<NpgsqlCommand> CreateCommandPool(ILogger<Pool<NpgsqlCommand>> logger, string commandText, int numberOfCommandsPerPool, params NpgsqlParameter[] parameters)
    {
        var command = new NpgsqlCommand(commandText);
        command.Parameters.AddRange(parameters);
        var commands = new List<NpgsqlCommand>(numberOfCommandsPerPool) { command };
        for (var k = 0; k < numberOfCommandsPerPool - 1; k++)
            commands.Add(command.Clone());
        return new(commands, logger);
    }

    public async Task<Result<(int limite, int saldo), AddError>> AddAsync(int idCliente, Transacao transacao, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        await using var connectionPoolItem = await connectionPool.RentAsync(cancellationToken);
        var connection = connectionPoolItem.Value;
        Debug.Assert(connection.State == ConnectionState.Open);
        var failureCode = 0;
        var limite = 0;
        var saldo = 0;
        await using var commandPoolItem = await insertCommandPool.RentAsync(cancellationToken);
        var command = commandPoolItem.Value;
        command.Connection = connection;
        command.Parameters[0].Value = idCliente;
        command.Parameters[1].Value = transacao.Tipo == TipoTransacao.c ? transacao.Valor : transacao.Valor * -1;
        command.Parameters[2].Value = transacao.Descricao;
        try
        {
            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                throw new InvalidOperationException("Could not read from db.");
            var record = reader.GetFieldValue<object[]>(0);
            failureCode = (int)record[0];
            if (failureCode != 0)
            {
                if (failureCode == -1)
                    return new Error<(int, int), AddError>(AddError.ClientNotFound);
                else if (failureCode == -2)
                    return new Error<(int, int), AddError>(AddError.LimitExceeded);
                else
                    throw new InvalidOperationException("Invalid failure code.");
            }
            saldo = (int)record[1];
            limite = -1 * (int)record[2];
        }
        finally
        {
            command.Connection = null;
        }
#if DEBUG
        logger.DbInserted(idCliente, transacao.Valor, transacao.Tipo);
#endif
        return new Ok<(int limite, int saldo), AddError>((limite, saldo));
    }


    public async Task<Extrato?> GetExtratoAsync(int idCliente, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        var saldo = await GetSaldoAsync(idCliente, cancellationToken);
        if (saldo is null)
            return null;
        var transacoes = await GetTransacoesAsync(idCliente, cancellationToken);
        var extrato = new Extrato((Saldo)saldo, transacoes);
        return extrato;
    }

    private async Task<Saldo?> GetSaldoAsync(int idCliente, CancellationToken cancellationToken)
    {
        await using var connectionsPoolItem = await connectionPool.RentAsync(cancellationToken);
        var connection = connectionsPoolItem.Value;
        Debug.Assert(connection.State == ConnectionState.Open);
        await using var commandPoolItem = await getClienteCommandPool.RentAsync(cancellationToken);
        var command = commandPoolItem.Value;
        command.Connection = connection;
        command.Parameters[0].Value = idCliente;
        Saldo? saldo = null;
        using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            command.Connection = connection;
            var success = await reader.ReadAsync(cancellationToken);
            if (success)
                saldo = new(reader.GetInt32(0), DateTime.UtcNow, reader.GetInt32(1) * -1);
        }
        command.Connection = null;
        return saldo;
    }

    private async Task<List<TransacaoComData>> GetTransacoesAsync(int idCliente, CancellationToken cancellationToken)
    {
        await using var connectionsPoolItem = await connectionPool.RentAsync(cancellationToken);
        var connection = connectionsPoolItem.Value;
        Debug.Assert(connection.State == ConnectionState.Open);
        await using var commandPoolItem = await getTransacoesCommandPool.RentAsync(cancellationToken);
        var command = commandPoolItem.Value;
        command.Connection = connection;
        command.Parameters[0].Value = idCliente;
        var transacoes = new List<TransacaoComData>();
        using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                var valor = reader.GetInt32(0);
                var transacao = new TransacaoComData(Math.Abs(valor), valor < 0 ? TipoTransacao.d : TipoTransacao.c, reader.GetString(1), DateTime.SpecifyKind(reader.GetDateTime(2), DateTimeKind.Utc));
                transacoes.Add(transacao);
            }
        }
        command.Connection = null;
        return transacoes;
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
        if (connectionPool is not null)
            await connectionPool.DisposeAsync();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(disposed, nameof(Db));

}

public sealed class DbConfig
{
    public int PoolSize
    {
        get
        {
            if (ConnectionString is null)
                return 0;
            var connBuilder = new NpgsqlConnectionStringBuilder(ConnectionString);
            return !connBuilder.Pooling ? 0 : connBuilder.MaxPoolSize;
        }
    }
    public string? ConnectionString { get; internal set; }
}

public enum AddError { ClientNotFound, LimitExceeded }