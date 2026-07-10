# SPDX-License-Identifier: MPL-2.0
# Verifies the release zip against the v1.3 packaging rules (VRP-001..VRP-007).
# Default: checks the newest artifacts/visual-json-v*-win-x64.zip.
# -ZipPath: checks a specific zip (used by -SelfTest and CI).
# -SelfTest: builds four synthetic zips (valid / docs contamination / missing LICENSE /
#            missing CONTRIBUTING) and asserts the verifier passes and fails as expected.
#            This implements UT-13-PKG-001..004.
[CmdletBinding()]
param(
    [string]$ArtifactsDir = (Join-Path (Split-Path -Parent $PSScriptRoot) "artifacts"),
    [string]$ZipPath,
    [switch]$SelfTest
)

$ErrorActionPreference = "Stop"

$RequiredDocs = @(
    "README.md",
    "README.ja.md",
    "LICENSE",
    "NOTICE.md",
    "THIRD_PARTY_NOTICES.md",
    "CONTRIBUTING.md",
    "CHANGELOG.md",
    "RELEASE_NOTES.md"
)

function Test-ReleaseZip {
    param(
        [Parameter(Mandatory = $true)][string]$Zip,
        [switch]$SkipHashCheck
    )

    $checks = New-Object System.Collections.Generic.List[object]
    function Add-Check([string]$id, [bool]$pass, [string]$detail) {
        $checks.Add([pscustomobject]@{ Id = $id; Result = $(if ($pass) { "PASS" } else { "FAIL" }); Detail = $detail })
    }

    # VRP-001: zip exists
    $zipExists = Test-Path $Zip
    Add-Check "VRP-001 zip exists" $zipExists $Zip
    if (-not $zipExists) { return $checks }

    # VRP-002 / VRP-007: sibling .sha256 exists, hash matches content, name matches zip
    $shaPath = [System.IO.Path]::ChangeExtension($Zip, ".sha256")
    $shaExists = Test-Path $shaPath
    Add-Check "VRP-002 sha256 exists" $shaExists $shaPath
    if ($shaExists -and -not $SkipHashCheck) {
        $shaLine = (Get-Content $shaPath -TotalCount 1).Trim()
        $parts = $shaLine -split "\s+", 2
        $recordedHash = $parts[0]
        $recordedName = if ($parts.Count -gt 1) { $parts[1].Trim() } else { "" }
        $actualHash = (Get-FileHash -Path $Zip -Algorithm SHA256).Hash
        Add-Check "VRP-002 sha256 content matches" ($recordedHash -ieq $actualHash) "recorded=$recordedHash actual=$actualHash"
        Add-Check "VRP-007 sha256 filename matches zip" ($recordedName -ieq ([System.IO.Path]::GetFileName($Zip))) "recorded=$recordedName"
    }

    $archive = [System.IO.Compression.ZipFile]::OpenRead((Resolve-Path $Zip).Path)
    try {
        $entries = @($archive.Entries | ForEach-Object { $_.FullName.Replace("\", "/") })

        # VRP-003: docs/ must not be included
        $docsEntries = @($entries | Where-Object { $_ -match '^docs/' })
        Add-Check "VRP-003 no docs/ entries" ($docsEntries.Count -eq 0) "docs entries: $($docsEntries.Count)"

        # VRP-004: all required public documents present at zip root
        $missingDocs = @($RequiredDocs | Where-Object { $entries -notcontains $_ })
        Add-Check "VRP-004 required documents present" ($missingDocs.Count -eq 0) $(if ($missingDocs) { "missing: $($missingDocs -join ', ')" } else { "all $($RequiredDocs.Count) present" })

        # VRP-005: relative links inside the bundled READMEs resolve to zip entries
        $linkFailures = New-Object System.Collections.Generic.List[string]
        $linkCount = 0
        foreach ($readmeName in @("README.md", "README.ja.md")) {
            $entry = $archive.Entries | Where-Object { $_.FullName.Replace("\", "/") -eq $readmeName } | Select-Object -First 1
            if ($null -eq $entry) { continue }
            $reader = New-Object System.IO.StreamReader($entry.Open())
            try { $text = $reader.ReadToEnd() } finally { $reader.Dispose() }
            foreach ($m in [regex]::Matches($text, '\]\(([^)]+)\)')) {
                $target = $m.Groups[1].Value.Trim()
                if ($target -match '^(https?:|mailto:|#)') { continue }
                $target = ($target -split '[#?]')[0].TrimEnd('/')
                if ([string]::IsNullOrWhiteSpace($target)) { continue }
                $linkCount++
                $found = ($entries -contains $target) -or (@($entries | Where-Object { $_.StartsWith("$target/") }).Count -gt 0)
                if (-not $found) { $linkFailures.Add("${readmeName}: $target") }
            }
        }
        Add-Check "VRP-005 README links resolve inside zip" ($linkFailures.Count -eq 0) $(if ($linkFailures.Count -gt 0) { $linkFailures -join "; " } else { "$linkCount relative links checked" })

        # VRP-006: application executable present
        Add-Check "VRP-006 VisualJson.App.exe present" ($entries -contains "VisualJson.App.exe") ""
    }
    finally {
        $archive.Dispose()
    }

    return $checks
}

function Invoke-SelfTest {
    $tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("vjson-verify-selftest-" + [Guid]::NewGuid().ToString("N"))
    New-Item -ItemType Directory -Path $tempRoot | Out-Null
    try {
        function New-SyntheticZip {
            param([string]$Name, [string[]]$SkipDocs, [switch]$IncludeDocsFolder)
            $zipFile = Join-Path $tempRoot $Name
            $zip = [System.IO.Compression.ZipFile]::Open($zipFile, [System.IO.Compression.ZipArchiveMode]::Create)
            try {
                function Add-TextEntry($archive, [string]$entryName, [string]$content) {
                    $entry = $archive.CreateEntry($entryName)
                    $writer = New-Object System.IO.StreamWriter($entry.Open())
                    try { $writer.Write($content) } finally { $writer.Dispose() }
                }
                Add-TextEntry $zip "VisualJson.App.exe" "dummy-exe"
                foreach ($doc in $RequiredDocs) {
                    if ($SkipDocs -contains $doc) { continue }
                    if ($doc -eq "README.md") {
                        Add-TextEntry $zip $doc "See [LICENSE](LICENSE) and [NOTICE.md](NOTICE.md)."
                    }
                    else {
                        Add-TextEntry $zip $doc "dummy $doc"
                    }
                }
                if ($IncludeDocsFolder) {
                    Add-TextEntry $zip "docs/markdown/spec.md" "must not ship"
                }
            }
            finally { $zip.Dispose() }
            return $zipFile
        }

        $cases = @(
            @{ Id = "UT-13-PKG-004 valid zip verifies";        Zip = (New-SyntheticZip "valid.zip" @());                          ExpectPass = $true },
            @{ Id = "UT-13-PKG-001 docs contamination fails";  Zip = (New-SyntheticZip "docs-contaminated.zip" @() -IncludeDocsFolder); ExpectPass = $false },
            @{ Id = "UT-13-PKG-002 missing LICENSE fails";     Zip = (New-SyntheticZip "no-license.zip" @("LICENSE"));            ExpectPass = $false },
            @{ Id = "UT-13-PKG-003 missing CONTRIBUTING fails"; Zip = (New-SyntheticZip "no-contributing.zip" @("CONTRIBUTING.md")); ExpectPass = $false }
        )

        $failures = 0
        foreach ($case in $cases) {
            # Synthetic zips have no sha256 sibling; skip the hash checks so only
            # the content rules (VRP-003..006) drive the expected outcome.
            $checks = Test-ReleaseZip -Zip $case.Zip -SkipHashCheck
            $contentChecks = $checks | Where-Object { $_.Id -notmatch 'VRP-002|VRP-007' }
            $allPass = (@($contentChecks | Where-Object { $_.Result -eq "FAIL" }).Count -eq 0)
            $ok = ($allPass -eq $case.ExpectPass)
            $status = if ($ok) { "PASS" } else { "FAIL" }
            Write-Host ("SelfTest {0} : {1}" -f $status, $case.Id)
            if (-not $ok) {
                $failures++
                $contentChecks | ForEach-Object { Write-Host ("    {0} {1} {2}" -f $_.Result, $_.Id, $_.Detail) }
            }
        }

        if ($failures -gt 0) { throw "verify-release-package self-test failed: $failures case(s)." }
        Write-Host "SelfTest: all 4 cases behaved as expected."
    }
    finally {
        Remove-Item -Recurse -Force $tempRoot -ErrorAction SilentlyContinue
    }
}

if ($SelfTest) {
    Invoke-SelfTest
    exit 0
}

if (-not $ZipPath) {
    $candidate = Get-ChildItem (Join-Path $ArtifactsDir "visual-json-v*-win-x64.zip") -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($null -eq $candidate) {
        Write-Error "No visual-json-v*-win-x64.zip found under $ArtifactsDir (VRP-001)."
        exit 1
    }
    $ZipPath = $candidate.FullName
}

Write-Host "Verifying: $ZipPath"
$results = Test-ReleaseZip -Zip $ZipPath
$results | Format-Table -AutoSize | Out-String | Write-Host

$failCount = @($results | Where-Object { $_.Result -eq "FAIL" }).Count
if ($failCount -gt 0) {
    Write-Error "verify-release-package: $failCount check(s) failed."
    exit 1
}
Write-Host "verify-release-package: all checks passed."
exit 0
