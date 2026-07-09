' SPDX-License-Identifier: MPL-2.0
Imports System.IO
Imports System.Text
Imports System.Text.Json
Imports System.Text.Json.Nodes
Imports System.Xml

Namespace Conversion
    ''' JSON to XML and XML to JSON conversion per spec 06 §5.
    ''' XML reading always disables DTD, external entities, and external subsets (NFR-SEC-003/004).
    Public Class JsonXmlConversionService
        Private Shared ReadOnly StrictOptions As New JsonDocumentOptions With {
            .AllowTrailingCommas = False,
            .CommentHandling = JsonCommentHandling.Disallow
        }

        Private Const XsiNamespace As String = "http://www.w3.org/2001/XMLSchema-instance"

        Public Function ConvertJsonToXml(jsonText As String) As ConversionResult
            Return ConvertJsonToXml(jsonText, XmlConversionOptions.CreateDefault())
        End Function

        Public Function ConvertJsonToXml(jsonText As String, options As XmlConversionOptions) As ConversionResult
            Dim effectiveOptions = If(options, XmlConversionOptions.CreateDefault())
            Dim warnings = New List(Of String)()

            Using document = JsonDocument.Parse(If(jsonText, ""), StrictOptions)
                Dim root = document.RootElement
                Dim rootName = "root"
                Dim rootElement = root

                ' Spec: when the document has no explicit root name, supplement "root".
                ' A single-property top-level object whose value is an object is treated as the named root.
                If root.ValueKind = JsonValueKind.Object Then
                    Dim properties = root.EnumerateObject().ToList()
                    If properties.Count = 1 AndAlso properties(0).Value.ValueKind = JsonValueKind.Object AndAlso Not properties(0).Name.StartsWith("@", StringComparison.Ordinal) Then
                        rootName = properties(0).Name
                        rootElement = properties(0).Value
                    End If
                End If

                Dim settings = New XmlWriterSettings With {
                    .Indent = True,
                    .IndentChars = "  ",
                    .Encoding = New UTF8Encoding(encoderShouldEmitUTF8Identifier:=False),
                    .OmitXmlDeclaration = False
                }

                Dim builder = New StringBuilder()
                Using writer = XmlWriter.Create(builder, settings)
                    WriteElement(writer, rootName, rootElement, warnings, effectiveOptions, isRoot:=True)
                End Using

                Return New ConversionResult(builder.ToString(), warnings)
            End Using
        End Function

        Public Function ConvertXmlToJson(xmlText As String) As ConversionResult
            Dim warnings = New List(Of String)()
            Dim settings = New XmlReaderSettings With {
                .DtdProcessing = DtdProcessing.Prohibit,
                .XmlResolver = Nothing,
                .MaxCharactersFromEntities = 0,
                .CloseInput = True
            }

            Dim xmlDocument = New XmlDocument With {.XmlResolver = Nothing}
            Using reader = XmlReader.Create(New StringReader(If(xmlText, "")), settings)
                xmlDocument.Load(reader)
            End Using

            Dim rootElement = xmlDocument.DocumentElement
            If rootElement Is Nothing Then
                Throw New InvalidOperationException("The XML document has no root element.")
            End If

            Dim rootObject = New JsonObject()
            rootObject(rootElement.Name) = ConvertElementToNode(rootElement, warnings)

            Dim output = rootObject.ToJsonString(New JsonSerializerOptions With {.WriteIndented = True})
            Return New ConversionResult(output, warnings)
        End Function

        Private Sub WriteElement(writer As XmlWriter, name As String, element As JsonElement, warnings As List(Of String), options As XmlConversionOptions, isRoot As Boolean)
            Dim safeName = GetSafeElementName(name, warnings)
            writer.WriteStartElement(safeName)
            If isRoot AndAlso options.NullMode = XmlNullMode.XsiNil Then
                writer.WriteAttributeString("xmlns", "xsi", Nothing, XsiNamespace)
            End If

            Select Case element.ValueKind
                Case JsonValueKind.Object
                    ' Attributes (@name) first, then #text, then child elements.
                    For Each item In element.EnumerateObject()
                        If item.Name.StartsWith("@", StringComparison.Ordinal) AndAlso item.Name.Length > 1 Then
                            If IsPrimitive(item.Value) Then
                                writer.WriteAttributeString(GetSafeElementName(item.Name.Substring(1), warnings), GetScalarText(item.Value))
                            Else
                                warnings.Add($"Attribute '{item.Name}' has a non-primitive value and was written as an element instead.")
                            End If
                        End If
                    Next

                    For Each item In element.EnumerateObject()
                        If item.Name.StartsWith("@", StringComparison.Ordinal) AndAlso item.Name.Length > 1 Then
                            If Not IsPrimitive(item.Value) Then
                                WriteChild(writer, item.Name.Substring(1), item.Value, warnings, options)
                            End If
                            Continue For
                        End If

                        If String.Equals(item.Name, "#text", StringComparison.Ordinal) Then
                            writer.WriteString(GetScalarText(item.Value))
                            Continue For
                        End If

                        WriteChild(writer, item.Name, item.Value, warnings, options)
                    Next

                Case JsonValueKind.Array
                    ' Arrays reaching here have no usable parent name (root arrays,
                    ' arrays inside arrays), so both modes expand as "item".
                    For Each item In element.EnumerateArray()
                        WriteElement(writer, "item", item, warnings, options, isRoot:=False)
                    Next

                Case JsonValueKind.Null
                    If options.NullMode = XmlNullMode.XsiNil Then
                        writer.WriteAttributeString("nil", XsiNamespace, "true")
                    Else
                        ' Keep the exact v1.0.0 empty-element output for the default mode.
                        writer.WriteString("")
                    End If

                Case Else
                    writer.WriteString(GetScalarText(element))
            End Select

            writer.WriteEndElement()
        End Sub

        ''' FR-P2-601: RepeatParentName expands a named array property as repeated
        ''' elements carrying the property name instead of a wrapper with items.
        Private Sub WriteChild(writer As XmlWriter, name As String, value As JsonElement, warnings As List(Of String), options As XmlConversionOptions)
            If options.ArrayMode = XmlArrayMode.RepeatParentName AndAlso value.ValueKind = JsonValueKind.Array Then
                For Each arrayItem In value.EnumerateArray()
                    WriteElement(writer, name, arrayItem, warnings, options, isRoot:=False)
                Next

                Return
            End If

            WriteElement(writer, name, value, warnings, options, isRoot:=False)
        End Sub

        Private Function ConvertElementToNode(element As XmlElement, warnings As List(Of String)) As JsonNode
            Dim hasAttributes = element.Attributes.Count > 0
            Dim childElements = element.ChildNodes.OfType(Of XmlElement)().ToList()
            Dim textContent = String.Concat(element.ChildNodes.
                OfType(Of XmlNode)().
                Where(Function(node) node.NodeType = XmlNodeType.Text OrElse node.NodeType = XmlNodeType.CDATA).
                Select(Function(node) node.Value)).Trim()

            ' Values keep their string representation by default (spec 06 §5).
            If Not hasAttributes AndAlso childElements.Count = 0 Then
                Return JsonValue.Create(textContent)
            End If

            Dim resultObject = New JsonObject()
            For Each attribute As XmlAttribute In element.Attributes
                If attribute.Name.StartsWith("xmlns", StringComparison.OrdinalIgnoreCase) Then
                    warnings.Add($"Namespace declaration '{attribute.Name}' was kept as an attribute property.")
                End If

                resultObject($"@{attribute.Name}") = JsonValue.Create(attribute.Value)
            Next

            If textContent.Length > 0 Then
                resultObject("#text") = JsonValue.Create(textContent)
            End If

            For Each groupItem In childElements.GroupBy(Function(child) child.Name)
                Dim converted = groupItem.Select(Function(child) ConvertElementToNode(child, warnings)).ToList()
                If converted.Count = 1 Then
                    resultObject(groupItem.Key) = converted(0)
                Else
                    Dim arrayNode = New JsonArray()
                    For Each item In converted
                        arrayNode.Add(item)
                    Next

                    resultObject(groupItem.Key) = arrayNode
                End If
            Next

            Return resultObject
        End Function

        Private Shared Function IsPrimitive(element As JsonElement) As Boolean
            Return element.ValueKind <> JsonValueKind.Object AndAlso element.ValueKind <> JsonValueKind.Array
        End Function

        Private Shared Function GetScalarText(element As JsonElement) As String
            Select Case element.ValueKind
                Case JsonValueKind.String
                    Return If(element.GetString(), "")
                Case JsonValueKind.Null
                    Return ""
                Case Else
                    Return element.GetRawText()
            End Select
        End Function

        Private Shared Function GetSafeElementName(name As String, warnings As List(Of String)) As String
            Dim candidate = If(String.IsNullOrWhiteSpace(name), "item", name)

            Try
                XmlConvert.VerifyName(candidate)
                Return candidate
            Catch ex As XmlException
                Dim encoded = XmlConvert.EncodeLocalName(candidate)
                warnings.Add($"The name '{candidate}' is not a valid XML name and was encoded as '{encoded}'.")
                Return encoded
            Catch ex As ArgumentException
                warnings.Add("An empty name was replaced with 'item'.")
                Return "item"
            End Try
        End Function
    End Class
End Namespace
