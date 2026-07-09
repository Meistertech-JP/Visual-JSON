' SPDX-License-Identifier: MPL-2.0
Imports VisualJson.Core.Models
Imports VisualJson.Core.Serialization

Namespace Services
    Public Class GridUndoService
        Private ReadOnly _serializer As New JsonTreeSerializer()
        Private ReadOnly _parser As New Parsing.JsonParserService()
        Private ReadOnly _undoStack As New Stack(Of String)()
        Private ReadOnly _redoStack As New Stack(Of String)()

        Public Sub Capture(root As JsonTreeNode)
            If root Is Nothing Then
                Return
            End If

            _undoStack.Push(_serializer.Serialize(root))
            _redoStack.Clear()
        End Sub

        ''' Registers a pre-edit snapshot captured earlier (e.g. when a grid cell edit started),
        ''' so a committed value/key edit becomes one undoable operation.
        Public Sub PushSnapshot(serializedRoot As String)
            If String.IsNullOrWhiteSpace(serializedRoot) Then
                Return
            End If

            _undoStack.Push(serializedRoot)
            _redoStack.Clear()
        End Sub

        Public Function CanUndo() As Boolean
            Return _undoStack.Count > 0
        End Function

        Public Function CanRedo() As Boolean
            Return _redoStack.Count > 0
        End Function

        Public Function Undo(currentRoot As JsonTreeNode) As JsonTreeNode
            If Not CanUndo() Then
                Return Nothing
            End If

            If currentRoot IsNot Nothing Then
                _redoStack.Push(_serializer.Serialize(currentRoot))
            End If

            Return _parser.Parse(_undoStack.Pop()).Root
        End Function

        Public Function Undo() As JsonTreeNode
            Return Undo(Nothing)
        End Function

        Public Function Redo(currentRoot As JsonTreeNode) As JsonTreeNode
            If Not CanRedo() Then
                Return Nothing
            End If

            If currentRoot IsNot Nothing Then
                _undoStack.Push(_serializer.Serialize(currentRoot))
            End If

            Return _parser.Parse(_redoStack.Pop()).Root
        End Function

        Public Sub Clear()
            _undoStack.Clear()
            _redoStack.Clear()
        End Sub
    End Class
End Namespace
