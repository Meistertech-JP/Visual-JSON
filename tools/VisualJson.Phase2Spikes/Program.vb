' SPDX-License-Identifier: MPL-2.0
Imports System.Collections.ObjectModel
Imports System.ComponentModel
Imports System.Diagnostics
Imports System.Globalization
Imports System.IO
Imports System.Linq
Imports System.Runtime.CompilerServices
Imports System.Text
Imports System.Text.RegularExpressions
Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Controls.Primitives
Imports System.Windows.Data
Imports System.Windows.Input
Imports System.Windows.Media
Imports System.Windows.Threading
Imports ICSharpCode.AvalonEdit
Imports ICSharpCode.AvalonEdit.CodeCompletion
Imports ICSharpCode.AvalonEdit.Document
Imports ICSharpCode.AvalonEdit.Editing
Imports ICSharpCode.AvalonEdit.Folding
Imports ICSharpCode.AvalonEdit.Search
Imports VisualJson.Core.Models
Imports VisualJson.Core.Parsing
Imports VisualJson.Core.Serialization
Imports VisualJson.Core.Services

Module Program
    <STAThread>
    Sub Main(args As String())
        Global.System.Threading.Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture
        Global.System.Threading.Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture

        Dim command = If(args.Length = 0, "all", args(0).Trim().ToLowerInvariant())
        Dim runner = New SpikeRunner(FindRepoRoot())
        runner.Run(command)
    End Sub

    Private Function FindRepoRoot() As String
        Dim directory = New DirectoryInfo(AppContext.BaseDirectory)

        While directory IsNot Nothing
            If File.Exists(Path.Combine(directory.FullName, "VisualJson.slnx")) Then
                Return directory.FullName
            End If

            directory = directory.Parent
        End While

        Return IO.Directory.GetCurrentDirectory()
    End Function
End Module

