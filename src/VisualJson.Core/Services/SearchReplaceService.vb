' SPDX-License-Identifier: MPL-2.0
Imports System.Text
Imports System.Text.RegularExpressions

Namespace Services
    Public Class SearchReplaceService
        Public Function FindMatches(text As String,
                                    pattern As String,
                                    options As SearchOptions,
                                    Optional maxMatches As Integer = Integer.MaxValue) As IReadOnlyList(Of SearchMatch)
            Dim source = If(text, "")
            Dim query = If(pattern, "")
            If query.Length = 0 OrElse maxMatches <= 0 Then
                Return Array.Empty(Of SearchMatch)()
            End If

            If options IsNot Nothing AndAlso options.UseRegex Then
                Return FindRegexMatches(source, query, options, maxMatches)
            End If

            Return FindLiteralMatches(source, query, options, maxMatches)
        End Function

        Public Function ReplaceAll(text As String,
                                   pattern As String,
                                   replacement As String,
                                   options As SearchOptions) As ReplaceResult
            Dim source = If(text, "")
            Dim query = If(pattern, "")
            If query.Length = 0 Then
                Return New ReplaceResult(source, 0, 0)
            End If

            If options IsNot Nothing AndAlso options.UseRegex Then
                Dim matches = FindRegexMatches(source, query, options, Integer.MaxValue)
                If matches.Any(Function(item) item.Length = 0) Then
                    Throw New InvalidOperationException("Zero-length regex matches are not supported.")
                End If

                Dim count = 0
                Dim regex = CreateRegex(query, options)
                Dim replaced = regex.Replace(source,
                                             Function(match)
                                                 count += 1
                                                 Return match.Result(If(replacement, ""))
                                             End Function)
                Return New ReplaceResult(replaced, count, 0)
            End If

            Dim literalMatches = FindLiteralMatches(source, query, options, Integer.MaxValue)
            If literalMatches.Count = 0 Then
                Return New ReplaceResult(source, 0, 0)
            End If

            Dim builder = New StringBuilder(source.Length)
            Dim current = 0
            For Each match In literalMatches
                builder.Append(source, current, match.StartIndex - current)
                builder.Append(If(replacement, ""))
                current = match.EndIndex
            Next

            builder.Append(source, current, source.Length - current)
            Return New ReplaceResult(builder.ToString(), literalMatches.Count, 0)
        End Function

        Public Function ReplaceSelection(text As String,
                                         pattern As String,
                                         replacement As String,
                                         selectionStart As Integer,
                                         selectionLength As Integer,
                                         options As SearchOptions) As ReplaceResult
            Dim source = If(text, "")
            Dim safeStart = Math.Min(Math.Max(0, selectionStart), source.Length)
            Dim safeLength = Math.Min(Math.Max(0, selectionLength), source.Length - safeStart)
            If safeLength = 0 Then
                Return New ReplaceResult(source, 0, safeStart)
            End If

            Dim match = FindMatches(source, pattern, options).FirstOrDefault(
                Function(item) item.StartIndex = safeStart AndAlso item.Length = safeLength)
            If match Is Nothing Then
                Return New ReplaceResult(source, 0, safeStart)
            End If

            Dim replacementText As String
            If options IsNot Nothing AndAlso options.UseRegex Then
                Dim regex = CreateRegex(If(pattern, ""), options)
                replacementText = regex.Replace(match.Value,
                                                Function(regexMatch) regexMatch.Result(If(replacement, "")),
                                                1)
            Else
                replacementText = If(replacement, "")
            End If

            Dim nextText = source.Substring(0, match.StartIndex) & replacementText & source.Substring(match.EndIndex)
            Return New ReplaceResult(nextText, 1, match.StartIndex + replacementText.Length)
        End Function

        Private Shared Function FindLiteralMatches(text As String,
                                                   pattern As String,
                                                   options As SearchOptions,
                                                   maxMatches As Integer) As IReadOnlyList(Of SearchMatch)
            Dim comparison = If(options IsNot Nothing AndAlso options.MatchCase, StringComparison.Ordinal, StringComparison.OrdinalIgnoreCase)
            Dim matches = New List(Of SearchMatch)()
            Dim index = 0

            While index <= text.Length - pattern.Length
                Dim found = text.IndexOf(pattern, index, comparison)
                If found < 0 Then
                    Exit While
                End If

                matches.Add(New SearchMatch(found, pattern.Length, text.Substring(found, pattern.Length)))
                If matches.Count >= maxMatches Then
                    Exit While
                End If

                index = found + Math.Max(1, pattern.Length)
            End While

            Return matches
        End Function

        Private Shared Function FindRegexMatches(text As String,
                                                 pattern As String,
                                                 options As SearchOptions,
                                                 maxMatches As Integer) As IReadOnlyList(Of SearchMatch)
            Dim regex = CreateRegex(pattern, options)
            Dim matches = New List(Of SearchMatch)()
            For Each item As Match In regex.Matches(text)
                matches.Add(New SearchMatch(item.Index, item.Length, item.Value))
                If matches.Count >= maxMatches Then
                    Exit For
                End If
            Next

            Return matches
        End Function

        Private Shared Function CreateRegex(pattern As String, options As SearchOptions) As Regex
            Dim flags = RegexOptions.CultureInvariant
            If options Is Nothing OrElse Not options.MatchCase Then
                flags = flags Or RegexOptions.IgnoreCase
            End If

            Return New Regex(If(pattern, ""), flags, TimeSpan.FromSeconds(2))
        End Function
    End Class
End Namespace
