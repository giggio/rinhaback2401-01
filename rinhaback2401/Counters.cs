using RinhaBack2401.Model;
using System.Diagnostics.Metrics;

namespace RinhaBack2401;

public static class MeterExtensions
{
    public static void AddCustomMeters(this WebApplication app)
    {
        var db = app.Services.GetRequiredService<Db>();
        var meterFactory = app.Services.GetRequiredService<IMeterFactory>();
        var meter = meterFactory.Create("Rinha", "1.0.0");
        meter.CreateObservableGauge("Connections Available", () => db.QuantityConnectionPoolItemsAvailable);
        meter.CreateObservableGauge("Command (Insert) Available", () => db.QuantityInsertCommandPoolItemssAvailable);
        meter.CreateObservableGauge("Command (Cliente) Available", () => db.QuantityGetClienteCommandPoolItemssAvailable);
        meter.CreateObservableGauge("Command (Transações) Available", () => db.QuantityGetTransacoesCommandPoolItemssAvailable);
        meter.CreateObservableGauge("Connections waiting for a pool item", () => db.QuantityConnectionPoolItemsWaiting);
        meter.CreateObservableGauge("Command (Insert) waiting for a pool item", () => db.QuantityInsertCommandPoolItemssWaiting);
        meter.CreateObservableGauge("Command (Cliente) waiting for a pool item", () => db.QuantityGetClienteCommandPoolItemssWaiting);
        meter.CreateObservableGauge("Command (Transações) waiting for a pool item", () => db.QuantityGetTransacoesCommandPoolItemssWaiting);
    }
}
