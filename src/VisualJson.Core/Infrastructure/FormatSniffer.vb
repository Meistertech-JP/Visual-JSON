' SPDX-License-Identifier: MPL-2.0
Imports System.Text.RegularExpressions
Imports VisualJson.Core.Parsing

Namespace Infrastructure
    Public Class FormatSniffer
        Private Shared ReadOnly UnquotedKeyPattern As New Regex("[\{,]\s*[A-Za-z_$][A-Za-z0-9_$]*\s*:", RegexOptions.Compiled)

        Public Function Sniff(text As String) As FormatSniffResult
            Dim source = If(text, "")
            Dim trimmed = source.TrimStart()

            If trimmed.Length = 0 Then
                Return New FormatSniffResult(JsonInputFormat.StandardJson, False, "empty")
            End If

            If trimmed.StartsWith("//", StringComparison.Ordinal) OrElse trimmed.StartsWith("/*", StringComparison.Ordinal) OrElse ContainsLineComment(source) Then
                Return New FormatSniffResult(JsonInputFormat.JsonC, True, "comment")
            End If

            If LooksLikeJsonLines(source) Then
                Return New FormatSniffResult(JsonInputFormat.JsonLines, True, "line-delimited JSON values")
            End If

            If source.Contains("'"c) OrElse UnquotedKeyPattern.IsMatch(source) Then
                Return New FormatSniffResult(JsonInputFormat.Json5, True, "JSON5 syntax")
            End If

            If trimmed(0) = "{"c OrElse trimmed(0) = "["c Then
                Return New FormatSniffResult(JsonInputFormat.StandardJson, True, "object or array root")
            End If

            Return New FormatSniffResult(JsonInputFormat.StandardJson, False, "unknown")
        End Function

        Private Shared Function ContainsLineComment(source As String) As Boolean
            Using reader = New IO.StringReader(source)
                While True
                    Dim line = reader.ReadLine()
                    If line Is Nothing Then
                        Exit While
                    End If

                    If line.TrimStart().StartsWith("//", StringComparison.Ordinal) Then
                        Return True
                    End If
                End While
            End Using

            Return False
        End Function

        Private Shared Function LooksLikeJsonLines(source As String) As Boolean
            Dim count = 0
            Using reader = New IO.StringReader(source)
                While True
                    Dim line = reader.ReadLine()
                    If line Is Nothing Then
                        Exit While
                    End If

                    Dim trimmed = line.Trim()
                    If trimmed.Length = 0 Then
                        Continue While
                    End If

                    If Not (trimmed.StartsWith("{", StringComparison.Ordinal) OrElse trimmed.StartsWith("[", StringComparison.Ordinal) OrElse trimmed.StartsWith("""", StringComparison.Ordinal) OrElse Char.IsDigit(trimmed(0)) OrElse trimmed(0) = "-"c) Then
                        Return False
                    End If

                    count += 1
                End While
            End Using

            Return count > 1
        End Function
    End Class
End Namespace
