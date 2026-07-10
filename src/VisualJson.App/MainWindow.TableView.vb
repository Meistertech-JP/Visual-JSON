' SPDX-License-Identifier: MPL-2.0
Imports System.ComponentModel
Imports System.Windows.Threading
Imports VisualJson.App.UI
Imports VisualJson.App.ViewModels
Imports VisualJson.Core.Models
Imports VisualJson.Core.Services

' P2-4: Table View (FR-13-103: table display, cell editing, sorting moved out of MainWindow.xaml.vb).
Partial Class MainWindow

#Region "Event Handlers"

    Private Sub OpenTable_Click(sender As Object, e As RoutedEventArgs)
        Dim node = GetNodeFromSender(sender)
        If node IsNot Nothing Then
            OpenTableView(node)
        End If
    End Sub

    Private Sub TableGrid_Sorting(sender As Object, e As DataGridSortingEventArgs)
        ' Header sorting reorders the display only (FR-P2-301, P2-4d); the array
        ' children keep their structural order until "Apply to structure".
        e.Handled = True
        If _tableModel Is Nothing OrElse e.Column Is Nothing Then
            Return
        End If

        Dim ascending = Not (e.Column.SortDirection.HasValue AndAlso e.Column.SortDirection.Value = ComponentModel.ListSortDirection.Ascending)
        ApplyTableSort(e.Column.DisplayIndex - 1, ascending)
    End Sub

    Private Sub TableApplySort_Click(sender As Object, e As RoutedEventArgs)
        ApplyTableSortToStructure()
    End Sub

    Private Sub TableGrid_BeginningEdit(sender As Object, e As DataGridBeginningEditEventArgs)
        Dim row = TryCast(e.Row?.Item, TableRow)
        Dim cellIndex = If(e.Column Is Nothing, -1, e.Column.DisplayIndex - 1)
        If row Is Nothing OrElse cellIndex < 0 OrElse cellIndex >= row.Cells.Count OrElse Not CanEditGrid(showMessage:=False) Then
            e.Cancel = True
            Return
        End If

        Dim cell = row.Cells(cellIndex)
        If cell.IsContainer Then
            e.Cancel = True
            Return
        End If

        ' Missing cells are editable only as property columns of object rows
        ' (P2-4c materialization); the value column stays empty for object rows.
        If cell.IsMissing AndAlso _tableModel IsNot Nothing AndAlso cellIndex < _tableModel.Columns.Count Then
            Dim column = _tableModel.Columns(cellIndex)
            If column.IsValueColumn OrElse row.ElementNode Is Nothing OrElse row.ElementNode.Kind <> JsonNodeKind.ObjectValue Then
                e.Cancel = True
            End If
        End If
    End Sub

    Private Sub TableGrid_CellEditEnding(sender As Object, e As DataGridCellEditEndingEventArgs)
        If e.EditAction <> DataGridEditAction.Commit Then
            Return
        End If

        Dim row = TryCast(e.Row?.Item, TableRow)
        Dim editor = TryCast(e.EditingElement, TextBox)
        Dim cellIndex = If(e.Column Is Nothing, -1, e.Column.DisplayIndex - 1)
        If row Is Nothing OrElse editor Is Nothing OrElse cellIndex < 0 Then
            Return
        End If

        Dim text = editor.Text
        ' The tree rebuild re-binds the table; defer it until the DataGrid has
        ' left its edit transaction to avoid re-entrancy.
        Dispatcher.BeginInvoke(Sub() CommitTableCellEdit(row, cellIndex, text), DispatcherPriority.Background)
    End Sub

    Private Sub TableAddRow_Click(sender As Object, e As RoutedEventArgs)
        AddTableRow()
    End Sub

    Private Sub TableAddColumn_Click(sender As Object, e As RoutedEventArgs)
        If _tableModel Is Nothing Then
            Return
        End If

        Dim prompt = New TextPromptWindow(LocalText("Add Column", "列を追加"),
                                          LocalText("Property name. The property is only created on rows you edit.", "プロパティ名。値を入力した行にのみプロパティが作成されます。"),
                                          LocalText("OK", "OK"),
                                          LocalText("Cancel", "キャンセル")) With {.Owner = Me}
        If prompt.ShowDialog() <> True Then
            Return
        End If

        If Not AddTableColumn(prompt.InputText) Then
            MessageBox.Show(Me,
                            LocalText("The column name is empty or already exists.", "列名が空か、既に存在します。"),
                            LocalText("Add Column", "列を追加"),
                            MessageBoxButton.OK, MessageBoxImage.Information)
        End If
    End Sub

    Private Sub TableHelp_Click(sender As Object, e As RoutedEventArgs)
        MessageBox.Show(Me,
                        LocalText("Rows are array elements, columns are properties. Edit scalar cells directly; editing an empty cell creates the property on that row only. Column sorting changes the display only until you apply it to the structure. Use < List to return to the tree.",
                                  "行=配列要素、列=プロパティです。スカラーセルは直接編集できます。空セルへの入力はその行にのみプロパティを作成します。列ソートは「構造へ反映」するまで表示のみです。< List でツリー表示へ戻ります。"),
                        LocalText("Table view help", "テーブルビューのヘルプ"),
                        MessageBoxButton.OK, MessageBoxImage.Information)
    End Sub

    Private Sub TableBack_Click(sender As Object, e As RoutedEventArgs)
        Dim pointer As String = Nothing
        Dim row = TryCast(TableGrid.SelectedItem, TableRow)
        If row?.ElementNode IsNot Nothing Then
            pointer = row.ElementNode.JsonPointer
        End If

        If pointer Is Nothing Then
            pointer = _tableModel?.SourcePointer
        End If

        CloseTableView(pointer)
    End Sub

    Private Sub TableGrid_MouseDoubleClick(sender As Object, e As MouseButtonEventArgs)
        If _tableModel Is Nothing Then
            Return
        End If

        Dim row = TryCast(TableGrid.SelectedItem, TableRow)
        Dim column = TableGrid.CurrentCell.Column
        If row Is Nothing OrElse column Is Nothing OrElse column.DisplayIndex <= 0 Then
            Return
        End If

        Dim cellIndex = column.DisplayIndex - 1
        If cellIndex >= row.Cells.Count Then
            Return
        End If

        Dim cell = row.Cells(cellIndex)
        If cell.IsContainer Then
            CloseTableView(cell.Node.JsonPointer)
        End If
    End Sub

