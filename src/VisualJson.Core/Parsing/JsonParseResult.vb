' SPDX-License-Identifier: MPL-2.0
Imports VisualJson.Core.Diagnostics
Imports VisualJson.Core.Models

Namespace Parsing
    Public Class JsonParseResult
        Public Sub New(root As JsonTreeNode, diagnostics As IReadOnlyList(Of ValidationDiagnostic))
            Me.Root = root
            Me.Diagnostics = diagnostics
        End Sub

        Public ReadOnly Property Root As JsonTreeNode
        Public ReadOnly Property Diagnostics As IReadOnlyList(Of ValidationDiagnostic)
    End Class
End Namespace
