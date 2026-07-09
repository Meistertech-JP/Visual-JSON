' SPDX-License-Identifier: MPL-2.0
Namespace Services
    Public NotInheritable Class GridViewState
        Public Sub New(selectedPointer As String,
                       expandedPointers As IEnumerable(Of String),
                       anchorPointer As String,
                       textCaretOffset As Integer,
                       textScrollOffset As Double)
            Me.SelectedPointer = If(selectedPointer, "")
            Me.ExpandedPointers = New HashSet(Of String)(If(expandedPointers, Array.Empty(Of String)()), StringComparer.Ordinal)
            Me.AnchorPointer = If(anchorPointer, "")
            Me.TextCaretOffset = Math.Max(0, textCaretOffset)
            Me.TextScrollOffset = Math.Max(0, textScrollOffset)
        End Sub

        Public ReadOnly Property SelectedPointer As String
        Public ReadOnly Property ExpandedPointers As HashSet(Of String)
        Public ReadOnly Property AnchorPointer As String
        Public ReadOnly Property TextCaretOffset As Integer
        Public ReadOnly Property TextScrollOffset As Double
    End Class
End Namespace
