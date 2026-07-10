' SPDX-License-Identifier: MPL-2.0
Imports System.Collections.ObjectModel
Imports System.Windows.Threading
Imports VisualJson.App.ViewModels
Imports VisualJson.Core.Diagnostics
Imports VisualJson.Core.Models
Imports VisualJson.Core.Services

' Grid/tree operations: selection, editing, filtering, drag & drop, undo/redo, and text sync (FR-13-103).
Partial Class MainWindow

#Region "Event Handlers"

    Private Sub ApplyGridToText_Click(sender As Object, e As RoutedEventArgs)
        ApplyGridToText()
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

#End Region

#Region "Private Helpers"

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

#End Region

End Class
