' SPDX-License-Identifier: MPL-2.0
Imports System.Text.Json

Namespace Serialization
    Public Class JsonFormatterService
        Private ReadOnly _preprocessor As New Parsing.JsonPreprocessorService()

        Private Shared ReadOnly StrictOptions As New JsonDocumentOptions With {
            .AllowTrailingCommas = False,
            .CommentHandling = JsonCommentHandling.Disallow
        }

        Private Shared ReadOnly IndentedOptions As New JsonSerializerOptions With {
            .WriteIndented = True
        }

        Public Function Format(text As String, Optional inputFormat As Parsing.JsonInputFormat = Parsing.JsonInputFormat.StandardJson) As String
            Using document = JsonDocument.Parse(_preprocessor.Normalize(If(text, ""), inputFormat), StrictOptions)
                Return JsonSerializer.Serialize(document.RootElement, IndentedOptions)
            End Using
        End Function
    End Class
End Namespace
