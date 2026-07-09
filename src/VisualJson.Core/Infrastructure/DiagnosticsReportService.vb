' SPDX-License-Identifier: MPL-2.0
Imports System.Runtime.InteropServices
Imports System.Text
Imports VisualJson.Core.Diagnostics

Namespace Infrastructure
    Public Class DiagnosticsReportService
        Public Function CreateReport(appVersion As String, filePath As String, textLength As Integer, diagnostics As IEnumerable(Of ValidationDiagnostic), lastException As Exception, Optional nodeCount As Integer = 0, Optional uiStack As String = "WPF", Optional language As String = "", Optional inputFormat As String = "", Optional encodingName As String = "", Optional newLineName As String = "") As String
            Dim diagnosticList = If(diagnostics, Array.Empty(Of ValidationDiagnostic)()).ToList()
            Dim builder = New StringBuilder()

            builder.AppendLine("Visual JSON Diagnostics")
            builder.AppendLine($"AppVersion: {If(String.IsNullOrWhiteSpace(appVersion), "unknown", appVersion)}")
            builder.AppendLine($"OS: {RuntimeInformation.OSDescription}")
            builder.AppendLine($".NET: {RuntimeInformation.FrameworkDescription}")
            builder.AppendLine($"Architecture: {RuntimeInformation.ProcessArchitecture}")
            builder.AppendLine($"UIStack: {If(String.IsNullOrWhiteSpace(uiStack), "unknown", uiStack)}")
            builder.AppendLine($"UILanguage: {If(String.IsNullOrWhiteSpace(language), "unknown", language)}")
            builder.AppendLine($"InputFormat: {If(String.IsNullOrWhiteSpace(inputFormat), "unknown", inputFormat)}")
            builder.AppendLine($"Encoding: {If(String.IsNullOrWhiteSpace(encodingName), "unknown", encodingName)}")
            builder.AppendLine($"NewLine: {If(String.IsNullOrWhiteSpace(newLineName), "unknown", newLineName)}")
            builder.AppendLine($"FileName: {If(String.IsNullOrWhiteSpace(filePath), "untitled", IO.Path.GetFileName(filePath))}")
            builder.AppendLine($"FileExtension: {If(String.IsNullOrWhiteSpace(filePath), "", IO.Path.GetExtension(filePath))}")
            builder.AppendLine($"FileExists: {If(Not String.IsNullOrWhiteSpace(filePath) AndAlso IO.File.Exists(filePath), "yes", "no")}")

            If Not String.IsNullOrWhiteSpace(filePath) AndAlso IO.File.Exists(filePath) Then
                Dim info = New IO.FileInfo(filePath)
                builder.AppendLine($"FileSizeBytes: {info.Length}")
            End If

            builder.AppendLine($"EditorTextLength: {Math.Max(0, textLength)}")
            builder.AppendLine($"NodeCountApprox: {Math.Max(0, nodeCount)}")
            builder.AppendLine($"ProcessMemoryBytes: {Environment.WorkingSet}")
            builder.AppendLine($"DiagnosticsCount: {diagnosticList.Count}")

            If diagnosticList.Count > 0 Then
                ' Spec 12 §4: the report must never carry document body fragments, so
                ' diagnostic/exception message text (which may quote JSON tokens) is excluded.
                Dim first = diagnosticList(0)
                builder.AppendLine($"FirstDiagnostic: {first.Severity} {first.ErrorCode} {first.Location}".TrimEnd())
            End If

            If lastException IsNot Nothing Then
                builder.AppendLine($"LastExceptionType: {lastException.GetType().FullName}")
                builder.AppendLine($"LastExceptionStackTrace: {lastException.StackTrace}")
            End If

            builder.AppendLine("JsonBodyIncluded: no")
            Return builder.ToString()
        End Function
    End Class
End Namespace
