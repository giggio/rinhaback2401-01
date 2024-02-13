Imports System.Threading
Imports System.Threading.Channels
Imports Microsoft.Extensions.Logging
Imports System.Diagnostics
Imports System.Runtime.ExceptionServices

Namespace Model
    Public NotInheritable Class Pool(Of T As {Class, IDisposable})
        Implements IDisposable

        Private ReadOnly poolSize As Integer
        Private ReadOnly typeName As String
        Private ReadOnly logger As ILogger(Of Pool(Of T))
        Private _waitingRenters As Integer
        Private ReadOnly queue As Channel(Of T) = Channel.CreateUnbounded(Of T)(New UnboundedChannelOptions With {
            .AllowSynchronousContinuations = True,
            .SingleReader = False,
            .SingleWriter = False
        })

#If EXTRAOPTIMIZE Then
        Public Sub New(ByVal items As ICollection(Of T))
#Else
        Public Sub New(ByVal items As ICollection(Of T), ByVal logger As ILogger(Of Pool(Of T)))
            Me.logger = logger
#End If
            poolSize = items.Count
            Debug.Assert(poolSize > 0)

            For Each item In items
                If Not queue.Writer.TryWrite(item) Then Throw New ApplicationException("Failed to enqueue starting item on Pool.")
            Next

            typeName = GetType(T).Name
#If Not EXTRAOPTIMIZE Then
            logger.PoolCreated(typeName, poolSize)
#End If
        End Sub

        Public Async Function RentAsync(ByVal cancellationToken As CancellationToken) As Task(Of PoolItem(Of T))
            Dim item As T = Nothing
            Interlocked.Increment(_waitingRenters)

            Dim capturedException As ExceptionDispatchInfo = Nothing

            Dim poolItem As PoolItem(Of T) = Nothing
            Try
#If Not EXTRAOPTIMIZE Then
                logger.PoolRentingItem(typeName, queue.Reader.Count)
#End If
                item = Await queue.Reader.ReadAsync(cancellationToken)
                poolItem = New PoolItem(Of T)(item, AddressOf ReturnPoolItemAsync)
            Catch ex As Exception
                capturedException = ExceptionDispatchInfo.Capture(ex) ' vb does not support await in catch block 🤦‍️ ‍️
            Finally
                Interlocked.Decrement(_waitingRenters)
            End Try
            If capturedException IsNot Nothing Then
                If item IsNot Nothing Then Await queue.Writer.WriteAsync(item, cancellationToken).AsTask()
                capturedException.Throw()
            End If
            Return poolItem
        End Function

        Public Async Function ReturnAllAsync(ByVal cancellationToken As CancellationToken) As Task(Of List(Of T))
#If Not EXTRAOPTIMIZE Then
            logger.PoolReturningAllItems(typeName, queue.Reader.Count)
#End If
            Dim items = New List(Of T)()

            Dim asyncEnumerator = queue.Reader.ReadAllAsync(cancellationToken).GetAsyncEnumerator(cancellationToken)
            While Await asyncEnumerator.MoveNextAsync()
                items.Add(asyncEnumerator.Current)
            End While

            Return items
        End Function

        Private Async Function ReturnPoolItemAsync(ByVal poolItem As PoolItem(Of T)) As Task
            Await queue.Writer.WriteAsync(poolItem.Value)
#If Not EXTRAOPTIMIZE Then
            logger.PoolReturnedItem(typeName, queue.Reader.Count)
#End If
        End Function

        Public Sub Dispose() Implements IDisposable.Dispose
            Dim items = ReturnAllAsync(CancellationToken.None).Result
            For Each item In items
                item.Dispose()
            Next
        End Sub

        Public ReadOnly Property QuantityAvailable As Integer
            Get
                Return queue.Reader.Count
            End Get
        End Property

        Public ReadOnly Property QuantityRented As Integer
            Get
                Return poolSize - queue.Reader.Count
            End Get
        End Property

        Public ReadOnly Property WaitingRenters As Integer
            Get
                Return _waitingRenters
            End Get
        End Property
    End Class

    Public Structure PoolItem(Of TItem)
        Public Sub New(value As TItem, returnPoolItemAsync As Func(Of PoolItem(Of TItem), Task))
            Me.returnPoolItemAsync = returnPoolItemAsync
            Me.Value = value
        End Sub
        Private ReadOnly returnPoolItemAsync As Func(Of PoolItem(Of TItem), Task)

        Public ReadOnly Property Value As TItem

        Public Function DisposeAsync() As Task
            Return returnPoolItemAsync(Me)
        End Function

    End Structure

End Namespace


