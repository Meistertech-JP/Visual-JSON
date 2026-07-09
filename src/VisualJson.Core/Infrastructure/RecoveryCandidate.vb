' SPDX-License-Identifier: MPL-2.0
Namespace Infrastructure
    Public Class RecoveryCandidate
        Public Sub New(filePath As String, displayName As String, lastWriteTimeUtc As DateTime, sizeBytes As Long)
            Me.FilePath = filePath
            Me.DisplayName = displayName
            Me.LastWriteTimeUtc = lastWriteTimeUtc
            Me.SizeBytes = sizeBytes
        End Sub

        Public ReadOnly Property FilePath As String
        Public ReadOnly Property DisplayName As String
        Public ReadOnly Property LastWriteTimeUtc As DateTime
        Public ReadOnly Property SizeBytes As Long
    End Class
End Namespace
