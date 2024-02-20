using Microsoft.Extensions.Options;
using Npgsql;
using System.Runtime.CompilerServices;

namespace RinhaBack2401.Model;

public sealed partial class Db(IOptions<DbConfig> configOption
#if !EXTRAOPTIMIZE
    , ILogger<Db> logger
#if POOL_OBJECTS
    , ILoggerFactory loggerFactory
#endif
#endif
    ) : IAsyncDisposable
{
    private bool disposed;
    private readonly NpgsqlDataSource dataSource =
        new NpgsqlSlimDataSourceBuilder(configOption.Value.ConnectionString ?? throw new NullReferenceException("ConnectionString should not be null."))
        .EnableRecords()
        .Build();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private NpgsqlConnection CreateConnection() => dataSource.OpenConnection();


    public async Task<(AddStatus, int limite, int saldo)> AddAsync(int idCliente, Transacao transacao)
    {
#if POOL_OBJECTS
        await using var commandPoolItem = await insertCommandPool.RentAsync();
        var command = commandPoolItem.Value;
#else
        await using var command = insertCommand.Clone();
#endif
        command.Parameters[0].Value = idCliente;
        command.Parameters[1].Value = transacao.Tipo == TipoTransacao.c ? transacao.Valor : transacao.Valor * -1;
        command.Parameters[2].Value = transacao.Descricao;
        await using var connection = CreateConnection();
        command.Connection = connection;
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            throw new InvalidOperationException("Could not read from db.");
        var record = reader.GetFieldValue<object[]>(0);
        if (record.Length == 1)
        {
            var failureCode = (int)record[0];
            if (failureCode == -1)
                return (AddStatus.ClientNotFound, 0, 0);
            else if (failureCode == -2)
                return (AddStatus.LimitExceeded, 0, 0);
            else
                throw new InvalidOperationException("Invalid failure code.");
        }
        var (saldo, limite) = ((int)record[0], -1 * (int)record[1]);
#if !EXTRAOPTIMIZE
        logger.DbInserted(idCliente, transacao.Valor, transacao.Tipo);
#endif
        return (AddStatus.Success, limite, saldo);
    }

    public async Task<(bool found, Extrato? extrato)> GetExtratoAsync(int idCliente)
    {
        using var connection = CreateConnection();
        var (success, saldo) = await GetSaldoAsync(idCliente, connection);
        if (success)
        {
            var transacoes = await GetTransacoesAsync(idCliente, connection);
            var extrato = new Extrato(saldo!, transacoes);
            return (true, extrato);
        }
        return (false, null);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private async Task<(bool success, Saldo? saldo)> GetSaldoAsync(int idCliente, NpgsqlConnection connection)
    {
#if POOL_OBJECTS
        await using var commandPoolItem = await getClienteCommandPool.RentAsync();
        var command = commandPoolItem.Value;
#else
        using var command = getClienteCommand.Clone();
#endif
        command.Connection = connection;
        command.Parameters[0].Value = idCliente;
        await using var reader = await command.ExecuteReaderAsync();
        var success = await reader.ReadAsync();
        if (success)
        {
            var saldo = new Saldo(reader.GetInt32(0), DateTime.UtcNow, reader.GetInt32(1) * -1);
            return (true, saldo);
        }
        return (false, null);
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private async Task<List<TransacaoComData>> GetTransacoesAsync(int idCliente, NpgsqlConnection connection)
    {
#if POOL_OBJECTS
        await using var commandPoolItem = await getTransacoesCommandPool.RentAsync();
        var command = commandPoolItem.Value;
#else
        using var command = getTransacoesCommand.Clone();
#endif
        command.Connection = connection;
        command.Parameters[0].Value = idCliente;
        var transacoes = new List<TransacaoComData>(10);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var valor = reader.GetInt32(0);
            var transacao = new TransacaoComData(Math.Abs(valor), valor < 0 ? TipoTransacao.d : TipoTransacao.c, reader.GetString(1), DateTime.SpecifyKind(reader.GetDateTime(2), DateTimeKind.Utc));
            transacoes.Add(transacao);
        }
        return transacoes;
    }

    public async Task WarmUpAsync(int count = 30_000) =>
        await Task.WhenAll(Enumerable.Range(0, count).Select((i) => GetExtratoAsync((i % 5) + 1)));
}
