using Npgsql;

namespace RinhaBack2401.Model;

#if !POOL_OBJECTS
public sealed partial class Db : IAsyncDisposable
{
    private readonly NpgsqlCommand insertCommand = CreateInsertCommand();
    private readonly NpgsqlCommand getClienteCommand = CreateGetClienteCommand();
    private readonly NpgsqlCommand getTransacoesCommand = CreateGetTransacoesCommand();

    private static NpgsqlCommand CreateInsertCommand() =>
        new("select criartransacao($1, $2, $3)")
        {
            Parameters =
            {
                new NpgsqlParameter<int>() { NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Integer },
                new NpgsqlParameter<int>() { NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Integer },
                new NpgsqlParameter<string>() { NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Varchar }
            }
        };

    private static NpgsqlCommand CreateGetClienteCommand() =>
        new("SELECT saldo, limite FROM cliente WHERE id = $1")
        { Parameters = { new NpgsqlParameter<int>() { NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Integer } } };

    private static NpgsqlCommand CreateGetTransacoesCommand() =>
        new("SELECT valor, descricao, realizadaem FROM transacao WHERE idcliente = $1 ORDER BY id DESC LIMIT 10")
        { Parameters = { new NpgsqlParameter<int>() { NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Integer } } };


    public async ValueTask DisposeAsync()
    {
        if (disposed)
            return;
        disposed = true;
        if (insertCommand is not null)
            await insertCommand.DisposeAsync();
        if (getClienteCommand is not null)
            await getClienteCommand.DisposeAsync();
        if (getTransacoesCommand is not null)
            await getTransacoesCommand.DisposeAsync();
    }

}

#endif
