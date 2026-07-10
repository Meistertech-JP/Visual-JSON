' SPDX-License-Identifier: MPL-2.0

' RoutedCommand definitions and bindings for the File menu/toolbar (FR-13-204).
' The commands carry no input gestures on purpose: keyboard shortcuts stay under
' the central Window_KeyDown router, so command routing cannot double-fire them.
Partial Class MainWindow

#Region "Command Definitions"

    Public Shared ReadOnly NewFileCommand As New RoutedUICommand("New", NameOf(NewFileCommand), GetType(MainWindow))
    Public Shared ReadOnly OpenFileCommand As New RoutedUICommand("Open", NameOf(OpenFileCommand), GetType(MainWindow))
    Public Shared ReadOnly SaveFileCommand As New RoutedUICommand("Save", NameOf(SaveFileCommand), GetType(MainWindow))
    Public Shared ReadOnly SaveFileAsCommand As New RoutedUICommand("Save As", NameOf(SaveFileAsCommand), GetType(MainWindow))
    Public Shared ReadOnly ExitCommand As New RoutedUICommand("Exit", NameOf(ExitCommand), GetType(MainWindow))

#End Region

#Region "Command Handlers"

    Private Sub InitializeCommands()
        CommandBindings.Add(New CommandBinding(NewFileCommand, AddressOf NewCommand_Executed, AddressOf FileCommand_CanExecute))
        CommandBindings.Add(New CommandBinding(OpenFileCommand, AddressOf OpenCommand_Executed, AddressOf FileCommand_CanExecute))
        CommandBindings.Add(New CommandBinding(SaveFileCommand, AddressOf SaveCommand_Executed, AddressOf FileCommand_CanExecute))
        CommandBindings.Add(New CommandBinding(SaveFileAsCommand, AddressOf SaveAsCommand_Executed, AddressOf FileCommand_CanExecute))
        CommandBindings.Add(New CommandBinding(ExitCommand, AddressOf ExitCommand_Executed, AddressOf FileCommand_CanExecute))
    End Sub

    Private Sub FileCommand_CanExecute(sender As Object, e As CanExecuteRoutedEventArgs)
        ' File commands were always enabled before the command migration; IsBusy is
        ' False until busy states are introduced, so behavior is unchanged.
        e.CanExecute = Not _viewModel.IsBusy
    End Sub

    Private Sub NewCommand_Executed(sender As Object, e As ExecutedRoutedEventArgs)
        New_Click(sender, e)
    End Sub

    Private Sub OpenCommand_Executed(sender As Object, e As ExecutedRoutedEventArgs)
        Open_Click(sender, e)
    End Sub

    Private Sub SaveCommand_Executed(sender As Object, e As ExecutedRoutedEventArgs)
        Save_Click(sender, e)
    End Sub

    Private Sub SaveAsCommand_Executed(sender As Object, e As ExecutedRoutedEventArgs)
        SaveAs_Click(sender, e)
    End Sub

    Private Sub ExitCommand_Executed(sender As Object, e As ExecutedRoutedEventArgs)
        Exit_Click(sender, e)
    End Sub

#End Region

End Class
