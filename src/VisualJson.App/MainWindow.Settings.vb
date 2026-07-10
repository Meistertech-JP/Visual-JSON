' SPDX-License-Identifier: MPL-2.0
Imports VisualJson.App.UI
Imports VisualJson.Core.Infrastructure

' Settings, About, and window placement persistence (FR-13-103).
Partial Class MainWindow

#Region "Event Handlers"

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

    Private Sub About_Click(sender As Object, e As RoutedEventArgs)
        Dim dialog = New AboutWindow(LocalText("About Visual JSON", "Visual JSONについて"), LocalText("Close", "閉じる")) With {.Owner = Me}
        dialog.ShowDialog()
    End Sub

    Private Sub SettingsControl_Changed(sender As Object, e As RoutedEventArgs)
        If _settings Is Nothing OrElse _suppressSettingsSave Then
            Return
        End If

        _settings.AllowExternalSchema = AllowExternalSchemaMenuItem.IsChecked
        SaveSettings()
    End Sub

#End Region

#Region "Private Helpers"

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

#End Region

End Class
