' SPDX-License-Identifier: MPL-2.0
Namespace Infrastructure
    Public Class FileSaveResult
        Public Sub New(path As String, backupPath As String)
            Me.Path = path
            Me.BackupPath = backupPath
        End Sub

        Public ReadOnly Property Path As String
        Public ReadOnly Property BackupPath As String
    End Class
End Namespace
