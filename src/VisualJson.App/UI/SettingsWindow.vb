' SPDX-License-Identifier: MPL-2.0
Imports System.Windows
Imports System.Windows.Controls
Imports VisualJson.Core.Infrastructure

Namespace UI
    Public Class SettingsWindow
        Inherits Window

        Private ReadOnly _languageCombo As ComboBox
        Private ReadOnly _backupBox As CheckBox
        Private ReadOnly _externalSchemaBox As CheckBox
        Private ReadOnly _autoCloseBox As CheckBox
        Private ReadOnly _schemaPathsBox As TextBox
        Private ReadOnly _clearHistoryBox As CheckBox

        Public Sub New(settings As AppSettings, text As SettingsWindowText)
            Dim labels = If(text, New SettingsWindowText())
            Title = labels.Title
            Width = 480
            Height = 360
            MinWidth = 420
            MinHeight = 320
            WindowStartupLocation = WindowStartupLocation.CenterOwner

            Dim source = If(settings, AppSettings.CreateDefault())
            Dim layout = New Grid With {.Margin = New Thickness(12)}
            layout.RowDefinitions.Add(New RowDefinition With {.Height = GridLength.Auto})
            layout.RowDefinitions.Add(New RowDefinition With {.Height = GridLength.Auto})
            layout.RowDefinitions.Add(New RowDefinition With {.Height = GridLength.Auto})
            layout.RowDefinitions.Add(New RowDefinition With {.Height = GridLength.Auto})
            layout.RowDefinitions.Add(New RowDefinition With {.Height = New GridLength(1, GridUnitType.Star)})
            layout.RowDefinitions.Add(New RowDefinition With {.Height = GridLength.Auto})

            _languageCombo = New ComboBox With {.Margin = New Thickness(0, 0, 0, 8), .Width = 180, .HorizontalAlignment = HorizontalAlignment.Left}
            _languageCombo.Items.Add(New ComboBoxItem With {.Content = labels.English, .Tag = "en"})
            _languageCombo.Items.Add(New ComboBoxItem With {.Content = labels.Japanese, .Tag = "ja"})
            _languageCombo.SelectedIndex = If(String.Equals(source.Language, "ja", StringComparison.OrdinalIgnoreCase), 1, 0)
            AddLabeled(layout, 0, labels.Language, _languageCombo)

            _backupBox = New CheckBox With {.Content = labels.BackupBeforeSave, .IsChecked = source.BackupBeforeSave, .Margin = New Thickness(0, 2, 0, 6)}
            Grid.SetRow(_backupBox, 1)
            layout.Children.Add(_backupBox)

            _externalSchemaBox = New CheckBox With {.Content = labels.AllowExternalSchema, .IsChecked = source.AllowExternalSchema, .Margin = New Thickness(0, 2, 0, 6)}
            Grid.SetRow(_externalSchemaBox, 2)
            layout.Children.Add(_externalSchemaBox)

            _autoCloseBox = New CheckBox With {.Content = labels.AutoCloseBrackets, .IsChecked = source.AutoCloseBrackets, .Margin = New Thickness(0, 2, 0, 6)}
            Grid.SetRow(_autoCloseBox, 3)
            layout.Children.Add(_autoCloseBox)

            _schemaPathsBox = New TextBox With {
                .Text = String.Join(Environment.NewLine, If(source.SchemaSearchPaths, New List(Of String)())),
                .AcceptsReturn = True,
                .VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            }
            AddLabeled(layout, 4, labels.SchemaSearchPaths, _schemaPathsBox)

            Dim bottom = New DockPanel With {.Margin = New Thickness(0, 12, 0, 0)}
            _clearHistoryBox = New CheckBox With {.Content = labels.ClearRecentHistory, .VerticalAlignment = VerticalAlignment.Center}
            DockPanel.SetDock(_clearHistoryBox, Dock.Left)
            bottom.Children.Add(_clearHistoryBox)

            Dim buttons = New StackPanel With {.Orientation = Orientation.Horizontal, .HorizontalAlignment = HorizontalAlignment.Right}
            Dim okButton = New Button With {.Content = labels.Ok, .MinWidth = 86, .Margin = New Thickness(6, 0, 0, 0), .Padding = New Thickness(10, 4, 10, 4), .IsDefault = True}
            Dim cancelButton = New Button With {.Content = labels.Cancel, .MinWidth = 86, .Margin = New Thickness(6, 0, 0, 0), .Padding = New Thickness(10, 4, 10, 4), .IsCancel = True}
            AddHandler okButton.Click, Sub()
                                           DialogResult = True
                                           Close()
                                       End Sub
            buttons.Children.Add(okButton)
            buttons.Children.Add(cancelButton)
            bottom.Children.Add(buttons)
            Grid.SetRow(bottom, 5)
            layout.Children.Add(bottom)

            Content = layout
            LocalizationSnapshot = String.Join("|", {
                labels.Title,
                labels.Language,
                labels.English,
                labels.Japanese,
                labels.BackupBeforeSave,
                labels.AllowExternalSchema,
                labels.AutoCloseBrackets,
                labels.SchemaSearchPaths,
                labels.ClearRecentHistory,
                labels.Ok,
                labels.Cancel
            })
        End Sub

        Public Sub ApplyTo(settings As AppSettings)
            If settings Is Nothing Then
                Return
            End If

            Dim item = TryCast(_languageCombo.SelectedItem, ComboBoxItem)
            settings.Language = If(TryCast(item?.Tag, String), "en")
            settings.BackupBeforeSave = _backupBox.IsChecked.GetValueOrDefault(True)
            settings.AllowExternalSchema = _externalSchemaBox.IsChecked.GetValueOrDefault(False)
            settings.AutoCloseBrackets = _autoCloseBox.IsChecked.GetValueOrDefault(True)
            settings.SchemaSearchPaths = _schemaPathsBox.Text.
                Split({ControlChars.Cr, ControlChars.Lf}, StringSplitOptions.RemoveEmptyEntries).
                Select(Function(path) path.Trim()).
                Where(Function(path) path.Length > 0).
                Distinct(StringComparer.OrdinalIgnoreCase).
                ToList()
        End Sub

        Public ReadOnly Property ClearHistoryRequested As Boolean
            Get
                Return _clearHistoryBox.IsChecked.GetValueOrDefault(False)
            End Get
        End Property

        Public ReadOnly Property LocalizationSnapshot As String

        Private Shared Sub AddLabeled(layout As Grid, row As Integer, labelText As String, control As FrameworkElement)
            Dim panel = New DockPanel With {.Margin = New Thickness(0, 0, 0, 8)}
            Dim label = New TextBlock With {.Text = labelText, .FontWeight = FontWeights.SemiBold, .Margin = New Thickness(0, 0, 0, 4)}
            DockPanel.SetDock(label, Dock.Top)
            panel.Children.Add(label)
            panel.Children.Add(control)
            Grid.SetRow(panel, row)
            layout.Children.Add(panel)
        End Sub
    End Class
End Namespace
