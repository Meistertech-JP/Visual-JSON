' SPDX-License-Identifier: MPL-2.0
Namespace Infrastructure
    Public Class DocumentTextReadResult
        Public Sub New(text As String, encodingInfo As DetectedTextEncoding)
            Me.Text = If(text, "")
            Me.EncodingInfo = If(encodingInfo, DetectedTextEncoding.CreateDefault())
        End Sub

        Public ReadOnly Property Text As String
        Public ReadOnly Property EncodingInfo As DetectedTextEncoding
    End Class
End Namespace
