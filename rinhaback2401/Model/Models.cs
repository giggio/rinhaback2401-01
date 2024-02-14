using System.Text.Json.Serialization;

namespace RinhaBack2401.Model;

// common
[JsonConverter(typeof(JsonStringEnumConverter<TipoTransacao>))]
public enum TipoTransacao { Incorrect, c, d }

// response
public record class Extrato(Saldo Saldo, List<TransacaoComData> UltimasTransacoes);

public record class Saldo(int Total, DateTime DataExtrato, int Limite);

public record class TransacaoComData(int Valor, TipoTransacao Tipo, string Descricao, DateTime RealizadaEm);

public record class Transacoes(int Limite, int Saldo);

// request
public record struct TransacaoModel(object Valor, string Tipo, string Descricao);
public record struct Transacao(int Valor, TipoTransacao Tipo, string Descricao);
