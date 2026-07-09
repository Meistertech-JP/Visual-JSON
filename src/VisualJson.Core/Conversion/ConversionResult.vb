' SPDX-License-Identifier: MPL-2.0
Namespace Conversion
    Public Class ConversionResult
        Public Sub New(output As String, Optional warnings As IEnumerable(Of String) = Nothing)
            Me.Output = If(output, "")
            Me.Warnings = If(warnings, Array.Empty(Of String)()).ToList()
        End Sub

        Public ReadOnly Property Output As String
        Public ReadOnly Property Warnings As List(Of String)
    End Class
End Namespace
