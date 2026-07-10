' SPDX-License-Identifier: MPL-2.0
Imports System.IO
Imports System.Reflection
Imports System.Threading.Tasks
Imports VisualJson.Core.Diagnostics
Imports VisualJson.Core.Infrastructure
Imports VisualJson.Core.Services

' Diagnostics, message panes, validation scheduling, recovery, and status text (FR-13-103).
Partial Class MainWindow

#Region "Event Handlers"

    Private Sub ValidateSync_Click(sender As Object, e As RoutedEventArgs)
        ValidateCurrentText(updateGrid:=True)
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

    Private Sub RecoverLatest_Click(sender As Object, e As RoutedEventArgs)
        RecoverLatestCandidate()
    End Sub

    Private Sub CopyDiagnostics_Click(sender As Object, e As RoutedEventArgs)
        Dim version = Assembly.GetExecutingAssembly().GetName().Version?.ToString()
        Dim report = _diagnosticsReport.CreateReport(version, Document.CurrentFilePath, CurrentText().Length, Document.Diagnostics, _lastException, _treeStats.CountNodes(Document.RootNode), "WPF", _language, GetFormatLabel(_currentFormat), _currentEncoding.Name, _currentEncoding.NewLineName)
        Clipboard.SetText(report)
        AddLog("Diagnostics copied without JSON body.")
    End Sub

#End Region

#Region "Private Helpers"

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

    Private Sub AddConversionMessage(message As String)
        ConversionList.Items.Insert(0, $"{DateTime.Now:HH:mm:ss} {message}")
    End Sub

#End Region

End Class
