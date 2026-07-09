' SPDX-License-Identifier: MPL-2.0
Namespace Services
    Public NotInheritable Class SearchMatch
        Public Sub New(startIndex As Integer, length As Integer, value As String)
            Me.StartIndex = Math.Max(0, startIndex)
            Me.Length = Math.Max(0, length)
            Me.Value = If(value, "")
        End Sub

        Public ReadOnly Property StartIndex As Integer
        Public ReadOnly Property Length As Integer
        Public ReadOnly Property Value As String

        Public ReadOnly Property EndIndex As Integer
            Get
                Return StartIndex + Length
            End Get
        End Property
    End Class
End Namespace
