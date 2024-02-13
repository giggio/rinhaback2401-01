Imports System.Data
Imports System.Diagnostics
Imports System.Runtime.CompilerServices
Imports System.Runtime.ExceptionServices
Imports System.Threading
Imports Microsoft.Extensions.Logging
Imports Microsoft.Extensions.Options
Imports Npgsql
Imports NpgsqlTypes

Namespace Model
    Public NotInheritable Class Db
        Implements IDisposable

        Private ReadOnly insertCommandPool As Pool(Of NpgsqlCommand)
        Private ReadOnly getClienteCommandPool As Pool(Of NpgsqlCommand)
        Private ReadOnly getTransacoesCommandPool As Pool(Of NpgsqlCommand)
        Private disposed As Boolean
        Private ReadOnly configOption As IOptions(Of DbConfig)
#If EXTRAOPTIMIZE Then
        Public Sub New(configOption As IOptions(Of DbConfig))
#Else
        Private ReadOnly logger As ILogger(Of Db)
        Public Sub New(configOption As IOptions(Of DbConfig), logger As ILogger(Of Db), loggerFactory As ILoggerFactory)
            Me.logger = logger
#End If
#If EXTRAOPTIMIZE Then
            insertCommandPool = CreateInsertCommandPool()
            getClienteCommandPool = CreateGetClienteCommandPool()
            getTransacoesCommandPool = CreateGetTransacoesCommandPool()
#Else
            insertCommandPool = CreateInsertCommandPool(loggerFactory.CreateLogger(Of Pool(Of NpgsqlCommand))())
            getClienteCommandPool = CreateGetClienteCommandPool(loggerFactory.CreateLogger(Of Pool(Of NpgsqlCommand))())
            getTransacoesCommandPool = CreateGetTransacoesCommandPool(loggerFactory.CreateLogger(Of Pool(Of NpgsqlCommand))())
#End If
            Me.configOption = configOption
        End Sub

        Public ReadOnly Property QuantityInsertCommandPoolItemssAvailable As Integer
            Get
                Return insertCommandPool.QuantityAvailable
            End Get
        End Property

        Public ReadOnly Property QuantityGetClienteCommandPoolItemssAvailable As Integer
            Get
                Return getClienteCommandPool.QuantityAvailable
            End Get
        End Property

        Public ReadOnly Property QuantityGetTransacoesCommandPoolItemssAvailable As Integer
            Get
                Return getTransacoesCommandPool.QuantityAvailable
            End Get
        End Property

        Public ReadOnly Property QuantityInsertCommandPoolItemssWaiting As Integer
            Get
                Return insertCommandPool.WaitingRenters
            End Get
        End Property

        Public ReadOnly Property QuantityGetClienteCommandPoolItemssWaiting As Integer
            Get
                Return getClienteCommandPool.WaitingRenters
            End Get
        End Property

        Public ReadOnly Property QuantityGetTransacoesCommandPoolItemssWaiting As Integer
            Get
                Return getTransacoesCommandPool.WaitingRenters
            End Get
        End Property

        Private Function CreateConnection() As NpgsqlConnection
            Dim conn As New NpgsqlConnection(configOption.Value.ConnectionString)
            conn.Open()
            Return conn
        End Function

#If EXTRAOPTIMIZE Then
        Private Shared Function CreateInsertCommandPool() As Pool(Of NpgsqlCommand)
            Return Db.CreateCommandPool("select criartransacao($1, $2, $3)", 1000, New NpgsqlParameter() {New NpgsqlParameter(Of Integer)() With {.NpgsqlDbType = NpgsqlDbType.[Integer]}, New NpgsqlParameter(Of Integer)() With {.NpgsqlDbType = NpgsqlDbType.[Integer]}, New NpgsqlParameter(Of String)() With {.NpgsqlDbType = NpgsqlDbType.Varchar}})
        End Function
