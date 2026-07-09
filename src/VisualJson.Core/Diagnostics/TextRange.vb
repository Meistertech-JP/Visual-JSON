' SPDX-License-Identifier: MPL-2.0
Namespace Diagnostics
    Public Class TextRange
        Public Sub New(startIndex As Integer, length As Integer)
            Me.StartIndex = Math.Max(0, startIndex)
            Me.Length = Math.Max(0, length)
        End Sub

        Public ReadOnly Property StartIndex As Integer
        Public ReadOnly Property Length As Integer

        Public ReadOnly Property EndIndex As Integer
            Get
                Return StartIndex + Length
            End Get
        End Property
    End Class
End Namespace
