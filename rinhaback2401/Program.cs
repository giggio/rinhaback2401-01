using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Http.Timeouts;
using Npgsql;
#if !EXTRAOPTIMIZE
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
#endif
using RinhaBack2401.Model;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRequestTimeouts(options => options.DefaultPolicy = new RequestTimeoutPolicy { Timeout = TimeSpan.FromSeconds(60) });
var connectionString = builder.Configuration.GetConnectionString("Rinha");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine("No connection string found.");
    return 1;
}
builder.Services.Configure<DbConfig>(dbConfig => dbConfig.ConnectionString = connectionString);
builder.Services.AddSingleton<Db>();

builder.Services.AddLogging(opt => opt.AddSimpleConsole(options => options.TimestampFormat = "[HH:mm:ss:fff] "));

#if !EXTRAOPTIMIZE
builder.Services.AddHealthChecks();
#endif

var addSwagger = builder.Configuration.GetValue<bool>("AddSwagger");
if (addSwagger)
{
#if DEBUG
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(o =>
    {
        o.SupportNonNullableReferenceTypes();
        o.EnableAnnotations();
    });
#else
    Console.Error.WriteLine("Swagger is only available in DEBUG builds.");
    return 1;
#endif
}

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, RinhaJsonContext.Default);
});
var addCounters = builder.Configuration.GetValue<bool>("AddCounters");
if (addCounters)
{
#if EXTRAOPTIMIZE
    Console.Error.WriteLine("Counters are not available in optimized builds.");
    return 1;
#else
    builder.Services.AddOpenTelemetry()
        .ConfigureResource(builder => builder.AddService(serviceName: "Rinha"))
        .WithMetrics(builder => builder.AddAspNetCoreInstrumentation());
#endif
}

var app = builder.Build();
app.UseRequestTimeouts();

var addLogging = builder.Configuration.GetValue<bool>("AddLogging");
if (addLogging)
{
#if EXTRAOPTIMIZE
    Console.Error.WriteLine("Logging is not available in optimized builds.");
    return 1;
#else
    app.Use(async (context, next) =>
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            app.Logger.AppError(context.Request.GetDisplayUrl(), ex.ToString());
            throw;
        }
    });
#endif
}
if (addSwagger)
{
#if DEBUG
    app.UseSwagger();
    app.UseSwaggerUI();
    app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
#endif
}
app.MapPost("/clientes/{idCliente}/transacoes", async Task<Results<Ok<Transacoes>, NotFound, UnprocessableEntity, StatusCodeHttpResult>>
    (int idCliente, TransacaoModel transacao, Db db, CancellationToken cancellationToken) =>
{
    if (transacao.Descricao is null or "" or { Length: > 10 })
        return TypedResults.UnprocessableEntity();
    if (int.TryParse(transacao.Valor?.ToString(), out var valor) is false)
        return TypedResults.UnprocessableEntity();
    var tipoTransacao = transacao.Tipo switch
    {
        "c" => TipoTransacao.c,
        "d" => TipoTransacao.d,
        _ => TipoTransacao.Incorrect
    };
    if (tipoTransacao == TipoTransacao.Incorrect)
        return TypedResults.UnprocessableEntity();
    try
    {
        return await db.AddAsync(idCliente, new Transacao(valor, tipoTransacao, transacao.Descricao), cancellationToken) switch
        {
            Ok<(int, int), AddError>((int limite, int saldo)) => TypedResults.Ok(new Transacoes(limite, saldo)),
            Error<(int, int), AddError>(AddError.ClientNotFound) => TypedResults.NotFound(),
            Error<(int, int), AddError>(AddError.LimitExceeded) => TypedResults.UnprocessableEntity(),
            _ => throw new InvalidOperationException("Invalid return from AddAsync.")
        };
    }
    catch (OperationCanceledException)
    {
        return TypedResults.StatusCode(408);
    }
});
app.MapGet("/clientes/{idCliente}/extrato", async Task<Results<Ok<Extrato>, NotFound, StatusCodeHttpResult>> (int idCliente, Db db, CancellationToken cancellationToken) =>
{
    try
    {
        var extrato = await db.GetExtratoAsync(idCliente, cancellationToken);
        if (extrato is null)
            return TypedResults.NotFound();
        return TypedResults.Ok(extrato);
    }
    catch (OperationCanceledException)
    {
        return TypedResults.StatusCode(408);
    }
});
#if !EXTRAOPTIMIZE
app.MapHealthChecks("/healthz").ExcludeFromDescription();
if (addLogging)
    NpgsqlLoggingConfiguration.InitializeLogging(app.Services.GetRequiredService<ILoggerFactory>());
if (addCounters)
    app.AddCustomMeters();
#endif
app.Run();

return 0;
