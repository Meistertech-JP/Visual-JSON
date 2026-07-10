' SPDX-License-Identifier: MPL-2.0
Imports System.Threading.Tasks
Imports System.Windows.Threading
Imports VisualJson.App.UI
Imports VisualJson.Core.Models
Imports VisualJson.Core.Services

' Text editor: search/replace, folding, caret sync, formatting, and key completion (FR-13-103).
Partial Class MainWindow

#Region "Event Handlers"

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
            Dim formatted = _formatter.Format(CurrentText(), Document.FormatKind)
            SetEditorText(formatted)
            Dim parsed = _parser.Parse(formatted, Document.FormatKind)
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

    Private Sub FindNext_Click(sender As Object, e As RoutedEventArgs)
        FindText(forward:=True)
    End Sub

    Private Sub FindPrevious_Click(sender As Object, e As RoutedEventArgs)
        FindText(forward:=False)
    End Sub

#End Region

#Region "Private Helpers"

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
        _viewModel.StatusText = "Not checked"
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

#End Region

End Class
