' SPDX-License-Identifier: MPL-2.0
Imports VisualJson.Core.Parsing

Namespace Infrastructure
    Public Class FormatSniffResult
        Public Sub New(format As JsonInputFormat, confident As Boolean, reason As String)
            Me.Format = format
            Me.Confident = confident
            Me.Reason = If(reason, "")
        End Sub

        Public ReadOnly Property Format As JsonInputFormat
        Public ReadOnly Property Confident As Boolean
        Public ReadOnly Property Reason As String
    End Class
End Namespace
