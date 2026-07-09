' SPDX-License-Identifier: MPL-2.0
Imports VisualJson.Core.Models

Namespace Services
    Public Class InferredJsonValue
        Public Sub New(kind As JsonNodeKind, valueText As String)
            Me.Kind = kind
            Me.ValueText = valueText
        End Sub

        Public ReadOnly Property Kind As JsonNodeKind
        Public ReadOnly Property ValueText As String
    End Class
End Namespace
