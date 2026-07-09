' SPDX-License-Identifier: MPL-2.0
Imports System.Windows
Imports System.Windows.Controls
Imports VisualJson.Core.Parsing

Namespace UI
    Public Class FormatSelectionWindow
        Inherits Window

        Private ReadOnly _formatCombo As ComboBox

        Public Sub New(titleText As String, okText As String, cancelText As String)
            Title = titleText
            Width = 360
            Height = 180
            MinWidth = 320
            MinHeight = 160
            WindowStartupLocation = WindowStartupLocation.CenterOwner

            Dim layout = New DockPanel With {.Margin = New Thickness(12)}
            Dim label = New TextBlock With {.Text = "Select input format", .Margin = New Thickness(0, 0, 0, 8)}
            DockPanel.SetDock(label, Dock.Top)
            layout.Children.Add(label)

            _formatCombo = New ComboBox With {.Margin = New Thickness(0, 0, 0, 12)}
            AddFormat("JSON", JsonInputFormat.StandardJson)
            AddFormat("JSONC", JsonInputFormat.JsonC)
            AddFormat("JSON5", JsonInputFormat.Json5)
            AddFormat("JSON Lines", JsonInputFormat.JsonLines)
            _formatCombo.SelectedIndex = 0
            DockPanel.SetDock(_formatCombo, Dock.Top)
            layout.Children.Add(_formatCombo)

            Dim buttons = New StackPanel With {.Orientation = Orientation.Horizontal, .HorizontalAlignment = HorizontalAlignment.Right}
            Dim okButton = New Button With {.Content = okText, .MinWidth = 84, .Margin = New Thickness(6, 0, 0, 0), .Padding = New Thickness(10, 4, 10, 4), .IsDefault = True}
            Dim cancelButton = New Button With {.Content = cancelText, .MinWidth = 84, .Margin = New Thickness(6, 0, 0, 0), .Padding = New Thickness(10, 4, 10, 4), .IsCancel = True}
            AddHandler okButton.Click, Sub()
                                           DialogResult = True
                                           Close()
                                       End Sub
            buttons.Children.Add(okButton)
            buttons.Children.Add(cancelButton)
            DockPanel.SetDock(buttons, Dock.Bottom)
            layout.Children.Add(buttons)

            Content = layout
        End Sub

        Public ReadOnly Property SelectedFormat As JsonInputFormat
            Get
                Dim item = TryCast(_formatCombo.SelectedItem, ComboBoxItem)
                If item Is Nothing Then
                    Return JsonInputFormat.StandardJson
                End If

                Return DirectCast(item.Tag, JsonInputFormat)
            End Get
        End Property

        Private Sub AddFormat(label As String, format As JsonInputFormat)
            _formatCombo.Items.Add(New ComboBoxItem With {.Content = label, .Tag = format})
        End Sub
    End Class
End Namespace
