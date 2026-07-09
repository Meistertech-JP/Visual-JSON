' SPDX-License-Identifier: MPL-2.0
Namespace Diagnostics
    Public Class ValidationDiagnostic
        Public Sub New(severity As String,
                       message As String,
                       Optional line As Integer? = Nothing,
                       Optional column As Integer? = Nothing,
                       Optional errorCode As String = Nothing,
                       Optional jsonPath As String = Nothing,
                       Optional jsonPointer As String = Nothing,
                       Optional schemaPath As String = Nothing,
                       Optional schemaUri As String = Nothing,
                       Optional relatedRange As TextRange = Nothing)
            Me.Severity = severity
            Me.Message = message
            Me.Line = line
            Me.Column = column
            Me.ErrorCode = If(errorCode, "")
            Me.JsonPath = jsonPath
            Me.JsonPointer = jsonPointer
            Me.SchemaPath = schemaPath
            Me.SchemaUri = schemaUri
            Me.RelatedRange = relatedRange
        End Sub

        Public ReadOnly Property ErrorCode As String
        Public ReadOnly Property Severity As String
        Public ReadOnly Property Message As String
        Public ReadOnly Property JsonPath As String
        Public ReadOnly Property JsonPointer As String
        Public ReadOnly Property Line As Integer?
        Public ReadOnly Property Column As Integer?
        Public ReadOnly Property SchemaPath As String
        Public ReadOnly Property SchemaUri As String
        Public ReadOnly Property RelatedRange As TextRange

        Public ReadOnly Property Location As String
            Get
                If Line.HasValue AndAlso Column.HasValue Then
                    Return $"{Line}:{Column}"
                End If

                If Line.HasValue Then
                    Return Line.Value.ToString(Globalization.CultureInfo.InvariantCulture)
                End If

                Return ""
            End Get
        End Property
    End Class
End Namespace
