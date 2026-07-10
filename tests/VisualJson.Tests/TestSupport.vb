' SPDX-License-Identifier: MPL-2.0
Imports System.IO
Imports System.Text
Imports VisualJson.Core.Infrastructure
Imports VisualJson.Core.Models
Imports VisualJson.Core.Parsing
Imports VisualJson.Core.Services

' Shared fixtures for the MSTest suites (moved verbatim from the legacy Module Program).
Friend Module TestSupport

    Friend Function CreateLargeObjectArray(rows As Integer) As JsonTreeNode
        Dim root = New JsonTreeNode("$", "$", JsonNodeKind.ArrayValue, "", "")
        For index = 0 To rows - 1
            Dim element = New JsonTreeNode($"[{index}]", $"$[{index}]", JsonNodeKind.ObjectValue, "", $"/{index}")
            element.Children.Add(New JsonTreeNode("id", $"$[{index}].id", JsonNodeKind.NumberValue, index.ToString(Globalization.CultureInfo.InvariantCulture), $"/{index}/id"))
            element.Children.Add(New JsonTreeNode("name", $"$[{index}].name", JsonNodeKind.StringValue, $"user-{index}", $"/{index}/name"))
            element.Children.Add(New JsonTreeNode("email", $"$[{index}].email", JsonNodeKind.StringValue, $"user-{index}@example.com", $"/{index}/email"))
            element.Children.Add(New JsonTreeNode("active", $"$[{index}].active", JsonNodeKind.BooleanValue, If(index Mod 2 = 0, "true", "false"), $"/{index}/active"))
            element.Children.Add(New JsonTreeNode("score", $"$[{index}].score", JsonNodeKind.NumberValue, (index Mod 100).ToString(Globalization.CultureInfo.InvariantCulture), $"/{index}/score"))
            root.Children.Add(element)
        Next

        Return root
    End Function

    Friend Sub WriteEncodedSample(path As String, text As String, encodingInfo As DetectedTextEncoding)
        Dim service = New EncodingDetectionService()
        File.WriteAllBytes(path, service.GetBytes(text, encodingInfo))
    End Sub

    Friend Function CreateLargeJson(targetCharacters As Integer) As String
        Dim builder = New StringBuilder(targetCharacters + 1024)
        builder.Append("{""items"":[")
        Dim index = 0

        While builder.Length < targetCharacters
            If index > 0 Then
                builder.Append(","c)
            End If

            builder.Append("{""id"":")
            builder.Append(index.ToString(Globalization.CultureInfo.InvariantCulture))
            builder.Append(",""name"":""item")
            builder.Append(index.ToString(Globalization.CultureInfo.InvariantCulture))
            builder.Append(""",""payload"":""")
            builder.Append("x"c, 2048)
            builder.Append("""}")
            index += 1
        End While

        builder.Append("]}")
        Return builder.ToString()
    End Function

    Friend Function CreateTempDirectory() As String
        Dim path = IO.Path.Combine(IO.Path.GetTempPath(), "VisualJson.Tests", Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory(path)
        Return path
    End Function
End Module
