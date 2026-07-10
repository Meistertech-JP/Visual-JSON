' SPDX-License-Identifier: MPL-2.0
Imports VisualJson.Core.Infrastructure
Imports VisualJson.Core.Models
Imports VisualJson.Core.Parsing
Imports VisualJson.Core.Services

Namespace ViewModels
    ''' Per-document state (FR-13-202): path, body text at document boundaries,
    ''' dirty flag, input format, encoding/newline, validation state, selected
    ''' pointer, grid view state, and the parsed grid root.
    ''' Diagnostics/log collections live on MessagePaneViewModel (FR-13-203).
    Public Class DocumentViewModel
        Inherits ObservableObject

        Private _currentFilePath As String = ""
        Private _isDirty As Boolean
        Private _text As String = ""
        Private _formatKind As JsonInputFormat = JsonInputFormat.StandardJson
        Private _encoding As DetectedTextEncoding = DetectedTextEncoding.CreateDefault()
        Private _rootNode As JsonTreeNode
        Private _nodeCount As Integer
        Private _validationState As String = "Not checked"
        Private _selectedPointer As String = ""
        Private _gridState As GridViewState

        Public Property CurrentFilePath As String
            Get
                Return _currentFilePath
            End Get
            Set(value As String)
                If SetProperty(_currentFilePath, If(value, "")) Then
                    OnPropertyChanged(NameOf(DisplayFilePath))
                    OnPropertyChanged(NameOf(DisplayFileName))
                End If
            End Set
        End Property

        Public Property IsDirty As Boolean
            Get
                Return _isDirty
            End Get
            Set(value As Boolean)
                If SetProperty(_isDirty, value) Then
                    OnPropertyChanged(NameOf(DirtyStatusDisplay))
                End If
            End Set
        End Property

        ''' Document body synchronized at boundaries only (new/open/save/import) —
        ''' AvalonEdit stays the live text authority; per-keystroke sync would copy
        ''' multi-megabyte strings and regress NFR-13-PERF-001.
        Public Property Text As String
            Get
                Return _text
            End Get
            Set(value As String)
                If SetProperty(_text, If(value, "")) Then
                    IsDirty = True
                End If
            End Set
        End Property

        Public Property FormatKind As JsonInputFormat
            Get
                Return _formatKind
            End Get
            Set(value As JsonInputFormat)
                SetProperty(_formatKind, value)
            End Set
        End Property

        Public Property Encoding As DetectedTextEncoding
            Get
                Return _encoding
            End Get
            Set(value As DetectedTextEncoding)
                If SetProperty(_encoding, value) Then
                    OnPropertyChanged(NameOf(EncodingName))
                    OnPropertyChanged(NameOf(NewLineName))
                End If
            End Set
        End Property

        Public ReadOnly Property EncodingName As String
            Get
                Return _encoding.Name
            End Get
        End Property

        Public ReadOnly Property NewLineName As String
            Get
                Return _encoding.NewLineName
            End Get
        End Property

        Public Property RootNode As JsonTreeNode
            Get
                Return _rootNode
            End Get
            Set(value As JsonTreeNode)
                SetProperty(_rootNode, value)
            End Set
        End Property

        ''' Syntax/schema validation summary of this document ("Not checked", "Valid JSON", "Invalid JSON").
        Public Property ValidationState As String
            Get
                Return _validationState
            End Get
            Set(value As String)
                SetProperty(_validationState, If(value, ""))
            End Set
        End Property

        ''' RFC 6901 pointer of the current selection/caret position ("" = root).
        Public Property SelectedPointer As String
            Get
                Return _selectedPointer
            End Get
            Set(value As String)
                SetProperty(_selectedPointer, If(value, ""))
            End Set
        End Property

        ''' Last captured grid view state (expansion/selection/scroll), used to
        ''' restore the grid across text/grid round trips.
        Public Property GridState As GridViewState
            Get
                Return _gridState
            End Get
            Set(value As GridViewState)
                SetProperty(_gridState, value)
            End Set
        End Property

        Public Property NodeCount As Integer
            Get
                Return _nodeCount
            End Get
            Set(value As Integer)
                If SetProperty(_nodeCount, value) Then
                    OnPropertyChanged(NameOf(NodeCountDisplay))
                End If
            End Set
        End Property

        Public ReadOnly Property NodeCountDisplay As String
            Get
                Return $"{_nodeCount} nodes"
            End Get
        End Property

        Public ReadOnly Property DirtyStatusDisplay As String
            Get
                Return If(_isDirty, "Unsaved", "Saved")
            End Get
        End Property

        Public ReadOnly Property DisplayFilePath As String
            Get
                If String.IsNullOrWhiteSpace(CurrentFilePath) Then
                    Return "Untitled"
                End If

                Return CurrentFilePath
            End Get
        End Property

        Public ReadOnly Property DisplayFileName As String
            Get
                If String.IsNullOrWhiteSpace(CurrentFilePath) Then
                    Return "Untitled"
                End If

                Return IO.Path.GetFileName(CurrentFilePath)
            End Get
        End Property
    End Class
End Namespace
