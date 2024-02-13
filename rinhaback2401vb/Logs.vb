Imports Microsoft.Extensions.Logging
Imports RinhaBack2401.Model
Imports System.Runtime.CompilerServices

#If Not EXTRAOPTIMIZE Then
Partial Public Module Logs
    <Extension()>
    Public Sub AppError(logger As ILogger, url As String, exceptionMessage As String)
        logger.LogError("Got unhandled exception at url {url}:" & vbLf & "{exceptionMessage}.", url, exceptionMessage)
    End Sub

    <Extension()>
    Public Sub DbInserted(logger As ILogger, rowsCount As Integer, details As String)
        logger.LogTrace("Inserted {rowsCount} rows into database. Details:" & vbLf & "{details}", rowsCount, details)
    End Sub

    <Extension()>
    Public Sub PoolRentingItem(logger As ILogger, typeName As String, itemsCount As Integer)
        logger.LogTrace("Pool of {typeName} rented an item, has {itemsCount} before renting.", typeName, itemsCount)
    End Sub

    <Extension()>
    Public Sub PoolReturnedItem(logger As ILogger, typeName As String, itemsCount As Integer)
        logger.LogTrace("Pool of {typeName} returned an item, had {itemsCount} after return.", typeName, itemsCount)
    End Sub

    <Extension()>
    Public Sub PoolReturningAllItems(logger As ILogger, typeName As String, itemsCount As Integer)
        logger.LogTrace("Pool of {typeName} returning all items, had {itemsCount}.", typeName, itemsCount)
    End Sub

    <Extension()>
    Public Sub PoolCreated(logger As ILogger, typeName As String, itemsCount As Integer)
        logger.LogTrace("Pool of {typeName} created with {itemsCount}.", typeName, itemsCount)
    End Sub

    <Extension()>
    Public Sub DbInserted(logger As ILogger, idCliente As Integer, valor As Integer, tipo As TipoTransacao)
        logger.LogTrace("Inserted transacao rows into database for client {idCliente}, tipo: {tipo}, valor: {valor}", idCliente, tipo, valor)
    End Sub
End Module

Public NotInheritable Class AppLogs
    End Class
#End If

