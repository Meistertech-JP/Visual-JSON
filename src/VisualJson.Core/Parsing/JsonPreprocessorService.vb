' SPDX-License-Identifier: MPL-2.0
Imports System.Text

Namespace Parsing
    Public Class JsonPreprocessorService
        Public Function Normalize(text As String, format As JsonInputFormat) As String
            Dim source = If(text, "")

            Select Case format
                Case JsonInputFormat.JsonLines
                    Return NormalizeJsonLines(source)
                Case JsonInputFormat.JsonC
                    Return RemoveTrailingCommas(RemoveComments(source))
                Case JsonInputFormat.Json5
                    Return NormalizeJson5(source)
                Case Else
                    Return source
            End Select
        End Function

        Public Function DetectFromPath(path As String) As JsonInputFormat
            Dim extension = If(IO.Path.GetExtension(path), "").ToLowerInvariant()
            Select Case extension
                Case ".jsonc"
                    Return JsonInputFormat.JsonC
                Case ".json5"
                    Return JsonInputFormat.Json5
                Case ".jsonl", ".ndjson"
                    Return JsonInputFormat.JsonLines
                Case Else
                    Return JsonInputFormat.StandardJson
            End Select
        End Function

        Private Function NormalizeJsonLines(source As String) As String
            ' After a grid sync, a JSONL document's editor text is the pretty-printed
            ' array-form standard JSON. Valid multi-line JSONL never parses as a
            ' single JSON document, so a multi-line text that does is the array form
            ' and is passed through unchanged. Single-line texts keep the historical
            ' per-line wrapping (a lone "[1,2]" line stays one array element).
            If CountNonEmptyLines(source) > 1 AndAlso ParsesAsSingleJsonDocument(source) Then
                Return source
            End If

            Dim builder = New StringBuilder()
            builder.Append("[")
            Dim first = True

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

                    If Not first Then
                        builder.Append(","c)
                    End If

                    builder.Append(trimmed)
                    first = False
                End While
            End Using

            builder.Append("]")
            Return builder.ToString()
        End Function

        Private Shared Function CountNonEmptyLines(source As String) As Integer
            Dim count = 0
            Using reader = New IO.StringReader(source)
                While True
                    Dim line = reader.ReadLine()
                    If line Is Nothing Then
                        Exit While
                    End If

                    If line.Trim().Length > 0 Then
                        count += 1
                        If count > 1 Then
                            Return count
                        End If
                    End If
                End While
            End Using

            Return count
        End Function

        Private Shared Function ParsesAsSingleJsonDocument(source As String) As Boolean
            Try
                Using Text.Json.JsonDocument.Parse(source)
                End Using

                Return True
            Catch ex As Text.Json.JsonException
                Return False
            End Try
        End Function

        Private Function NormalizeJson5(source As String) As String
            Dim withoutComments = RemoveComments(source)
            Dim quotedStrings = ConvertSingleQuotedStrings(withoutComments)
            Dim quotedKeys = QuoteUnquotedObjectKeys(quotedStrings)
            Return RemoveTrailingCommas(quotedKeys)
        End Function

        Private Function RemoveComments(source As String) As String
            Dim builder = New StringBuilder(source.Length)
            Dim index = 0
            Dim inString = False
            Dim quote As Char = ControlChars.NullChar
            Dim escaped = False

            While index < source.Length
                Dim ch = source(index)

                If inString Then
                    builder.Append(ch)
                    If escaped Then
                        escaped = False
                    ElseIf ch = "\"c Then
                        escaped = True
                    ElseIf ch = quote Then
                        inString = False
                    End If

                    index += 1
                    Continue While
                End If

                If ch = """"c OrElse ch = "'"c Then
                    inString = True
                    quote = ch
                    builder.Append(ch)
                    index += 1
                    Continue While
                End If

                If ch = "/"c AndAlso index + 1 < source.Length Then
                    Dim nextCh = source(index + 1)
                    If nextCh = "/"c Then
                        index += 2
                        While index < source.Length AndAlso source(index) <> ControlChars.Lf
                            index += 1
                        End While
                        Continue While
                    End If

                    If nextCh = "*"c Then
                        index += 2
                        While index + 1 < source.Length AndAlso Not (source(index) = "*"c AndAlso source(index + 1) = "/"c)
                            If source(index) = ControlChars.Lf Then
                                builder.Append(ControlChars.Lf)
                            End If
                            index += 1
                        End While
                        index = Math.Min(source.Length, index + 2)
                        Continue While
                    End If
                End If

                builder.Append(ch)
                index += 1
            End While

            Return builder.ToString()
        End Function

        Private Function ConvertSingleQuotedStrings(source As String) As String
            Dim builder = New StringBuilder(source.Length)
            Dim index = 0
            Dim inDouble = False
            Dim escaped = False

            While index < source.Length
                Dim ch = source(index)

                If inDouble Then
                    builder.Append(ch)
                    If escaped Then
                        escaped = False
                    ElseIf ch = "\"c Then
                        escaped = True
                    ElseIf ch = """"c Then
                        inDouble = False
                    End If
                    index += 1
                    Continue While
                End If

                If ch = """"c Then
                    inDouble = True
                    builder.Append(ch)
                    index += 1
                    Continue While
                End If

                If ch = "'"c Then
                    builder.Append(""""c)
                    index += 1
                    escaped = False
                    While index < source.Length
                        Dim inner = source(index)
                        If escaped Then
                            Select Case inner
                                Case "'"c
                                    builder.Append("'"c)
                                Case """"c
                                    builder.Append("\""")
                                Case Else
                                    builder.Append("\"c)
                                    builder.Append(inner)
                            End Select
                            escaped = False
                        ElseIf inner = "\"c Then
                            escaped = True
                        ElseIf inner = "'"c Then
                            Exit While
                        ElseIf inner = """"c Then
                            builder.Append("\""")
                        Else
                            builder.Append(inner)
                        End If

                        index += 1
                    End While

                    builder.Append(""""c)
                    If index < source.Length AndAlso source(index) = "'"c Then
                        index += 1
                    End If
                    Continue While
                End If

                builder.Append(ch)
                index += 1
            End While

            Return builder.ToString()
        End Function

        Private Function QuoteUnquotedObjectKeys(source As String) As String
            Dim builder = New StringBuilder(source.Length)
            Dim index = 0
            Dim inString = False
            Dim escaped = False

            While index < source.Length
                Dim ch = source(index)

                If inString Then
                    builder.Append(ch)
                    If escaped Then
                        escaped = False
                    ElseIf ch = "\"c Then
                        escaped = True
                    ElseIf ch = """"c Then
                        inString = False
                    End If
                    index += 1
                    Continue While
                End If

                If ch = """"c Then
                    inString = True
                    builder.Append(ch)
                    index += 1
                    Continue While
                End If

                If ch = "{"c OrElse ch = ","c Then
                    builder.Append(ch)
                    index += 1

                    While index < source.Length AndAlso Char.IsWhiteSpace(source(index))
                        builder.Append(source(index))
                        index += 1
                    End While

                    If index < source.Length AndAlso IsIdentifierStart(source(index)) Then
                        Dim start = index
                        index += 1
                        While index < source.Length AndAlso IsIdentifierPart(source(index))
                            index += 1
                        End While

                        Dim probe = index
                        While probe < source.Length AndAlso Char.IsWhiteSpace(source(probe))
                            probe += 1
                        End While

                        If probe < source.Length AndAlso source(probe) = ":"c Then
                            builder.Append(""""c)
                            builder.Append(source.Substring(start, index - start))
                            builder.Append(""""c)
                            Continue While
                        End If

                        builder.Append(source.Substring(start, index - start))
                        Continue While
                    End If

                    Continue While
                End If

                builder.Append(ch)
                index += 1
            End While

            Return builder.ToString()
        End Function

        Private Function RemoveTrailingCommas(source As String) As String
            Dim builder = New StringBuilder(source.Length)
            Dim index = 0
            Dim inString = False
            Dim escaped = False

            While index < source.Length
                Dim ch = source(index)
                If inString Then
                    builder.Append(ch)
                    If escaped Then
                        escaped = False
                    ElseIf ch = "\"c Then
                        escaped = True
                    ElseIf ch = """"c Then
                        inString = False
                    End If
                    index += 1
                    Continue While
                End If

                If ch = """"c Then
                    inString = True
                    builder.Append(ch)
                    index += 1
                    Continue While
                End If

                If ch = ","c Then
                    Dim probe = index + 1
                    While probe < source.Length AndAlso Char.IsWhiteSpace(source(probe))
                        probe += 1
                    End While

                    If probe < source.Length AndAlso (source(probe) = "}"c OrElse source(probe) = "]"c) Then
                        index += 1
                        Continue While
                    End If
                End If

                builder.Append(ch)
                index += 1
            End While

            Return builder.ToString()
        End Function

        Private Shared Function IsIdentifierStart(ch As Char) As Boolean
            Return Char.IsLetter(ch) OrElse ch = "_"c OrElse ch = "$"c
        End Function

        Private Shared Function IsIdentifierPart(ch As Char) As Boolean
            Return Char.IsLetterOrDigit(ch) OrElse ch = "_"c OrElse ch = "$"c
        End Function
    End Class
End Namespace