#Else
        Private Shared Function CreateInsertCommandPool(logger As ILogger(Of Pool(Of NpgsqlCommand))) As Pool(Of NpgsqlCommand)
            Return CreateCommandPool(logger, "select criartransacao($1, $2, $3)", 1000, New NpgsqlParameter() {New NpgsqlParameter(Of Integer)() With {.NpgsqlDbType = NpgsqlDbType.[Integer]}, New NpgsqlParameter(Of Integer)() With {.NpgsqlDbType = NpgsqlDbType.[Integer]}, New NpgsqlParameter(Of String)() With {.NpgsqlDbType = NpgsqlDbType.Varchar}})
        End Function
#End If

#If EXTRAOPTIMIZE Then
        Private Shared Function CreateGetClienteCommandPool() As Pool(Of NpgsqlCommand)
            Return Db.CreateCommandPool("SELECT saldo, limite FROM cliente WHERE id = $1", 200, New NpgsqlParameter() {New NpgsqlParameter(Of Integer)() With {.NpgsqlDbType = NpgsqlDbType.[Integer]}})
        End Function
#Else
        Private Shared Function CreateGetClienteCommandPool(logger As ILogger(Of Pool(Of NpgsqlCommand))) As Pool(Of NpgsqlCommand)
            Return CreateCommandPool(logger, "SELECT saldo, limite FROM cliente WHERE id = $1", 200, New NpgsqlParameter() {New NpgsqlParameter(Of Integer)() With {.NpgsqlDbType = NpgsqlDbType.[Integer]}})
        End Function
#End If

#If EXTRAOPTIMIZE Then
        Private Shared Function CreateGetTransacoesCommandPool() As Pool(Of NpgsqlCommand)
            Return Db.CreateCommandPool("SELECT valor, descricao, realizadaem FROM transacao WHERE idcliente = $1 ORDER BY id DESC LIMIT 10", 200, New NpgsqlParameter() {New NpgsqlParameter(Of Integer)() With {.NpgsqlDbType = NpgsqlDbType.[Integer]}})
        End Function
#Else
        Private Shared Function CreateGetTransacoesCommandPool(logger As ILogger(Of Pool(Of NpgsqlCommand))) As Pool(Of NpgsqlCommand)
            Return CreateCommandPool(logger, "SELECT valor, descricao, realizadaem FROM transacao WHERE idcliente = $1 ORDER BY id DESC LIMIT 10", 200, New NpgsqlParameter() {New NpgsqlParameter(Of Integer)() With {.NpgsqlDbType = NpgsqlDbType.[Integer]}})
        End Function
#End If

#If EXTRAOPTIMIZE Then
        Private Shared Function CreateCommandPool(commandText As String, numberOfCommandsPerPool As Integer, ParamArray parameters As NpgsqlParameter()) As Pool(Of NpgsqlCommand)
#Else
        Private Shared Function CreateCommandPool(logger As ILogger(Of Pool(Of NpgsqlCommand)), commandText As String, numberOfCommandsPerPool As Integer, ParamArray parameters As NpgsqlParameter()) As Pool(Of NpgsqlCommand)
#End If
            Dim command As New NpgsqlCommand(commandText)
            command.Parameters.AddRange(parameters)
            Dim commands As New List(Of NpgsqlCommand)(numberOfCommandsPerPool) From {command}
            For i As Integer = 0 To numberOfCommandsPerPool - 1 - 1
                commands.Add(command.Clone())
            Next
#If EXTRAOPTIMIZE Then
            Return New Pool(Of NpgsqlCommand)(commands)
#Else
            Return New Pool(Of NpgsqlCommand)(commands, logger)
