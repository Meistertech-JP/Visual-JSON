' SPDX-License-Identifier: MPL-2.0
Imports System.Text

Namespace Infrastructure
    Public Enum TextEncodingKind
        Utf8
        Utf8Bom
        Utf16Le
        Utf16LeBom
        Utf16Be
        Utf16BeBom
    End Enum

    Public Enum NewLineKind
        Lf
        CrLf
    End Enum

    Public Class DetectedTextEncoding
        Public Sub New(kind As TextEncodingKind, encoding As Encoding, hasBom As Boolean, newLine As NewLineKind, warning As String)
            Me.Kind = kind
            Me.Encoding = encoding
            Me.HasBom = hasBom
            Me.NewLine = newLine
            Me.Warning = If(warning, "")
        End Sub

        Public ReadOnly Property Kind As TextEncodingKind
        Public ReadOnly Property Encoding As Encoding
        Public ReadOnly Property HasBom As Boolean
        Public Property NewLine As NewLineKind
        Public Property Warning As String

        Public ReadOnly Property Name As String
            Get
                Select Case Kind
                    Case TextEncodingKind.Utf8Bom
                        Return "UTF-8 BOM"
                    Case TextEncodingKind.Utf16Le
                        Return "UTF-16 LE"
                    Case TextEncodingKind.Utf16LeBom
                        Return "UTF-16 LE BOM"
                    Case TextEncodingKind.Utf16Be
                        Return "UTF-16 BE"
                    Case TextEncodingKind.Utf16BeBom
                        Return "UTF-16 BE BOM"
                    Case Else
                        Return "UTF-8"
                End Select
            End Get
        End Property

        Public ReadOnly Property NewLineName As String
            Get
                If NewLine = NewLineKind.CrLf Then
                    Return "CRLF"
                End If

                Return "LF"
            End Get
        End Property

        Public Shared Function CreateDefault() As DetectedTextEncoding
            Return New DetectedTextEncoding(TextEncodingKind.Utf8, New UTF8Encoding(encoderShouldEmitUTF8Identifier:=False, throwOnInvalidBytes:=True), False, NewLineKind.Lf, "")
        End Function
    End Class
End Namespace
