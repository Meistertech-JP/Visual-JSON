' SPDX-License-Identifier: MPL-2.0
Imports System.Text.Json
Imports System.Text.RegularExpressions
Imports VisualJson.Core.Diagnostics
Imports VisualJson.Core.Models

Namespace Validation
    ''' JSON Schema validation for the MVP-2 subset defined in spec 06 §4.2 plus the
    ''' Phase 2 extensions (P2-5): const, minLength/maxLength, local $ref (#/...),
    ''' and format warnings. oneOf/anyOf/allOf, $id, and remote $ref stay unsupported;
    ''' external $ref is reported as a warning and never touches the network.
    Public Class SchemaValidationService
        Private Const MaxRefDepth As Integer = 32
        Private Shared ReadOnly SchemaDocumentOptions As New JsonDocumentOptions With {
            .AllowTrailingCommas = False,
            .CommentHandling = JsonCommentHandling.Skip
        }

        Private Shared ReadOnly InstanceDocumentOptions As New JsonDocumentOptions With {
            .AllowTrailingCommas = False,
            .CommentHandling = JsonCommentHandling.Disallow
        }

        Private Shared ReadOnly PatternTimeout As TimeSpan = TimeSpan.FromMilliseconds(500)

        ''' Validates instance JSON text against schema JSON text. When documentRoot is provided,
        ''' diagnostics carry the source line/column of the offending body location.
        Public Function Validate(instanceText As String,
                                 schemaText As String,
                                 Optional schemaUri As String = "",
                                 Optional documentRoot As JsonTreeNode = Nothing) As List(Of ValidationDiagnostic)
            Dim diagnostics = New List(Of ValidationDiagnostic)()
            Dim positions = BuildPositionIndex(documentRoot)

            Using schemaDocument = JsonDocument.Parse(If(schemaText, ""), SchemaDocumentOptions)
                Using instanceDocument = JsonDocument.Parse(If(instanceText, ""), InstanceDocumentOptions)
                    ValidateElement(instanceDocument.RootElement,
                                    schemaDocument.RootElement,
                                    "",
                                    "$",
                                    "#",
                                    If(schemaUri, ""),
                                    positions,
                                    diagnostics,
                                    schemaDocument.RootElement,
                                    New List(Of String)())
                End Using
            End Using

            Return diagnostics
        End Function

        Private Sub ValidateElement(instance As JsonElement,
                                    schema As JsonElement,
                                    pointer As String,
                                    jsonPath As String,
                                    schemaPath As String,
                                    schemaUri As String,
                                    positions As Dictionary(Of String, JsonTreeNode),
                                    diagnostics As List(Of ValidationDiagnostic),
                                    rootSchema As JsonElement,
                                    refStack As List(Of String))
            ' Boolean schemas: true allows anything, false rejects everything.
            If schema.ValueKind = JsonValueKind.True Then
                Return
            End If

            If schema.ValueKind = JsonValueKind.False Then
                Add(diagnostics, "SCH-SCHEMA", $"Schema '{schemaPath}' does not allow any value here.", pointer, jsonPath, schemaPath, schemaUri, positions)
                Return
            End If

            If schema.ValueKind <> JsonValueKind.Object Then
                Return
            End If

            ' FR-P2-502: when $ref is present, only $ref is evaluated (draft-07
            ' compatible). Diagnostics keep the referencing schemaPath so the
            ' definition jump lands on the referencing location (spec 06 §3.2).
            Dim refElement As JsonElement = Nothing
            If schema.TryGetProperty("$ref", refElement) AndAlso refElement.ValueKind = JsonValueKind.String Then
                Dim refValue = If(refElement.GetString(), "")
                If Not refValue.StartsWith("#", StringComparison.Ordinal) Then
                    Add(diagnostics, "SCH-REF-UNSUPPORTED", $"The external reference '{refValue}' is not supported and was ignored. No network access is performed.",
                        pointer, jsonPath, $"{schemaPath}/$ref", schemaUri, positions, severity:="Warning")
                    Return
                End If

                If refStack.Contains(refValue) Then
                    Add(diagnostics, "SCH-REF-CYCLE", $"The reference '{refValue}' is part of a reference cycle; validation for this branch was stopped.",
                        pointer, jsonPath, $"{schemaPath}/$ref", schemaUri, positions)
                    Return
                End If

                If refStack.Count >= MaxRefDepth Then
                    Add(diagnostics, "SCH-REF-DEPTH", $"The reference chain exceeds the depth limit of {MaxRefDepth}.",
                        pointer, jsonPath, $"{schemaPath}/$ref", schemaUri, positions)
                    Return
                End If

                Dim resolved As JsonElement = Nothing
                If Not TryResolveSchemaPointer(rootSchema, refValue, resolved) Then
                    Add(diagnostics, "SCH-REF-NOTFOUND", $"The reference '{refValue}' could not be resolved in the schema document.",
                        pointer, jsonPath, $"{schemaPath}/$ref", schemaUri, positions)
                    Return
                End If

                refStack.Add(refValue)
                ValidateElement(instance, resolved, pointer, jsonPath, schemaPath, schemaUri, positions, diagnostics, rootSchema, refStack)
                refStack.RemoveAt(refStack.Count - 1)
                Return
            End If

            ValidateType(instance, schema, pointer, jsonPath, schemaPath, schemaUri, positions, diagnostics)
            ValidateEnum(instance, schema, pointer, jsonPath, schemaPath, schemaUri, positions, diagnostics)
            ValidateConst(instance, schema, pointer, jsonPath, schemaPath, schemaUri, positions, diagnostics)
            ValidateNumberRange(instance, schema, pointer, jsonPath, schemaPath, schemaUri, positions, diagnostics)
            ValidateStringLength(instance, schema, pointer, jsonPath, schemaPath, schemaUri, positions, diagnostics)
            ValidatePattern(instance, schema, pointer, jsonPath, schemaPath, schemaUri, positions, diagnostics)
            ValidateFormat(instance, schema, pointer, jsonPath, schemaPath, schemaUri, positions, diagnostics)

            If instance.ValueKind = JsonValueKind.Object Then
                ValidateObject(instance, schema, pointer, jsonPath, schemaPath, schemaUri, positions, diagnostics, rootSchema, refStack)
            ElseIf instance.ValueKind = JsonValueKind.Array Then
                ValidateArray(instance, schema, pointer, jsonPath, schemaPath, schemaUri, positions, diagnostics, rootSchema, refStack)
            End If
        End Sub

        Private Shared Function TryResolveSchemaPointer(rootSchema As JsonElement, reference As String, ByRef resolved As JsonElement) As Boolean
            resolved = rootSchema
            If String.Equals(reference, "#", StringComparison.Ordinal) Then
                Return True
            End If

            If Not reference.StartsWith("#/", StringComparison.Ordinal) Then
                Return False
            End If

            Dim current = rootSchema
            For Each rawSegment In reference.Substring(2).Split("/"c)
                Dim segment = rawSegment.Replace("~1", "/").Replace("~0", "~")
                If current.ValueKind = JsonValueKind.Object Then
                    Dim child As JsonElement = Nothing
                    If Not current.TryGetProperty(segment, child) Then
                        Return False
                    End If

                    current = child
                ElseIf current.ValueKind = JsonValueKind.Array Then
                    Dim index As Integer
                    If Not Integer.TryParse(segment, Globalization.NumberStyles.None, Globalization.CultureInfo.InvariantCulture, index) OrElse index >= current.GetArrayLength() Then
                        Return False
                    End If

                    current = current.EnumerateArray().ElementAt(index)
                Else
                    Return False
                End If
            Next

            resolved = current
            Return True
        End Function

        ''' FR-P2-501: const comparison via structural equality.
        Private Sub ValidateConst(instance As JsonElement,
                                  schema As JsonElement,
                                  pointer As String,
                                  jsonPath As String,
                                  schemaPath As String,
                                  schemaUri As String,
                                  positions As Dictionary(Of String, JsonTreeNode),
                                  diagnostics As List(Of ValidationDiagnostic))
            Dim constElement As JsonElement = Nothing
            If Not schema.TryGetProperty("const", constElement) Then
                Return
            End If

            If Not JsonElement.DeepEquals(instance, constElement) Then
                Add(diagnostics, "SCH-CONST", $"The value must be exactly {constElement.GetRawText()}.",
                    pointer, jsonPath, $"{schemaPath}/const", schemaUri, positions)
            End If
        End Sub

        ''' FR-P2-501: minLength/maxLength count Unicode code points.
        Private Sub ValidateStringLength(instance As JsonElement,
                                         schema As JsonElement,
                                         pointer As String,
                                         jsonPath As String,
                                         schemaPath As String,
                                         schemaUri As String,
                                         positions As Dictionary(Of String, JsonTreeNode),
                                         diagnostics As List(Of ValidationDiagnostic))
            If instance.ValueKind <> JsonValueKind.String Then
                Return
            End If

            Dim bound As JsonElement = Nothing
            Dim hasMin = schema.TryGetProperty("minLength", bound) AndAlso bound.ValueKind = JsonValueKind.Number
            Dim minValue = If(hasMin, bound.GetInt32(), 0)
            Dim maxBound As JsonElement = Nothing
            Dim hasMax = schema.TryGetProperty("maxLength", maxBound) AndAlso maxBound.ValueKind = JsonValueKind.Number
            If Not hasMin AndAlso Not hasMax Then
                Return
            End If

            Dim length = CountCodePoints(If(instance.GetString(), ""))
            If hasMin AndAlso length < minValue Then
                Add(diagnostics, "SCH-MINLENGTH", $"The string length {length} is below the minimum length {minValue}.",
                    pointer, jsonPath, $"{schemaPath}/minLength", schemaUri, positions)
            End If

            If hasMax AndAlso length > maxBound.GetInt32() Then
                Add(diagnostics, "SCH-MAXLENGTH", $"The string length {length} is above the maximum length {maxBound.GetInt32()}.",
                    pointer, jsonPath, $"{schemaPath}/maxLength", schemaUri, positions)
            End If
        End Sub

        Private Shared Function CountCodePoints(value As String) As Integer
            Dim count = 0
            Dim index = 0
            While index < value.Length
                If Char.IsHighSurrogate(value(index)) AndAlso index + 1 < value.Length AndAlso Char.IsLowSurrogate(value(index + 1)) Then
                    index += 2
                Else
                    index += 1
                End If

                count += 1
            End While

            Return count
        End Function

        ''' FR-P2-503 (Could): format checks report warnings; unknown formats are ignored.
        Private Sub ValidateFormat(instance As JsonElement,
                                   schema As JsonElement,
                                   pointer As String,
                                   jsonPath As String,
                                   schemaPath As String,
                                   schemaUri As String,
                                   positions As Dictionary(Of String, JsonTreeNode),
                                   diagnostics As List(Of ValidationDiagnostic))
            If instance.ValueKind <> JsonValueKind.String Then
                Return
            End If

            Dim formatElement As JsonElement = Nothing
            If Not schema.TryGetProperty("format", formatElement) OrElse formatElement.ValueKind <> JsonValueKind.String Then
                Return
            End If

            Dim formatName = If(formatElement.GetString(), "")
            Dim value = If(instance.GetString(), "")
            Dim valid As Boolean
            Select Case formatName
                Case "date-time"
                    Dim parsedOffset As DateTimeOffset
                    valid = DateTimeOffset.TryParseExact(value,
                                                         {"yyyy-MM-dd'T'HH:mm:ssK", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK"},
                                                         Globalization.CultureInfo.InvariantCulture,
                                                         Globalization.DateTimeStyles.None,
                                                         parsedOffset)
                Case "date"
                    Dim parsedDate As DateTime
                    valid = DateTime.TryParseExact(value, "yyyy-MM-dd", Globalization.CultureInfo.InvariantCulture, Globalization.DateTimeStyles.None, parsedDate)
                Case "time"
                    Dim parsedTime As DateTime
                    valid = DateTime.TryParseExact(value,
                                                   {"HH:mm:ss", "HH:mm:ss.FFFFFFF"},
                                                   Globalization.CultureInfo.InvariantCulture,
                                                   Globalization.DateTimeStyles.None,
                                                   parsedTime)
                Case "email"
                    valid = Regex.IsMatch(value, "^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.None, PatternTimeout)
                Case "uri"
                    Dim parsedUri As Uri = Nothing
                    valid = Uri.TryCreate(value, UriKind.Absolute, parsedUri)
                Case Else
                    Return
            End Select

            If Not valid Then
                Add(diagnostics, "SCH-FORMAT", $"The string does not match the format '{formatName}'.",
                    pointer, jsonPath, $"{schemaPath}/format", schemaUri, positions, severity:="Warning")
            End If
        End Sub

        Private Sub ValidateType(instance As JsonElement,
                                 schema As JsonElement,
                                 pointer As String,
                                 jsonPath As String,
                                 schemaPath As String,
                                 schemaUri As String,
                                 positions As Dictionary(Of String, JsonTreeNode),
                                 diagnostics As List(Of ValidationDiagnostic))
            Dim typeElement As JsonElement = Nothing
            If Not schema.TryGetProperty("type", typeElement) Then
                Return
            End If

            Dim allowed = New List(Of String)()
            If typeElement.ValueKind = JsonValueKind.String Then
                allowed.Add(If(typeElement.GetString(), ""))
            ElseIf typeElement.ValueKind = JsonValueKind.Array Then
                For Each item In typeElement.EnumerateArray()
                    If item.ValueKind = JsonValueKind.String Then
                        allowed.Add(If(item.GetString(), ""))
                    End If
                Next
            End If

            If allowed.Count = 0 Then
                Return
            End If

            For Each allowedType In allowed
                If MatchesType(instance, allowedType) Then
                    Return
                End If
            Next

            Add(diagnostics, "SCH-TYPE",
                $"Expected type '{String.Join(" or ", allowed)}' but found '{DescribeKind(instance)}'.",
                pointer, jsonPath, $"{schemaPath}/type", schemaUri, positions)
        End Sub

        Private Sub ValidateEnum(instance As JsonElement,
                                 schema As JsonElement,
                                 pointer As String,
                                 jsonPath As String,
                                 schemaPath As String,
                                 schemaUri As String,
                                 positions As Dictionary(Of String, JsonTreeNode),
                                 diagnostics As List(Of ValidationDiagnostic))
            Dim enumElement As JsonElement = Nothing
            If Not schema.TryGetProperty("enum", enumElement) OrElse enumElement.ValueKind <> JsonValueKind.Array Then
                Return
            End If

            For Each candidate In enumElement.EnumerateArray()
                If JsonElement.DeepEquals(instance, candidate) Then
                    Return
                End If
            Next

            Add(diagnostics, "SCH-ENUM", "The value is not one of the allowed enum values.",
                pointer, jsonPath, $"{schemaPath}/enum", schemaUri, positions)
        End Sub

        Private Sub ValidateNumberRange(instance As JsonElement,
                                        schema As JsonElement,
                                        pointer As String,
                                        jsonPath As String,
                                        schemaPath As String,
                                        schemaUri As String,
                                        positions As Dictionary(Of String, JsonTreeNode),
                                        diagnostics As List(Of ValidationDiagnostic))
            If instance.ValueKind <> JsonValueKind.Number Then
                Return
            End If

            Dim value = instance.GetDouble()
            Dim bound As JsonElement = Nothing

            If schema.TryGetProperty("minimum", bound) AndAlso bound.ValueKind = JsonValueKind.Number AndAlso value < bound.GetDouble() Then
                Add(diagnostics, "SCH-MINIMUM", $"The value {instance.GetRawText()} is below the minimum {bound.GetRawText()}.",
                    pointer, jsonPath, $"{schemaPath}/minimum", schemaUri, positions)
            End If

            If schema.TryGetProperty("maximum", bound) AndAlso bound.ValueKind = JsonValueKind.Number AndAlso value > bound.GetDouble() Then
                Add(diagnostics, "SCH-MAXIMUM", $"The value {instance.GetRawText()} is above the maximum {bound.GetRawText()}.",
                    pointer, jsonPath, $"{schemaPath}/maximum", schemaUri, positions)
            End If
        End Sub

        Private Sub ValidatePattern(instance As JsonElement,
                                    schema As JsonElement,
                                    pointer As String,
                                    jsonPath As String,
                                    schemaPath As String,
                                    schemaUri As String,
                                    positions As Dictionary(Of String, JsonTreeNode),
                                    diagnostics As List(Of ValidationDiagnostic))
            If instance.ValueKind <> JsonValueKind.String Then
                Return
            End If

            Dim patternElement As JsonElement = Nothing
            If Not schema.TryGetProperty("pattern", patternElement) OrElse patternElement.ValueKind <> JsonValueKind.String Then
                Return
            End If

            Dim pattern = If(patternElement.GetString(), "")
            Try
                If Not Regex.IsMatch(If(instance.GetString(), ""), pattern, RegexOptions.None, PatternTimeout) Then
                    Add(diagnostics, "SCH-PATTERN", $"The string does not match the pattern '{pattern}'.",
                        pointer, jsonPath, $"{schemaPath}/pattern", schemaUri, positions)
                End If
            Catch ex As ArgumentException
                Add(diagnostics, "SCH-PATTERN-INVALID", $"The schema pattern '{pattern}' is not a valid regular expression.",
                    pointer, jsonPath, $"{schemaPath}/pattern", schemaUri, positions, severity:="Warning")
            Catch ex As RegexMatchTimeoutException
                Add(diagnostics, "SCH-PATTERN-TIMEOUT", $"Evaluating the pattern '{pattern}' timed out.",
                    pointer, jsonPath, $"{schemaPath}/pattern", schemaUri, positions, severity:="Warning")
            End Try
        End Sub

        Private Sub ValidateObject(instance As JsonElement,
                                   schema As JsonElement,
                                   pointer As String,
                                   jsonPath As String,
                                   schemaPath As String,
                                   schemaUri As String,
                                   positions As Dictionary(Of String, JsonTreeNode),
                                   diagnostics As List(Of ValidationDiagnostic),
                                   rootSchema As JsonElement,
                                   refStack As List(Of String))
            Dim requiredElement As JsonElement = Nothing
            If schema.TryGetProperty("required", requiredElement) AndAlso requiredElement.ValueKind = JsonValueKind.Array Then
                For Each requiredName In requiredElement.EnumerateArray()
                    If requiredName.ValueKind <> JsonValueKind.String Then
                        Continue For
                    End If

                    Dim name = If(requiredName.GetString(), "")
                    Dim found As JsonElement = Nothing
                    If Not instance.TryGetProperty(name, found) Then
                        Add(diagnostics, "SCH-REQUIRED", $"The required property '{name}' is missing.",
                            pointer, jsonPath, $"{schemaPath}/required", schemaUri, positions)
                    End If
                Next
            End If

            Dim propertiesElement As JsonElement = Nothing
            Dim hasProperties = schema.TryGetProperty("properties", propertiesElement) AndAlso propertiesElement.ValueKind = JsonValueKind.Object
            Dim additionalElement As JsonElement = Nothing
            Dim hasAdditional = schema.TryGetProperty("additionalProperties", additionalElement)

            For Each propertyItem In instance.EnumerateObject()
                Dim childPointer = $"{pointer}/{JsonTreeNode.EscapePointerSegment(propertyItem.Name)}"
                Dim childPath = $"{jsonPath}.{propertyItem.Name}"
                Dim propertySchema As JsonElement = Nothing

                If hasProperties AndAlso propertiesElement.TryGetProperty(propertyItem.Name, propertySchema) Then
                    ValidateElement(propertyItem.Value, propertySchema, childPointer, childPath,
                                    $"{schemaPath}/properties/{JsonTreeNode.EscapePointerSegment(propertyItem.Name)}",
                                    schemaUri, positions, diagnostics, rootSchema, refStack)
                ElseIf hasAdditional Then
                    If additionalElement.ValueKind = JsonValueKind.False Then
                        Add(diagnostics, "SCH-ADDITIONAL", $"The property '{propertyItem.Name}' is not allowed by the schema.",
                            childPointer, childPath, $"{schemaPath}/additionalProperties", schemaUri, positions)
                    ElseIf additionalElement.ValueKind = JsonValueKind.Object Then
                        ValidateElement(propertyItem.Value, additionalElement, childPointer, childPath,
                                        $"{schemaPath}/additionalProperties", schemaUri, positions, diagnostics, rootSchema, refStack)
                    End If
                End If
            Next
        End Sub

        Private Sub ValidateArray(instance As JsonElement,
                                  schema As JsonElement,
                                  pointer As String,
                                  jsonPath As String,
                                  schemaPath As String,
                                  schemaUri As String,
                                  positions As Dictionary(Of String, JsonTreeNode),
                                  diagnostics As List(Of ValidationDiagnostic),
                                  rootSchema As JsonElement,
                                  refStack As List(Of String))
            Dim itemsElement As JsonElement = Nothing
            If Not schema.TryGetProperty("items", itemsElement) Then
                Return
            End If

            If itemsElement.ValueKind <> JsonValueKind.Object AndAlso
                itemsElement.ValueKind <> JsonValueKind.True AndAlso
                itemsElement.ValueKind <> JsonValueKind.False Then
                Return
            End If

            Dim index = 0
            For Each item In instance.EnumerateArray()
                ValidateElement(item, itemsElement, $"{pointer}/{index}", $"{jsonPath}[{index}]",
                                $"{schemaPath}/items", schemaUri, positions, diagnostics, rootSchema, refStack)
                index += 1
            Next
        End Sub

        Private Shared Function MatchesType(instance As JsonElement, typeName As String) As Boolean
            Select Case typeName
                Case "object"
                    Return instance.ValueKind = JsonValueKind.Object
                Case "array"
                    Return instance.ValueKind = JsonValueKind.Array
                Case "string"
                    Return instance.ValueKind = JsonValueKind.String
                Case "boolean"
                    Return instance.ValueKind = JsonValueKind.True OrElse instance.ValueKind = JsonValueKind.False
                Case "null"
                    Return instance.ValueKind = JsonValueKind.Null
                Case "number"
                    Return instance.ValueKind = JsonValueKind.Number
                Case "integer"
                    If instance.ValueKind <> JsonValueKind.Number Then
                        Return False
                    End If

                    Dim integerValue As Long
                    If instance.TryGetInt64(integerValue) Then
                        Return True
                    End If

                    Dim doubleValue = instance.GetDouble()
                    Return doubleValue = Math.Truncate(doubleValue) AndAlso Not Double.IsInfinity(doubleValue)
                Case Else
                    Return True
            End Select
        End Function

        Private Shared Function DescribeKind(instance As JsonElement) As String
            Select Case instance.ValueKind
                Case JsonValueKind.Object
                    Return "object"
                Case JsonValueKind.Array
                    Return "array"
                Case JsonValueKind.String
                    Return "string"
                Case JsonValueKind.Number
                    Return "number"
                Case JsonValueKind.True, JsonValueKind.False
                    Return "boolean"
                Case JsonValueKind.Null
                    Return "null"
                Case Else
                    Return "unknown"
            End Select
        End Function

        Private Shared Function BuildPositionIndex(root As JsonTreeNode) As Dictionary(Of String, JsonTreeNode)
            Dim map = New Dictionary(Of String, JsonTreeNode)(StringComparer.Ordinal)
            AppendPositions(root, map)
            Return map
        End Function

        Private Shared Sub AppendPositions(node As JsonTreeNode, map As Dictionary(Of String, JsonTreeNode))
            If node Is Nothing Then
                Return
            End If

            map(If(node.JsonPointer, "")) = node
            For Each child In node.Children
                AppendPositions(child, map)
            Next
        End Sub

        Private Shared Sub Add(diagnostics As List(Of ValidationDiagnostic),
                               errorCode As String,
                               message As String,
                               pointer As String,
                               jsonPath As String,
                               schemaPath As String,
                               schemaUri As String,
                               positions As Dictionary(Of String, JsonTreeNode),
                               Optional severity As String = "Error")
            Dim line As Integer? = Nothing
            Dim column As Integer? = Nothing
            Dim relatedRange As TextRange = Nothing
            Dim node As JsonTreeNode = Nothing

            If positions.TryGetValue(If(pointer, ""), node) Then
                line = node.SourceLine
                column = node.SourceColumn
                If node.SourceStartIndex.HasValue Then
                    relatedRange = New TextRange(node.SourceStartIndex.Value, 1)
                End If
            End If

            diagnostics.Add(New ValidationDiagnostic(severity, message, line, column,
                                                     errorCode:=errorCode,
                                                     jsonPath:=jsonPath,
                                                     jsonPointer:=pointer,
                                                     schemaPath:=schemaPath,
                                                     schemaUri:=schemaUri,
                                                     relatedRange:=relatedRange))
        End Sub
    End Class
End Namespace
