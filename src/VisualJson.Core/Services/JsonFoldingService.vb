' SPDX-License-Identifier: MPL-2.0
Namespace Services
    Public Class JsonFoldingService
        Public Function CreateRanges(text As String) As IReadOnlyList(Of JsonFoldingRange)
            Dim source = If(text, "")
            Dim ranges = New List(Of JsonFoldingRange)()
            Dim stack = New Stack(Of FoldStart)()
            Dim line = 1
            Dim inString = False
            Dim escaped = False

            For index = 0 To source.Length - 1
                Dim ch = source(index)

                If inString Then
                    If escaped Then
                        escaped = False
                    ElseIf ch = "\"c Then
                        escaped = True
                    ElseIf ch = """"c Then
                        inString = False
                    End If
                Else
                    Select Case ch
                        Case """"c
                            inString = True
                        Case "{"c, "["c
                            stack.Push(New FoldStart(index, line, ch))
                        Case "}"c, "]"c
                            If stack.Count > 0 Then
                                Dim start = stack.Pop()
                                If IsMatching(start.Opening, ch) AndAlso line > start.Line Then
                                    ranges.Add(New JsonFoldingRange(start.Index, index + 1, start.Line, line))
                                End If
                            End If
                    End Select
                End If

                If ch = ControlChars.Lf Then
                    line += 1
                End If
            Next

            ranges.Sort(Function(left, right)
                            Dim byStart = left.StartIndex.CompareTo(right.StartIndex)
                            If byStart <> 0 Then
                                Return byStart
                            End If

                            Return left.EndIndex.CompareTo(right.EndIndex)
                        End Function)
            Return ranges
        End Function

        Private Shared Function IsMatching(opening As Char, closing As Char) As Boolean
            Return (opening = "{"c AndAlso closing = "}"c) OrElse
                (opening = "["c AndAlso closing = "]"c)
        End Function

        Private NotInheritable Class FoldStart
            Public Sub New(index As Integer, line As Integer, opening As Char)
                Me.Index = index
                Me.Line = line
                Me.Opening = opening
            End Sub

            Public ReadOnly Property Index As Integer
            Public ReadOnly Property Line As Integer
            Public ReadOnly Property Opening As Char
        End Class
    End Class
End Namespace
