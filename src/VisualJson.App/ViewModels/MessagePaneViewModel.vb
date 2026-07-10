' SPDX-License-Identifier: MPL-2.0
Imports System.Collections.ObjectModel
Imports VisualJson.Core.Diagnostics

Namespace ViewModels
    ''' Message-pane state for the Syntax/Schema/Conversion/Log tabs (FR-13-203).
    ''' Items reuse the Core diagnostic types directly; dedicated per-item view models
    ''' are deferred until the panes need display logic of their own.
    Public Class MessagePaneViewModel
        Public ReadOnly Property SyntaxDiagnostics As New ObservableCollection(Of ValidationDiagnostic)()
        Public ReadOnly Property SchemaDiagnostics As New ObservableCollection(Of ValidationDiagnostic)()
        Public ReadOnly Property ConversionDiagnostics As New ObservableCollection(Of String)()
        Public ReadOnly Property LogEntries As New ObservableCollection(Of String)()
    End Class
End Namespace
