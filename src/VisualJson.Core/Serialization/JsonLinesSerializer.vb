' SPDX-License-Identifier: MPL-2.0
Imports System.Text
Imports System.Text.Json
Imports VisualJson.Core.Models

Namespace Serialization
    Public Class JsonLinesSerializationResult
        Public Sub New(text As String, warnings As IReadOnlyList(Of String))
            Me.Text = If(text, "")
            Me.Warnings = If(warnings, Array.Empty(Of String)())
        End Sub

        Public ReadOnly Property Text As String
        Public ReadOnly Property Warnings As IReadOnlyList(Of String)
    End Class

    ''' FR-P2-602: writes an array-rooted document as one compact JSON per line.
    ''' Blank lines and per-line formatting of the original file are not preserved
    ''' (spec 06 §6/§8).
    Public Class JsonLinesSerializer
        Private ReadOnly _treeSerializer As New JsonTreeSerializer()

        Public Function Serialize(root As JsonTreeNode, newLine As String) As JsonLinesSerializationResult
            If root Is Nothing Then
                Throw New ArgumentNullException(NameOf(root))
            End If

            Dim separator = If(String.IsNullOrEmpty(newLine), vbLf, newLine)
            Dim warnings = New List(Of String)()
            Dim standardJson = _treeSerializer.Serialize(root)

            Using document = JsonDocument.Parse(standardJson)
                Dim builder = New StringBuilder()
                If document.RootElement.ValueKind = JsonValueKind.Array Then
                    For Each element In document.RootElement.EnumerateArray()
                        builder.Append(WriteCompact(element))
                        builder.Append(separator)
                    Next
                Else
                    warnings.Add("The document root is not an array; it was saved as a single JSON line.")
                    builder.Append(WriteCompact(document.RootElement))
                    builder.Append(separator)
                End If

                Return New JsonLinesSerializationResult(builder.ToString(), warnings)
            End Using
        End Function

        Private Shared Function WriteCompact(element As JsonElement) As String
            Return JsonSerializer.Serialize(element, New JsonSerializerOptions With {.WriteIndented = False})
        End Function
    End Class
End Namespace
