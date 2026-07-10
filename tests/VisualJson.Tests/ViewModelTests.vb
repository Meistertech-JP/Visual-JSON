' SPDX-License-Identifier: MPL-2.0
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports VisualJson.App.ViewModels
Imports VisualJson.Core.Diagnostics

<TestClass>
Public Class ViewModelTests

    <TestMethod(DisplayName:="UT-13-VM-001 MainViewModel exposes sane initial state")>
    Public Sub MainViewModelInitialState()
        Dim vm = New MainViewModel()

        AssertTrue(vm.ActiveDocument IsNot Nothing, "active document")
        AssertTrue(vm.Messages IsNot Nothing, "messages")
        AssertEqual("Not checked", vm.StatusText, "status text default")
        AssertEqual("Text", vm.CurrentMode, "mode default")
        AssertFalse(vm.IsBusy, "not busy by default")
        AssertEqual("", vm.SelectedJsonPointer, "root pointer default")
        AssertEqual("Pointer: (root)", vm.PointerDisplayText, "pointer display default")
        AssertEqual("Untitled", vm.ActiveDocument.DisplayFilePath, "file display default")
        AssertEqual("Saved", vm.ActiveDocument.DirtyStatusDisplay, "dirty display default")
        AssertEqual("UTF-8", vm.ActiveDocument.EncodingName, "encoding default")
        AssertEqual("LF", vm.ActiveDocument.NewLineName, "newline default")
        AssertEqual("0 nodes", vm.ActiveDocument.NodeCountDisplay, "node count default")
        AssertEqual(0, vm.Messages.SyntaxDiagnostics.Count, "no syntax diagnostics")
        AssertEqual(0, vm.Messages.SchemaDiagnostics.Count, "no schema diagnostics")
        AssertEqual(0, vm.Messages.ConversionDiagnostics.Count, "no conversion diagnostics")
        AssertEqual(0, vm.Messages.LogEntries.Count, "no log entries")
    End Sub

    <TestMethod(DisplayName:="UT-13-VM-002 DocumentViewModel text change marks the document dirty")>
    Public Sub DocumentTextChangeSetsDirty()
        Dim document = New DocumentViewModel()
        AssertFalse(document.IsDirty, "clean before edit")

        document.Text = "{""a"":1}"

        AssertTrue(document.IsDirty, "dirty after text change")
        AssertEqual("Unsaved", document.DirtyStatusDisplay, "dirty display")

        document.IsDirty = False
        document.Text = "{""a"":1}"
        AssertFalse(document.IsDirty, "same text does not re-dirty")
    End Sub

    <TestMethod(DisplayName:="UT-13-VM-003 DocumentViewModel file path drives the display name")>
    Public Sub DocumentFilePathUpdatesDisplayName()
        Dim document = New DocumentViewModel()
        Dim changed = New List(Of String)()
        AddHandler document.PropertyChanged, Sub(sender, e) changed.Add(e.PropertyName)

        document.CurrentFilePath = "C:\data\sample.json"

        AssertEqual("sample.json", document.DisplayFileName, "display file name")
        AssertEqual("C:\data\sample.json", document.DisplayFilePath, "display file path")
        AssertTrue(changed.Contains(NameOf(DocumentViewModel.DisplayFileName)), "display name change raised")
        AssertTrue(changed.Contains(NameOf(DocumentViewModel.DisplayFilePath)), "display path change raised")
    End Sub

    <TestMethod(DisplayName:="UT-13-VM-004 MessagePaneViewModel receives syntax diagnostics")>
    Public Sub MessagePaneReceivesSyntaxDiagnostics()
        Dim messages = New MessagePaneViewModel()

        messages.SyntaxDiagnostics.Add(New ValidationDiagnostic("Error", "unexpected token", errorCode:="SYN-TEST"))

        AssertEqual(1, messages.SyntaxDiagnostics.Count, "diagnostic added")
        AssertEqual("SYN-TEST", messages.SyntaxDiagnostics(0).ErrorCode, "diagnostic content preserved")
    End Sub

    <TestMethod(DisplayName:="UT-13-VM-005 SelectedJsonPointer raises PropertyChanged")>
    Public Sub SelectedPointerRaisesPropertyChanged()
        Dim vm = New MainViewModel()
        Dim changed = New List(Of String)()
        AddHandler vm.PropertyChanged, Sub(sender, e) changed.Add(e.PropertyName)

        vm.SelectedJsonPointer = "/items/3/name"

        AssertTrue(changed.Contains(NameOf(MainViewModel.SelectedJsonPointer)), "pointer change raised")
        AssertTrue(changed.Contains(NameOf(MainViewModel.PointerDisplayText)), "display change raised")
        AssertEqual("Pointer: /items/3/name", vm.PointerDisplayText, "pointer display")

        changed.Clear()
        vm.SelectedJsonPointer = "/items/3/name"
        AssertEqual(0, changed.Count, "no event for identical value")
    End Sub

End Class
