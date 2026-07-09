' SPDX-License-Identifier: MPL-2.0
Imports System.IO
Imports System.Text
Imports VisualJson.Core.Serialization

Namespace Infrastructure
    Public Class FileSaveService
        Private ReadOnly _formatter As New JsonFormatterService()
        Private ReadOnly _encoding As New EncodingDetectionService()

        Public Function Save(path As String, jsonText As String, Optional encodingInfo As DetectedTextEncoding = Nothing, Optional backupBeforeSave As Boolean = True) As FileSaveResult
            If String.IsNullOrWhiteSpace(path) Then
                Throw New ArgumentException("A save path is required.", NameOf(path))
            End If

            Dim fullPath = System.IO.Path.GetFullPath(path)
            Dim directoryPath = System.IO.Path.GetDirectoryName(fullPath)
            If String.IsNullOrWhiteSpace(directoryPath) Then
                Throw New InvalidOperationException("The save path must include a directory.")
            End If

            Directory.CreateDirectory(directoryPath)
            Dim formatted = _formatter.Format(jsonText)
            Return WriteAtomically(fullPath, formatted, encodingInfo, backupBeforeSave)
        End Function

        ''' FR-P2-602: writes pre-serialized content (e.g. JSON Lines) through the
        ''' same temp-file replacement and backup pipeline, without re-formatting
        ''' it as a single standard JSON document.
        Public Function SaveRaw(path As String, content As String, Optional encodingInfo As DetectedTextEncoding = Nothing, Optional backupBeforeSave As Boolean = True) As FileSaveResult
            If String.IsNullOrWhiteSpace(path) Then
                Throw New ArgumentException("A save path is required.", NameOf(path))
            End If

            Dim fullPath = System.IO.Path.GetFullPath(path)
            Dim directoryPath = System.IO.Path.GetDirectoryName(fullPath)
            If String.IsNullOrWhiteSpace(directoryPath) Then
                Throw New InvalidOperationException("The save path must include a directory.")
            End If

            Directory.CreateDirectory(directoryPath)
            Return WriteAtomically(fullPath, If(content, ""), encodingInfo, backupBeforeSave)
        End Function

        Private Function WriteAtomically(fullPath As String, content As String, encodingInfo As DetectedTextEncoding, backupBeforeSave As Boolean) As FileSaveResult
            Dim tempPath = $"{fullPath}.{Guid.NewGuid():N}.tmp"
            Dim backupPath As String = Nothing

            File.WriteAllBytes(tempPath, _encoding.GetBytes(content, If(encodingInfo, DetectedTextEncoding.CreateDefault())))

            Try
                If File.Exists(fullPath) Then
                    If backupBeforeSave Then
                        backupPath = CreateBackup(fullPath)
                    End If
                    File.Replace(tempPath, fullPath, Nothing, ignoreMetadataErrors:=True)
                Else
                    File.Move(tempPath, fullPath)
                End If
            Catch
                If File.Exists(tempPath) Then
                    File.Delete(tempPath)
                End If

                Throw
            End Try

            Return New FileSaveResult(fullPath, backupPath)
        End Function

        Private Shared Function CreateBackup(path As String) As String
            Dim stamp = DateTime.Now.ToString("yyyyMMddHHmmss", Globalization.CultureInfo.InvariantCulture)
            Dim candidate = $"{path}.{stamp}.bak"
            Dim index = 1

            While File.Exists(candidate)
                candidate = $"{path}.{stamp}.{index}.bak"
                index += 1
            End While

            File.Copy(path, candidate, overwrite:=False)
            Return candidate
        End Function
    End Class
End Namespace
