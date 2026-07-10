' SPDX-License-Identifier: MPL-2.0
Imports System.ComponentModel
Imports System.IO
Imports System.Windows.Threading
Imports VisualJson.App.UI
Imports VisualJson.App.ViewModels
Imports VisualJson.Core.Diagnostics
Imports VisualJson.Core.Models
Imports VisualJson.Core.Parsing
Imports VisualJson.Core.Services
Imports VisualJson.Core.Validation

' Automation hooks consumed by tools/VisualJson.Phase2Acceptance (names and signatures are a compatibility contract; see D-V13-0-A).
Partial Class MainWindow

#Region "Automation Hooks"

    Public Function OpenPathForAutomation(path As String) As Boolean
        Return OpenDocumentPath(path, addToRecent:=True, showDialogs:=False)
    End Function

    Public Function OpenDroppedFileForAutomation(path As String) As Boolean
        Return OpenDocumentPath(path, addToRecent:=True, showDialogs:=False)
    End Function

    Public Function OpenRecentForAutomation(path As String) As Boolean
        If Not File.Exists(path) Then
            _recentFiles.Remove(_settings, path)
            SaveSettings()
            RefreshRecentFilesMenu()
            Return False
        End If

        Return OpenDocumentPath(path, addToRecent:=True, showDialogs:=False)
    End Function

    Public Sub LoadTextForAutomation(text As String, Optional filePath As String = "")
        _validationTimer.Stop()
        _recoveryTimer.Stop()
        SetDocument(If(text, ""), filePath, isDirty:=False)
        _validationTimer.Stop()
        ValidateCurrentText(updateGrid:=True)
    End Sub

    Public Function ValidateForAutomation(Optional updateGrid As Boolean = True) As Boolean
        _validationTimer.Stop()
        Return ValidateCurrentText(updateGrid)
    End Function

    Public Sub SetCaretOffsetForAutomation(offset As Integer)
        _editor.SetCaretOffset(offset)
        UpdateCaretStatus()
    End Sub

    Public Function GetCaretOffsetForAutomation() As Integer
        Return _editor.GetCaretOffset()
    End Function

    Public Function SwitchToGridForAutomation() As Boolean
        Return ShowGridTab()
    End Function

    Public Sub SwitchToTextForAutomation()
        ShowTextTab()
    End Sub

    Public Function GetSelectedGridPointerForAutomation() As String
        Return GetSelectedGridPointer()
    End Function

    Public Function GetPointerStatusForAutomation() As String
        Return PointerStatusText.Text
    End Function

    Public Sub SetGridFilterForAutomation(query As String)
        GridFilterBox.Text = If(query, "")
        RefreshGridView()
    End Sub

    Public Function EditNodeValueForAutomation(pointer As String, value As String) As Boolean
        If Document.RootNode Is Nothing Then
            Return False
        End If

        Dim node = _documentState.FindNodeByPointer(Document.RootNode, pointer)
        If node Is Nothing OrElse Not node.CanEditValue Then
            Return False
        End If

        node.ValueText = If(value, "")
        SyncGridToText(showText:=False, focusPointer:=pointer)
        Return True
    End Function

    Public Function ReplaceAllForAutomation(searchText As String,
                                            replacementText As String,
                                            Optional matchCase As Boolean = False,
                                            Optional useRegex As Boolean = False) As Integer
        SearchBox.Text = If(searchText, "")
        ReplaceBox.Text = If(replacementText, "")
        CaseSensitiveBox.IsChecked = matchCase
        RegexSearchBox.IsChecked = useRegex
        Return ReplaceAllCurrent()
    End Function

    Public Function GetTextForAutomation() As String
        Return CurrentText()
    End Function

    Public Sub SetAutoPairingForAutomation(enabled As Boolean)
        AutoPairBox.IsChecked = enabled
        _editor.AutoPairingEnabled = enabled
    End Sub

    Public Sub SetLanguageForAutomation(language As String)
        For index = 0 To LanguageCombo.Items.Count - 1
            Dim item = TryCast(LanguageCombo.Items(index), ComboBoxItem)
            If item IsNot Nothing AndAlso String.Equals(TryCast(item.Tag, String), language, StringComparison.OrdinalIgnoreCase) Then
                LanguageCombo.SelectedIndex = index
                Return
            End If
        Next
    End Sub

    Public Function GetLanguageForAutomation() As String
        Return _language
    End Function

    Public Sub InsertCharacterForAutomation(ch As Char)
        _editor.InsertCharacterForAutomation(ch)
    End Sub

    Public Function SearchHighlightCountForAutomation(searchText As String,
                                                      Optional matchCase As Boolean = False,
                                                      Optional useRegex As Boolean = False) As Integer
        SearchBox.Text = If(searchText, "")
        CaseSensitiveBox.IsChecked = matchCase
        RegexSearchBox.IsChecked = useRegex
        Return UpdateSearchHighlights()
    End Function

    Public Function RefreshFoldingsForAutomation() As Integer
        Dim ranges = _foldingService.CreateRanges(CurrentText())
        _editor.ApplyJsonFoldings(ranges)
        Return _editor.GetFoldingCount()
    End Function

    Public Function CollapseFirstFoldingForAutomation() As Boolean
        Return _editor.CollapseFirstFolding()
    End Function

    Public Function GetFoldedCountForAutomation() As Integer
        Return _editor.GetFoldedCount()
    End Function

    Public Function SavePathForAutomation(path As String) As Boolean
        Document.CurrentFilePath = If(path, "")
        Return SaveCurrent(saveAs:=False, showDialogs:=False)
    End Function

    Public Function GetRecentFilesForAutomation() As IReadOnlyList(Of String)
        Return If(_settings.RecentFiles, New List(Of String)()).ToList()
    End Function

    Public Sub ClearRecentFilesForAutomation()
        _recentFiles.Clear(_settings)
        SaveSettings()
        RefreshRecentFilesMenu()
    End Sub

    Public Function UndoGridForAutomation() As Boolean
        Dim before = CurrentText()
        UndoGrid_Click(Me, New RoutedEventArgs())
        Return Not String.Equals(before, CurrentText(), StringComparison.Ordinal)
    End Function

    Public Function RedoGridForAutomation() As Boolean
        Dim before = CurrentText()
        RedoGrid_Click(Me, New RoutedEventArgs())
        Return Not String.Equals(before, CurrentText(), StringComparison.Ordinal)
    End Function

    Public Function DuplicateNodeForAutomation(pointer As String) As Boolean
        If Document.RootNode Is Nothing AndAlso Not ValidateCurrentText(updateGrid:=True) Then
            Return False
        End If

        Dim node = _documentState.FindNodeByPointer(Document.RootNode, pointer)
        If node Is Nothing Then
            Return False
        End If

        CaptureGridUndo()
        Dim duplicate = _gridOps.Duplicate(Document.RootNode, node)
        If duplicate Is Nothing Then
            Return False
        End If

        AfterGridOperation("Duplicated node.", duplicate.JsonPointer)
        Return True
    End Function

    Public Function OpenTableViewForAutomation(pointer As String) As Boolean
        Dim node = FindNodeByPointer(Document.RootNode, pointer)
        If node Is Nothing Then
            Return False
        End If

        OpenTableView(node, showDialogs:=False)
        Return TablePanel.Visibility = Visibility.Visible
    End Function

    Public Function IsTableViewOpenForAutomation() As Boolean
        Return TablePanel.Visibility = Visibility.Visible
    End Function

    Public Function GetTableShapeForAutomation() As String
        If _tableModel Is Nothing Then
            Return ""
        End If

        Return $"{_tableModel.Rows.Count}x{_tableModel.Columns.Count}"
    End Function

    Public Function GetTableCellTextForAutomation(rowIndex As Integer, columnName As String) As String
        If _tableModel Is Nothing OrElse rowIndex < 0 OrElse rowIndex >= _tableModel.Rows.Count Then
            Return Nothing
        End If

        For index = 0 To _tableModel.Columns.Count - 1
            If String.Equals(_tableModel.Columns(index).Name, columnName, StringComparison.Ordinal) Then
                Return _tableModel.Rows(rowIndex).Cells(index).DisplayText
            End If
        Next

        Return Nothing
    End Function

    Public Function EditTableCellForAutomation(rowIndex As Integer, columnName As String, text As String) As Boolean
        If _tableModel Is Nothing OrElse rowIndex < 0 OrElse rowIndex >= _tableModel.Rows.Count Then
            Return False
        End If

        For index = 0 To _tableModel.Columns.Count - 1
            If String.Equals(_tableModel.Columns(index).Name, columnName, StringComparison.Ordinal) Then
                Return CommitTableCellEdit(_tableModel.Rows(rowIndex), index, text)
            End If
        Next

        Return False
    End Function

    Public Function AddTableRowForAutomation() As Boolean
        Return AddTableRow()
    End Function

    Public Function ShowKeyCompletionForAutomation() As Integer
        Return ShowKeyCompletion()
    End Function

    Public Sub LoadSchemaTextForAutomation(schemaText As String, Optional sourceName As String = "automation-schema.json")
        _schemaText = schemaText
        _schemaSource = sourceName
        UpdateSchemaStatus()
    End Sub

    ''' Runs schema validation and returns "code|pointer|line" per diagnostic.
    Public Function ValidateSchemaForAutomation() As IReadOnlyList(Of String)
        RunSchemaValidation(showTab:=False)
        Dim diagnostics = TryCast(SchemaList.ItemsSource, IEnumerable(Of ValidationDiagnostic))
        If diagnostics Is Nothing Then
            Return Array.Empty(Of String)()
        End If

        Return diagnostics.
            Select(Function(item) $"{item.ErrorCode}|{item.JsonPointer}|{If(item.Line.HasValue, item.Line.Value.ToString(Globalization.CultureInfo.InvariantCulture), "")}").
            ToList()
    End Function

    Public Function CommitCompletionForAutomation() As Boolean
        Return _editor.CommitSelectedCompletion()
    End Function

    Public Function GetCompletionCandidatesForAutomation(pointer As String) As String
        Dim node = FindNodeByPointer(Document.RootNode, pointer)
        If node Is Nothing Then
            Return ""
        End If

        Return String.Join(",", _completionCandidates.GetKeyCandidates(Document.RootNode, node, _schemaText))
    End Function

    ''' FR-P2-303 D&D path without dialogs. Returns moved/keyConflict/descendant/invalid.
    Public Function MoveNodeForAutomation(sourcePointer As String, targetPointer As String, confirmKeyConflict As Boolean) As String
        Dim source = FindNodeByPointer(Document.RootNode, sourcePointer)
        Dim target = FindNodeByPointer(Document.RootNode, targetPointer)
        Dim check = _gridOps.CheckMoveBefore(Document.RootNode, source, target)
        Select Case check
            Case CrossParentMoveStatus.Invalid
                Return "invalid"
            Case CrossParentMoveStatus.IntoOwnDescendant
                Return "descendant"
            Case CrossParentMoveStatus.KeyConflict
                If Not confirmKeyConflict Then
                    Return "keyConflict"
                End If
        End Select

        CaptureGridUndo()
        If _gridOps.MoveBeforeAcrossParents(Document.RootNode, source, target) Then
            AfterGridOperation("Moved node by drag and drop.", source.JsonPointer)
            Return "moved"
        End If

        Return "invalid"
    End Function

    Public Function SortTableForAutomation(columnName As String, ascending As Boolean) As Boolean
        If _tableModel Is Nothing Then
            Return False
        End If

        For index = 0 To _tableModel.Columns.Count - 1
            If String.Equals(_tableModel.Columns(index).Name, columnName, StringComparison.Ordinal) Then
                ApplyTableSort(index, ascending)
                Return True
            End If
        Next

        Return False
    End Function

    Public Sub ClearTableSortForAutomation()
        ApplyTableSort(-1, ascending:=True)
    End Sub

    Public Function ApplyTableSortToStructureForAutomation() As Boolean
        Return ApplyTableSortToStructure()
    End Function

    Public Function IsTableSortPendingForAutomation() As Boolean
        Return _tableModel IsNot Nothing AndAlso _tableSortColumnIndex >= 0
    End Function

    ''' Structural indexes of the rows in display order, e.g. "1,2,0".
    Public Function GetTableDisplayOrderForAutomation() As String
        If _tableViewRows Is Nothing Then
            Return ""
        End If

        Return String.Join(",", _tableViewRows.Select(Function(row) row.Index.ToString(Globalization.CultureInfo.InvariantCulture)))
    End Function

    Public Function AddTableColumnForAutomation(name As String) As Boolean
        Return AddTableColumn(name)
    End Function

    ''' Drives the real DataGrid edit pipeline (BeginEdit -> editing TextBox ->
    ''' CommitEdit -> CellEditEnding) instead of calling the commit helper directly.
    Public Function EditTableCellViaGridForAutomation(rowIndex As Integer, columnName As String, text As String) As Boolean
        If _tableModel Is Nothing OrElse _tableViewRows Is Nothing Then
            Return False
        End If

        Dim columnIndex = -1
        For index = 0 To _tableModel.Columns.Count - 1
            If String.Equals(_tableModel.Columns(index).Name, columnName, StringComparison.Ordinal) Then
                columnIndex = index
                Exit For
            End If
        Next

        If columnIndex < 0 OrElse columnIndex + 1 >= TableGrid.Columns.Count Then
            Return False
        End If

        Dim item As TableRow = Nothing
        For Each row In _tableViewRows
            If row.Index = rowIndex Then
                item = row
                Exit For
            End If
        Next

        If item Is Nothing Then
            Return False
        End If

        Dim column = TableGrid.Columns(columnIndex + 1)
        TableGrid.ScrollIntoView(item, column)
        TableGrid.UpdateLayout()
        TableGrid.SelectedItem = item
        TableGrid.CurrentCell = New DataGridCellInfo(item, column)
        If Not TableGrid.BeginEdit() Then
            Return False
        End If

        Dim editor = TryCast(column.GetCellContent(item), TextBox)
        If editor Is Nothing Then
            TableGrid.CancelEdit()
            Return False
        End If

        editor.Text = text
        Return TableGrid.CommitEdit(DataGridEditingUnit.Cell, True)
    End Function

    Public Sub SelectTableRowForAutomation(rowIndex As Integer)
        If _tableModel IsNot Nothing AndAlso rowIndex >= 0 AndAlso rowIndex < _tableModel.Rows.Count Then
            TableGrid.SelectedItem = _tableModel.Rows(rowIndex)
        End If
    End Sub

    Public Function CloseTableViewForAutomation() As String
        TableBack_Click(Me, New RoutedEventArgs())
        Return GetSelectedGridPointer()
    End Function

    Public Function GetEncodingNameForAutomation() As String
        Return _currentEncoding.Name
    End Function

    Public Function GetNewLineNameForAutomation() As String
        Return _currentEncoding.NewLineName
    End Function

    Public Function GetFormatLabelForAutomation() As String
        Return GetFormatLabel(_currentFormat)
    End Function

    Public Function HasGridContextMenuForAutomation() As Boolean
        Dim style = JsonTree.ItemContainerStyle
        If style Is Nothing Then
            Return False
        End If

        Return style.Setters.OfType(Of EventSetter)().Any(Function(item) item.Event Is FrameworkElement.ContextMenuOpeningEvent)
    End Function

    Public Function AboutSmokeForAutomation() As Boolean
        Dim dialog = New AboutWindow(LocalText("About Visual JSON", "Visual JSONについて"), LocalText("Close", "閉じる")) With {.Owner = Me}
        Return dialog.BodyText.Contains("Visual JSON", StringComparison.Ordinal) AndAlso
            dialog.BodyText.Contains("Version:", StringComparison.Ordinal) AndAlso
            dialog.BodyText.Contains("Build:", StringComparison.Ordinal) AndAlso
            dialog.BodyText.Contains("AvalonEdit MIT", StringComparison.Ordinal)
    End Function

    Public Function SettingsDialogLocalizationSmokeForAutomation(language As String) As Boolean
        SetLanguageForAutomation(language)
        Dim dialog = New SettingsWindow(_settings, CreateSettingsWindowText()) With {.Owner = Me}
        Dim snapshot = dialog.LocalizationSnapshot

        If String.Equals(language, "ja", StringComparison.OrdinalIgnoreCase) Then
            Return snapshot.Contains("設定", StringComparison.Ordinal) AndAlso
                snapshot.Contains("言語", StringComparison.Ordinal) AndAlso
                snapshot.Contains("英語", StringComparison.Ordinal) AndAlso
                snapshot.Contains("日本語", StringComparison.Ordinal) AndAlso
                snapshot.Contains("保存前にバックアップ", StringComparison.Ordinal) AndAlso
                snapshot.Contains("外部HTTPS Schema", StringComparison.Ordinal) AndAlso
                snapshot.Contains("括弧と引用符", StringComparison.Ordinal) AndAlso
                snapshot.Contains("Schema探索パス", StringComparison.Ordinal) AndAlso
                snapshot.Contains("キャンセル", StringComparison.Ordinal)
        End If

        Return snapshot.Contains("Settings", StringComparison.Ordinal) AndAlso
            snapshot.Contains("Language", StringComparison.Ordinal) AndAlso
            snapshot.Contains("English", StringComparison.Ordinal) AndAlso
            snapshot.Contains("Japanese", StringComparison.Ordinal)
    End Function

#End Region

End Class
