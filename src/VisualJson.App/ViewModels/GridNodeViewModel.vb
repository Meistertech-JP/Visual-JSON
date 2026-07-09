' SPDX-License-Identifier: MPL-2.0
Imports System.Collections.ObjectModel
Imports VisualJson.Core.Models
Imports VisualJson.Core.Services

Namespace ViewModels
    Public Class GridNodeViewModel
        Inherits ObservableObject

        Private _isExpanded As Boolean
        Private _isSelected As Boolean

        Public Sub New(model As JsonTreeNode)
            Me.New(model, Nothing, "")
        End Sub

        Public Sub New(model As JsonTreeNode, expandedPointers As ISet(Of String), selectedPointer As String)
            Me.Model = model
            Children = New ObservableCollection(Of GridNodeViewModel)()

            If model IsNot Nothing Then
                _isExpanded = expandedPointers IsNot Nothing AndAlso expandedPointers.Contains(model.JsonPointer)
                _isSelected = String.Equals(model.JsonPointer, If(selectedPointer, ""), StringComparison.Ordinal)

                For Each child In model.Children
                    Children.Add(New GridNodeViewModel(child, expandedPointers, selectedPointer))
                Next
            End If
        End Sub

        Public ReadOnly Property Model As JsonTreeNode
        Public ReadOnly Property Children As ObservableCollection(Of GridNodeViewModel)

        ''' Drives the Action column "Table" button (FR-P2-301: object-majority arrays).
        Public ReadOnly Property IsTableCandidate As Boolean
            Get
                Return TableViewModelBuilder.IsCandidate(Model)
            End Get
        End Property

        Public Property IsExpanded As Boolean
            Get
                Return _isExpanded
            End Get
            Set(value As Boolean)
                SetProperty(_isExpanded, value)
            End Set
        End Property

        Public Property IsSelected As Boolean
            Get
                Return _isSelected
            End Get
            Set(value As Boolean)
                SetProperty(_isSelected, value)
            End Set
        End Property

        Public Shared Function FromNode(model As JsonTreeNode, expandedPointers As ISet(Of String), selectedPointer As String) As GridNodeViewModel
            If model Is Nothing Then
                Return Nothing
            End If

            Return New GridNodeViewModel(model, expandedPointers, selectedPointer)
        End Function

        Public Function FindByPointer(pointer As String) As GridNodeViewModel
            If Model IsNot Nothing AndAlso String.Equals(Model.JsonPointer, If(pointer, ""), StringComparison.Ordinal) Then
                Return Me
            End If

            For Each child In Children
                Dim found = child.FindByPointer(pointer)
                If found IsNot Nothing Then
                    Return found
                End If
            Next

            Return Nothing
        End Function

        Public Sub CollectExpandedPointers(result As ISet(Of String))
            If result Is Nothing OrElse Model Is Nothing Then
                Return
            End If

            If IsExpanded Then
                result.Add(Model.JsonPointer)
            End If

            For Each child In Children
                child.CollectExpandedPointers(result)
            Next
        End Sub

        Public Function FindSelectedPointer() As String
            If Model IsNot Nothing AndAlso IsSelected Then
                Return Model.JsonPointer
            End If

            For Each child In Children
                Dim selected = child.FindSelectedPointer()
                If selected IsNot Nothing Then
                    Return selected
                End If
            Next

            Return Nothing
        End Function
    End Class
End Namespace
