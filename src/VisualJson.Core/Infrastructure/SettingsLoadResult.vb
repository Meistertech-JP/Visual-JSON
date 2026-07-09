' SPDX-License-Identifier: MPL-2.0
Namespace Infrastructure
    Public Class SettingsLoadResult
        Public Sub New(settings As AppSettings, recoveredFromBroken As Boolean, brokenPath As String, errorMessage As String)
            Me.Settings = If(settings, AppSettings.CreateDefault())
            Me.RecoveredFromBroken = recoveredFromBroken
            Me.BrokenPath = If(brokenPath, "")
            Me.ErrorMessage = If(errorMessage, "")
        End Sub

        Public ReadOnly Property Settings As AppSettings
        Public ReadOnly Property RecoveredFromBroken As Boolean
        Public ReadOnly Property BrokenPath As String
        Public ReadOnly Property ErrorMessage As String
    End Class
End Namespace
