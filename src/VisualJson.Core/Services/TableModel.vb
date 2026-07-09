' SPDX-License-Identifier: MPL-2.0
Imports VisualJson.Core.Models

Namespace Services
    Public Class TableColumn
        Public Sub New(name As String, isValueColumn As Boolean)
            Me.Name = If(name, "")
            Me.IsValueColumn = isValueColumn
        End Sub

        Public ReadOnly Property Name As String
        ''' The synthetic "(value)" column holding non-object array elements.
        Public ReadOnly Property IsValueColumn As Boolean
    End Class

    Public Class TableCell
        Public Sub New(node As JsonTreeNode)
            Me.Node = node
        End Sub

        ''' Nothing when the row's element has no property for this column (missing cell).
        Public ReadOnly Property Node As JsonTreeNode

        Public ReadOnly Property IsMissing As Boolean
            Get
                Return Node Is Nothing
            End Get
        End Property

        Public ReadOnly Property IsContainer As Boolean
            Get
                Return Node IsNot Nothing AndAlso (Node.Kind = JsonNodeKind.ObjectValue OrElse Node.Kind = JsonNodeKind.ArrayValue)
            End Get
        End Property

        Public ReadOnly Property DisplayText As String
            Get
                If Node Is Nothing Then
                    Return ""
                End If

                Select Case Node.Kind
                    Case JsonNodeKind.ObjectValue
                        Return "{…}"
                    Case JsonNodeKind.ArrayValue
                        Return $"[{Node.Children.Count}]"
                    Case Else
                        Return Node.ValueText
                End Select
            End Get
        End Property

        ''' Read/write surface for DataGrid cell editing. WPF DataGrid coerces a
        ''' column to read-only when its binding path has no setter, so the setter
        ''' must exist; the actual commit is routed through the grid's
        ''' CellEditEnding handler and the value written here is ignored.
        Public Property EditText As String
            Get
                Return DisplayText
            End Get
            Set(value As String)
            End Set
        End Property
    End Class

    Public Class TableRow
        Public Sub New(index As Integer, elementNode As JsonTreeNode, cells As IReadOnlyList(Of TableCell))
            Me.Index = index
            Me.ElementNode = elementNode
            Me.Cells = If(cells, Array.Empty(Of TableCell)())
        End Sub

        Public ReadOnly Property Index As Integer
        Public ReadOnly Property ElementNode As JsonTreeNode
        Public ReadOnly Property Cells As IReadOnlyList(Of TableCell)

        Public ReadOnly Property RowNumber As Integer
            Get
                Return Index + 1
            End Get
        End Property
    End Class

    Public Class TableModel
        Public Sub New(sourceNode As JsonTreeNode, columns As IReadOnlyList(Of TableColumn), rows As IReadOnlyList(Of TableRow))
            Me.SourceNode = sourceNode
            Me.Columns = If(columns, Array.Empty(Of TableColumn)())
            Me.Rows = If(rows, Array.Empty(Of TableRow)())
        End Sub

        Public ReadOnly Property SourceNode As JsonTreeNode
        Public ReadOnly Property Columns As IReadOnlyList(Of TableColumn)
        Public ReadOnly Property Rows As IReadOnlyList(Of TableRow)

        Public ReadOnly Property SourcePointer As String
            Get
                Return If(SourceNode?.JsonPointer, "")
            End Get
        End Property
    End Class
End Namespace
