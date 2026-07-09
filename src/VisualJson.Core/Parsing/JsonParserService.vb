' SPDX-License-Identifier: MPL-2.0
Imports System.Text.Json
Imports VisualJson.Core.Diagnostics
Imports VisualJson.Core.Models

Namespace Parsing
    Public Class JsonParserService
        Private ReadOnly _preprocessor As New JsonPreprocessorService()

        Private Shared ReadOnly StrictOptions As New JsonDocumentOptions With {
            .AllowTrailingCommas = False,
            .CommentHandling = JsonCommentHandling.Disallow
        }

        Public Function Parse(text As String, Optional format As JsonInputFormat = JsonInputFormat.StandardJson) As JsonParseResult
            Dim originalText = If(text, "")
            Dim sourceText = _preprocessor.Normalize(originalText, format)
            Using document = JsonDocument.Parse(sourceText, StrictOptions)
                Dim root = BuildNode("$", "$", "", document.RootElement)
                JsonSourcePositionMapper.Map(sourceText, root)
                Return New JsonParseResult(root, Array.Empty(Of ValidationDiagnostic)())
            End Using
        End Function

        Private Function BuildNode(key As String, displayPath As String, jsonPointer As String, element As JsonElement) As JsonTreeNode
            Select Case element.ValueKind
                Case JsonValueKind.Object
                    Dim node = New JsonTreeNode(key, displayPath, JsonNodeKind.ObjectValue, "", jsonPointer)
                    For Each item In element.EnumerateObject()
                        Dim childPointer = $"{jsonPointer}/{JsonTreeNode.EscapePointerSegment(item.Name)}"
                        node.Children.Add(BuildNode(item.Name, AppendPropertyPath(displayPath, item.Name), childPointer, item.Value))
                    Next
                    Return node

                Case JsonValueKind.Array
                    Dim node = New JsonTreeNode(key, displayPath, JsonNodeKind.ArrayValue, "", jsonPointer)
                    Dim index = 0
                    For Each item In element.EnumerateArray()
                        node.Children.Add(BuildNode($"[{index}]", $"{displayPath}[{index}]", $"{jsonPointer}/{index}", item))
                        index += 1
                    Next
                    Return node

                Case JsonValueKind.String
                    Return New JsonTreeNode(key, displayPath, JsonNodeKind.StringValue, If(element.GetString(), ""), jsonPointer)

                Case JsonValueKind.Number
                    Return New JsonTreeNode(key, displayPath, JsonNodeKind.NumberValue, element.GetRawText(), jsonPointer)

                Case JsonValueKind.True
                    Return New JsonTreeNode(key, displayPath, JsonNodeKind.BooleanValue, "true", jsonPointer)

                Case JsonValueKind.False
                    Return New JsonTreeNode(key, displayPath, JsonNodeKind.BooleanValue, "false", jsonPointer)

                Case JsonValueKind.Null
                    Return New JsonTreeNode(key, displayPath, JsonNodeKind.NullValue, "null", jsonPointer)

                Case Else
                    Return New JsonTreeNode(key, displayPath, JsonNodeKind.NullValue, "null", jsonPointer)
            End Select
        End Function

        Private Shared Function AppendPropertyPath(parentPath As String, propertyName As String) As String
            If IsSimplePropertyName(propertyName) Then
                Return $"{parentPath}.{propertyName}"
            End If

            Dim escaped = propertyName.Replace("\", "\\").Replace("'", "\'")
            Return $"{parentPath}['{escaped}']"
        End Function

        Private Shared Function IsSimplePropertyName(value As String) As Boolean
            If String.IsNullOrEmpty(value) Then
                Return False
            End If

            If Not (Char.IsLetter(value(0)) OrElse value(0) = "_"c) Then
                Return False
            End If

            For Each ch In value
                If Not (Char.IsLetterOrDigit(ch) OrElse ch = "_"c) Then
                    Return False
                End If
            Next

            Return True
        End Function
    End Class
End Namespace
