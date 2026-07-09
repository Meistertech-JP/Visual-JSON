' SPDX-License-Identifier: MPL-2.0
Imports VisualJson.Core.Models

Namespace Services
    Public Class GridFilterService
        Public Function Filter(root As JsonTreeNode, query As String) As JsonTreeNode
            If root Is Nothing Then
                Return Nothing
            End If

            If String.IsNullOrWhiteSpace(query) Then
                Return root
            End If

            Return FilterNode(root, query.Trim())
        End Function

        Private Function FilterNode(node As JsonTreeNode, query As String) As JsonTreeNode
            Dim matchesSelf = Matches(node, query)
            Dim clone = New JsonTreeNode(node.Key, node.DisplayPath, node.Kind, node.ValueText, node.JsonPointer) With {
                .SourceColumn = node.SourceColumn,
                .SourceLine = node.SourceLine,
                .SourceStartIndex = node.SourceStartIndex,
                .SourceEndIndex = node.SourceEndIndex,
                .SourceKeyStartIndex = node.SourceKeyStartIndex,
                .SourceKeyEndIndex = node.SourceKeyEndIndex
            }

            For Each child In node.Children
                Dim filtered = FilterNode(child, query)
                If filtered IsNot Nothing Then
                    clone.Children.Add(filtered)
                End If
            Next

            If matchesSelf OrElse clone.Children.Count > 0 Then
                Return clone
            End If

            Return Nothing
        End Function

        Private Shared Function Matches(node As JsonTreeNode, query As String) As Boolean
            Return ContainsIgnoreCase(node.Key, query) OrElse
                ContainsIgnoreCase(node.ValueText, query) OrElse
                ContainsIgnoreCase(node.TypeName, query) OrElse
                ContainsIgnoreCase(node.DisplayPath, query)
        End Function

        Private Shared Function ContainsIgnoreCase(value As String, query As String) As Boolean
            Return If(value, "").IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
        End Function
    End Class
End Namespace
