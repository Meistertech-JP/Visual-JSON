' SPDX-License-Identifier: MPL-2.0
Imports System.IO
Imports System.Diagnostics
Imports System.Text
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports VisualJson.Core.Conversion
Imports VisualJson.Core.Infrastructure
Imports VisualJson.Core.Models
Imports VisualJson.Core.Parsing
Imports VisualJson.Core.Serialization
Imports VisualJson.Core.Services
Imports VisualJson.Core.Validation

<TestClass>
Public Class FileSafetyTests

    <TestMethod(DisplayName:="Save creates backup and keeps valid JSON")>
    Public Sub SaveCreatesBackupAndKeepsValidJson()
        Dim tempRoot = CreateTempDirectory()
        Try
            Dim target = Path.Combine(tempRoot, "sample.json")
            File.WriteAllText(target, "{""old"":true}")

            Dim saver = New FileSaveService()
            Dim result = saver.Save(target, "{""old"":false,""items"":[1,2]}")

            AssertTrue(File.Exists(result.Path), "saved file exists")
            AssertTrue(File.Exists(result.BackupPath), "backup file exists")
            AssertContains(File.ReadAllText(target), """old"": false", "saved body")
            AssertContains(File.ReadAllText(result.BackupPath), """old"":true", "backup body")
        Finally
            Directory.Delete(tempRoot, recursive:=True)
        End Try
    End Sub

    <TestMethod(DisplayName:="Invalid save preserves existing file")>
    Public Sub InvalidSavePreservesExistingFile()
        Dim tempRoot = CreateTempDirectory()
        Try
            Dim target = Path.Combine(tempRoot, "sample.json")
            File.WriteAllText(target, "{""old"":true}")

            Dim saver = New FileSaveService()
            Try
                saver.Save(target, "{""broken"":")
                Throw New InvalidOperationException("invalid save unexpectedly succeeded")
            Catch ex As System.Text.Json.JsonException
                ' Expected: strict validation rejects the save; the assert below checks the file is untouched.
            End Try

            AssertEqual("{""old"":true}", File.ReadAllText(target), "existing file after invalid save")
        Finally
            Directory.Delete(tempRoot, recursive:=True)
        End Try
    End Sub

    <TestMethod(DisplayName:="Recovery snapshots can be listed and loaded")>
    Public Sub RecoverySnapshotsCanBeListedAndLoaded()
        Dim tempRoot = CreateTempDirectory()
        Try
            Dim recovery = New RecoveryService(tempRoot)
            Dim candidate = recovery.CreateSnapshot("sample.json", "{""draft"":true}")
            Dim candidates = recovery.ListCandidates()

            AssertEqual(1, candidates.Count, "candidate count")
            AssertEqual(candidate.DisplayName, candidates(0).DisplayName, "candidate name")
            AssertEqual("{""draft"":true}", recovery.Load(candidates(0)), "candidate content")

            recovery.Delete(candidates(0))
            AssertEqual(0, recovery.ListCandidates().Count, "candidate deleted")
        Finally
            Directory.Delete(tempRoot, recursive:=True)
        End Try
    End Sub

    <TestMethod(DisplayName:="Diagnostics report omits JSON body")>
    Public Sub DiagnosticsReportOmitsJsonBody()
        Dim reportService = New DiagnosticsReportService()
        Dim validator = New SyntaxValidationService()
        Dim diagnostics = validator.Validate("{""secret"":")
        Dim report = reportService.CreateReport("test", "secret.json", 10, diagnostics, Nothing, 0, "WPF")

        AssertContains(report, "JsonBodyIncluded: no", "body flag")
        AssertFalse(report.Contains("""secret"":", StringComparison.Ordinal), "body content should not be included")
        AssertContains(report, "UIStack: WPF", "ui stack")
        AssertContains(report, "ProcessMemoryBytes:", "memory")
    End Sub

    <TestMethod(DisplayName:="Diagnostics report omits diagnostic and exception messages")>
    Public Sub DiagnosticsReportOmitsMessages()
        Dim reportService = New DiagnosticsReportService()
        Dim validator = New SyntaxValidationService()
        Dim diagnostics = validator.Validate("{""topsecretvalue"":")
        Dim exception = New InvalidOperationException("contains topsecretvalue body fragment")
        Dim report = reportService.CreateReport("test", "secret.json", 10, diagnostics, exception, 0, "WPF")

        AssertFalse(report.Contains("topsecretvalue", StringComparison.OrdinalIgnoreCase), "no body fragments via messages")
        AssertContains(report, "LastExceptionType: System.InvalidOperationException", "exception type retained")
        AssertContains(report, "FirstDiagnostic: Error SYN-PARSE", "diagnostic code retained without message")
        AssertContains(report, "JsonBodyIncluded: no", "body flag")
    End Sub

    <TestMethod(DisplayName:="Text export creates backup and preserves target on failure")>
    Public Sub TextExportCreatesBackupAndPreservesTarget()
        Dim tempRoot = CreateTempDirectory()
        Try
            Dim export = New TextExportService()
            Dim target = Path.Combine(tempRoot, "out.yaml")
            File.WriteAllText(target, "original: true")

            Dim result = export.Save(target, "updated: true")
            AssertEqual("updated: true", File.ReadAllText(target), "export replaced target")
            AssertTrue(File.Exists(result.BackupPath), "export backup exists")
            AssertEqual("original: true", File.ReadAllText(result.BackupPath), "backup preserves original")
        Finally
            Directory.Delete(tempRoot, recursive:=True)
        End Try
    End Sub

    <TestMethod(DisplayName:="File log omits exception messages and body")>
    Public Sub P2FileLogOmitsBody()
        Dim tempRoot = CreateTempDirectory()
        Try
            Dim log = New FileLogService(tempRoot)
            Dim path = log.WriteException("ValidationFailed", New InvalidOperationException("""secret"":true"))
            Dim content = File.ReadAllText(path, Encoding.UTF8)

            AssertContains(content, "System.InvalidOperationException", "exception type")
            AssertFalse(content.Contains("secret", StringComparison.OrdinalIgnoreCase), "exception message omitted")
        Finally
            Directory.Delete(tempRoot, recursive:=True)
        End Try
    End Sub
    <TestMethod(DisplayName:="UT-13-LOG-001 crash-log formatting omits exception messages")>
    Public Sub CrashLogFormattingOmitsMessages()
        ' NFR-13-SEC-003: Message text may carry document content and must never
        ' reach a persisted log. The crash log uses this shared formatter.
        Dim inner = New InvalidOperationException("""secret-inner"":true")
        Dim ex As Exception
        Try
            Throw New InvalidOperationException("""secret-outer"":1", inner)
        Catch caught As Exception
            ' Expected: capture a stack-bearing exception for the sanitizer assertions below.
            ex = caught
        End Try

        Dim text = FileLogService.DescribeException(ex)

        AssertContains(text, "System.InvalidOperationException", "exception type recorded")
        AssertContains(text, "--- inner (1) ---", "inner chain recorded")
        AssertFalse(text.Contains("secret-outer", StringComparison.OrdinalIgnoreCase), "outer message omitted")
        AssertFalse(text.Contains("secret-inner", StringComparison.OrdinalIgnoreCase), "inner message omitted")
    End Sub

End Class