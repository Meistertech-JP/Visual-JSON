' SPDX-License-Identifier: MPL-2.0
Imports System.Net

Namespace Validation
    ''' Security policy for external JSON Schema references (NFR-SEC-005/006/007).
    ''' External references are OFF by default; when the user opts in, only HTTPS is
    ''' allowed and redirects to HTTP, file, UNC, localhost, or private IP ranges are blocked.
    Public NotInheritable Class SchemaUrlPolicy
        Private Sub New()
        End Sub

        ''' Returns Nothing when the initial URL may be fetched, otherwise a human-readable reason.
        Public Shared Function ValidateInitialUrl(url As String, allowExternal As Boolean) As String
            If Not allowExternal Then
                Return "External schema references are disabled by default."
            End If

            Dim uri As Uri = Nothing
            If Not Uri.TryCreate(url, UriKind.Absolute, uri) Then
                Return "The schema URL is not a valid absolute URL."
            End If

            Return DescribeBlockedTarget(uri)
        End Function

        ''' Returns Nothing when the redirect target may be followed, otherwise the block reason.
        Public Shared Function ValidateRedirectTarget(target As Uri) As String
            If target Is Nothing Then
                Return "The redirect target is missing."
            End If

            Return DescribeBlockedTarget(target)
        End Function

        ''' Validates every DNS-resolved address for a schema host. Blocking any private,
        ''' loopback, or link-local result (including IPv4-mapped IPv6) closes the gap where
        ''' a public hostname resolves to an internal address (NFR-SEC-007).
        Public Shared Function ValidateResolvedAddresses(host As String, addresses As IEnumerable(Of IPAddress)) As String
            Dim resolved = If(addresses, Array.Empty(Of IPAddress)()).ToList()
            If resolved.Count = 0 Then
                Return $"The schema host '{host}' did not resolve to any address."
            End If

            For Each address In resolved
                If IsPrivateOrLocalAddress(address) Then
                    Return $"The schema host '{host}' resolves to a private or local address ({address})."
                End If
            Next

            Return Nothing
        End Function

        Private Shared Function DescribeBlockedTarget(uri As Uri) As String
            If Not String.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) Then
                Return $"Only HTTPS schema URLs are allowed. Blocked scheme: {uri.Scheme}"
            End If

            If uri.IsUnc Then
                Return "UNC paths are blocked for schema references."
            End If

            Dim host = uri.Host
            If String.IsNullOrWhiteSpace(host) Then
                Return "The schema URL has no host."
            End If

            If IsLocalhost(host) Then
                Return "Localhost schema URLs are blocked."
            End If

            Dim address As IPAddress = Nothing
            If IPAddress.TryParse(host.Trim("["c, "]"c), address) AndAlso IsPrivateOrLocalAddress(address) Then
                Return $"Private or local IP addresses are blocked: {host}"
            End If

            Return Nothing
        End Function

        Private Shared Function IsLocalhost(host As String) As Boolean
            Return String.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) OrElse
                host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase)
        End Function

        Private Shared Function IsPrivateOrLocalAddress(rawAddress As IPAddress) As Boolean
            ' Normalize IPv4-mapped IPv6 (::ffff:192.168.0.1) so the IPv4 range checks apply.
            Dim address = If(rawAddress.IsIPv4MappedToIPv6, rawAddress.MapToIPv4(), rawAddress)

            If IPAddress.IsLoopback(address) Then
                Return True
            End If

            If address.AddressFamily = Sockets.AddressFamily.InterNetwork Then
                Dim bytes = address.GetAddressBytes()
                ' 10.0.0.0/8, 172.16.0.0/12, 192.168.0.0/16, 169.254.0.0/16 (link-local), 0.0.0.0/8
                Return bytes(0) = 10 OrElse
                    (bytes(0) = 172 AndAlso bytes(1) >= 16 AndAlso bytes(1) <= 31) OrElse
                    (bytes(0) = 192 AndAlso bytes(1) = 168) OrElse
                    (bytes(0) = 169 AndAlso bytes(1) = 254) OrElse
                    bytes(0) = 0
            End If

            If address.AddressFamily = Sockets.AddressFamily.InterNetworkV6 Then
                ' fc00::/7 unique local, fe80::/10 link-local
                Dim bytes = address.GetAddressBytes()
                Return (bytes(0) And &HFE) = &HFC OrElse
                    (bytes(0) = &HFE AndAlso (bytes(1) And &HC0) = &H80) OrElse
                    address.IsIPv6LinkLocal OrElse
                    address.IsIPv6SiteLocal
            End If

            Return False
        End Function
    End Class
End Namespace
