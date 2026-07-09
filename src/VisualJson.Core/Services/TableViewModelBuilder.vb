' SPDX-License-Identifier: MPL-2.0
Imports VisualJson.Core.Models

Namespace Services
    ''' FR-P2-301 (P2-4a): builds a row/column table over an object-majority array.
    Public Class TableViewModelBuilder
        Public Const MaxRows As Integer = 10000
        Public Const ValueColumnName As String = "(value)"

        Private ReadOnly _inference As New TypeInferenceService()

        ''' Candidate check drives the grid Action column "Table" button (doc03 §4.2):
        ''' an array whose children are more than half objects. The row limit is
        ''' checked separately so the UI can warn instead of hiding the button.
        Public Shared Function IsCandidate(arrayNode As JsonTreeNode) As Boolean
            If arrayNode Is Nothing OrElse arrayNode.Kind <> JsonNodeKind.ArrayValue Then
                Return False
            End If

            Dim total = arrayNode.Children.Count
            If total = 0 Then
                Return False
            End If

            Dim objectCount = arrayNode.Children.Where(Function(child) child.Kind = JsonNodeKind.ObjectValue).Count()
            Return objectCount * 2 > total
        End Function

        Public Shared Function ExceedsRowLimit(arrayNode As JsonTreeNode) As Boolean
            Return arrayNode IsNot Nothing AndAlso arrayNode.Children.Count > MaxRows
        End Function

        Public Function CanBuild(arrayNode As JsonTreeNode) As Boolean
            Return IsCandidate(arrayNode) AndAlso Not ExceedsRowLimit(arrayNode)
        End Function

        ''' FR-P2-301 (P2-4b/c): edits a scalar cell with the shared type inference
        ''' rules. A missing property cell is materialized on that row only; container
        ''' cells and the value column of object rows are rejected.
        Public Function ApplyCellEdit(model As TableModel, row As TableRow, columnIndex As Integer, text As String) As JsonTreeNode
            If model Is Nothing OrElse row Is Nothing OrElse columnIndex < 0 OrElse columnIndex >= row.Cells.Count OrElse columnIndex >= model.Columns.Count Then
                Return Nothing
            End If

            Dim cell = row.Cells(columnIndex)
            If cell.IsContainer Then
                Return Nothing
            End If

            If cell.IsMissing Then
                Dim column = model.Columns(columnIndex)
                Dim element = row.ElementNode
                If column.IsValueColumn OrElse element Is Nothing OrElse element.Kind <> JsonNodeKind.ObjectValue Then
                    Return Nothing
                End If

                Dim key = column.Name
                Dim child = New JsonTreeNode(key, $"{element.DisplayPath}.{key}", JsonNodeKind.StringValue, If(text, ""), $"{element.JsonPointer}/{JsonTreeNode.EscapePointerSegment(key)}")
                _inference.ApplyToNode(child)
                element.Children.Add(child)
                Return child
            End If

            Dim node = cell.Node
            node.ValueText = If(text, "")
            _inference.ApplyToNode(node)
            Return node
        End Function

        ''' FR-P2-301 (P2-4d): returns rows sorted by one column for display only —
        ''' the source array children are not touched. Comparison is type-aware:
        ''' missing < null < boolean < number < string < containers.
        Public Function SortRows(model As TableModel, columnIndex As Integer, ascending As Boolean) As IReadOnlyList(Of TableRow)
            If model Is Nothing Then
                Return Array.Empty(Of TableRow)()
            End If

            If columnIndex < 0 OrElse columnIndex >= model.Columns.Count Then
                Return model.Rows
            End If

            Dim comparer = New TableCellComparer()
            Dim ordered As IEnumerable(Of TableRow)
            If ascending Then
                ordered = model.Rows.OrderBy(Function(row) row.Cells(columnIndex), comparer)
            Else
                ordered = model.Rows.OrderByDescending(Function(row) row.Cells(columnIndex), comparer)
            End If

            Return ordered.ToList()
        End Function

        Private Class TableCellComparer
            Implements IComparer(Of TableCell)

            Public Function Compare(x As TableCell, y As TableCell) As Integer Implements IComparer(Of TableCell).Compare
                Dim rankX = Rank(x)
                Dim rankY = Rank(y)
                If rankX <> rankY Then
                    Return rankX.CompareTo(rankY)
                End If

                Select Case rankX
                    Case 2
                        Return String.CompareOrdinal(x.Node.ValueText, y.Node.ValueText) ' "false" < "true"
                    Case 3
                        Dim numberX As Double
                        Dim numberY As Double
                        If Double.TryParse(x.Node.ValueText, Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture, numberX) AndAlso
                           Double.TryParse(y.Node.ValueText, Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture, numberY) Then
                            Return numberX.CompareTo(numberY)
                        End If

                        Return String.CompareOrdinal(x.Node.ValueText, y.Node.ValueText)
                    Case Else
                        Return String.CompareOrdinal(x.DisplayText, y.DisplayText)
                End Select
            End Function

            Private Shared Function Rank(cell As TableCell) As Integer
                If cell Is Nothing OrElse cell.IsMissing Then
                    Return 0
                End If

                Select Case cell.Node.Kind
                    Case JsonNodeKind.NullValue
                        Return 1
                    Case JsonNodeKind.BooleanValue
                        Return 2
                    Case JsonNodeKind.NumberValue
                        Return 3
                    Case JsonNodeKind.StringValue
                        Return 4
                    Case Else
                        Return 5
                End Select
            End Function
        End Class

        ''' FR-P2-301 (P2-4e): writes the display order into the array children and
        ''' renumbers the array keys. Rejected unless the order is exactly the same
        ''' node set as the array. The caller wraps this in one undo unit.
        Public Function ApplySortToStructure(arrayNode As JsonTreeNode, order As IEnumerable(Of TableRow)) As Boolean
            If arrayNode Is Nothing OrElse arrayNode.Kind <> JsonNodeKind.ArrayValue OrElse order Is Nothing Then
                Return False
            End If

            Dim ordered = order.Select(Function(row) row?.ElementNode).ToList()
            If ordered.Count <> arrayNode.Children.Count Then
                Return False
            End If

            Dim remaining = New HashSet(Of JsonTreeNode)(arrayNode.Children)
            For Each node In ordered
                If node Is Nothing OrElse Not remaining.Remove(node) Then
                    Return False
                End If
            Next

            If remaining.Count > 0 Then
                Return False
            End If

            arrayNode.Children.Clear()
            For index = 0 To ordered.Count - 1
                Dim child = ordered(index)
                child.Key = $"[{index}]"
                arrayNode.Children.Add(child)
            Next

            Return True
        End Function

        ''' FR-P2-301 (P2-4c): appends an empty object row to the source array.
        Public Function AddRow(arrayNode As JsonTreeNode) As JsonTreeNode
            If arrayNode Is Nothing OrElse arrayNode.Kind <> JsonNodeKind.ArrayValue Then
                Return Nothing
            End If

            Dim index = arrayNode.Children.Count
            Dim element = New JsonTreeNode($"[{index}]", $"{arrayNode.DisplayPath}[{index}]", JsonNodeKind.ObjectValue, "", $"{arrayNode.JsonPointer}/{index}")
            arrayNode.Children.Add(element)
            Return element
        End Function

        Public Function Build(arrayNode As JsonTreeNode) As TableModel
            Return Build(arrayNode, Nothing)
        End Function

        ''' extraColumns: display-only columns added via "+ Column" (P2-4c). They are
        ''' kept across rebuilds until a cell edit materializes them as properties.
        Public Function Build(arrayNode As JsonTreeNode, extraColumns As IEnumerable(Of String)) As TableModel
            If Not CanBuild(arrayNode) Then
                Throw New InvalidOperationException("The node is not a table-view candidate.")
            End If

            Dim columnNames = New List(Of String)()
            Dim seenColumns = New HashSet(Of String)(StringComparer.Ordinal)
            Dim hasNonObject = False
            For Each element In arrayNode.Children
                If element.Kind = JsonNodeKind.ObjectValue Then
                    For Each prop In element.Children
                        If seenColumns.Add(prop.Key) Then
                            columnNames.Add(prop.Key)
                        End If
                    Next
                Else
                    hasNonObject = True
                End If
            Next

            If extraColumns IsNot Nothing Then
                For Each extra In extraColumns
                    If Not String.IsNullOrWhiteSpace(extra) AndAlso Not String.Equals(extra, ValueColumnName, StringComparison.Ordinal) AndAlso seenColumns.Add(extra) Then
                        columnNames.Add(extra)
                    End If
                Next
            End If

            Dim columns = New List(Of TableColumn)(columnNames.Count + 1)
            For Each name In columnNames
                columns.Add(New TableColumn(name, isValueColumn:=False))
            Next

            If hasNonObject Then
                columns.Add(New TableColumn(ValueColumnName, isValueColumn:=True))
            End If

            Dim rows = New List(Of TableRow)(arrayNode.Children.Count)
            Dim missingCell = New TableCell(Nothing)
            For index = 0 To arrayNode.Children.Count - 1
                Dim element = arrayNode.Children(index)
                Dim propertyMap As Dictionary(Of String, JsonTreeNode) = Nothing
                If element.Kind = JsonNodeKind.ObjectValue Then
                    propertyMap = New Dictionary(Of String, JsonTreeNode)(element.Children.Count, StringComparer.Ordinal)
                    For Each prop In element.Children
                        If Not propertyMap.ContainsKey(prop.Key) Then
                            propertyMap.Add(prop.Key, prop)
                        End If
                    Next
                End If

                Dim cells = New List(Of TableCell)(columns.Count)
                For Each column In columns
                    If column.IsValueColumn Then
                        cells.Add(If(propertyMap Is Nothing, New TableCell(element), missingCell))
                    ElseIf propertyMap Is Nothing Then
                        cells.Add(missingCell)
                    Else
                        Dim prop As JsonTreeNode = Nothing
                        cells.Add(If(propertyMap.TryGetValue(column.Name, prop), New TableCell(prop), missingCell))
                    End If
                Next

                rows.Add(New TableRow(index, element, cells))
            Next

            Return New TableModel(arrayNode, columns, rows)
        End Function
    End Class
End Namespace
