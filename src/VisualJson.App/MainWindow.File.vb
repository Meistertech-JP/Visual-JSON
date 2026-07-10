' SPDX-License-Identifier: MPL-2.0
Imports System.Collections.ObjectModel
Imports System.IO
Imports System.Text
Imports Microsoft.Win32
Imports VisualJson.App.UI
Imports VisualJson.App.ViewModels
Imports VisualJson.Core.Infrastructure
Imports VisualJson.Core.Models
Imports VisualJson.Core.Parsing

' File lifecycle: New/Open/Save/SaveAs, drag & drop, recent files, unsaved-changes confirmation (FR-13-103).
Partial Class MainWindow

#Region "Event Handlers"

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

    Private Sub Save_Click(sender As Object, e As RoutedEventArgs)
        SaveCurrent(saveAs:=False)
    End Sub

    Private Sub SaveAs_Click(sender As Object, e As RoutedEventArgs)
        SaveCurrent(saveAs:=True)
    End Sub

    Private Sub Exit_Click(sender As Object, e As RoutedEventArgs)
        Close()
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

#End Region

#Region "Private Helpers"

    Public Sub OpenPathForStartup(path As String)
        If String.IsNullOrWhiteSpace(path) Then
            Return
        End If

        OpenDocumentPath(path, addToRecent:=True)
    End Sub

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
        ' Boundary sync (D-V13-2-A): the view model receives the body only at document
        ' boundaries; setting Text marks the document dirty, so assign IsDirty after it.
        Document.Text = If(text, "")
        Document.IsDirty = isDirty
        Document.RootNode = Nothing
        _gridRootView = Nothing
        Document.GridState = Nothing
        _filterRestoreState = Nothing
        _gridIsCurrent = False
        Document.FormatKind = format
        Document.Encoding = If(encodingInfo, DetectedTextEncoding.CreateDefault())
        _gridDisabled = Not String.IsNullOrWhiteSpace(filePath) AndAlso File.Exists(filePath) AndAlso New FileInfo(filePath).Length >= 50L * 1024L * 1024L
        _gridUndo.Clear()
        GridFilterBox.Text = ""
        JsonTree.ItemsSource = New ObservableCollection(Of GridNodeViewModel)()
        JsonTree.IsEnabled = Not _gridDisabled
        ' Schema validation results belong to the previous document; the loaded schema is kept.
        _viewModel.Messages.SchemaDiagnostics.Clear()

        FileFormatStatusText.Text = GetFormatLabel(Document.FormatKind)
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
            Dim formatted = _formatter.Format(CurrentText(), Document.FormatKind)
            Dim result As FileSaveResult

            If Document.FormatKind = JsonInputFormat.JsonLines Then
                ' FR-P2-602: JSONL documents are saved one compact JSON per line
                ' while the editor keeps showing the array-style standard JSON.
                Dim parsed = _parser.Parse(formatted)
                Dim lineResult = _jsonLines.Serialize(parsed.Root, If(Document.Encoding.NewLine = NewLineKind.CrLf, vbCrLf, vbLf))
                result = _saveService.SaveRaw(Document.CurrentFilePath, lineResult.Text, Document.Encoding, _settings.BackupBeforeSave)
                For Each warning In lineResult.Warnings
                    AddConversionMessage(warning)
                Next

                If lineResult.Warnings.Count > 0 Then
                    MessageTabs.SelectedItem = ConversionTab
                End If
            Else
                result = _saveService.Save(Document.CurrentFilePath, formatted, Document.Encoding, _settings.BackupBeforeSave)
            End If

            Dim savedText = EncodingDetectionService.NormalizeNewLines(formatted, Document.Encoding.NewLine)
            SetEditorText(savedText)
            Document.Text = savedText
            Document.IsDirty = False
            _recentFiles.Add(_settings, Document.CurrentFilePath)
            SaveSettings()
            RefreshRecentFilesMenu()
            UpdateChrome()
            ScheduleValidation()

            AddLog($"Saved {Path.GetFileName(result.Path)}.")
            _fileLog.Write("Save", $"{Path.GetFileName(result.Path)} encoding={Document.Encoding.Name} newline={Document.Encoding.NewLineName}")
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

#End Region

End Class
