' SPDX-License-Identifier: MPL-2.0
Namespace ViewModels
    Public Class MainViewModel
        Inherits ObservableObject

        Public Sub New()
            ActiveDocument = New DocumentViewModel()
        End Sub

        Public ReadOnly Property ActiveDocument As DocumentViewModel
    End Class
End Namespace
