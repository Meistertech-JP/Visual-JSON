' SPDX-License-Identifier: MPL-2.0
Imports System.IO
Imports System.Text

Namespace Infrastructure
    Public Class FileLogService
        Public Sub New(Optional logDirectory As String = Nothing)
            If String.IsNullOrWhiteSpace(logDirectory) Then
                logDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VisualJson", "logs")
            End If

            Me.LogDirectory = logDirectory
        End Sub

        Public ReadOnly Property LogDirectory As String

        Public Function Write(operation As String, detail As String) As String
            Directory.CreateDirectory(LogDirectory)
            CleanupOldLogs()

            Dim path = CurrentLogPath()
            Dim safeOperation = Sanitize(operation)
            Dim safeDetail = Sanitize(detail)
            Dim line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz} {safeOperation} {safeDetail}{Environment.NewLine}"
            File.AppendAllText(path, line, New UTF8Encoding(encoderShouldEmitUTF8Identifier:=False))
            Return path
        End Function

        Public Function WriteException(operation As String, ex As Exception) As String
            Dim detail = ""
            If ex IsNot Nothing Then
                detail = $"{ex.GetType().FullName} stack={If(ex.StackTrace, "")}"
            End If

            Return Write(operation, detail)
        End Function

        ''' Formats an exception chain for persisted logs without any Message text,
        ''' so document/schema content that leaked into a message can never reach a
        ''' log file (NFR-13-SEC-003). Used by the crash log as well.
        Public Shared Function DescribeException(ex As Exception) As String
            Dim builder = New StringBuilder()
            Dim current = ex
            Dim depth = 0
            While current IsNot Nothing AndAlso depth < 10
                If depth > 0 Then
                    builder.AppendLine($"--- inner ({depth}) ---")
                End If
                builder.AppendLine(current.GetType().FullName)
                builder.AppendLine(If(current.StackTrace, "(no stack trace)"))
                current = current.InnerException
                depth += 1
            End While

            Return builder.ToString()
        End Function

        Private Function CurrentLogPath() As String
            Return Path.Combine(LogDirectory, $"visualjson-{DateTime.Now:yyyyMMdd}.log")
        End Function

        Private Sub CleanupOldLogs()
            If Not Directory.Exists(LogDirectory) Then
                Return
            End If

            Dim files = Directory.GetFiles(LogDirectory, "visualjson-*.log").
                OrderByDescending(Function(path) path, StringComparer.OrdinalIgnoreCase).
                ToList()

            For Each path In files.Skip(7)
                Try
                    File.Delete(path)
                Catch
                    ' IgnoreWithReason: failing to prune an old log must not break logging itself.
                End Try
            Next
        End Sub

        Private Shared Function Sanitize(value As String) As String
            Return If(value, "").Replace(ControlChars.Cr, " "c).Replace(ControlChars.Lf, " "c)
        End Function
    End Class
End Namespace
