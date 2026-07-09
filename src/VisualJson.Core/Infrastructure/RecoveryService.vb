' SPDX-License-Identifier: MPL-2.0
Imports System.IO
Imports System.Linq
Imports System.Text

Namespace Infrastructure
    Public Class RecoveryService
        Private ReadOnly _rootDirectory As String

        Public Sub New(Optional rootDirectory As String = Nothing)
            _rootDirectory = If(rootDirectory, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VisualJson", "Recovery"))
        End Sub

        Public ReadOnly Property RootDirectory As String
            Get
                Return _rootDirectory
            End Get
        End Property

        Public Function CreateSnapshot(sourcePath As String, text As String) As RecoveryCandidate
            Directory.CreateDirectory(_rootDirectory)

            Dim sourceName = If(String.IsNullOrWhiteSpace(sourcePath), "untitled", Path.GetFileName(sourcePath))
            Dim safeName = MakeSafeFileName(sourceName)
            Dim filePath = Path.Combine(_rootDirectory, $"{DateTime.UtcNow:yyyyMMddHHmmssfff}_{safeName}.json.recovery")
            File.WriteAllText(filePath, If(text, ""), New UTF8Encoding(encoderShouldEmitUTF8Identifier:=False))
            Return ToCandidate(filePath)
        End Function

        Public Function ListCandidates() As IReadOnlyList(Of RecoveryCandidate)
            If Not Directory.Exists(_rootDirectory) Then
                Return Array.Empty(Of RecoveryCandidate)()
            End If

            Return Directory.EnumerateFiles(_rootDirectory, "*.json.recovery").
                Select(Function(path) ToCandidate(path)).
                OrderByDescending(Function(candidate) candidate.LastWriteTimeUtc).
                ToList()
        End Function

        Public Function Load(candidate As RecoveryCandidate) As String
            If candidate Is Nothing Then
                Throw New ArgumentNullException(NameOf(candidate))
            End If

            Return File.ReadAllText(candidate.FilePath, Encoding.UTF8)
        End Function

        Public Sub Delete(candidate As RecoveryCandidate)
            If candidate Is Nothing Then
                Return
            End If

            If File.Exists(candidate.FilePath) Then
                File.Delete(candidate.FilePath)
            End If
        End Sub

        Public Sub DeleteAll()
            For Each candidate In ListCandidates()
                Delete(candidate)
            Next
        End Sub

        Private Shared Function ToCandidate(filePath As String) As RecoveryCandidate
            Dim info = New FileInfo(filePath)
            Return New RecoveryCandidate(filePath, info.Name, info.LastWriteTimeUtc, info.Length)
        End Function

        Private Shared Function MakeSafeFileName(value As String) As String
            Dim invalid = Path.GetInvalidFileNameChars()
            Dim builder = New StringBuilder()

            For Each ch In value
                If invalid.Contains(ch) Then
                    builder.Append("_"c)
                Else
                    builder.Append(ch)
                End If
            Next

            If builder.Length = 0 Then
                Return "untitled"
            End If

            Return builder.ToString()
        End Function
    End Class
End Namespace
