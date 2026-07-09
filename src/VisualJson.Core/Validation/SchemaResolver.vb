' SPDX-License-Identifier: MPL-2.0
Imports System.IO
Imports System.Net
Imports System.Net.Http
Imports System.Net.Sockets
Imports System.Text
Imports System.Text.Json
Imports System.Threading
Imports System.Threading.Tasks

Namespace Validation
    ''' Resolves JSON Schema documents. Local files are preferred; external URLs are
    ''' fetched only when the user explicitly allows it, and only under SchemaUrlPolicy.
    Public Class SchemaResolver
        Private Const MaxRedirects As Integer = 5
        Private Const MaxSchemaBytes As Long = 8L * 1024L * 1024L

        Public Function LoadLocalSchema(path As String) As String
            If String.IsNullOrWhiteSpace(path) Then
                Throw New ArgumentException("A schema path is required.", NameOf(path))
            End If

            Dim text = File.ReadAllText(path, Encoding.UTF8)
            EnsureParsableJson(text)
            Return text
        End Function

        Public Async Function FetchExternalSchemaAsync(url As String, allowExternal As Boolean) As Task(Of String)
            Dim initialError = SchemaUrlPolicy.ValidateInitialUrl(url, allowExternal)
            If initialError IsNot Nothing Then
                Throw New InvalidOperationException(initialError)
            End If

            Dim current = New Uri(url, UriKind.Absolute)

            ' Every connection resolves DNS itself, validates ALL resolved addresses
            ' (including IPv4-mapped IPv6), and connects only to a validated address.
            ' This blocks public hostnames that resolve to private/local IPs and DNS
            ' rebinding between the check and the connect (NFR-SEC-007).
            Using handler = New SocketsHttpHandler With {
                .AllowAutoRedirect = False,
                .ConnectCallback = Function(context, cancellationToken) New ValueTask(Of Stream)(ConnectToValidatedAddressAsync(context, cancellationToken))
            }
                Using client = New HttpClient(handler) With {.Timeout = TimeSpan.FromSeconds(30)}
                    For hop = 0 To MaxRedirects
                        Using response = Await client.GetAsync(current).ConfigureAwait(False)
                            Dim status = CInt(response.StatusCode)
                            If status >= 300 AndAlso status <= 399 Then
                                Dim location = response.Headers.Location
                                If location Is Nothing Then
                                    Throw New InvalidOperationException("The schema server sent a redirect without a target.")
                                End If

                                Dim target = If(location.IsAbsoluteUri, location, New Uri(current, location))
                                Dim redirectError = SchemaUrlPolicy.ValidateRedirectTarget(target)
                                If redirectError IsNot Nothing Then
                                    Throw New InvalidOperationException($"Redirect blocked: {redirectError}")
                                End If

                                current = target
                                Continue For
                            End If

                            response.EnsureSuccessStatusCode()
                            If response.Content.Headers.ContentLength.HasValue AndAlso response.Content.Headers.ContentLength.Value > MaxSchemaBytes Then
                                Throw New InvalidOperationException("The schema document is too large.")
                            End If

                            Dim text = Await response.Content.ReadAsStringAsync().ConfigureAwait(False)
                            EnsureParsableJson(text)
                            Return text
                        End Using
                    Next
                End Using
            End Using

            Throw New InvalidOperationException("Too many schema redirects.")
        End Function

        Private Shared Async Function ConnectToValidatedAddressAsync(context As SocketsHttpConnectionContext, cancellationToken As CancellationToken) As Task(Of Stream)
            Dim host = context.DnsEndPoint.Host
            Dim addresses = Await Dns.GetHostAddressesAsync(host, cancellationToken).ConfigureAwait(False)

            Dim reason = SchemaUrlPolicy.ValidateResolvedAddresses(host, addresses)
            If reason IsNot Nothing Then
                Throw New InvalidOperationException($"Schema fetch blocked: {reason}")
            End If

            Dim socket = New Socket(SocketType.Stream, ProtocolType.Tcp) With {.NoDelay = True}
            Try
                Await socket.ConnectAsync(addresses, context.DnsEndPoint.Port, cancellationToken).ConfigureAwait(False)
                Return New NetworkStream(socket, ownsSocket:=True)
            Catch
                socket.Dispose()
                Throw
            End Try
        End Function

        Private Shared Sub EnsureParsableJson(text As String)
            Using JsonDocument.Parse(text, New JsonDocumentOptions With {
                .AllowTrailingCommas = False,
                .CommentHandling = JsonCommentHandling.Skip
            })
            End Using
        End Sub
    End Class
End Namespace
