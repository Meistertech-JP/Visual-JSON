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
Public Class ParsingTests

    <TestMethod(DisplayName:="Parse standard JSON to tree")>
    Public Sub ParseStandardJsonToTree()
        Dim parser = New JsonParserService()
        Dim result = parser.Parse("{""name"":""Visual"",""items"":[1,true,null]}")
        AssertEqual(JsonNodeKind.ObjectValue, result.Root.Kind, "root kind")
        AssertEqual(2, result.Root.Children.Count, "root child count")
        AssertEqual("name", result.Root.Children(0).Key, "first key")
        AssertEqual(JsonNodeKind.ArrayValue, result.Root.Children(1).Kind, "items kind")
        AssertEqual(3, result.Root.Children(1).Children.Count, "array child count")
    End Sub

    <TestMethod(DisplayName:="Attach source line and column to nodes")>
    Public Sub AttachSourceLineAndColumnToNodes()
        Dim parser = New JsonParserService()
        Dim text = "{" & Environment.NewLine & "  ""a"": 1," & Environment.NewLine & "  ""b"": { ""c"": true }" & Environment.NewLine & "}"
        Dim root = parser.Parse(text).Root
        Dim aNode = root.Children(0)
        Dim bNode = root.Children(1)
        Dim cNode = bNode.Children(0)

        AssertEqual(2, aNode.SourceLine.Value, "a line")
        AssertTrue(aNode.SourceColumn.HasValue AndAlso aNode.SourceColumn.Value > 1, "a column")
        AssertEqual(3, bNode.SourceLine.Value, "b line")
        AssertEqual(3, cNode.SourceLine.Value, "c line")
    End Sub

    <TestMethod(DisplayName:="Parse JSONC with comments and trailing commas")>
    Public Sub ParseJsonCWithCommentsAndTrailingCommas()
        Dim parser = New JsonParserService()
        Dim validator = New SyntaxValidationService()
        Dim text = "{" & Environment.NewLine &
            "  // friendly comment" & Environment.NewLine &
            "  ""name"": ""Visual""," & Environment.NewLine &
            "  ""items"": [1, 2,]," & Environment.NewLine &
            "}"

        Dim diagnostics = validator.Validate(text, JsonInputFormat.JsonC)
        Dim root = parser.Parse(text, JsonInputFormat.JsonC).Root

        AssertEqual(0, diagnostics.Count, "jsonc diagnostics")
        AssertEqual("name", root.Children(0).Key, "jsonc first key")
        AssertEqual(2, root.Children(1).Children.Count, "jsonc array count")
    End Sub

    <TestMethod(DisplayName:="Parse representative JSON5 syntax")>
    Public Sub ParseRepresentativeJson5Syntax()
        Dim parser = New JsonParserService()
        Dim validator = New SyntaxValidationService()
        Dim text = "{" & Environment.NewLine &
            "  name: 'Visual JSON'," & Environment.NewLine &
            "  nested: { enabled: true, }," & Environment.NewLine &
            "}"

        Dim diagnostics = validator.Validate(text, JsonInputFormat.Json5)
        Dim root = parser.Parse(text, JsonInputFormat.Json5).Root

        AssertEqual(0, diagnostics.Count, "json5 diagnostics")
        AssertEqual("Visual JSON", root.Children(0).ValueText, "json5 single quoted string")
        AssertEqual(JsonNodeKind.BooleanValue, root.Children(1).Children(0).Kind, "json5 boolean kind")
    End Sub

    <TestMethod(DisplayName:="Parse JSON Lines as array tree")>
    Public Sub ParseJsonLinesAsArrayTree()
        Dim parser = New JsonParserService()
        Dim text = "{""id"":1,""name"":""one""}" & Environment.NewLine &
            "{""id"":2,""name"":""two""}" & Environment.NewLine

        Dim root = parser.Parse(text, JsonInputFormat.JsonLines).Root

        AssertEqual(JsonNodeKind.ArrayValue, root.Kind, "jsonl root kind")
        AssertEqual(2, root.Children.Count, "jsonl row count")
        AssertEqual("two", root.Children(1).Children(1).ValueText, "jsonl second row value")
    End Sub

    <TestMethod(DisplayName:="Report syntax errors with location")>
    Public Sub ReportSyntaxErrorsWithLocation()
        Dim validator = New SyntaxValidationService()
        Dim diagnostics = validator.Validate("{" & Environment.NewLine & "  ""a"": 1")
        AssertEqual(1, diagnostics.Count, "diagnostic count")
        AssertTrue(diagnostics(0).Line.HasValue, "line should be present")
        AssertTrue(diagnostics(0).Column.HasValue, "column should be present")
    End Sub

    <TestMethod(DisplayName:="Parse 10MB target JSON within practical bounds")>
    Public Sub Parse10MbTargetJsonWithinPracticalBounds()
        Dim json = CreateLargeJson(10 * 1024 * 1024)
        Dim parser = New JsonParserService()
        Dim timer = Stopwatch.StartNew()
        Dim root = parser.Parse(json).Root
        timer.Stop()

        Dim nodeCount = New TreeStatisticsService().CountNodes(root)
        AssertTrue(json.Length >= 10 * 1024 * 1024, "generated size")
        AssertTrue(nodeCount > 1000, "node count")
        AssertTrue(timer.Elapsed < TimeSpan.FromSeconds(20), $"10MB parse time was {timer.Elapsed}")
    End Sub

    <TestMethod(DisplayName:="Nodes carry RFC6901 JSON Pointers")>
    Public Sub NodesCarryJsonPointers()
        Dim parser = New JsonParserService()
        Dim root = parser.Parse("{""a"":{""b/c"":[10,20]},""m~n"":true}").Root

        AssertEqual("", root.JsonPointer, "root pointer")
        AssertEqual("(root)", root.PointerDisplay, "root pointer display")
        AssertEqual("/a", root.Children(0).JsonPointer, "child pointer")
        AssertEqual("/a/b~1c", root.Children(0).Children(0).JsonPointer, "escaped slash pointer")
        AssertEqual("/a/b~1c/1", root.Children(0).Children(0).Children(1).JsonPointer, "array pointer")
        AssertEqual("/m~0n", root.Children(1).JsonPointer, "escaped tilde pointer")
    End Sub

    <TestMethod(DisplayName:="UT-P2-FMT-001 format sniffer detects JSONC JSON5 JSONL")>
    Public Sub P2FormatSnifferDetectsVariants()
        Dim sniffer = New FormatSniffer()

        AssertEqual(JsonInputFormat.JsonC, sniffer.Sniff("// comment" & Environment.NewLine & "{""a"":1}").Format, "jsonc")
        AssertEqual(JsonInputFormat.Json5, sniffer.Sniff("{name:'a'}").Format, "json5")
        AssertEqual(JsonInputFormat.JsonLines, sniffer.Sniff("{""a"":1}" & Environment.NewLine & "{""b"":2}").Format, "jsonl")
    End Sub

    <TestMethod(DisplayName:="UT-P2-FMT-002 unknown format is not confident")>
    Public Sub P2FormatSnifferUnknownIsNotConfident()
        Dim sniffer = New FormatSniffer()
        Dim result = sniffer.Sniff("not json")

        AssertFalse(result.Confident, "unknown not confident")
    End Sub
End Class
