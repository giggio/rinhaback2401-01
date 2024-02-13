Imports System.Text.Json.Serialization
Imports System
Imports System.Collections.Generic
Imports System.Runtime.CompilerServices
Imports System.Text
Namespace Model

    <JsonConverter(GetType(JsonStringEnumConverter(Of TipoTransacao)))>
    Public Enum TipoTransacao
        Incorrect
        c
        d
    End Enum



    Public Structure Extrato
        Public Sub New(Saldo As Saldo, UltimasTransacoes As List(Of TransacaoComData))
            Me.Saldo = Saldo
            Me.UltimasTransacoes = UltimasTransacoes
        End Sub

        Public Property Saldo As Saldo

        Public Property UltimasTransacoes As List(Of TransacaoComData)
    End Structure

    Public Structure Saldo
        Public Sub New(Total As Integer, DataExtrato As DateTime, Limite As Integer)
            Me.Total = Total
            Me.DataExtrato = DataExtrato
            Me.Limite = Limite
        End Sub
        Public Property Total As Integer
        Public Property DataExtrato As DateTime
        Public Property Limite As Integer
    End Structure

    Public Structure Transacao
        Public Sub New(Valor As Integer, Tipo As TipoTransacao, Descricao As String)
            Me.Valor = Valor
            Me.Tipo = Tipo
            Me.Descricao = Descricao
        End Sub
        Public Property Valor As Integer
        Public Property Tipo As TipoTransacao
        Public Property Descricao As String
    End Structure

    Public Structure TransacaoComData
        Public Sub New(Valor As Integer, Tipo As TipoTransacao, Descricao As String, RealizadaEm As DateTime)
            Me.Valor = Valor
            Me.Tipo = Tipo
            Me.Descricao = Descricao
            Me.RealizadaEm = RealizadaEm
        End Sub
        Public Property Valor As Integer
        Public Property Tipo As TipoTransacao
        Public Property Descricao As String
        Public Property RealizadaEm As DateTime
    End Structure

    Public Structure TransacaoModel
        Public Sub New(Valor As Object, Tipo As String, Descricao As String)
            Me.Valor = Valor
            Me.Tipo = Tipo
            Me.Descricao = Descricao
        End Sub
        Public Property Valor As Object
        Public Property Tipo As String
        Public Property Descricao As String
    End Structure

    Public Class Transacoes

        Public Sub New(limite As Integer, saldo As Integer)
            Me.Limite = limite
            Me.Saldo = saldo
        End Sub

        Public Property Limite As Integer

        Public Property Saldo As Integer
    End Class
End Namespace