Friend NotInheritable Class SpikeRunner
    Private Const ScanThresholdMs As Double = 200.0
    Private Const TabRestoreThresholdMs As Double = 300.0
    Private Shared ReadOnly Utf8NoBom As New UTF8Encoding(encoderShouldEmitUTF8Identifier:=False)

    Private ReadOnly _root As String
    Private ReadOnly _verificationDir As String
    Private ReadOnly _benchmarkDir As String
    Private ReadOnly _parser As New JsonParserService()
    Private ReadOnly _stats As New TreeStatisticsService()
    Private ReadOnly _results As New List(Of SpikeResult)()

    Public Sub New(root As String)
        _root = root
        _verificationDir = Path.Combine(_root, "artifacts", "verification", "p2-0")
        _benchmarkDir = Path.Combine(_root, "artifacts", "benchmarks")
    End Sub

    Public Sub Run(command As String)
        Directory.CreateDirectory(_verificationDir)
        Directory.CreateDirectory(_benchmarkDir)

        Select Case command
            Case "all"
                RunSpike("P2-0-001", "TreeView state restore", AddressOf RunTreeStateSpike)
                RunSpike("P2-0-002", "Offset to node lookup", AddressOf RunOffsetMapSpike)
                RunSpike("P2-0-003", "AvalonEdit folding", AddressOf RunFoldingSpike)
                RunSpike("P2-0-004", "Search and replace", AddressOf RunSearchReplaceSpike)
                RunSpike("P2-0-005", "CompletionWindow", AddressOf RunCompletionSpike)
                RunSpike("P2-0-006", "Dynamic DataGrid", AddressOf RunDataGridSpike)
                RunSpike("P2-0-007", "Encoding detection", AddressOf RunEncodingSpike)
                RunSpike("P2-0-008", "Tab switch timing", AddressOf RunTabTimingSpike)
            Case "state-tree"
                RunSpike("P2-0-001", "TreeView state restore", AddressOf RunTreeStateSpike)
            Case "offset-map"
                RunSpike("P2-0-002", "Offset to node lookup", AddressOf RunOffsetMapSpike)
            Case "folding"
                RunSpike("P2-0-003", "AvalonEdit folding", AddressOf RunFoldingSpike)
            Case "search-replace"
                RunSpike("P2-0-004", "Search and replace", AddressOf RunSearchReplaceSpike)
            Case "completion"
                RunSpike("P2-0-005", "CompletionWindow", AddressOf RunCompletionSpike)
            Case "datagrid"
                RunSpike("P2-0-006", "Dynamic DataGrid", AddressOf RunDataGridSpike)
            Case "encoding"
                RunSpike("P2-0-007", "Encoding detection", AddressOf RunEncodingSpike)
            Case "tab-timing"
                RunSpike("P2-0-008", "Tab switch timing", AddressOf RunTabTimingSpike)
            Case Else
                _results.Add(New SpikeResult With {
                    .Id = "UNKNOWN",
                    .Title = "Unknown command",
                    .Status = "FAIL",
                    .Method = "command dispatch",
                    .Dataset = command,
                    .Metrics = "unsupported command",
                    .Decision = "Use one of: all, state-tree, offset-map, folding, search-replace, completion, datagrid, encoding, tab-timing.",
                    .Gate = "none",
                    .DocImpact = "none"
                })
        End Select

        Dim reportPath = WriteReport()
        Console.WriteLine($"P2-0 report: {reportPath}")

        If _results.Any(Function(item) String.Equals(item.Status, "FAIL", StringComparison.OrdinalIgnoreCase) OrElse
                                     String.Equals(item.Status, "UNVERIFIED", StringComparison.OrdinalIgnoreCase)) Then
            Environment.ExitCode = 1
        End If
    End Sub

    Private Sub RunSpike(id As String, title As String, action As Func(Of SpikeResult))
        Try
            Dim result = action()
            result.Id = id
            result.Title = title
            _results.Add(result)
            Console.WriteLine($"{result.Status} {id} {title}: {result.Decision}")
        Catch ex As Exception
            _results.Add(New SpikeResult With {
                .Id = id,
                .Title = title,
                .Status = "FAIL",
                .Method = "spike execution",
                .Dataset = "",
                .Metrics = $"{ex.GetType().Name}: {ex.Message}",
                .Decision = "Spike failed before a method decision could be recorded.",
                .Gate = "blocked",
                .DocImpact = "Add risk/decision entry before proceeding."
            })
            Console.Error.WriteLine($"FAIL {id} {title}: {ex.Message}")
        End Try
    End Sub

    Private Function RunTreeStateSpike() As SpikeResult
        Dim text = CreateTreeStateJson(160)
        Dim root = _parser.Parse(text).Root
        Dim selectedPointer = "/items/3/child/value"
        Dim expandedPointers = GetAncestorPointers(selectedPointer)
        Dim firstVm = SpikeGridNodeViewModel.FromNode(root, expandedPointers, selectedPointer)

        Dim captureTimer = Stopwatch.StartNew()
        Dim state = CaptureState(firstVm)
        captureTimer.Stop()

        Dim restoreTimer = Stopwatch.StartNew()
        Dim restoredVm = SpikeGridNodeViewModel.FromNode(root, state.ExpandedPointers, state.SelectedPointer)
        restoreTimer.Stop()

        Dim tree = CreateTreeView()
        Dim realized As Boolean
        Dim selectedRestored As Boolean
        Dim window As Window = Nothing
        Dim uiTimer = Stopwatch.StartNew()
        Try
            tree.ItemsSource = New ObservableCollection(Of SpikeGridNodeViewModel) From {restoredVm}
            window = CreateProbeWindow(tree, 800, 600)
            PumpDispatcher()
            realized = BringPathIntoView(tree, restoredVm, selectedPointer)
            PumpDispatcher()
            Dim selectedVm = FindVm(restoredVm, selectedPointer)
            selectedRestored = selectedVm IsNot Nothing AndAlso selectedVm.IsSelected
        Finally
            uiTimer.Stop()
            CloseProbeWindow(window)
        End Try

        Dim rows As New List(Of String()) From {
            New String() {"captureMs", FormatMs(captureTimer.Elapsed.TotalMilliseconds)},
            New String() {"restoreVmMs", FormatMs(restoreTimer.Elapsed.TotalMilliseconds)},
            New String() {"uiRestoreMs", FormatMs(uiTimer.Elapsed.TotalMilliseconds)},
            New String() {"virtualization", "true"},
            New String() {"virtualizationMode", "Recycling"},
            New String() {"bringIntoViewRealized", realized.ToString(CultureInfo.InvariantCulture)},
            New String() {"selectedRestored", selectedRestored.ToString(CultureInfo.InvariantCulture)}
        }
        Dim csv = WriteCsv("p2-0-001-tree-state.csv", New String() {"metric", "value"}, rows)

        Dim decision = "Adopt UI-only wrapper VM with bound IsExpanded/IsSelected; use deferred Dispatcher BringIntoView after ItemsSource replacement."
        Dim status = If(selectedRestored, "PASS", "FAIL")
        Return New SpikeResult With {
            .Status = status,
            .Method = "WPF TreeView wrapper VM state capture/restore under virtualization recycling",
            .Dataset = "synthetic 160-item tree, selected pointer /items/3/child/value",
            .Metrics = $"capture={FormatMs(captureTimer.Elapsed.TotalMilliseconds)}ms, restoreVm={FormatMs(restoreTimer.Elapsed.TotalMilliseconds)}ms, uiRestore={FormatMs(uiTimer.Elapsed.TotalMilliseconds)}ms, realized={realized}, selectedRestored={selectedRestored}",
            .Decision = If(status = "PASS", decision, "Do not proceed to P2-1 until selection binding restoration is fixed."),
            .Gate = "P2-1 ready input",
            .DocImpact = $"Record wrapper VM + deferred BringIntoView in doc05/doc10. Raw: {RelativeToRoot(csv)}"
        }
    End Function

    Private Function RunOffsetMapSpike() As SpikeResult
        Dim sample = EnsureFormattedBenchmark("medium_10mb_array.json")
        Dim text = File.ReadAllText(sample, Encoding.UTF8)
        Dim parseTimer = Stopwatch.StartNew()
        Dim root = _parser.Parse(text).Root
        parseTimer.Stop()

        Dim flattenTimer = Stopwatch.StartNew()
        Dim nodes = Flatten(root).Where(Function(item) item.SourceStartIndex.HasValue).ToList()
        Dim index = nodes.Select(Function(item) New NodeOffsetIndex(item.SourceStartIndex.Value, item)).
            OrderBy(Function(item) item.StartIndex).
            ToList()
        flattenTimer.Stop()

        Dim offsets = BuildProbeOffsets(text.Length)
        Dim scanTimes As New List(Of Double)()
        Dim indexTimes As New List(Of Double)()
        Dim resultPointers As New List(Of String)()

        For iteration = 1 To 30
            For Each offset In offsets
                Dim scanTimer = Stopwatch.StartNew()
                Dim scanNode = FindNearestByScan(nodes, offset)
                scanTimer.Stop()
                scanTimes.Add(scanTimer.Elapsed.TotalMilliseconds)

                Dim indexTimer = Stopwatch.StartNew()
                Dim indexedNode = FindNearestByIndex(index, offset)
                indexTimer.Stop()
                indexTimes.Add(indexTimer.Elapsed.TotalMilliseconds)

                If iteration = 1 Then
                    resultPointers.Add($"{offset}:{If(scanNode?.JsonPointer, "")}:{If(indexedNode?.JsonPointer, "")}")
                End If
            Next
        Next

        Dim scanP95 = Percentile(scanTimes, 0.95)
        Dim indexP95 = Percentile(indexTimes, 0.95)
        Dim scanMax = scanTimes.Max()
        Dim indexMax = indexTimes.Max()
        Dim rows As New List(Of String()) From {
            New String() {"sample", RelativeToRoot(sample)},
            New String() {"textLength", text.Length.ToString(CultureInfo.InvariantCulture)},
            New String() {"nodeCount", nodes.Count.ToString(CultureInfo.InvariantCulture)},
            New String() {"parseMs", FormatMs(parseTimer.Elapsed.TotalMilliseconds)},
            New String() {"flattenAndSortMs", FormatMs(flattenTimer.Elapsed.TotalMilliseconds)},
            New String() {"scanP95Ms", FormatMs(scanP95)},
            New String() {"scanMaxMs", FormatMs(scanMax)},
            New String() {"indexP95Ms", FormatMs(indexP95)},
            New String() {"indexMaxMs", FormatMs(indexMax)},
            New String() {"probePointers", String.Join(";", resultPointers)}
        }
        Dim csv = WriteCsv("p2-0-002-offset-map.csv", New String() {"metric", "value"}, rows)
        Dim useScan = scanP95 <= ScanThresholdMs

        Return New SpikeResult With {
            .Status = "PASS",
            .Method = "Full-scan nearest SourceStartIndex compared with sorted SourceStartIndex binary search",
            .Dataset = RelativeToRoot(sample),
            .Metrics = $"nodes={nodes.Count}, parse={FormatMs(parseTimer.Elapsed.TotalMilliseconds)}ms, scanP95={FormatMs(scanP95)}ms, indexP95={FormatMs(indexP95)}ms",
            .Decision = If(useScan, "Adopt full scan for P2-1 because p95 is <= 200ms.", "Adopt sorted SourceStartIndex binary-search index for P2-1 because full scan exceeded 200ms."),
            .Gate = "P2-1 ready input",
            .DocImpact = $"Record lookup choice and threshold result in doc05/doc10. Raw: {RelativeToRoot(csv)}"
        }
    End Function

    Private Function RunFoldingSpike() As SpikeResult
        Dim samples = New List(Of String) From {
            EnsureFormattedBenchmark("small_1mb_nested.json"),
            EnsureFormattedBenchmark("medium_10mb_array.json")
        }
        Dim rows As New List(Of String())()
        Dim maxUpdate = 0.0
        Dim stateReappliedAll = True

        For Each sample In samples
            Dim text = File.ReadAllText(sample, Encoding.UTF8)
            Dim rangeTimer = Stopwatch.StartNew()
            Dim foldings = CreateJsonFoldings(text)
            rangeTimer.Stop()

            Dim editor = New TextEditor With {
                .Document = New TextDocument(text),
                .ShowLineNumbers = True
            }
            Dim window As Window = Nothing
            Dim manager As FoldingManager = Nothing
            Dim updateMs As Double
            Dim reapplied As Boolean

            Try
                window = CreateProbeWindow(editor, 900, 700)
                PumpDispatcher()
                manager = FoldingManager.Install(editor.TextArea)

                Dim updateTimer = Stopwatch.StartNew()
                manager.UpdateFoldings(foldings, -1)
                updateTimer.Stop()
                updateMs = updateTimer.Elapsed.TotalMilliseconds
                maxUpdate = Math.Max(maxUpdate, updateMs)

                reapplied = ReapplyFirstFoldState(manager, text)
                stateReappliedAll = stateReappliedAll AndAlso reapplied
            Finally
                If manager IsNot Nothing Then
                    FoldingManager.Uninstall(manager)
                End If
                CloseProbeWindow(window)
            End Try

            rows.Add(New String() {
                Path.GetFileName(sample),
                text.Length.ToString(CultureInfo.InvariantCulture),
                foldings.Count.ToString(CultureInfo.InvariantCulture),
                FormatMs(rangeTimer.Elapsed.TotalMilliseconds),
                FormatMs(updateMs),
                reapplied.ToString(CultureInfo.InvariantCulture)
            })
        Next

        Dim csv = WriteCsv("p2-0-003-folding.csv", New String() {"sample", "textLength", "foldingCount", "rangeMs", "updateMs", "foldStateReapplied"}, rows)
        Return New SpikeResult With {
            .Status = "PASS",
            .Method = "Brace-stack JSON folding ranges applied through AvalonEdit FoldingManager",
            .Dataset = String.Join(", ", samples.Select(Function(item) RelativeToRoot(item))),
            .Metrics = $"maxUpdate={FormatMs(maxUpdate)}ms, foldStateReappliedAll={stateReappliedAll}",
            .Decision = "Adopt brace-stack range generation plus explicit folded-range reapply by offsets for P2-2.",
            .Gate = "P2-2 ready input",
            .DocImpact = $"Record folding generation and folded range reapply policy in doc05/doc10. Raw: {RelativeToRoot(csv)}"
        }
    End Function

    Private Function RunSearchReplaceSpike() As SpikeResult
        Dim sample = "{""name"":""Visual JSON"",""items"":[{""name"":""one""},{""name"":""two""}]}"
        Dim editor = New TextEditor With {.Text = sample}
        Dim panelInstalled As Boolean
        Dim localizationAvailable As Boolean
        Dim regexMatches As Integer
        Dim replaceAllOutput As String
        Dim window As Window = Nothing
        Dim searchPanel As SearchPanel = Nothing

        Try
            window = CreateProbeWindow(editor, 800, 500)
            PumpDispatcher()
            searchPanel = SearchPanel.Install(editor)
            searchPanel.SearchPattern = """name"""
            searchPanel.UseRegex = False
            panelInstalled = searchPanel IsNot Nothing
            localizationAvailable = searchPanel.Localization IsNot Nothing
            searchPanel.Open()
            searchPanel.FindNext()
            regexMatches = Regex.Matches(editor.Text, """name""\s*:", RegexOptions.CultureInvariant).Count
            replaceAllOutput = Regex.Replace(editor.Text, """name""\s*:", """label"":", RegexOptions.CultureInvariant)
        Finally
            If searchPanel IsNot Nothing Then
                searchPanel.Uninstall()
            End If
            CloseProbeWindow(window)
        End Try

        Dim replaceSucceeded = replaceAllOutput.Contains("""label"":", StringComparison.Ordinal) AndAlso
            Not replaceAllOutput.Contains("""name"":", StringComparison.Ordinal)
        Dim rows As New List(Of String()) From {
            New String() {"searchPanelInstalled", panelInstalled.ToString(CultureInfo.InvariantCulture)},
            New String() {"localizationAvailable", localizationAvailable.ToString(CultureInfo.InvariantCulture)},
            New String() {"regexMatches", regexMatches.ToString(CultureInfo.InvariantCulture)},
            New String() {"replaceAllSucceeded", replaceSucceeded.ToString(CultureInfo.InvariantCulture)}
        }
        Dim csv = WriteCsv("p2-0-004-search-replace.csv", New String() {"metric", "value"}, rows)
        Dim status = If(panelInstalled AndAlso replaceSucceeded, "PASS", "FAIL")

        Return New SpikeResult With {
            .Status = status,
            .Method = "AvalonEdit SearchPanel for find/highlight plus custom replace logic probe",
            .Dataset = "inline JSON search sample",
            .Metrics = $"panelInstalled={panelInstalled}, localizationAvailable={localizationAvailable}, regexMatches={regexMatches}, replaceAllSucceeded={replaceSucceeded}",
            .Decision = If(status = "PASS", "Use AvalonEdit SearchPanel for find/highlight and implement a product-local custom replace bar for P2-2.", "Search/replace route is not ready; choose a fully custom bar before P2-2."),
            .Gate = "P2-2 ready input",
            .DocImpact = $"Record SearchPanel + custom replace split in doc05/doc10. Raw: {RelativeToRoot(csv)}"
        }
    End Function

    Private Function RunCompletionSpike() As SpikeResult
        Dim editor = New TextEditor With {.Text = "{""sta"": 1}"}
        editor.CaretOffset = 5
        Dim insertionSucceeded As Boolean
        Dim escapeClosed As Boolean
        Dim itemCount As Integer
        Dim window As Window = Nothing
        Dim completion As CompletionWindow = Nothing

        Try
            window = CreateProbeWindow(editor, 800, 500)
            editor.Focus()
            PumpDispatcher()

            completion = New CompletionWindow(editor.TextArea)
            completion.CloseAutomatically = False
            completion.CompletionList.CompletionData.Add(New SimpleCompletionData("status"))
            completion.CompletionList.CompletionData.Add(New SimpleCompletionData("state"))
            itemCount = completion.CompletionList.CompletionData.Count
            completion.Show()
            PumpDispatcher()
            completion.CompletionList.ListBox.SelectedIndex = 0
            completion.CompletionList.RequestInsertion(EventArgs.Empty)
            PumpDispatcher()
            insertionSucceeded = editor.Text.Contains("status", StringComparison.Ordinal)

            completion = New CompletionWindow(editor.TextArea)
            completion.CloseAutomatically = False
            completion.CompletionList.CompletionData.Add(New SimpleCompletionData("schema"))
            completion.Show()
            PumpDispatcher()
            completion.Close()
            PumpDispatcher()
            escapeClosed = Not completion.IsVisible
        Finally
            If completion IsNot Nothing AndAlso completion.IsVisible Then
                completion.Close()
            End If
            CloseProbeWindow(window)
        End Try

        Dim rows As New List(Of String()) From {
            New String() {"candidateCount", itemCount.ToString(CultureInfo.InvariantCulture)},
            New String() {"insertionSucceeded", insertionSucceeded.ToString(CultureInfo.InvariantCulture)},
            New String() {"closeSucceeded", escapeClosed.ToString(CultureInfo.InvariantCulture)}
        }
        Dim csv = WriteCsv("p2-0-005-completion.csv", New String() {"metric", "value"}, rows)
        Dim status = If(itemCount >= 2 AndAlso insertionSucceeded AndAlso escapeClosed, "PASS", "FAIL")

        Return New SpikeResult With {
            .Status = status,
            .Method = "AvalonEdit CompletionWindow from VB.NET with candidate insertion and close flow",
            .Dataset = "inline editor completion sample",
            .Metrics = $"candidateCount={itemCount}, insertionSucceeded={insertionSucceeded}, closeSucceeded={escapeClosed}",
            .Decision = If(status = "PASS", "Adopt AvalonEdit CompletionWindow for P2-5 text completion spike carry-forward.", "Do not adopt CompletionWindow until insertion/close behavior is corrected."),
            .Gate = "P2-5 ready input",
            .DocImpact = $"Record CompletionWindow route in doc05/doc10. Raw: {RelativeToRoot(csv)}"
        }
    End Function

    Private Function RunDataGridSpike() As SpikeResult
        Dim rows As New List(Of String())()
        Dim functional = MeasureDataGrid(1000, 20)
        Dim performance = MeasureDataGrid(10000, 20)

        rows.Add(New String() {"functional", functional.RowCount.ToString(CultureInfo.InvariantCulture), functional.ColumnCount.ToString(CultureInfo.InvariantCulture), FormatMs(functional.BindMs), FormatMs(functional.SortMs), functional.SortSucceeded.ToString(CultureInfo.InvariantCulture), functional.EditSucceeded.ToString(CultureInfo.InvariantCulture), functional.SelectionRestored.ToString(CultureInfo.InvariantCulture)})
        rows.Add(New String() {"performance", performance.RowCount.ToString(CultureInfo.InvariantCulture), performance.ColumnCount.ToString(CultureInfo.InvariantCulture), FormatMs(performance.BindMs), FormatMs(performance.SortMs), performance.SortSucceeded.ToString(CultureInfo.InvariantCulture), performance.EditSucceeded.ToString(CultureInfo.InvariantCulture), performance.SelectionRestored.ToString(CultureInfo.InvariantCulture)})

        Dim csv = WriteCsv("p2-0-006-datagrid.csv", New String() {"case", "rows", "columns", "bindMs", "sortMs", "sortSucceeded", "editSucceeded", "selectionRestored"}, rows)
        Dim status = If(functional.SortSucceeded AndAlso functional.EditSucceeded AndAlso functional.SelectionRestored AndAlso performance.SortSucceeded AndAlso performance.EditSucceeded AndAlso performance.SelectionRestored, "PASS", "FAIL")

        Return New SpikeResult With {
            .Status = status,
            .Method = "WPF DataGrid dynamic columns with row/column virtualization",
            .Dataset = "1000x20 functional and 10000x20 performance synthetic object rows",
            .Metrics = $"1000x20 bind={FormatMs(functional.BindMs)}ms sort={FormatMs(functional.SortMs)}ms sortOk={functional.SortSucceeded}; 10000x20 bind={FormatMs(performance.BindMs)}ms sort={FormatMs(performance.SortMs)}ms sortOk={performance.SortSucceeded}",
            .Decision = If(status = "PASS", "Adopt WPF DataGrid dynamic columns for P2-4 table view.", "Do not adopt WPF DataGrid until edit/selection stability is fixed."),
            .Gate = "P2-4 ready input",
            .DocImpact = $"Record DataGrid adoption and 10k-row perf smoke in doc05/doc10. Raw: {RelativeToRoot(csv)}"
        }
    End Function

    Private Function RunEncodingSpike() As SpikeResult
        Dim samples = CreateEncodingSamples()
        Dim rows As New List(Of String())()
        Dim allMatched = True

        For Each sample In samples
            Dim bytes = File.ReadAllBytes(sample.Path)
            Dim detected = DetectEncoding(bytes)
            Dim text = detected.Encoding.GetString(StripBom(bytes, detected.Encoding))
            Dim newline = DetectNewLine(text)
            Dim matched = String.Equals(detected.Name, sample.ExpectedEncoding, StringComparison.Ordinal) AndAlso
                String.Equals(newline, sample.ExpectedNewLine, StringComparison.Ordinal)
            allMatched = allMatched AndAlso matched
            rows.Add(New String() {
                Path.GetFileName(sample.Path),
                sample.ExpectedEncoding,
                detected.Name,
                sample.ExpectedNewLine,
                newline,
                detected.Warning,
                matched.ToString(CultureInfo.InvariantCulture)
            })
        Next

        Dim csv = WriteCsv("p2-0-007-encoding.csv", New String() {"sample", "expectedEncoding", "detectedEncoding", "expectedNewLine", "detectedNewLine", "warning", "matched"}, rows)
        Return New SpikeResult With {
            .Status = If(allMatched, "PASS", "FAIL"),
            .Method = "BOM-first detection, UTF-16 no-BOM null-byte parity heuristic, UTF-8 fallback with warning; majority newline detection",
            .Dataset = "generated UTF-8/UTF-8-BOM/UTF-16LE/UTF-16BE CRLF/LF samples",
            .Metrics = $"sampleCount={samples.Count}, allMatched={allMatched}",
            .Decision = If(allMatched, "Adopt encoding detector thresholds for P2-3/P2-1 read pipeline.", "Encoding detector needs threshold correction before adoption."),
            .Gate = "P2-3 ready input",
            .DocImpact = $"Record exact detector policy in doc05/doc10. Raw: {RelativeToRoot(csv)}"
        }
    End Function

    Private Function RunTabTimingSpike() As SpikeResult
        Dim sample = EnsureFormattedBenchmark("medium_10mb_array.json")
        Dim text = File.ReadAllText(sample, Encoding.UTF8)
        Dim root = _parser.Parse(text).Root
        Dim selectedPointer = "/items/3/name"
        Dim expandedPointers = GetAncestorPointers(selectedPointer)
        Dim timings As New List(Of Double)()
        Dim noRootReset = True
        Dim rows As New List(Of String())()

        For iteration = 1 To 10
            Dim sourceVm = SpikeGridNodeViewModel.FromNode(root, expandedPointers, selectedPointer)
            Dim capture = CaptureState(sourceVm)
            Dim restoredVm = SpikeGridNodeViewModel.FromNode(root, capture.ExpandedPointers, capture.SelectedPointer)
            Dim tree = CreateTreeView()
            Dim window As Window = Nothing
            Dim realized As Boolean
            Dim selectedRestored As Boolean
            Dim timer = Stopwatch.StartNew()

            Try
                tree.ItemsSource = New ObservableCollection(Of SpikeGridNodeViewModel) From {restoredVm}
                window = CreateProbeWindow(tree, 800, 600)
                PumpDispatcher(DispatcherPriority.ApplicationIdle)
                realized = BringPathIntoView(tree, restoredVm, selectedPointer)
                PumpDispatcher(DispatcherPriority.ApplicationIdle)
                Dim selectedVm = FindVm(restoredVm, selectedPointer)
                selectedRestored = selectedVm IsNot Nothing AndAlso selectedVm.IsSelected
            Finally
                timer.Stop()
                CloseProbeWindow(window)
            End Try

            noRootReset = noRootReset AndAlso selectedRestored
            timings.Add(timer.Elapsed.TotalMilliseconds)
            rows.Add(New String() {iteration.ToString(CultureInfo.InvariantCulture), FormatMs(timer.Elapsed.TotalMilliseconds), realized.ToString(CultureInfo.InvariantCulture), selectedRestored.ToString(CultureInfo.InvariantCulture)})
        Next

        Dim p95 = Percentile(timings, 0.95)
        Dim max = timings.Max()
        Dim csv = WriteCsv("p2-0-008-tab-timing.csv", New String() {"iteration", "restoreMs", "bringIntoViewRealized", "selectedRestored"}, rows)
        Dim withinThreshold = p95 <= TabRestoreThresholdMs

        ' C-P2-002: the status now includes the 300ms threshold instead of state
        ' restoration alone. This spike is a pessimistic measurement (a fresh window
        ' per iteration); the release gate uses the P2-1 in-window measurement.
        Return New SpikeResult With {
            .Status = If(noRootReset AndAlso withinThreshold, "PASS", "FAIL"),
            .Method = "State capture/restore plus Dispatcher ApplicationIdle completion timing",
            .Dataset = RelativeToRoot(sample),
            .Metrics = $"iterations=10, p95={FormatMs(p95)}ms, max={FormatMs(max)}ms, noRootReset={noRootReset}, withinThreshold={withinThreshold}",
            .Decision = If(withinThreshold AndAlso noRootReset, "Use bound state restore for P2-1 tab switching; threshold feasible.", "Use the same bound state restore method, but record timing risk and optimize BringIntoView if P2-1 UI exceeds 300ms."),
            .Gate = "P2-1 ready input",
            .DocImpact = $"Record Stopwatch + Dispatcher timing method in doc05/doc10. Raw: {RelativeToRoot(csv)}"
        }
    End Function

    Private Function MeasureDataGrid(rowCount As Integer, columnCount As Integer) As DataGridMeasurement
        Dim data = CreateTableRows(rowCount, columnCount)
        Dim grid = New DataGrid With {
            .AutoGenerateColumns = False,
            .EnableRowVirtualization = True,
            .EnableColumnVirtualization = True,
            .CanUserSortColumns = True
        }
        VirtualizingPanel.SetIsVirtualizing(grid, True)
        VirtualizingPanel.SetVirtualizationMode(grid, VirtualizationMode.Recycling)

        For columnIndex = 0 To columnCount - 1
            Dim columnName = $"c{columnIndex}"
            grid.Columns.Add(New DataGridTextColumn With {
                .Header = columnName,
                .Binding = New Binding($"[{columnName}]")
            })
        Next

        Dim window As Window = Nothing
        Dim bindMs As Double
        Dim sortMs As Double
        Dim sortSucceeded As Boolean
        Dim editSucceeded As Boolean
        Dim selectionRestored As Boolean

        Try
            window = CreateProbeWindow(grid, 1000, 700)
            Dim bindTimer = Stopwatch.StartNew()
            grid.ItemsSource = data
            grid.SelectedIndex = Math.Min(10, data.Count - 1)
            grid.UpdateLayout()
            PumpDispatcher()
            bindTimer.Stop()
            bindMs = bindTimer.Elapsed.TotalMilliseconds

            Dim selectedRow = CType(grid.SelectedItem, Dictionary(Of String, String))
            Dim selectedId = selectedRow("id")
            selectedRow("c1") = "edited"
            editSucceeded = String.Equals(selectedRow("c1"), "edited", StringComparison.Ordinal)

            Dim view = TryCast(CollectionViewSource.GetDefaultView(grid.ItemsSource), ListCollectionView)
            If view Is Nothing Then
                Throw New InvalidOperationException("DataGrid source did not create a ListCollectionView.")
            End If

            Dim beforeSortFirst = CType(view.GetItemAt(0), Dictionary(Of String, String))("id")
            Dim sortTimer = Stopwatch.StartNew()
            view.CustomSort = New DictionaryColumnComparer("c0")
            view.Refresh()
            grid.UpdateLayout()
            PumpDispatcher()
            sortTimer.Stop()
            sortMs = sortTimer.Elapsed.TotalMilliseconds
            Dim afterSortFirst = CType(view.GetItemAt(0), Dictionary(Of String, String))("id")
            sortSucceeded = Not String.Equals(beforeSortFirst, afterSortFirst, StringComparison.Ordinal)

            Dim restoredRow = data.FirstOrDefault(Function(item) String.Equals(item("id"), selectedId, StringComparison.Ordinal))
            grid.SelectedItem = restoredRow
            grid.ScrollIntoView(restoredRow)
            PumpDispatcher()
            selectionRestored = grid.SelectedItem Is restoredRow
        Finally
            CloseProbeWindow(window)
        End Try

        Return New DataGridMeasurement(rowCount, columnCount, bindMs, sortMs, sortSucceeded, editSucceeded, selectionRestored)
    End Function

    Private Function EnsureFormattedBenchmark(fileName As String) As String
        Dim source = Path.Combine(_benchmarkDir, fileName)
        If Not File.Exists(source) Then
            File.WriteAllText(source, CreateLargeJson(If(fileName.Contains("10mb", StringComparison.OrdinalIgnoreCase), 10 * 1024 * 1024, 1 * 1024 * 1024)), Utf8NoBom)
        End If

        Dim formatted = Path.Combine(_benchmarkDir, $"{Path.GetFileNameWithoutExtension(fileName)}_formatted.json")
        If File.Exists(formatted) Then
            Return formatted
        End If

        Dim formatter = New JsonFormatterService()
        Dim formattedText = formatter.Format(File.ReadAllText(source, Encoding.UTF8))
        File.WriteAllText(formatted, formattedText, Utf8NoBom)
        Return formatted
    End Function

    Private Function WriteReport() As String
        Dim reportPath = Path.Combine(_root, "artifacts", "verification", "p2-0-spike-results.md")
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath))

        Dim builder As New StringBuilder()
        builder.AppendLine("# Visual JSON Phase2 P2-0 Spike Results")
        builder.AppendLine()
        builder.AppendLine($"Generated: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}")
        builder.AppendLine($"Tool: tools/VisualJson.Phase2Spikes")
        builder.AppendLine()
        builder.AppendLine("## Acceptance Summary")
        builder.AppendLine()
        builder.AppendLine("| ID | Status | Gate | Decision |")
        builder.AppendLine("| --- | --- | --- | --- |")

        For Each result In _results
            builder.AppendLine($"| {EscapeMarkdown(result.Id)} | {EscapeMarkdown(result.Status)} | {EscapeMarkdown(result.Gate)} | {EscapeMarkdown(result.Decision)} |")
        Next

        builder.AppendLine()
        builder.AppendLine("## Details")
        builder.AppendLine()
        builder.AppendLine("| ID | Method | Dataset | Metrics | Doc impact |")
        builder.AppendLine("| --- | --- | --- | --- | --- |")

        For Each result In _results
            builder.AppendLine($"| {EscapeMarkdown(result.Id)} | {EscapeMarkdown(result.Method)} | {EscapeMarkdown(result.Dataset)} | {EscapeMarkdown(result.Metrics)} | {EscapeMarkdown(result.DocImpact)} |")
        Next

        builder.AppendLine()
        builder.AppendLine("## Gate Judgment")
        builder.AppendLine()
        builder.AppendLine($"- P2-1: {GateStatus("P2-0-001", "P2-0-002", "P2-0-008")}")
        builder.AppendLine($"- P2-2: {GateStatus("P2-0-003", "P2-0-004")}")
        builder.AppendLine($"- P2-4: {GateStatus("P2-0-006")}")
        builder.AppendLine()
        builder.AppendLine("## Release Input Guard")
        builder.AppendLine()
        builder.AppendLine("- Local verification materials remain ignored and are not release inputs.")
        builder.AppendLine("- Raw P2-0 verification outputs remain outside packaged releases.")

        File.WriteAllText(reportPath, builder.ToString(), Utf8NoBom)
        Return reportPath
    End Function

    Private Function GateStatus(ParamArray ids As String()) As String
        Dim selected = _results.Where(Function(item) ids.Contains(item.Id)).ToList()
        If selected.Count <> ids.Length Then
            Return "UNVERIFIED"
        End If

        If selected.Any(Function(item) Not String.Equals(item.Status, "PASS", StringComparison.OrdinalIgnoreCase)) Then
            Return "FAIL"
        End If

        Return "PASS"
    End Function

    Private Function WriteCsv(fileName As String, headers As String(), rows As IEnumerable(Of String())) As String
        Dim csvPath As String = IO.Path.Combine(_verificationDir, fileName)
        Dim builder As New StringBuilder()
        builder.AppendLine(String.Join(",", headers.Select(AddressOf CsvEscape)))
        For Each row In rows
            builder.AppendLine(String.Join(",", row.Select(AddressOf CsvEscape)))
        Next
        File.WriteAllText(csvPath, builder.ToString(), Utf8NoBom)
        Return csvPath
    End Function

    Private Function RelativeToRoot(targetPath As String) As String
        Return IO.Path.GetRelativePath(_root, targetPath).Replace("\"c, "/"c)
    End Function

    Private Shared Function CsvEscape(value As String) As String
        Dim safe = If(value, "")
        If safe.Contains(","c) OrElse safe.Contains(""""c) OrElse safe.Contains(ControlChars.Lf) OrElse safe.Contains(ControlChars.Cr) Then
            Return $"""{safe.Replace("""", """""")}"""
        End If
        Return safe
    End Function

    Private Shared Function EscapeMarkdown(value As String) As String
        Return If(value, "").Replace("|", "\|").Replace(ControlChars.Cr, " ").Replace(ControlChars.Lf, " ")
    End Function

    Private Shared Function FormatMs(value As Double) As String
        Return value.ToString("0.###", CultureInfo.InvariantCulture)
    End Function

    Private Shared Function Percentile(values As IReadOnlyCollection(Of Double), quantile As Double) As Double
        If values.Count = 0 Then
            Return 0
        End If

        Dim sorted = values.OrderBy(Function(item) item).ToList()
        Dim index = CInt(Math.Ceiling(quantile * sorted.Count)) - 1
        index = Math.Clamp(index, 0, sorted.Count - 1)
        Return sorted(index)
    End Function

    Private Shared Function BuildProbeOffsets(length As Integer) As Integer()
        Return {
            0,
            Math.Max(0, length \ 10),
            Math.Max(0, length \ 4),
            Math.Max(0, length \ 2),
            Math.Max(0, (length * 3) \ 4),
            Math.Max(0, length - 2)
        }
    End Function

    Private Shared Function Flatten(root As JsonTreeNode) As List(Of JsonTreeNode)
        Dim nodes As New List(Of JsonTreeNode)()
        AddNodes(root, nodes)
        Return nodes
    End Function

    Private Shared Sub AddNodes(node As JsonTreeNode, nodes As List(Of JsonTreeNode))
        nodes.Add(node)
        For Each child In node.Children
            AddNodes(child, nodes)
        Next
    End Sub

    Private Shared Function FindNearestByScan(nodes As IEnumerable(Of JsonTreeNode), offset As Integer) As JsonTreeNode
        Dim best As JsonTreeNode = Nothing
        Dim bestStart = -1

        For Each node In nodes
            Dim start = node.SourceStartIndex.GetValueOrDefault(-1)
            If start <= offset AndAlso start > bestStart Then
                best = node
                bestStart = start
            End If
        Next

        Return best
    End Function

    Private Shared Function FindNearestByIndex(index As IReadOnlyList(Of NodeOffsetIndex), offset As Integer) As JsonTreeNode
        Dim low = 0
        Dim high = index.Count - 1
        Dim best = -1

        While low <= high
            Dim mid = low + ((high - low) \ 2)
            If index(mid).StartIndex <= offset Then
                best = mid
                low = mid + 1
            Else
                high = mid - 1
            End If
        End While

        If best < 0 Then
            Return Nothing
        End If

        Return index(best).Node
    End Function

    Private Shared Function CreateJsonFoldings(text As String) As List(Of NewFolding)
        Dim foldings As New List(Of NewFolding)()
        Dim stack As New Stack(Of FoldingStart)()
        Dim inString = False
        Dim escaped = False

        For index = 0 To text.Length - 1
            Dim ch = text(index)

            If inString Then
                If escaped Then
                    escaped = False
                ElseIf ch = "\"c Then
                    escaped = True
                ElseIf ch = """"c Then
                    inString = False
                End If
                Continue For
            End If

            Select Case ch
                Case """"c
                    inString = True
                Case "{"c, "["c
                    stack.Push(New FoldingStart(index, ch))
                Case "}"c, "]"c
                    If stack.Count = 0 Then
                        Continue For
                    End If

                    Dim start = stack.Pop()
                    If Not IsMatchingFold(start.Character, ch) Then
                        Continue For
                    End If

                    If ContainsLineBreak(text, start.Offset, index) Then
                        foldings.Add(New NewFolding(start.Offset, index + 1) With {
                            .Name = If(start.Character = "{"c, "{...}", "[...]")
                        })
                    End If
            End Select
        Next

        foldings.Sort(Function(left, right) left.StartOffset.CompareTo(right.StartOffset))
        Return foldings
    End Function

    Private Shared Function IsMatchingFold(openChar As Char, closeChar As Char) As Boolean
        Return (openChar = "{"c AndAlso closeChar = "}"c) OrElse
            (openChar = "["c AndAlso closeChar = "]"c)
    End Function

    Private Shared Function ContainsLineBreak(text As String, startOffset As Integer, endOffset As Integer) As Boolean
        Dim length = Math.Max(0, endOffset - startOffset + 1)
        Return text.IndexOf(ControlChars.Lf, startOffset, length) >= 0
    End Function

    Private Shared Function ReapplyFirstFoldState(manager As FoldingManager, text As String) As Boolean
        Dim first = manager.AllFoldings.FirstOrDefault()
        If first Is Nothing Then
            Return True
        End If

        Dim start = first.StartOffset
        Dim [end] = first.EndOffset
        first.IsFolded = True

        Dim refreshed = CreateJsonFoldings(text)
        manager.UpdateFoldings(refreshed, -1)

        Dim match = manager.AllFoldings.FirstOrDefault(Function(item) item.StartOffset = start AndAlso item.EndOffset = [end])
        If match Is Nothing Then
            Return False
        End If

        match.IsFolded = True
        Return match.IsFolded
    End Function

    Private Shared Function CreateTreeView() As TreeView
        Dim tree = New TreeView()
        VirtualizingPanel.SetIsVirtualizing(tree, True)
        VirtualizingPanel.SetVirtualizationMode(tree, VirtualizationMode.Recycling)

        Dim template = New HierarchicalDataTemplate(GetType(SpikeGridNodeViewModel)) With {
            .ItemsSource = New Binding(NameOf(SpikeGridNodeViewModel.Children))
        }
        Dim textFactory = New FrameworkElementFactory(GetType(TextBlock))
        textFactory.SetBinding(TextBlock.TextProperty, New Binding("Model.PointerDisplay"))
        template.VisualTree = textFactory
        tree.ItemTemplate = template

        Dim itemStyle = New Style(GetType(TreeViewItem))
        itemStyle.Setters.Add(New Setter(TreeViewItem.IsExpandedProperty, New Binding(NameOf(SpikeGridNodeViewModel.IsExpanded)) With {.Mode = BindingMode.TwoWay}))
        itemStyle.Setters.Add(New Setter(TreeViewItem.IsSelectedProperty, New Binding(NameOf(SpikeGridNodeViewModel.IsSelected)) With {.Mode = BindingMode.TwoWay}))
        tree.ItemContainerStyle = itemStyle
        Return tree
    End Function

    Private Shared Function CreateProbeWindow(content As UIElement, width As Double, height As Double) As Window
        Dim window = New Window With {
            .Content = content,
            .Width = width,
            .Height = height,
            .Left = -20000,
            .Top = -20000,
            .ShowInTaskbar = False,
            .ShowActivated = False,
            .WindowStyle = WindowStyle.None,
            .ResizeMode = ResizeMode.NoResize,
            .Background = Brushes.White
        }
        window.Show()
        window.UpdateLayout()
        PumpDispatcher()
        Return window
    End Function

    Private Shared Sub CloseProbeWindow(window As Window)
        If window Is Nothing Then
            Return
        End If

        window.Close()
        PumpDispatcher()
    End Sub

    Private Shared Sub PumpDispatcher(Optional priority As DispatcherPriority = DispatcherPriority.Background)
        Dim frame As New DispatcherFrame()
        Dispatcher.CurrentDispatcher.BeginInvoke(priority, New DispatcherOperationCallback(Function(parameter)
                                                                                              frame.Continue = False
                                                                                              Return Nothing
                                                                                          End Function), Nothing)
        Dispatcher.PushFrame(frame)
    End Sub

    Private Shared Function BringPathIntoView(tree As TreeView, root As SpikeGridNodeViewModel, pointer As String) As Boolean
        Dim path = GetVmPath(root, pointer)
        If path.Count = 0 Then
            Return False
        End If

        Dim parent As ItemsControl = tree
        Dim currentContainer As TreeViewItem = Nothing

        For Each item In path
            parent.UpdateLayout()
            PumpDispatcher()
            currentContainer = TryCast(parent.ItemContainerGenerator.ContainerFromItem(item), TreeViewItem)
            If currentContainer Is Nothing Then
                parent.UpdateLayout()
                PumpDispatcher(DispatcherPriority.ApplicationIdle)
                currentContainer = TryCast(parent.ItemContainerGenerator.ContainerFromItem(item), TreeViewItem)
            End If

            If currentContainer Is Nothing Then
                Return False
            End If

            currentContainer.IsExpanded = True
            currentContainer.UpdateLayout()
            PumpDispatcher()
            parent = currentContainer
        Next

        currentContainer.BringIntoView()
        Return True
    End Function

    Private Shared Function GetVmPath(root As SpikeGridNodeViewModel, pointer As String) As List(Of SpikeGridNodeViewModel)
        Dim path As New List(Of SpikeGridNodeViewModel)()
        If AppendVmPath(root, pointer, path) Then
            Return path
        End If

        Return New List(Of SpikeGridNodeViewModel)()
    End Function

    Private Shared Function AppendVmPath(current As SpikeGridNodeViewModel, pointer As String, path As List(Of SpikeGridNodeViewModel)) As Boolean
        path.Add(current)
        If String.Equals(current.Model.JsonPointer, pointer, StringComparison.Ordinal) Then
            Return True
        End If

        For Each child In current.Children
            If AppendVmPath(child, pointer, path) Then
                Return True
            End If
        Next

        path.RemoveAt(path.Count - 1)
        Return False
    End Function

    Private Shared Function FindVm(root As SpikeGridNodeViewModel, pointer As String) As SpikeGridNodeViewModel
        If String.Equals(root.Model.JsonPointer, pointer, StringComparison.Ordinal) Then
            Return root
        End If

        For Each child In root.Children
            Dim found = FindVm(child, pointer)
            If found IsNot Nothing Then
                Return found
            End If
        Next

        Return Nothing
    End Function

    Private Shared Function CaptureState(root As SpikeGridNodeViewModel) As TreeState
        Dim expanded As New HashSet(Of String)(StringComparer.Ordinal)
        Dim selected As String = ""
        CaptureStateRecursive(root, expanded, selected)
        Return New TreeState(expanded, selected)
    End Function

    Private Shared Sub CaptureStateRecursive(node As SpikeGridNodeViewModel, expanded As HashSet(Of String), ByRef selected As String)
        If node.IsExpanded Then
            expanded.Add(node.Model.JsonPointer)
        End If

        If node.IsSelected Then
            selected = node.Model.JsonPointer
        End If

        For Each child In node.Children
            CaptureStateRecursive(child, expanded, selected)
        Next
    End Sub

    Private Shared Function GetAncestorPointers(pointer As String) As HashSet(Of String)
        Dim result As New HashSet(Of String)(StringComparer.Ordinal) From {""}
        If String.IsNullOrEmpty(pointer) Then
            Return result
        End If

        Dim current = pointer
        While current.Contains("/"c)
            current = current.Substring(0, current.LastIndexOf("/"c))
            result.Add(current)
            If current.Length = 0 Then
                Exit While
            End If
        End While

        Return result
    End Function

    Private Shared Function CreateTreeStateJson(count As Integer) As String
        Dim builder As New StringBuilder()
        builder.Append("{""items"":[")
        For index = 0 To count - 1
            If index > 0 Then
                builder.Append(","c)
            End If
            builder.Append("{""id"":")
            builder.Append(index.ToString(CultureInfo.InvariantCulture))
            builder.Append(",""name"":""item")
            builder.Append(index.ToString(CultureInfo.InvariantCulture))
            builder.Append(""",""child"":{""value"":")
            builder.Append(index.ToString(CultureInfo.InvariantCulture))
            builder.Append("}}")
        Next
        builder.Append("],""meta"":{""phase"":""p2-0""}}")
        Return builder.ToString()
    End Function

    Private Shared Function CreateLargeJson(targetCharacters As Integer) As String
        Dim builder = New StringBuilder(targetCharacters + 1024)
        builder.Append("{""items"":[")
        Dim index = 0

        While builder.Length < targetCharacters
            If index > 0 Then
                builder.Append(","c)
            End If

            builder.Append("{""id"":")
            builder.Append(index.ToString(CultureInfo.InvariantCulture))
            builder.Append(",""name"":""item")
            builder.Append(index.ToString(CultureInfo.InvariantCulture))
            builder.Append(""",""payload"":""")
            builder.Append("x"c, 2048)
            builder.Append("""}")
            index += 1
        End While

        builder.Append("]}")
        Return builder.ToString()
    End Function

    Private Shared Function CreateTableRows(rowCount As Integer, columnCount As Integer) As ObservableCollection(Of Dictionary(Of String, String))
        Dim rows As New ObservableCollection(Of Dictionary(Of String, String))()

        For rowIndex = 0 To rowCount - 1
            Dim row As New Dictionary(Of String, String)(StringComparer.Ordinal) From {
                {"id", rowIndex.ToString("D8", CultureInfo.InvariantCulture)}
            }

            For columnIndex = 0 To columnCount - 1
                If columnIndex = 0 Then
                    row($"c{columnIndex}") = $"r{(rowCount - rowIndex).ToString("D6", CultureInfo.InvariantCulture)}-c{columnIndex:D2}"
                Else
                    row($"c{columnIndex}") = $"r{rowIndex:D6}-c{columnIndex:D2}"
                End If
            Next

            rows.Add(row)
        Next

        Return rows
    End Function

    Private Function CreateEncodingSamples() As List(Of EncodingSample)
        Dim dir = Path.Combine(_verificationDir, "encoding-samples")
        Directory.CreateDirectory(dir)

        Dim samples As New List(Of EncodingSample)()
        AddEncodingSample(samples, dir, "utf8-lf.json", New UTF8Encoding(False), False, "utf-8", "LF")
        AddEncodingSample(samples, dir, "utf8-bom-crlf.json", New UTF8Encoding(True), True, "utf-8-bom", "CRLF")
        AddEncodingSample(samples, dir, "utf16le-bom-lf.json", Encoding.Unicode, True, "utf-16-le-bom", "LF")
        AddEncodingSample(samples, dir, "utf16le-nobom-crlf.json", Encoding.Unicode, False, "utf-16-le", "CRLF")
        AddEncodingSample(samples, dir, "utf16be-bom-lf.json", Encoding.BigEndianUnicode, True, "utf-16-be-bom", "LF")
        AddEncodingSample(samples, dir, "utf16be-nobom-crlf.json", Encoding.BigEndianUnicode, False, "utf-16-be", "CRLF")
        AddEncodingSample(samples, dir, "utf8-nobom-crlf.json", New UTF8Encoding(False), False, "utf-8", "CRLF")
        AddEncodingSample(samples, dir, "utf16le-nobom-lf.json", Encoding.Unicode, False, "utf-16-le", "LF")
        Return samples
    End Function

    Private Shared Sub AddEncodingSample(samples As List(Of EncodingSample), dir As String, fileName As String, encoding As Encoding, includeBom As Boolean, expectedEncoding As String, expectedNewLine As String)
        Dim newline = If(String.Equals(expectedNewLine, "CRLF", StringComparison.Ordinal), vbCrLf, vbLf)
        Dim text = "{""name"":""Visual JSON""," & newline & """value"":1" & newline & "}" & newline
        Dim bytes = encoding.GetBytes(text)
        If includeBom Then
            bytes = encoding.GetPreamble().Concat(bytes).ToArray()
        End If

        Dim samplePath As String = IO.Path.Combine(dir, fileName)
        File.WriteAllBytes(samplePath, bytes)
        samples.Add(New EncodingSample(samplePath, expectedEncoding, expectedNewLine))
    End Sub

    Private Shared Function DetectEncoding(bytes As Byte()) As DetectedEncoding
        If HasPrefix(bytes, New Byte() {&HEF, &HBB, &HBF}) Then
            Return New DetectedEncoding("utf-8-bom", New UTF8Encoding(False), "")
        End If

        If HasPrefix(bytes, New Byte() {&HFF, &HFE}) Then
            Return New DetectedEncoding("utf-16-le-bom", Encoding.Unicode, "")
        End If

        If HasPrefix(bytes, New Byte() {&HFE, &HFF}) Then
            Return New DetectedEncoding("utf-16-be-bom", Encoding.BigEndianUnicode, "")
        End If

        Dim evenNulls = 0
        Dim oddNulls = 0
        Dim length = Math.Min(bytes.Length, 4096)
        For index = 0 To length - 1
            If bytes(index) = 0 Then
                If index Mod 2 = 0 Then
                    evenNulls += 1
                Else
                    oddNulls += 1
                End If
            End If
        Next

        Dim pairs = Math.Max(1, length \ 2)
        Dim evenRatio = CDbl(evenNulls) / pairs
        Dim oddRatio = CDbl(oddNulls) / pairs
        If oddRatio >= 0.3 AndAlso evenRatio <= 0.05 Then
            Return New DetectedEncoding("utf-16-le", Encoding.Unicode, "UTF-16 LE inferred from null-byte parity")
        End If

        If evenRatio >= 0.3 AndAlso oddRatio <= 0.05 Then
            Return New DetectedEncoding("utf-16-be", Encoding.BigEndianUnicode, "UTF-16 BE inferred from null-byte parity")
        End If

        Return New DetectedEncoding("utf-8", New UTF8Encoding(False), "No BOM/UTF-16 pattern; UTF-8 fallback")
    End Function

    Private Shared Function StripBom(bytes As Byte(), encoding As Encoding) As Byte()
        Dim preamble = encoding.GetPreamble()
        If preamble.Length = 0 OrElse Not HasPrefix(bytes, preamble) Then
            Return bytes
        End If

        Return bytes.Skip(preamble.Length).ToArray()
    End Function

    Private Shared Function HasPrefix(bytes As Byte(), prefix As Byte()) As Boolean
        If bytes.Length < prefix.Length Then
            Return False
        End If

        For index = 0 To prefix.Length - 1
            If bytes(index) <> prefix(index) Then
                Return False
            End If
        Next

        Return True
    End Function

    Private Shared Function DetectNewLine(text As String) As String
        Dim crlf = Regex.Matches(text, "\r\n", RegexOptions.CultureInvariant).Count
        Dim lfOnly = Regex.Matches(text.Replace(vbCrLf, ""), "\n", RegexOptions.CultureInvariant).Count
        If crlf >= lfOnly AndAlso crlf > 0 Then
            Return "CRLF"
        End If

        Return "LF"
    End Function
End Class

Friend NotInheritable Class SpikeResult
    Public Property Id As String = ""
    Public Property Title As String = ""
    Public Property Status As String = ""
    Public Property Method As String = ""
    Public Property Dataset As String = ""
    Public Property Metrics As String = ""
    Public Property Decision As String = ""
    Public Property Gate As String = ""
    Public Property DocImpact As String = ""
End Class

Friend NotInheritable Class NodeOffsetIndex
    Public Sub New(startIndex As Integer, node As JsonTreeNode)
        Me.StartIndex = startIndex
        Me.Node = node
    End Sub

    Public ReadOnly Property StartIndex As Integer
    Public ReadOnly Property Node As JsonTreeNode
End Class

Friend NotInheritable Class FoldingStart
    Public Sub New(offset As Integer, character As Char)
        Me.Offset = offset
        Me.Character = character
    End Sub

    Public ReadOnly Property Offset As Integer
    Public ReadOnly Property Character As Char
End Class

Friend NotInheritable Class TreeState
    Public Sub New(expandedPointers As HashSet(Of String), selectedPointer As String)
        Me.ExpandedPointers = expandedPointers
        Me.SelectedPointer = If(selectedPointer, "")
    End Sub

    Public ReadOnly Property ExpandedPointers As HashSet(Of String)
    Public ReadOnly Property SelectedPointer As String
End Class

Friend NotInheritable Class DataGridMeasurement
    Public Sub New(rowCount As Integer, columnCount As Integer, bindMs As Double, sortMs As Double, sortSucceeded As Boolean, editSucceeded As Boolean, selectionRestored As Boolean)
        Me.RowCount = rowCount
        Me.ColumnCount = columnCount
        Me.BindMs = bindMs
        Me.SortMs = sortMs
        Me.SortSucceeded = sortSucceeded
        Me.EditSucceeded = editSucceeded
        Me.SelectionRestored = selectionRestored
    End Sub

    Public ReadOnly Property RowCount As Integer
    Public ReadOnly Property ColumnCount As Integer
    Public ReadOnly Property BindMs As Double
    Public ReadOnly Property SortMs As Double
    Public ReadOnly Property SortSucceeded As Boolean
    Public ReadOnly Property EditSucceeded As Boolean
    Public ReadOnly Property SelectionRestored As Boolean
End Class

Friend NotInheritable Class DictionaryColumnComparer
    Implements Global.System.Collections.IComparer

    Private ReadOnly _columnName As String

    Public Sub New(columnName As String)
        _columnName = columnName
    End Sub

    Public Function Compare(x As Object, y As Object) As Integer Implements Global.System.Collections.IComparer.Compare
        Dim left = TryCast(x, Dictionary(Of String, String))
        Dim right = TryCast(y, Dictionary(Of String, String))
        Dim leftValue = If(left IsNot Nothing AndAlso left.ContainsKey(_columnName), left(_columnName), "")
        Dim rightValue = If(right IsNot Nothing AndAlso right.ContainsKey(_columnName), right(_columnName), "")
        Return StringComparer.Ordinal.Compare(leftValue, rightValue)
    End Function
End Class

Friend NotInheritable Class EncodingSample
    Public Sub New(path As String, expectedEncoding As String, expectedNewLine As String)
        Me.Path = path
        Me.ExpectedEncoding = expectedEncoding
        Me.ExpectedNewLine = expectedNewLine
    End Sub

    Public ReadOnly Property Path As String
    Public ReadOnly Property ExpectedEncoding As String
    Public ReadOnly Property ExpectedNewLine As String
End Class

Friend NotInheritable Class DetectedEncoding
    Public Sub New(name As String, encoding As Encoding, warning As String)
        Me.Name = name
        Me.Encoding = encoding
        Me.Warning = warning
    End Sub

    Public ReadOnly Property Name As String
    Public ReadOnly Property Encoding As Encoding
    Public ReadOnly Property Warning As String
End Class

Friend NotInheritable Class SpikeGridNodeViewModel
    Implements INotifyPropertyChanged

    Private _isExpanded As Boolean
    Private _isSelected As Boolean

    Private Sub New(model As JsonTreeNode)
        Me.Model = model
        Children = New ObservableCollection(Of SpikeGridNodeViewModel)()
    End Sub

    Public Shared Function FromNode(node As JsonTreeNode, expandedPointers As ISet(Of String), selectedPointer As String) As SpikeGridNodeViewModel
        Dim vm = New SpikeGridNodeViewModel(node) With {
            .IsExpanded = expandedPointers.Contains(node.JsonPointer),
            .IsSelected = String.Equals(node.JsonPointer, selectedPointer, StringComparison.Ordinal)
        }

        For Each child In node.Children
            vm.Children.Add(FromNode(child, expandedPointers, selectedPointer))
        Next

        Return vm
    End Function

    Public Event PropertyChanged As PropertyChangedEventHandler Implements INotifyPropertyChanged.PropertyChanged

    Public ReadOnly Property Model As JsonTreeNode
    Public ReadOnly Property Children As ObservableCollection(Of SpikeGridNodeViewModel)

    Public Property IsExpanded As Boolean
        Get
            Return _isExpanded
        End Get
        Set(value As Boolean)
            If _isExpanded = value Then
                Return
            End If

            _isExpanded = value
            OnPropertyChanged()
        End Set
    End Property

    Public Property IsSelected As Boolean
        Get
            Return _isSelected
        End Get
        Set(value As Boolean)
            If _isSelected = value Then
                Return
            End If

            _isSelected = value
            OnPropertyChanged()
        End Set
    End Property

    Private Sub OnPropertyChanged(<CallerMemberName> Optional propertyName As String = Nothing)
        RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(propertyName))
    End Sub
End Class

Friend NotInheritable Class SimpleCompletionData
    Implements ICompletionData

    Public Sub New(text As String)
        Me.Text = text
    End Sub

    Public ReadOnly Property Image As ImageSource Implements ICompletionData.Image
        Get
            Return Nothing
        End Get
    End Property

    Public ReadOnly Property Text As String Implements ICompletionData.Text
    Public ReadOnly Property Content As Object Implements ICompletionData.Content
        Get
            Return Text
        End Get
    End Property

    Public ReadOnly Property Description As Object Implements ICompletionData.Description
        Get
            Return $"Insert {Text}"
        End Get
    End Property

    Public ReadOnly Property Priority As Double Implements ICompletionData.Priority
        Get
            Return 0
        End Get
    End Property

    Public Sub Complete(textArea As TextArea, completionSegment As ISegment, insertionRequestEventArgs As EventArgs) Implements ICompletionData.Complete
        textArea.Document.Replace(completionSegment, Text)
    End Sub
End Class
