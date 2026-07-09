' SPDX-License-Identifier: MPL-2.0
Imports VisualJson.Core.Models

Namespace Services
    Public Class DocumentStateService
        Public Function CreateState(selectedPointer As String,
                                    expandedPointers As IEnumerable(Of String),
                                    Optional anchorPointer As String = "",
                                    Optional textCaretOffset As Integer = 0,
                                    Optional textScrollOffset As Double = 0) As GridViewState
            Return New GridViewState(selectedPointer, expandedPointers, anchorPointer, textCaretOffset, textScrollOffset)
        End Function

        Public Function ResolveRestoreTarget(root As JsonTreeNode, state As GridViewState) As JsonTreeNode
            If root Is Nothing Then
                Return Nothing
            End If

            If state Is Nothing Then
                Return root
            End If

            Dim target = ResolvePointerWithAncestors(root, state.SelectedPointer)
            If target IsNot Nothing Then
                Return target
            End If

            target = ResolvePointerWithAncestors(root, state.AnchorPointer)
            If target IsNot Nothing Then
                Return target
            End If

            Return root
        End Function

        Public Function FindNodeByPointer(root As JsonTreeNode, pointer As String) As JsonTreeNode
            If root Is Nothing Then
                Return Nothing
            End If

            If String.Equals(root.JsonPointer, If(pointer, ""), StringComparison.Ordinal) Then
                Return root
            End If

            For Each child In root.Children
                Dim found = FindNodeByPointer(child, pointer)
                If found IsNot Nothing Then
                    Return found
                End If
            Next

            Return Nothing
        End Function

        Public Function FindNodeAtOffset(root As JsonTreeNode, offset As Integer) As JsonTreeNode
            If root Is Nothing Then
                Return Nothing
            End If

            Dim safeOffset = Math.Max(0, offset)
            Dim best As JsonTreeNode = Nothing
            Dim bestStart = Integer.MinValue
            Dim bestDepth = -1
            VisitByOffset(root, safeOffset, 0, best, bestStart, bestDepth)
            Return If(best, root)
        End Function

        Public Function GetPointerAtOffset(root As JsonTreeNode, offset As Integer) As String
            Dim node = FindNodeAtOffset(root, offset)
            If node Is Nothing Then
                Return ""
            End If

            Return node.JsonPointer
        End Function

        Public Function GetPointerDisplay(root As JsonTreeNode, offset As Integer) As String
            Return ToPointerDisplay(GetPointerAtOffset(root, offset))
        End Function

        Public Shared Function ToPointerDisplay(pointer As String) As String
            If String.IsNullOrEmpty(pointer) Then
                Return "(root)"
            End If

            Return pointer
        End Function

        Public Shared Function GetAncestorPointers(pointer As String) As HashSet(Of String)
            Dim result = New HashSet(Of String)(StringComparer.Ordinal) From {""}
            Dim current = If(pointer, "")

            If current.Length = 0 Then
                Return result
            End If

            If Not current.StartsWith("/", StringComparison.Ordinal) Then
                Return result
            End If

            Dim parts = current.Split("/"c)
            Dim path = ""
            For index = 1 To parts.Length - 1
                path &= "/" & parts(index)
                result.Add(path)
            Next

            Return result
        End Function

        Private Function ResolvePointerWithAncestors(root As JsonTreeNode, pointer As String) As JsonTreeNode
            Dim current = If(pointer, "")

            Do
                Dim node = FindNodeByPointer(root, current)
                If node IsNot Nothing Then
                    Return node
                End If

                If current.Length = 0 Then
                    Exit Do
                End If

                Dim slash = current.LastIndexOf("/"c)
                current = If(slash <= 0, "", current.Substring(0, slash))
            Loop

            Return Nothing
        End Function

        Private Shared Sub VisitByOffset(node As JsonTreeNode,
                                         offset As Integer,
                                         depth As Integer,
                                         ByRef best As JsonTreeNode,
                                         ByRef bestStart As Integer,
                                         ByRef bestDepth As Integer)
            Dim candidateStart = GetCandidateStart(node)
            If candidateStart.HasValue AndAlso candidateStart.Value <= offset Then
                If candidateStart.Value > bestStart OrElse
                    (candidateStart.Value = bestStart AndAlso depth > bestDepth) Then
                    best = node
                    bestStart = candidateStart.Value
                    bestDepth = depth
                End If
            End If

            For Each child In node.Children
                VisitByOffset(child, offset, depth + 1, best, bestStart, bestDepth)
            Next
        End Sub

        Private Shared Function GetCandidateStart(node As JsonTreeNode) As Integer?
            If node.SourceKeyStartIndex.HasValue Then
                Return node.SourceKeyStartIndex
            End If

            Return node.SourceStartIndex
        End Function
    End Class
End Namespace
