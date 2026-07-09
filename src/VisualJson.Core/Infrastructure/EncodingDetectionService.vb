' SPDX-License-Identifier: MPL-2.0
Imports System.IO
Imports System.Text

Namespace Infrastructure
    Public Class EncodingDetectionService
        Private Shared ReadOnly StrictUtf8 As New UTF8Encoding(encoderShouldEmitUTF8Identifier:=False, throwOnInvalidBytes:=True)
        Private Shared ReadOnly LenientUtf8 As New UTF8Encoding(encoderShouldEmitUTF8Identifier:=False, throwOnInvalidBytes:=False)

        Public Function ReadText(path As String) As DocumentTextReadResult
            Dim bytes = File.ReadAllBytes(path)
            Dim info = Detect(bytes)
            Dim payload = StripBom(bytes, info.Kind)
            Dim text = info.Encoding.GetString(payload)
            info.NewLine = DetectNewLine(text)
            Return New DocumentTextReadResult(text, info)
        End Function

        Public Function Detect(bytes As Byte()) As DetectedTextEncoding
            Dim source = If(bytes, Array.Empty(Of Byte)())

            If StartsWith(source, &HEF, &HBB, &HBF) Then
                Return New DetectedTextEncoding(TextEncodingKind.Utf8Bom, StrictUtf8, True, NewLineKind.Lf, "")
            End If

            If StartsWith(source, &HFF, &HFE) Then
                Return New DetectedTextEncoding(TextEncodingKind.Utf16LeBom, Encoding.Unicode, True, NewLineKind.Lf, "")
            End If

            If StartsWith(source, &HFE, &HFF) Then
                Return New DetectedTextEncoding(TextEncodingKind.Utf16BeBom, Encoding.BigEndianUnicode, True, NewLineKind.Lf, "")
            End If

            Dim inferred = InferUtf16(source)
            If inferred.HasValue Then
                If inferred.Value = TextEncodingKind.Utf16Le Then
                    Return New DetectedTextEncoding(TextEncodingKind.Utf16Le, Encoding.Unicode, False, NewLineKind.Lf, "UTF-16 LE inferred from null-byte parity.")
                End If

                Return New DetectedTextEncoding(TextEncodingKind.Utf16Be, Encoding.BigEndianUnicode, False, NewLineKind.Lf, "UTF-16 BE inferred from null-byte parity.")
            End If

            Try
                StrictUtf8.GetString(source)
                Return New DetectedTextEncoding(TextEncodingKind.Utf8, StrictUtf8, False, NewLineKind.Lf, "")
            Catch
                Return New DetectedTextEncoding(TextEncodingKind.Utf8, LenientUtf8, False, NewLineKind.Lf, "Encoding could not be determined; opened as UTF-8.")
            End Try
        End Function

        Public Function GetBytes(text As String, encodingInfo As DetectedTextEncoding) As Byte()
            Dim info = If(encodingInfo, DetectedTextEncoding.CreateDefault())
            Dim normalized = NormalizeNewLines(If(text, ""), info.NewLine)
            Dim body = GetWriteEncoding(info.Kind).GetBytes(normalized)
            If Not info.HasBom Then
                Return body
            End If

            Dim preamble = GetPreamble(info.Kind)
            Dim result(preamble.Length + body.Length - 1) As Byte
            Buffer.BlockCopy(preamble, 0, result, 0, preamble.Length)
            Buffer.BlockCopy(body, 0, result, preamble.Length, body.Length)
            Return result
        End Function

        Public Sub WriteText(path As String, text As String, encodingInfo As DetectedTextEncoding)
            File.WriteAllBytes(path, GetBytes(text, encodingInfo))
        End Sub

        Public Shared Function NormalizeNewLines(text As String, newLine As NewLineKind) As String
            Dim unified = If(text, "").Replace(vbCrLf, vbLf).Replace(vbCr, vbLf)
            If newLine = NewLineKind.CrLf Then
                Return unified.Replace(vbLf, vbCrLf)
            End If

            Return unified
        End Function

        Public Shared Function DetectNewLine(text As String) As NewLineKind
            Dim crlf = 0
            Dim lfOnly = 0
            Dim index = 0
            Dim source = If(text, "")

            While index < source.Length
                If source(index) = ControlChars.Cr Then
                    If index + 1 < source.Length AndAlso source(index + 1) = ControlChars.Lf Then
                        crlf += 1
                        index += 2
                    Else
                        lfOnly += 1
                        index += 1
                    End If
                ElseIf source(index) = ControlChars.Lf Then
                    lfOnly += 1
                    index += 1
                Else
                    index += 1
                End If
            End While

            If crlf > lfOnly Then
                Return NewLineKind.CrLf
            End If

            Return NewLineKind.Lf
        End Function

        Private Shared Function InferUtf16(bytes As Byte()) As TextEncodingKind?
            If bytes Is Nothing OrElse bytes.Length < 4 Then
                Return Nothing
            End If

            Dim limit = Math.Min(bytes.Length, 4096)
            Dim evenZeros = 0
            Dim oddZeros = 0
            For index = 0 To limit - 1
                If bytes(index) = 0 Then
                    If index Mod 2 = 0 Then
                        evenZeros += 1
                    Else
                        oddZeros += 1
                    End If
                End If
            Next

            Dim threshold = Math.Max(2, limit \ 8)
            If oddZeros >= threshold AndAlso oddZeros >= evenZeros * 2 Then
                Return TextEncodingKind.Utf16Le
            End If
            If evenZeros >= threshold AndAlso evenZeros >= oddZeros * 2 Then
                Return TextEncodingKind.Utf16Be
            End If

            Return Nothing
        End Function

        Private Shared Function StartsWith(bytes As Byte(), ParamArray prefix As Byte()) As Boolean
            If bytes Is Nothing OrElse bytes.Length < prefix.Length Then
                Return False
            End If

            For index = 0 To prefix.Length - 1
                If bytes(index) <> prefix(index) Then
                    Return False
                End If
            Next

            Return True
        End Function

        Private Shared Function StripBom(bytes As Byte(), kind As TextEncodingKind) As Byte()
            Dim length = 0
            Select Case kind
                Case TextEncodingKind.Utf8Bom
                    length = 3
                Case TextEncodingKind.Utf16LeBom, TextEncodingKind.Utf16BeBom
                    length = 2
            End Select

            If length = 0 Then
                Return If(bytes, Array.Empty(Of Byte)())
            End If

            Return bytes.Skip(length).ToArray()
        End Function

        Private Shared Function GetWriteEncoding(kind As TextEncodingKind) As Encoding
            Select Case kind
                Case TextEncodingKind.Utf16Le, TextEncodingKind.Utf16LeBom
                    Return Encoding.Unicode
                Case TextEncodingKind.Utf16Be, TextEncodingKind.Utf16BeBom
                    Return Encoding.BigEndianUnicode
                Case Else
                    Return StrictUtf8
            End Select
        End Function

        Private Shared Function GetPreamble(kind As TextEncodingKind) As Byte()
            Select Case kind
                Case TextEncodingKind.Utf8Bom
                    Return New Byte() {&HEF, &HBB, &HBF}
                Case TextEncodingKind.Utf16LeBom
                    Return New Byte() {&HFF, &HFE}
                Case TextEncodingKind.Utf16BeBom
                    Return New Byte() {&HFE, &HFF}
                Case Else
                    Return Array.Empty(Of Byte)()
            End Select
        End Function
    End Class
End Namespace
