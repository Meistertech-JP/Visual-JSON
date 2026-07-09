' SPDX-License-Identifier: MPL-2.0
Imports System.IO
Imports System.Diagnostics
Imports System.Text
Imports VisualJson.Core.Conversion
Imports VisualJson.Core.Infrastructure
Imports VisualJson.Core.Models
Imports VisualJson.Core.Parsing
Imports VisualJson.Core.Serialization
Imports VisualJson.Core.Services
Imports VisualJson.Core.Validation

Module Program
    Private _failures As Integer

    Sub Main()
        Run("Parse standard JSON to tree", AddressOf ParseStandardJsonToTree)
        Run("Attach source line and column to nodes", AddressOf AttachSourceLineAndColumnToNodes)
        Run("Infer primitive values and serialize grid", AddressOf InferPrimitiveValuesAndSerializeGrid)
        Run("Infer primitive values by MVP-0 rules", AddressOf InferPrimitiveValuesByMvp0Rules)
        Run("Serialize edited grid key", AddressOf SerializeEditedGridKey)
        Run("Parse JSONC with comments and trailing commas", AddressOf ParseJsonCWithCommentsAndTrailingCommas)
        Run("Parse representative JSON5 syntax", AddressOf ParseRepresentativeJson5Syntax)
        Run("Parse JSON Lines as array tree", AddressOf ParseJsonLinesAsArrayTree)
        Run("Grid actions add delete move and change type", AddressOf GridActionsAddDeleteMoveAndChangeType)
        Run("Grid undo restores moved node order", AddressOf GridUndoRestoresMovedNodeOrder)
        Run("Grid filter keeps matching nodes and parents", AddressOf GridFilterKeepsMatchingNodesAndParents)
        Run("Report syntax errors with location", AddressOf ReportSyntaxErrorsWithLocation)
        Run("Save creates backup and keeps valid JSON", AddressOf SaveCreatesBackupAndKeepsValidJson)
        Run("Invalid save preserves existing file", AddressOf InvalidSavePreservesExistingFile)
        Run("Recovery snapshots can be listed and loaded", AddressOf RecoverySnapshotsCanBeListedAndLoaded)
        Run("Diagnostics report omits JSON body", AddressOf DiagnosticsReportOmitsJsonBody)
        Run("Parse 10MB target JSON within practical bounds", AddressOf Parse10MbTargetJsonWithinPracticalBounds)
        Run("Nodes carry RFC6901 JSON Pointers", AddressOf NodesCarryJsonPointers)
        Run("Grid value edit snapshot becomes one undo operation", AddressOf GridValueEditSnapshotBecomesOneUndoOperation)
        Run("Diagnostics report omits diagnostic and exception messages", AddressOf DiagnosticsReportOmitsMessages)
        Run("UT-M2-001 schema required error", AddressOf SchemaRequiredError)
        Run("UT-M2-002 schema type error with body position", AddressOf SchemaTypeErrorWithBodyPosition)
        Run("UT-M2-003 schema pattern error", AddressOf SchemaPatternError)
        Run("Schema enum range additionalProperties and items", AddressOf SchemaEnumRangeAdditionalAndItems)
        Run("UT-M2-004 HTTP schema URL is blocked", AddressOf HttpSchemaUrlIsBlocked)
        Run("UT-M2-005 dangerous schema redirects are blocked", AddressOf DangerousSchemaRedirectsAreBlocked)
        Run("DNS-resolved private and mapped addresses are blocked", AddressOf DnsResolvedPrivateAddressesAreBlocked)
        Run("IT-M2-001 schema diagnostics map to body location", AddressOf SchemaDiagnosticsMapToBodyLocation)
        Run("UT-M3-001 JSON to XML with attributes", AddressOf JsonToXmlWithAttributes)
        Run("UT-M3-002 JSON to YAML two space list", AddressOf JsonToYamlTwoSpaceList)
        Run("UT-M3-003 XXE is not resolved", AddressOf XxeIsNotResolved)
        Run("IT-M3-001 failed conversion never touches files", AddressOf FailedConversionNeverTouchesFiles)
        Run("XML to JSON round trip", AddressOf XmlToJsonRoundTrip)
        Run("YAML to JSON round trip", AddressOf YamlToJsonRoundTrip)
        Run("YAML unsupported features are rejected", AddressOf YamlUnsupportedFeaturesAreRejected)
        Run("Text export creates backup and preserves target on failure", AddressOf TextExportCreatesBackupAndPreservesTarget)
        Run("UT-P2-STA-001 capture restore keeps selected and expanded pointers", AddressOf P2StateCaptureRestoreKeepsPointers)
        Run("UT-P2-STA-002 missing pointer falls back to nearest ancestor", AddressOf P2MissingPointerFallsBackToAncestor)
        Run("UT-P2-STA-003 offset maps to key value and delimiter nodes", AddressOf P2OffsetMapsToNode)
        Run("UT-P2-STA-004 pointer display uses root label", AddressOf P2PointerDisplayUsesRootLabel)
        Run("UT-P2-STA-005 state stores text caret and scroll offsets", AddressOf P2StateStoresTextOffsets)
        Run("UT-P2-STA-006 10MB formatted offset lookup stays within 200ms", AddressOf P2TenMbFormattedOffsetLookupWithin200Ms)
        Run("UT-P2-FLD-001 folding ignores braces in strings and handles nesting", AddressOf P2FoldingIgnoresStringBracesAndNests)
        Run("UT-P2-FLD-002 single line blocks are not foldable", AddressOf P2SingleLineBlocksAreNotFoldable)
        Run("UT-P2-RPL-001 replace all supports case and regex options", AddressOf P2ReplaceAllSupportsCaseAndRegex)
        Run("UT-P2-GRD-001 Redo restores moved node order", AddressOf P2GridRedoRestoresMovedNodeOrder)
        Run("UT-P2-GRD-002 New grid operation clears Redo", AddressOf P2GridRedoClearedByNewOperation)
        Run("UT-P2-GRD-003 Duplicate creates unique sibling", AddressOf P2GridDuplicateCreatesUniqueSibling)
        Run("UT-P2-SET-001 settings round trip", AddressOf P2SettingsRoundTrip)
        Run("UT-P2-SET-002 broken settings are moved aside", AddressOf P2BrokenSettingsMovedAside)
        Run("UT-P2-SET-003 unknown settings keys are preserved", AddressOf P2UnknownSettingsKeysPreserved)
        Run("UT-P2-SET-004 recent files max duplicate clear", AddressOf P2RecentFilesMaxDuplicateClear)
        Run("UT-P2-ENC-001 encoding samples are detected", AddressOf P2EncodingSamplesDetected)
        Run("UT-P2-ENC-002 save preserves detected encoding", AddressOf P2SavePreservesDetectedEncoding)
        Run("UT-P2-ENC-003 newline majority is preserved", AddressOf P2NewLineMajorityDetected)
        Run("UT-P2-ENC-004 undecodable bytes fall back to UTF-8 warning", AddressOf P2EncodingFallbackWarning)
        Run("UT-P2-FMT-001 format sniffer detects JSONC JSON5 JSONL", AddressOf P2FormatSnifferDetectsVariants)
        Run("UT-P2-FMT-002 unknown format is not confident", AddressOf P2FormatSnifferUnknownIsNotConfident)
        Run("File log omits exception messages and body", AddressOf P2FileLogOmitsBody)
        Run("UT-P2-SCH-001 const mismatch reports SCH-CONST", AddressOf P2SchemaConstMismatch)
        Run("UT-P2-SCH-002 string length violations report min and max codes", AddressOf P2SchemaStringLength)
        Run("UT-P2-SCH-003 local ref delegates to definitions", AddressOf P2SchemaLocalRef)
        Run("UT-P2-SCH-004 ref cycle stops without recursion", AddressOf P2SchemaRefCycle)
        Run("UT-P2-SCH-005 external ref warns without network access", AddressOf P2SchemaExternalRefWarning)
        Run("UT-P2-SCH-006 format violations warn", AddressOf P2SchemaFormatWarnings)
        Run("UT-P2-CNV-001 RepeatParentName expands arrays with the property name", AddressOf P2XmlRepeatParentName)
        Run("UT-P2-CNV-002 XsiNil writes nil attribute and xsi namespace", AddressOf P2XmlXsiNil)
        Run("UT-P2-CNV-003 default XML options match the legacy output", AddressOf P2XmlDefaultOptionsRegression)
        Run("UT-P2-CNV-004 JSONL save writes one parsable line per element", AddressOf P2JsonLinesArrayRoot)
        Run("UT-P2-CNV-005 JSONL save of non-array root warns", AddressOf P2JsonLinesNonArrayRoot)
        Run("UT-P2-CNV-006 JSONL round trip preserves structure", AddressOf P2JsonLinesRoundTrip)
        Run("UT-P2-CMP-001 completion candidates merge siblings and schema minus existing", AddressOf P2CompletionCandidates)
        Run("UT-P2-GRD-004 cross-parent move allows confirms and rejects", AddressOf P2GridCrossParentMove)
        Run("UT-P2-GRD-005 empty braces and brackets infer container types", AddressOf P2GridInferContainerTypes)
        Run("UT-P2-TBL-001 table columns are first-seen union with empty missing cells", AddressOf P2TableColumnsUnionWithMissingCells)
        Run("UT-P2-TBL-002 non-object elements land in the value column", AddressOf P2TableNonObjectElementsUseValueColumn)
        Run("UT-P2-TBL-003 missing cell edit materializes the property on that row only", AddressOf P2TableMissingCellEditMaterializesRowOnly)
        Run("UT-P2-TBL-004 cell edit infers type and undoes as one operation", AddressOf P2TableCellEditInfersTypeAndUndoes)
        Run("UT-P2-TBL-005 display sort leaves array children order unchanged", AddressOf P2TableDisplaySortLeavesStructure)
        Run("UT-P2-TBL-006 apply sort rewrites array order and undoes as one operation", AddressOf P2TableApplySortRewritesStructure)
        Run("UT-P2-TBL-007 10001 rows exceed the table limit", AddressOf P2TableRowLimitExceeded)
        Run("UT-P2-TBL-008 10000 rows build within two seconds", AddressOf P2TableTenThousandRowsBuildFast)

        If _failures > 0 Then
            Console.Error.WriteLine($"{_failures} test(s) failed.")
            Environment.ExitCode = 1
            Return
        End If

        Console.WriteLine("All tests passed.")
    End Sub

    Private Sub Run(name As String, test As Action)
        Try
            test()
            Console.WriteLine($"PASS {name}")
        Catch ex As Exception
            _failures += 1
            Console.Error.WriteLine($"FAIL {name}: {ex.Message}")
        End Try
    End Sub

    Private Sub ParseStandardJsonToTree()
        Dim parser = New JsonParserService()
        Dim result = parser.Parse("{""name"":""Visual"",""items"":[1,true,null]}")
        AssertEqual(JsonNodeKind.ObjectValue, result.Root.Kind, "root kind")
        AssertEqual(2, result.Root.Children.Count, "root child count")
        AssertEqual("name", result.Root.Children(0).Key, "first key")
        AssertEqual(JsonNodeKind.ArrayValue, result.Root.Children(1).Kind, "items kind")
        AssertEqual(3, result.Root.Children(1).Children.Count, "array child count")
    End Sub

    Private Sub AttachSourceLineAndColumnToNodes()
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

    Private Sub InferPrimitiveValuesAndSerializeGrid()
        Dim parser = New JsonParserService()
        Dim typeInference = New TypeInferenceService()
        Dim serializer = New JsonTreeSerializer()
        Dim root = parser.Parse("{""count"":1,""enabled"":false,""label"":""old""}").Root

        Dim countNode = root.Children(0)
        countNode.ValueText = "42"
        typeInference.ApplyToNode(countNode)

        Dim enabledNode = root.Children(1)
        enabledNode.ValueText = "true"
        typeInference.ApplyToNode(enabledNode)

        Dim labelNode = root.Children(2)
        labelNode.ValueText = """new"""
        typeInference.ApplyToNode(labelNode)

        Dim serialized = serializer.Serialize(root)
        AssertContains(serialized, """count"": 42", "number serialization")
        AssertContains(serialized, """enabled"": true", "boolean serialization")
        AssertContains(serialized, """label"": ""new""", "string serialization")
    End Sub

    Private Sub InferPrimitiveValuesByMvp0Rules()
        Dim inference = New TypeInferenceService()

        AssertEqual(JsonNodeKind.StringValue, inference.NormalizePrimitiveInput("""123""").Kind, "quoted number kind")
        AssertEqual("123", inference.NormalizePrimitiveInput("""123""").ValueText, "quoted number value")
        AssertEqual(JsonNodeKind.NumberValue, inference.NormalizePrimitiveInput("123").Kind, "number kind")
        AssertEqual(JsonNodeKind.StringValue, inference.NormalizePrimitiveInput("001").Kind, "leading zero string")
        AssertEqual(JsonNodeKind.BooleanValue, inference.NormalizePrimitiveInput("true").Kind, "boolean")
        AssertEqual(JsonNodeKind.NullValue, inference.NormalizePrimitiveInput("null").Kind, "null")
        AssertEqual(JsonNodeKind.StringValue, inference.NormalizePrimitiveInput("NaN").Kind, "NaN string")
    End Sub

    Private Sub SerializeEditedGridKey()
        Dim parser = New JsonParserService()
        Dim serializer = New JsonTreeSerializer()
        Dim root = parser.Parse("{""oldKey"":1}").Root
        root.Children(0).Key = "newKey"
        Dim serialized = serializer.Serialize(root)
        AssertContains(serialized, """newKey"": 1", "new key")
        AssertFalse(serialized.Contains("oldKey", StringComparison.Ordinal), "old key should not remain")
    End Sub

    Private Sub ParseJsonCWithCommentsAndTrailingCommas()
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

    Private Sub ParseRepresentativeJson5Syntax()
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

    Private Sub ParseJsonLinesAsArrayTree()
        Dim parser = New JsonParserService()
        Dim text = "{""id"":1,""name"":""one""}" & Environment.NewLine &
            "{""id"":2,""name"":""two""}" & Environment.NewLine

        Dim root = parser.Parse(text, JsonInputFormat.JsonLines).Root

        AssertEqual(JsonNodeKind.ArrayValue, root.Kind, "jsonl root kind")
        AssertEqual(2, root.Children.Count, "jsonl row count")
        AssertEqual("two", root.Children(1).Children(1).ValueText, "jsonl second row value")
    End Sub

    Private Sub GridActionsAddDeleteMoveAndChangeType()
        Dim parser = New JsonParserService()
        Dim serializer = New JsonTreeSerializer()
        Dim ops = New GridOperationService()
        Dim root = parser.Parse("{""first"":1,""second"":2}").Root

        Dim child = ops.AddChild(root)
        child.Key = "third"
        child.ValueText = "3"
        ops.ChangeType(child, JsonNodeKind.NumberValue)

        Dim sibling = ops.AddSibling(root, root.Children(0))
        sibling.Key = "middle"
        ops.ChangeType(sibling, JsonNodeKind.BooleanValue)
        sibling.ValueText = "true"

        AssertTrue(ops.MoveDown(root, root.Children(0)), "move down should succeed")
        AssertTrue(ops.Delete(root, root.Children(root.Children.Count - 1)), "delete should succeed")

        Dim serialized = serializer.Serialize(root)
        AssertContains(serialized, """middle"": true", "added sibling")
        AssertFalse(serialized.Contains("""third"":", StringComparison.Ordinal), "deleted child")
    End Sub

    Private Sub GridUndoRestoresMovedNodeOrder()
        Dim parser = New JsonParserService()
        Dim serializer = New JsonTreeSerializer()
        Dim ops = New GridOperationService()
        Dim undo = New GridUndoService()
        Dim root = parser.Parse("{""a"":1,""b"":2,""c"":3}").Root

        undo.Capture(root)
        AssertTrue(ops.MoveBefore(root, root.Children(2), root.Children(0)), "move before should succeed")

        Dim moved = serializer.Serialize(root)
        AssertTrue(moved.IndexOf("""c""", StringComparison.Ordinal) < moved.IndexOf("""a""", StringComparison.Ordinal), "moved order")

        Dim restored = undo.Undo()
        Dim restoredText = serializer.Serialize(restored)
        AssertTrue(restoredText.IndexOf("""a""", StringComparison.Ordinal) < restoredText.IndexOf("""b""", StringComparison.Ordinal), "restored a before b")
        AssertTrue(restoredText.IndexOf("""b""", StringComparison.Ordinal) < restoredText.IndexOf("""c""", StringComparison.Ordinal), "restored b before c")
    End Sub

    Private Sub GridFilterKeepsMatchingNodesAndParents()
        Dim parser = New JsonParserService()
        Dim filter = New GridFilterService()
        Dim root = parser.Parse("{""users"":[{""name"":""Alice""},{""name"":""Bob""}],""meta"":{""count"":2}}").Root

        Dim filtered = filter.Filter(root, "Bob")

        AssertEqual("$", filtered.Key, "filtered root key")
        AssertEqual(1, filtered.Children.Count, "filtered root child count")
        AssertEqual("users", filtered.Children(0).Key, "parent array retained")
        AssertEqual(1, filtered.Children(0).Children.Count, "only matching row retained")
        AssertEqual("Bob", filtered.Children(0).Children(0).Children(0).ValueText, "matching value retained")
    End Sub

    Private Sub ReportSyntaxErrorsWithLocation()
        Dim validator = New SyntaxValidationService()
        Dim diagnostics = validator.Validate("{" & Environment.NewLine & "  ""a"": 1")
        AssertEqual(1, diagnostics.Count, "diagnostic count")
        AssertTrue(diagnostics(0).Line.HasValue, "line should be present")
        AssertTrue(diagnostics(0).Column.HasValue, "column should be present")
    End Sub

    Private Sub SaveCreatesBackupAndKeepsValidJson()
        Dim tempRoot = CreateTempDirectory()
        Try
            Dim target = Path.Combine(tempRoot, "sample.json")
            File.WriteAllText(target, "{""old"":true}")

            Dim saver = New FileSaveService()
            Dim result = saver.Save(target, "{""old"":false,""items"":[1,2]}")

            AssertTrue(File.Exists(result.Path), "saved file exists")
            AssertTrue(File.Exists(result.BackupPath), "backup file exists")
            AssertContains(File.ReadAllText(target), """old"": false", "saved body")
            AssertContains(File.ReadAllText(result.BackupPath), """old"":true", "backup body")
        Finally
            Directory.Delete(tempRoot, recursive:=True)
        End Try
    End Sub

    Private Sub InvalidSavePreservesExistingFile()
        Dim tempRoot = CreateTempDirectory()
        Try
            Dim target = Path.Combine(tempRoot, "sample.json")
            File.WriteAllText(target, "{""old"":true}")

            Dim saver = New FileSaveService()
            Try
                saver.Save(target, "{""broken"":")
                Throw New InvalidOperationException("invalid save unexpectedly succeeded")
            Catch ex As System.Text.Json.JsonException
            End Try

            AssertEqual("{""old"":true}", File.ReadAllText(target), "existing file after invalid save")
        Finally
            Directory.Delete(tempRoot, recursive:=True)
        End Try
    End Sub

    Private Sub RecoverySnapshotsCanBeListedAndLoaded()
        Dim tempRoot = CreateTempDirectory()
        Try
            Dim recovery = New RecoveryService(tempRoot)
            Dim candidate = recovery.CreateSnapshot("sample.json", "{""draft"":true}")
            Dim candidates = recovery.ListCandidates()

            AssertEqual(1, candidates.Count, "candidate count")
            AssertEqual(candidate.DisplayName, candidates(0).DisplayName, "candidate name")
            AssertEqual("{""draft"":true}", recovery.Load(candidates(0)), "candidate content")

            recovery.Delete(candidates(0))
            AssertEqual(0, recovery.ListCandidates().Count, "candidate deleted")
        Finally
            Directory.Delete(tempRoot, recursive:=True)
        End Try
    End Sub

    Private Sub DiagnosticsReportOmitsJsonBody()
        Dim reportService = New DiagnosticsReportService()
        Dim validator = New SyntaxValidationService()
        Dim diagnostics = validator.Validate("{""secret"":")
        Dim report = reportService.CreateReport("test", "secret.json", 10, diagnostics, Nothing, 0, "WPF")

        AssertContains(report, "JsonBodyIncluded: no", "body flag")
        AssertFalse(report.Contains("""secret"":", StringComparison.Ordinal), "body content should not be included")
        AssertContains(report, "UIStack: WPF", "ui stack")
        AssertContains(report, "ProcessMemoryBytes:", "memory")
    End Sub

    Private Sub Parse10MbTargetJsonWithinPracticalBounds()
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

    Private Sub NodesCarryJsonPointers()
        Dim parser = New JsonParserService()
        Dim root = parser.Parse("{""a"":{""b/c"":[10,20]},""m~n"":true}").Root

        AssertEqual("", root.JsonPointer, "root pointer")
        AssertEqual("(root)", root.PointerDisplay, "root pointer display")
        AssertEqual("/a", root.Children(0).JsonPointer, "child pointer")
        AssertEqual("/a/b~1c", root.Children(0).Children(0).JsonPointer, "escaped slash pointer")
        AssertEqual("/a/b~1c/1", root.Children(0).Children(0).Children(1).JsonPointer, "array pointer")
        AssertEqual("/m~0n", root.Children(1).JsonPointer, "escaped tilde pointer")
    End Sub

    Private Sub GridValueEditSnapshotBecomesOneUndoOperation()
        Dim parser = New JsonParserService()
        Dim serializer = New JsonTreeSerializer()
        Dim undo = New GridUndoService()
        Dim root = parser.Parse("{""a"":1}").Root

        Dim preEditSnapshot = serializer.Serialize(root)
        root.Children(0).ValueText = "999"
        undo.PushSnapshot(preEditSnapshot)

        AssertTrue(undo.CanUndo(), "undo should be available after a committed value edit")
        Dim restored = undo.Undo()
        AssertContains(serializer.Serialize(restored), """a"": 1", "restored original value")
        AssertFalse(undo.CanUndo(), "single edit is one undo operation")
    End Sub

    Private Sub DiagnosticsReportOmitsMessages()
        Dim reportService = New DiagnosticsReportService()
        Dim validator = New SyntaxValidationService()
        Dim diagnostics = validator.Validate("{""topsecretvalue"":")
        Dim exception = New InvalidOperationException("contains topsecretvalue body fragment")
        Dim report = reportService.CreateReport("test", "secret.json", 10, diagnostics, exception, 0, "WPF")

        AssertFalse(report.Contains("topsecretvalue", StringComparison.OrdinalIgnoreCase), "no body fragments via messages")
        AssertContains(report, "LastExceptionType: System.InvalidOperationException", "exception type retained")
        AssertContains(report, "FirstDiagnostic: Error SYN-PARSE", "diagnostic code retained without message")
        AssertContains(report, "JsonBodyIncluded: no", "body flag")
    End Sub

    Private Sub SchemaRequiredError()
        Dim schemaValidation = New SchemaValidationService()
        Dim schema = "{""type"":""object"",""required"":[""name""]}"
        Dim diagnostics = schemaValidation.Validate("{}", schema, "local-schema.json")

        AssertEqual(1, diagnostics.Count, "required diagnostic count")
        AssertEqual("SCH-REQUIRED", diagnostics(0).ErrorCode, "required code")
        AssertEqual("#/required", diagnostics(0).SchemaPath, "required schema path")
        AssertEqual("local-schema.json", diagnostics(0).SchemaUri, "schema uri")
    End Sub

    Private Sub SchemaTypeErrorWithBodyPosition()
        Dim parser = New JsonParserService()
        Dim schemaValidation = New SchemaValidationService()
        Dim instance = "{" & Environment.NewLine & "  ""count"": ""abc""" & Environment.NewLine & "}"
        Dim schema = "{""type"":""object"",""properties"":{""count"":{""type"":""integer""}}}"
        Dim root = parser.Parse(instance).Root
        Dim diagnostics = schemaValidation.Validate(instance, schema, "", root)

        AssertEqual(1, diagnostics.Count, "type diagnostic count")
        AssertEqual("SCH-TYPE", diagnostics(0).ErrorCode, "type code")
        AssertEqual("/count", diagnostics(0).JsonPointer, "type pointer")
        AssertEqual("$.count", diagnostics(0).JsonPath, "type json path")
        AssertEqual(2, diagnostics(0).Line.Value, "type body line")
        AssertContains(diagnostics(0).SchemaPath, "#/properties/count/type", "type schema path")
    End Sub

    Private Sub SchemaPatternError()
        Dim schemaValidation = New SchemaValidationService()
        Dim schema = "{""type"":""object"",""properties"":{""code"":{""type"":""string"",""pattern"":""^[A-Z]+$""}}}"
        Dim diagnostics = schemaValidation.Validate("{""code"":""abc""}", schema)

        AssertEqual(1, diagnostics.Count, "pattern diagnostic count")
        AssertEqual("SCH-PATTERN", diagnostics(0).ErrorCode, "pattern code")
    End Sub

    Private Sub SchemaEnumRangeAdditionalAndItems()
        Dim schemaValidation = New SchemaValidationService()
        Dim schema = "{""type"":""object""," &
            """properties"":{" &
            """color"":{""enum"":[""red"",""blue""]}," &
            """age"":{""type"":""integer"",""minimum"":0,""maximum"":150}," &
            """tags"":{""type"":""array"",""items"":{""type"":""string""}}}," &
            """additionalProperties"":false}"
        Dim instance = "{""color"":""green"",""age"":200,""tags"":[""ok"",5],""extra"":1}"
        Dim diagnostics = schemaValidation.Validate(instance, schema)

        AssertTrue(diagnostics.Any(Function(item) item.ErrorCode = "SCH-ENUM"), "enum error")
        AssertTrue(diagnostics.Any(Function(item) item.ErrorCode = "SCH-MAXIMUM"), "maximum error")
        AssertTrue(diagnostics.Any(Function(item) item.ErrorCode = "SCH-TYPE" AndAlso item.JsonPointer = "/tags/1"), "items type error")
        AssertTrue(diagnostics.Any(Function(item) item.ErrorCode = "SCH-ADDITIONAL" AndAlso item.JsonPointer = "/extra"), "additional properties error")

        Dim belowMinimum = schemaValidation.Validate("{""age"":-1}", schema)
        AssertTrue(belowMinimum.Any(Function(item) item.ErrorCode = "SCH-MINIMUM"), "minimum error")
    End Sub

    Private Sub HttpSchemaUrlIsBlocked()
        AssertTrue(SchemaUrlPolicy.ValidateInitialUrl("https://example.com/schema.json", allowExternal:=False) IsNot Nothing, "external references are OFF by default")
        AssertTrue(SchemaUrlPolicy.ValidateInitialUrl("http://example.com/schema.json", allowExternal:=True) IsNot Nothing, "http is blocked even when external is allowed")
        AssertTrue(SchemaUrlPolicy.ValidateInitialUrl("https://example.com/schema.json", allowExternal:=True) Is Nothing, "https to a public host is allowed when opted in")
    End Sub

    Private Sub DangerousSchemaRedirectsAreBlocked()
        AssertTrue(SchemaUrlPolicy.ValidateRedirectTarget(New Uri("http://example.com/s.json")) IsNot Nothing, "redirect to http blocked")
        AssertTrue(SchemaUrlPolicy.ValidateRedirectTarget(New Uri("file:///c:/schema.json")) IsNot Nothing, "redirect to file blocked")
        AssertTrue(SchemaUrlPolicy.ValidateRedirectTarget(New Uri("file://server/share/schema.json")) IsNot Nothing, "redirect to UNC blocked")
        AssertTrue(SchemaUrlPolicy.ValidateRedirectTarget(New Uri("https://localhost/s.json")) IsNot Nothing, "redirect to localhost blocked")
        AssertTrue(SchemaUrlPolicy.ValidateRedirectTarget(New Uri("https://127.0.0.1/s.json")) IsNot Nothing, "redirect to loopback blocked")
        AssertTrue(SchemaUrlPolicy.ValidateRedirectTarget(New Uri("https://10.0.0.5/s.json")) IsNot Nothing, "redirect to 10/8 blocked")
        AssertTrue(SchemaUrlPolicy.ValidateRedirectTarget(New Uri("https://172.20.1.1/s.json")) IsNot Nothing, "redirect to 172.16/12 blocked")
        AssertTrue(SchemaUrlPolicy.ValidateRedirectTarget(New Uri("https://192.168.1.1/s.json")) IsNot Nothing, "redirect to 192.168/16 blocked")
        AssertTrue(SchemaUrlPolicy.ValidateRedirectTarget(New Uri("https://169.254.0.9/s.json")) IsNot Nothing, "redirect to link-local blocked")
        AssertTrue(SchemaUrlPolicy.ValidateRedirectTarget(New Uri("https://[fe80::1]/s.json")) IsNot Nothing, "redirect to IPv6 link-local blocked")
        AssertTrue(SchemaUrlPolicy.ValidateRedirectTarget(New Uri("https://[fc00::1]/s.json")) IsNot Nothing, "redirect to IPv6 unique-local blocked")
        AssertTrue(SchemaUrlPolicy.ValidateRedirectTarget(New Uri("https://example.com/s.json")) Is Nothing, "redirect to public https allowed")
    End Sub

    Private Sub DnsResolvedPrivateAddressesAreBlocked()
        Dim blockedSingles = {
            "127.0.0.1", "10.0.0.5", "172.20.1.1", "192.168.1.1", "169.254.0.9",
            "::1", "fe80::1", "fc00::1",
            "::ffff:127.0.0.1", "::ffff:192.168.1.1", "::ffff:10.1.2.3"
        }

        For Each candidate In blockedSingles
            Dim addresses = {Net.IPAddress.Parse(candidate)}
            AssertTrue(SchemaUrlPolicy.ValidateResolvedAddresses("example.com", addresses) IsNot Nothing, $"resolved {candidate} must be blocked")
        Next

        Dim publicAddresses = {Net.IPAddress.Parse("93.184.216.34"), Net.IPAddress.Parse("2606:2800:220:1:248:1893:25c8:1946")}
        AssertTrue(SchemaUrlPolicy.ValidateResolvedAddresses("example.com", publicAddresses) Is Nothing, "public resolved addresses are allowed")

        Dim mixed = {Net.IPAddress.Parse("93.184.216.34"), Net.IPAddress.Parse("192.168.0.10")}
        AssertTrue(SchemaUrlPolicy.ValidateResolvedAddresses("example.com", mixed) IsNot Nothing, "any private address in the resolution set blocks the fetch")

        AssertTrue(SchemaUrlPolicy.ValidateResolvedAddresses("example.com", Array.Empty(Of Net.IPAddress)()) IsNot Nothing, "empty resolution set is blocked")
    End Sub

    Private Sub SchemaDiagnosticsMapToBodyLocation()
        Dim parser = New JsonParserService()
        Dim schemaValidation = New SchemaValidationService()
        Dim instance = "{" & Environment.NewLine &
            "  ""user"": {" & Environment.NewLine &
            "    ""name"": 5" & Environment.NewLine &
            "  }" & Environment.NewLine &
            "}"
        Dim schema = "{""properties"":{""user"":{""properties"":{""name"":{""type"":""string""}}}}}"
        Dim root = parser.Parse(instance).Root
        Dim diagnostics = schemaValidation.Validate(instance, schema, "", root)

        AssertEqual(1, diagnostics.Count, "nested diagnostic count")
        AssertEqual("/user/name", diagnostics(0).JsonPointer, "nested pointer")
        AssertEqual(3, diagnostics(0).Line.Value, "nested body line for error jump")
        AssertTrue(diagnostics(0).Column.HasValue, "nested body column for error jump")
        AssertTrue(diagnostics(0).RelatedRange IsNot Nothing, "related range present")
    End Sub

    Private Sub JsonToXmlWithAttributes()
        Dim conversion = New JsonXmlConversionService()
        Dim result = conversion.ConvertJsonToXml("{""root"":{""@id"":""1"",""name"":""A""}}")

        AssertContains(result.Output, "<root id=""1"">", "attribute on root")
        AssertContains(result.Output, "<name>A</name>", "child element")

        Dim listResult = conversion.ConvertJsonToXml("{""list"":[1,2]}")
        AssertContains(listResult.Output, "<item>1</item>", "array item element")
        AssertContains(listResult.Output, "<item>2</item>", "second array item element")
    End Sub

    Private Sub JsonToYamlTwoSpaceList()
        Dim conversion = New JsonYamlConversionService()
        Dim result = conversion.ConvertJsonToYaml("{""list"":[1,2],""name"":""Visual JSON"",""meta"":{""ok"":true,""none"":null}}")

        AssertContains(result.Output, "list:" & Environment.NewLine & "  - 1" & Environment.NewLine & "  - 2", "2-space list")
        AssertContains(result.Output, "name: Visual JSON", "plain string scalar")
        AssertContains(result.Output, "  ok: true", "boolean kept as YAML built-in")
        AssertContains(result.Output, "  none: null", "null kept as YAML built-in")
    End Sub

    Private Sub XxeIsNotResolved()
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

    Private Sub FailedConversionNeverTouchesFiles()
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

    Private Sub XmlToJsonRoundTrip()
        Dim conversion = New JsonXmlConversionService()
        Dim result = conversion.ConvertXmlToJson("<root id=""1""><name>A</name><name>B</name></root>")

        AssertContains(result.Output, """@id"": ""1""", "attribute mapped to @id")
        AssertContains(result.Output, """name"": [", "repeated elements become an array")
        AssertContains(result.Output, """A""", "first repeated value")
        AssertContains(result.Output, """B""", "second repeated value")
    End Sub

    Private Sub YamlToJsonRoundTrip()
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

    Private Sub YamlUnsupportedFeaturesAreRejected()
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

    Private Sub TextExportCreatesBackupAndPreservesTarget()
        Dim tempRoot = CreateTempDirectory()
        Try
            Dim export = New TextExportService()
            Dim target = Path.Combine(tempRoot, "out.yaml")
            File.WriteAllText(target, "original: true")

            Dim result = export.Save(target, "updated: true")
            AssertEqual("updated: true", File.ReadAllText(target), "export replaced target")
            AssertTrue(File.Exists(result.BackupPath), "export backup exists")
            AssertEqual("original: true", File.ReadAllText(result.BackupPath), "backup preserves original")
        Finally
            Directory.Delete(tempRoot, recursive:=True)
        End Try
    End Sub

    Private Sub P2StateCaptureRestoreKeepsPointers()
        Dim parser = New JsonParserService()
        Dim stateService = New DocumentStateService()
        Dim root = parser.Parse("{""a"":{""b"":1},""c"":2}").Root
        Dim state = stateService.CreateState("/a/b", {"", "/a"}, "/a", 12, 40.5)
        Dim target = stateService.ResolveRestoreTarget(root, state)

        AssertEqual("/a/b", target.JsonPointer, "selected pointer restored")
        AssertTrue(state.ExpandedPointers.Contains(""), "root expanded pointer retained")
        AssertTrue(state.ExpandedPointers.Contains("/a"), "expanded pointer retained")
    End Sub

    Private Sub P2MissingPointerFallsBackToAncestor()
        Dim parser = New JsonParserService()
        Dim stateService = New DocumentStateService()
        Dim root = parser.Parse("{""a"":{""c"":1},""d"":2}").Root
        Dim target = stateService.ResolveRestoreTarget(root, stateService.CreateState("/a/b/0", Array.Empty(Of String)()))

        AssertEqual("/a", target.JsonPointer, "nearest existing ancestor")

        Dim rootTarget = stateService.ResolveRestoreTarget(root, stateService.CreateState("/missing/value", Array.Empty(Of String)()))
        AssertEqual("", rootTarget.JsonPointer, "root fallback")
    End Sub

    Private Sub P2OffsetMapsToNode()
        Dim parser = New JsonParserService()
        Dim stateService = New DocumentStateService()
        Dim text = "{""a"":1,""b"":{""c"":true}}"
        Dim root = parser.Parse(text).Root

        AssertEqual("/b", stateService.GetPointerAtOffset(root, text.IndexOf("""b""", StringComparison.Ordinal)), "key offset maps to property")
        AssertEqual("/b/c", stateService.GetPointerAtOffset(root, text.IndexOf("true", StringComparison.Ordinal)), "value offset maps to nested value")
        AssertEqual("/a", stateService.GetPointerAtOffset(root, text.IndexOf(","c)), "delimiter after value keeps previous node")
    End Sub

    Private Sub P2PointerDisplayUsesRootLabel()
        Dim parser = New JsonParserService()
        Dim stateService = New DocumentStateService()
        Dim root = parser.Parse("{""a"":[1]}").Root

        AssertEqual("(root)", DocumentStateService.ToPointerDisplay(root.JsonPointer), "root display")
        AssertEqual("/a/0", stateService.GetPointerDisplay(root, "{""a"":[".Length), "nested display")
    End Sub

    Private Sub P2StateStoresTextOffsets()
        Dim stateService = New DocumentStateService()
        Dim state = stateService.CreateState("/a", {"/a"}, "/a", 128, 256.75)

        AssertEqual(128, state.TextCaretOffset, "caret offset")
        AssertEqual(256.75, state.TextScrollOffset, "scroll offset")
    End Sub

    Private Sub P2TenMbFormattedOffsetLookupWithin200Ms()
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

        Dim timer = Stopwatch.StartNew()
        For iteration = 0 To 19
            For Each offset In offsets
                Dim node = stateService.FindNodeAtOffset(root, offset)
                AssertTrue(node IsNot Nothing, "offset lookup returns node")
            Next
        Next
        timer.Stop()

        AssertTrue(timer.Elapsed < TimeSpan.FromMilliseconds(200), $"offset lookup elapsed {timer.Elapsed.TotalMilliseconds:n1}ms")
    End Sub

    Private Sub P2FoldingIgnoresStringBracesAndNests()
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

    Private Sub P2SingleLineBlocksAreNotFoldable()
        Dim folding = New JsonFoldingService()
        Dim ranges = folding.CreateRanges("{""a"":{""b"":1},""c"":[1,2]}")

        AssertEqual(0, ranges.Count, "single-line blocks are not foldable")
    End Sub

    Private Sub P2ReplaceAllSupportsCaseAndRegex()
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

    Private Sub P2GridRedoRestoresMovedNodeOrder()
        Dim parser = New JsonParserService()
        Dim serializer = New JsonTreeSerializer()
        Dim ops = New GridOperationService()
        Dim undo = New GridUndoService()
        Dim root = parser.Parse("{""a"":1,""b"":2,""c"":3}").Root

        undo.Capture(root)
        AssertTrue(ops.MoveBefore(root, root.Children(2), root.Children(0)), "move before should succeed")
        Dim movedText = serializer.Serialize(root)
        Dim restored = undo.Undo(root)
        Dim redone = undo.Redo(restored)

        AssertEqual(movedText, serializer.Serialize(redone), "redo returns moved state")
    End Sub

    Private Sub P2GridRedoClearedByNewOperation()
        Dim parser = New JsonParserService()
        Dim ops = New GridOperationService()
        Dim undo = New GridUndoService()
        Dim root = parser.Parse("{""a"":1,""b"":2,""c"":3}").Root

        undo.Capture(root)
        AssertTrue(ops.MoveBefore(root, root.Children(2), root.Children(0)), "move before should succeed")
        Dim restored = undo.Undo(root)
        AssertTrue(undo.CanRedo(), "redo exists after undo")

        undo.Capture(restored)
        AssertTrue(ops.Delete(restored, restored.Children(0)), "delete succeeds")
        AssertFalse(undo.CanRedo(), "new operation clears redo")
    End Sub

    Private Sub P2GridDuplicateCreatesUniqueSibling()
        Dim parser = New JsonParserService()
        Dim serializer = New JsonTreeSerializer()
        Dim ops = New GridOperationService()
        Dim root = parser.Parse("{""item"":{""name"":""a""},""itemCopy"":{""name"":""existing""}}").Root

        Dim duplicate = ops.Duplicate(root, root.Children(0))
        AssertTrue(duplicate IsNot Nothing, "duplicate returned")
        AssertEqual("itemCopy1", duplicate.Key, "unique object key")
        AssertContains(serializer.Serialize(root), """itemCopy1"": {", "duplicate serialized")
    End Sub

    Private Sub P2SchemaConstMismatch()
        Dim validation = New SchemaValidationService()
        Dim schema = "{""properties"":{""env"":{""const"":""prod""}}}"

        Dim diagnostics = validation.Validate("{""env"":""dev""}", schema)
        AssertEqual(1, diagnostics.Count, "const diagnostic count")
        AssertEqual("SCH-CONST", diagnostics(0).ErrorCode, "const code")
        AssertEqual("/env", diagnostics(0).JsonPointer, "const pointer")

        AssertEqual(0, validation.Validate("{""env"":""prod""}", schema).Count, "matching const passes")
    End Sub

    Private Sub P2SchemaStringLength()
        Dim validation = New SchemaValidationService()
        Dim schema = "{""properties"":{""code"":{""minLength"":3,""maxLength"":5}}}"

        Dim tooShort = validation.Validate("{""code"":""ab""}", schema)
        AssertEqual("SCH-MINLENGTH", tooShort(0).ErrorCode, "min length code")

        Dim tooLong = validation.Validate("{""code"":""abcdef""}", schema)
        AssertEqual("SCH-MAXLENGTH", tooLong(0).ErrorCode, "max length code")

        AssertEqual(0, validation.Validate("{""code"":""abcd""}", schema).Count, "in-range string passes")
        AssertEqual(0, validation.Validate("{""code"":""𠀋𠀋𠀋""}", schema).Count, "surrogate pairs count as single code points")
    End Sub

    Private Sub P2SchemaLocalRef()
        Dim validation = New SchemaValidationService()
        Dim schema = "{""properties"":{""user"":{""$ref"":""#/definitions/person""}},""definitions"":{""person"":{""type"":""object"",""required"":[""name""]}}}"

        Dim diagnostics = validation.Validate("{""user"":{}}", schema)
        AssertEqual(1, diagnostics.Count, "ref diagnostic count")
        AssertEqual("SCH-REQUIRED", diagnostics(0).ErrorCode, "referenced rule applied")
        AssertEqual("/user", diagnostics(0).JsonPointer, "referenced rule pointer")

        AssertEqual(0, validation.Validate("{""user"":{""name"":""a""}}", schema).Count, "valid instance passes through ref")

        Dim missingRef = validation.Validate("{""user"":{}}", "{""properties"":{""user"":{""$ref"":""#/definitions/absent""}}}")
        AssertEqual("SCH-REF-NOTFOUND", missingRef(0).ErrorCode, "unresolved ref code")
    End Sub

    Private Sub P2SchemaRefCycle()
        Dim validation = New SchemaValidationService()
        Dim schema = "{""$ref"":""#/definitions/a"",""definitions"":{""a"":{""$ref"":""#/definitions/b""},""b"":{""$ref"":""#/definitions/a""}}}"

        Dim timer = Stopwatch.StartNew()
        Dim diagnostics = validation.Validate("{}", schema)
        timer.Stop()

        AssertEqual(1, diagnostics.Count, "cycle reported once")
        AssertEqual("SCH-REF-CYCLE", diagnostics(0).ErrorCode, "cycle code")
        AssertTrue(timer.Elapsed < TimeSpan.FromSeconds(1), "cycle detection terminates quickly")
    End Sub

    Private Sub P2SchemaExternalRefWarning()
        Dim validation = New SchemaValidationService()
        Dim diagnostics = validation.Validate("{}", "{""$ref"":""https://example.com/schema.json""}")

        AssertEqual(1, diagnostics.Count, "external ref diagnostic count")
        AssertEqual("SCH-REF-UNSUPPORTED", diagnostics(0).ErrorCode, "external ref code")
        AssertEqual("Warning", diagnostics(0).Severity, "external ref severity")
    End Sub

    Private Sub P2SchemaFormatWarnings()
        Dim validation = New SchemaValidationService()
        Dim schema = "{""properties"":{""at"":{""format"":""date-time""},""mail"":{""format"":""email""},""link"":{""format"":""uri""},""other"":{""format"":""custom-unknown""}}}"

        Dim invalid = validation.Validate("{""at"":""not-a-date"",""mail"":""nope"",""link"":""not a uri"",""other"":""anything""}", schema)
        AssertEqual(3, invalid.Count, "three format warnings")
        AssertTrue(invalid.All(Function(item) item.ErrorCode = "SCH-FORMAT" AndAlso item.Severity = "Warning"), "format warnings only")

        Dim valid = validation.Validate("{""at"":""2026-07-09T12:00:00Z"",""mail"":""a@b.co"",""link"":""https://example.com/x""}", schema)
        AssertEqual(0, valid.Count, "valid formats pass")
    End Sub

    Private Sub P2XmlRepeatParentName()
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

    Private Sub P2XmlXsiNil()
        Dim service = New JsonXmlConversionService()
        Dim options = New XmlConversionOptions With {.NullMode = XmlNullMode.XsiNil}

        Dim result = service.ConvertJsonToXml("{""data"":{""empty"":null,""name"":""a""}}", options)
        AssertContains(result.Output, "xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance""", "xsi namespace on root")
        AssertContains(result.Output, "<empty xsi:nil=""true""", "nil attribute on null element")
    End Sub

    Private Sub P2XmlDefaultOptionsRegression()
        Dim service = New JsonXmlConversionService()
        Dim json = "{""root"":{""@id"":""1"",""tags"":[1,2],""empty"":null,""name"":""a""}}"

        Dim legacy = service.ConvertJsonToXml(json)
        Dim viaDefault = service.ConvertJsonToXml(json, XmlConversionOptions.CreateDefault())
        AssertEqual(legacy.Output, viaDefault.Output, "default options equal the parameterless overload")
        AssertContains(legacy.Output, "<item>1</item>", "default arrays keep item elements")
        AssertContains(legacy.Output, "<empty></empty>", "default null keeps the empty element")
        AssertFalse(legacy.Output.Contains("xsi", StringComparison.Ordinal), "default output has no xsi declarations")
    End Sub

    Private Sub P2JsonLinesArrayRoot()
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

    Private Sub P2JsonLinesNonArrayRoot()
        Dim parser = New JsonParserService()
        Dim serializer = New JsonLinesSerializer()
        Dim root = parser.Parse("{""a"":1}").Root

        Dim result = serializer.Serialize(root, vbLf)
        AssertEqual(1, result.Warnings.Count, "non-array root warns")
        AssertEqual(1, result.Text.Split(New String() {vbLf}, StringSplitOptions.RemoveEmptyEntries).Length, "single line output")
    End Sub

    Private Sub P2JsonLinesRoundTrip()
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

    Private Sub P2CompletionCandidates()
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

    Private Sub P2GridCrossParentMove()
        Dim parser = New JsonParserService()
        Dim ops = New GridOperationService()
        Dim root = parser.Parse("{""a"":{""x"":1,""y"":2},""b"":{""x"":9},""arr"":[1,2]}").Root
        Dim nodeA = root.Children(0)
        Dim nodeY = nodeA.Children(1)
        Dim nodeB = root.Children(1)
        Dim nodeBx = nodeB.Children(0)
        Dim nodeAx = nodeA.Children(0)

        AssertEqual(CrossParentMoveStatus.Allowed, ops.CheckMoveBefore(root, nodeY, nodeBx), "no-conflict cross-parent move is allowed")
        AssertTrue(ops.MoveBeforeAcrossParents(root, nodeY, nodeBx), "cross-parent move succeeds")
        AssertEqual(1, nodeA.Children.Count, "source parent lost the node")
        AssertEqual(2, nodeB.Children.Count, "target parent gained the node")
        AssertEqual("y", nodeB.Children(0).Key, "moved node inserted before target")

        AssertEqual(CrossParentMoveStatus.KeyConflict, ops.CheckMoveBefore(root, nodeAx, nodeB.Children(1)), "duplicate key requires confirmation")
        AssertTrue(ops.MoveBeforeAcrossParents(root, nodeAx, nodeB.Children(1)), "confirmed conflict move succeeds")
        AssertEqual("x1", nodeB.Children.First(Function(child) Not String.Equals(child.Key, "x", StringComparison.Ordinal) AndAlso Not String.Equals(child.Key, "y", StringComparison.Ordinal)).Key, "conflicting key is uniquified")

        AssertEqual(CrossParentMoveStatus.Invalid, ops.CheckMoveBefore(root, root.Children(0), root.Children(0)), "self target is invalid")
        Dim arrNode = root.Children(2)
        AssertEqual(CrossParentMoveStatus.IntoOwnDescendant, ops.CheckMoveBefore(root, arrNode, arrNode.Children(0)), "moving into own descendant is rejected")
        AssertFalse(ops.MoveBeforeAcrossParents(root, arrNode, arrNode.Children(0)), "descendant move does not execute")

        Dim intoArray = ops.CheckMoveBefore(root, nodeB.Children(0), arrNode.Children(0))
        AssertEqual(CrossParentMoveStatus.Allowed, intoArray, "object-to-array move allowed")
        AssertTrue(ops.MoveBeforeAcrossParents(root, nodeB.Children(0), arrNode.Children(0)), "object-to-array move succeeds")
        AssertEqual("[0]", arrNode.Children(0).Key, "array keys reindexed after insert")
    End Sub

    Private Sub P2GridInferContainerTypes()
        Dim inference = New TypeInferenceService()
        AssertEqual(JsonNodeKind.ObjectValue, inference.NormalizePrimitiveInput("{}").Kind, "braces become object")
        AssertEqual(JsonNodeKind.ArrayValue, inference.NormalizePrimitiveInput(" [] ").Kind, "brackets become array")
        AssertEqual(JsonNodeKind.StringValue, inference.NormalizePrimitiveInput("{ }").Kind, "spaced braces stay string")
        AssertEqual(JsonNodeKind.StringValue, inference.NormalizePrimitiveInput("{x}").Kind, "non-empty braces stay string")

        Dim node = New JsonTreeNode("cell", "$.cell", JsonNodeKind.StringValue, "{}", "/cell")
        inference.ApplyToNode(node)
        AssertEqual(JsonNodeKind.ObjectValue, node.Kind, "cell input {} becomes object node")
        AssertEqual("", node.ValueText, "container node has empty value text")
        AssertEqual(0, node.Children.Count, "container starts empty")
    End Sub

    Private Sub P2TableColumnsUnionWithMissingCells()
        Dim parser = New JsonParserService()
        Dim builder = New TableViewModelBuilder()
        Dim root = parser.Parse("[{""id"":1,""name"":""a""},{""id"":2,""email"":""b@example.com""},{""name"":""c"",""active"":true}]").Root

        AssertTrue(TableViewModelBuilder.IsCandidate(root), "object array is a table candidate")
        AssertTrue(builder.CanBuild(root), "object array is buildable")

        Dim model = builder.Build(root)
        AssertEqual(4, model.Columns.Count, "column union count")
        AssertEqual("id", model.Columns(0).Name, "first-seen column id")
        AssertEqual("name", model.Columns(1).Name, "first-seen column name")
        AssertEqual("email", model.Columns(2).Name, "first-seen column email")
        AssertEqual("active", model.Columns(3).Name, "first-seen column active")
        AssertEqual(3, model.Rows.Count, "row count")
        AssertTrue(model.Rows(0).Cells(2).IsMissing, "row0 email is missing")
        AssertEqual("", model.Rows(0).Cells(2).DisplayText, "missing cell renders empty")
        AssertEqual("2", model.Rows(1).Cells(0).DisplayText, "number cell text")
        AssertEqual("b@example.com", model.Rows(1).Cells(2).DisplayText, "string cell text")
        AssertTrue(model.Rows(2).Cells(0).IsMissing, "row2 id is missing")
        AssertEqual("true", model.Rows(2).Cells(3).DisplayText, "boolean cell text")
    End Sub

    Private Sub P2TableNonObjectElementsUseValueColumn()
        Dim parser = New JsonParserService()
        Dim builder = New TableViewModelBuilder()
        Dim root = parser.Parse("[{""id"":1},{""id"":2,""tags"":[1,2],""meta"":{""x"":1}},""plain"",{""id"":3}]").Root

        AssertTrue(builder.CanBuild(root), "majority-object array is buildable")

        Dim model = builder.Build(root)
        Dim valueIndex = model.Columns.Count - 1
        AssertEqual("(value)", model.Columns(valueIndex).Name, "value column name")
        AssertTrue(model.Columns(valueIndex).IsValueColumn, "value column flag")
        AssertEqual("plain", model.Rows(2).Cells(valueIndex).DisplayText, "non-object element in value column")
        AssertTrue(model.Rows(2).Cells(0).IsMissing, "non-object row has no property cells")
        AssertTrue(model.Rows(0).Cells(valueIndex).IsMissing, "object row value cell is empty")
        AssertEqual("[2]", model.Rows(1).Cells(1).DisplayText, "array cell summary")
        AssertEqual("{…}", model.Rows(1).Cells(2).DisplayText, "object cell summary")
        AssertTrue(model.Rows(1).Cells(1).IsContainer, "array cell is container")
    End Sub

    Private Sub P2TableCellEditInfersTypeAndUndoes()
        Dim parser = New JsonParserService()
        Dim builder = New TableViewModelBuilder()
        Dim undo = New GridUndoService()
        Dim root = parser.Parse("[{""id"":1,""name"":""a""},{""id"":2}]").Root
        Dim model = builder.Build(root)

        undo.Capture(root)
        Dim edited = builder.ApplyCellEdit(model, model.Rows(0), 1, "42")
        AssertTrue(edited IsNot Nothing, "existing scalar cell is editable")
        AssertEqual(JsonNodeKind.NumberValue, edited.Kind, "numeric string becomes number")
        AssertEqual("42", edited.ValueText, "edited value text")

        Dim restored = undo.Undo(root)
        Dim restoredName = restored.Children(0).Children(1)
        AssertEqual(JsonNodeKind.StringValue, restoredName.Kind, "one undo restores kind")
        AssertEqual("a", restoredName.ValueText, "one undo restores value")
    End Sub

    Private Sub P2TableMissingCellEditMaterializesRowOnly()
        Dim parser = New JsonParserService()
        Dim builder = New TableViewModelBuilder()
        Dim root = parser.Parse("[{""id"":1,""name"":""a""},{""id"":2},""plain""]").Root
        Dim model = builder.Build(root)

        Dim edited = builder.ApplyCellEdit(model, model.Rows(1), 1, "b")
        AssertTrue(edited IsNot Nothing, "missing property cell is materialized")
        AssertEqual("name", edited.Key, "materialized key")
        AssertEqual(JsonNodeKind.StringValue, edited.Kind, "materialized kind")
        AssertEqual("/1/name", edited.JsonPointer, "materialized pointer")
        AssertEqual(2, root.Children(1).Children.Count, "edited row gained the property")
        AssertEqual(2, root.Children(0).Children.Count, "other rows unchanged")

        Dim valueIndex = model.Columns.Count - 1
        AssertTrue(builder.ApplyCellEdit(model, model.Rows(0), valueIndex, "x") Is Nothing, "object row value column stays rejected")
        AssertTrue(builder.ApplyCellEdit(model, model.Rows(2), 0, "x") Is Nothing, "non-object row property column stays rejected")

        Dim extras = New List(Of String) From {"note"}
        Dim extended = builder.Build(root, extras)
        AssertEqual("note", extended.Columns(2).Name, "extra column appended before value column")
        Dim noteNode = builder.ApplyCellEdit(extended, extended.Rows(0), 2, "7")
        AssertTrue(noteNode IsNot Nothing, "extra column cell materializes")
        AssertEqual(JsonNodeKind.NumberValue, noteNode.Kind, "extra column value inferred")
        AssertEqual(3, root.Children(0).Children.Count, "note added to edited row only")
        AssertEqual(2, root.Children(1).Children.Count, "note not added to other rows")

        Dim rebuilt = builder.Build(root, extras)
        AssertEqual(extended.Columns.Count, rebuilt.Columns.Count, "materialized extra column does not duplicate")
    End Sub

    Private Sub P2TableDisplaySortLeavesStructure()
        Dim parser = New JsonParserService()
        Dim builder = New TableViewModelBuilder()
        Dim root = parser.Parse("[{""v"":10},{""v"":2},{""v"":3}]").Root
        Dim model = builder.Build(root)

        Dim ascendingRows = builder.SortRows(model, 0, ascending:=True)
        AssertEqual("2", ascendingRows(0).Cells(0).DisplayText, "numeric ascending first")
        AssertEqual("3", ascendingRows(1).Cells(0).DisplayText, "numeric ascending second")
        AssertEqual("10", ascendingRows(2).Cells(0).DisplayText, "numeric sort is not lexicographic")

        Dim descendingRows = builder.SortRows(model, 0, ascending:=False)
        AssertEqual("10", descendingRows(0).Cells(0).DisplayText, "descending first")

        AssertEqual("10", root.Children(0).Children(0).ValueText, "structure order unchanged after sort")
        AssertEqual("2", root.Children(1).Children(0).ValueText, "structure order unchanged after sort 2")
        AssertEqual(0, model.Rows(0).Index, "model rows keep structural indexes")

        Dim mixed = parser.Parse("[{""v"":""s""},{""v"":null},{""x"":1},{""v"":true},{""v"":5}]").Root
        Dim mixedModel = builder.Build(mixed)
        Dim mixedSorted = builder.SortRows(mixedModel, 0, ascending:=True)
        AssertTrue(mixedSorted(0).Cells(0).IsMissing, "missing sorts first")
        AssertEqual("null", mixedSorted(1).Cells(0).DisplayText, "null after missing")
        AssertEqual("true", mixedSorted(2).Cells(0).DisplayText, "boolean after null")
        AssertEqual("5", mixedSorted(3).Cells(0).DisplayText, "number after boolean")
        AssertEqual("s", mixedSorted(4).Cells(0).DisplayText, "string last")
    End Sub

    Private Sub P2TableApplySortRewritesStructure()
        Dim parser = New JsonParserService()
        Dim builder = New TableViewModelBuilder()
        Dim undo = New GridUndoService()
        Dim root = parser.Parse("[{""v"":10},{""v"":2},{""v"":3}]").Root
        Dim model = builder.Build(root)
        Dim sorted = builder.SortRows(model, 0, ascending:=True)

        undo.Capture(root)
        AssertTrue(builder.ApplySortToStructure(root, sorted), "apply sort succeeds")
        AssertEqual("2", root.Children(0).Children(0).ValueText, "array order rewritten")
        AssertEqual("3", root.Children(1).Children(0).ValueText, "array order rewritten 2")
        AssertEqual("10", root.Children(2).Children(0).ValueText, "array order rewritten 3")
        AssertEqual("[0]", root.Children(0).Key, "array keys reindexed")
        AssertEqual("[2]", root.Children(2).Key, "array keys reindexed last")

        Dim restored = undo.Undo(root)
        AssertEqual("10", restored.Children(0).Children(0).ValueText, "one undo restores original order")

        Dim foreign = builder.Build(parser.Parse("[{""v"":1},{""v"":2},{""v"":3}]").Root)
        AssertFalse(builder.ApplySortToStructure(root, foreign.Rows), "foreign row set is rejected")
    End Sub

    Private Sub P2TableRowLimitExceeded()
        Dim builder = New TableViewModelBuilder()
        Dim root = CreateLargeObjectArray(10001)

        AssertTrue(TableViewModelBuilder.IsCandidate(root), "large object array remains a candidate")
        AssertTrue(TableViewModelBuilder.ExceedsRowLimit(root), "10001 rows exceed the limit")
        AssertFalse(builder.CanBuild(root), "CanBuild is false above 10000 rows")
    End Sub

    Private Sub P2TableTenThousandRowsBuildFast()
        Dim builder = New TableViewModelBuilder()
        Dim root = CreateLargeObjectArray(10000)

        AssertTrue(builder.CanBuild(root), "CanBuild is true at exactly 10000 rows")

        Dim timer = Stopwatch.StartNew()
        Dim model = builder.Build(root)
        timer.Stop()

        AssertEqual(10000, model.Rows.Count, "row count")
        AssertEqual(5, model.Columns.Count, "column count")
        AssertTrue(timer.Elapsed < TimeSpan.FromSeconds(2), $"NFR-P2-PERF-004 build time was {timer.Elapsed}")
    End Sub

    Private Function CreateLargeObjectArray(rows As Integer) As JsonTreeNode
        Dim root = New JsonTreeNode("$", "$", JsonNodeKind.ArrayValue, "", "")
        For index = 0 To rows - 1
            Dim element = New JsonTreeNode($"[{index}]", $"$[{index}]", JsonNodeKind.ObjectValue, "", $"/{index}")
            element.Children.Add(New JsonTreeNode("id", $"$[{index}].id", JsonNodeKind.NumberValue, index.ToString(Globalization.CultureInfo.InvariantCulture), $"/{index}/id"))
            element.Children.Add(New JsonTreeNode("name", $"$[{index}].name", JsonNodeKind.StringValue, $"user-{index}", $"/{index}/name"))
            element.Children.Add(New JsonTreeNode("email", $"$[{index}].email", JsonNodeKind.StringValue, $"user-{index}@example.com", $"/{index}/email"))
            element.Children.Add(New JsonTreeNode("active", $"$[{index}].active", JsonNodeKind.BooleanValue, If(index Mod 2 = 0, "true", "false"), $"/{index}/active"))
            element.Children.Add(New JsonTreeNode("score", $"$[{index}].score", JsonNodeKind.NumberValue, (index Mod 100).ToString(Globalization.CultureInfo.InvariantCulture), $"/{index}/score"))
            root.Children.Add(element)
        Next

        Return root
    End Function

    Private Sub P2SettingsRoundTrip()
        Dim tempRoot = CreateTempDirectory()
        Try
            Dim service = New SettingsService(tempRoot)
            Dim settings = AppSettings.CreateDefault()
            settings.Language = "ja"
            settings.BackupBeforeSave = False
            settings.AllowExternalSchema = True
            settings.AutoCloseBrackets = False
            settings.SchemaSearchPaths.Add("C:\schemas")
            settings.RecentFiles.Add("C:\data\a.json")
            settings.Window.Width = 1400
            settings.Window.Height = 900
            settings.Window.Maximized = True

            service.Save(settings)
            Dim loaded = service.Load().Settings

            AssertEqual("ja", loaded.Language, "language")
            AssertFalse(loaded.BackupBeforeSave, "backup")
            AssertTrue(loaded.AllowExternalSchema, "external schema")
            AssertFalse(loaded.AutoCloseBrackets, "auto close")
            AssertEqual("C:\schemas", loaded.SchemaSearchPaths(0), "schema path")
            AssertEqual("C:\data\a.json", loaded.RecentFiles(0), "recent path")
            AssertEqual(1400.0, loaded.Window.Width, "window width")
            AssertTrue(loaded.Window.Maximized, "maximized")
        Finally
            Directory.Delete(tempRoot, recursive:=True)
        End Try
    End Sub

    Private Sub P2BrokenSettingsMovedAside()
        Dim tempRoot = CreateTempDirectory()
        Try
            Dim service = New SettingsService(tempRoot)
            Directory.CreateDirectory(tempRoot)
            File.WriteAllText(service.SettingsPath, "{ broken", Encoding.UTF8)

            Dim result = service.Load()

            AssertTrue(result.RecoveredFromBroken, "broken recovered")
            AssertTrue(File.Exists(result.BrokenPath), "broken file exists")
            AssertEqual("en", result.Settings.Language, "default language")
        Finally
            Directory.Delete(tempRoot, recursive:=True)
        End Try
    End Sub

    Private Sub P2UnknownSettingsKeysPreserved()
        Dim tempRoot = CreateTempDirectory()
        Try
            Dim service = New SettingsService(tempRoot)
            Directory.CreateDirectory(tempRoot)
            File.WriteAllText(service.SettingsPath, "{""version"":1,""language"":""ja"",""futureValue"":{""enabled"":true}}", New UTF8Encoding(False))

            Dim loaded = service.Load().Settings
            service.Save(loaded)
            Dim saved = File.ReadAllText(service.SettingsPath, Encoding.UTF8)

            AssertContains(saved, "futureValue", "unknown key")
            AssertContains(saved, "enabled", "nested unknown key")
        Finally
            Directory.Delete(tempRoot, recursive:=True)
        End Try
    End Sub

    Private Sub P2RecentFilesMaxDuplicateClear()
        Dim settings = AppSettings.CreateDefault()
        Dim recent = New RecentFilesService()

        For index = 0 To 10
            recent.Add(settings, $"C:\data\file{index}.json")
        Next
        AssertEqual(RecentFilesService.MaxRecentFiles, settings.RecentFiles.Count, "max recent")

        recent.Add(settings, "C:\data\file5.json")
        AssertEqual("C:\data\file5.json", settings.RecentFiles(0), "duplicate moved to front")
        AssertEqual(RecentFilesService.MaxRecentFiles, settings.RecentFiles.Count, "duplicate count unchanged")

        recent.Clear(settings)
        AssertEqual(0, settings.RecentFiles.Count, "cleared")
    End Sub

    Private Sub P2EncodingSamplesDetected()
        Dim tempRoot = CreateTempDirectory()
        Try
            Dim service = New EncodingDetectionService()
            Dim text = "{""a"":1}" & vbCrLf
            WriteEncodedSample(Path.Combine(tempRoot, "utf8.json"), text, New DetectedTextEncoding(TextEncodingKind.Utf8, New UTF8Encoding(False, True), False, NewLineKind.CrLf, ""))
            WriteEncodedSample(Path.Combine(tempRoot, "utf8bom.json"), text, New DetectedTextEncoding(TextEncodingKind.Utf8Bom, New UTF8Encoding(False, True), True, NewLineKind.CrLf, ""))
            WriteEncodedSample(Path.Combine(tempRoot, "utf16le.json"), text, New DetectedTextEncoding(TextEncodingKind.Utf16Le, Encoding.Unicode, False, NewLineKind.CrLf, ""))
            WriteEncodedSample(Path.Combine(tempRoot, "utf16be.json"), text, New DetectedTextEncoding(TextEncodingKind.Utf16Be, Encoding.BigEndianUnicode, False, NewLineKind.CrLf, ""))

            AssertEqual("UTF-8", service.ReadText(Path.Combine(tempRoot, "utf8.json")).EncodingInfo.Name, "utf8")
            AssertEqual("UTF-8 BOM", service.ReadText(Path.Combine(tempRoot, "utf8bom.json")).EncodingInfo.Name, "utf8 bom")
            AssertEqual("UTF-16 LE", service.ReadText(Path.Combine(tempRoot, "utf16le.json")).EncodingInfo.Name, "utf16 le")
            AssertEqual("UTF-16 BE", service.ReadText(Path.Combine(tempRoot, "utf16be.json")).EncodingInfo.Name, "utf16 be")
        Finally
            Directory.Delete(tempRoot, recursive:=True)
        End Try
    End Sub

    Private Sub P2SavePreservesDetectedEncoding()
        Dim tempRoot = CreateTempDirectory()
        Try
            Dim target = Path.Combine(tempRoot, "sample.json")
            Dim encodingService = New EncodingDetectionService()
            Dim originalEncoding = New DetectedTextEncoding(TextEncodingKind.Utf16LeBom, Encoding.Unicode, True, NewLineKind.CrLf, "")
            File.WriteAllBytes(target, encodingService.GetBytes("{""a"":1}" & vbCrLf, originalEncoding))

            Dim read = encodingService.ReadText(target)
            Dim saver = New FileSaveService()
            saver.Save(target, "{""a"":2}", read.EncodingInfo)
            Dim saved = encodingService.ReadText(target)

            AssertEqual("UTF-16 LE BOM", saved.EncodingInfo.Name, "encoding preserved")
            AssertEqual("CRLF", saved.EncodingInfo.NewLineName, "newline preserved")
            AssertContains(saved.Text, """a"": 2", "saved content")
        Finally
            Directory.Delete(tempRoot, recursive:=True)
        End Try
    End Sub

    Private Sub P2NewLineMajorityDetected()
        Dim text = "{" & vbCrLf & "  ""a"": 1," & vbCrLf & "  ""b"": 2" & vbLf & "}"
        AssertEqual(NewLineKind.CrLf, EncodingDetectionService.DetectNewLine(text), "crlf majority")
        AssertEqual("a" & vbCrLf & "b", EncodingDetectionService.NormalizeNewLines("a" & vbLf & "b", NewLineKind.CrLf), "normalize crlf")
    End Sub

    Private Sub P2EncodingFallbackWarning()
        Dim service = New EncodingDetectionService()
        Dim info = service.Detect(New Byte() {&HFF, &HFF, &HFF})

        AssertEqual(TextEncodingKind.Utf8, info.Kind, "fallback kind")
        AssertTrue(info.Warning.Length > 0, "warning")
    End Sub

    Private Sub P2FormatSnifferDetectsVariants()
        Dim sniffer = New FormatSniffer()

        AssertEqual(JsonInputFormat.JsonC, sniffer.Sniff("// comment" & Environment.NewLine & "{""a"":1}").Format, "jsonc")
        AssertEqual(JsonInputFormat.Json5, sniffer.Sniff("{name:'a'}").Format, "json5")
        AssertEqual(JsonInputFormat.JsonLines, sniffer.Sniff("{""a"":1}" & Environment.NewLine & "{""b"":2}").Format, "jsonl")
    End Sub

    Private Sub P2FormatSnifferUnknownIsNotConfident()
        Dim sniffer = New FormatSniffer()
        Dim result = sniffer.Sniff("not json")

        AssertFalse(result.Confident, "unknown not confident")
    End Sub

    Private Sub P2FileLogOmitsBody()
        Dim tempRoot = CreateTempDirectory()
        Try
            Dim log = New FileLogService(tempRoot)
            Dim path = log.WriteException("ValidationFailed", New InvalidOperationException("""secret"":true"))
            Dim content = File.ReadAllText(path, Encoding.UTF8)

            AssertContains(content, "System.InvalidOperationException", "exception type")
            AssertFalse(content.Contains("secret", StringComparison.OrdinalIgnoreCase), "exception message omitted")
        Finally
            Directory.Delete(tempRoot, recursive:=True)
        End Try
    End Sub

    Private Sub WriteEncodedSample(path As String, text As String, encodingInfo As DetectedTextEncoding)
        Dim service = New EncodingDetectionService()
        File.WriteAllBytes(path, service.GetBytes(text, encodingInfo))
    End Sub

    Private Function CreateLargeJson(targetCharacters As Integer) As String
        Dim builder = New StringBuilder(targetCharacters + 1024)
        builder.Append("{""items"":[")
        Dim index = 0

        While builder.Length < targetCharacters
            If index > 0 Then
                builder.Append(","c)
            End If

            builder.Append("{""id"":")
            builder.Append(index.ToString(Globalization.CultureInfo.InvariantCulture))
            builder.Append(",""name"":""item")
            builder.Append(index.ToString(Globalization.CultureInfo.InvariantCulture))
            builder.Append(""",""payload"":""")
            builder.Append("x"c, 2048)
            builder.Append("""}")
            index += 1
        End While

        builder.Append("]}")
        Return builder.ToString()
    End Function

    Private Function CreateTempDirectory() As String
        Dim path = IO.Path.Combine(IO.Path.GetTempPath(), "VisualJson.Tests", Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory(path)
        Return path
    End Function

    Private Sub AssertTrue(condition As Boolean, message As String)
        If Not condition Then
            Throw New InvalidOperationException(message)
        End If
    End Sub

    Private Sub AssertFalse(condition As Boolean, message As String)
        If condition Then
            Throw New InvalidOperationException(message)
        End If
    End Sub

    Private Sub AssertEqual(Of T)(expected As T, actual As T, message As String)
        If Not EqualityComparer(Of T).Default.Equals(expected, actual) Then
            Throw New InvalidOperationException($"{message}: expected <{expected}>, actual <{actual}>")
        End If
    End Sub

    Private Sub AssertContains(value As String, expectedSubstring As String, message As String)
        If value Is Nothing OrElse Not value.Contains(expectedSubstring, StringComparison.Ordinal) Then
            Throw New InvalidOperationException($"{message}: missing <{expectedSubstring}>")
        End If
    End Sub
End Module
