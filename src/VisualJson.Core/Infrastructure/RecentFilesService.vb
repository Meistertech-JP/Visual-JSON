' SPDX-License-Identifier: MPL-2.0
Imports System.IO

Namespace Infrastructure
    Public Class RecentFilesService
        Public Const MaxRecentFiles As Integer = 10

        Public Sub Add(settings As AppSettings, path As String)
            If settings Is Nothing OrElse String.IsNullOrWhiteSpace(path) Then
                Return
            End If

            If settings.RecentFiles Is Nothing Then
                settings.RecentFiles = New List(Of String)()
            End If

            Dim normalized = NormalizePath(path)
            settings.RecentFiles.RemoveAll(Function(item) String.Equals(NormalizePath(item), normalized, StringComparison.OrdinalIgnoreCase))
            settings.RecentFiles.Insert(0, normalized)

            If settings.RecentFiles.Count > MaxRecentFiles Then
                settings.RecentFiles.RemoveRange(MaxRecentFiles, settings.RecentFiles.Count - MaxRecentFiles)
            End If
        End Sub

        Public Sub Remove(settings As AppSettings, path As String)
            If settings?.RecentFiles Is Nothing OrElse String.IsNullOrWhiteSpace(path) Then
                Return
            End If

            Dim normalized = NormalizePath(path)
            settings.RecentFiles.RemoveAll(Function(item) String.Equals(NormalizePath(item), normalized, StringComparison.OrdinalIgnoreCase))
        End Sub

        Public Sub Clear(settings As AppSettings)
            If settings Is Nothing Then
                Return
            End If

            If settings.RecentFiles Is Nothing Then
                settings.RecentFiles = New List(Of String)()
            Else
                settings.RecentFiles.Clear()
            End If
        End Sub

        Private Shared Function NormalizePath(path As String) As String
            Try
                Return IO.Path.GetFullPath(If(path, "").Trim())
            Catch
                ' IgnoreWithReason: an unnormalizable path is kept as typed so history stays usable.
                Return If(path, "").Trim()
            End Try
        End Function
    End Class
End Namespace
