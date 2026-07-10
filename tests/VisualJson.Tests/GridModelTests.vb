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
Public Class GridModelTests

    <TestMethod(DisplayName:="Infer primitive values and serialize grid")>
    Public Sub InferPrimitiveValuesAndSerializeGrid()
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

    <TestMethod(DisplayName:="Infer primitive values by MVP-0 rules")>
    Public Sub InferPrimitiveValuesByMvp0Rules()
        Dim inference = New TypeInferenceService()

        AssertEqual(JsonNodeKind.StringValue, inference.NormalizePrimitiveInput("""123""").Kind, "quoted number kind")
        AssertEqual("123", inference.NormalizePrimitiveInput("""123""").ValueText, "quoted number value")
        AssertEqual(JsonNodeKind.NumberValue, inference.NormalizePrimitiveInput("123").Kind, "number kind")
        AssertEqual(JsonNodeKind.StringValue, inference.NormalizePrimitiveInput("001").Kind, "leading zero string")
        AssertEqual(JsonNodeKind.BooleanValue, inference.NormalizePrimitiveInput("true").Kind, "boolean")
        AssertEqual(JsonNodeKind.NullValue, inference.NormalizePrimitiveInput("null").Kind, "null")
        AssertEqual(JsonNodeKind.StringValue, inference.NormalizePrimitiveInput("NaN").Kind, "NaN string")
    End Sub

    <TestMethod(DisplayName:="Serialize edited grid key")>
    Public Sub SerializeEditedGridKey()
        Dim parser = New JsonParserService()
        Dim serializer = New JsonTreeSerializer()
        Dim root = parser.Parse("{""oldKey"":1}").Root
        root.Children(0).Key = "newKey"
        Dim serialized = serializer.Serialize(root)
        AssertContains(serialized, """newKey"": 1", "new key")
        AssertFalse(serialized.Contains("oldKey", StringComparison.Ordinal), "old key should not remain")
    End Sub

    <TestMethod(DisplayName:="Grid actions add delete move and change type")>
    Public Sub GridActionsAddDeleteMoveAndChangeType()
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

    <TestMethod(DisplayName:="Grid undo restores moved node order")>
    Public Sub GridUndoRestoresMovedNodeOrder()
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

    <TestMethod(DisplayName:="Grid filter keeps matching nodes and parents")>
    Public Sub GridFilterKeepsMatchingNodesAndParents()
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

    <TestMethod(DisplayName:="Grid value edit snapshot becomes one undo operation")>
    Public Sub GridValueEditSnapshotBecomesOneUndoOperation()
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

    <TestMethod(DisplayName:="UT-P2-GRD-001 Redo restores moved node order")>
    Public Sub P2GridRedoRestoresMovedNodeOrder()
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

    <TestMethod(DisplayName:="UT-P2-GRD-002 New grid operation clears Redo")>
    Public Sub P2GridRedoClearedByNewOperation()
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

    <TestMethod(DisplayName:="UT-P2-GRD-003 Duplicate creates unique sibling")>
    Public Sub P2GridDuplicateCreatesUniqueSibling()
        Dim parser = New JsonParserService()
        Dim serializer = New JsonTreeSerializer()
        Dim ops = New GridOperationService()
        Dim root = parser.Parse("{""item"":{""name"":""a""},""itemCopy"":{""name"":""existing""}}").Root

        Dim duplicate = ops.Duplicate(root, root.Children(0))
        AssertTrue(duplicate IsNot Nothing, "duplicate returned")
        AssertEqual("itemCopy1", duplicate.Key, "unique object key")
        AssertContains(serializer.Serialize(root), """itemCopy1"": {", "duplicate serialized")
    End Sub

    <TestMethod(DisplayName:="UT-P2-GRD-004 cross-parent move allows confirms and rejects")>
    Public Sub P2GridCrossParentMove()
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

    <TestMethod(DisplayName:="UT-P2-GRD-005 empty braces and brackets infer container types")>
    Public Sub P2GridInferContainerTypes()
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
End Class
