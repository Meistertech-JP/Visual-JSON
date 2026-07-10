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
Public Class EditorStateTests

    <TestMethod(DisplayName:="UT-P2-STA-001 capture restore keeps selected and expanded pointers")>
    Public Sub P2StateCaptureRestoreKeepsPointers()
        Dim parser = New JsonParserService()
        Dim stateService = New DocumentStateService()
        Dim root = parser.Parse("{""a"":{""b"":1},""c"":2}").Root
        Dim state = stateService.CreateState("/a/b", {"", "/a"}, "/a", 12, 40.5)
        Dim target = stateService.ResolveRestoreTarget(root, state)

        AssertEqual("/a/b", target.JsonPointer, "selected pointer restored")
        AssertTrue(state.ExpandedPointers.Contains(""), "root expanded pointer retained")
        AssertTrue(state.ExpandedPointers.Contains("/a"), "expanded pointer retained")
    End Sub

    <TestMethod(DisplayName:="UT-P2-STA-002 missing pointer falls back to nearest ancestor")>
    Public Sub P2MissingPointerFallsBackToAncestor()
        Dim parser = New JsonParserService()
        Dim stateService = New DocumentStateService()
        Dim root = parser.Parse("{""a"":{""c"":1},""d"":2}").Root
        Dim target = stateService.ResolveRestoreTarget(root, stateService.CreateState("/a/b/0", Array.Empty(Of String)()))

        AssertEqual("/a", target.JsonPointer, "nearest existing ancestor")

        Dim rootTarget = stateService.ResolveRestoreTarget(root, stateService.CreateState("/missing/value", Array.Empty(Of String)()))
        AssertEqual("", rootTarget.JsonPointer, "root fallback")
    End Sub

    <TestMethod(DisplayName:="UT-P2-STA-003 offset maps to key value and delimiter nodes")>
    Public Sub P2OffsetMapsToNode()
        Dim parser = New JsonParserService()
        Dim stateService = New DocumentStateService()
        Dim text = "{""a"":1,""b"":{""c"":true}}"
        Dim root = parser.Parse(text).Root

        AssertEqual("/b", stateService.GetPointerAtOffset(root, text.IndexOf("""b""", StringComparison.Ordinal)), "key offset maps to property")
        AssertEqual("/b/c", stateService.GetPointerAtOffset(root, text.IndexOf("true", StringComparison.Ordinal)), "value offset maps to nested value")
        AssertEqual("/a", stateService.GetPointerAtOffset(root, text.IndexOf(","c)), "delimiter after value keeps previous node")
    End Sub

    <TestMethod(DisplayName:="UT-P2-STA-004 pointer display uses root label")>
    Public Sub P2PointerDisplayUsesRootLabel()
        Dim parser = New JsonParserService()
        Dim stateService = New DocumentStateService()
        Dim root = parser.Parse("{""a"":[1]}").Root

        AssertEqual("(root)", DocumentStateService.ToPointerDisplay(root.JsonPointer), "root display")
        AssertEqual("/a/0", stateService.GetPointerDisplay(root, "{""a"":[".Length), "nested display")
    End Sub

    <TestMethod(DisplayName:="UT-P2-STA-005 state stores text caret and scroll offsets")>
    Public Sub P2StateStoresTextOffsets()
        Dim stateService = New DocumentStateService()
        Dim state = stateService.CreateState("/a", {"/a"}, "/a", 128, 256.75)

        AssertEqual(128, state.TextCaretOffset, "caret offset")
        AssertEqual(256.75, state.TextScrollOffset, "scroll offset")
    End Sub

    <TestMethod(DisplayName:="UT-P2-STA-006 10MB formatted state restore and offset lookup p95 stays within 200ms")>
    Public Sub P2TenMbFormattedOffsetLookupWithin200Ms()
        Dim parser = New JsonParserService()
        Dim formatter = New JsonFormatterService()
        Dim stateService = New DocumentStateService()
        Dim formatted = formatter.Format(CreateLargeJson(10 * 1024 * 1024))
        Dim root = parser.Parse(formatted).Root
        Dim offsets = {
            formatted.IndexOf("""items""", StringComparison.Ordinal),
            Math.Max(0, formatted.IndexOf("""id"":", StringComparison.Ordinal)),
            Math.Max(0, formatted.LastIndexOf("""payload""", StringComparison.Ordinal))
        }

        Dim selectedNode = stateService.FindNodeAtOffset(root, offsets(offsets.Length - 1))
        AssertTrue(selectedNode IsNot Nothing, "selected node for state restore")
        Dim warmState = stateService.CreateState(selectedNode.JsonPointer, {selectedNode.JsonPointer}, selectedNode.JsonPointer)
        stateService.ResolveRestoreTarget(root, warmState)
        For Each offset In offsets
            stateService.FindNodeAtOffset(root, offset)
        Next

        Dim samples As New List(Of Double)()
        Dim timer = New Stopwatch()
        For iteration = 0 To 19
            timer.Restart()
            Dim state = stateService.CreateState(selectedNode.JsonPointer, {selectedNode.JsonPointer}, selectedNode.JsonPointer)
            Dim restoredNode = stateService.ResolveRestoreTarget(root, state)
            Dim allOffsetsFound = True
            For Each offset In offsets
                Dim node = stateService.FindNodeAtOffset(root, offset)
                allOffsetsFound = allOffsetsFound AndAlso node IsNot Nothing
            Next
            timer.Stop()
            AssertTrue(restoredNode IsNot Nothing, "state restore returns node")
            AssertTrue(allOffsetsFound, "offset lookups return nodes")
            samples.Add(timer.Elapsed.TotalMilliseconds)
        Next

        samples.Sort()
        Dim p95Index = Math.Max(0, CInt(Math.Ceiling(samples.Count * 0.95)) - 1)
        Dim p95 = samples(p95Index)
        AssertTrue(p95 < 200, $"state restore and offset lookup p95 {p95:n1}ms")
    End Sub

    <TestMethod(DisplayName:="UT-P2-FLD-001 folding ignores braces in strings and handles nesting")>
    Public Sub P2FoldingIgnoresStringBracesAndNests()
        Dim folding = New JsonFoldingService()
        Dim text = "{" & Environment.NewLine &
            "  ""text"": ""not { a block } and \""still not\""""," & Environment.NewLine &
            "  ""items"": [" & Environment.NewLine &
            "    { ""id"": 1 }" & Environment.NewLine &
            "  ]" & Environment.NewLine &
            "}"

        Dim ranges = folding.CreateRanges(text)

        AssertEqual(2, ranges.Count, "folding range count")
        AssertEqual(1, ranges(0).StartLine, "root starts on line 1")
        AssertEqual(6, ranges(0).EndLine, "root ends on line 6")
        AssertTrue(ranges.Any(Function(item) item.StartLine = 3 AndAlso item.EndLine = 5), "array range")
        Dim stringStart = text.IndexOf("not { a block", StringComparison.Ordinal)
        Dim stringEnd = text.IndexOf("""items""", StringComparison.Ordinal)
        AssertFalse(ranges.Any(Function(item) item.StartIndex > stringStart AndAlso item.StartIndex < stringEnd), "string braces ignored")
    End Sub

    <TestMethod(DisplayName:="UT-P2-FLD-002 single line blocks are not foldable")>
    Public Sub P2SingleLineBlocksAreNotFoldable()
        Dim folding = New JsonFoldingService()
        Dim ranges = folding.CreateRanges("{""a"":{""b"":1},""c"":[1,2]}")

        AssertEqual(0, ranges.Count, "single-line blocks are not foldable")
    End Sub

    <TestMethod(DisplayName:="UT-P2-RPL-001 replace all supports case and regex options")>
    Public Sub P2ReplaceAllSupportsCaseAndRegex()
        Dim service = New SearchReplaceService()
        Dim insensitive = service.ReplaceAll("old OLD old", "old", "new", New SearchOptions With {.MatchCase = False})
        AssertEqual(3, insensitive.Count, "case-insensitive count")
        AssertEqual("new new new", insensitive.Text, "case-insensitive result")

        Dim sensitive = service.ReplaceAll("old OLD old", "old", "new", New SearchOptions With {.MatchCase = True})
        AssertEqual(2, sensitive.Count, "case-sensitive count")
        AssertEqual("new OLD new", sensitive.Text, "case-sensitive result")

        Dim regex = service.ReplaceAll("""id"": 12, ""id"": 34", """id"":\s*(\d+)", """id"": ""#$1""", New SearchOptions With {.UseRegex = True, .MatchCase = True})
        AssertEqual(2, regex.Count, "regex count")
        AssertEqual("""id"": ""#12"", ""id"": ""#34""", regex.Text, "regex replacement")
    End Sub

    <TestMethod(DisplayName:="UT-P2-CMP-001 completion candidates merge siblings and schema minus existing")>
    Public Sub P2CompletionCandidates()
        Dim parser = New JsonParserService()
        Dim service = New CompletionCandidateService()
        Dim root = parser.Parse("[{""id"":1,""name"":""a""},{""id"":2,""email"":""e""}]").Root
        Dim target = root.Children(1)
        Dim schema = "{""items"":{""$ref"":""#/definitions/rec""},""definitions"":{""rec"":{""properties"":{""id"":{},""name"":{},""active"":{}}}}}"

        Dim candidates = service.GetKeyCandidates(root, target, schema)
        AssertEqual(2, candidates.Count, "candidate count")
        AssertEqual("active", candidates(0), "schema-only property included")
        AssertEqual("name", candidates(1), "sibling key included once")

        Dim withoutSchema = service.GetKeyCandidates(root, target, Nothing)
        AssertEqual(1, withoutSchema.Count, "sibling keys only without schema")
        AssertEqual("name", withoutSchema(0), "sibling candidate")

        AssertEqual(0, service.GetKeyCandidates(root, root, schema).Count, "non-object target yields nothing")
    End Sub
End Class
