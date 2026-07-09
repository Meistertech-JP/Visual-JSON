' SPDX-License-Identifier: MPL-2.0
Imports System.Collections.ObjectModel
Imports VisualJson.Core.Diagnostics
Imports VisualJson.Core.Models

Namespace ViewModels
    Public Class DocumentViewModel
        Inherits ObservableObject

        Private _currentFilePath As String = ""
        Private _isDirty As Boolean
        Private _rootNode As JsonTreeNode
        Private _nodeCount As Integer
        Private _parseStatus As String = "Not checked"

        Public ReadOnly Property Diagnostics As New ObservableCollection(Of ValidationDiagnostic)()
        Public ReadOnly Property Logs As New ObservableCollection(Of String)()

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
                SetProperty(_isDirty, value)
            End Set
        End Property

        Public Property RootNode As JsonTreeNode
            Get
                Return _rootNode
            End Get
            Set(value As JsonTreeNode)
                SetProperty(_rootNode, value)
            End Set
        End Property

        Public Property NodeCount As Integer
            Get
                Return _nodeCount
            End Get
            Set(value As Integer)
                SetProperty(_nodeCount, value)
            End Set
        End Property

        Public Property ParseStatus As String
            Get
                Return _parseStatus
            End Get
            Set(value As String)
                SetProperty(_parseStatus, If(value, ""))
            End Set
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
