' SPDX-License-Identifier: MPL-2.0
Imports System.IO
Imports System.Text
Imports System.Text.Json

Namespace Infrastructure
    Public Class SettingsService
        Private Shared ReadOnly SerializerOptions As New JsonSerializerOptions With {
            .PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            .WriteIndented = True
        }

        Public Sub New(Optional settingsDirectory As String = Nothing)
            If String.IsNullOrWhiteSpace(settingsDirectory) Then
                settingsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VisualJson")
            End If

            Me.SettingsDirectory = settingsDirectory
            Me.SettingsPath = Path.Combine(settingsDirectory, "settings.json")
        End Sub

        Public ReadOnly Property SettingsDirectory As String
        Public ReadOnly Property SettingsPath As String

        Public Function Load() As SettingsLoadResult
            If Not File.Exists(SettingsPath) Then
                Return New SettingsLoadResult(AppSettings.CreateDefault(), False, "", "")
            End If

            Try
                Dim json = File.ReadAllText(SettingsPath, Encoding.UTF8)
                Dim settings = JsonSerializer.Deserialize(Of AppSettings)(json, SerializerOptions)
                Return New SettingsLoadResult(Normalize(settings), False, "", "")
            Catch ex As Exception
                Dim brokenPath = MoveBrokenSettings()
                Return New SettingsLoadResult(AppSettings.CreateDefault(), True, brokenPath, ex.Message)
            End Try
        End Function

        Public Sub Save(settings As AppSettings)
            Dim normalized = Normalize(settings)
            Directory.CreateDirectory(SettingsDirectory)

            Dim json = JsonSerializer.Serialize(normalized, SerializerOptions)
            Dim lastError As Exception = Nothing

            For attempt = 0 To 1
                Dim tempPath = $"{SettingsPath}.{Guid.NewGuid():N}.tmp"
                Try
                    File.WriteAllText(tempPath, json, New UTF8Encoding(encoderShouldEmitUTF8Identifier:=False))
                    If File.Exists(SettingsPath) Then
                        File.Replace(tempPath, SettingsPath, Nothing, ignoreMetadataErrors:=True)
                    Else
                        File.Move(tempPath, SettingsPath)
                    End If
                    Return
                Catch ex As Exception
                    lastError = ex
                    If File.Exists(tempPath) Then
                        Try
                            File.Delete(tempPath)
                        Catch
                            ' IgnoreWithReason: temp-file cleanup is best effort; the retry loop reports the save failure.
                        End Try
                    End If
                    Threading.Thread.Sleep(25)
                End Try
            Next

            Throw New IOException("Settings could not be saved after retry.", lastError)
        End Sub

        Private Function MoveBrokenSettings() As String
            Directory.CreateDirectory(SettingsDirectory)
            Dim stamp = DateTime.Now.ToString("yyyyMMddHHmmss", Globalization.CultureInfo.InvariantCulture)
            Dim candidate = Path.Combine(SettingsDirectory, $"settings.broken-{stamp}.json")
            Dim index = 1

            While File.Exists(candidate)
                candidate = Path.Combine(SettingsDirectory, $"settings.broken-{stamp}-{index}.json")
                index += 1
            End While

            File.Move(SettingsPath, candidate)
            Return candidate
        End Function

        Private Shared Function Normalize(settings As AppSettings) As AppSettings
            Dim result = If(settings, AppSettings.CreateDefault())
            result.Version = Math.Max(1, result.Version)
            If Not String.Equals(result.Language, "ja", StringComparison.OrdinalIgnoreCase) Then
                result.Language = "en"
            Else
                result.Language = "ja"
            End If

            If result.SchemaSearchPaths Is Nothing Then
                result.SchemaSearchPaths = New List(Of String)()
            End If
            result.SchemaSearchPaths = result.SchemaSearchPaths.
                Where(Function(item) Not String.IsNullOrWhiteSpace(item)).
                Select(Function(item) item.Trim()).
                Distinct(StringComparer.OrdinalIgnoreCase).
                ToList()

            If result.RecentFiles Is Nothing Then
                result.RecentFiles = New List(Of String)()
            End If
            result.RecentFiles = result.RecentFiles.
                Where(Function(item) Not String.IsNullOrWhiteSpace(item)).
                Select(Function(item) item.Trim()).
                Distinct(StringComparer.OrdinalIgnoreCase).
                Take(RecentFilesService.MaxRecentFiles).
                ToList()

            If result.Window Is Nothing Then
                result.Window = New AppWindowSettings()
            End If
            If result.Window.Width < 920 Then
                result.Window.Width = 1180
            End If
            If result.Window.Height < 600 Then
                result.Window.Height = 760
            End If

            Return result
        End Function
    End Class
End Namespace
