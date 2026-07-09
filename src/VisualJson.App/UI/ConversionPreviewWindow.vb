' SPDX-License-Identifier: MPL-2.0
Imports System.Windows
Imports System.Windows.Controls
Imports VisualJson.Core.Conversion

Namespace UI
    ''' Conversion preview shown before an export is written (FR-M3-005).
    ''' The export only happens when the user confirms; Cancel leaves every file untouched.
    ''' For XML exports the preview hosts the FR-P2-601 options and re-converts on
    ''' change; options are not persisted (every export starts from the defaults).
    Public Class ConversionPreviewWindow
        Inherits Window

        Private ReadOnly _previewBox As TextBox
        Private ReadOnly _warningsPanel As StackPanel
        Private ReadOnly _warningsLabel As String
        Private ReadOnly _optionsPanel As StackPanel
        Private _regenerate As Func(Of XmlConversionOptions, (Output As String, Warnings As IReadOnlyList(Of String)))
        Private _arrayModeCombo As ComboBox
        Private _nullModeCombo As ComboBox

        Public Sub New(title As String, content As String, warnings As IReadOnlyList(Of String), saveLabel As String, cancelLabel As String, warningsLabel As String)
            Me.Title = title
            Width = 860
            Height = 640
            MinWidth = 520
            MinHeight = 360
            WindowStartupLocation = WindowStartupLocation.CenterOwner
            _warningsLabel = warningsLabel
            CurrentOutput = If(content, "")

            Dim layout = New Grid() With {.Margin = New Thickness(10)}
            layout.RowDefinitions.Add(New RowDefinition With {.Height = GridLength.Auto})
            layout.RowDefinitions.Add(New RowDefinition With {.Height = New GridLength(1, GridUnitType.Star)})
            layout.RowDefinitions.Add(New RowDefinition With {.Height = GridLength.Auto})
            layout.RowDefinitions.Add(New RowDefinition With {.Height = GridLength.Auto})

            _optionsPanel = New StackPanel With {.Orientation = Orientation.Horizontal, .Margin = New Thickness(0, 0, 0, 8), .Visibility = Visibility.Collapsed}
            Grid.SetRow(_optionsPanel, 0)
            layout.Children.Add(_optionsPanel)

            _previewBox = New TextBox With {
                .Text = If(content, ""),
                .IsReadOnly = True,
                .AcceptsReturn = True,
                .FontFamily = New Media.FontFamily("Consolas"),
                .FontSize = 13,
                .VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                .HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
            }
            Grid.SetRow(_previewBox, 1)
            layout.Children.Add(_previewBox)

            _warningsPanel = New StackPanel With {.Margin = New Thickness(0, 8, 0, 0)}
            Grid.SetRow(_warningsPanel, 2)
            layout.Children.Add(_warningsPanel)
            UpdateWarnings(warnings)

            Dim buttons = New StackPanel With {
                .Orientation = Orientation.Horizontal,
                .HorizontalAlignment = HorizontalAlignment.Right,
                .Margin = New Thickness(0, 10, 0, 0)
            }

            Dim saveButton = New Button With {.Content = saveLabel, .MinWidth = 96, .Margin = New Thickness(6, 0, 0, 0), .Padding = New Thickness(10, 4, 10, 4), .IsDefault = True}
            Dim cancelButton = New Button With {.Content = cancelLabel, .MinWidth = 96, .Margin = New Thickness(6, 0, 0, 0), .Padding = New Thickness(10, 4, 10, 4), .IsCancel = True}

            AddHandler saveButton.Click, Sub()
                                             DialogResult = True
                                             Close()
                                         End Sub

            buttons.Children.Add(saveButton)
            buttons.Children.Add(cancelButton)
            Grid.SetRow(buttons, 3)
            layout.Children.Add(buttons)

            Me.Content = layout
        End Sub

        ''' The output the Save button will write (updated on option changes).
        Public Property CurrentOutput As String

        Public ReadOnly Property SelectedXmlOptions As XmlConversionOptions
            Get
                Dim options = XmlConversionOptions.CreateDefault()
                If _arrayModeCombo IsNot Nothing AndAlso _arrayModeCombo.SelectedIndex = 1 Then
                    options.ArrayMode = XmlArrayMode.RepeatParentName
                End If

                If _nullModeCombo IsNot Nothing AndAlso _nullModeCombo.SelectedIndex = 1 Then
                    options.NullMode = XmlNullMode.XsiNil
                End If

                Return options
            End Get
        End Property

        ''' FR-P2-601: shows the array/null option combos and re-converts on change.
        Public Sub AttachXmlOptions(arrayModeLabel As String,
                                    itemElementsLabel As String,
                                    repeatParentLabel As String,
                                    nullModeLabel As String,
                                    emptyElementLabel As String,
                                    xsiNilLabel As String,
                                    regenerate As Func(Of XmlConversionOptions, (Output As String, Warnings As IReadOnlyList(Of String))))
            _regenerate = regenerate

            _optionsPanel.Children.Clear()
            _optionsPanel.Children.Add(New TextBlock With {.Text = arrayModeLabel, .VerticalAlignment = VerticalAlignment.Center, .Margin = New Thickness(0, 0, 6, 0)})
            _arrayModeCombo = New ComboBox With {.MinWidth = 170}
            _arrayModeCombo.Items.Add(New ComboBoxItem With {.Content = itemElementsLabel})
            _arrayModeCombo.Items.Add(New ComboBoxItem With {.Content = repeatParentLabel})
            _arrayModeCombo.SelectedIndex = 0
            _optionsPanel.Children.Add(_arrayModeCombo)

            _optionsPanel.Children.Add(New TextBlock With {.Text = nullModeLabel, .VerticalAlignment = VerticalAlignment.Center, .Margin = New Thickness(14, 0, 6, 0)})
            _nullModeCombo = New ComboBox With {.MinWidth = 170}
            _nullModeCombo.Items.Add(New ComboBoxItem With {.Content = emptyElementLabel})
            _nullModeCombo.Items.Add(New ComboBoxItem With {.Content = xsiNilLabel})
            _nullModeCombo.SelectedIndex = 0
            _optionsPanel.Children.Add(_nullModeCombo)

            AddHandler _arrayModeCombo.SelectionChanged, AddressOf Options_Changed
            AddHandler _nullModeCombo.SelectionChanged, AddressOf Options_Changed
            _optionsPanel.Visibility = Visibility.Visible
        End Sub

        Public Sub SelectXmlOptionsForAutomation(repeatParentName As Boolean, xsiNil As Boolean)
            If _arrayModeCombo Is Nothing OrElse _nullModeCombo Is Nothing Then
                Return
            End If

            _arrayModeCombo.SelectedIndex = If(repeatParentName, 1, 0)
            _nullModeCombo.SelectedIndex = If(xsiNil, 1, 0)
        End Sub

        Private Sub Options_Changed(sender As Object, e As SelectionChangedEventArgs)
            If _regenerate Is Nothing Then
                Return
            End If

            Dim result = _regenerate(SelectedXmlOptions)
            CurrentOutput = result.Output
            _previewBox.Text = result.Output
            UpdateWarnings(result.Warnings)
        End Sub

        Private Sub UpdateWarnings(warnings As IReadOnlyList(Of String))
            _warningsPanel.Children.Clear()
            If warnings Is Nothing OrElse warnings.Count = 0 Then
                Return
            End If

            _warningsPanel.Children.Add(New TextBlock With {
                .Text = _warningsLabel,
                .FontWeight = FontWeights.SemiBold
            })

            Dim warningList = New ListBox With {.MaxHeight = 110}
            For Each warning In warnings
                warningList.Items.Add(warning)
            Next
            _warningsPanel.Children.Add(warningList)
        End Sub
    End Class
End Namespace
