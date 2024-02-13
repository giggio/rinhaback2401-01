Imports Microsoft.AspNetCore.Builder
Imports Microsoft.Extensions.DependencyInjection
Imports RinhaBack2401.Model
Imports System.Diagnostics.Metrics
Imports System.Runtime.CompilerServices

Module MeterExtensions
    <Extension()>
    Sub AddCustomMeters(ByVal app As WebApplication)
        Dim db = app.Services.GetRequiredService(Of Db)()
        Dim meterFactory = app.Services.GetRequiredService(Of IMeterFactory)()
        Dim meter = meterFactory.Create("Rinha", "1.0.0")
        meter.CreateObservableGauge("Command (Insert) Available", Function() db.QuantityInsertCommandPoolItemssAvailable)
        meter.CreateObservableGauge("Command (Cliente) Available", Function() db.QuantityGetClienteCommandPoolItemssAvailable)
        meter.CreateObservableGauge("Command (Transações) Available", Function() db.QuantityGetTransacoesCommandPoolItemssAvailable)
        meter.CreateObservableGauge("Command (Insert) waiting for a pool item", Function() db.QuantityInsertCommandPoolItemssWaiting)
        meter.CreateObservableGauge("Command (Cliente) waiting for a pool item", Function() db.QuantityGetClienteCommandPoolItemssWaiting)
        meter.CreateObservableGauge("Command (Transações) waiting for a pool item", Function() db.QuantityGetTransacoesCommandPoolItemssWaiting)
    End Sub
End Module


