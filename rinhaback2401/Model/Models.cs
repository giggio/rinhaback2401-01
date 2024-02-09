using System.Text.Json.Serialization;

namespace RinhaBack2401.Model;

// common
[JsonConverter(typeof(JsonStringEnumConverter<TipoTransacao>))]
public enum TipoTransacao { Incorrect, c, d }

// response
public record Extrato(Saldo Saldo, List<TransacaoComData> UltimasTransacoes);

public record struct Saldo(int Total, DateTime DataExtrato, int Limite);

public record struct TransacaoComData(int Valor, TipoTransacao Tipo, string Descricao, DateTime RealizadaEm);

public record Transacoes(int Limite, int Saldo);

// request
public record struct TransacaoModel(object Valor, string Tipo, string Descricao);
public record struct Transacao(int Valor, TipoTransacao Tipo, string Descricao);
