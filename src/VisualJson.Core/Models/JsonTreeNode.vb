' SPDX-License-Identifier: MPL-2.0
Imports System.Collections.ObjectModel
Imports System.ComponentModel
Imports System.Runtime.CompilerServices

Namespace Models
    Public Class JsonTreeNode
        Implements INotifyPropertyChanged

        Private _key As String
        Private _kind As JsonNodeKind
        Private _valueText As String
        Private _sourceLine As Integer?
        Private _sourceColumn As Integer?
        Private _sourceStartIndex As Integer?
        Private _sourceEndIndex As Integer?
        Private _sourceKeyStartIndex As Integer?
        Private _sourceKeyEndIndex As Integer?

        Public Sub New(key As String, displayPath As String, kind As JsonNodeKind, Optional valueText As String = "", Optional jsonPointer As String = "")
            _key = key
            Me.DisplayPath = displayPath
            Me.JsonPointer = If(jsonPointer, "")
            _kind = kind
            _valueText = If(valueText, "")
            Children = New ObservableCollection(Of JsonTreeNode)()
        End Sub

        Public Shared Function EscapePointerSegment(segment As String) As String
            Return If(segment, "").Replace("~", "~0").Replace("/", "~1")
        End Function

        Public Event PropertyChanged As PropertyChangedEventHandler Implements INotifyPropertyChanged.PropertyChanged

        Public Property Key As String
            Get
                Return _key
            End Get
            Set(value As String)
                Dim nextValue = If(value, "")
                If String.Equals(_key, nextValue, StringComparison.Ordinal) Then
                    Return
                End If

                _key = nextValue
                OnPropertyChanged()
            End Set
        End Property

        Public ReadOnly Property DisplayPath As String
        Public ReadOnly Property JsonPointer As String
        Public ReadOnly Property Children As ObservableCollection(Of JsonTreeNode)

        ''' JsonPointer for display: the root pointer is the empty string per RFC 6901.
        Public ReadOnly Property PointerDisplay As String
            Get
                If String.IsNullOrEmpty(JsonPointer) Then
                    Return "(root)"
                End If

                Return JsonPointer
            End Get
        End Property

        Public Property SourceLine As Integer?
            Get
                Return _sourceLine
            End Get
            Set(value As Integer?)
                If Nullable.Equals(_sourceLine, value) Then
                    Return
                End If

                _sourceLine = value
                OnPropertyChanged()
                OnPropertyChanged(NameOf(SourceLocation))
            End Set
        End Property

        Public Property SourceColumn As Integer?
            Get
                Return _sourceColumn
            End Get
            Set(value As Integer?)
                If Nullable.Equals(_sourceColumn, value) Then
                    Return
                End If

                _sourceColumn = value
                OnPropertyChanged()
                OnPropertyChanged(NameOf(SourceLocation))
            End Set
        End Property

        Public Property SourceStartIndex As Integer?
            Get
                Return _sourceStartIndex
            End Get
            Set(value As Integer?)
                If Nullable.Equals(_sourceStartIndex, value) Then
                    Return
                End If

                _sourceStartIndex = value
                OnPropertyChanged()
            End Set
        End Property

        Public Property SourceEndIndex As Integer?
            Get
                Return _sourceEndIndex
            End Get
            Set(value As Integer?)
                If Nullable.Equals(_sourceEndIndex, value) Then
                    Return
                End If

                _sourceEndIndex = value
                OnPropertyChanged()
            End Set
        End Property

        Public Property SourceKeyStartIndex As Integer?
            Get
                Return _sourceKeyStartIndex
            End Get
            Set(value As Integer?)
                If Nullable.Equals(_sourceKeyStartIndex, value) Then
                    Return
                End If

                _sourceKeyStartIndex = value
                OnPropertyChanged()
            End Set
        End Property

        Public Property SourceKeyEndIndex As Integer?
            Get
                Return _sourceKeyEndIndex
            End Get
            Set(value As Integer?)
                If Nullable.Equals(_sourceKeyEndIndex, value) Then
                    Return
                End If

                _sourceKeyEndIndex = value
                OnPropertyChanged()
            End Set
        End Property

        Public Property Kind As JsonNodeKind
            Get
                Return _kind
            End Get
            Set(value As JsonNodeKind)
                If _kind = value Then
                    Return
                End If

                _kind = value
                OnPropertyChanged()
                OnPropertyChanged(NameOf(CanEditValue))
                OnPropertyChanged(NameOf(TypeName))
            End Set
        End Property

        Public Property ValueText As String
            Get
                Return _valueText
            End Get
            Set(value As String)
                Dim nextValue = If(value, "")
                If String.Equals(_valueText, nextValue, StringComparison.Ordinal) Then
                    Return
                End If

                _valueText = nextValue
                OnPropertyChanged()
            End Set
        End Property

        Public ReadOnly Property CanEditValue As Boolean
            Get
                Return Kind <> JsonNodeKind.ObjectValue AndAlso Kind <> JsonNodeKind.ArrayValue
            End Get
        End Property

        Public ReadOnly Property CanEditKey As Boolean
            Get
                Return Not String.Equals(Key, "$", StringComparison.Ordinal) AndAlso Not Key.StartsWith("[", StringComparison.Ordinal)
            End Get
        End Property

        Public ReadOnly Property SourceLocation As String
            Get
                If SourceLine.HasValue AndAlso SourceColumn.HasValue Then
                    Return $"{SourceLine}:{SourceColumn}"
                End If

                Return ""
            End Get
        End Property

        Public ReadOnly Property TypeName As String
            Get
                Select Case Kind
                    Case JsonNodeKind.ObjectValue
                        Return "object"
                    Case JsonNodeKind.ArrayValue
                        Return "array"
                    Case JsonNodeKind.StringValue
                        Return "string"
                    Case JsonNodeKind.NumberValue
                        Return "number"
                    Case JsonNodeKind.BooleanValue
                        Return "boolean"
                    Case JsonNodeKind.NullValue
                        Return "null"
                    Case Else
                        Return "unknown"
                End Select
            End Get
        End Property

        Private Sub OnPropertyChanged(<CallerMemberName> Optional propertyName As String = Nothing)
            RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(propertyName))
        End Sub
    End Class
End Namespace
