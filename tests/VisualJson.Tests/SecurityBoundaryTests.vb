' SPDX-License-Identifier: MPL-2.0
Imports System.Diagnostics
Imports System.Net
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports VisualJson.Core.Validation

' v1.3 SSRF boundary additions (FR-13-601/602): CGNAT, IPv4 multicast/reserved,
' IPv6 unspecified/multicast, and embedded-IPv4 extraction for 6to4 and Teredo.
<TestClass>
Public Class SecurityBoundaryTests

    Private Shared Function ResolvedBlockReason(address As String) As String
        Return SchemaUrlPolicy.ValidateResolvedAddresses("example.test", {IPAddress.Parse(address)})
    End Function

    <TestMethod(DisplayName:="UT-13-SEC-001 CGNAT lower bound 100.64.0.1 is blocked")>
    Public Sub CgnatLowerBoundBlocked()
        AssertTrue(ResolvedBlockReason("100.64.0.1") IsNot Nothing, "100.64.0.1 blocked")
        AssertTrue(SchemaUrlPolicy.ValidateInitialUrl("https://100.64.0.1/schema.json", allowExternal:=True) IsNot Nothing, "literal CGNAT URL blocked")
    End Sub

    <TestMethod(DisplayName:="UT-13-SEC-002 CGNAT upper bound 100.127.255.255 is blocked")>
    Public Sub CgnatUpperBoundBlocked()
        AssertTrue(ResolvedBlockReason("100.127.255.255") IsNot Nothing, "100.127.255.255 blocked")
    End Sub

    <TestMethod(DisplayName:="UT-13-SEC-003 100.128.0.1 is outside CGNAT and stays allowed")>
    Public Sub BeyondCgnatFallsThroughToExistingRules()
        AssertTrue(ResolvedBlockReason("100.128.0.1") Is Nothing, "100.128.0.1 not blocked")
        AssertTrue(ResolvedBlockReason("100.63.255.255") Is Nothing, "100.63.255.255 not blocked")
    End Sub

    <TestMethod(DisplayName:="UT-13-SEC-004 IPv4-mapped IPv6 loopback stays blocked")>
    Public Sub MappedLoopbackBlocked()
        AssertTrue(ResolvedBlockReason("::ffff:127.0.0.1") IsNot Nothing, "mapped loopback blocked")
        AssertTrue(ResolvedBlockReason("::ffff:100.64.0.1") IsNot Nothing, "mapped CGNAT blocked")
    End Sub

    <TestMethod(DisplayName:="UT-13-SEC-005 6to4 addresses embedding private IPv4 are blocked")>
    Public Sub SixToFourEmbeddedPrivateBlocked()
        ' 2002:0a00:0001:: embeds 10.0.0.1
        AssertTrue(ResolvedBlockReason("2002:0a00:0001::") IsNot Nothing, "6to4 private blocked")
        ' 2002:c0a8:0101:: embeds 192.168.1.1
        AssertTrue(ResolvedBlockReason("2002:c0a8:0101::") IsNot Nothing, "6to4 rfc1918 blocked")
        ' 2002:0102:0304:: embeds 1.2.3.4 (public) and stays allowed
        AssertTrue(ResolvedBlockReason("2002:0102:0304::") Is Nothing, "6to4 public allowed")
    End Sub

    <TestMethod(DisplayName:="UT-13-SEC-006 Teredo addresses embedding private IPv4 are blocked")>
    Public Sub TeredoEmbeddedPrivateBlocked()
        ' RFC 4380: the client IPv4 lives in the last 4 bytes, each XORed with 0xFF.
        ' 10.0.0.1 -> F5.FF.FF.FE
        AssertTrue(ResolvedBlockReason("2001:0:1:2:3:4:f5ff:fffe") IsNot Nothing, "teredo 10.0.0.1 blocked")
        ' 192.168.1.1 -> 3F.57.FE.FE
        AssertTrue(ResolvedBlockReason("2001:0:1:2:3:4:3f57:fefe") IsNot Nothing, "teredo 192.168.1.1 blocked")
        ' 1.2.3.4 -> FE.FD.FC.FB (public) stays allowed
        AssertTrue(ResolvedBlockReason("2001:0:1:2:3:4:fefd:fcfb") Is Nothing, "teredo public allowed")
        ' 2001:db8::/32 documentation range is NOT Teredo (2001:0::/32) and must not be decoded
        AssertTrue(ResolvedBlockReason("2001:db8::f5ff:fffe") Is Nothing, "2001:db8 not treated as teredo")
    End Sub

    <TestMethod(DisplayName:="UT-13-SEC-007 disabled external schema short-circuits before DNS")>
    Public Sub DisabledExternalSchemaDoesNotTouchNetwork()
        ' The OFF gate must reject before any DNS/HTTP work: the reason is the
        ' disabled message (not a resolution error) and the call returns instantly
        ' even for an unresolvable host.
        Dim reason = SchemaUrlPolicy.ValidateInitialUrl("https://no-such-host.invalid/schema.json", allowExternal:=False)
        AssertContains(reason, "disabled", "off gate reason")

        Dim resolver = New SchemaResolver()
        Dim watch = Stopwatch.StartNew()
        Dim thrown As Exception = Nothing
        Try
            resolver.FetchExternalSchemaAsync("https://no-such-host.invalid/schema.json", allowExternal:=False).GetAwaiter().GetResult()
        Catch ex As Exception
            thrown = ex
        End Try
        watch.Stop()

        AssertTrue(thrown IsNot Nothing, "fetch rejected")
        AssertContains(thrown.Message, "disabled", "rejected by the off gate, not by name resolution")
        AssertTrue(watch.ElapsedMilliseconds < 1000, "short-circuits without network work")

        ' IPv6 additions ride the same guard: unspecified and multicast are blocked.
        AssertTrue(ResolvedBlockReason("::") IsNot Nothing, "unspecified blocked")
        AssertTrue(ResolvedBlockReason("ff02::1") IsNot Nothing, "multicast blocked")
        AssertTrue(ResolvedBlockReason("224.0.0.1") IsNot Nothing, "ipv4 multicast blocked")
        AssertTrue(ResolvedBlockReason("240.0.0.1") IsNot Nothing, "ipv4 reserved blocked")
    End Sub

End Class
