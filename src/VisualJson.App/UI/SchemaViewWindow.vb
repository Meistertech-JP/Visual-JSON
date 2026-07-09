' SPDX-License-Identifier: MPL-2.0
Imports System.Windows
Imports System.Windows.Controls

Namespace UI
    ''' Read-only view of the loaded schema, scrolled to a definition location (FR-M2-004).
    Public Class SchemaViewWindow
        Inherits Window

        Public Sub New(title As String, schemaText As String, selectionStart As Integer?, headerText As String)
            Me.Title = title
            Width = 780
            Height = 600
            MinWidth = 480
            MinHeight = 320
            WindowStartupLocation = WindowStartupLocation.CenterOwner

            Dim layout = New DockPanel With {.Margin = New Thickness(10)}

            Dim header = New TextBlock With {
                .Text = If(headerText, ""),
                .Margin = New Thickness(0, 0, 0, 8),
                .FontWeight = FontWeights.SemiBold,
                .TextTrimming = TextTrimming.CharacterEllipsis
            }
            DockPanel.SetDock(header, Dock.Top)
            layout.Children.Add(header)

            Dim schemaBox = New TextBox With {
                .Text = If(schemaText, ""),
                .IsReadOnly = True,
                .AcceptsReturn = True,
                .FontFamily = New Media.FontFamily("Consolas"),
                .FontSize = 13,
                .VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                .HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
            }
            layout.Children.Add(schemaBox)
            Content = layout

            AddHandler Loaded, Sub()
                                   If selectionStart.HasValue AndAlso selectionStart.Value >= 0 AndAlso selectionStart.Value < schemaBox.Text.Length Then
                                       schemaBox.Focus()
                                       schemaBox.Select(selectionStart.Value, 1)
                                       Dim lineIndex = schemaBox.GetLineIndexFromCharacterIndex(selectionStart.Value)
                                       schemaBox.ScrollToLine(Math.Max(0, lineIndex))
                                   End If
                               End Sub
        End Sub
    End Class
End Namespace
