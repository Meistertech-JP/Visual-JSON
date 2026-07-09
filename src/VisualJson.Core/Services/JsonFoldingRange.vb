' SPDX-License-Identifier: MPL-2.0
Namespace Services
    Public NotInheritable Class JsonFoldingRange
        Public Sub New(startIndex As Integer, endIndex As Integer, startLine As Integer, endLine As Integer)
            Me.StartIndex = Math.Max(0, startIndex)
            Me.EndIndex = Math.Max(Me.StartIndex, endIndex)
            Me.StartLine = Math.Max(1, startLine)
            Me.EndLine = Math.Max(Me.StartLine, endLine)
        End Sub

        Public ReadOnly Property StartIndex As Integer
        Public ReadOnly Property EndIndex As Integer
        Public ReadOnly Property StartLine As Integer
        Public ReadOnly Property EndLine As Integer
    End Class
End Namespace
