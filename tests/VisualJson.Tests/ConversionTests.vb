' SPDX-License-Identifier: MPL-2.0
Imports System.IO
Imports System.Diagnostics
Imports System.Text
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports VisualJson.Core.Conversion
Imports VisualJson.Core.Infrastructure
Imports VisualJson.Core.Models
Imports VisualJson.Core.Parsing
Imports VisualJson.Core.Serialization
Imports VisualJson.Core.Services
Imports VisualJson.Core.Validation

<TestClass>
Public Class ConversionTests

    <TestMethod(DisplayName:="UT-M3-001 JSON to XML with attributes")>
    Public Sub JsonToXmlWithAttributes()
        Dim conversion = New JsonXmlConversionService()
        Dim result = conversion.ConvertJsonToXml("{""root"":{""@id"":""1"",""name"":""A""}}")

        AssertContains(result.Output, "<root id=""1"">", "attribute on root")
        AssertContains(result.Output, "<name>A</name>", "child element")

        Dim listResult = conversion.ConvertJsonToXml("{""list"":[1,2]}")
        AssertContains(listResult.Output, "<item>1</item>", "array item element")
        AssertContains(listResult.Output, "<item>2</item>", "second array item element")
    End Sub

    <TestMethod(DisplayName:="UT-M3-002 JSON to YAML two space list")>
    Public Sub JsonToYamlTwoSpaceList()
        Dim conversion = New JsonYamlConversionService()
        Dim result = conversion.ConvertJsonToYaml("{""list"":[1,2],""name"":""Visual JSON"",""meta"":{""ok"":true,""none"":null}}")

        AssertContains(result.Output, "list:" & Environment.NewLine & "  - 1" & Environment.NewLine & "  - 2", "2-space list")
        AssertContains(result.Output, "name: Visual JSON", "plain string scalar")
        AssertContains(result.Output, "  ok: true", "boolean kept as YAML built-in")
        AssertContains(result.Output, "  none: null", "null kept as YAML built-in")
    End Sub

    <TestMethod(DisplayName:="UT-M3-003 XXE is not resolved")>
    Public Sub XxeIsNotResolved()
        Dim conversion = New JsonXmlConversionService()
        Dim tempRoot = CreateTempDirectory()
        Try
            Dim secretPath = Path.Combine(tempRoot, "secret.txt")
            File.WriteAllText(secretPath, "XXE-SECRET-CONTENT")

            Dim xml = "<?xml version=""1.0""?>" &
                $"<!DOCTYPE root [<!ENTITY xxe SYSTEM ""file:///{secretPath.Replace("\", "/")}"">]>" &
                "<root>&xxe;</root>"

            Dim resolved = False
            Try
                Dim result = conversion.ConvertXmlToJson(xml)
                resolved = result.Output.Contains("XXE-SECRET-CONTENT", StringComparison.Ordinal)
            Catch ex As Xml.XmlException
                ' Expected: DTD processing is prohibited.
            End Try

            AssertFalse(resolved, "external entity must not be resolved")
        Finally
            Directory.Delete(tempRoot, recursive:=True)
        End Try
    End Sub

    <TestMethod(DisplayName:="IT-M3-001 failed conversion never touches files")>
    Public Sub FailedConversionNeverTouchesFiles()
        Dim conversion = New JsonXmlConversionService()
        Dim tempRoot = CreateTempDirectory()
        Try
            Dim source = Path.Combine(tempRoot, "source.json")
            Dim target = Path.Combine(tempRoot, "out.xml")
            File.WriteAllText(source, "{""broken"":")
            File.WriteAllText(target, "<original/>")

            Try
                Dim result = conversion.ConvertJsonToXml(File.ReadAllText(source))
                Throw New InvalidOperationException("invalid JSON conversion unexpectedly succeeded")
            Catch ex As System.Text.Json.JsonException
            End Try

            AssertEqual("{""broken"":", File.ReadAllText(source), "source unchanged after failed conversion")
            AssertEqual("<original/>", File.ReadAllText(target), "target unchanged after failed conversion")
        Finally
            Directory.Delete(tempRoot, recursive:=True)
        End Try
    End Sub

    <TestMethod(DisplayName:="XML to JSON round trip")>
    Public Sub XmlToJsonRoundTrip()
        Dim conversion = New JsonXmlConversionService()
        Dim result = conversion.ConvertXmlToJson("<root id=""1""><name>A</name><name>B</name></root>")

        AssertContains(result.Output, """@id"": ""1""", "attribute mapped to @id")
        AssertContains(result.Output, """name"": [", "repeated elements become an array")
        AssertContains(result.Output, """A""", "first repeated value")
        AssertContains(result.Output, """B""", "second repeated value")
    End Sub

    <TestMethod(DisplayName:="YAML to JSON round trip")>
    Public Sub YamlToJsonRoundTrip()
        Dim yamlConversion = New JsonYamlConversionService()
        Dim original = "{""name"":""Visual JSON"",""count"":3,""pi"":3.14,""enabled"":true,""missing"":null," &
            """tags"":[""a b"",""002"",""true""]," &
            """nested"":{""list"":[{""id"":1},{""id"":2}],""empty"":{},""none"":[]}}"

        Dim yaml = yamlConversion.ConvertJsonToYaml(original).Output
        Dim roundTripped = yamlConversion.ConvertYamlToJson(yaml).Output

        Using expected = System.Text.Json.JsonDocument.Parse(original)
            Using actual = System.Text.Json.JsonDocument.Parse(roundTripped)
                AssertTrue(System.Text.Json.JsonElement.DeepEquals(expected.RootElement, actual.RootElement), "YAML round trip preserves structure and types")
            End Using
        End Using
    End Sub

    <TestMethod(DisplayName:="YAML unsupported features are rejected")>
    Public Sub YamlUnsupportedFeaturesAreRejected()
        Dim yamlConversion = New JsonYamlConversionService()

        Try
            yamlConversion.ConvertYamlToJson("value: &anchor 1")
            Throw New InvalidOperationException("anchor unexpectedly accepted")
        Catch ex As InvalidOperationException When ex.Message.Contains("Unsupported YAML feature", StringComparison.Ordinal)
        End Try

        Try
            yamlConversion.ConvertYamlToJson("value: {a: 1}")
            Throw New InvalidOperationException("non-empty flow mapping unexpectedly accepted")
        Catch ex As InvalidOperationException When ex.Message.Contains("flow collections", StringComparison.Ordinal)
        End Try
    End Sub

    <TestMethod(DisplayName:="UT-P2-CNV-001 RepeatParentName expands arrays with the property name")>
    Public Sub P2XmlRepeatParentName()
        Dim service = New JsonXmlConversionService()
        Dim options = New XmlConversionOptions With {.ArrayMode = XmlArrayMode.RepeatParentName}

        Dim result = service.ConvertJsonToXml("{""data"":{""tags"":[1,2],""name"":""a""}}", options)
        AssertContains(result.Output, "<tags>1</tags>", "first repeated element")
        AssertContains(result.Output, "<tags>2</tags>", "second repeated element")
        AssertFalse(result.Output.Contains("<item>", StringComparison.Ordinal), "no item wrapper for named arrays")

        Dim rootArray = service.ConvertJsonToXml("[1,2]", options)
        AssertContains(rootArray.Output, "<item>1</item>", "root array still uses item elements")

        Dim nested = service.ConvertJsonToXml("{""data"":{""grid"":[[1],[2]]}}", options)
        AssertContains(nested.Output, "<grid>", "outer array repeats parent name")
        AssertContains(nested.Output, "<item>1</item>", "inner array falls back to item")
    End Sub

    <TestMethod(DisplayName:="UT-P2-CNV-002 XsiNil writes nil attribute and xsi namespace")>
    Public Sub P2XmlXsiNil()
        Dim service = New JsonXmlConversionService()
        Dim options = New XmlConversionOptions With {.NullMode = XmlNullMode.XsiNil}

        Dim result = service.ConvertJsonToXml("{""data"":{""empty"":null,""name"":""a""}}", options)
        AssertContains(result.Output, "xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance""", "xsi namespace on root")
        AssertContains(result.Output, "<empty xsi:nil=""true""", "nil attribute on null element")
    End Sub

    <TestMethod(DisplayName:="UT-P2-CNV-003 default XML options match the legacy output")>
    Public Sub P2XmlDefaultOptionsRegression()
        Dim service = New JsonXmlConversionService()
        Dim json = "{""root"":{""@id"":""1"",""tags"":[1,2],""empty"":null,""name"":""a""}}"

        Dim legacy = service.ConvertJsonToXml(json)
        Dim viaDefault = service.ConvertJsonToXml(json, XmlConversionOptions.CreateDefault())
        AssertEqual(legacy.Output, viaDefault.Output, "default options equal the parameterless overload")
        AssertContains(legacy.Output, "<item>1</item>", "default arrays keep item elements")
        AssertContains(legacy.Output, "<empty></empty>", "default null keeps the empty element")
        AssertFalse(legacy.Output.Contains("xsi", StringComparison.Ordinal), "default output has no xsi declarations")
    End Sub

    <TestMethod(DisplayName:="UT-P2-CNV-004 JSONL save writes one parsable line per element")>
    Public Sub P2JsonLinesArrayRoot()
        Dim parser = New JsonParserService()
        Dim serializer = New JsonLinesSerializer()
        Dim root = parser.Parse("[{""a"":1},{""b"":[1,2]},3]").Root

        Dim result = serializer.Serialize(root, vbLf)
        AssertEqual(0, result.Warnings.Count, "array root has no warnings")

        Dim lines = result.Text.Split(New String() {vbLf}, StringSplitOptions.RemoveEmptyEntries)
        AssertEqual(3, lines.Length, "one line per element")
        For Each line In lines
            Using Text.Json.JsonDocument.Parse(line)
            End Using
            AssertFalse(line.Contains(vbCr, StringComparison.Ordinal), "no stray carriage returns")
        Next

        AssertEqual("{""a"":1}", lines(0), "compact single-line JSON")
        AssertTrue(result.Text.EndsWith(vbLf, StringComparison.Ordinal), "output ends with a newline")
    End Sub

    <TestMethod(DisplayName:="UT-P2-CNV-005 JSONL save of non-array root warns")>
    Public Sub P2JsonLinesNonArrayRoot()
        Dim parser = New JsonParserService()
        Dim serializer = New JsonLinesSerializer()
        Dim root = parser.Parse("{""a"":1}").Root

        Dim result = serializer.Serialize(root, vbLf)
        AssertEqual(1, result.Warnings.Count, "non-array root warns")
        AssertEqual(1, result.Text.Split(New String() {vbLf}, StringSplitOptions.RemoveEmptyEntries).Length, "single line output")
    End Sub

    <TestMethod(DisplayName:="UT-P2-CNV-006 JSONL round trip preserves structure")>
    Public Sub P2JsonLinesRoundTrip()
        Dim parser = New JsonParserService()
        Dim treeSerializer = New JsonTreeSerializer()
        Dim lineSerializer = New JsonLinesSerializer()
        Dim original = "{""a"":1,""nested"":{""x"":[1,2]}}" & vbLf & "{""b"":[1,2]}" & vbLf & "true" & vbLf

        Dim firstTree = parser.Parse(original, JsonInputFormat.JsonLines).Root
        Dim saved = lineSerializer.Serialize(firstTree, vbLf)
        Dim secondTree = parser.Parse(saved.Text, JsonInputFormat.JsonLines).Root

        AssertEqual(treeSerializer.Serialize(firstTree), treeSerializer.Serialize(secondTree), "structure and types survive the round trip")

        ' Grid-sync regression: after editing, the editor shows the pretty-printed
        ' array form; it must still parse as the same JSONL document.
        Dim prettyText = treeSerializer.Serialize(firstTree)
        Dim reparsed = parser.Parse(prettyText, JsonInputFormat.JsonLines).Root
        AssertEqual(treeSerializer.Serialize(firstTree), treeSerializer.Serialize(reparsed), "array-form text parses as the same JSONL document")

        Dim singleArrayLine = parser.Parse("[1,2]", JsonInputFormat.JsonLines).Root
        AssertEqual(1, singleArrayLine.Children.Count, "single array line stays one element")
        AssertEqual(JsonNodeKind.ArrayValue, singleArrayLine.Children(0).Kind, "single array line keeps historical wrapping")
    End Sub
End Class
