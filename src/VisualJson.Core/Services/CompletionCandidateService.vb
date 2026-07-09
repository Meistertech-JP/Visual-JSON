' SPDX-License-Identifier: MPL-2.0
Imports System.Text.Json
Imports VisualJson.Core.Models

Namespace Services
    ''' FR-P2-504: enumerates key completion candidates for the object that
    ''' contains the caret. Candidates are the union of keys used by sibling
    ''' objects in the same array and the schema "properties" for the location,
    ''' minus the keys the object already has.
    Public Class CompletionCandidateService
        Private Const MaxSchemaRefDepth As Integer = 32

        Public Function GetKeyCandidates(root As JsonTreeNode, containingObject As JsonTreeNode, Optional schemaText As String = Nothing) As IReadOnlyList(Of String)
            If containingObject Is Nothing OrElse containingObject.Kind <> JsonNodeKind.ObjectValue Then
                Return Array.Empty(Of String)()
            End If

            Dim existing = New HashSet(Of String)(StringComparer.Ordinal)
            For Each child In containingObject.Children
                existing.Add(child.Key)
            Next

            Dim candidates = New List(Of String)()
            Dim seen = New HashSet(Of String)(StringComparer.Ordinal)

            Dim parent = FindParent(root, containingObject)
            If parent IsNot Nothing AndAlso parent.Kind = JsonNodeKind.ArrayValue Then
                For Each sibling In parent.Children
                    If sibling.Kind <> JsonNodeKind.ObjectValue OrElse Object.ReferenceEquals(sibling, containingObject) Then
                        Continue For
                    End If

                    For Each child In sibling.Children
                        If Not existing.Contains(child.Key) AndAlso seen.Add(child.Key) Then
                            candidates.Add(child.Key)
                        End If
                    Next
                Next
            End If

            If Not String.IsNullOrWhiteSpace(schemaText) Then
                Try
                    Using schemaDocument = JsonDocument.Parse(schemaText, New JsonDocumentOptions With {.AllowTrailingCommas = False, .CommentHandling = JsonCommentHandling.Skip})
                        Dim schema = ResolveSchemaForPointer(schemaDocument.RootElement, schemaDocument.RootElement, containingObject.JsonPointer)
                        Dim propertiesElement As JsonElement = Nothing
                        If schema.HasValue AndAlso schema.Value.ValueKind = JsonValueKind.Object AndAlso
                           schema.Value.TryGetProperty("properties", propertiesElement) AndAlso propertiesElement.ValueKind = JsonValueKind.Object Then
                            For Each propertyItem In propertiesElement.EnumerateObject()
                                If Not existing.Contains(propertyItem.Name) AndAlso seen.Add(propertyItem.Name) Then
                                    candidates.Add(propertyItem.Name)
                                End If
                            Next
                        End If
                    End Using
                Catch ex As JsonException
                    ' An unparsable schema silently contributes no candidates.
                End Try
            End If

            candidates.Sort(StringComparer.Ordinal)
            Return candidates
        End Function

        ''' Walks the schema along the instance pointer via properties/items,
        ''' resolving local $ref on the way. Anything unsupported ends the walk.
        Private Function ResolveSchemaForPointer(rootSchema As JsonElement, schema As JsonElement, instancePointer As String) As JsonElement?
            Dim current = DerefLocal(rootSchema, schema, 0)
            If Not current.HasValue Then
                Return Nothing
            End If

            If String.IsNullOrEmpty(instancePointer) Then
                Return current
            End If

            For Each rawSegment In instancePointer.Substring(1).Split("/"c)
                If current.Value.ValueKind <> JsonValueKind.Object Then
                    Return Nothing
                End If

                Dim segment = rawSegment.Replace("~1", "/").Replace("~0", "~")
                Dim index As Integer
                Dim nextSchema As JsonElement = Nothing
                If Integer.TryParse(segment, Globalization.NumberStyles.None, Globalization.CultureInfo.InvariantCulture, index) AndAlso
                   current.Value.TryGetProperty("items", nextSchema) Then
                    ' Array index: descend into items.
                Else
                    Dim propertiesElement As JsonElement = Nothing
                    If Not current.Value.TryGetProperty("properties", propertiesElement) OrElse propertiesElement.ValueKind <> JsonValueKind.Object OrElse
                       Not propertiesElement.TryGetProperty(segment, nextSchema) Then
                        Return Nothing
                    End If
                End If

                current = DerefLocal(rootSchema, nextSchema, 0)
                If Not current.HasValue Then
                    Return Nothing
                End If
            Next

            Return current
        End Function

        Private Function DerefLocal(rootSchema As JsonElement, schema As JsonElement, depth As Integer) As JsonElement?
            If depth > MaxSchemaRefDepth Then
                Return Nothing
            End If

            If schema.ValueKind <> JsonValueKind.Object Then
                Return schema
            End If

            Dim refElement As JsonElement = Nothing
            If Not schema.TryGetProperty("$ref", refElement) OrElse refElement.ValueKind <> JsonValueKind.String Then
                Return schema
            End If

            Dim reference = If(refElement.GetString(), "")
            If Not reference.StartsWith("#", StringComparison.Ordinal) Then
                Return Nothing
            End If

            Dim current = rootSchema
            If Not String.Equals(reference, "#", StringComparison.Ordinal) Then
                If Not reference.StartsWith("#/", StringComparison.Ordinal) Then
                    Return Nothing
                End If

                For Each rawSegment In reference.Substring(2).Split("/"c)
                    Dim segment = rawSegment.Replace("~1", "/").Replace("~0", "~")
                    Dim child As JsonElement = Nothing
                    If current.ValueKind <> JsonValueKind.Object OrElse Not current.TryGetProperty(segment, child) Then
                        Return Nothing
                    End If

                    current = child
                Next
            End If

            Return DerefLocal(rootSchema, current, depth + 1)
        End Function

        Private Function FindParent(root As JsonTreeNode, target As JsonTreeNode) As JsonTreeNode
            If root Is Nothing OrElse target Is Nothing Then
                Return Nothing
            End If

            For Each child In root.Children
                If Object.ReferenceEquals(child, target) Then
                    Return root
                End If

                Dim found = FindParent(child, target)
                If found IsNot Nothing Then
                    Return found
                End If
            Next

            Return Nothing
        End Function
    End Class
End Namespace
