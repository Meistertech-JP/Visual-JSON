' SPDX-License-Identifier: MPL-2.0
Imports System.Diagnostics
Imports System.IO
Imports System.Text
Imports VisualJson.Core.Parsing
Imports VisualJson.Core.Services

Module Program
    Private ReadOnly Parser As New JsonParserService()
    Private ReadOnly Stats As New TreeStatisticsService()

    Sub Main()
        Dim root = FindRepoRoot()
        Dim output = Path.Combine(root, "artifacts", "benchmarks")
        Directory.CreateDirectory(output)

        Console.WriteLine("sample,sizeBytes,readMs,parseAndGridMs,nodeCount,workingSetBeforeBytes,workingSetAfterBytes,memoryDeltaBytes,deltaSizeRatio")

        MeasureSample(output, "small_1mb_nested.json", 1 * 1024 * 1024)
        MeasureSample(output, "medium_10mb_array.json", 10 * 1024 * 1024)
        MeasureSample(output, "large_50mb_flat.json", 50 * 1024 * 1024)
        MeasureSample(output, "huge_100mb_mixed.json", 100 * 1024 * 1024)
    End Sub

    Private Sub MeasureSample(output As String, name As String, targetBytes As Integer)
        Dim path = IO.Path.Combine(output, name)
        If Not File.Exists(path) OrElse New FileInfo(path).Length < targetBytes Then
            File.WriteAllText(path, CreateJson(targetBytes), New UTF8Encoding(encoderShouldEmitUTF8Identifier:=False))
        End If

        GC.Collect()
        GC.WaitForPendingFinalizers()
        GC.Collect()

        Dim before = Process.GetCurrentProcess().WorkingSet64
        Dim readTimer = Stopwatch.StartNew()
        Dim text = File.ReadAllText(path, Encoding.UTF8)
        readTimer.Stop()

        Dim parseTimer = Stopwatch.StartNew()
        Dim root = Parser.Parse(text).Root
        Dim nodeCount = Stats.CountNodes(root)
        parseTimer.Stop()

        Dim after = Process.GetCurrentProcess().WorkingSet64
        Dim delta = Math.Max(0, after - before)
        Dim ratio = If(text.Length = 0, 0, CDbl(delta) / CDbl(text.Length))

        Console.WriteLine($"{name},{text.Length},{readTimer.ElapsedMilliseconds},{parseTimer.ElapsedMilliseconds},{nodeCount},{before},{after},{delta},{ratio:F2}")
    End Sub

    Private Function CreateJson(targetCharacters As Integer) As String
        Dim builder = New StringBuilder(targetCharacters + 1024)
        builder.Append("{""items"":[")
        Dim index = 0

        While builder.Length < targetCharacters
            If index > 0 Then
                builder.Append(","c)
            End If

            builder.Append("{""id"":")
            builder.Append(index.ToString(Globalization.CultureInfo.InvariantCulture))
            builder.Append(",""name"":""item")
            builder.Append(index.ToString(Globalization.CultureInfo.InvariantCulture))
            builder.Append(""",""enabled"":")
            builder.Append(If(index Mod 2 = 0, "true", "false"))
            builder.Append(",""payload"":""")
            builder.Append("x"c, 2048)
            builder.Append("""}")
            index += 1
        End While

        builder.Append("]}")
        Return builder.ToString()
    End Function

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
