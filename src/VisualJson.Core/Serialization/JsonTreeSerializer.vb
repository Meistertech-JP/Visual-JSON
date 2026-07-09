' SPDX-License-Identifier: MPL-2.0
Imports System.IO
Imports System.Text
Imports System.Text.Json
Imports VisualJson.Core.Models

Namespace Serialization
    Public Class JsonTreeSerializer
        Public Function Serialize(root As JsonTreeNode) As String
            If root Is Nothing Then
                Return "{}"
            End If

            Dim options = New JsonWriterOptions With {.Indented = True}
            Using stream = New MemoryStream()
                Using writer = New Utf8JsonWriter(stream, options)
                    WriteNode(writer, root)
                End Using

                Return Encoding.UTF8.GetString(stream.ToArray())
            End Using
        End Function

        Private Sub WriteNode(writer As Utf8JsonWriter, node As JsonTreeNode)
            Select Case node.Kind
                Case JsonNodeKind.ObjectValue
                    writer.WriteStartObject()
                    For Each child In node.Children
                        writer.WritePropertyName(child.Key)
                        WriteNode(writer, child)
                    Next
                    writer.WriteEndObject()

                Case JsonNodeKind.ArrayValue
                    writer.WriteStartArray()
                    For Each child In node.Children
                        WriteNode(writer, child)
                    Next
                    writer.WriteEndArray()

                Case JsonNodeKind.StringValue
                    writer.WriteStringValue(If(node.ValueText, ""))

                Case JsonNodeKind.NumberValue
                    Dim rawValue = If(String.IsNullOrWhiteSpace(node.ValueText), "0", node.ValueText.Trim())
                    writer.WriteRawValue(rawValue, skipInputValidation:=False)

                Case JsonNodeKind.BooleanValue
                    writer.WriteBooleanValue(String.Equals(node.ValueText, "true", StringComparison.OrdinalIgnoreCase))

                Case JsonNodeKind.NullValue
                    writer.WriteNullValue()
            End Select
        End Sub
    End Class
End Namespace
