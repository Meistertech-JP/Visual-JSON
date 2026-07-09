' SPDX-License-Identifier: MPL-2.0
Imports System.Windows
Imports System.Windows.Controls

Namespace UI
    ''' Minimal single-field prompt used by the table view "+ Column" action.
    Public Class TextPromptWindow
        Inherits Window

        Private ReadOnly _inputBox As TextBox

        Public Sub New(titleText As String, promptText As String, okText As String, cancelText As String)
            Title = titleText
            Width = 380
            Height = 170
            MinWidth = 320
            MinHeight = 150
            WindowStartupLocation = WindowStartupLocation.CenterOwner
            ResizeMode = ResizeMode.NoResize

            Dim layout = New DockPanel With {.Margin = New Thickness(12)}
            Dim label = New TextBlock With {.Text = promptText, .Margin = New Thickness(0, 0, 0, 8), .TextWrapping = TextWrapping.Wrap}
            DockPanel.SetDock(label, Dock.Top)
            layout.Children.Add(label)

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

            _inputBox = New TextBox With {.Margin = New Thickness(0, 0, 0, 12), .Padding = New Thickness(4, 2, 4, 2)}
            DockPanel.SetDock(_inputBox, Dock.Top)
            layout.Children.Add(_inputBox)

            Content = layout
            AddHandler Loaded, Sub() _inputBox.Focus()
        End Sub

        Public ReadOnly Property InputText As String
            Get
                Return If(_inputBox.Text, "")
            End Get
        End Property
    End Class
End Namespace
