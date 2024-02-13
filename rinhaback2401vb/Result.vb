Public MustInherit Class Result(Of T, TError)
End Class

Public Class Ok(Of T, TError)
        Inherits Result(Of T, TError)
        Public Sub New(value As T)
            Me.Value = value
        End Sub
        Public ReadOnly Property Value As T
    End Class

    Public Class [Error](Of T, TError)
        Inherits Result(Of T, TError)
        Public Sub New(anError As TError)
            [Error] = anError
        End Sub
        Public ReadOnly Property [Error] As TError
    End Class
