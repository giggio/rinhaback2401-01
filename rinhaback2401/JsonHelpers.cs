using RinhaBack2401.Model;
using System.Text.Json.Serialization;

namespace RinhaBack2401;

[JsonSerializable(typeof(Transacao))]
[JsonSerializable(typeof(Transacoes))]
[JsonSerializable(typeof(Extrato))]
[JsonSerializable(typeof(Saldo))]
[JsonSerializable(typeof(TransacaoComData))]
[JsonSerializable(typeof(List<TransacaoComData>))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(DateTime))]
internal sealed partial class RinhaJsonContext : JsonSerializerContext { }
