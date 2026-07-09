' SPDX-License-Identifier: MPL-2.0
Imports System.Windows.Media

Namespace UI
    Friend Class SyntaxToken
        Public Sub New(start As Integer, length As Integer, foreground As Brush)
            Me.Start = start
            Me.Length = length
            Me.Foreground = foreground
        End Sub

        Public ReadOnly Property Start As Integer
        Public ReadOnly Property Length As Integer
        Public ReadOnly Property Foreground As Brush
    End Class

    Friend NotInheritable Class JsonSyntaxTokenizer
        Private Shared ReadOnly StringBrush As Brush = New SolidColorBrush(Color.FromRgb(3, 105, 161))
        Private Shared ReadOnly NumberBrush As Brush = New SolidColorBrush(Color.FromRgb(147, 51, 234))
        Private Shared ReadOnly LiteralBrush As Brush = New SolidColorBrush(Color.FromRgb(22, 101, 52))
        Private Shared ReadOnly PunctuationBrush As Brush = New SolidColorBrush(Color.FromRgb(82, 82, 91))

        Private Sub New()
        End Sub

        Public Shared Iterator Function Tokenize(text As String) As IEnumerable(Of SyntaxToken)
            Dim index = 0
            Dim source = If(text, "")

            While index < source.Length
                Dim ch = source(index)

                If ch = """"c Then
                    Dim start = index
                    SkipString(source, index)
                    Yield New SyntaxToken(start, index - start, StringBrush)
                ElseIf Char.IsDigit(ch) OrElse ch = "-"c Then
                    Dim start = index
                    SkipNumber(source, index)
                    Yield New SyntaxToken(start, Math.Max(1, index - start), NumberBrush)
                ElseIf IsLiteralStart(source, index) Then
                    Dim start = index
                    SkipLetters(source, index)
                    Yield New SyntaxToken(start, index - start, LiteralBrush)
                ElseIf "{}[]:,".IndexOf(ch) >= 0 Then
                    Yield New SyntaxToken(index, 1, PunctuationBrush)
                    index += 1
                Else
                    index += 1
                End If
            End While
        End Function

        Private Shared Sub SkipString(text As String, ByRef index As Integer)
            index += 1
            Dim escaped = False

            While index < text.Length
                Dim ch = text(index)
                index += 1

                If escaped Then
                    escaped = False
                ElseIf ch = "\"c Then
                    escaped = True
                ElseIf ch = """"c Then
                    Exit While
                End If
            End While
        End Sub

        Private Shared Sub SkipNumber(text As String, ByRef index As Integer)
            While index < text.Length
                Dim ch = text(index)
                If Char.IsDigit(ch) OrElse ch = "-"c OrElse ch = "+"c OrElse ch = "."c OrElse ch = "e"c OrElse ch = "E"c Then
                    index += 1
                Else
                    Exit While
                End If
            End While
        End Sub

        Private Shared Function IsLiteralStart(text As String, index As Integer) As Boolean
            Return StartsWith(text, index, "true") OrElse StartsWith(text, index, "false") OrElse StartsWith(text, index, "null")
        End Function

        Private Shared Function StartsWith(text As String, index As Integer, value As String) As Boolean
            If index + value.Length > text.Length Then
                Return False
            End If

            Return String.CompareOrdinal(text, index, value, 0, value.Length) = 0
        End Function

        Private Shared Sub SkipLetters(text As String, ByRef index As Integer)
            While index < text.Length AndAlso Char.IsLetter(text(index))
                index += 1
            End While
        End Sub
    End Class
End Namespace
