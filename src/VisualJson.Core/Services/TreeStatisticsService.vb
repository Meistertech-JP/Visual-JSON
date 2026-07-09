' SPDX-License-Identifier: MPL-2.0
Imports VisualJson.Core.Models

Namespace Services
    Public Class TreeStatisticsService
        Public Function CountNodes(root As JsonTreeNode) As Integer
            If root Is Nothing Then
                Return 0
            End If

            Dim total = 1
            For Each child In root.Children
                total += CountNodes(child)
            Next

            Return total
        End Function
    End Class
End Namespace