#End If
        End Function

        Public Async Function AddAsync(idCliente As Integer, transacao As Transacao, cancellationToken As CancellationToken) As Task(Of Result(Of (limite As Integer, Saldo As Integer), AddError))
            ThrowIfDisposed()
            Using connection = CreateConnection()
                Debug.Assert(connection.State = ConnectionState.Open)
                Dim failureCode = 0
                Dim limite = 0
                Dim saldo = 0
                Dim commandPoolItem As PoolItem(Of NpgsqlCommand)? = Nothing
                Dim capturedException As ExceptionDispatchInfo = Nothing
                Dim result As Result(Of (limite As Integer, Saldo As Integer), AddError) = Nothing
                Dim reader As NpgsqlDataReader = Nothing
                Try
                    commandPoolItem = Await insertCommandPool.RentAsync(cancellationToken)
                    Dim command = commandPoolItem.Value.Value
                    Debug.Assert(command.Connection Is Nothing, "Command connection is not nothing")
                    command.Connection = connection
                    command.Parameters(0).Value = idCliente
                    command.Parameters(1).Value = If(transacao.Tipo = TipoTransacao.c, transacao.Valor, transacao.Valor * -1)
                    command.Parameters(2).Value = transacao.Descricao
                    reader = Await command.ExecuteReaderAsync(retryCount:=4, cancellationToken)
                    If Not Await reader.ReadAsync(cancellationToken) Then Throw New InvalidOperationException("Could not read from db.")
                    Dim record = reader.GetFieldValue(Of Object())(0)
                    If record.Length = 1 Then
                        failureCode = DirectCast(record(0), Integer)
                        If failureCode = -1 Then
                            result = New [Error](Of (Integer, Integer), AddError)(AddError.ClientNotFound)
                        ElseIf failureCode = -2 Then
                            result = New [Error](Of (Integer, Integer), AddError)(AddError.LimitExceeded)
                        Else
                            Throw New InvalidOperationException("Invalid failure code.")
                        End If
                    Else
                        saldo = DirectCast(record(0), Integer)
                        limite = -1 * DirectCast(record(1), Integer)
                        result = New Ok(Of (Integer, Integer), AddError)((limite, saldo))
#If Not EXTRAOPTIMIZE Then
                        logger.DbInserted(idCliente, transacao.Valor, transacao.Tipo)
