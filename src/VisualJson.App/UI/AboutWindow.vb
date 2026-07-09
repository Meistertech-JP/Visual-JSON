' SPDX-License-Identifier: MPL-2.0
Imports System.Reflection
Imports System.Windows
Imports System.Windows.Controls

Namespace UI
    Public Class AboutWindow
        Inherits Window

        Public Sub New(titleText As String, closeText As String)
            Title = titleText
            Width = 420
            Height = 300
            MinWidth = 360
            MinHeight = 260
            WindowStartupLocation = WindowStartupLocation.CenterOwner

            Dim appAssembly = Assembly.GetExecutingAssembly()
            Dim version = appAssembly.GetName().Version?.ToString()
            Dim buildTime = IO.File.GetLastWriteTime(appAssembly.Location).ToString("yyyy-MM-dd HH:mm:ss")
            Dim body = String.Join(Environment.NewLine, {
                "Visual JSON",
                $"Version: {version}",
                $"Build: {buildTime}",
                "",
                "Licenses: Visual JSON source MPL-2.0; Visual JSON name/branding reserved; AvalonEdit MIT; .NET runtime notices included in release package."
            })
            BodyText = body

            Dim layout = New DockPanel With {.Margin = New Thickness(16)}
            Dim text = New TextBlock With {
                .Text = body,
                .TextWrapping = TextWrapping.Wrap
            }
            layout.Children.Add(text)

            Dim closeButton = New Button With {
                .Content = closeText,
                .MinWidth = 88,
                .Padding = New Thickness(10, 4, 10, 4),
                .Margin = New Thickness(0, 12, 0, 0),
                .HorizontalAlignment = HorizontalAlignment.Right,
                .IsDefault = True
            }
            AddHandler closeButton.Click, Sub()
                                             DialogResult = True
                                             Close()
                                         End Sub
            DockPanel.SetDock(closeButton, Dock.Bottom)
            layout.Children.Add(closeButton)

            Content = layout
        End Sub

        Public ReadOnly Property BodyText As String
    End Class
End Namespace