#End Region

#Region "Private Helpers"

    Private Sub OpenTableView(node As JsonTreeNode, Optional showDialogs As Boolean = True)
        If node Is Nothing OrElse _gridDisabled Then
            Return
        End If

        If GridFilterBox IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(GridFilterBox.Text) Then
            If showDialogs Then
                MessageBox.Show(Me,
                                LocalText("Clear the grid filter before opening the table view. Filtered rows are a read-only view.", "テーブルビューを開く前にグリッドのフィルタを解除してください。フィルタ結果は読み取り専用ビューです。"),
                                LocalText("Filter active", "フィルタ適用中"),
                                MessageBoxButton.OK, MessageBoxImage.Information)
            End If
            Return
        End If

        If TableViewModelBuilder.ExceedsRowLimit(node) Then
            If showDialogs Then
                MessageBox.Show(Me,
                                String.Format(LocalText("This array has {0:N0} rows. Table view supports up to {1:N0} rows, so the tree view stays active.", "この配列は{0:N0}行です。テーブルビューは{1:N0}行まで対応のため、ツリー表示のままにします。"), node.Children.Count, TableViewModelBuilder.MaxRows),
                                LocalText("Table view", "テーブルビュー"),
                                MessageBoxButton.OK, MessageBoxImage.Warning)
            End If
            AddLog($"Table view rejected for {node.PointerDisplay}: {node.Children.Count} rows exceed the {TableViewModelBuilder.MaxRows} row limit.")
            Return
        End If

        If Not _tableBuilder.CanBuild(node) Then
            Return
        End If

        _tableExtraColumns.Clear()
        _tableSortColumnIndex = -2
        _tableModel = _tableBuilder.Build(node)
        BindTableModel(_tableModel)
        TablePanel.Visibility = Visibility.Visible
        GridTreePanel.Visibility = Visibility.Collapsed
        _viewModel.CurrentMode = "Table"
        UpdatePointerStatus(node.JsonPointer)
        AddLog($"Table view opened for {node.PointerDisplay} ({_tableModel.Rows.Count} rows, {_tableModel.Columns.Count} columns).")
    End Sub

    Private Sub BindTableModel(model As TableModel)
        TableGrid.Columns.Clear()
        TableGrid.Columns.Add(New DataGridTextColumn With {
            .Header = "#",
            .Binding = New Binding(NameOf(TableRow.RowNumber)),
            .IsReadOnly = True,
            .MinWidth = 44
        })

        For index = 0 To model.Columns.Count - 1
            ' EditText has a no-op setter so the column stays editable; the real
            ' commit is routed through TableGrid_CellEditEnding.
            TableGrid.Columns.Add(New DataGridTextColumn With {
                .Header = model.Columns(index).Name,
                .Binding = New Binding($"Cells[{index}].EditText"),
                .MinWidth = 80
            })
        Next

        _tableViewRows = model.Rows
        TableGrid.ItemsSource = _tableViewRows
        If _tableSortColumnIndex >= 0 Then
            ApplyTableSort(_tableSortColumnIndex, _tableSortAscending)
        End If

        UpdateTableApplySortButton()
        UpdateTableSubjectText()
    End Sub

    Private Sub ApplyTableSort(columnIndex As Integer, ascending As Boolean)
        If _tableModel Is Nothing Then
            Return
        End If

        If columnIndex < 0 OrElse columnIndex >= _tableModel.Columns.Count Then
            ' The "#" column restores the structural order and clears the sort.
            _tableSortColumnIndex = -2
            _tableViewRows = _tableModel.Rows
        Else
            _tableSortColumnIndex = columnIndex
            _tableSortAscending = ascending
            _tableViewRows = _tableBuilder.SortRows(_tableModel, columnIndex, ascending)
        End If

        TableGrid.ItemsSource = _tableViewRows

        For Each column In TableGrid.Columns
            If _tableSortColumnIndex >= 0 AndAlso column.DisplayIndex = _tableSortColumnIndex + 1 Then
                column.SortDirection = If(_tableSortAscending, ComponentModel.ListSortDirection.Ascending, ComponentModel.ListSortDirection.Descending)
            Else
                column.SortDirection = Nothing
            End If
        Next

        UpdateTableApplySortButton()
    End Sub

    ''' doc03 §7: the apply button is emphasized while a display sort is pending.
    Private Sub UpdateTableApplySortButton()
        Dim pending = _tableModel IsNot Nothing AndAlso _tableSortColumnIndex >= 0
        TableApplySortButton.IsEnabled = pending
        TableApplySortButton.FontWeight = If(pending, FontWeights.SemiBold, FontWeights.Normal)
        If pending Then
            TableApplySortButton.Background = New SolidColorBrush(Color.FromRgb(&HFD, &HE6, &H8A))
        Else
            TableApplySortButton.ClearValue(Button.BackgroundProperty)
        End If
    End Sub

    Private Function ApplyTableSortToStructure() As Boolean
        If _tableModel Is Nothing OrElse _tableSortColumnIndex < 0 OrElse _tableViewRows Is Nothing OrElse Not CanEditGrid(showMessage:=False) Then
            Return False
        End If

        CaptureGridUndo()
        If Not _tableBuilder.ApplySortToStructure(_tableModel.SourceNode, _tableViewRows) Then
            Return False
        End If

        Dim sourcePointer = _tableModel.SourcePointer
        _tableSortColumnIndex = -2
        AfterGridOperation($"Table sort applied to structure at {_tableModel.SourceNode.PointerDisplay}.", sourcePointer)
        Return True
    End Function

    Private Sub SelectTableRowByStructureIndex(structureIndex As Integer)
        If _tableViewRows Is Nothing Then
            Return
        End If

        For Each row In _tableViewRows
            If row.Index = structureIndex Then
                TableGrid.SelectedItem = row
                Return
            End If
        Next
    End Sub

    Private Function CommitTableCellEdit(row As TableRow, columnIndex As Integer, text As String) As Boolean
        If _tableModel Is Nothing OrElse row Is Nothing OrElse columnIndex < 0 OrElse columnIndex >= row.Cells.Count Then
            Return False
        End If

        Dim cell = row.Cells(columnIndex)
        If cell.IsContainer OrElse Not CanEditGrid(showMessage:=False) Then
            Return False
        End If

        If String.Equals(cell.DisplayText, text, StringComparison.Ordinal) Then
            Return False
        End If

        If cell.IsMissing AndAlso columnIndex < _tableModel.Columns.Count Then
            Dim column = _tableModel.Columns(columnIndex)
            If column.IsValueColumn OrElse row.ElementNode Is Nothing OrElse row.ElementNode.Kind <> JsonNodeKind.ObjectValue Then
                Return False
            End If
        End If

        CaptureGridUndo()
        Dim edited = _tableBuilder.ApplyCellEdit(_tableModel, row, columnIndex, text)
        If edited Is Nothing Then
            Return False
        End If

        Dim rowIndex = row.Index
        AfterGridOperation($"Table cell edited at {edited.PointerDisplay}.", edited.JsonPointer)
        SelectTableRowByStructureIndex(rowIndex)
        Return True
    End Function

    Private Sub UpdateTableSubjectText()
        If _tableModel Is Nothing Then
            TableSubjectText.Text = ""
            Return
        End If

        TableSubjectText.Text = String.Format(LocalText("Target: {0} ({1:N0} rows)", "対象: {0}({1:N0}行)"),
                                              _tableModel.SourceNode.PointerDisplay, _tableModel.Rows.Count)
    End Sub

    Private Function AddTableRow() As Boolean
        If _tableModel Is Nothing OrElse Not CanEditGrid(showMessage:=False) Then
            Return False
        End If

        If _tableModel.Rows.Count >= TableViewModelBuilder.MaxRows Then
            MessageBox.Show(Me,
                            String.Format(LocalText("The table already has {0:N0} rows, which is the table view limit.", "テーブルは既に上限の{0:N0}行です。"), TableViewModelBuilder.MaxRows),
                            LocalText("Table view", "テーブルビュー"),
                            MessageBoxButton.OK, MessageBoxImage.Warning)
            Return False
        End If

        CaptureGridUndo()
        Dim added = _tableBuilder.AddRow(_tableModel.SourceNode)
        If added Is Nothing Then
            Return False
        End If

        AfterGridOperation($"Table row added at {added.PointerDisplay}.", added.JsonPointer)
        If _tableModel IsNot Nothing AndAlso _tableModel.Rows.Count > 0 Then
            SelectTableRowByStructureIndex(_tableModel.Rows.Count - 1)
        End If

        Return True
    End Function

    ''' Adds a display-only column; no document change (and no undo entry) until a
    ''' cell in the column is edited (FR-P2-301: 列追加は当該行のみ実体化).
    Private Function AddTableColumn(name As String) As Boolean
        If _tableModel Is Nothing Then
            Return False
        End If

        Dim trimmed = If(name, "").Trim()
        If String.IsNullOrEmpty(trimmed) OrElse String.Equals(trimmed, TableViewModelBuilder.ValueColumnName, StringComparison.Ordinal) Then
            Return False
        End If

        If _tableModel.Columns.Any(Function(column) String.Equals(column.Name, trimmed, StringComparison.Ordinal)) Then
            Return False
        End If

        _tableExtraColumns.Add(trimmed)
        _tableModel = _tableBuilder.Build(_tableModel.SourceNode, _tableExtraColumns)
        BindTableModel(_tableModel)
        AddLog($"Table column added: {trimmed}.")
        Return True
    End Function

    Private Sub CloseTableView(Optional selectPointer As String = Nothing)
        _tableModel = Nothing
        _tableExtraColumns.Clear()
        _tableSortColumnIndex = -2
        _tableViewRows = Nothing
        TableGrid.ItemsSource = Nothing
        TableGrid.Columns.Clear()
        TableSubjectText.Text = ""
        TablePanel.Visibility = Visibility.Collapsed
        GridTreePanel.Visibility = Visibility.Visible
        _viewModel.CurrentMode = "Grid"

        If selectPointer IsNot Nothing Then
            SelectPointerInGrid(selectPointer, bringIntoView:=True)
        End If
    End Sub

    ''' Re-binds the open table after the document tree is rebuilt (revalidation,
    ''' undo/redo, tab switches). Falls back to the tree view when the target array
    ''' is gone or no longer a table candidate (doc04 §5).
    Private Sub RebindTableView(root As JsonTreeNode)
        If _tableModel Is Nothing OrElse TablePanel Is Nothing OrElse TablePanel.Visibility <> Visibility.Visible Then
            Return
        End If

        Dim pointer = _tableModel.SourcePointer
        Dim node = If(root Is Nothing, Nothing, _documentState.FindNodeByPointer(root, pointer))
        If node IsNot Nothing AndAlso _tableBuilder.CanBuild(node) Then
            _tableModel = _tableBuilder.Build(node, _tableExtraColumns)
            BindTableModel(_tableModel)
        Else
            CloseTableView(If(node IsNot Nothing, pointer, Nothing))
            AddLog(LocalText("Table view closed because the target array changed.", "対象配列が変化したためテーブルビューを閉じました。"))
        End If
    End Sub

#End Region

End Class
