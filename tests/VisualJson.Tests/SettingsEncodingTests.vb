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
Public Class SettingsEncodingTests

    <TestMethod(DisplayName:="UT-P2-SET-001 settings round trip")>
    Public Sub P2SettingsRoundTrip()
        Dim tempRoot = CreateTempDirectory()
        Try
            Dim service = New SettingsService(tempRoot)
            Dim settings = AppSettings.CreateDefault()
            settings.Language = "ja"
            settings.BackupBeforeSave = False
            settings.AllowExternalSchema = True
            settings.AutoCloseBrackets = False
            settings.SchemaSearchPaths.Add("C:\schemas")
            settings.RecentFiles.Add("C:\data\a.json")
            settings.Window.Width = 1400
            settings.Window.Height = 900
            settings.Window.Maximized = True

            service.Save(settings)
            Dim loaded = service.Load().Settings

            AssertEqual("ja", loaded.Language, "language")
            AssertFalse(loaded.BackupBeforeSave, "backup")
            AssertTrue(loaded.AllowExternalSchema, "external schema")
            AssertFalse(loaded.AutoCloseBrackets, "auto close")
            AssertEqual("C:\schemas", loaded.SchemaSearchPaths(0), "schema path")
            AssertEqual("C:\data\a.json", loaded.RecentFiles(0), "recent path")
            AssertEqual(1400.0, loaded.Window.Width, "window width")
            AssertTrue(loaded.Window.Maximized, "maximized")
        Finally
            Directory.Delete(tempRoot, recursive:=True)
        End Try
    End Sub

    <TestMethod(DisplayName:="UT-P2-SET-002 broken settings are moved aside")>
    Public Sub P2BrokenSettingsMovedAside()
        Dim tempRoot = CreateTempDirectory()
        Try
            Dim service = New SettingsService(tempRoot)
            Directory.CreateDirectory(tempRoot)
            File.WriteAllText(service.SettingsPath, "{ broken", Encoding.UTF8)

            Dim result = service.Load()

            AssertTrue(result.RecoveredFromBroken, "broken recovered")
            AssertTrue(File.Exists(result.BrokenPath), "broken file exists")
            AssertEqual("en", result.Settings.Language, "default language")
        Finally
            Directory.Delete(tempRoot, recursive:=True)
        End Try
    End Sub

    <TestMethod(DisplayName:="UT-P2-SET-003 unknown settings keys are preserved")>
    Public Sub P2UnknownSettingsKeysPreserved()
        Dim tempRoot = CreateTempDirectory()
        Try
            Dim service = New SettingsService(tempRoot)
            Directory.CreateDirectory(tempRoot)
            File.WriteAllText(service.SettingsPath, "{""version"":1,""language"":""ja"",""futureValue"":{""enabled"":true}}", New UTF8Encoding(False))

            Dim loaded = service.Load().Settings
            service.Save(loaded)
            Dim saved = File.ReadAllText(service.SettingsPath, Encoding.UTF8)

            AssertContains(saved, "futureValue", "unknown key")
            AssertContains(saved, "enabled", "nested unknown key")
        Finally
            Directory.Delete(tempRoot, recursive:=True)
        End Try
    End Sub

    <TestMethod(DisplayName:="UT-P2-SET-004 recent files max duplicate clear")>
    Public Sub P2RecentFilesMaxDuplicateClear()
        Dim settings = AppSettings.CreateDefault()
        Dim recent = New RecentFilesService()

        For index = 0 To 10
            recent.Add(settings, $"C:\data\file{index}.json")
        Next
        AssertEqual(RecentFilesService.MaxRecentFiles, settings.RecentFiles.Count, "max recent")

        recent.Add(settings, "C:\data\file5.json")
        AssertEqual("C:\data\file5.json", settings.RecentFiles(0), "duplicate moved to front")
        AssertEqual(RecentFilesService.MaxRecentFiles, settings.RecentFiles.Count, "duplicate count unchanged")

        recent.Clear(settings)
        AssertEqual(0, settings.RecentFiles.Count, "cleared")
    End Sub

    <TestMethod(DisplayName:="UT-P2-ENC-001 encoding samples are detected")>
    Public Sub P2EncodingSamplesDetected()
        Dim tempRoot = CreateTempDirectory()
        Try
            Dim service = New EncodingDetectionService()
            Dim text = "{""a"":1}" & vbCrLf
            WriteEncodedSample(Path.Combine(tempRoot, "utf8.json"), text, New DetectedTextEncoding(TextEncodingKind.Utf8, New UTF8Encoding(False, True), False, NewLineKind.CrLf, ""))
            WriteEncodedSample(Path.Combine(tempRoot, "utf8bom.json"), text, New DetectedTextEncoding(TextEncodingKind.Utf8Bom, New UTF8Encoding(False, True), True, NewLineKind.CrLf, ""))
            WriteEncodedSample(Path.Combine(tempRoot, "utf16le.json"), text, New DetectedTextEncoding(TextEncodingKind.Utf16Le, Encoding.Unicode, False, NewLineKind.CrLf, ""))
            WriteEncodedSample(Path.Combine(tempRoot, "utf16be.json"), text, New DetectedTextEncoding(TextEncodingKind.Utf16Be, Encoding.BigEndianUnicode, False, NewLineKind.CrLf, ""))

            AssertEqual("UTF-8", service.ReadText(Path.Combine(tempRoot, "utf8.json")).EncodingInfo.Name, "utf8")
            AssertEqual("UTF-8 BOM", service.ReadText(Path.Combine(tempRoot, "utf8bom.json")).EncodingInfo.Name, "utf8 bom")
            AssertEqual("UTF-16 LE", service.ReadText(Path.Combine(tempRoot, "utf16le.json")).EncodingInfo.Name, "utf16 le")
            AssertEqual("UTF-16 BE", service.ReadText(Path.Combine(tempRoot, "utf16be.json")).EncodingInfo.Name, "utf16 be")
        Finally
            Directory.Delete(tempRoot, recursive:=True)
        End Try
    End Sub

    <TestMethod(DisplayName:="UT-P2-ENC-002 save preserves detected encoding")>
    Public Sub P2SavePreservesDetectedEncoding()
        Dim tempRoot = CreateTempDirectory()
        Try
            Dim target = Path.Combine(tempRoot, "sample.json")
            Dim encodingService = New EncodingDetectionService()
            Dim originalEncoding = New DetectedTextEncoding(TextEncodingKind.Utf16LeBom, Encoding.Unicode, True, NewLineKind.CrLf, "")
            File.WriteAllBytes(target, encodingService.GetBytes("{""a"":1}" & vbCrLf, originalEncoding))

            Dim read = encodingService.ReadText(target)
            Dim saver = New FileSaveService()
            saver.Save(target, "{""a"":2}", read.EncodingInfo)
            Dim saved = encodingService.ReadText(target)

            AssertEqual("UTF-16 LE BOM", saved.EncodingInfo.Name, "encoding preserved")
            AssertEqual("CRLF", saved.EncodingInfo.NewLineName, "newline preserved")
            AssertContains(saved.Text, """a"": 2", "saved content")
        Finally
            Directory.Delete(tempRoot, recursive:=True)
        End Try
    End Sub

    <TestMethod(DisplayName:="UT-P2-ENC-003 newline majority is preserved")>
    Public Sub P2NewLineMajorityDetected()
        Dim text = "{" & vbCrLf & "  ""a"": 1," & vbCrLf & "  ""b"": 2" & vbLf & "}"
        AssertEqual(NewLineKind.CrLf, EncodingDetectionService.DetectNewLine(text), "crlf majority")
        AssertEqual("a" & vbCrLf & "b", EncodingDetectionService.NormalizeNewLines("a" & vbLf & "b", NewLineKind.CrLf), "normalize crlf")
    End Sub

    <TestMethod(DisplayName:="UT-P2-ENC-004 undecodable bytes fall back to UTF-8 warning")>
    Public Sub P2EncodingFallbackWarning()
        Dim service = New EncodingDetectionService()
        Dim info = service.Detect(New Byte() {&HFF, &HFF, &HFF})

        AssertEqual(TextEncodingKind.Utf8, info.Kind, "fallback kind")
        AssertTrue(info.Warning.Length > 0, "warning")
    End Sub
End Class
