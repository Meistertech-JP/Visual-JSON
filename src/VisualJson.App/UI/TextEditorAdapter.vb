' SPDX-License-Identifier: MPL-2.0
Imports System.Windows
Imports System.Windows.Input
Imports System.Windows.Media
Imports ICSharpCode.AvalonEdit
Imports ICSharpCode.AvalonEdit.CodeCompletion
Imports ICSharpCode.AvalonEdit.Document
Imports ICSharpCode.AvalonEdit.Editing
Imports ICSharpCode.AvalonEdit.Folding
Imports ICSharpCode.AvalonEdit.Rendering
Imports VisualJson.Core.Diagnostics
Imports VisualJson.Core.Services

Namespace UI
    ''' Adapter around the AvalonEdit TextEditor. AvalonEdit virtualizes rendering and
    ''' colorizes only visible lines, so large documents stay responsive (the previous
    ''' RichTextBox froze the UI on a few hundred KB because per-token formatting over
    ''' the whole FlowDocument is quadratic).
    Public Class TextEditorAdapter
        ''' Lines longer than this are rendered without token coloring to keep
        ''' pathological single-line documents (e.g. unformatted machine JSON) responsive.
        Friend Const MaxColorizedLineLength As Integer = 8000

        Private ReadOnly _editor As TextEditor
        Private ReadOnly _errorColorizer As ErrorLineColorizer
        Private ReadOnly _searchColorizer As SearchResultColorizer
        Private ReadOnly _foldingManager As FoldingManager

        Public Sub New(editor As TextEditor)
            _editor = editor
            _editor.Options.EnableHyperlinks = False
            _editor.Options.EnableEmailHyperlinks = False
            _editor.Options.AllowScrollBelowDocument = False
            _editor.TextArea.TextView.LineTransformers.Add(New JsonSyntaxColorizer())
            _searchColorizer = New SearchResultColorizer()
            _editor.TextArea.TextView.LineTransformers.Add(_searchColorizer)
            _errorColorizer = New ErrorLineColorizer()
            _editor.TextArea.TextView.LineTransformers.Add(_errorColorizer)
            _foldingManager = FoldingManager.Install(_editor.TextArea)
            AutoPairingEnabled = True
            AddHandler _editor.TextArea.TextEntering, AddressOf TextArea_TextEntering
        End Sub

        Public Property AutoPairingEnabled As Boolean

        Private _completionWindow As CompletionWindow

        ''' FR-P2-504: shows the key completion popup (Q-P2-002: AvalonEdit
        ''' CompletionWindow). Returns the number of listed candidates.
        Public Function ShowKeyCompletion(candidates As IReadOnlyList(Of String)) As Integer
            _completionWindow?.Close()
            If candidates Is Nothing OrElse candidates.Count = 0 Then
                Return 0
            End If

            Dim window = New CompletionWindow(_editor.TextArea)
            For Each name In candidates
                window.CompletionList.CompletionData.Add(New KeyCompletionData(name))
            Next

            _completionWindow = window
            AddHandler window.Closed, Sub() _completionWindow = Nothing
            window.Show()
            Return candidates.Count
        End Function

        Public ReadOnly Property IsCompletionOpen As Boolean
            Get
                Return _completionWindow IsNot Nothing
            End Get
        End Property

        Public Function CommitSelectedCompletion() As Boolean
            Dim window = _completionWindow
            If window Is Nothing OrElse window.CompletionList.CompletionData.Count = 0 Then
                Return False
            End If

            If window.CompletionList.SelectedItem Is Nothing Then
                window.CompletionList.SelectedItem = window.CompletionList.CompletionData(0)
            End If

            window.CompletionList.RequestInsertion(EventArgs.Empty)
            Return True
        End Function

        Private NotInheritable Class KeyCompletionData
            Implements ICompletionData

            Private ReadOnly _name As String

            Public Sub New(name As String)
                _name = If(name, "")
            End Sub

            Public ReadOnly Property Image As ImageSource Implements ICompletionData.Image
                Get
                    Return Nothing
                End Get
            End Property

            Public ReadOnly Property Text As String Implements ICompletionData.Text
                Get
                    Return _name
                End Get
            End Property

            Public ReadOnly Property Content As Object Implements ICompletionData.Content
                Get
                    Return _name
                End Get
            End Property

            Public ReadOnly Property Description As Object Implements ICompletionData.Description
                Get
                    Return $"""{_name}"": "
                End Get
            End Property

            Public ReadOnly Property Priority As Double Implements ICompletionData.Priority
                Get
                    Return 0
                End Get
            End Property

            Public Sub Complete(textArea As TextArea, completionSegment As ISegment, insertionRequestEventArgs As EventArgs) Implements ICompletionData.Complete
                textArea.Document.Replace(completionSegment, $"""{_name}"": ")
            End Sub
        End Class

        Public Function GetText() As String
            Return _editor.Text
        End Function

        Public Sub SetText(text As String)
            _editor.Document.Text = If(text, "")
            _editor.CaretOffset = 0
            _editor.ScrollToHome()
        End Sub

        Public Function GetCaretOffset() As Integer
            Return _editor.CaretOffset
        End Function

        Public Sub SetCaretOffset(offset As Integer)
            SelectText(offset, 0)
        End Sub

        Public Function GetVerticalOffset() As Double
            Return _editor.VerticalOffset
        End Function

        Public Sub ScrollToVerticalOffset(offset As Double)
            _editor.ScrollToVerticalOffset(Math.Max(0, offset))
        End Sub

        Public Function GetSelectionStart() As Integer
            Return _editor.SelectionStart
        End Function

        Public Function GetSelectionLength() As Integer
            Return _editor.SelectionLength
        End Function

        Public Sub InsertCharacterForAutomation(ch As Char)
            If Not TryInsertPairedCharacter(ch) Then
                Dim offset = _editor.CaretOffset
                _editor.Document.Insert(_editor.CaretOffset, ch.ToString())
                _editor.CaretOffset = offset + 1
            End If
        End Sub

        Public Sub ReplaceText(text As String)
            Dim caret = Math.Min(_editor.CaretOffset, If(text, "").Length)
            _editor.Document.Text = If(text, "")
            _editor.CaretOffset = caret
        End Sub

        Public Sub SelectText(offset As Integer, length As Integer)
            Dim documentLength = _editor.Document.TextLength
            Dim start = Math.Min(Math.Max(0, offset), documentLength)
            Dim safeLength = Math.Min(Math.Max(0, length), documentLength - start)

            _editor.Select(start, safeLength)
            Dim location = _editor.Document.GetLocation(start)
            _editor.ScrollTo(location.Line, location.Column)
            _editor.TextArea.Focus()
        End Sub

        Public Sub SetSearchHighlights(matches As IEnumerable(Of SearchMatch), Optional maxHighlights As Integer = 1000)
            Dim ranges = New List(Of TextRange)()
            For Each match In If(matches, Array.Empty(Of SearchMatch)()).Take(Math.Max(0, maxHighlights))
                If match.Length > 0 Then
                    ranges.Add(New TextRange(match.StartIndex, match.Length))
                End If
            Next

            _searchColorizer.SetRanges(ranges)
            _editor.TextArea.TextView.Redraw()
        End Sub

        Public Sub ApplyJsonFoldings(ranges As IEnumerable(Of JsonFoldingRange))
            Dim folded = New HashSet(Of String)(StringComparer.Ordinal)
            For Each section In _foldingManager.AllFoldings
                If section.IsFolded Then
                    folded.Add(FoldingKey(section.StartOffset, section.EndOffset))
                End If
            Next

            Dim newFoldings = New List(Of NewFolding)()
            For Each range In If(ranges, Array.Empty(Of JsonFoldingRange)())
                If range.EndIndex > range.StartIndex Then
                    newFoldings.Add(New NewFolding(range.StartIndex, range.EndIndex) With {
                        .Name = "...",
                        .DefaultClosed = folded.Contains(FoldingKey(range.StartIndex, range.EndIndex))
                    })
                End If
            Next

            _foldingManager.UpdateFoldings(newFoldings, -1)

            For Each section In _foldingManager.AllFoldings
                If folded.Contains(FoldingKey(section.StartOffset, section.EndOffset)) Then
                    section.IsFolded = True
                End If
            Next
        End Sub

        Public Function GetFoldingCount() As Integer
            Return _foldingManager.AllFoldings.Count()
        End Function

        Public Function GetFoldedCount() As Integer
            Return _foldingManager.AllFoldings.Count(Function(item) item.IsFolded)
        End Function

        Public Function CollapseFirstFolding() As Boolean
            Dim first = _foldingManager.AllFoldings.OrderBy(Function(item) item.StartOffset).FirstOrDefault()
            If first Is Nothing Then
                Return False
            End If

            first.IsFolded = True
            Return True
        End Function

        Public Sub MoveToLineColumn(line As Integer, column As Integer)
            SelectText(GetOffsetForLineColumn(line, column), 0)
        End Sub

        Public Function GetLineColumnFromOffset(offset As Integer) As (Line As Integer, Column As Integer)
            Dim documentLength = _editor.Document.TextLength
            Dim location = _editor.Document.GetLocation(Math.Min(Math.Max(0, offset), documentLength))
            Return (location.Line, location.Column)
        End Function

        Public Function GetOffsetForLineColumn(line As Integer, column As Integer) As Integer
            Dim safeLine = Math.Min(Math.Max(1, line), _editor.Document.LineCount)
            Dim documentLine = _editor.Document.GetLineByNumber(safeLine)
            Dim columnOffset = Math.Min(Math.Max(1, column) - 1, documentLine.Length)
            Return documentLine.Offset + columnOffset
        End Function

        ''' Updates the error markers. Coloring itself happens lazily per visible line,
        ''' so this stays cheap no matter how large the document is.
        Public Sub ApplySyntaxHighlighting(diagnostics As IEnumerable(Of ValidationDiagnostic))
            Dim errorLines = New HashSet(Of Integer)()
            For Each diagnostic In If(diagnostics, Array.Empty(Of ValidationDiagnostic)())
                If diagnostic.Line.HasValue Then
                    errorLines.Add(diagnostic.Line.Value)
                End If
            Next

            _errorColorizer.SetErrorLines(errorLines)
            _editor.TextArea.TextView.Redraw()
        End Sub

        Private Shared Function FoldingKey(startOffset As Integer, endOffset As Integer) As String
            Return $"{startOffset}:{endOffset}"
        End Function

        Private Sub TextArea_TextEntering(sender As Object, e As TextCompositionEventArgs)
            If Not AutoPairingEnabled OrElse String.IsNullOrEmpty(e.Text) OrElse e.Text.Length <> 1 Then
                Return
            End If

            If TryInsertPairedCharacter(e.Text(0)) Then
                e.Handled = True
            End If
        End Sub

        Private Function TryInsertPairedCharacter(ch As Char) As Boolean
            If Not AutoPairingEnabled Then
                Return False
            End If

            Dim pair = GetPair(ch)
            If pair Is Nothing Then
                Return False
            End If

            If _editor.SelectionLength = 0 AndAlso ch = pair.Value.Closing AndAlso IsNextCharacter(ch) Then
                _editor.CaretOffset += 1
                Return True
            End If

            If ch <> pair.Value.Opening Then
                Return False
            End If

            Dim selectionStart = _editor.SelectionStart
            Dim selectionLength = _editor.SelectionLength
            If selectionLength > 0 Then
                Dim selected = _editor.Document.GetText(selectionStart, selectionLength)
                Dim inserted = pair.Value.Opening & selected & pair.Value.Closing
                _editor.Document.Replace(selectionStart, selectionLength, inserted)
                _editor.CaretOffset = selectionStart + inserted.Length
            Else
                Dim offset = _editor.CaretOffset
                _editor.Document.Insert(offset, $"{pair.Value.Opening}{pair.Value.Closing}")
                _editor.CaretOffset = offset + 1
            End If

            Return True
        End Function

        Private Function IsNextCharacter(ch As Char) As Boolean
            Dim offset = _editor.CaretOffset
            Return offset < _editor.Document.TextLength AndAlso
                String.Equals(_editor.Document.GetText(offset, 1), ch.ToString(), StringComparison.Ordinal)
        End Function

        Private Shared Function GetPair(ch As Char) As (Opening As Char, Closing As Char)?
            Select Case ch
                Case "{"c, "}"c
                    Return ("{"c, "}"c)
                Case "["c, "]"c
                    Return ("["c, "]"c)
                Case """"c
                    Return (""""c, """"c)
                Case Else
                    Return Nothing
            End Select
        End Function

        Private NotInheritable Class JsonSyntaxColorizer
            Inherits DocumentColorizingTransformer

            Protected Overrides Sub ColorizeLine(line As DocumentLine)
                If line.Length = 0 OrElse line.Length > MaxColorizedLineLength Then
                    Return
                End If

                Dim text = CurrentContext.Document.GetText(line)
                For Each token In JsonSyntaxTokenizer.Tokenize(text)
                    Dim tokenStart = line.Offset + token.Start
                    Dim brush = token.Foreground
                    ChangeLinePart(tokenStart, tokenStart + token.Length,
                                   Sub(element) element.TextRunProperties.SetForegroundBrush(brush))
                Next
            End Sub
        End Class

        Private NotInheritable Class SearchResultColorizer
            Inherits DocumentColorizingTransformer

            Private Shared ReadOnly SearchBackground As Brush = New SolidColorBrush(Color.FromRgb(254, 240, 138))
            Private _ranges As New List(Of TextRange)()

            Public Sub SetRanges(ranges As IEnumerable(Of TextRange))
                _ranges = If(ranges, Array.Empty(Of TextRange)()).OrderBy(Function(item) item.StartIndex).ToList()
            End Sub

            Protected Overrides Sub ColorizeLine(line As DocumentLine)
                If _ranges.Count = 0 Then
                    Return
                End If

                Dim lineStart = line.Offset
                Dim lineEnd = line.EndOffset
                For Each range In _ranges
                    If range.EndIndex <= lineStart Then
                        Continue For
                    End If

                    If range.StartIndex >= lineEnd Then
                        Exit For
                    End If

                    Dim start = Math.Max(lineStart, range.StartIndex)
                    Dim [end] = Math.Min(lineEnd, range.EndIndex)
                    If [end] > start Then
                        ChangeLinePart(start, [end],
                                       Sub(element) element.TextRunProperties.SetBackgroundBrush(SearchBackground))
                    End If
                Next
            End Sub
        End Class

        Private NotInheritable Class ErrorLineColorizer
            Inherits DocumentColorizingTransformer

            Private Shared ReadOnly ErrorBackground As Brush = New SolidColorBrush(Color.FromRgb(254, 226, 226))
            Private _errorLines As New HashSet(Of Integer)()

            Public Sub SetErrorLines(lines As HashSet(Of Integer))
                _errorLines = If(lines, New HashSet(Of Integer)())
            End Sub

            Protected Overrides Sub ColorizeLine(line As DocumentLine)
                If line.Length = 0 OrElse Not _errorLines.Contains(line.LineNumber) Then
                    Return
                End If

                ChangeLinePart(line.Offset, line.EndOffset,
                               Sub(element)
                                   element.TextRunProperties.SetBackgroundBrush(ErrorBackground)
                                   element.TextRunProperties.SetTextDecorations(TextDecorations.Underline)
                               End Sub)
            End Sub
        End Class
    End Class
End Namespace
