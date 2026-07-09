' SPDX-License-Identifier: MPL-2.0
Imports VisualJson.Core.Models

Namespace Parsing
    Friend NotInheritable Class JsonSourcePositionMapper
        Private Sub New()
        End Sub

        Public Shared Sub Map(text As String, root As JsonTreeNode)
            If root Is Nothing Then
                Return
            End If

            Dim cursor = New SourceCursor(If(text, ""))
            cursor.SkipWhitespace()
            MapNode(cursor, root)
        End Sub

        Private Shared Sub MapNode(cursor As SourceCursor, node As JsonTreeNode)
            cursor.SkipWhitespace()
            node.SourceLine = cursor.Line
            node.SourceColumn = cursor.Column
            node.SourceStartIndex = cursor.Index

            Select Case node.Kind
                Case JsonNodeKind.ObjectValue
                    MapObject(cursor, node)
                Case JsonNodeKind.ArrayValue
                    MapArray(cursor, node)
                Case JsonNodeKind.StringValue
                    cursor.SkipString()
                Case JsonNodeKind.NumberValue
                    cursor.SkipNumber()
                Case JsonNodeKind.BooleanValue
                    cursor.SkipLiteral()
                Case JsonNodeKind.NullValue
                    cursor.SkipLiteral()
            End Select

            node.SourceEndIndex = cursor.Index
        End Sub

        Private Shared Sub MapObject(cursor As SourceCursor, node As JsonTreeNode)
            cursor.Consume("{"c)
            cursor.SkipWhitespace()

            If cursor.TryConsume("}"c) Then
                Return
            End If

            For Each child In node.Children
                cursor.SkipWhitespace()
                Dim keyStart = cursor.Index
                cursor.SkipString()
                child.SourceKeyStartIndex = keyStart
                child.SourceKeyEndIndex = cursor.Index
                cursor.SkipWhitespace()
                cursor.Consume(":"c)
                MapNode(cursor, child)
                cursor.SkipWhitespace()

                If cursor.TryConsume(","c) Then
                    Continue For
                End If

                cursor.TryConsume("}"c)
                Exit For
            Next
        End Sub

        Private Shared Sub MapArray(cursor As SourceCursor, node As JsonTreeNode)
            cursor.Consume("["c)
            cursor.SkipWhitespace()

            If cursor.TryConsume("]"c) Then
                Return
            End If

            For Each child In node.Children
                MapNode(cursor, child)
                cursor.SkipWhitespace()

                If cursor.TryConsume(","c) Then
                    Continue For
                End If

                cursor.TryConsume("]"c)
                Exit For
            Next
        End Sub

        Private NotInheritable Class SourceCursor
            Private ReadOnly _text As String

            Public Sub New(text As String)
                _text = text
                Index = 0
                Line = 1
                Column = 1
            End Sub

            Public Property Index As Integer
            Public Property Line As Integer
            Public Property Column As Integer

            Public Sub SkipWhitespace()
                While Index < _text.Length AndAlso Char.IsWhiteSpace(_text(Index))
                    Advance()
                End While
            End Sub

            Public Sub Consume(expected As Char)
                If Index >= _text.Length OrElse _text(Index) <> expected Then
                    Return
                End If

                Advance()
            End Sub

            Public Function TryConsume(expected As Char) As Boolean
                If Index < _text.Length AndAlso _text(Index) = expected Then
                    Advance()
                    Return True
                End If

                Return False
            End Function

            Public Sub SkipString()
                If Index >= _text.Length OrElse _text(Index) <> """"c Then
                    Return
                End If

                Advance()
                Dim escaped = False

                While Index < _text.Length
                    Dim ch = _text(Index)
                    Advance()

                    If escaped Then
                        escaped = False
                    ElseIf ch = "\"c Then
                        escaped = True
                    ElseIf ch = """"c Then
                        Exit While
                    End If
                End While
            End Sub

            Public Sub SkipNumber()
                While Index < _text.Length
                    Dim ch = _text(Index)
                    If Char.IsDigit(ch) OrElse ch = "-"c OrElse ch = "+"c OrElse ch = "."c OrElse ch = "e"c OrElse ch = "E"c Then
                        Advance()
                    Else
                        Exit While
                    End If
                End While
            End Sub

            Public Sub SkipLiteral()
                While Index < _text.Length AndAlso Char.IsLetter(_text(Index))
                    Advance()
                End While
            End Sub

            Private Sub Advance()
                If Index >= _text.Length Then
                    Return
                End If

                Dim ch = _text(Index)
                Index += 1

                If ch = ControlChars.Lf Then
                    Line += 1
                    Column = 1
                Else
                    Column += 1
                End If
            End Sub
        End Class
    End Class
End Namespace
