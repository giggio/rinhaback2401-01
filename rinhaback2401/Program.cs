using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Http.HttpResults;
using Npgsql;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using RinhaBack2401.Model;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("Rinha");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine("No connection string found.");
    return 1;
}
var addLogging = builder.Configuration.GetValue<bool>("AddLogging");
builder.Services.Configure<DbConfig>(dbConfig => dbConfig.ConnectionString = connectionString);

builder.Services.AddSingleton<Db>();
if (addLogging)
    builder.Services.AddLogging(opt => opt.AddSimpleConsole(options => options.TimestampFormat = "[HH:mm:ss:fff] "));
builder.Services.AddHealthChecks();

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
    builder.Services.AddOpenTelemetry()
        .ConfigureResource(builder => builder.AddService(serviceName: "Rinha"))
        .WithMetrics(builder => builder.AddAspNetCoreInstrumentation());
}

var app = builder.Build();

if (addLogging)
{
    app.Use(async (context, next) =>
    {
        try
        {
            await next(context);
        }
        catch (BadHttpRequestException)
        {
            throw;
        }
        catch (Exception ex)
        {
            app.Logger.AppError(context.Request.GetDisplayUrl(), ex.ToString());
            throw;
        }
    });
}
if (addSwagger)
{
#if DEBUG
    app.UseSwagger();
    app.UseSwaggerUI();
    app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
#endif
}
app.MapPost("/clientes/{idCliente}/transacoes", async Task<Results<Ok<Transacoes>, NotFound, UnprocessableEntity>> (int idCliente, Transacao transacao, Db db, CancellationToken cancellationToken) =>
{
    if (transacao.Descricao is null or "" or { Length: > 10 })
        return TypedResults.UnprocessableEntity();
    return await db.AddAsync(idCliente, transacao, cancellationToken) switch
    {
        Ok<(int, int), AddError>((int limite, int saldo)) => TypedResults.Ok(new Transacoes(limite, saldo)),
        Error<(int, int), AddError>(AddError.ClientNotFound) => TypedResults.NotFound(),
        Error<(int, int), AddError>(AddError.LimitExceeded) => TypedResults.UnprocessableEntity(),
        _ => throw new InvalidOperationException("Invalid return from AddAsync.")
    };
});
app.MapGet("/clientes/{idCliente}/extrato", async Task<Results<Ok<Extrato>, NotFound>> (int idCliente, Db db, CancellationToken cancellationToken) =>
{
    var extrato = await db.GetExtratoAsync(idCliente, cancellationToken);
    if (extrato is null)
        return TypedResults.NotFound();
    return TypedResults.Ok(extrato);
});
app.MapHealthChecks("/healthz");
if (addLogging)
{
    var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
    NpgsqlLoggingConfiguration.InitializeLogging(loggerFactory);
}
if (addCounters)
{
    app.AddCustomMeters();
}
app.Run();

return 0;
