' SPDX-License-Identifier: MPL-2.0
Imports System.Collections.ObjectModel
Imports System.ComponentModel
Imports System.Diagnostics
Imports System.IO
Imports System.Reflection
Imports System.Text
Imports System.Threading.Tasks
Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Data
Imports System.Windows.Input
Imports System.Windows.Media
Imports System.Windows.Threading
Imports Microsoft.Win32
Imports VisualJson.App.UI
Imports VisualJson.Core.Conversion
Imports VisualJson.Core.Diagnostics
Imports VisualJson.Core.Infrastructure
Imports VisualJson.Core.Models
Imports VisualJson.Core.Parsing
Imports VisualJson.Core.Serialization
Imports VisualJson.Core.Services
Imports VisualJson.Core.Validation
Imports VisualJson.App.ViewModels

Public Class MainWindow
    ''' Documents with a single line longer than this render slowly in any WPF text
    ''' control (one enormous visual line), so the app offers to pretty-print on open.
    Private Const MaxComfortableLineLength As Integer = 20000

    Public Shared ReadOnly AddChildActionTextProperty As DependencyProperty =
        DependencyProperty.Register(NameOf(AddChildActionText), GetType(String), GetType(MainWindow), New PropertyMetadata("+ Child"))

    Public Shared ReadOnly AddSiblingActionTextProperty As DependencyProperty =
        DependencyProperty.Register(NameOf(AddSiblingActionText), GetType(String), GetType(MainWindow), New PropertyMetadata("+ Row"))

    Public Shared ReadOnly DeleteActionTextProperty As DependencyProperty =
        DependencyProperty.Register(NameOf(DeleteActionText), GetType(String), GetType(MainWindow), New PropertyMetadata("Del"))

    Public Shared ReadOnly MoveUpActionTextProperty As DependencyProperty =
        DependencyProperty.Register(NameOf(MoveUpActionText), GetType(String), GetType(MainWindow), New PropertyMetadata("Up"))

    Public Shared ReadOnly MoveDownActionTextProperty As DependencyProperty =
        DependencyProperty.Register(NameOf(MoveDownActionText), GetType(String), GetType(MainWindow), New PropertyMetadata("Down"))

    Public Shared ReadOnly DragGripToolTipTextProperty As DependencyProperty =
        DependencyProperty.Register(NameOf(DragGripToolTipText), GetType(String), GetType(MainWindow), New PropertyMetadata("Drag"))

    Public Shared ReadOnly TableActionTextProperty As DependencyProperty =
        DependencyProperty.Register(NameOf(TableActionText), GetType(String), GetType(MainWindow), New PropertyMetadata("Table"))

    Private ReadOnly _viewModel As New MainViewModel()
    Private ReadOnly _preprocessor As New JsonPreprocessorService()
    Private ReadOnly _parser As New JsonParserService()
    Private ReadOnly _validator As New SyntaxValidationService()
    Private ReadOnly _serializer As New JsonTreeSerializer()
    Private ReadOnly _formatter As New JsonFormatterService()
    Private ReadOnly _typeInference As New TypeInferenceService()
    Private ReadOnly _gridOps As New GridOperationService()
    Private ReadOnly _gridFilter As New GridFilterService()
    Private ReadOnly _gridUndo As New GridUndoService()
    Private ReadOnly _treeStats As New TreeStatisticsService()
    Private ReadOnly _saveService As New FileSaveService()
    Private ReadOnly _recoveryService As New RecoveryService()
    Private ReadOnly _diagnosticsReport As New DiagnosticsReportService()
    Private ReadOnly _schemaValidation As New SchemaValidationService()
    Private ReadOnly _schemaResolver As New SchemaResolver()
    Private ReadOnly _textExport As New TextExportService()
    Private ReadOnly _xmlConversion As New JsonXmlConversionService()
    Private ReadOnly _yamlConversion As New JsonYamlConversionService()
    Private ReadOnly _encodingService As New EncodingDetectionService()
    Private ReadOnly _settingsService As SettingsService
    Private ReadOnly _recentFiles As New RecentFilesService()
    Private ReadOnly _formatSniffer As New FormatSniffer()
    Private ReadOnly _fileLog As FileLogService
    Private ReadOnly _documentState As New DocumentStateService()
    Private ReadOnly _tableBuilder As New TableViewModelBuilder()
    Private ReadOnly _jsonLines As New JsonLinesSerializer()
    Private ReadOnly _completionCandidates As New CompletionCandidateService()
    Private ReadOnly _foldingService As New JsonFoldingService()
    Private ReadOnly _searchReplace As New SearchReplaceService()
    Private ReadOnly _validationTimer As New DispatcherTimer()
    Private ReadOnly _recoveryTimer As New DispatcherTimer()

    Private _editor As TextEditorAdapter
    Private _gridEditSnapshot As String
    Private _schemaText As String
    Private _schemaSource As String
    Private _ignoreTextChanges As Boolean
    Private _lastException As Exception
    Private _currentFormat As JsonInputFormat = JsonInputFormat.StandardJson
    Private _gridDisabled As Boolean
    Private _dragStartPoint As Point
    Private _dragNode As JsonTreeNode
    Private _language As String = "en"
    Private _settings As AppSettings = AppSettings.CreateDefault()
    Private _currentEncoding As DetectedTextEncoding = DetectedTextEncoding.CreateDefault()
    Private _gridRootView As GridNodeViewModel
    Private _lastGridState As GridViewState
    Private _filterRestoreState As GridViewState
    Private _gridIsCurrent As Boolean
    Private _handlingTabSwitch As Boolean
    Private _suppressStartupPrompts As Boolean
    Private _suppressAutomaticFolding As Boolean
    Private _suppressSettingsSave As Boolean
    Private _foldingUpdateVersion As Integer
    Private _tableModel As TableModel
    Private ReadOnly _tableExtraColumns As New List(Of String)()
    ''' -2 = no display sort active (structure order shown).
    Private _tableSortColumnIndex As Integer = -2
    Private _tableSortAscending As Boolean = True
    Private _tableViewRows As IReadOnlyList(Of TableRow)

    Private ReadOnly Property Document As DocumentViewModel
        Get
            Return _viewModel.ActiveDocument
        End Get
    End Property

    Public Property AddChildActionText As String
        Get
            Return CStr(GetValue(AddChildActionTextProperty))
        End Get
        Set(value As String)
            SetValue(AddChildActionTextProperty, value)
        End Set
    End Property

    Public Property AddSiblingActionText As String
        Get
            Return CStr(GetValue(AddSiblingActionTextProperty))
        End Get
        Set(value As String)
            SetValue(AddSiblingActionTextProperty, value)
        End Set
    End Property

    Public Property DeleteActionText As String
        Get
            Return CStr(GetValue(DeleteActionTextProperty))
        End Get
        Set(value As String)
            SetValue(DeleteActionTextProperty, value)
        End Set
    End Property

    Public Property MoveUpActionText As String
        Get
            Return CStr(GetValue(MoveUpActionTextProperty))
        End Get
        Set(value As String)
            SetValue(MoveUpActionTextProperty, value)
        End Set
    End Property

    Public Property MoveDownActionText As String
        Get
            Return CStr(GetValue(MoveDownActionTextProperty))
        End Get
        Set(value As String)
            SetValue(MoveDownActionTextProperty, value)
        End Set
    End Property

    Public Property DragGripToolTipText As String
        Get
            Return CStr(GetValue(DragGripToolTipTextProperty))
        End Get
        Set(value As String)
            SetValue(DragGripToolTipTextProperty, value)
        End Set
    End Property

    Public Property TableActionText As String
        Get
            Return CStr(GetValue(TableActionTextProperty))
        End Get
        Set(value As String)
            SetValue(TableActionTextProperty, value)
        End Set
    End Property

    Public Sub New(Optional suppressStartupPrompts As Boolean = False, Optional settingsDirectory As String = Nothing, Optional logDirectory As String = Nothing)
        _suppressStartupPrompts = suppressStartupPrompts
        _suppressAutomaticFolding = suppressStartupPrompts
        _suppressSettingsSave = True
        _settingsService = New SettingsService(settingsDirectory)
        _fileLog = New FileLogService(logDirectory)
        InitializeComponent()

        DataContext = _viewModel
        _editor = New TextEditorAdapter(JsonEditor)
        AddHandler JsonEditor.TextChanged, AddressOf JsonEditor_TextChanged
        AddHandler JsonEditor.TextArea.Caret.PositionChanged, AddressOf EditorCaret_PositionChanged
        AddHandler AllowExternalSchemaMenuItem.Checked, AddressOf SettingsControl_Changed
        AddHandler AllowExternalSchemaMenuItem.Unchecked, AddressOf SettingsControl_Changed
        DiagnosticList.ItemsSource = Document.Diagnostics
        LogList.ItemsSource = Document.Logs

        _validationTimer.Interval = TimeSpan.FromMilliseconds(500)
        AddHandler _validationTimer.Tick, AddressOf ValidationTimer_Tick

        _recoveryTimer.Interval = TimeSpan.FromSeconds(15)
        AddHandler _recoveryTimer.Tick, AddressOf RecoveryTimer_Tick

        Dim settingsResult = _settingsService.Load()
        _settings = settingsResult.Settings
        ApplySettingsToControls()
        SetDocumentCore("{}", "", isDirty:=False, JsonInputFormat.StandardJson, DetectedTextEncoding.CreateDefault())
        ApplyLanguage()
        RefreshRecentFilesMenu()
        If settingsResult.RecoveredFromBroken Then
            AddLog($"Settings were reset; broken file moved to {Path.GetFileName(settingsResult.BrokenPath)}.")
        End If
        _fileLog.Write("Startup", $"version={Assembly.GetExecutingAssembly().GetName().Version}")
        AddLog("Ready.")
    End Sub

    Private Function CurrentText() As String
        If _editor Is Nothing Then
            Return ""
        End If

        Return _editor.GetText()
    End Function

    Private Sub Window_Loaded(sender As Object, e As RoutedEventArgs)
        If Not _suppressStartupPrompts Then
            PromptForRecoveryCandidates()
        End If
        ScheduleValidation()
    End Sub

    Private Sub Window_Closing(sender As Object, e As CancelEventArgs)
        If _suppressStartupPrompts Then
            SaveSettings()
            Return
        End If

        If Not ConfirmDiscardUnsavedChanges() Then
            e.Cancel = True
            Return
        End If

        SaveSettings()
        _fileLog.Write("Shutdown", "normal")
    End Sub

    Private Sub Window_KeyDown(sender As Object, e As KeyEventArgs)
        If Keyboard.Modifiers = ModifierKeys.Control AndAlso e.Key = Key.N Then
            New_Click(sender, e)
            e.Handled = True
        ElseIf Keyboard.Modifiers = ModifierKeys.Control AndAlso e.Key = Key.O Then
            Open_Click(sender, e)
            e.Handled = True
        ElseIf Keyboard.Modifiers = ModifierKeys.Control AndAlso e.Key = Key.S Then
            Save_Click(sender, e)
            e.Handled = True
        ElseIf Keyboard.Modifiers = (ModifierKeys.Control Or ModifierKeys.Shift) AndAlso e.Key = Key.S Then
            SaveAs_Click(sender, e)
            e.Handled = True
        ElseIf Keyboard.Modifiers = (ModifierKeys.Control Or ModifierKeys.Shift) AndAlso e.Key = Key.F Then
            Format_Click(sender, e)
            e.Handled = True
        ElseIf Keyboard.Modifiers = ModifierKeys.Control AndAlso e.Key = Key.F Then
            FocusSearchBox()
            e.Handled = True
        ElseIf Keyboard.Modifiers = ModifierKeys.Control AndAlso e.Key = Key.H Then
            FocusReplaceBox()
            e.Handled = True
        ElseIf Keyboard.Modifiers = ModifierKeys.Control AndAlso e.Key = Key.T Then
            TextMode_Click(sender, e)
            e.Handled = True
        ElseIf Keyboard.Modifiers = ModifierKeys.Control AndAlso e.Key = Key.G Then
            GridMode_Click(sender, e)
            e.Handled = True
        ElseIf Keyboard.Modifiers = ModifierKeys.Control AndAlso e.Key = Key.Z Then
            UndoGrid_Click(sender, e)
            e.Handled = True
        ElseIf Keyboard.Modifiers = ModifierKeys.Control AndAlso e.Key = Key.Y Then
            RedoGrid_Click(sender, e)
            e.Handled = True
        ElseIf Keyboard.Modifiers = ModifierKeys.Control AndAlso e.Key = Key.Space Then
            ShowKeyCompletion()
            e.Handled = True
        ElseIf e.Key = Key.F5 Then
            ValidateSync_Click(sender, e)
            e.Handled = True
        End If
    End Sub

    ''' FR-P2-504: Ctrl+Space lists key candidates for the object containing the
    ''' caret (sibling-array object keys + schema properties, minus existing keys).
    Private Function ShowKeyCompletion() As Integer
        If Document.RootNode Is Nothing OrElse EditorTabs.SelectedItem IsNot TextTab Then
            Return 0
        End If

        Dim containingObject = FindContainingObjectAtCaret()
        If containingObject Is Nothing Then
            Return 0
        End If

        Dim candidates = _completionCandidates.GetKeyCandidates(Document.RootNode, containingObject, _schemaText)
        Return _editor.ShowKeyCompletion(candidates)
    End Function

    Private Function FindContainingObjectAtCaret() As JsonTreeNode
        Dim node = _documentState.FindNodeAtOffset(Document.RootNode, _editor.GetCaretOffset())
        While node IsNot Nothing
            If node.Kind = JsonNodeKind.ObjectValue Then
                Return node
            End If

            Dim pointer = node.JsonPointer
            If String.IsNullOrEmpty(pointer) Then
                Return Nothing
            End If

            Dim parentPointer = pointer.Substring(0, pointer.LastIndexOf("/"c))
            node = _documentState.FindNodeByPointer(Document.RootNode, parentPointer)
        End While

        Return Nothing
    End Function

    Private Sub New_Click(sender As Object, e As RoutedEventArgs)
        If Not ConfirmDiscardUnsavedChanges() Then
            Return
        End If

        SetDocumentCore("{}", "", isDirty:=False, JsonInputFormat.StandardJson, DetectedTextEncoding.CreateDefault())
        AddLog("Created a new document.")
    End Sub

    Private Sub Open_Click(sender As Object, e As RoutedEventArgs)
        If Not ConfirmDiscardUnsavedChanges() Then
            Return
        End If

        Dim dialog = New OpenFileDialog With {
            .Filter = "JSON files (*.json;*.jsonc;*.json5;*.jsonl;*.ndjson)|*.json;*.jsonc;*.json5;*.jsonl;*.ndjson|All files (*.*)|*.*",
            .Title = "Open JSON"
        }

        If dialog.ShowDialog(Me) = True Then
            OpenDocumentPath(dialog.FileName, addToRecent:=True)
        End If
    End Sub

    Public Sub OpenPathForStartup(path As String)
        If String.IsNullOrWhiteSpace(path) Then
            Return
        End If

        OpenDocumentPath(path, addToRecent:=True)
    End Sub

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

    Private Function OpenDocumentPath(path As String, addToRecent As Boolean, Optional showDialogs As Boolean = True) As Boolean
        Try
            Dim fullPath = IO.Path.GetFullPath(path)
            Dim info = New FileInfo(fullPath)
            If info.Length >= 50L * 1024L * 1024L AndAlso showDialogs Then
                MessageBox.Show(Me, "This file is 50MB or larger. Visual JSON will open it in text mode and disable full grid editing for this session.", "Large JSON", MessageBoxButton.OK, MessageBoxImage.Information)
            End If

            Dim readResult = _encodingService.ReadText(fullPath)
            Dim format = ResolveInputFormat(fullPath, readResult.Text, showDialogs)
            Dim prepared = PrepareDocumentTextForDisplay(readResult.Text, format)
            SetDocumentCore(prepared.Text, fullPath, isDirty:=prepared.WasFormatted, format, readResult.EncodingInfo)
            AddLog($"Opened {IO.Path.GetFileName(path)}.")
            _fileLog.Write("Open", $"{IO.Path.GetFileName(fullPath)} size={info.Length} encoding={readResult.EncodingInfo.Name} newline={readResult.EncodingInfo.NewLineName}")
            If Not String.IsNullOrWhiteSpace(readResult.EncodingInfo.Warning) Then
                AddLog(readResult.EncodingInfo.Warning)
                If showDialogs AndAlso readResult.EncodingInfo.Warning.StartsWith("Encoding could not", StringComparison.Ordinal) Then
                    MessageBox.Show(Me, readResult.EncodingInfo.Warning, "Encoding warning", MessageBoxButton.OK, MessageBoxImage.Warning)
                End If
            End If
            If prepared.WasFormatted Then
                AddLog("Formatted long single-line JSON for editing. Save to persist the formatting.")
            End If
            If addToRecent Then
                _recentFiles.Add(_settings, fullPath)
                SaveSettings()
                RefreshRecentFilesMenu()
            End If
            Return True
        Catch ex As Exception
            _lastException = ex
            _fileLog.WriteException("OpenFailed", ex)
            If showDialogs Then
                MessageBox.Show(Me, ex.Message, "Open failed", MessageBoxButton.OK, MessageBoxImage.Error)
            End If
            AddLog($"Open failed: {ex.Message}")
            Return False
        End Try
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

    Private Sub Save_Click(sender As Object, e As RoutedEventArgs)
        SaveCurrent(saveAs:=False)
    End Sub

    Private Sub SaveAs_Click(sender As Object, e As RoutedEventArgs)
        SaveCurrent(saveAs:=True)
    End Sub

    Private Sub Exit_Click(sender As Object, e As RoutedEventArgs)
        Close()
    End Sub

    Private Sub ValidateSync_Click(sender As Object, e As RoutedEventArgs)
        ValidateCurrentText(updateGrid:=True)
    End Sub

    Private Sub ApplyGridToText_Click(sender As Object, e As RoutedEventArgs)
        ApplyGridToText()
    End Sub

    Private Sub ReplaceFocus_Click(sender As Object, e As RoutedEventArgs)
        FocusReplaceBox()
    End Sub

    Private Sub SearchBox_TextChanged(sender As Object, e As TextChangedEventArgs)
        UpdateSearchHighlights()
    End Sub

    Private Sub SearchOptions_Changed(sender As Object, e As RoutedEventArgs)
        UpdateSearchHighlights()
    End Sub

    Private Sub AutoPairBox_Changed(sender As Object, e As RoutedEventArgs)
        If _editor IsNot Nothing AndAlso AutoPairBox IsNot Nothing Then
            _editor.AutoPairingEnabled = AutoPairBox.IsChecked.GetValueOrDefault(True)
            If _settings IsNot Nothing Then
                _settings.AutoCloseBrackets = _editor.AutoPairingEnabled
                SaveSettings()
            End If
        End If
    End Sub

    Private Sub Settings_Click(sender As Object, e As RoutedEventArgs)
        Dim dialog = New SettingsWindow(_settings, CreateSettingsWindowText()) With {.Owner = Me}

        If dialog.ShowDialog() <> True Then
            Return
        End If

        dialog.ApplyTo(_settings)
        If dialog.ClearHistoryRequested Then
            _recentFiles.Clear(_settings)
        End If

        ApplySettingsToControls()
        ApplyLanguage()
        RefreshRecentFilesMenu()
        SaveSettings()
        AddLog("Settings saved.")
        _fileLog.Write("Settings", "saved")
    End Sub

    Private Function CreateSettingsWindowText() As SettingsWindowText
        Return New SettingsWindowText With {
            .Title = LocalText("Settings", "設定"),
            .Ok = LocalText("OK", "OK"),
            .Cancel = LocalText("Cancel", "キャンセル"),
            .Language = LocalText("Language", "言語"),
            .English = LocalText("English", "英語"),
            .Japanese = LocalText("Japanese", "日本語"),
            .BackupBeforeSave = LocalText("Create backup before save", "保存前にバックアップを作成"),
            .AllowExternalSchema = LocalText("Allow external HTTPS schema", "外部HTTPS Schemaを許可"),
            .AutoCloseBrackets = LocalText("Auto close brackets and quotes", "括弧と引用符を自動補完"),
            .SchemaSearchPaths = LocalText("Schema search paths", "Schema探索パス"),
            .ClearRecentHistory = LocalText("Clear recent file history", "最近使ったファイル履歴を消去")
        }
    End Function

    Private Sub About_Click(sender As Object, e As RoutedEventArgs)
        Dim dialog = New AboutWindow(LocalText("About Visual JSON", "Visual JSONについて"), LocalText("Close", "閉じる")) With {.Owner = Me}
        dialog.ShowDialog()
    End Sub

    Private Sub Replace_Click(sender As Object, e As RoutedEventArgs)
        ReplaceCurrent()
    End Sub

    Private Sub ReplaceAll_Click(sender As Object, e As RoutedEventArgs)
        ReplaceAllCurrent()
    End Sub

    Private Sub Format_Click(sender As Object, e As RoutedEventArgs)
        If Not ValidateCurrentText(updateGrid:=True) Then
            MessageBox.Show(Me, "Fix syntax errors before formatting.", "Invalid JSON", MessageBoxButton.OK, MessageBoxImage.Warning)
            Return
        End If

        Try
            Dim caretPointer = If(Document.RootNode Is Nothing, "", _documentState.GetPointerAtOffset(Document.RootNode, _editor.GetCaretOffset()))
            Dim state = CaptureGridState(caretPointer)
            Dim formatted = _formatter.Format(CurrentText(), _currentFormat)
            SetEditorText(formatted)
            Dim parsed = _parser.Parse(formatted, _currentFormat)
            SetGridRoot(parsed.Root, state, caretPointer, bringIntoView:=False)
            MoveTextCaretToPointer(caretPointer)
            ScheduleFoldingUpdate()
            Document.IsDirty = True
            UpdateChrome()
            ScheduleRecoverySnapshot()
            AddLog("Formatted JSON.")
        Catch ex As Exception
            _lastException = ex
            MessageBox.Show(Me, ex.Message, "Format failed", MessageBoxButton.OK, MessageBoxImage.Error)
            AddLog($"Format failed: {ex.Message}")
        End Try
    End Sub

    Private Sub TextMode_Click(sender As Object, e As RoutedEventArgs)
        ShowTextTab()
    End Sub

    Private Sub GridMode_Click(sender As Object, e As RoutedEventArgs)
        ShowGridTab()
    End Sub

    Private Sub EditorTabs_SelectionChanged(sender As Object, e As SelectionChangedEventArgs)
        If _handlingTabSwitch OrElse Not Object.ReferenceEquals(e.OriginalSource, EditorTabs) Then
            Return
        End If

        If Object.ReferenceEquals(EditorTabs.SelectedItem, GridTab) Then
            _handlingTabSwitch = True
            Try
                If Not SwitchTextToGrid() Then
                    EditorTabs.SelectedItem = TextTab
                End If
            Finally
                _handlingTabSwitch = False
            End Try
        ElseIf Object.ReferenceEquals(EditorTabs.SelectedItem, TextTab) Then
            SwitchGridToText()
        End If
    End Sub

    Private Sub JsonTree_SelectedItemChanged(sender As Object, e As RoutedPropertyChangedEventArgs(Of Object))
        Dim node = GetNodeFromDataContext(e.NewValue)
        If node Is Nothing Then
            Return
        End If

        _lastGridState = CaptureGridState(node.JsonPointer)
        UpdatePointerStatus(node.JsonPointer)
    End Sub

    Private Sub GridCell_GotKeyboardFocus(sender As Object, e As KeyboardFocusChangedEventArgs)
        Dim textBox = TryCast(sender, TextBox)
        If textBox IsNot Nothing Then
            textBox.Tag = textBox.Text
            ' Pre-edit snapshot so a committed cell edit registers as one undo operation (spec 05 §6.2).
            _gridEditSnapshot = If(Document.RootNode Is Nothing, Nothing, _serializer.Serialize(Document.RootNode))
        End If
    End Sub

    Private Sub GridCell_KeyDown(sender As Object, e As KeyEventArgs)
        Dim textBox = TryCast(sender, TextBox)
        If textBox Is Nothing Then
            Return
        End If

        If e.Key = Key.Enter Then
            Dim expression = BindingOperations.GetBindingExpression(textBox, TextBox.TextProperty)
            expression?.UpdateSource()
            If Not String.Equals(TryCast(textBox.Tag, String), textBox.Text, StringComparison.Ordinal) AndAlso _gridEditSnapshot IsNot Nothing Then
                _gridUndo.PushSnapshot(_gridEditSnapshot)
                _gridEditSnapshot = Nothing
                textBox.Tag = textBox.Text
            End If
            Dim node = GetNodeFromSender(sender)
            SyncGridToText(showText:=False, focusPointer:=node?.JsonPointer)
            e.Handled = True
        ElseIf e.Key = Key.Escape Then
            textBox.Text = If(TryCast(textBox.Tag, String), "")
            Dim expression = BindingOperations.GetBindingExpression(textBox, TextBox.TextProperty)
            expression?.UpdateSource()
            e.Handled = True
        ElseIf e.Key = Key.F2 Then
            textBox.Focus()
            textBox.SelectAll()
            e.Handled = True
        End If
    End Sub

    Private Sub GridCell_MouseDoubleClick(sender As Object, e As MouseButtonEventArgs)
        Dim textBox = TryCast(sender, TextBox)
        If textBox IsNot Nothing Then
            textBox.Focus()
            textBox.SelectAll()
        End If
    End Sub

    Private Sub GridFilterBox_TextChanged(sender As Object, e As TextChangedEventArgs)
        If JsonTree Is Nothing Then
            Return
        End If

        RefreshGridView()
        If ClearFilterButton IsNot Nothing Then
            ClearFilterButton.IsEnabled = Not String.IsNullOrWhiteSpace(GridFilterBox.Text)
        End If
    End Sub

    Private Sub ClearFilter_Click(sender As Object, e As RoutedEventArgs)
        Dim restoreState = If(_filterRestoreState, CaptureGridState())
        GridFilterBox.Text = ""
        _filterRestoreState = restoreState
        RefreshGridView()
    End Sub

    Private Sub UndoGrid_Click(sender As Object, e As RoutedEventArgs)
        If Not _gridUndo.CanUndo() Then
            AddLog("No grid operation to undo.")
            Return
        End If

        Dim restored = _gridUndo.Undo(Document.RootNode)
        If restored Is Nothing Then
            Return
        End If

        Document.RootNode = restored
        Dim state = CaptureGridState()
        SetGridRoot(restored, state, state.SelectedPointer, bringIntoView:=True)
        SyncGridToText(showText:=False, focusPointer:=state.SelectedPointer)
        AddLog("Undid grid operation.")
    End Sub

    Private Sub RedoGrid_Click(sender As Object, e As RoutedEventArgs)
        If Not _gridUndo.CanRedo() Then
            AddLog("No grid operation to redo.")
            Return
        End If

        Dim restored = _gridUndo.Redo(Document.RootNode)
        If restored Is Nothing Then
            Return
        End If

        Document.RootNode = restored
        Dim state = CaptureGridState()
        SetGridRoot(restored, state, state.SelectedPointer, bringIntoView:=True)
        SyncGridToText(showText:=False, focusPointer:=state.SelectedPointer)
        AddLog("Redid grid operation.")
    End Sub

    Private Sub AddChild_Click(sender As Object, e As RoutedEventArgs)
        Dim node = GetNodeFromSender(sender)
        If node Is Nothing OrElse Not CanEditGrid() Then
            Return
        End If

        CaptureGridUndo()
        Dim child = _gridOps.AddChild(node)
        AfterGridOperation("Added child node.", child.JsonPointer)
    End Sub

    Private Sub AddSibling_Click(sender As Object, e As RoutedEventArgs)
        Dim node = GetNodeFromSender(sender)
        If node Is Nothing OrElse Document.RootNode Is Nothing OrElse Not CanEditGrid() Then
            Return
        End If

        CaptureGridUndo()
        Dim sibling = _gridOps.AddSibling(Document.RootNode, node)
        AfterGridOperation("Added sibling node.", sibling.JsonPointer)
    End Sub

    Private Sub DeleteNode_Click(sender As Object, e As RoutedEventArgs)
        Dim node = GetNodeFromSender(sender)
        If node Is Nothing OrElse Document.RootNode Is Nothing OrElse Not CanEditGrid() Then
            Return
        End If

        CaptureGridUndo()
        Dim focusPointer = node.JsonPointer
        If _gridOps.Delete(Document.RootNode, node) Then
            AfterGridOperation("Deleted node.", focusPointer)
        End If
    End Sub

    Private Sub MoveUp_Click(sender As Object, e As RoutedEventArgs)
        Dim node = GetNodeFromSender(sender)
        If node Is Nothing OrElse Document.RootNode Is Nothing OrElse Not CanEditGrid() Then
            Return
        End If

        CaptureGridUndo()
        If _gridOps.MoveUp(Document.RootNode, node) Then
            AfterGridOperation("Moved node up.", node.JsonPointer)
        End If
    End Sub

    Private Sub MoveDown_Click(sender As Object, e As RoutedEventArgs)
        Dim node = GetNodeFromSender(sender)
        If node Is Nothing OrElse Document.RootNode Is Nothing OrElse Not CanEditGrid() Then
            Return
        End If

        CaptureGridUndo()
        If _gridOps.MoveDown(Document.RootNode, node) Then
            AfterGridOperation("Moved node down.", node.JsonPointer)
        End If
    End Sub

    Private Sub DuplicateNode_Click(sender As Object, e As RoutedEventArgs)
        Dim node = GetNodeFromSender(sender)
        If node Is Nothing OrElse Document.RootNode Is Nothing OrElse Not CanEditGrid() Then
            Return
        End If

        CaptureGridUndo()
        Dim duplicate = _gridOps.Duplicate(Document.RootNode, node)
        If duplicate IsNot Nothing Then
            AfterGridOperation("Duplicated node.", duplicate.JsonPointer)
        End If
    End Sub

    Private Sub CopyPointer_Click(sender As Object, e As RoutedEventArgs)
        Dim node = GetNodeFromSender(sender)
        If node Is Nothing Then
            Return
        End If

        Clipboard.SetText(DocumentStateService.ToPointerDisplay(node.JsonPointer))
        AddLog("Copied JSON Pointer.")
    End Sub

    Private Sub JumpToText_Click(sender As Object, e As RoutedEventArgs)
        Dim node = GetNodeFromSender(sender)
        If node Is Nothing Then
            Return
        End If

        ShowTextTab(node.JsonPointer)
        AddLog("Jumped to text.")
    End Sub

    Private Sub TypeCombo_Loaded(sender As Object, e As RoutedEventArgs)
        Dim combo = TryCast(sender, ComboBox)
        Dim node = GetNodeFromSender(sender)
        If combo Is Nothing OrElse node Is Nothing Then
            Return
        End If

        combo.Tag = "loading"
        combo.Items.Clear()
        For Each kind In {JsonNodeKind.ObjectValue, JsonNodeKind.ArrayValue, JsonNodeKind.StringValue, JsonNodeKind.NumberValue, JsonNodeKind.BooleanValue, JsonNodeKind.NullValue}
            combo.Items.Add(New ComboBoxItem With {
                .Content = GetTypeLabel(kind),
                .Tag = kind
            })
        Next

        For index = 0 To combo.Items.Count - 1
            Dim item = TryCast(combo.Items(index), ComboBoxItem)
            If item IsNot Nothing AndAlso DirectCast(item.Tag, JsonNodeKind) = node.Kind Then
                combo.SelectedIndex = index
                Exit For
            End If
        Next
        combo.Tag = Nothing
    End Sub

    Private Sub TypeCombo_SelectionChanged(sender As Object, e As SelectionChangedEventArgs)
        Dim combo = TryCast(sender, ComboBox)
        If combo Is Nothing OrElse String.Equals(TryCast(combo.Tag, String), "loading", StringComparison.Ordinal) Then
            Return
        End If

        Dim node = GetNodeFromSender(sender)
        Dim item = TryCast(combo.SelectedItem, ComboBoxItem)
        If node Is Nothing OrElse item Is Nothing OrElse Not CanEditGrid() Then
            Return
        End If

        Dim nextKind = DirectCast(item.Tag, JsonNodeKind)
        If node.Kind = nextKind Then
            Return
        End If

        CaptureGridUndo()
        _gridOps.ChangeType(node, nextKind)
        AfterGridOperation("Changed node type.", node.JsonPointer)
    End Sub

    Private Sub Grip_PreviewMouseLeftButtonDown(sender As Object, e As MouseButtonEventArgs)
        Dim button = TryCast(sender, Button)
        _dragNode = TryCast(button?.Tag, JsonTreeNode)
        _dragStartPoint = e.GetPosition(JsonTree)
    End Sub

    Private Sub JsonTree_PreviewMouseMove(sender As Object, e As MouseEventArgs)
        If _dragNode Is Nothing OrElse e.LeftButton <> MouseButtonState.Pressed OrElse Not CanEditGrid(showMessage:=False) Then
            Return
        End If

        Dim current = e.GetPosition(JsonTree)
        If Math.Abs(current.X - _dragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance AndAlso
            Math.Abs(current.Y - _dragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance Then
            Return
        End If

        DragDrop.DoDragDrop(JsonTree, New DataObject(GetType(JsonTreeNode), _dragNode), DragDropEffects.Move)
        _dragNode = Nothing
    End Sub

    Private Sub JsonTree_Drop(sender As Object, e As DragEventArgs)
        If Document.RootNode Is Nothing OrElse Not CanEditGrid() Then
            Return
        End If

        Dim source = TryCast(e.Data.GetData(GetType(JsonTreeNode)), JsonTreeNode)
        Dim target = GetNodeFromDependencyObject(TryCast(e.OriginalSource, DependencyObject))
        If source Is Nothing OrElse target Is Nothing Then
            Return
        End If

        e.Handled = True
        Select Case _gridOps.CheckMoveBefore(Document.RootNode, source, target)
            Case CrossParentMoveStatus.IntoOwnDescendant
                MessageBox.Show(Me,
                                LocalText("A node cannot be moved into itself or its own descendants.", "ノードを自分自身または自分の子孫へ移動することはできません。"),
                                LocalText("Move rejected", "移動できません"),
                                MessageBoxButton.OK, MessageBoxImage.Warning)
                Return
            Case CrossParentMoveStatus.KeyConflict
                Dim answer = MessageBox.Show(Me,
                                             String.Format(LocalText("The destination object already has a key ""{0}"". Move anyway with a unique key?", "移動先のオブジェクトには既にキー「{0}」があります。キーを一意化して移動しますか?"), source.Key),
                                             LocalText("Key conflict", "キー重複"),
                                             MessageBoxButton.YesNo, MessageBoxImage.Question)
                If answer <> MessageBoxResult.Yes Then
                    Return
                End If
            Case CrossParentMoveStatus.Invalid
                Return
        End Select

        CaptureGridUndo()
        If _gridOps.MoveBeforeAcrossParents(Document.RootNode, source, target) Then
            AfterGridOperation("Moved node by drag and drop.", source.JsonPointer)
        End If
    End Sub

    Private Sub TreeViewItem_PreviewMouseRightButtonDown(sender As Object, e As MouseButtonEventArgs)
        Dim item = TryCast(sender, TreeViewItem)
        If item IsNot Nothing Then
            item.IsSelected = True
            item.Focus()
            e.Handled = False
        End If
    End Sub

    Private Sub TreeViewItem_ContextMenuOpening(sender As Object, e As ContextMenuEventArgs)
        Dim item = TryCast(sender, TreeViewItem)
        If item Is Nothing Then
            Return
        End If

        item.ContextMenu = CreateGridContextMenu(item.DataContext)
    End Sub

    Private Function CreateGridContextMenu(dataContext As Object) As ContextMenu
        Dim menu = New ContextMenu With {.DataContext = dataContext}
        AddHandler menu.Opened, AddressOf GridNodeContextMenu_Opened

        menu.Items.Add(CreateGridMenuItem("Add child", "AddChild", AddressOf AddChild_Click))
        menu.Items.Add(CreateGridMenuItem("Add sibling", "AddSibling", AddressOf AddSibling_Click))
        menu.Items.Add(CreateGridMenuItem("Delete", "Delete", AddressOf DeleteNode_Click))
        menu.Items.Add(New Separator())
        menu.Items.Add(CreateGridMenuItem("Move Up", "MoveUp", AddressOf MoveUp_Click))
        menu.Items.Add(CreateGridMenuItem("Move Down", "MoveDown", AddressOf MoveDown_Click))
        menu.Items.Add(CreateGridMenuItem("Duplicate", "Duplicate", AddressOf DuplicateNode_Click))
        menu.Items.Add(New Separator())
        menu.Items.Add(CreateGridMenuItem("Copy JSON Pointer", "CopyPointer", AddressOf CopyPointer_Click))
        menu.Items.Add(CreateGridMenuItem("Jump to Text", "JumpToText", AddressOf JumpToText_Click))
        Return menu
    End Function

    Private Function CreateGridMenuItem(header As String, tag As String, handler As RoutedEventHandler) As MenuItem
        Dim item = New MenuItem With {.Header = header, .Tag = tag}
        AddHandler item.Click, handler
        Return item
    End Function

    Private Sub GridNodeContextMenu_Opened(sender As Object, e As RoutedEventArgs)
        Dim menu = TryCast(sender, ContextMenu)
        Dim node = GetNodeFromDataContext(menu?.DataContext)
        Dim canEdit = node IsNot Nothing AndAlso Document.RootNode IsNot Nothing AndAlso CanEditGrid(showMessage:=False)
        Dim isRoot = node IsNot Nothing AndAlso String.IsNullOrEmpty(node.JsonPointer)

        For Each item In menu.Items.OfType(Of MenuItem)()
            Dim tag = TryCast(item.Tag, String)
            Select Case tag
                Case "AddChild"
                    item.Header = LocalText("Add child", "子を追加")
                    item.IsEnabled = canEdit AndAlso (node.Kind = JsonNodeKind.ObjectValue OrElse node.Kind = JsonNodeKind.ArrayValue)
                Case "AddSibling"
                    item.Header = LocalText("Add sibling", "兄弟を追加")
                    item.IsEnabled = canEdit AndAlso Not isRoot
                Case "Delete"
                    item.Header = LocalText("Delete", "削除")
                    item.IsEnabled = canEdit AndAlso Not isRoot
                Case "MoveUp"
                    item.Header = LocalText("Move Up", "上へ移動")
                    item.IsEnabled = canEdit
                Case "MoveDown"
                    item.Header = LocalText("Move Down", "下へ移動")
                    item.IsEnabled = canEdit
                Case "Duplicate"
                    item.Header = LocalText("Duplicate", "複製")
                    item.IsEnabled = canEdit AndAlso Not isRoot
                Case "CopyPointer"
                    item.Header = LocalText("Copy JSON Pointer", "JSON Pointerをコピー")
                    item.IsEnabled = node IsNot Nothing
                Case "JumpToText"
                    item.Header = LocalText("Jump to Text", "テキストへ移動")
                    item.IsEnabled = node IsNot Nothing
            End Select
        Next
    End Sub

    Private Sub LanguageCombo_SelectionChanged(sender As Object, e As SelectionChangedEventArgs)
        Dim combo = TryCast(sender, ComboBox)
        Dim item = TryCast(combo?.SelectedItem, ComboBoxItem)
        If item IsNot Nothing Then
            _language = If(TryCast(item.Tag, String), "en")
            _settings.Language = _language
        End If

        ApplyLanguage()
        SaveSettings()
    End Sub

    Private Sub SettingsControl_Changed(sender As Object, e As RoutedEventArgs)
        If _settings Is Nothing OrElse _suppressSettingsSave Then
            Return
        End If

        _settings.AllowExternalSchema = AllowExternalSchemaMenuItem.IsChecked
        SaveSettings()
    End Sub

    Private Sub JsonEditor_TextChanged(sender As Object, e As EventArgs)
        If _ignoreTextChanges OrElse _editor Is Nothing Then
            Return
        End If

        Document.IsDirty = True
        _gridIsCurrent = False
        UpdateCaretStatus()
        UpdateSearchHighlights()
        UpdateChrome()
        ScheduleValidation()
        ScheduleRecoverySnapshot()
    End Sub

    Private Sub EditorCaret_PositionChanged(sender As Object, e As EventArgs)
        UpdateCaretStatus()
    End Sub

    Private Async Sub ValidationTimer_Tick(sender As Object, e As EventArgs)
        _validationTimer.Stop()
        Await ValidateCurrentTextAsync(updateGrid:=True)
    End Sub

    Private Sub RecoveryTimer_Tick(sender As Object, e As EventArgs)
        _recoveryTimer.Stop()

        Dim text = CurrentText()
        If Not Document.IsDirty OrElse String.IsNullOrWhiteSpace(text) Then
            Return
        End If

        Try
            Dim candidate = _recoveryService.CreateSnapshot(Document.CurrentFilePath, text)
            AddLog($"Recovery snapshot saved: {candidate.DisplayName}")
        Catch ex As Exception
            _lastException = ex
            AddLog($"Recovery snapshot failed: {ex.Message}")
        End Try
    End Sub

    Private Sub DiagnosticList_SelectionChanged(sender As Object, e As SelectionChangedEventArgs)
        Dim diagnostic = TryCast(DiagnosticList.SelectedItem, ValidationDiagnostic)
        If diagnostic Is Nothing OrElse Not diagnostic.Line.HasValue Then
            Return
        End If

        EditorTabs.SelectedIndex = 0
        _editor.MoveToLineColumn(diagnostic.Line.Value, If(diagnostic.Column, 1))
    End Sub

    Private Sub FindNext_Click(sender As Object, e As RoutedEventArgs)
        FindText(forward:=True)
    End Sub

    Private Sub FindPrevious_Click(sender As Object, e As RoutedEventArgs)
        FindText(forward:=False)
    End Sub

    Private Sub RecoverLatest_Click(sender As Object, e As RoutedEventArgs)
        RecoverLatestCandidate()
    End Sub

    Private Sub CopyDiagnostics_Click(sender As Object, e As RoutedEventArgs)
        Dim version = Assembly.GetExecutingAssembly().GetName().Version?.ToString()
        Dim report = _diagnosticsReport.CreateReport(version, Document.CurrentFilePath, CurrentText().Length, Document.Diagnostics, _lastException, _treeStats.CountNodes(Document.RootNode), "WPF", _language, GetFormatLabel(_currentFormat), _currentEncoding.Name, _currentEncoding.NewLineName)
        Clipboard.SetText(report)
        AddLog("Diagnostics copied without JSON body.")
    End Sub

    ''' Offers to pretty-print documents whose longest line would make rendering sluggish.
    ''' The document itself is never changed without the user's consent; declining opens as-is.
    Private Function PrepareDocumentTextForDisplay(text As String, format As JsonInputFormat) As (Text As String, WasFormatted As Boolean)
        Dim source = If(text, "")
        If LongestLineLength(source) <= MaxComfortableLineLength Then
            Return (source, False)
        End If

        Dim message = LocalText(
            "This file contains extremely long lines, which slows down text rendering." & Environment.NewLine & Environment.NewLine &
            "Format the JSON now for comfortable editing? (The file on disk is not changed until you save.)",
            "このファイルには極端に長い行が含まれており、テキスト描画が遅くなります。" & Environment.NewLine & Environment.NewLine &
            "編集しやすいようにJSONを整形して開きますか?(保存するまでディスク上のファイルは変更されません)")
        Dim result = MessageBox.Show(Me, message, LocalText("Long lines detected", "長い行を検出"), MessageBoxButton.YesNo, MessageBoxImage.Question)
        If result <> MessageBoxResult.Yes Then
            AddLog("Opened without formatting; rendering may be slow for very long lines.")
            Return (source, False)
        End If

        Try
            Return (_formatter.Format(source, format), True)
        Catch ex As Exception
            _lastException = ex
            AddLog($"Could not format on open (invalid JSON?): {ex.Message}")
            Return (source, False)
        End Try
    End Function

    Private Shared Function LongestLineLength(text As String) As Integer
        Dim longest = 0
        Dim current = 0

        For Each ch In text
            If ch = ControlChars.Lf OrElse ch = ControlChars.Cr Then
                longest = Math.Max(longest, current)
                current = 0
            Else
                current += 1
            End If
        Next

        Return Math.Max(longest, current)
    End Function

    Private Sub SetDocument(text As String, filePath As String, isDirty As Boolean)
        SetDocumentCore(text, filePath, isDirty, _preprocessor.DetectFromPath(filePath), DetectedTextEncoding.CreateDefault())
    End Sub

    Private Sub SetDocumentCore(text As String, filePath As String, isDirty As Boolean, format As JsonInputFormat, encodingInfo As DetectedTextEncoding)
        SetEditorText(If(text, ""))
        Document.CurrentFilePath = If(filePath, "")
        Document.IsDirty = isDirty
        Document.RootNode = Nothing
        _gridRootView = Nothing
        _lastGridState = Nothing
        _filterRestoreState = Nothing
        _gridIsCurrent = False
        _currentFormat = format
        _currentEncoding = If(encodingInfo, DetectedTextEncoding.CreateDefault())
        _gridDisabled = Not String.IsNullOrWhiteSpace(filePath) AndAlso File.Exists(filePath) AndAlso New FileInfo(filePath).Length >= 50L * 1024L * 1024L
        _gridUndo.Clear()
        GridFilterBox.Text = ""
        JsonTree.ItemsSource = New ObservableCollection(Of GridNodeViewModel)()
        JsonTree.IsEnabled = Not _gridDisabled
        ' Schema validation results belong to the previous document; the loaded schema is kept.
        If SchemaList IsNot Nothing Then
            SchemaList.ItemsSource = Nothing
        End If

        FileFormatStatusText.Text = GetFormatLabel(_currentFormat)
        UpdateDocumentMetadataStatus()
        UpdateCaretStatus()
        UpdateChrome()
        ScheduleValidation()
    End Sub

    Private Function ResolveInputFormat(filePath As String, text As String, showDialogs As Boolean) As JsonInputFormat
        If IsKnownJsonExtension(filePath) Then
            Return _preprocessor.DetectFromPath(filePath)
        End If

        Dim sniff = _formatSniffer.Sniff(text)
        If sniff.Confident Then
            AddLog($"Detected format: {GetFormatLabel(sniff.Format)} ({sniff.Reason}).")
            Return sniff.Format
        End If

        If showDialogs AndAlso Not _suppressStartupPrompts Then
            Dim dialog = New FormatSelectionWindow(LocalText("Select JSON format", "JSON形式を選択"), LocalText("OK", "OK"), LocalText("Cancel", "キャンセル")) With {.Owner = Me}
            If dialog.ShowDialog() = True Then
                Return dialog.SelectedFormat
            End If
        End If

        AddLog("Format could not be inferred; using JSON.")
        Return JsonInputFormat.StandardJson
    End Function

    Private Shared Function IsKnownJsonExtension(filePath As String) As Boolean
        Dim extension = If(IO.Path.GetExtension(filePath), "").ToLowerInvariant()
        Select Case extension
            Case ".json", ".jsonc", ".json5", ".jsonl", ".ndjson"
                Return True
            Case Else
                Return False
        End Select
    End Function

    Private Sub SetEditorText(text As String)
        _ignoreTextChanges = True
        _editor.SetText(text)
        _ignoreTextChanges = False
        UpdateSearchHighlights()
        ScheduleFoldingUpdate()
    End Sub

    Private Function SaveCurrent(saveAs As Boolean, Optional showDialogs As Boolean = True) As Boolean
        If saveAs OrElse String.IsNullOrWhiteSpace(Document.CurrentFilePath) Then
            Dim dialog = New SaveFileDialog With {
                .Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                .Title = "Save JSON",
                .DefaultExt = ".json"
            }

            If dialog.ShowDialog(Me) <> True Then
                Return False
            End If

            Document.CurrentFilePath = dialog.FileName
        End If

        If Not ValidateCurrentText(updateGrid:=False) Then
            If showDialogs Then
                MessageBox.Show(Me, "Fix syntax errors before saving.", "Invalid JSON", MessageBoxButton.OK, MessageBoxImage.Warning)
            End If
            Return False
        End If

        Try
            Dim formatted = _formatter.Format(CurrentText(), _currentFormat)
            Dim result As FileSaveResult

            If _currentFormat = JsonInputFormat.JsonLines Then
                ' FR-P2-602: JSONL documents are saved one compact JSON per line
                ' while the editor keeps showing the array-style standard JSON.
                Dim parsed = _parser.Parse(formatted)
                Dim lineResult = _jsonLines.Serialize(parsed.Root, If(_currentEncoding.NewLine = NewLineKind.CrLf, vbCrLf, vbLf))
                result = _saveService.SaveRaw(Document.CurrentFilePath, lineResult.Text, _currentEncoding, _settings.BackupBeforeSave)
                For Each warning In lineResult.Warnings
                    AddConversionMessage(warning)
                Next

                If lineResult.Warnings.Count > 0 Then
                    MessageTabs.SelectedItem = ConversionTab
                End If
            Else
                result = _saveService.Save(Document.CurrentFilePath, formatted, _currentEncoding, _settings.BackupBeforeSave)
            End If

            SetEditorText(EncodingDetectionService.NormalizeNewLines(formatted, _currentEncoding.NewLine))
            Document.IsDirty = False
            _recentFiles.Add(_settings, Document.CurrentFilePath)
            SaveSettings()
            RefreshRecentFilesMenu()
            UpdateDocumentMetadataStatus()
            UpdateChrome()
            ScheduleValidation()

            AddLog($"Saved {Path.GetFileName(result.Path)}.")
            _fileLog.Write("Save", $"{Path.GetFileName(result.Path)} encoding={_currentEncoding.Name} newline={_currentEncoding.NewLineName}")
            If Not String.IsNullOrWhiteSpace(result.BackupPath) Then
                AddLog($"Backup created: {Path.GetFileName(result.BackupPath)}")
            End If

            Return True
        Catch ex As Exception
            _lastException = ex
            _fileLog.WriteException("SaveFailed", ex)
            If showDialogs Then
                MessageBox.Show(Me, ex.Message, "Save failed", MessageBoxButton.OK, MessageBoxImage.Error)
            End If
            AddLog($"Save failed: {ex.Message}")
            Return False
        End Try
    End Function

    Private Function ValidateCurrentText(updateGrid As Boolean) As Boolean
        Try
            Dim text = CurrentText()
            Dim diagnostics = _validator.Validate(text, _currentFormat)
            ReplaceDiagnostics(diagnostics)

            If diagnostics.Count > 0 Then
                ParseStatusText.Text = "Invalid JSON"
                Return False
            End If

            ParseStatusText.Text = "Valid JSON"
            If updateGrid AndAlso Not _gridDisabled Then
                Dim state = CaptureGridState()
                Dim parsed = _parser.Parse(text, _currentFormat)
                SetGridRoot(parsed.Root, state, state.SelectedPointer, bringIntoView:=False)
            End If

            ScheduleFoldingUpdate()

            Return True
        Catch ex As Exception
            _lastException = ex
            ReplaceDiagnostics({New ValidationDiagnostic("Error", ex.Message)})
            ParseStatusText.Text = "Invalid JSON"
            AddLog($"Validation failed: {ex.Message}")
            Return False
        Finally
            UpdateChrome()
        End Try
    End Function

    Private Async Function ValidateCurrentTextAsync(updateGrid As Boolean) As Task(Of Boolean)
        Dim capturedText = CurrentText()

        Try
            Dim diagnostics = Await Task.Run(Function() _validator.Validate(capturedText, _currentFormat))
            If Not String.Equals(capturedText, CurrentText(), StringComparison.Ordinal) Then
                Return False
            End If

            ReplaceDiagnostics(diagnostics)
            If diagnostics.Count > 0 Then
                ParseStatusText.Text = "Invalid JSON"
                UpdateChrome()
                Return False
            End If

            ParseStatusText.Text = "Valid JSON"
            If updateGrid AndAlso Not _gridDisabled Then
                Dim state = CaptureGridState()
                Dim parsed = Await Task.Run(Function() _parser.Parse(capturedText, _currentFormat))
                If String.Equals(capturedText, CurrentText(), StringComparison.Ordinal) Then
                    SetGridRoot(parsed.Root, state, state.SelectedPointer, bringIntoView:=False)
                End If
            End If

            ScheduleFoldingUpdate()

            UpdateChrome()
            Return True
        Catch ex As Exception
            _lastException = ex
            ReplaceDiagnostics({New ValidationDiagnostic("Error", ex.Message)})
            ParseStatusText.Text = "Invalid JSON"
            UpdateChrome()
            AddLog($"Validation failed: {ex.Message}")
            Return False
        End Try
    End Function

    Private Sub SetGridRoot(root As JsonTreeNode,
                            Optional state As GridViewState = Nothing,
                            Optional selectedPointer As String = Nothing,
                            Optional bringIntoView As Boolean = False)
        Document.RootNode = root
        _gridIsCurrent = root IsNot Nothing

        Dim restoreState = If(state, CaptureGridState())
        Dim targetPointer = If(selectedPointer, restoreState?.SelectedPointer)
        If String.IsNullOrEmpty(targetPointer) AndAlso restoreState IsNot Nothing Then
            targetPointer = restoreState.AnchorPointer
        End If

        Dim target = If(String.IsNullOrEmpty(targetPointer), _documentState.ResolveRestoreTarget(root, restoreState), _documentState.FindNodeByPointer(root, targetPointer))
        If target Is Nothing Then
            target = _documentState.ResolveRestoreTarget(root, restoreState)
        End If

        If target IsNot Nothing Then
            targetPointer = target.JsonPointer
        End If

        Dim visibleRoot = root
        Dim query = If(GridFilterBox?.Text, "")
        Dim recordState = String.IsNullOrWhiteSpace(query)
        If Not recordState Then
            visibleRoot = _gridFilter.Filter(root, query)
        End If

        SetGridItems(visibleRoot, restoreState, targetPointer, bringIntoView, recordState)
        JsonTree.IsEnabled = Not _gridDisabled AndAlso recordState
        NodeCountStatusText.Text = $"{_treeStats.CountNodes(root)} nodes"
        RebindTableView(root)
    End Sub

    Private Sub SetGridItems(root As JsonTreeNode,
                             state As GridViewState,
                             selectedPointer As String,
                             bringIntoView As Boolean,
                             recordState As Boolean)
        Dim expanded = New HashSet(Of String)(StringComparer.Ordinal)
        If state IsNot Nothing Then
            For Each pointer In state.ExpandedPointers
                expanded.Add(pointer)
            Next
        End If

        expanded.Add("")
        For Each pointer In DocumentStateService.GetAncestorPointers(selectedPointer)
            expanded.Add(pointer)
        Next

        _gridRootView = GridNodeViewModel.FromNode(root, expanded, selectedPointer)
        Dim roots = New ObservableCollection(Of GridNodeViewModel)()
        If _gridRootView IsNot Nothing Then
            roots.Add(_gridRootView)
        End If

        JsonTree.ItemsSource = roots

        If recordState Then
            Dim caretOffset = If(state Is Nothing, 0, state.TextCaretOffset)
            Dim scrollOffset = If(state Is Nothing, 0, state.TextScrollOffset)
            _lastGridState = _documentState.CreateState(If(selectedPointer, ""), expanded, If(selectedPointer, ""), caretOffset, scrollOffset)
        End If

        UpdatePointerStatus(selectedPointer)

        If bringIntoView AndAlso Not String.IsNullOrEmpty(selectedPointer) Then
            Dispatcher.BeginInvoke(Sub() BringPointerIntoView(selectedPointer), DispatcherPriority.Background)
        End If
    End Sub

    Private Sub ApplyGridToText()
        SyncGridToText(showText:=True)
    End Sub

    Private Sub SyncGridToText(showText As Boolean, Optional focusPointer As String = Nothing, Optional state As GridViewState = Nothing)
        If Document.RootNode Is Nothing Then
            If Not ValidateCurrentText(updateGrid:=True) Then
                Return
            End If
        End If

        If Document.RootNode Is Nothing Then
            Return
        End If

        Try
            ApplyPrimitiveInference(Document.RootNode)
            Dim restoreState = If(state, CaptureGridState(focusPointer))
            Dim text = _serializer.Serialize(Document.RootNode)
            SetEditorText(text)
            Dim parsed = _parser.Parse(text, _currentFormat)
            Dim targetPointer = If(focusPointer, restoreState.SelectedPointer)
            SetGridRoot(parsed.Root, restoreState, targetPointer, bringIntoView:=Not showText)

            Document.IsDirty = True
            ParseStatusText.Text = "Valid JSON"
            ReplaceDiagnostics(Array.Empty(Of ValidationDiagnostic)())
            UpdateChrome()
            ScheduleRecoverySnapshot()
            If showText Then
                ShowTextTab(targetPointer)
            Else
                MoveTextCaretToPointer(targetPointer)
            End If
            AddLog("Applied grid changes to text.")
        Catch ex As Exception
            _lastException = ex
            MessageBox.Show(Me, ex.Message, "Grid serialization failed", MessageBoxButton.OK, MessageBoxImage.Error)
            AddLog($"Grid serialization failed: {ex.Message}")
        End Try
    End Sub

    Private Sub RefreshGridView()
        If Document.RootNode Is Nothing Then
            SetGridRoot(Nothing)
            Return
        End If

        Dim query = GridFilterBox.Text
        If String.IsNullOrWhiteSpace(query) Then
            Dim state = If(_filterRestoreState, CaptureGridState())
            _filterRestoreState = Nothing
            SetGridRoot(Document.RootNode, state, state.SelectedPointer, bringIntoView:=True)
            Return
        End If

        If _filterRestoreState Is Nothing Then
            _filterRestoreState = CaptureGridState()
        End If

        Dim visibleRoot = _gridFilter.Filter(Document.RootNode, query)
        SetGridItems(visibleRoot, _filterRestoreState, _filterRestoreState.SelectedPointer, bringIntoView:=False, recordState:=False)
        JsonTree.IsEnabled = False
        NodeCountStatusText.Text = $"{_treeStats.CountNodes(Document.RootNode)} nodes"
    End Sub

    Private Function CanEditGrid(Optional showMessage As Boolean = True) As Boolean
        If _gridDisabled Then
            If showMessage Then
                MessageBox.Show(Me, "Grid editing is disabled for this large file. Text mode remains available.", "Grid disabled", MessageBoxButton.OK, MessageBoxImage.Information)
            End If
            Return False
        End If

        If GridFilterBox IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(GridFilterBox.Text) Then
            If showMessage Then
                MessageBox.Show(Me, "Clear the grid filter before editing. Filtered rows are a read-only view.", "Filter active", MessageBoxButton.OK, MessageBoxImage.Information)
            End If
            Return False
        End If

        Return True
    End Function

    Private Sub CaptureGridUndo()
        If Document.RootNode IsNot Nothing Then
            _gridUndo.Capture(Document.RootNode)
        End If
    End Sub

    Private Sub OpenTable_Click(sender As Object, e As RoutedEventArgs)
        Dim node = GetNodeFromSender(sender)
        If node IsNot Nothing Then
            OpenTableView(node)
        End If
    End Sub

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

    Private Sub TableApplySort_Click(sender As Object, e As RoutedEventArgs)
        ApplyTableSortToStructure()
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

    Private Sub TableAddRow_Click(sender As Object, e As RoutedEventArgs)
        AddTableRow()
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

        If selectPointer IsNot Nothing Then
            SelectPointerInGrid(selectPointer, bringIntoView:=True)
        End If
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

    Private Sub AfterGridOperation(message As String, Optional focusPointer As String = Nothing)
        Dim state = CaptureGridState(focusPointer)
        SyncGridToText(showText:=False, focusPointer:=focusPointer, state:=state)
        AddLog(message)
    End Sub

    Private Function GetNodeFromSender(sender As Object) As JsonTreeNode
        Dim element = TryCast(sender, FrameworkElement)
        Return GetNodeFromDataContext(element?.DataContext)
    End Function

    Private Function GetNodeFromDependencyObject(source As DependencyObject) As JsonTreeNode
        Dim current = source
        While current IsNot Nothing
            Dim element = TryCast(current, FrameworkElement)
            If element IsNot Nothing Then
                Dim node = GetNodeFromDataContext(element.DataContext)
                If node IsNot Nothing Then
                    Return node
                End If
            End If

            current = VisualTreeHelper.GetParent(current)
        End While

        Return Nothing
    End Function

    Private Shared Function GetNodeFromDataContext(dataContext As Object) As JsonTreeNode
        Dim vm = TryCast(dataContext, GridNodeViewModel)
        If vm IsNot Nothing Then
            Return vm.Model
        End If

        Return TryCast(dataContext, JsonTreeNode)
    End Function

    Private Function GetTypeLabel(kind As JsonNodeKind) As String
        Select Case kind
            Case JsonNodeKind.ObjectValue
                Return "object"
            Case JsonNodeKind.ArrayValue
                Return "array"
            Case JsonNodeKind.StringValue
                Return "string"
            Case JsonNodeKind.NumberValue
                Return "number"
            Case JsonNodeKind.BooleanValue
                Return "boolean"
            Case JsonNodeKind.NullValue
                Return "null"
            Case Else
                Return "unknown"
        End Select
    End Function

    Private Function GetFormatLabel(format As JsonInputFormat) As String
        Select Case format
            Case JsonInputFormat.JsonC
                Return "JSONC"
            Case JsonInputFormat.Json5
                Return "JSON5"
            Case JsonInputFormat.JsonLines
                Return "JSON Lines"
            Case Else
                Return "JSON"
        End Select
    End Function

    Private Function LocalText(english As String, japanese As String) As String
        If String.Equals(_language, "ja", StringComparison.OrdinalIgnoreCase) Then
            Return japanese
        End If

        Return english
    End Function

    Private Sub ApplyLanguage()
        If FileMenu Is Nothing OrElse ActionHeader Is Nothing Then
            Return
        End If

        FileMenu.Header = LocalText("_File", "ファイル(_F)")
        NewMenuItem.Header = LocalText("_New", "新規(_N)")
        OpenMenuItem.Header = LocalText("_Open...", "開く(_O)...")
        RecentFilesMenuItem.Header = LocalText("_Recent", "最近使ったファイル(_R)")
        SaveMenuItem.Header = LocalText("_Save", "保存(_S)")
        SaveAsMenuItem.Header = LocalText("Save _As...", "名前を付けて保存(_A)...")
        RecoverMenuItem.Header = LocalText("_Recover Latest Snapshot", "最新スナップショットを復元(_R)")
        SettingsMenuItem.Header = LocalText("S_ettings...", "設定(_E)...")
        ExitMenuItem.Header = LocalText("E_xit", "終了(_X)")

        EditMenu.Header = LocalText("_Edit", "編集(_E)")
        ValidateMenuItem.Header = LocalText("_Validate and Sync Text to Grid", "検証してテキストをグリッドへ同期(_V)")
        ApplyGridMenuItem.Header = LocalText("_Apply Grid to Text", "グリッドをテキストへ反映(_A)")
        FormatMenuItem.Header = LocalText("_Format JSON", "JSON整形(_F)")
        UndoGridMenuItem.Header = LocalText("_Undo Grid Operation", "グリッド操作を元に戻す(_U)")
        RedoGridMenuItem.Header = LocalText("_Redo Grid Operation", "グリッド操作をやり直す(_R)")
        FindNextMenuItem.Header = LocalText("_Find Next", "次を検索(_F)")
        ReplaceMenuItem.Header = LocalText("_Replace", "置換(_R)")

        ViewMenu.Header = LocalText("_View", "表示(_V)")
        TextModeMenuItem.Header = LocalText("_Text Mode", "テキストモード(_T)")
        GridModeMenuItem.Header = LocalText("_Grid Mode", "グリッドモード(_G)")
        SchemaMenu.Header = LocalText("_Schema", "スキーマ(_S)")
        LoadSchemaMenuItem.Header = LocalText("_Load Local Schema...", "ローカルSchemaを読み込む(_L)...")
        LoadSchemaFromUrlMenuItem.Header = LocalText("Load Schema from $schema _URL...", "$schema URLからSchemaを取得(_U)...")
        ClearSchemaMenuItem.Header = LocalText("_Clear Schema", "Schemaを解除(_C)")
        ValidateSchemaMenuItem.Header = LocalText("_Validate with Schema", "Schemaで検証(_V)")
        AllowExternalSchemaMenuItem.Header = LocalText("Allow _External HTTPS Schema (off by default)", "外部HTTPS Schemaを許可(既定OFF)(_E)")
        ConvertMenu.Header = LocalText("_Convert", "変換(_C)")
        ExportXmlMenuItem.Header = LocalText("Export as _XML...", "XMLへExport(_X)...")
        ExportYamlMenuItem.Header = LocalText("Export as _YAML...", "YAMLへExport(_Y)...")
        ImportXmlMenuItem.Header = LocalText("Open XML as _JSON...", "XMLをJSONとして開く(_J)...")
        ImportYamlMenuItem.Header = LocalText("Open YAML as JS_ON...", "YAMLをJSONとして開く(_O)...")
        HelpMenu.Header = LocalText("_Help", "ヘルプ(_H)")
        CopyDiagnosticsMenuItem.Header = LocalText("_Copy Diagnostics", "診断情報をコピー(_C)")
        AboutMenuItem.Header = LocalText("_About Visual JSON", "Visual JSONについて(_A)")

        NewButton.Content = LocalText("New", "新規")
        OpenButton.Content = LocalText("Open", "開く")
        SaveButton.Content = LocalText("Save", "保存")
        SaveAsButton.Content = LocalText("Save As", "別名保存")
        ValidateButton.Content = LocalText("Validate", "検証")
        FormatButton.Content = LocalText("Format", "整形")
        ApplyGridButton.Content = LocalText("Apply Grid", "反映")
        UndoGridButton.Content = LocalText("Undo", "元に戻す")
        RedoGridButton.Content = LocalText("Redo", "やり直し")
        DiagnosticsButton.Content = LocalText("Diagnostics", "診断")
        LanguageLabel.Text = LocalText("Language", "言語")

        TextTab.Header = LocalText("Text", "テキスト")
        GridTab.Header = LocalText("Grid", "グリッド")
        SyntaxTab.Header = LocalText("Syntax", "構文")
        LogTab.Header = LocalText("Log", "ログ")
        SchemaResultTab.Header = LocalText("Schema", "スキーマ")
        ConversionTab.Header = LocalText("Conversion", "変換")
        SyntaxSeverityColumn.Header = LocalText("Severity", "重大度")
        SyntaxCodeColumn.Header = LocalText("Code", "コード")
        SyntaxLineColumn.Header = LocalText("Line", "行")
        SyntaxColumnColumn.Header = LocalText("Column", "列")
        SyntaxMessageColumn.Header = LocalText("Message", "メッセージ")
        SchemaSeverityColumn.Header = LocalText("Severity", "重大度")
        SchemaCodeColumn.Header = LocalText("Code", "コード")
        SchemaLineColumn.Header = LocalText("Line", "行")
        SchemaPointerColumn.Header = LocalText("Pointer", "ポインタ")
        SchemaPathColumn.Header = LocalText("SchemaPath", "Schemaパス")
        SchemaMessageColumn.Header = LocalText("Message", "メッセージ")
        ShowSchemaDefinitionButton.Content = LocalText("Show Schema Definition", "Schema定義を表示")
        UpdateSchemaStatus()
        FindLabel.Text = LocalText("Find", "検索")
        FindNextButton.Content = LocalText("Next", "次")
        FindPrevButton.Content = LocalText("Prev", "前")
        ReplaceLabel.Text = LocalText("Replace", "置換")
        ReplaceButton.Content = LocalText("Replace", "置換")
        ReplaceAllButton.Content = LocalText("All", "すべて")
        CaseSensitiveBox.Content = LocalText("Aa", "Aa")
        RegexSearchBox.Content = LocalText(".*", ".*")
        AutoPairBox.Content = LocalText("Pairs", "補完")
        GridFilterLabel.Text = LocalText("Filter", "フィルタ")
        ClearFilterButton.Content = LocalText("Clear", "クリア")
        GripHeader.Text = LocalText("Grip", "移動")
        KeyHeader.Text = LocalText("Key", "キー")
        ValueHeader.Text = LocalText("Value", "値")
        TypeHeader.Text = LocalText("Type", "型")
        PathHeader.Text = LocalText("Path", "パス")
        ActionHeader.Text = LocalText("Action", "操作")
        AddChildActionText = LocalText("+ Child", "+ 子")
        AddSiblingActionText = LocalText("+ Row", "+ 行")
        DeleteActionText = LocalText("Del", "削除")
        MoveUpActionText = LocalText("Up", "上")
        MoveDownActionText = LocalText("Down", "下")
        DragGripToolTipText = LocalText("Drag", "ドラッグ")
        TableActionText = LocalText("Table", "テーブル")
        TableBackButton.Content = LocalText("< List", "< リスト")
        TableAddRowButton.Content = LocalText("+ Row", "+ 行")
        TableAddColumnButton.Content = LocalText("+ Column", "+ 列")
        TableApplySortButton.Content = LocalText("Apply to Structure", "構造へ反映")
        TableHelpButton.Content = LocalText("Help", "ヘルプ")
        UpdateTableSubjectText()
        RefreshRecentFilesMenu()
    End Sub

    Private Sub ApplyPrimitiveInference(node As JsonTreeNode)
        If node Is Nothing Then
            Return
        End If

        If node.CanEditValue Then
            _typeInference.ApplyToNode(node)
        End If

        For Each child In node.Children
            ApplyPrimitiveInference(child)
        Next
    End Sub

    Private Sub ReplaceDiagnostics(items As IEnumerable(Of ValidationDiagnostic))
        Document.Diagnostics.Clear()
        For Each item In items
            Document.Diagnostics.Add(item)
        Next

        ' Only updates the error-line marker set and invalidates visible lines; cheap at any size.
        _editor.ApplySyntaxHighlighting(Document.Diagnostics)
    End Sub

    Private Sub ScheduleValidation()
        _validationTimer.Stop()
        _validationTimer.Start()
    End Sub

    Private Sub ScheduleRecoverySnapshot()
        _recoveryTimer.Stop()
        If Document.IsDirty Then
            _recoveryTimer.Start()
        End If
    End Sub

    Private Function ConfirmDiscardUnsavedChanges() As Boolean
        If Not Document.IsDirty Then
            Return True
        End If

        Dim result = MessageBox.Show(Me, "Save changes before continuing?", "Unsaved changes", MessageBoxButton.YesNoCancel, MessageBoxImage.Question)
        If result = MessageBoxResult.Cancel Then
            Return False
        End If

        If result = MessageBoxResult.Yes Then
            Return SaveCurrent(saveAs:=False)
        End If

        Return True
    End Function

    Private Sub PromptForRecoveryCandidates()
        Dim candidates = _recoveryService.ListCandidates()
        If candidates.Count = 0 Then
            Return
        End If

        Dim latest = candidates(0)
        Dim message = $"Recovery snapshots are available.{Environment.NewLine}{Environment.NewLine}Yes: open latest snapshot{Environment.NewLine}No: discard all snapshots{Environment.NewLine}Cancel: keep snapshots for later"
        Dim result = MessageBox.Show(Me, message, "Recovery", MessageBoxButton.YesNoCancel, MessageBoxImage.Information)

        If result = MessageBoxResult.Yes Then
            LoadRecoveryCandidate(latest)
        ElseIf result = MessageBoxResult.No Then
            _recoveryService.DeleteAll()
            AddLog("Recovery snapshots discarded.")
        End If
    End Sub

    Private Sub RecoverLatestCandidate()
        Dim candidates = _recoveryService.ListCandidates()
        If candidates.Count = 0 Then
            MessageBox.Show(Me, "No recovery snapshots were found.", "Recovery", MessageBoxButton.OK, MessageBoxImage.Information)
            Return
        End If

        LoadRecoveryCandidate(candidates(0))
    End Sub

    Private Sub LoadRecoveryCandidate(candidate As RecoveryCandidate)
        If candidate Is Nothing Then
            Return
        End If

        If Not ConfirmDiscardUnsavedChanges() Then
            Return
        End If

        Try
            Dim text = _recoveryService.Load(candidate)
            SetDocument(text, "", isDirty:=True)
            AddLog($"Loaded recovery snapshot: {candidate.DisplayName}")
        Catch ex As Exception
            _lastException = ex
            MessageBox.Show(Me, ex.Message, "Recovery failed", MessageBoxButton.OK, MessageBoxImage.Error)
            AddLog($"Recovery failed: {ex.Message}")
        End Try
    End Sub

    Private Function FindText(forward As Boolean) As Boolean
        Dim query = SearchBox.Text
        If String.IsNullOrEmpty(query) Then
            SearchBox.Focus()
            Return False
        End If

        Try
            Dim text = CurrentText()
            Dim matches = _searchReplace.FindMatches(text, query, GetSearchOptions())
            If matches.Count = 0 Then
                SearchCountText.Text = LocalText("0 matches", "0件")
                Return False
            End If

            Dim selectionStart = _editor.GetSelectionStart()
            Dim selectionLength = _editor.GetSelectionLength()
            Dim anchor = If(forward, selectionStart + selectionLength, Math.Max(0, selectionStart - 1))
            Dim match As SearchMatch

            If forward Then
                match = matches.FirstOrDefault(Function(item) item.StartIndex >= anchor)
                If match Is Nothing Then
                    match = matches(0)
                End If
            Else
                match = matches.LastOrDefault(Function(item) item.StartIndex <= anchor)
                If match Is Nothing Then
                    match = matches(matches.Count - 1)
                End If
            End If

            EditorTabs.SelectedIndex = 0
            _editor.SelectText(match.StartIndex, match.Length)
            Return True
        Catch ex As Exception
            SearchCountText.Text = LocalText("Invalid pattern", "パターン不正")
            AddLog($"Search failed: {ex.Message}")
            Return False
        End Try
    End Function

    Private Sub FocusSearchBox()
        EditorTabs.SelectedItem = TextTab
        SearchBox.Focus()
        SearchBox.SelectAll()
    End Sub

    Private Sub FocusReplaceBox()
        EditorTabs.SelectedItem = TextTab
        ReplaceBox.Focus()
        ReplaceBox.SelectAll()
    End Sub

    Private Function GetSearchOptions() As SearchOptions
        Return New SearchOptions With {
            .MatchCase = CaseSensitiveBox IsNot Nothing AndAlso CaseSensitiveBox.IsChecked.GetValueOrDefault(False),
            .UseRegex = RegexSearchBox IsNot Nothing AndAlso RegexSearchBox.IsChecked.GetValueOrDefault(False)
        }
    End Function

    Private Function UpdateSearchHighlights() As Integer
        If _editor Is Nothing OrElse SearchBox Is Nothing OrElse SearchCountText Is Nothing Then
            Return 0
        End If

        Dim query = SearchBox.Text
        If String.IsNullOrEmpty(query) Then
            _editor.SetSearchHighlights(Array.Empty(Of SearchMatch)())
            SearchCountText.Text = ""
            Return 0
        End If

        Try
            Dim matches = _searchReplace.FindMatches(CurrentText(), query, GetSearchOptions(), 1001)
            _editor.SetSearchHighlights(matches, 1000)
            If matches.Count > 1000 Then
                SearchCountText.Text = LocalText("1000+ matches", "1000件以上")
            Else
                SearchCountText.Text = LocalText($"{matches.Count} matches", $"{matches.Count}件")
            End If

            Return matches.Count
        Catch ex As Exception
            _editor.SetSearchHighlights(Array.Empty(Of SearchMatch)())
            SearchCountText.Text = LocalText("Invalid pattern", "パターン不正")
            _lastException = ex
            Return 0
        End Try
    End Function

    Private Sub ReplaceCurrent()
        If String.IsNullOrEmpty(SearchBox.Text) Then
            FocusSearchBox()
            Return
        End If

        Try
            Dim result = _searchReplace.ReplaceSelection(CurrentText(), SearchBox.Text, ReplaceBox.Text, _editor.GetSelectionStart(), _editor.GetSelectionLength(), GetSearchOptions())
            If result.Count = 0 Then
                If Not FindText(forward:=True) Then
                    Return
                End If

                result = _searchReplace.ReplaceSelection(CurrentText(), SearchBox.Text, ReplaceBox.Text, _editor.GetSelectionStart(), _editor.GetSelectionLength(), GetSearchOptions())
            End If

            If result.Count = 0 Then
                Return
            End If

            ApplyReplacementResult(result)
            AddLog("Replaced 1 match.")
            FindText(forward:=True)
        Catch ex As Exception
            _lastException = ex
            MessageBox.Show(Me, ex.Message, LocalText("Replace failed", "置換失敗"), MessageBoxButton.OK, MessageBoxImage.Warning)
            AddLog($"Replace failed: {ex.Message}")
        End Try
    End Sub

    Private Function ReplaceAllCurrent() As Integer
        If String.IsNullOrEmpty(SearchBox.Text) Then
            FocusSearchBox()
            Return 0
        End If

        Try
            Dim timer = Stopwatch.StartNew()
            Dim result = _searchReplace.ReplaceAll(CurrentText(), SearchBox.Text, ReplaceBox.Text, GetSearchOptions())
            timer.Stop()
            If result.Count > 0 Then
                ApplyReplacementResult(result)
            End If

            AddLog($"Replaced {result.Count} match(es) in {timer.Elapsed.TotalMilliseconds:n1} ms.")
            Return result.Count
        Catch ex As Exception
            _lastException = ex
            MessageBox.Show(Me, ex.Message, LocalText("Replace All failed", "一括置換失敗"), MessageBoxButton.OK, MessageBoxImage.Warning)
            AddLog($"Replace All failed: {ex.Message}")
            Return 0
        End Try
    End Function

    Private Sub ApplyReplacementResult(result As ReplaceResult)
        SetEditorText(result.Text)
        _editor.SetCaretOffset(Math.Min(result.NextOffset, result.Text.Length))
        Document.IsDirty = True
        _gridIsCurrent = False
        ParseStatusText.Text = "Not checked"
        UpdateCaretStatus()
        UpdateSearchHighlights()
        UpdateChrome()
        ScheduleValidation()
        ScheduleRecoverySnapshot()
    End Sub

    Private Async Sub ScheduleFoldingUpdate()
        If _editor Is Nothing OrElse _suppressAutomaticFolding Then
            Return
        End If

        _foldingUpdateVersion += 1
        Dim version = _foldingUpdateVersion
        Dim text = CurrentText()

        Try
            Dim ranges = Await Task.Run(Function() _foldingService.CreateRanges(text))
            Dim operation = Dispatcher.BeginInvoke(Sub() ApplyFoldingUpdate(version, text, ranges), DispatcherPriority.ApplicationIdle)
        Catch ex As Exception
            _lastException = ex
            If Dispatcher.CheckAccess() Then
                AddLog($"Folding update failed: {ex.Message}")
            Else
                Dim operation = Dispatcher.BeginInvoke(Sub() AddLog($"Folding update failed: {ex.Message}"), DispatcherPriority.Background)
            End If
        End Try
    End Sub

    Private Sub ApplyFoldingUpdate(version As Integer, text As String, ranges As IReadOnlyList(Of JsonFoldingRange))
        Try
            If version <> _foldingUpdateVersion OrElse Not String.Equals(text, CurrentText(), StringComparison.Ordinal) Then
                Return
            End If

            _editor.ApplyJsonFoldings(ranges)
        Catch ex As Exception
            _lastException = ex
            AddLog($"Folding update failed: {ex.Message}")
        End Try
    End Sub

    Private Function ShowGridTab() As Boolean
        If Not SwitchTextToGrid() Then
            Return False
        End If

        _handlingTabSwitch = True
        Try
            EditorTabs.SelectedItem = GridTab
        Finally
            _handlingTabSwitch = False
        End Try

        JsonTree.Focus()
        Return True
    End Function

    Private Sub ShowTextTab(Optional focusPointer As String = Nothing)
        SwitchGridToText(focusPointer)

        _handlingTabSwitch = True
        Try
            EditorTabs.SelectedItem = TextTab
        Finally
            _handlingTabSwitch = False
        End Try

        JsonEditor.Focus()
    End Sub

    Private Function SwitchTextToGrid() As Boolean
        If _gridDisabled Then
            MessageBox.Show(Me, "Grid editing is disabled for this large file. Text mode remains available.", "Grid disabled", MessageBoxButton.OK, MessageBoxImage.Information)
            Return False
        End If

        Dim caretOffset = _editor.GetCaretOffset()
        Dim scrollOffset = _editor.GetVerticalOffset()
        Dim priorState = CaptureGridState(textCaretOffset:=caretOffset, textScrollOffset:=scrollOffset)

        If Not _gridIsCurrent OrElse Document.RootNode Is Nothing Then
            If Not ValidateCurrentText(updateGrid:=True) Then
                Return False
            End If
        End If

        If Document.RootNode Is Nothing Then
            Return False
        End If

        Dim pointer = _documentState.GetPointerAtOffset(Document.RootNode, caretOffset)
        _lastGridState = _documentState.CreateState(pointer, priorState.ExpandedPointers, pointer, caretOffset, scrollOffset)
        SelectPointerInGrid(pointer, bringIntoView:=True)
        UpdatePointerStatus(pointer)
        Return True
    End Function

    Private Sub SwitchGridToText(Optional focusPointer As String = Nothing)
        Dim pointer = If(focusPointer, GetSelectedGridPointer())
        _lastGridState = CaptureGridState(pointer)

        If Not String.IsNullOrEmpty(pointer) AndAlso MoveTextCaretToPointer(pointer) Then
            UpdatePointerStatus(pointer)
            Return
        End If

        If _lastGridState IsNot Nothing Then
            _editor.SetCaretOffset(_lastGridState.TextCaretOffset)
            _editor.ScrollToVerticalOffset(_lastGridState.TextScrollOffset)
        End If

        UpdateCaretStatus()
    End Sub

    Private Function CaptureGridState(Optional selectedPointer As String = Nothing,
                                      Optional textCaretOffset As Integer? = Nothing,
                                      Optional textScrollOffset As Double? = Nothing) As GridViewState
        Dim pointer = If(selectedPointer, GetSelectedGridPointer())
        If String.IsNullOrEmpty(pointer) AndAlso _lastGridState IsNot Nothing Then
            pointer = _lastGridState.SelectedPointer
        End If

        Dim expanded = New HashSet(Of String)(StringComparer.Ordinal)
        If _gridRootView IsNot Nothing Then
            _gridRootView.CollectExpandedPointers(expanded)
        End If

        If expanded.Count = 0 AndAlso _lastGridState IsNot Nothing Then
            For Each item In _lastGridState.ExpandedPointers
                expanded.Add(item)
            Next
        End If

        expanded.Add("")
        For Each item In DocumentStateService.GetAncestorPointers(pointer)
            expanded.Add(item)
        Next

        Dim caret = If(textCaretOffset.HasValue, textCaretOffset.Value, If(_editor Is Nothing, 0, _editor.GetCaretOffset()))
        Dim scroll = If(textScrollOffset.HasValue, textScrollOffset.Value, If(_editor Is Nothing, 0, _editor.GetVerticalOffset()))
        Dim anchor = If(String.IsNullOrEmpty(pointer) AndAlso _lastGridState IsNot Nothing, _lastGridState.AnchorPointer, pointer)
        Return _documentState.CreateState(pointer, expanded, anchor, caret, scroll)
    End Function

    Private Function GetSelectedGridPointer() As String
        Dim node = GetNodeFromDataContext(JsonTree?.SelectedItem)
        If node IsNot Nothing Then
            Return node.JsonPointer
        End If

        Dim selectedFromVm = _gridRootView?.FindSelectedPointer()
        If selectedFromVm IsNot Nothing Then
            Return selectedFromVm
        End If

        If _lastGridState IsNot Nothing Then
            Return _lastGridState.SelectedPointer
        End If

        Return Nothing
    End Function

    Private Sub SelectPointerInGrid(pointer As String, bringIntoView As Boolean)
        If _gridRootView Is Nothing Then
            Return
        End If

        Dim targetPointer = If(pointer, "")
        Dim target = _documentState.FindNodeByPointer(Document.RootNode, targetPointer)
        If target Is Nothing Then
            target = _documentState.ResolveRestoreTarget(Document.RootNode, _documentState.CreateState(targetPointer, Array.Empty(Of String)()))
            targetPointer = If(target?.JsonPointer, "")
        End If

        ClearGridSelection(_gridRootView)
        ExpandAndSelect(_gridRootView, targetPointer)
        _lastGridState = CaptureGridState(targetPointer)

        If bringIntoView Then
            Dispatcher.BeginInvoke(Sub() BringPointerIntoView(targetPointer), DispatcherPriority.Background)
        End If
    End Sub

    Private Shared Sub ClearGridSelection(vm As GridNodeViewModel)
        If vm Is Nothing Then
            Return
        End If

        vm.IsSelected = False
        For Each child In vm.Children
            ClearGridSelection(child)
        Next
    End Sub

    Private Shared Function ExpandAndSelect(vm As GridNodeViewModel, pointer As String) As Boolean
        If vm Is Nothing OrElse vm.Model Is Nothing Then
            Return False
        End If

        If String.Equals(vm.Model.JsonPointer, If(pointer, ""), StringComparison.Ordinal) Then
            vm.IsSelected = True
            Return True
        End If

        For Each child In vm.Children
            If ExpandAndSelect(child, pointer) Then
                vm.IsExpanded = True
                Return True
            End If
        Next

        Return False
    End Function

    Private Sub BringPointerIntoView(pointer As String)
        If _gridRootView Is Nothing Then
            Return
        End If

        Dim targetVm = _gridRootView.FindByPointer(pointer)
        If targetVm Is Nothing Then
            Return
        End If

        JsonTree.UpdateLayout()
        Dim item = FindTreeViewItem(JsonTree, targetVm)
        If item IsNot Nothing Then
            item.IsSelected = True
            item.BringIntoView()
            item.Focus()
        End If
    End Sub

    Private Function FindTreeViewItem(parent As ItemsControl, target As Object) As TreeViewItem
        If parent Is Nothing Then
            Return Nothing
        End If

        parent.UpdateLayout()
        Dim direct = TryCast(parent.ItemContainerGenerator.ContainerFromItem(target), TreeViewItem)
        If direct IsNot Nothing Then
            Return direct
        End If

        For Each item In parent.Items
            Dim container = TryCast(parent.ItemContainerGenerator.ContainerFromItem(item), TreeViewItem)
            Dim found = FindTreeViewItem(container, target)
            If found IsNot Nothing Then
                Return found
            End If
        Next

        Return Nothing
    End Function

    Private Function MoveTextCaretToPointer(pointer As String) As Boolean
        If Document.RootNode Is Nothing Then
            Return False
        End If

        Dim node = _documentState.FindNodeByPointer(Document.RootNode, pointer)
        If node Is Nothing Then
            node = _documentState.ResolveRestoreTarget(Document.RootNode, _documentState.CreateState(pointer, Array.Empty(Of String)()))
        End If

        If node Is Nothing Then
            Return False
        End If

        If node.SourceLine.HasValue AndAlso node.SourceColumn.HasValue Then
            _editor.MoveToLineColumn(node.SourceLine.Value, node.SourceColumn.Value)
        ElseIf node.SourceStartIndex.HasValue Then
            _editor.SetCaretOffset(node.SourceStartIndex.Value)
        Else
            Return False
        End If

        UpdateCaretStatus()
        Return True
    End Function

    Private Sub UpdateCaretStatus()
        If _editor Is Nothing Then
            Return
        End If

        Dim caret = _editor.GetLineColumnFromOffset(_editor.GetCaretOffset())
        CaretStatusText.Text = $"Ln {caret.Line}, Col {caret.Column}"

        If Object.ReferenceEquals(EditorTabs?.SelectedItem, TextTab) AndAlso Document.RootNode IsNot Nothing AndAlso _gridIsCurrent Then
            UpdatePointerStatus(_documentState.GetPointerAtOffset(Document.RootNode, _editor.GetCaretOffset()))
        End If
    End Sub

    Private Sub UpdatePointerStatus(pointer As String)
        If PointerStatusText Is Nothing Then
            Return
        End If

        PointerStatusText.Text = $"Pointer: {DocumentStateService.ToPointerDisplay(pointer)}"
    End Sub

    Private Sub UpdateChrome()
        Dim fileName = If(String.IsNullOrWhiteSpace(Document.CurrentFilePath), "Untitled", Path.GetFileName(Document.CurrentFilePath))
        Title = $"{fileName}{If(Document.IsDirty, " *", "")} - Visual JSON"
        FileStatusText.Text = If(String.IsNullOrWhiteSpace(Document.CurrentFilePath), "Untitled", Document.CurrentFilePath)
        DirtyStatusText.Text = If(Document.IsDirty, "Unsaved", "Saved")
        UpdateDocumentMetadataStatus()
        UndoGridMenuItem.IsEnabled = _gridUndo.CanUndo()
        UndoGridButton.IsEnabled = _gridUndo.CanUndo()
        RedoGridMenuItem.IsEnabled = _gridUndo.CanRedo()
        RedoGridButton.IsEnabled = _gridUndo.CanRedo()
    End Sub

    Private Sub UpdateDocumentMetadataStatus()
        If EncodingStatusText IsNot Nothing Then
            EncodingStatusText.Text = _currentEncoding.Name
        End If
        If NewLineStatusText IsNot Nothing Then
            NewLineStatusText.Text = _currentEncoding.NewLineName
        End If
    End Sub

    Private Sub AddLog(message As String)
        Document.Logs.Insert(0, $"{DateTime.Now:HH:mm:ss} {message}")
    End Sub

    Private Sub ApplySettingsToControls()
        If _settings Is Nothing Then
            _settings = AppSettings.CreateDefault()
        End If

        _suppressSettingsSave = True
        Try
            _language = _settings.Language
            If LanguageCombo IsNot Nothing Then
                For index = 0 To LanguageCombo.Items.Count - 1
                    Dim item = TryCast(LanguageCombo.Items(index), ComboBoxItem)
                    If item IsNot Nothing AndAlso String.Equals(TryCast(item.Tag, String), _language, StringComparison.OrdinalIgnoreCase) Then
                        LanguageCombo.SelectedIndex = index
                        Exit For
                    End If
                Next
            End If

            If AutoPairBox IsNot Nothing Then
                AutoPairBox.IsChecked = _settings.AutoCloseBrackets
            End If
            If _editor IsNot Nothing Then
                _editor.AutoPairingEnabled = _settings.AutoCloseBrackets
            End If
            If AllowExternalSchemaMenuItem IsNot Nothing Then
                AllowExternalSchemaMenuItem.IsChecked = _settings.AllowExternalSchema
            End If

            If _settings.Window IsNot Nothing Then
                Width = Math.Max(MinWidth, _settings.Window.Width)
                Height = Math.Max(MinHeight, _settings.Window.Height)
                If _settings.Window.X.HasValue Then
                    Left = _settings.Window.X.Value
                End If
                If _settings.Window.Y.HasValue Then
                    Top = _settings.Window.Y.Value
                End If
                If _settings.Window.Maximized Then
                    WindowState = System.Windows.WindowState.Maximized
                End If
            End If
        Finally
            _suppressSettingsSave = False
        End Try
    End Sub

    Private Sub SaveSettings()
        If _settings Is Nothing OrElse _suppressSettingsSave Then
            Return
        End If

        Try
            CaptureWindowSettings()
            _settingsService.Save(_settings)
        Catch ex As Exception
            _lastException = ex
            AddLog($"Settings save failed: {ex.Message}")
            _fileLog.WriteException("SettingsSaveFailed", ex)
        End Try
    End Sub

    Private Sub CaptureWindowSettings()
        If _settings.Window Is Nothing Then
            _settings.Window = New AppWindowSettings()
        End If

        _settings.Window.Maximized = WindowState = System.Windows.WindowState.Maximized
        Dim bounds = RestoreBounds
        If Not Double.IsNaN(bounds.Left) AndAlso Not Double.IsInfinity(bounds.Left) Then
            _settings.Window.X = bounds.Left
        End If
        If Not Double.IsNaN(bounds.Top) AndAlso Not Double.IsInfinity(bounds.Top) Then
            _settings.Window.Y = bounds.Top
        End If
        If bounds.Width >= MinWidth Then
            _settings.Window.Width = bounds.Width
        End If
        If bounds.Height >= MinHeight Then
            _settings.Window.Height = bounds.Height
        End If
    End Sub

    Private Sub RefreshRecentFilesMenu()
        If RecentFilesMenuItem Is Nothing Then
            Return
        End If

        RecentFilesMenuItem.Items.Clear()
        Dim files As List(Of String) = If(_settings Is Nothing OrElse _settings.RecentFiles Is Nothing, New List(Of String)(), _settings.RecentFiles)
        If files.Count = 0 Then
            Dim emptyItem = New MenuItem With {.Header = LocalText("(empty)", "(なし)"), .IsEnabled = False}
            RecentFilesMenuItem.Items.Add(emptyItem)
        Else
            For Each filePath In files
                Dim item = New MenuItem With {
                    .Header = IO.Path.GetFileName(filePath),
                    .ToolTip = filePath,
                    .Tag = filePath
                }
                AddHandler item.Click, AddressOf RecentFile_Click
                RecentFilesMenuItem.Items.Add(item)
            Next
        End If

        RecentFilesMenuItem.Items.Add(New Separator())
        Dim clearItem = New MenuItem With {.Header = LocalText("_Clear History", "履歴を消去(_C)")}
        AddHandler clearItem.Click, AddressOf ClearRecentFiles_Click
        RecentFilesMenuItem.Items.Add(clearItem)
    End Sub

    Private Sub RecentFile_Click(sender As Object, e As RoutedEventArgs)
        Dim item = TryCast(sender, MenuItem)
        Dim filePath = TryCast(item?.Tag, String)
        If String.IsNullOrWhiteSpace(filePath) Then
            Return
        End If

        If Not File.Exists(filePath) Then
            _recentFiles.Remove(_settings, filePath)
            SaveSettings()
            RefreshRecentFilesMenu()
            MessageBox.Show(Me, LocalText("The file no longer exists and was removed from Recent Files.", "ファイルが存在しないため履歴から削除しました。"), "Recent Files", MessageBoxButton.OK, MessageBoxImage.Information)
            Return
        End If

        If Not ConfirmDiscardUnsavedChanges() Then
            Return
        End If

        OpenDocumentPath(filePath, addToRecent:=True)
    End Sub

    Private Sub ClearRecentFiles_Click(sender As Object, e As RoutedEventArgs)
        _recentFiles.Clear(_settings)
        SaveSettings()
        RefreshRecentFilesMenu()
        AddLog("Recent file history cleared.")
    End Sub

    Private Sub Window_DragOver(sender As Object, e As DragEventArgs)
        If e.Data.GetDataPresent(DataFormats.FileDrop) Then
            e.Effects = DragDropEffects.Copy
            e.Handled = True
        End If
    End Sub

    Private Sub Window_Drop(sender As Object, e As DragEventArgs)
        If Not e.Data.GetDataPresent(DataFormats.FileDrop) Then
            Return
        End If

        Dim files = TryCast(e.Data.GetData(DataFormats.FileDrop), String())
        If files Is Nothing OrElse files.Length = 0 Then
            Return
        End If

        If Not ConfirmDiscardUnsavedChanges() Then
            Return
        End If

        OpenDocumentPath(files(0), addToRecent:=True)
        e.Handled = True
    End Sub

    Private Sub AddConversionMessage(message As String)
        ConversionList.Items.Insert(0, $"{DateTime.Now:HH:mm:ss} {message}")
    End Sub

    ' ---------- MVP-2: Schema validation ----------

    Private Sub LoadSchema_Click(sender As Object, e As RoutedEventArgs)
        Dim dialog = New OpenFileDialog With {
            .Filter = "JSON Schema (*.json;*.schema.json)|*.json;*.schema.json|All files (*.*)|*.*",
            .Title = "Load Local Schema"
        }

        If dialog.ShowDialog(Me) <> True Then
            Return
        End If

        Try
            _schemaText = _schemaResolver.LoadLocalSchema(dialog.FileName)
            _schemaSource = dialog.FileName
            UpdateSchemaStatus()
            AddLog($"Loaded schema {Path.GetFileName(dialog.FileName)}.")
            RunSchemaValidation(showTab:=True)
        Catch ex As Exception
            _lastException = ex
            MessageBox.Show(Me, ex.Message, LocalText("Schema load failed", "Schema読み込み失敗"), MessageBoxButton.OK, MessageBoxImage.Error)
            AddLog($"Schema load failed: {ex.Message}")
        End Try
    End Sub

    Private Async Sub LoadSchemaFromUrl_Click(sender As Object, e As RoutedEventArgs)
        Dim url = TryGetDollarSchemaUrl()
        If String.IsNullOrWhiteSpace(url) Then
            MessageBox.Show(Me, LocalText("The document has no $schema URL, or the document is not valid JSON.", "ドキュメントに$schema URLがないか、JSONが不正です。"), "Schema", MessageBoxButton.OK, MessageBoxImage.Information)
            Return
        End If

        Try
            ' External references are OFF by default (NFR-SEC-005); the fetch enforces
            ' HTTPS-only and blocks dangerous redirects (NFR-SEC-006/007).
            Dim allowExternal = AllowExternalSchemaMenuItem.IsChecked
            Dim schemaText = Await _schemaResolver.FetchExternalSchemaAsync(url, allowExternal)
            _schemaText = schemaText
            _schemaSource = url
            UpdateSchemaStatus()
            AddLog($"Loaded external schema from {url}.")
            RunSchemaValidation(showTab:=True)
        Catch ex As Exception
            _lastException = ex
            MessageBox.Show(Me, ex.Message, LocalText("Schema fetch blocked or failed", "Schema取得はブロックまたは失敗しました"), MessageBoxButton.OK, MessageBoxImage.Warning)
            AddLog($"Schema fetch blocked or failed: {ex.Message}")
        End Try
    End Sub

    Private Sub ClearSchema_Click(sender As Object, e As RoutedEventArgs)
        _schemaText = Nothing
        _schemaSource = Nothing
        SchemaList.ItemsSource = Nothing
        UpdateSchemaStatus()
        AddLog("Cleared schema.")
    End Sub

    Private Sub ValidateSchema_Click(sender As Object, e As RoutedEventArgs)
        RunSchemaValidation(showTab:=True)
    End Sub

    Private Sub RunSchemaValidation(showTab As Boolean)
        If String.IsNullOrWhiteSpace(_schemaText) Then
            MessageBox.Show(Me, LocalText("Load a local schema first. External URLs are off by default.", "先にローカルSchemaを読み込んでください。外部URLは既定で無効です。"), "Schema", MessageBoxButton.OK, MessageBoxImage.Information)
            Return
        End If

        If Not ValidateCurrentText(updateGrid:=False) Then
            MessageBox.Show(Me, LocalText("Fix syntax errors before schema validation.", "Schema検証の前に構文エラーを修正してください。"), "Schema", MessageBoxButton.OK, MessageBoxImage.Warning)
            Return
        End If

        Try
            Dim normalized = _preprocessor.Normalize(CurrentText(), _currentFormat)
            Dim documentRoot = _parser.Parse(CurrentText(), _currentFormat).Root
            Dim diagnostics = _schemaValidation.Validate(normalized, _schemaText, _schemaSource, documentRoot)

            SchemaList.ItemsSource = diagnostics
            If showTab Then
                MessageTabs.SelectedItem = SchemaResultTab
            End If

            If diagnostics.Count = 0 Then
                AddLog("Schema validation passed with no errors.")
            Else
                AddLog($"Schema validation found {diagnostics.Count} issue(s).")
            End If
        Catch ex As Exception
            _lastException = ex
            MessageBox.Show(Me, ex.Message, LocalText("Schema validation failed", "Schema検証に失敗しました"), MessageBoxButton.OK, MessageBoxImage.Error)
            AddLog($"Schema validation failed: {ex.Message}")
        End Try
    End Sub

    Private Function TryGetDollarSchemaUrl() As String
        Try
            Dim normalized = _preprocessor.Normalize(CurrentText(), _currentFormat)
            Using document = Text.Json.JsonDocument.Parse(normalized)
                If document.RootElement.ValueKind = Text.Json.JsonValueKind.Object Then
                    Dim schemaProperty As Text.Json.JsonElement = Nothing
                    If document.RootElement.TryGetProperty("$schema", schemaProperty) AndAlso schemaProperty.ValueKind = Text.Json.JsonValueKind.String Then
                        Return schemaProperty.GetString()
                    End If
                End If
            End Using
        Catch ex As Exception
            _lastException = ex
        End Try

        Return Nothing
    End Function

    Private Sub SchemaList_SelectionChanged(sender As Object, e As SelectionChangedEventArgs)
        Dim diagnostic = TryCast(SchemaList.SelectedItem, ValidationDiagnostic)
        If diagnostic Is Nothing OrElse Not diagnostic.Line.HasValue Then
            Return
        End If

        EditorTabs.SelectedIndex = 0
        _editor.MoveToLineColumn(diagnostic.Line.Value, If(diagnostic.Column, 1))
    End Sub

    Private Sub SchemaList_MouseDoubleClick(sender As Object, e As MouseButtonEventArgs)
        ShowSchemaDefinitionForSelection()
    End Sub

    Private Sub ShowSchemaDefinition_Click(sender As Object, e As RoutedEventArgs)
        ShowSchemaDefinitionForSelection()
    End Sub

    Private Sub ShowSchemaDefinitionForSelection()
        If String.IsNullOrWhiteSpace(_schemaText) Then
            MessageBox.Show(Me, LocalText("No schema is loaded.", "Schemaが読み込まれていません。"), "Schema", MessageBoxButton.OK, MessageBoxImage.Information)
            Return
        End If

        Dim diagnostic = TryCast(SchemaList.SelectedItem, ValidationDiagnostic)
        Dim selectionStart As Integer? = Nothing
        Dim header = If(_schemaSource, "")

        If diagnostic IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(diagnostic.SchemaPath) Then
            selectionStart = FindSchemaDefinitionOffset(diagnostic.SchemaPath)
            header = $"{header}  {diagnostic.SchemaPath}"
        End If

        Dim viewer = New SchemaViewWindow(LocalText("Schema Definition", "Schema定義"), _schemaText, selectionStart, header) With {.Owner = Me}
        viewer.ShowDialog()
    End Sub

    Private Function FindSchemaDefinitionOffset(schemaPath As String) As Integer?
        Try
            Dim pointer = If(schemaPath, "").TrimStart("#"c)
            Dim schemaRoot = _parser.Parse(_schemaText).Root
            Dim node = FindNodeByPointer(schemaRoot, pointer)

            ' Fall back to the nearest existing ancestor (e.g. "#/required" points at a keyword).
            While node Is Nothing AndAlso pointer.Contains("/"c)
                pointer = pointer.Substring(0, pointer.LastIndexOf("/"c))
                node = FindNodeByPointer(schemaRoot, pointer)
            End While

            If node IsNot Nothing AndAlso node.SourceStartIndex.HasValue Then
                Return node.SourceStartIndex.Value
            End If
        Catch ex As Exception
            _lastException = ex
        End Try

        Return Nothing
    End Function

    Private Function FindNodeByPointer(root As JsonTreeNode, pointer As String) As JsonTreeNode
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

    Private Sub UpdateSchemaStatus()
        Dim text As String
        If String.IsNullOrWhiteSpace(_schemaSource) Then
            text = LocalText("No schema", "Schema未設定")
            SchemaSourceText.Text = LocalText("No schema loaded.", "Schemaは読み込まれていません。")
        Else
            Dim name = If(_schemaSource.StartsWith("http", StringComparison.OrdinalIgnoreCase), _schemaSource, Path.GetFileName(_schemaSource))
            text = $"{LocalText("Schema", "Schema")}: {name}"
            SchemaSourceText.Text = _schemaSource
        End If

        SchemaStatusText.Text = text
    End Sub

    ' ---------- MVP-3: XML/YAML conversion ----------

    Private Sub ExportXml_Click(sender As Object, e As RoutedEventArgs)
        ExportDocument(toXml:=True)
    End Sub

    Private Sub ExportYaml_Click(sender As Object, e As RoutedEventArgs)
        ExportDocument(toXml:=False)
    End Sub

    Private Sub ExportDocument(toXml As Boolean)
        If Not ValidateCurrentText(updateGrid:=False) Then
            MessageBox.Show(Me, LocalText("Fix syntax errors before exporting.", "Exportの前に構文エラーを修正してください。"), LocalText("Invalid JSON", "不正なJSON"), MessageBoxButton.OK, MessageBoxImage.Warning)
            Return
        End If

        ' Spec 06 §8: the destination is chosen before the conversion runs.
        Dim dialog = New SaveFileDialog With {
            .Filter = If(toXml, "XML files (*.xml)|*.xml|All files (*.*)|*.*", "YAML files (*.yaml;*.yml)|*.yaml;*.yml|All files (*.*)|*.*"),
            .Title = If(toXml, "Export as XML", "Export as YAML"),
            .DefaultExt = If(toXml, ".xml", ".yaml")
        }

        If dialog.ShowDialog(Me) <> True Then
            Return
        End If

        Try
            Dim standardJson = _formatter.Format(CurrentText(), _currentFormat)
            Dim result = If(toXml, _xmlConversion.ConvertJsonToXml(standardJson), _yamlConversion.ConvertJsonToYaml(standardJson))

            Dim commentWarning = LocalText("Comments are not preserved in the conversion output (best effort).", "コメントは変換結果に保持されません(best effort)。")
            Dim hasCommentWarning = _currentFormat = JsonInputFormat.JsonC OrElse _currentFormat = JsonInputFormat.Json5
            Dim warnings = New List(Of String)(result.Warnings)
            If hasCommentWarning Then
                warnings.Add(commentWarning)
            End If

            Dim preview = New ConversionPreviewWindow(
                If(toXml, LocalText("XML Export Preview", "XML Exportプレビュー"), LocalText("YAML Export Preview", "YAML Exportプレビュー")),
                result.Output,
                warnings,
                LocalText("Save", "保存"),
                LocalText("Cancel", "キャンセル"),
                LocalText("Warnings", "警告")) With {.Owner = Me}

            If toXml Then
                ' FR-P2-601: options re-convert the preview in place; nothing is
                ' written until Save is confirmed.
                preview.AttachXmlOptions(
                    LocalText("Arrays:", "配列:"),
                    LocalText("<item> elements (default)", "<item>要素(既定)"),
                    LocalText("Repeat parent name", "親名を繰り返す"),
                    LocalText("null:", "null:"),
                    LocalText("Empty element (default)", "空要素(既定)"),
                    LocalText("xsi:nil=""true""", "xsi:nil=""true"""),
                    Function(options)
                        Dim reconverted = _xmlConversion.ConvertJsonToXml(standardJson, options)
                        Dim updatedWarnings = New List(Of String)(reconverted.Warnings)
                        If hasCommentWarning Then
                            updatedWarnings.Add(commentWarning)
                        End If

                        Return (reconverted.Output, DirectCast(updatedWarnings, IReadOnlyList(Of String)))
                    End Function)
            End If

            If preview.ShowDialog() <> True Then
                AddLog("Export cancelled at preview. No files were changed.")
                Return
            End If

            Dim saveResult = _textExport.Save(dialog.FileName, preview.CurrentOutput)
            AddLog($"Exported {Path.GetFileName(saveResult.Path)}.")
            If Not String.IsNullOrWhiteSpace(saveResult.BackupPath) Then
                AddLog($"Export backup created: {Path.GetFileName(saveResult.BackupPath)}")
            End If

            For Each warning In warnings
                AddConversionMessage(warning)
            Next

            AddConversionMessage($"{If(toXml, "XML", "YAML")} export completed: {saveResult.Path}")
            If warnings.Count > 0 Then
                MessageTabs.SelectedItem = ConversionTab
            End If
        Catch ex As Exception
            _lastException = ex
            MessageBox.Show(Me, ex.Message, LocalText("Conversion failed. The original file was not changed.", "変換に失敗しました。元ファイルは変更されていません。"), MessageBoxButton.OK, MessageBoxImage.Error)
            AddLog($"Conversion failed: {ex.Message}")
            AddConversionMessage($"Conversion failed: {ex.Message}")
        End Try
    End Sub

    Private Sub ImportXml_Click(sender As Object, e As RoutedEventArgs)
        ImportDocument(fromXml:=True)
    End Sub

    Private Sub ImportYaml_Click(sender As Object, e As RoutedEventArgs)
        ImportDocument(fromXml:=False)
    End Sub

    Private Sub ImportDocument(fromXml As Boolean)
        If Not ConfirmDiscardUnsavedChanges() Then
            Return
        End If

        Dim dialog = New OpenFileDialog With {
            .Filter = If(fromXml, "XML files (*.xml)|*.xml|All files (*.*)|*.*", "YAML files (*.yaml;*.yml)|*.yaml;*.yml|All files (*.*)|*.*"),
            .Title = If(fromXml, "Open XML as JSON", "Open YAML as JSON")
        }

        If dialog.ShowDialog(Me) <> True Then
            Return
        End If

        Try
            Dim sourceText = File.ReadAllText(dialog.FileName, Encoding.UTF8)
            Dim result = If(fromXml, _xmlConversion.ConvertXmlToJson(sourceText), _yamlConversion.ConvertYamlToJson(sourceText))

            ' The converted JSON opens as a new, unsaved document; the source file is never modified.
            SetDocument(result.Output, "", isDirty:=True)
            AddLog($"Opened {Path.GetFileName(dialog.FileName)} as JSON.")

            For Each warning In result.Warnings
                AddConversionMessage(warning)
            Next

            AddConversionMessage($"{If(fromXml, "XML", "YAML")} import completed: {dialog.FileName}")
        Catch ex As Exception
            _lastException = ex
            MessageBox.Show(Me, ex.Message, LocalText("Conversion failed. The source file was not changed.", "変換に失敗しました。元ファイルは変更されていません。"), MessageBoxButton.OK, MessageBoxImage.Error)
            AddLog($"Import conversion failed: {ex.Message}")
            AddConversionMessage($"Import conversion failed: {ex.Message}")
        End Try
    End Sub
End Class
