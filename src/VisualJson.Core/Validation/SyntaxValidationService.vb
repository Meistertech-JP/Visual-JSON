' SPDX-License-Identifier: MPL-2.0
Imports System.Text.Json
Imports VisualJson.Core.Diagnostics

Namespace Validation
    Public Class SyntaxValidationService
        Private ReadOnly _preprocessor As New Parsing.JsonPreprocessorService()

        Private Shared ReadOnly StrictOptions As New JsonDocumentOptions With {
            .AllowTrailingCommas = False,
            .CommentHandling = JsonCommentHandling.Disallow
        }

        Public Function Validate(text As String, Optional format As Parsing.JsonInputFormat = Parsing.JsonInputFormat.StandardJson) As List(Of ValidationDiagnostic)
            Dim diagnostics = New List(Of ValidationDiagnostic)()
            If String.IsNullOrWhiteSpace(text) Then
                diagnostics.Add(New ValidationDiagnostic("Error", "JSON text is empty.", 1, 1, errorCode:="SYN-EMPTY"))
                Return diagnostics
            End If

            Try
                Using JsonDocument.Parse(_preprocessor.Normalize(text, format), StrictOptions)
                End Using
            Catch ex As JsonException
                diagnostics.Add(CreateJsonExceptionDiagnostic(ex))
            Catch ex As Exception
                diagnostics.Add(New ValidationDiagnostic("Error", ex.Message, errorCode:="SYN-ERROR"))
            End Try

            Return diagnostics
        End Function

        Private Shared Function CreateJsonExceptionDiagnostic(ex As JsonException) As ValidationDiagnostic
            Dim line As Integer? = Nothing
            Dim column As Integer? = Nothing

            If ex.LineNumber.HasValue Then
                line = CInt(ex.LineNumber.Value) + 1
            End If

            If ex.BytePositionInLine.HasValue Then
                column = CInt(ex.BytePositionInLine.Value) + 1
            End If

            Return New ValidationDiagnostic("Error", ex.Message, line, column, errorCode:="SYN-PARSE")
        End Function
    End Class
End Namespace
