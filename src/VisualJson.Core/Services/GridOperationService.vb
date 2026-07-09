' SPDX-License-Identifier: MPL-2.0
Imports VisualJson.Core.Models

Namespace Services
    ''' FR-P2-303: validation outcome for a drag-and-drop move across parents.
    Public Enum CrossParentMoveStatus
        Allowed
        ''' Target object already has a child with the source key; the UI must
        ''' confirm before the move proceeds with a uniquified key.
        KeyConflict
        ''' Moving a node into itself or one of its own descendants is forbidden.
        IntoOwnDescendant
        Invalid
    End Enum

    Public Class GridOperationService
        Public Function AddChild(target As JsonTreeNode) As JsonTreeNode
            If target Is Nothing Then
                Throw New ArgumentNullException(NameOf(target))
            End If

            If target.Kind <> JsonNodeKind.ObjectValue AndAlso target.Kind <> JsonNodeKind.ArrayValue Then
                target.Kind = JsonNodeKind.ObjectValue
                target.ValueText = ""
            End If

            Dim child As JsonTreeNode
            If target.Kind = JsonNodeKind.ArrayValue Then
                child = New JsonTreeNode($"[{target.Children.Count}]", $"{target.DisplayPath}[{target.Children.Count}]", JsonNodeKind.StringValue, "", $"{target.JsonPointer}/{target.Children.Count}")
            Else
                Dim key = GetUniqueObjectKey(target, "newProperty")
                child = New JsonTreeNode(key, $"{target.DisplayPath}.{key}", JsonNodeKind.StringValue, "", $"{target.JsonPointer}/{JsonTreeNode.EscapePointerSegment(key)}")
            End If

            target.Children.Add(child)
            Return child
        End Function

        Public Function AddSibling(root As JsonTreeNode, target As JsonTreeNode) As JsonTreeNode
            If root Is Nothing OrElse target Is Nothing Then
                Throw New ArgumentNullException(NameOf(target))
            End If

            Dim parent = FindParent(root, target)
            If parent Is Nothing Then
                Return AddChild(target)
            End If

            Dim insertAt = parent.Children.IndexOf(target) + 1
            Dim sibling As JsonTreeNode
            If parent.Kind = JsonNodeKind.ArrayValue Then
                sibling = New JsonTreeNode($"[{insertAt}]", $"{parent.DisplayPath}[{insertAt}]", JsonNodeKind.StringValue, "", $"{parent.JsonPointer}/{insertAt}")
            Else
                Dim key = GetUniqueObjectKey(parent, $"{target.Key}Copy")
                sibling = New JsonTreeNode(key, $"{parent.DisplayPath}.{key}", JsonNodeKind.StringValue, "", $"{parent.JsonPointer}/{JsonTreeNode.EscapePointerSegment(key)}")
            End If

            parent.Children.Insert(insertAt, sibling)
            ReindexArrays(root)
            Return sibling
        End Function

        Public Function Delete(root As JsonTreeNode, target As JsonTreeNode) As Boolean
            Dim parent = FindParent(root, target)
            If parent Is Nothing Then
                Return False
            End If

            Dim removed = parent.Children.Remove(target)
            If removed Then
                ReindexArrays(root)
            End If

            Return removed
        End Function

        Public Function MoveUp(root As JsonTreeNode, target As JsonTreeNode) As Boolean
            Return MoveBy(root, target, -1)
        End Function

        Public Function MoveDown(root As JsonTreeNode, target As JsonTreeNode) As Boolean
            Return MoveBy(root, target, 1)
        End Function

        Public Function Duplicate(root As JsonTreeNode, target As JsonTreeNode) As JsonTreeNode
            If root Is Nothing OrElse target Is Nothing Then
                Return Nothing
            End If

            Dim parent = FindParent(root, target)
            If parent Is Nothing Then
                Return Nothing
            End If

            Dim insertAt = parent.Children.IndexOf(target) + 1
            If insertAt <= 0 Then
                Return Nothing
            End If

            Dim cloneKey As String
            If parent.Kind = JsonNodeKind.ArrayValue Then
                cloneKey = $"[{insertAt}]"
            Else
                cloneKey = GetUniqueObjectKey(parent, $"{target.Key}Copy")
            End If

            Dim clone = CloneForParent(target, cloneKey, parent.DisplayPath, parent.JsonPointer, insertAt)
            parent.Children.Insert(insertAt, clone)
            ReindexArrays(root)
            Return clone
        End Function

        Public Function MoveBefore(root As JsonTreeNode, source As JsonTreeNode, target As JsonTreeNode) As Boolean
            If source Is Nothing OrElse target Is Nothing OrElse Object.ReferenceEquals(source, target) Then
                Return False
            End If

            Dim sourceParent = FindParent(root, source)
            Dim targetParent = FindParent(root, target)
            If sourceParent Is Nothing OrElse targetParent Is Nothing OrElse Not Object.ReferenceEquals(sourceParent, targetParent) Then
                Return False
            End If

            Dim oldIndex = sourceParent.Children.IndexOf(source)
            Dim newIndex = sourceParent.Children.IndexOf(target)
            If oldIndex < 0 OrElse newIndex < 0 Then
                Return False
            End If

            sourceParent.Children.Move(oldIndex, newIndex)
            ReindexArrays(root)
            Return True
        End Function

        ''' FR-P2-303: classifies a move of `source` to just before `target`,
        ''' which may live under a different parent.
        Public Function CheckMoveBefore(root As JsonTreeNode, source As JsonTreeNode, target As JsonTreeNode) As CrossParentMoveStatus
            If source Is Nothing OrElse target Is Nothing OrElse Object.ReferenceEquals(source, target) Then
                Return CrossParentMoveStatus.Invalid
            End If

            Dim sourceParent = FindParent(root, source)
            Dim targetParent = FindParent(root, target)
            If sourceParent Is Nothing OrElse targetParent Is Nothing Then
                Return CrossParentMoveStatus.Invalid
            End If

            If IsSelfOrDescendant(source, targetParent) Then
                Return CrossParentMoveStatus.IntoOwnDescendant
            End If

            If Object.ReferenceEquals(sourceParent, targetParent) Then
                Return CrossParentMoveStatus.Allowed
            End If

            If targetParent.Kind = JsonNodeKind.ObjectValue AndAlso
               targetParent.Children.Any(Function(child) String.Equals(child.Key, source.Key, StringComparison.Ordinal)) Then
                Return CrossParentMoveStatus.KeyConflict
            End If

            Return CrossParentMoveStatus.Allowed
        End Function

        ''' FR-P2-303: moves `source` before `target` across parents. A key conflict
        ''' is resolved by uniquifying the key — the confirmation belongs to the UI,
        ''' which must not call this after IntoOwnDescendant/Invalid.
        Public Function MoveBeforeAcrossParents(root As JsonTreeNode, source As JsonTreeNode, target As JsonTreeNode) As Boolean
            Dim check = CheckMoveBefore(root, source, target)
            If check = CrossParentMoveStatus.Invalid OrElse check = CrossParentMoveStatus.IntoOwnDescendant Then
                Return False
            End If

            Dim sourceParent = FindParent(root, source)
            Dim targetParent = FindParent(root, target)
            If Object.ReferenceEquals(sourceParent, targetParent) Then
                Return MoveBefore(root, source, target)
            End If

            sourceParent.Children.Remove(source)
            Dim insertAt = targetParent.Children.IndexOf(target)
            If insertAt < 0 Then
                insertAt = targetParent.Children.Count
            End If

            If targetParent.Kind = JsonNodeKind.ObjectValue Then
                Dim baseKey = If(String.IsNullOrEmpty(source.Key) OrElse source.Key.StartsWith("[", StringComparison.Ordinal), "item", source.Key)
                If targetParent.Children.Any(Function(child) String.Equals(child.Key, baseKey, StringComparison.Ordinal)) Then
                    source.Key = GetUniqueObjectKey(targetParent, baseKey)
                Else
                    source.Key = baseKey
                End If
            End If

            targetParent.Children.Insert(insertAt, source)
            ReindexArrays(root)
            Return True
        End Function

        Private Shared Function IsSelfOrDescendant(source As JsonTreeNode, candidate As JsonTreeNode) As Boolean
            If Object.ReferenceEquals(source, candidate) Then
                Return True
            End If

            For Each child In source.Children
                If IsSelfOrDescendant(child, candidate) Then
                    Return True
                End If
            Next

            Return False
        End Function

        Public Sub ChangeType(target As JsonTreeNode, kind As JsonNodeKind)
            If target Is Nothing Then
                Return
            End If

            target.Kind = kind
            target.Children.Clear()

            Select Case kind
                Case JsonNodeKind.ObjectValue, JsonNodeKind.ArrayValue
                    target.ValueText = ""
                Case JsonNodeKind.StringValue
                    target.ValueText = If(target.ValueText, "")
                Case JsonNodeKind.NumberValue
                    target.ValueText = "0"
                Case JsonNodeKind.BooleanValue
                    target.ValueText = "false"
                Case JsonNodeKind.NullValue
                    target.ValueText = "null"
            End Select
        End Sub

        Public Function FindParent(root As JsonTreeNode, target As JsonTreeNode) As JsonTreeNode
            If root Is Nothing OrElse target Is Nothing Then
                Return Nothing
            End If

            For Each child In root.Children
                If Object.ReferenceEquals(child, target) Then
                    Return root
                End If

                Dim found = FindParent(child, target)
                If found IsNot Nothing Then
                    Return found
                End If
            Next

            Return Nothing
        End Function

        Public Function CloneTree(root As JsonTreeNode) As JsonTreeNode
            If root Is Nothing Then
                Return Nothing
            End If

            Dim clone = New JsonTreeNode(root.Key, root.DisplayPath, root.Kind, root.ValueText, root.JsonPointer) With {
                .SourceColumn = root.SourceColumn,
                .SourceLine = root.SourceLine,
                .SourceStartIndex = root.SourceStartIndex,
                .SourceEndIndex = root.SourceEndIndex,
                .SourceKeyStartIndex = root.SourceKeyStartIndex,
                .SourceKeyEndIndex = root.SourceKeyEndIndex
            }

            For Each child In root.Children
                clone.Children.Add(CloneTree(child))
            Next

            Return clone
        End Function

        Private Function CloneForParent(source As JsonTreeNode, key As String, parentDisplayPath As String, parentPointer As String, arrayIndex As Integer) As JsonTreeNode
            Dim isArrayKey = key.StartsWith("[", StringComparison.Ordinal)
            Dim pointerSegment = If(isArrayKey, arrayIndex.ToString(Globalization.CultureInfo.InvariantCulture), JsonTreeNode.EscapePointerSegment(key))
            Dim displayPath = If(String.IsNullOrEmpty(parentDisplayPath), key, If(isArrayKey, $"{parentDisplayPath}{key}", $"{parentDisplayPath}.{key}"))
            Dim pointer = If(String.IsNullOrEmpty(parentPointer), $"/{pointerSegment}", $"{parentPointer}/{pointerSegment}")
            Dim clone = New JsonTreeNode(key, displayPath, source.Kind, source.ValueText, pointer) With {
                .SourceColumn = source.SourceColumn,
                .SourceLine = source.SourceLine,
                .SourceStartIndex = source.SourceStartIndex,
                .SourceEndIndex = source.SourceEndIndex,
                .SourceKeyStartIndex = source.SourceKeyStartIndex,
                .SourceKeyEndIndex = source.SourceKeyEndIndex
            }

            For index = 0 To source.Children.Count - 1
                Dim child = source.Children(index)
                Dim childKey = If(source.Kind = JsonNodeKind.ArrayValue, $"[{index}]", child.Key)
                clone.Children.Add(CloneForParent(child, childKey, clone.DisplayPath, clone.JsonPointer, index))
            Next

            Return clone
        End Function

        Private Function MoveBy(root As JsonTreeNode, target As JsonTreeNode, delta As Integer) As Boolean
            Dim parent = FindParent(root, target)
            If parent Is Nothing Then
                Return False
            End If

            Dim current = parent.Children.IndexOf(target)
            Dim nextIndex = current + delta
            If current < 0 OrElse nextIndex < 0 OrElse nextIndex >= parent.Children.Count Then
                Return False
            End If

            parent.Children.Move(current, nextIndex)
            ReindexArrays(root)
            Return True
        End Function

        Private Sub ReindexArrays(root As JsonTreeNode)
            If root Is Nothing Then
                Return
            End If

            If root.Kind = JsonNodeKind.ArrayValue Then
                For index = 0 To root.Children.Count - 1
                    root.Children(index).Key = $"[{index}]"
                Next
            End If

            For Each child In root.Children
                ReindexArrays(child)
            Next
        End Sub

        Private Shared Function GetUniqueObjectKey(parent As JsonTreeNode, baseKey As String) As String
            Dim candidate = If(String.IsNullOrWhiteSpace(baseKey), "newProperty", baseKey)
            Dim index = 1

            While parent.Children.Any(Function(child) String.Equals(child.Key, candidate, StringComparison.Ordinal))
                candidate = $"{baseKey}{index}"
                index += 1
            End While

            Return candidate
        End Function
    End Class
End Namespace
