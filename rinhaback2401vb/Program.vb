Imports Microsoft.AspNetCore.Builder
Imports Microsoft.AspNetCore.Http
Imports Microsoft.AspNetCore.Http.Extensions
Imports Microsoft.AspNetCore.Http.HttpResults
Imports Microsoft.AspNetCore.Http.Timeouts
Imports Microsoft.Extensions.Configuration
Imports Microsoft.Extensions.DependencyInjection
Imports Microsoft.Extensions.Logging
Imports Npgsql
#If Not EXTRAOPTIMIZE Then
Imports OpenTelemetry.Metrics
Imports OpenTelemetry.Resources
#End If
Imports RinhaBack2401.Model
Imports System
Imports System.Text.Json
Imports System.Threading

Module Program
    '<MTAThread>
    Public Function Main(args As String()) As Integer
        Dim builder = WebApplication.CreateBuilder(args)
        builder.Services.AddRequestTimeouts(Sub(options) options.DefaultPolicy = New RequestTimeoutPolicy())
        Dim connectionString = builder.Configuration.GetConnectionString("Rinha")
        If String.IsNullOrWhiteSpace(connectionString) Then
            Console.Error.WriteLine("No connection string found.")
            Return 1
        End If
        builder.Services.Configure(Of DbConfig)(Sub(dbConfig) dbConfig.ConnectionString = connectionString)
        builder.Services.AddSingleton(Of Db)()
        builder.Services.AddLogging(Sub(opt) opt.AddSimpleConsole(Sub(options) options.TimestampFormat = "[HH:mm:ss:fff] "))
#If Not EXTRAOPTIMIZE Then
        builder.Services.AddHealthChecks()
#End If
        Dim addSwagger = builder.Configuration.GetValue(Of Boolean)("AddSwagger")
        If addSwagger Then
#If DEBUG Then
            builder.Services.AddEndpointsApiExplorer()
            builder.Services.AddSwaggerGen(Sub(o)
                                               o.SupportNonNullableReferenceTypes()
                                               o.EnableAnnotations()
                                           End Sub
            )
#Else
                Console.Error.WriteLine("Swagger is only available in DEBUG builds.")
                Return 1
#End If
        End If
        builder.Services.ConfigureHttpJsonOptions(Sub(options) options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower)
        Dim addCounters = builder.Configuration.GetValue(Of Boolean)("AddCounters")
        If addCounters Then
#If EXTRAOPTIMIZE Then
                Console.Error.WriteLine("Counters are not available in optimized builds.")
                Return 1
#Else
            builder.Services.AddOpenTelemetry().
                ConfigureResource(Sub(b) b.AddService(serviceName:="Rinha")).
                WithMetrics(Sub(b) b.AddAspNetCoreInstrumentation())
#End If
        End If
        Dim app = builder.Build()
        app.UseRequestTimeouts()
        Dim addLogging = builder.Configuration.GetValue(Of Boolean)("AddLogging")
        If addLogging Then
#If EXTRAOPTIMIZE Then
                Console.Error.WriteLine("Logging is not available in optimized builds.")
                Return 1
#Else
            app.Use(Async Function(context, [next])
                        Try
                            Await [next](context)
                        Catch ex As Exception
                            app.Logger.AppError(context.Request.GetDisplayUrl(), ex.ToString())
                            Throw
                        End Try
                    End Function
                )
#End If
        End If
        If addSwagger Then
#If DEBUG Then
            app.UseSwagger()
            app.UseSwaggerUI()
            app.MapGet("/", Sub() Results.Redirect("/swagger")).ExcludeFromDescription()
#End If
        End If
        app.MapPost("/clientes/{idCliente}/transacoes",
                    Async Function(idCliente As Integer, transacao As TransacaoModel, db As Db, cancellationToken As CancellationToken) As Task(Of Results(Of Ok(Of Transacoes), NotFound, UnprocessableEntity, StatusCodeHttpResult))
                        If String.IsNullOrWhiteSpace(transacao.Descricao) OrElse transacao.Descricao.Length > 10 Then
                            Return TypedResults.UnprocessableEntity()
                        End If
                        Dim valor As Integer
                        If Not Integer.TryParse(transacao.Valor.ToString(), valor) Then
                            Return TypedResults.UnprocessableEntity()
                        End If
                        Dim tipoTransacao As TipoTransacao
                        Select Case transacao.Tipo
                            Case "c"
                                tipoTransacao = TipoTransacao.c
                            Case "d"
                                tipoTransacao = TipoTransacao.d
                            Case Else
                                tipoTransacao = TipoTransacao.Incorrect
                        End Select

                        If tipoTransacao = TipoTransacao.Incorrect Then
                            Return TypedResults.UnprocessableEntity()
                        End If
                        Try
                            Dim x = Await db.AddAsync(idCliente, New Transacao(valor, tipoTransacao, transacao.Descricao), cancellationToken)
                            If TypeOf x Is Ok(Of (Integer, Integer), AddError) Then
                                Dim y = DirectCast(x, Ok(Of (limite As Integer, saldo As Integer), AddError))
                                Return TypedResults.Ok(New Transacoes(y.Value.limite, y.Value.saldo))
                            End If
                            If TypeOf x Is [Error](Of (Integer, Integer), AddError) Then
                                Dim y = DirectCast(x, [Error](Of (limite As Integer, saldo As Integer), AddError))
                                If y.Error = AddError.ClientNotFound Then
                                    Return TypedResults.NotFound()
                                End If
                                If y.Error = AddError.LimitExceeded Then
                                    Return TypedResults.UnprocessableEntity()
                                End If
                            End If
                            Throw New InvalidOperationException("Invalid return from AddAsync.")
                        Catch ex As OperationCanceledException
                            Return TypedResults.StatusCode(408)
                        End Try
                    End Function)
        app.MapGet("/clientes/{idCliente}/extrato",
                   Async Function(idCliente As Integer, db As Db, cancellationToken As CancellationToken) As Task(Of IResult)
                       Try
                           Dim extrato = Await db.GetExtratoAsync(idCliente, cancellationToken)
                           If extrato Is Nothing Then Return TypedResults.NotFound()
                           Return TypedResults.Ok(extrato)
                       Catch ex As OperationCanceledException
                           Return TypedResults.StatusCode(408)
                       End Try
                   End Function)
#If Not EXTRAOPTIMIZE Then
        app.MapHealthChecks("/healthz").ExcludeFromDescription()
        If addLogging Then NpgsqlLoggingConfiguration.InitializeLogging(app.Services.GetRequiredService(Of ILoggerFactory)())
        If addCounters Then app.AddCustomMeters()
#End If
        app.Run()
        Return 0
    End Function
End Module
