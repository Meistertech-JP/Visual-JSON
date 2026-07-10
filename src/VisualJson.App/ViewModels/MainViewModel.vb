' SPDX-License-Identifier: MPL-2.0
Imports VisualJson.Core.Services

Namespace ViewModels
    ''' Application-wide state (FR-13-201). Holds no WPF types (FR-13-205);
    ''' the view binds to these properties and pushes user actions back through
    ''' the MainWindow partials.
    Public Class MainViewModel
        Inherits ObservableObject

        Private _currentMode As String = "Text"
        Private _isBusy As Boolean
        Private _statusText As String = "Not checked"
        Private _selectedJsonPointer As String = ""

        Public Sub New()
            ActiveDocument = New DocumentViewModel()
            Messages = New MessagePaneViewModel()
        End Sub

        Public ReadOnly Property ActiveDocument As DocumentViewModel

        Public ReadOnly Property Messages As MessagePaneViewModel

        ''' "Text", "Grid", or "Table" (the current editor surface).
        Public Property CurrentMode As String
            Get
                Return _currentMode
            End Get
            Set(value As String)
                SetProperty(_currentMode, If(value, ""))
            End Set
        End Property

        Public Property IsBusy As Boolean
            Get
                Return _isBusy
            End Get
            Set(value As Boolean)
                SetProperty(_isBusy, value)
            End Set
        End Property

        ''' Main status-bar wording (parse/validation state of the active document).
        Public Property StatusText As String
            Get
                Return _statusText
            End Get
            Set(value As String)
                SetProperty(_statusText, If(value, ""))
            End Set
        End Property

        ''' RFC 6901 pointer of the current selection/caret position ("" = root).
        Public Property SelectedJsonPointer As String
            Get
                Return _selectedJsonPointer
            End Get
            Set(value As String)
                If SetProperty(_selectedJsonPointer, If(value, "")) Then
                    OnPropertyChanged(NameOf(PointerDisplayText))
                End If
            End Set
        End Property

        Public ReadOnly Property PointerDisplayText As String
            Get
                Return $"Pointer: {DocumentStateService.ToPointerDisplay(SelectedJsonPointer)}"
            End Get
        End Property
    End Class
End Namespace
