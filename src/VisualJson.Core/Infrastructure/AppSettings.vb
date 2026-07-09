' SPDX-License-Identifier: MPL-2.0
Imports System.Text.Json
Imports System.Text.Json.Serialization

Namespace Infrastructure
    Public Class AppSettings
        Public Property Version As Integer = 1
        Public Property Language As String = "en"
        Public Property BackupBeforeSave As Boolean = True
        Public Property AllowExternalSchema As Boolean = False
        Public Property AutoCloseBrackets As Boolean = True
        Public Property SchemaSearchPaths As List(Of String) = New List(Of String)()
        Public Property RecentFiles As List(Of String) = New List(Of String)()
        Public Property Window As AppWindowSettings = New AppWindowSettings()

        <JsonExtensionData>
        Public Property ExtensionData As Dictionary(Of String, JsonElement)

        Public Shared Function CreateDefault() As AppSettings
            Return New AppSettings()
        End Function
    End Class
End Namespace
