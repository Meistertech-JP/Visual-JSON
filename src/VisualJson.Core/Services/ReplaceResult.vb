' SPDX-License-Identifier: MPL-2.0
Namespace Services
    Public NotInheritable Class ReplaceResult
        Public Sub New(text As String, count As Integer, nextOffset As Integer)
            Me.Text = If(text, "")
            Me.Count = Math.Max(0, count)
            Me.NextOffset = Math.Max(0, nextOffset)
        End Sub

        Public ReadOnly Property Text As String
        Public ReadOnly Property Count As Integer
        Public ReadOnly Property NextOffset As Integer
    End Class
End Namespace
