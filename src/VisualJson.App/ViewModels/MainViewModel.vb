' SPDX-License-Identifier: MPL-2.0
Imports System.ComponentModel
Imports VisualJson.Core.Services

Namespace ViewModels
    ''' Application-wide state (FR-13-201). Holds no WPF types (FR-13-205);
    ''' the view binds to these properties and pushes user actions back through
    ''' the MainWindow partials. Validation state and the selected pointer are
    ''' stored on the active document (FR-13-202); this class exposes them for
    ''' the status bar and forwards their change notifications.
    Public Class MainViewModel
        Inherits ObservableObject

        Private _currentMode As String = "Text"
        Private _isBusy As Boolean

        Public Sub New()
            ActiveDocument = New DocumentViewModel()
            Messages = New MessagePaneViewModel()
            AddHandler ActiveDocument.PropertyChanged, AddressOf OnDocumentPropertyChanged
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

        ''' Main status-bar wording — delegates to the active document's validation state.
        Public Property StatusText As String
            Get
                Return ActiveDocument.ValidationState
            End Get
            Set(value As String)
                ActiveDocument.ValidationState = value
            End Set
        End Property

        ''' RFC 6901 pointer of the current selection ("" = root) — delegates to the active document.
        Public Property SelectedJsonPointer As String
            Get
                Return ActiveDocument.SelectedPointer
            End Get
            Set(value As String)
                ActiveDocument.SelectedPointer = value
            End Set
        End Property

        Public ReadOnly Property PointerDisplayText As String
            Get
                Return $"Pointer: {DocumentStateService.ToPointerDisplay(ActiveDocument.SelectedPointer)}"
            End Get
        End Property

        Private Sub OnDocumentPropertyChanged(sender As Object, e As PropertyChangedEventArgs)
            If String.Equals(e.PropertyName, NameOf(DocumentViewModel.ValidationState), StringComparison.Ordinal) Then
                OnPropertyChanged(NameOf(StatusText))
            ElseIf String.Equals(e.PropertyName, NameOf(DocumentViewModel.SelectedPointer), StringComparison.Ordinal) Then
                OnPropertyChanged(NameOf(SelectedJsonPointer))
                OnPropertyChanged(NameOf(PointerDisplayText))
            End If
        End Sub
    End Class
End Namespace
