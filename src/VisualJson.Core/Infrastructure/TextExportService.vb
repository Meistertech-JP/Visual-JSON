' SPDX-License-Identifier: MPL-2.0
Imports System.IO
Imports System.Text

Namespace Infrastructure
    ''' Saves conversion output (XML/YAML/JSON text) safely: write to a temp file first,
    ''' back up an existing target, then replace it. A failed export never corrupts the target
    ''' and never touches the source document (spec 06 §8, NFR-REL-005).
    Public Class TextExportService
        Public Function Save(path As String, content As String) As FileSaveResult
            If String.IsNullOrWhiteSpace(path) Then
                Throw New ArgumentException("An export path is required.", NameOf(path))
            End If

            Dim fullPath = System.IO.Path.GetFullPath(path)
            Dim directoryPath = System.IO.Path.GetDirectoryName(fullPath)
            If String.IsNullOrWhiteSpace(directoryPath) Then
                Throw New InvalidOperationException("The export path must include a directory.")
            End If

            Directory.CreateDirectory(directoryPath)
            Dim tempPath = $"{fullPath}.{Guid.NewGuid():N}.tmp"
            Dim backupPath As String = Nothing

            File.WriteAllText(tempPath, If(content, ""), New UTF8Encoding(encoderShouldEmitUTF8Identifier:=False))

            Try
                If File.Exists(fullPath) Then
                    backupPath = CreateBackup(fullPath)
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
