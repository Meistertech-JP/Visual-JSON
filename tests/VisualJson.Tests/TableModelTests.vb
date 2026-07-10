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
Public Class TableModelTests

    <TestMethod(DisplayName:="UT-P2-TBL-001 table columns are first-seen union with empty missing cells")>
    Public Sub P2TableColumnsUnionWithMissingCells()
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

    <TestMethod(DisplayName:="UT-P2-TBL-002 non-object elements land in the value column")>
    Public Sub P2TableNonObjectElementsUseValueColumn()
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

    <TestMethod(DisplayName:="UT-P2-TBL-003 missing cell edit materializes the property on that row only")>
    Public Sub P2TableMissingCellEditMaterializesRowOnly()
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

    <TestMethod(DisplayName:="UT-P2-TBL-004 cell edit infers type and undoes as one operation")>
    Public Sub P2TableCellEditInfersTypeAndUndoes()
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

    <TestMethod(DisplayName:="UT-P2-TBL-005 display sort leaves array children order unchanged")>
    Public Sub P2TableDisplaySortLeavesStructure()
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

    <TestMethod(DisplayName:="UT-P2-TBL-006 apply sort rewrites array order and undoes as one operation")>
    Public Sub P2TableApplySortRewritesStructure()
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

    <TestMethod(DisplayName:="UT-P2-TBL-007 10001 rows exceed the table limit")>
    Public Sub P2TableRowLimitExceeded()
        Dim builder = New TableViewModelBuilder()
        Dim root = CreateLargeObjectArray(10001)

        AssertTrue(TableViewModelBuilder.IsCandidate(root), "large object array remains a candidate")
        AssertTrue(TableViewModelBuilder.ExceedsRowLimit(root), "10001 rows exceed the limit")
        AssertFalse(builder.CanBuild(root), "CanBuild is false above 10000 rows")
    End Sub

    <TestMethod(DisplayName:="UT-P2-TBL-008 10000 rows build within two seconds")>
    Public Sub P2TableTenThousandRowsBuildFast()
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
End Class