#End If
                    End If
                Catch ex As Exception
                    capturedException = ExceptionDispatchInfo.Capture(ex) ' vb does not support await in catch block 🤦‍️ ‍️
                End Try
                If reader IsNot Nothing Then
                    Await reader.DisposeAsync()
                End If
                If commandPoolItem IsNot Nothing Then
                    commandPoolItem.Value.Value.Connection = Nothing
                    Await commandPoolItem.Value.DisposeAsync()
                End If
                capturedException?.Throw()
                Return result
            End Using
        End Function

        Public Async Function GetExtratoAsync(idCliente As Integer, cancellationToken As CancellationToken) As <Nullable(New Byte() {1, 2})> Task(Of Extrato?)
            ThrowIfDisposed()
            Using connection As NpgsqlConnection = CreateConnection()
                Debug.Assert(connection.State = ConnectionState.Open)
                Dim saldo = Await GetSaldoAsync(idCliente, connection, cancellationToken)
                If saldo Is Nothing Then
                    Return Nothing
                End If
                Dim transacoes = Await GetTransacoesAsync(idCliente, connection, cancellationToken)
                Dim extrato = New Extrato(saldo.Value, transacoes)
                Return extrato
            End Using
        End Function

        Private Async Function GetSaldoAsync(idCliente As Integer, connection As NpgsqlConnection, cancellationToken As CancellationToken) As Task(Of Saldo?)
            Dim commandPoolItem As PoolItem(Of NpgsqlCommand)? = Nothing
            Dim capturedException As ExceptionDispatchInfo = Nothing
            Dim saldo As Saldo? = Nothing
            Dim reader As NpgsqlDataReader = Nothing
            Try
                commandPoolItem = Await getClienteCommandPool.RentAsync(cancellationToken)
                Dim command = commandPoolItem.Value.Value
                Debug.Assert(command.Connection Is Nothing, "Command connection is nothing")
                command.Connection = connection
                command.Parameters(0).Value = idCliente
                reader = Await command.ExecuteReaderAsync(retryCount:=4, cancellationToken)
                Dim success = Await reader.ReadAsync(cancellationToken)
                If success Then
                    saldo = New Saldo(reader.GetInt32(0), Date.UtcNow, reader.GetInt32(1) * -1)
                End If
            Catch ex As Exception
                capturedException = ExceptionDispatchInfo.Capture(ex) ' vb does not support await in catch block 🤦‍️ ‍️
            End Try
            If reader IsNot Nothing Then
                Await reader.DisposeAsync()
            End If
            If commandPoolItem IsNot Nothing Then
                commandPoolItem.Value.Value.Connection = Nothing
                Await commandPoolItem.Value.DisposeAsync()
            End If
            capturedException?.Throw()
            Return saldo
        End Function

        Private Async Function GetTransacoesAsync(idCliente As Integer, connection As NpgsqlConnection, cancellationToken As CancellationToken) As Task(Of List(Of TransacaoComData))
            Dim commandPoolItem As PoolItem(Of NpgsqlCommand)? = Nothing
            Dim capturedException As ExceptionDispatchInfo = Nothing
            Dim transacoes = New List(Of TransacaoComData)()
            Dim reader As NpgsqlDataReader = Nothing
            Try
                commandPoolItem = Await getTransacoesCommandPool.RentAsync(cancellationToken)
                Dim command = commandPoolItem.Value.Value
                Debug.Assert(command.Connection Is Nothing, "Command connection is not nothing")
                command.Connection = connection
                command.Parameters(0).Value = idCliente
                reader = Await command.ExecuteReaderAsync(retryCount:=4, cancellationToken)
                While Await reader.ReadAsync(cancellationToken)
                    Dim valor = reader.GetInt32(0)
                    Dim transacao = New TransacaoComData(Math.Abs(valor), If(valor < 0, TipoTransacao.d, TipoTransacao.c), reader.GetString(1), Date.SpecifyKind(reader.GetDateTime(2), DateTimeKind.Utc))
                    transacoes.Add(transacao)
                End While
            Catch ex As Exception
                capturedException = ExceptionDispatchInfo.Capture(ex) ' vb does not support await in catch block 🤦‍️ ‍️
            End Try
            If reader IsNot Nothing Then
                Await reader.DisposeAsync()
            End If
            If commandPoolItem IsNot Nothing Then
                commandPoolItem.Value.Value.Connection = Nothing
                Await commandPoolItem.Value.DisposeAsync()
            End If
            capturedException?.Throw()
            Return transacoes
        End Function

        Public Sub Dispose() Implements IDisposable.Dispose
            If disposed Then Return
            disposed = True
            insertCommandPool?.Dispose()
            getClienteCommandPool?.Dispose()
            getTransacoesCommandPool?.Dispose()
        End Sub

        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Private Sub ThrowIfDisposed()
            ObjectDisposedException.ThrowIf(disposed, "Db")
        End Sub

    End Class

    Public NotInheritable Class DbConfig
        Public ReadOnly Property PoolSize As Integer
            Get
                Dim flag As Boolean = ConnectionString = Nothing
                Dim num As Integer
                If flag Then
                    num = 0
                Else
                    Dim connBuilder As New NpgsqlConnectionStringBuilder(ConnectionString)
                    num = If((Not connBuilder.Pooling), 0, connBuilder.MaxPoolSize)
                End If
                Return num
            End Get
        End Property

        Public Property ConnectionString As String
    End Class
    Public Enum AddError
        ClientNotFound
        LimitExceeded
    End Enum

    Public Module ADOExtensions
        <Extension()>
        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Public Async Function ExecuteReaderAsync(command As NpgsqlCommand, retryCount As Byte, cancellationToken As CancellationToken) As Task(Of NpgsqlDataReader)
            Dim reader As NpgsqlDataReader = Nothing
            While True
                Try
                    reader = Await command.ExecuteReaderAsync(cancellationToken)
                    Return reader
                Catch ex As NpgsqlException When retryCount < 4 AndAlso TypeOf ex.InnerException Is TimeoutException
                End Try
                Debug.Assert(reader Is Nothing)
                retryCount += CByte(1)
            End While
            Throw New Exception("Just so the compiler is happy")
        End Function
    End Module

End Namespace

