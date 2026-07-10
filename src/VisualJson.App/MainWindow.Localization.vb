' SPDX-License-Identifier: MPL-2.0
Imports VisualJson.Core.Parsing

' Localization: LocalText and ApplyLanguage for the EN/JA UI switch (FR-13-103).
Partial Class MainWindow

#Region "Event Handlers"

    Private Sub LanguageCombo_SelectionChanged(sender As Object, e As SelectionChangedEventArgs)
        Dim combo = TryCast(sender, ComboBox)
        Dim item = TryCast(combo?.SelectedItem, ComboBoxItem)
        If item IsNot Nothing Then
            _language = If(TryCast(item.Tag, String), "en")
            _settings.Language = _language
        End If

        ApplyLanguage()
        SaveSettings()
    End Sub

#End Region

#Region "Private Helpers"

    Private Function LocalText(english As String, japanese As String) As String
        If String.Equals(_language, "ja", StringComparison.OrdinalIgnoreCase) Then
            Return japanese
        End If

        Return english
    End Function

    Private Function GetFormatLabel(format As JsonInputFormat) As String
        Select Case format
            Case JsonInputFormat.JsonC
                Return "JSONC"
            Case JsonInputFormat.Json5
                Return "JSON5"
            Case JsonInputFormat.JsonLines
                Return "JSON Lines"
            Case Else
                Return "JSON"
        End Select
    End Function

    Private Sub ApplyLanguage()
        If FileMenu Is Nothing OrElse ActionHeader Is Nothing Then
            Return
        End If

        FileMenu.Header = LocalText("_File", "ファイル(_F)")
        NewMenuItem.Header = LocalText("_New", "新規(_N)")
        OpenMenuItem.Header = LocalText("_Open...", "開く(_O)...")
        RecentFilesMenuItem.Header = LocalText("_Recent", "最近使ったファイル(_R)")
        SaveMenuItem.Header = LocalText("_Save", "保存(_S)")
        SaveAsMenuItem.Header = LocalText("Save _As...", "名前を付けて保存(_A)...")
        RecoverMenuItem.Header = LocalText("_Recover Latest Snapshot", "最新スナップショットを復元(_R)")
        SettingsMenuItem.Header = LocalText("S_ettings...", "設定(_E)...")
        ExitMenuItem.Header = LocalText("E_xit", "終了(_X)")

        EditMenu.Header = LocalText("_Edit", "編集(_E)")
        ValidateMenuItem.Header = LocalText("_Validate and Sync Text to Grid", "検証してテキストをグリッドへ同期(_V)")
        ApplyGridMenuItem.Header = LocalText("_Apply Grid to Text", "グリッドをテキストへ反映(_A)")
        FormatMenuItem.Header = LocalText("_Format JSON", "JSON整形(_F)")
        UndoGridMenuItem.Header = LocalText("_Undo Grid Operation", "グリッド操作を元に戻す(_U)")
        RedoGridMenuItem.Header = LocalText("_Redo Grid Operation", "グリッド操作をやり直す(_R)")
        FindNextMenuItem.Header = LocalText("_Find Next", "次を検索(_F)")
        ReplaceMenuItem.Header = LocalText("_Replace", "置換(_R)")

        ViewMenu.Header = LocalText("_View", "表示(_V)")
        TextModeMenuItem.Header = LocalText("_Text Mode", "テキストモード(_T)")
        GridModeMenuItem.Header = LocalText("_Grid Mode", "グリッドモード(_G)")
        SchemaMenu.Header = LocalText("_Schema", "スキーマ(_S)")
        LoadSchemaMenuItem.Header = LocalText("_Load Local Schema...", "ローカルSchemaを読み込む(_L)...")
        LoadSchemaFromUrlMenuItem.Header = LocalText("Load Schema from $schema _URL...", "$schema URLからSchemaを取得(_U)...")
        ClearSchemaMenuItem.Header = LocalText("_Clear Schema", "Schemaを解除(_C)")
        ValidateSchemaMenuItem.Header = LocalText("_Validate with Schema", "Schemaで検証(_V)")
        AllowExternalSchemaMenuItem.Header = LocalText("Allow _External HTTPS Schema (off by default)", "外部HTTPS Schemaを許可(既定OFF)(_E)")
        ConvertMenu.Header = LocalText("_Convert", "変換(_C)")
        ExportXmlMenuItem.Header = LocalText("Export as _XML...", "XMLへExport(_X)...")
        ExportYamlMenuItem.Header = LocalText("Export as _YAML...", "YAMLへExport(_Y)...")
        ImportXmlMenuItem.Header = LocalText("Open XML as _JSON...", "XMLをJSONとして開く(_J)...")
        ImportYamlMenuItem.Header = LocalText("Open YAML as JS_ON...", "YAMLをJSONとして開く(_O)...")
        HelpMenu.Header = LocalText("_Help", "ヘルプ(_H)")
        CopyDiagnosticsMenuItem.Header = LocalText("_Copy Diagnostics", "診断情報をコピー(_C)")
        AboutMenuItem.Header = LocalText("_About Visual JSON", "Visual JSONについて(_A)")

        NewButton.Content = LocalText("New", "新規")
        OpenButton.Content = LocalText("Open", "開く")
        SaveButton.Content = LocalText("Save", "保存")
        SaveAsButton.Content = LocalText("Save As", "別名保存")
        ValidateButton.Content = LocalText("Validate", "検証")
        FormatButton.Content = LocalText("Format", "整形")
        ApplyGridButton.Content = LocalText("Apply Grid", "反映")
        UndoGridButton.Content = LocalText("Undo", "元に戻す")
        RedoGridButton.Content = LocalText("Redo", "やり直し")
        DiagnosticsButton.Content = LocalText("Diagnostics", "診断")
        LanguageLabel.Text = LocalText("Language", "言語")

        TextTab.Header = LocalText("Text", "テキスト")
        GridTab.Header = LocalText("Grid", "グリッド")
        SyntaxTab.Header = LocalText("Syntax", "構文")
        LogTab.Header = LocalText("Log", "ログ")
        SchemaResultTab.Header = LocalText("Schema", "スキーマ")
        ConversionTab.Header = LocalText("Conversion", "変換")
        SyntaxSeverityColumn.Header = LocalText("Severity", "重大度")
        SyntaxCodeColumn.Header = LocalText("Code", "コード")
        SyntaxLineColumn.Header = LocalText("Line", "行")
        SyntaxColumnColumn.Header = LocalText("Column", "列")
        SyntaxMessageColumn.Header = LocalText("Message", "メッセージ")
        SchemaSeverityColumn.Header = LocalText("Severity", "重大度")
        SchemaCodeColumn.Header = LocalText("Code", "コード")
        SchemaLineColumn.Header = LocalText("Line", "行")
        SchemaPointerColumn.Header = LocalText("Pointer", "ポインタ")
        SchemaPathColumn.Header = LocalText("SchemaPath", "Schemaパス")
        SchemaMessageColumn.Header = LocalText("Message", "メッセージ")
        ShowSchemaDefinitionButton.Content = LocalText("Show Schema Definition", "Schema定義を表示")
        UpdateSchemaStatus()
        FindLabel.Text = LocalText("Find", "検索")
        FindNextButton.Content = LocalText("Next", "次")
        FindPrevButton.Content = LocalText("Prev", "前")
        ReplaceLabel.Text = LocalText("Replace", "置換")
        ReplaceButton.Content = LocalText("Replace", "置換")
        ReplaceAllButton.Content = LocalText("All", "すべて")
        CaseSensitiveBox.Content = LocalText("Aa", "Aa")
        RegexSearchBox.Content = LocalText(".*", ".*")
        AutoPairBox.Content = LocalText("Pairs", "補完")
        GridFilterLabel.Text = LocalText("Filter", "フィルタ")
        ClearFilterButton.Content = LocalText("Clear", "クリア")
        GripHeader.Text = LocalText("Grip", "移動")
        KeyHeader.Text = LocalText("Key", "キー")
        ValueHeader.Text = LocalText("Value", "値")
        TypeHeader.Text = LocalText("Type", "型")
        PathHeader.Text = LocalText("Path", "パス")
        ActionHeader.Text = LocalText("Action", "操作")
        AddChildActionText = LocalText("+ Child", "+ 子")
        AddSiblingActionText = LocalText("+ Row", "+ 行")
        DeleteActionText = LocalText("Del", "削除")
        MoveUpActionText = LocalText("Up", "上")
        MoveDownActionText = LocalText("Down", "下")
        DragGripToolTipText = LocalText("Drag", "ドラッグ")
        TableActionText = LocalText("Table", "テーブル")
        TableBackButton.Content = LocalText("< List", "< リスト")
        TableAddRowButton.Content = LocalText("+ Row", "+ 行")
        TableAddColumnButton.Content = LocalText("+ Column", "+ 列")
        TableApplySortButton.Content = LocalText("Apply to Structure", "構造へ反映")
        TableHelpButton.Content = LocalText("Help", "ヘルプ")
        UpdateTableSubjectText()
        RefreshRecentFilesMenu()
    End Sub

#End Region

End Class
