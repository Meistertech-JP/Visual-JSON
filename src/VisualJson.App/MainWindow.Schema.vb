' SPDX-License-Identifier: MPL-2.0
Imports System.IO
Imports Microsoft.Win32
Imports VisualJson.App.UI
Imports VisualJson.Core.Diagnostics
Imports VisualJson.Core.Models

' MVP-2: Schema validation (FR-13-103: load/validate/diagnostics-jump moved out of MainWindow.xaml.vb).
Partial Class MainWindow

#Region "Event Handlers"

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
        _viewModel.Messages.SchemaDiagnostics.Clear()
        UpdateSchemaStatus()
        AddLog("Cleared schema.")
    End Sub

    Private Sub ValidateSchema_Click(sender As Object, e As RoutedEventArgs)
        RunSchemaValidation(showTab:=True)
    End Sub

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

#End Region

#Region "Private Helpers"

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
            Dim normalized = _preprocessor.Normalize(CurrentText(), Document.FormatKind)
            Dim documentRoot = _parser.Parse(CurrentText(), Document.FormatKind).Root
            Dim diagnostics = _schemaValidation.Validate(normalized, _schemaText, _schemaSource, documentRoot)

            _viewModel.Messages.SchemaDiagnostics.Clear()
            For Each diagnostic In diagnostics
                _viewModel.Messages.SchemaDiagnostics.Add(diagnostic)
            Next

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
            Dim normalized = _preprocessor.Normalize(CurrentText(), Document.FormatKind)
            Using document = Text.Json.JsonDocument.Parse(normalized)
                If document.RootElement.ValueKind = Text.Json.JsonValueKind.Object Then
                    Dim schemaProperty As Text.Json.JsonElement = Nothing
                    If document.RootElement.TryGetProperty("$schema", schemaProperty) AndAlso schemaProperty.ValueKind = Text.Json.JsonValueKind.String Then
                        Return schemaProperty.GetString()
                    End If
                End If
            End Using
        Catch ex As Exception
            ' LogOnly: $schema probing is best effort; the user can still load a schema manually.
            _lastException = ex
            _fileLog.WriteException("SchemaUrlProbeFailed", ex)
        End Try

        Return Nothing
    End Function

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
            ' LogOnly: falling back to an unhighlighted schema view is acceptable; record why.
            _lastException = ex
            _fileLog.WriteException("SchemaDefinitionLookupFailed", ex)
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

#End Region

End Class
