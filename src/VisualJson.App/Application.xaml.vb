' SPDX-License-Identifier: MPL-2.0
Imports System.Windows
Imports System.Windows.Threading

Class Application
    Private Sub Application_Startup(sender As Object, e As StartupEventArgs)
        AddHandler DispatcherUnhandledException, AddressOf Application_DispatcherUnhandledException
        AddHandler AppDomain.CurrentDomain.UnhandledException, AddressOf CurrentDomain_UnhandledException

        Dim window = New MainWindow()
        window.Show()

        If e.Args IsNot Nothing AndAlso e.Args.Length > 0 Then
            window.OpenPathForStartup(e.Args(0))
        End If
    End Sub

    Private Sub Application_DispatcherUnhandledException(sender As Object, e As DispatcherUnhandledExceptionEventArgs)
        WriteCrashLog(e.Exception)
    End Sub

    Private Sub CurrentDomain_UnhandledException(sender As Object, e As UnhandledExceptionEventArgs)
        Dim ex = TryCast(e.ExceptionObject, Exception)
        If ex IsNot Nothing Then
            WriteCrashLog(ex)
        End If
    End Sub

    Private Shared Sub WriteCrashLog(ex As Exception)
        Try
            Dim path = IO.Path.Combine(IO.Path.GetTempPath(), "VisualJson.lastcrash.log")
            IO.File.WriteAllText(path, ex.ToString())
        Catch
            ' IgnoreWithReason (spec 06 §3.1): a secondary failure while writing the
            ' crash log must not mask the original crash being reported.
        End Try
    End Sub
End Class
