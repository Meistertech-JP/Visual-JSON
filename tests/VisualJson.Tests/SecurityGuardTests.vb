' SPDX-License-Identifier: MPL-2.0
Imports System.IO
Imports System.Diagnostics
Imports System.Text
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports VisualJson.Core.Conversion
Imports VisualJson.Core.Infrastructure
Imports VisualJson.Core.Models
Imports VisualJson.Core.Parsing
Imports VisualJson.Core.Serialization
Imports VisualJson.Core.Services
Imports VisualJson.Core.Validation

<TestClass>
Public Class SecurityGuardTests

    <TestMethod(DisplayName:="UT-M2-004 HTTP schema URL is blocked")>
    Public Sub HttpSchemaUrlIsBlocked()
        AssertTrue(SchemaUrlPolicy.ValidateInitialUrl("https://example.com/schema.json", allowExternal:=False) IsNot Nothing, "external references are OFF by default")
        AssertTrue(SchemaUrlPolicy.ValidateInitialUrl("http://example.com/schema.json", allowExternal:=True) IsNot Nothing, "http is blocked even when external is allowed")
        AssertTrue(SchemaUrlPolicy.ValidateInitialUrl("https://example.com/schema.json", allowExternal:=True) Is Nothing, "https to a public host is allowed when opted in")
    End Sub

    <TestMethod(DisplayName:="UT-M2-005 dangerous schema redirects are blocked")>
    Public Sub DangerousSchemaRedirectsAreBlocked()
        AssertTrue(SchemaUrlPolicy.ValidateRedirectTarget(New Uri("http://example.com/s.json")) IsNot Nothing, "redirect to http blocked")
        AssertTrue(SchemaUrlPolicy.ValidateRedirectTarget(New Uri("file:///c:/schema.json")) IsNot Nothing, "redirect to file blocked")
        AssertTrue(SchemaUrlPolicy.ValidateRedirectTarget(New Uri("file://server/share/schema.json")) IsNot Nothing, "redirect to UNC blocked")
        AssertTrue(SchemaUrlPolicy.ValidateRedirectTarget(New Uri("https://localhost/s.json")) IsNot Nothing, "redirect to localhost blocked")
        AssertTrue(SchemaUrlPolicy.ValidateRedirectTarget(New Uri("https://127.0.0.1/s.json")) IsNot Nothing, "redirect to loopback blocked")
        AssertTrue(SchemaUrlPolicy.ValidateRedirectTarget(New Uri("https://10.0.0.5/s.json")) IsNot Nothing, "redirect to 10/8 blocked")
        AssertTrue(SchemaUrlPolicy.ValidateRedirectTarget(New Uri("https://172.20.1.1/s.json")) IsNot Nothing, "redirect to 172.16/12 blocked")
        AssertTrue(SchemaUrlPolicy.ValidateRedirectTarget(New Uri("https://192.168.1.1/s.json")) IsNot Nothing, "redirect to 192.168/16 blocked")
        AssertTrue(SchemaUrlPolicy.ValidateRedirectTarget(New Uri("https://169.254.0.9/s.json")) IsNot Nothing, "redirect to link-local blocked")
        AssertTrue(SchemaUrlPolicy.ValidateRedirectTarget(New Uri("https://[fe80::1]/s.json")) IsNot Nothing, "redirect to IPv6 link-local blocked")
        AssertTrue(SchemaUrlPolicy.ValidateRedirectTarget(New Uri("https://[fc00::1]/s.json")) IsNot Nothing, "redirect to IPv6 unique-local blocked")
        AssertTrue(SchemaUrlPolicy.ValidateRedirectTarget(New Uri("https://example.com/s.json")) Is Nothing, "redirect to public https allowed")
    End Sub

    <TestMethod(DisplayName:="DNS-resolved private and mapped addresses are blocked")>
    Public Sub DnsResolvedPrivateAddressesAreBlocked()
        Dim blockedSingles = {
            "127.0.0.1", "10.0.0.5", "172.20.1.1", "192.168.1.1", "169.254.0.9",
            "::1", "fe80::1", "fc00::1",
            "::ffff:127.0.0.1", "::ffff:192.168.1.1", "::ffff:10.1.2.3"
        }

        For Each candidate In blockedSingles
            Dim addresses = {Net.IPAddress.Parse(candidate)}
            AssertTrue(SchemaUrlPolicy.ValidateResolvedAddresses("example.com", addresses) IsNot Nothing, $"resolved {candidate} must be blocked")
        Next

        Dim publicAddresses = {Net.IPAddress.Parse("93.184.216.34"), Net.IPAddress.Parse("2606:2800:220:1:248:1893:25c8:1946")}
        AssertTrue(SchemaUrlPolicy.ValidateResolvedAddresses("example.com", publicAddresses) Is Nothing, "public resolved addresses are allowed")

        Dim mixed = {Net.IPAddress.Parse("93.184.216.34"), Net.IPAddress.Parse("192.168.0.10")}
        AssertTrue(SchemaUrlPolicy.ValidateResolvedAddresses("example.com", mixed) IsNot Nothing, "any private address in the resolution set blocks the fetch")

        AssertTrue(SchemaUrlPolicy.ValidateResolvedAddresses("example.com", Array.Empty(Of Net.IPAddress)()) IsNot Nothing, "empty resolution set is blocked")
    End Sub
End Class
