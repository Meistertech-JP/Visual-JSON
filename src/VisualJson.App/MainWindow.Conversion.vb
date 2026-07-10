' SPDX-License-Identifier: MPL-2.0
Imports System.IO
Imports System.Text
Imports Microsoft.Win32
Imports VisualJson.App.UI
Imports VisualJson.Core.Parsing

' MVP-3: XML/YAML conversion (FR-13-103: preview/export/import moved out of MainWindow.xaml.vb).
Partial Class MainWindow

#Region "Event Handlers"

    Private Sub ExportXml_Click(sender As Object, e As RoutedEventArgs)
        ExportDocument(toXml:=True)
    End Sub

    Private Sub ExportYaml_Click(sender As Object, e As RoutedEventArgs)
        ExportDocument(toXml:=False)
    End Sub

    Private Sub ImportXml_Click(sender As Object, e As RoutedEventArgs)
        ImportDocument(fromXml:=True)
    End Sub

    Private Sub ImportYaml_Click(sender As Object, e As RoutedEventArgs)
        ImportDocument(fromXml:=False)
    End Sub

#End Region

#Region "Private Helpers"

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

#End Region

End Class
