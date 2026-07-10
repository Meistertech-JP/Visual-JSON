' SPDX-License-Identifier: MPL-2.0
Imports Microsoft.VisualStudio.TestTools.UnitTesting

' Legacy assert helpers kept as a compatibility shim (FR-13-404): the 82 migrated test
' bodies are byte-identical, so failures must keep the same inputs and expectations
' while surfacing through the MSTest assertion pipeline.
Friend Module TestAssert

    Friend Sub AssertTrue(condition As Boolean, message As String)
        Assert.IsTrue(condition, message)
    End Sub

    Friend Sub AssertFalse(condition As Boolean, message As String)
        Assert.IsFalse(condition, message)
    End Sub

    Friend Sub AssertEqual(Of T)(expected As T, actual As T, message As String)
        Assert.AreEqual(Of T)(expected, actual, message)
    End Sub

    Friend Sub AssertContains(value As String, expectedSubstring As String, message As String)
        Assert.IsNotNull(value, message)
        StringAssert.Contains(value, expectedSubstring, message)
    End Sub
End Module
