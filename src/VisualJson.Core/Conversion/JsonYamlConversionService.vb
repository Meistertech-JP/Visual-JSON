' SPDX-License-Identifier: MPL-2.0
Imports System.Text
Imports System.Text.Json
Imports System.Text.Json.Nodes

Namespace Conversion
    ''' JSON to YAML and YAML to JSON conversion per spec 06 §6.
    ''' Output uses 2-space indentation and keeps Number/Boolean/Null as YAML built-in types.
    ''' YAML to JSON covers a representative block-style subset; anchors, aliases, tags,
    ''' block scalars, and non-empty flow collections are rejected with a clear error.
    Public Class JsonYamlConversionService
        Private Shared ReadOnly StrictOptions As New JsonDocumentOptions With {
            .AllowTrailingCommas = False,
            .CommentHandling = JsonCommentHandling.Disallow
        }

        Private Shared ReadOnly ReservedScalars As String() = {
            "true", "false", "null", "yes", "no", "on", "off", "~", ""
        }

        Public Function ConvertJsonToYaml(jsonText As String) As ConversionResult
            Dim warnings = New List(Of String)()

            Using document = JsonDocument.Parse(If(jsonText, ""), StrictOptions)
                Dim builder = New StringBuilder()
                WriteNode(builder, document.RootElement, 0, isSequenceItem:=False)

                If builder.Length = 0 Then
                    builder.AppendLine("{}")
                End If

                Return New ConversionResult(builder.ToString(), warnings)
            End Using
        End Function

        Public Function ConvertYamlToJson(yamlText As String) As ConversionResult
            Dim warnings = New List(Of String)()
            Dim lines = ParseLines(If(yamlText, ""))

            If lines.Count = 0 Then
                Throw New InvalidOperationException("The YAML document is empty.")
            End If

            Dim index = 0
            Dim node = ParseBlock(lines, index, lines(0).Indent)
            If index < lines.Count Then
                Throw New InvalidOperationException($"Unexpected content at line {lines(index).LineNumber}.")
            End If

            Dim output = If(node Is Nothing, "null", node.ToJsonString(New JsonSerializerOptions With {.WriteIndented = True}))
            Return New ConversionResult(output, warnings)
        End Function

        ' ---------- JSON -> YAML ----------

        Private Sub WriteNode(builder As StringBuilder, element As JsonElement, indent As Integer, isSequenceItem As Boolean)
            Select Case element.ValueKind
                Case JsonValueKind.Object
                    If Not element.EnumerateObject().Any() Then
                        builder.AppendLine("{}")
                        Return
                    End If

                    Dim first = True
                    For Each item In element.EnumerateObject()
                        If Not (first AndAlso isSequenceItem) Then
                            builder.Append(" "c, indent)
                        End If
                        first = False

                        builder.Append(FormatKey(item.Name))
                        AppendValue(builder, item.Value, indent)
                    Next

                Case JsonValueKind.Array
                    If element.GetArrayLength() = 0 Then
                        builder.AppendLine("[]")
                        Return
                    End If

                    For Each item In element.EnumerateArray()
                        builder.Append(" "c, indent)
                        If IsScalar(item) Then
                            builder.Append("- ")
                            builder.AppendLine(FormatScalar(item))
                        ElseIf IsEmptyContainer(item) Then
                            builder.Append("- ")
                            builder.AppendLine(If(item.ValueKind = JsonValueKind.Object, "{}", "[]"))
                        ElseIf item.ValueKind = JsonValueKind.Array Then
                            builder.AppendLine("-")
                            WriteNode(builder, item, indent + 2, isSequenceItem:=False)
                        Else
                            builder.Append("- ")
                            WriteNode(builder, item, indent + 2, isSequenceItem:=True)
                        End If
                    Next

                Case Else
                    builder.Append(" "c, indent)
                    builder.AppendLine(FormatScalar(element))
            End Select
        End Sub

        Private Sub AppendValue(builder As StringBuilder, value As JsonElement, indent As Integer)
            If IsScalar(value) Then
                builder.Append(": ")
                builder.AppendLine(FormatScalar(value))
            ElseIf IsEmptyContainer(value) Then
                builder.Append(": ")
                builder.AppendLine(If(value.ValueKind = JsonValueKind.Object, "{}", "[]"))
            Else
                builder.AppendLine(":")
                WriteNode(builder, value, indent + 2, isSequenceItem:=False)
            End If
        End Sub

        Private Shared Function IsScalar(element As JsonElement) As Boolean
            Return element.ValueKind <> JsonValueKind.Object AndAlso element.ValueKind <> JsonValueKind.Array
        End Function

        Private Shared Function IsEmptyContainer(element As JsonElement) As Boolean
            If element.ValueKind = JsonValueKind.Object Then
                Return Not element.EnumerateObject().Any()
            End If

            If element.ValueKind = JsonValueKind.Array Then
                Return element.GetArrayLength() = 0
            End If

            Return False
        End Function

        Private Shared Function FormatScalar(element As JsonElement) As String
            Select Case element.ValueKind
                Case JsonValueKind.Number
                    Return element.GetRawText()
                Case JsonValueKind.True
                    Return "true"
                Case JsonValueKind.False
                    Return "false"
                Case JsonValueKind.Null
                    Return "null"
                Case Else
                    Return FormatString(If(element.GetString(), ""))
            End Select
        End Function

        Private Shared Function FormatKey(key As String) As String
            Return FormatString(key)
        End Function

        ''' Emits a plain scalar when safe, otherwise a double-quoted YAML scalar (JSON-compatible escaping).
        Private Shared Function FormatString(value As String) As String
            If NeedsQuoting(value) Then
                Return JsonSerializer.Serialize(value)
            End If

            Return value
        End Function

        Private Shared Function NeedsQuoting(value As String) As Boolean
            If value.Length = 0 Then
                Return True
            End If

            If ReservedScalars.Contains(value.ToLowerInvariant()) Then
                Return True
            End If

            If Char.IsWhiteSpace(value(0)) OrElse Char.IsWhiteSpace(value(value.Length - 1)) Then
                Return True
            End If

            ' Values that YAML would read as numbers must stay strings.
            Dim numberProbe As Double
            If Double.TryParse(value, Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture, numberProbe) Then
                Return True
            End If

            If value.StartsWith("- ", StringComparison.Ordinal) OrElse value = "-" Then
                Return True
            End If

            For Each ch In value
                Select Case ch
                    Case ":"c, "#"c, "{"c, "}"c, "["c, "]"c, ","c, "&"c, "*"c, "!"c, "|"c, ">"c, "'"c, """"c, "%"c, "@"c, "`"c, ControlChars.Lf, ControlChars.Cr, ControlChars.Tab
                        Return True
                End Select
            Next

            Return False
        End Function

        ' ---------- YAML -> JSON (representative subset) ----------

        Private NotInheritable Class YamlLine
            Public Sub New(indent As Integer, content As String, lineNumber As Integer)
                Me.Indent = indent
                Me.Content = content
                Me.LineNumber = lineNumber
            End Sub

            Public ReadOnly Property Indent As Integer
            Public ReadOnly Property Content As String
            Public ReadOnly Property LineNumber As Integer
        End Class

        Private Shared Function ParseLines(text As String) As List(Of YamlLine)
            Dim result = New List(Of YamlLine)()
            Dim lineNumber = 0

            Using reader = New IO.StringReader(text)
                While True
                    Dim raw = reader.ReadLine()
                    If raw Is Nothing Then
                        Exit While
                    End If

                    lineNumber += 1
                    Dim withoutComment = StripComment(raw)
                    Dim trimmed = withoutComment.TrimEnd()
                    If trimmed.Length = 0 Then
                        Continue While
                    End If

                    Dim contentStart = 0
                    While contentStart < trimmed.Length AndAlso trimmed(contentStart) = " "c
                        contentStart += 1
                    End While

                    If trimmed.Contains(ControlChars.Tab) Then
                        Throw New InvalidOperationException($"Tab characters are not supported in YAML indentation (line {lineNumber}).")
                    End If

                    Dim content = trimmed.Substring(contentStart)
                    If content = "---" OrElse content = "..." Then
                        Continue While
                    End If

                    result.Add(New YamlLine(contentStart, content, lineNumber))
                End While
            End Using

            Return result
        End Function

        Private Shared Function StripComment(line As String) As String
            Dim inSingle = False
            Dim inDouble = False
            Dim escaped = False

            For index = 0 To line.Length - 1
                Dim ch = line(index)

                If inDouble Then
                    If escaped Then
                        escaped = False
                    ElseIf ch = "\"c Then
                        escaped = True
                    ElseIf ch = """"c Then
                        inDouble = False
                    End If
                    Continue For
                End If

                If inSingle Then
                    If ch = "'"c Then
                        inSingle = False
                    End If
                    Continue For
                End If

                If ch = """"c Then
                    inDouble = True
                ElseIf ch = "'"c Then
                    inSingle = True
                ElseIf ch = "#"c AndAlso (index = 0 OrElse Char.IsWhiteSpace(line(index - 1))) Then
                    Return line.Substring(0, index)
                End If
            Next

            Return line
        End Function

        Private Function ParseBlock(lines As List(Of YamlLine), ByRef index As Integer, indent As Integer) As JsonNode
            If index >= lines.Count Then
                Return Nothing
            End If

            Dim line = lines(index)
            If line.Content.StartsWith("- ", StringComparison.Ordinal) OrElse line.Content = "-" Then
                Return ParseSequence(lines, index, indent)
            End If

            If line.Indent = indent AndAlso ContainsMappingSeparator(line.Content) Then
                Return ParseMapping(lines, index, indent)
            End If

            ' Single scalar document.
            index += 1
            Return ParseScalar(line.Content, line.LineNumber)
        End Function

        Private Function ParseSequence(lines As List(Of YamlLine), ByRef index As Integer, indent As Integer) As JsonArray
            Dim resultArray = New JsonArray()

            While index < lines.Count
                Dim line = lines(index)
                If line.Indent <> indent OrElse Not (line.Content.StartsWith("- ", StringComparison.Ordinal) OrElse line.Content = "-") Then
                    Exit While
                End If

                index += 1
                Dim rest = If(line.Content = "-", "", line.Content.Substring(2).Trim())

                If rest.Length = 0 Then
                    ' Nested block on the following, deeper-indented lines.
                    If index < lines.Count AndAlso lines(index).Indent > indent Then
                        resultArray.Add(ParseBlock(lines, index, lines(index).Indent))
                    Else
                        resultArray.Add(Nothing)
                    End If
                ElseIf ContainsMappingSeparator(rest) Then
                    ' Compact mapping opened on the dash line: "- key: value".
                    resultArray.Add(ParseCompactMapping(lines, index, indent, rest, line.LineNumber))
                Else
                    resultArray.Add(ParseScalar(rest, line.LineNumber))
                End If
            End While

            Return resultArray
        End Function

        Private Function ParseCompactMapping(lines As List(Of YamlLine), ByRef index As Integer, dashIndent As Integer, firstEntry As String, lineNumber As Integer) As JsonObject
            Dim mapping = New JsonObject()
            AddMappingEntry(mapping, lines, index, dashIndent + 2, firstEntry, lineNumber)

            While index < lines.Count AndAlso lines(index).Indent = dashIndent + 2 AndAlso
                Not lines(index).Content.StartsWith("- ", StringComparison.Ordinal) AndAlso
                ContainsMappingSeparator(lines(index).Content)
                Dim line = lines(index)
                index += 1
                AddMappingEntry(mapping, lines, index, dashIndent + 2, line.Content, line.LineNumber)
            End While

            Return mapping
        End Function

        Private Function ParseMapping(lines As List(Of YamlLine), ByRef index As Integer, indent As Integer) As JsonObject
            Dim mapping = New JsonObject()

            While index < lines.Count
                Dim line = lines(index)
                If line.Indent <> indent OrElse line.Content.StartsWith("- ", StringComparison.Ordinal) OrElse line.Content = "-" Then
                    Exit While
                End If

                If Not ContainsMappingSeparator(line.Content) Then
                    Throw New InvalidOperationException($"Expected a 'key: value' entry at line {line.LineNumber}.")
                End If

                index += 1
                AddMappingEntry(mapping, lines, index, indent, line.Content, line.LineNumber)
            End While

            Return mapping
        End Function

        Private Sub AddMappingEntry(mapping As JsonObject, lines As List(Of YamlLine), ByRef index As Integer, indent As Integer, entry As String, lineNumber As Integer)
            Dim separatorIndex = FindMappingSeparator(entry)
            If separatorIndex < 0 Then
                Throw New InvalidOperationException($"Expected a 'key: value' entry at line {lineNumber}.")
            End If

            Dim keyText = entry.Substring(0, separatorIndex).Trim()
            Dim valueText = entry.Substring(separatorIndex + 1).Trim()
            Dim key = ParseKey(keyText, lineNumber)

            If mapping.ContainsKey(key) Then
                Throw New InvalidOperationException($"Duplicate key '{key}' at line {lineNumber}.")
            End If

            If valueText.Length = 0 Then
                If index < lines.Count AndAlso lines(index).Indent > indent Then
                    mapping(key) = ParseBlock(lines, index, lines(index).Indent)
                Else
                    mapping(key) = Nothing
                End If
            Else
                mapping(key) = ParseScalar(valueText, lineNumber)
            End If
        End Sub

        Private Shared Function ContainsMappingSeparator(content As String) As Boolean
            Return FindMappingSeparator(content) >= 0
        End Function

        ''' Finds the top-level ": " (or trailing ":") outside quoted keys.
        Private Shared Function FindMappingSeparator(content As String) As Integer
            Dim inSingle = False
            Dim inDouble = False
            Dim escaped = False

            For index = 0 To content.Length - 1
                Dim ch = content(index)

                If inDouble Then
                    If escaped Then
                        escaped = False
                    ElseIf ch = "\"c Then
                        escaped = True
                    ElseIf ch = """"c Then
                        inDouble = False
                    End If
                    Continue For
                End If

                If inSingle Then
                    If ch = "'"c Then
                        inSingle = False
                    End If
                    Continue For
                End If

                If ch = """"c Then
                    inDouble = True
                ElseIf ch = "'"c Then
                    inSingle = True
                ElseIf ch = ":"c Then
                    If index = content.Length - 1 OrElse content(index + 1) = " "c Then
                        Return index
                    End If
                End If
            Next

            Return -1
        End Function

        Private Shared Function ParseKey(keyText As String, lineNumber As Integer) As String
            If keyText.Length = 0 Then
                Throw New InvalidOperationException($"Empty mapping key at line {lineNumber}.")
            End If

            If keyText.StartsWith("""", StringComparison.Ordinal) Then
                Return ParseDoubleQuoted(keyText, lineNumber)
            End If

            If keyText.StartsWith("'", StringComparison.Ordinal) Then
                Return ParseSingleQuoted(keyText, lineNumber)
            End If

            RejectUnsupportedScalar(keyText, lineNumber)
            Return keyText
        End Function

        Private Shared Function ParseScalar(valueText As String, lineNumber As Integer) As JsonNode
            If valueText = "{}" Then
                Return New JsonObject()
            End If

            If valueText = "[]" Then
                Return New JsonArray()
            End If

            If valueText.StartsWith("""", StringComparison.Ordinal) Then
                Return JsonValue.Create(ParseDoubleQuoted(valueText, lineNumber))
            End If

            If valueText.StartsWith("'", StringComparison.Ordinal) Then
                Return JsonValue.Create(ParseSingleQuoted(valueText, lineNumber))
            End If

            RejectUnsupportedScalar(valueText, lineNumber)

            Select Case valueText.ToLowerInvariant()
                Case "null", "~"
                    Return Nothing
                Case "true"
                    Return JsonValue.Create(True)
                Case "false"
                    Return JsonValue.Create(False)
            End Select

            ' Numbers must be valid JSON numbers to keep the built-in type.
            Try
                Using probe = JsonDocument.Parse(valueText)
                    If probe.RootElement.ValueKind = JsonValueKind.Number Then
                        Return JsonNode.Parse(valueText)
                    End If
                End Using
            Catch ex As JsonException
            End Try

            Return JsonValue.Create(valueText)
        End Function

        Private Shared Sub RejectUnsupportedScalar(valueText As String, lineNumber As Integer)
            If valueText.StartsWith("&", StringComparison.Ordinal) OrElse
                valueText.StartsWith("*", StringComparison.Ordinal) OrElse
                valueText.StartsWith("!", StringComparison.Ordinal) OrElse
                valueText.StartsWith("|", StringComparison.Ordinal) OrElse
                valueText.StartsWith(">", StringComparison.Ordinal) Then
                Throw New InvalidOperationException($"Unsupported YAML feature (anchor, alias, tag, or block scalar) at line {lineNumber}. Visual JSON supports a representative block-style YAML subset.")
            End If

            If (valueText.StartsWith("{", StringComparison.Ordinal) AndAlso valueText <> "{}") OrElse
                (valueText.StartsWith("[", StringComparison.Ordinal) AndAlso valueText <> "[]") Then
                Throw New InvalidOperationException($"Non-empty flow collections are not supported at line {lineNumber}. Use block-style YAML.")
            End If
        End Sub

        Private Shared Function ParseDoubleQuoted(text As String, lineNumber As Integer) As String
            Try
                Using document = JsonDocument.Parse(text)
                    If document.RootElement.ValueKind = JsonValueKind.String Then
                        Return If(document.RootElement.GetString(), "")
                    End If
                End Using
            Catch ex As JsonException
            End Try

            Throw New InvalidOperationException($"Invalid double-quoted scalar at line {lineNumber}.")
        End Function

        Private Shared Function ParseSingleQuoted(text As String, lineNumber As Integer) As String
            If text.Length < 2 OrElse Not text.EndsWith("'", StringComparison.Ordinal) Then
                Throw New InvalidOperationException($"Invalid single-quoted scalar at line {lineNumber}.")
            End If

            Return text.Substring(1, text.Length - 2).Replace("''", "'")
        End Function
    End Class
End Namespace
