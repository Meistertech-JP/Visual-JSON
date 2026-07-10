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
    Private _gridDisabled As Boolean
    Private _dragStartPoint As Point
    Private _dragNode As JsonTreeNode
    Private _language As String = "en"
    Private _settings As AppSettings = AppSettings.CreateDefault()
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
        InitializeCommands()
        _editor = New TextEditorAdapter(JsonEditor)
        AddHandler JsonEditor.TextChanged, AddressOf JsonEditor_TextChanged
        AddHandler JsonEditor.TextArea.Caret.PositionChanged, AddressOf EditorCaret_PositionChanged
        AddHandler AllowExternalSchemaMenuItem.Checked, AddressOf SettingsControl_Changed
        AddHandler AllowExternalSchemaMenuItem.Unchecked, AddressOf SettingsControl_Changed
        DiagnosticList.ItemsSource = _viewModel.Messages.SyntaxDiagnostics
        LogList.ItemsSource = _viewModel.Messages.LogEntries
        SchemaList.ItemsSource = _viewModel.Messages.SchemaDiagnostics
        ConversionList.ItemsSource = _viewModel.Messages.ConversionDiagnostics

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

End Class
