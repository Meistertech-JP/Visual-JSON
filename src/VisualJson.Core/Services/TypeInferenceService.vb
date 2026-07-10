' SPDX-License-Identifier: MPL-2.0
Imports System.Text.Json
Imports VisualJson.Core.Models

Namespace Services
    Public Class TypeInferenceService
        Public Function NormalizePrimitiveInput(input As String) As InferredJsonValue
            Dim original = If(input, "")
            Dim trimmed = original.Trim()

            ' FR-P2-306: exactly {} / [] turn the cell into an empty container.
            If String.Equals(trimmed, "{}", StringComparison.Ordinal) Then
                Return New InferredJsonValue(JsonNodeKind.ObjectValue, "")
            End If

            If String.Equals(trimmed, "[]", StringComparison.Ordinal) Then
                Return New InferredJsonValue(JsonNodeKind.ArrayValue, "")
            End If

            If String.Equals(trimmed, "null", StringComparison.OrdinalIgnoreCase) Then
                Return New InferredJsonValue(JsonNodeKind.NullValue, "null")
            End If

            If String.Equals(trimmed, "true", StringComparison.OrdinalIgnoreCase) Then
                Return New InferredJsonValue(JsonNodeKind.BooleanValue, "true")
            End If

            If String.Equals(trimmed, "false", StringComparison.OrdinalIgnoreCase) Then
                Return New InferredJsonValue(JsonNodeKind.BooleanValue, "false")
            End If

            If IsJsonNumber(trimmed) Then
                Return New InferredJsonValue(JsonNodeKind.NumberValue, trimmed)
            End If

            Dim parsedString As String = Nothing
            If TryParseQuotedJsonString(trimmed, parsedString) Then
                Return New InferredJsonValue(JsonNodeKind.StringValue, parsedString)
            End If

            Return New InferredJsonValue(JsonNodeKind.StringValue, original)
        End Function

        Public Sub ApplyToNode(node As JsonTreeNode)
            If node Is Nothing OrElse Not node.CanEditValue Then
                Return
            End If

            Dim inferred = NormalizePrimitiveInput(node.ValueText)
            node.Kind = inferred.Kind
            node.ValueText = inferred.ValueText
        End Sub

        Private Shared Function IsJsonNumber(value As String) As Boolean
            If String.IsNullOrWhiteSpace(value) Then
                Return False
            End If

            Try
                Using document = JsonDocument.Parse(value)
                    Return document.RootElement.ValueKind = JsonValueKind.Number
                End Using
            Catch ex As JsonException
                ' IgnoreWithReason: this is a predicate; not-parsable means the input is not this type.
                Return False
            End Try
        End Function

        Private Shared Function TryParseQuotedJsonString(value As String, ByRef parsed As String) As Boolean
            parsed = Nothing
            If value.Length < 2 OrElse Not value.StartsWith("""", StringComparison.Ordinal) OrElse Not value.EndsWith("""", StringComparison.Ordinal) Then
                Return False
            End If

            Try
                Using document = JsonDocument.Parse(value)
                    If document.RootElement.ValueKind <> JsonValueKind.String Then
                        Return False
                    End If

                    parsed = If(document.RootElement.GetString(), "")
                    Return True
                End Using
            Catch ex As JsonException
                ' IgnoreWithReason: this is a predicate; not-parsable means the input is not this type.
                Return False
            End Try
        End Function
    End Class
End Namespace
