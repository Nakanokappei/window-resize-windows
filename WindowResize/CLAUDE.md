# Window Resize - Windows Tray App

## Overview
Windows で動作中のアプリウィンドウを既定サイズにリサイズするタスクトレイ常駐アプリ。
macOS 版 (Window Resize) の Windows 移植。

## Tech Stack
- **Language:** C# (.NET 8, WinForms)
- **Build:** `dotnet publish` でMac上からクロスコンパイル
- **配布:** 自己完結型の単一exe（.NETランタイム不要）
- **ウィンドウ操作:** Win32 API (P/Invoke) — `EnumWindows`, `SetWindowPos`

## Build & Run
```bash
# dotnet パス (Homebrew)
DOTNET=/opt/homebrew/Cellar/dotnet@8/8.0.123/libexec/dotnet

# デバッグビルド（macOS上でクロスコンパイル）
$DOTNET build WindowResize.csproj

# Windows向けリリースビルド（単一exe、自己完結型）
$DOTNET publish WindowResize.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true

# 出力先
# bin/Release/net8.0-windows/win-x64/publish/WindowResize.exe
```

## Project Structure
```
WindowResize/
├── CLAUDE.md                          # このファイル
├── WindowResize.csproj                # プロジェクト設定
├── Program.cs                         # エントリポイント
├── TrayApplicationContext.cs          # NotifyIcon・メニュー構築・リサイズ実行
├── WindowManager.cs                   # Win32 P/Invoke（ウィンドウ列挙・リサイズ）
├── PresetSize.cs                      # サイズモデル
├── SettingsStore.cs                   # JSON永続化・レジストリ自動起動
├── SettingsForm.cs                    # WinForms設定画面
├── ScreenshotHelper.cs               # スクリーンショットキャプチャ (PrintWindow API)
└── Resources/
    ├── Strings.resx                   # 英語（デフォルト）
    ├── Strings.Designer.cs            # 自動生成リソースクラス
    ├── Strings.zh-Hans.resx           # 簡体中文
    ├── Strings.es.resx                # スペイン語
    ├── Strings.hi.resx                # ヒンディー語
    ├── Strings.ar.resx                # アラビア語
    ├── Strings.id.resx                # インドネシア語
    ├── Strings.pt.resx                # ポルトガル語
    ├── Strings.fr.resx                # フランス語
    ├── Strings.ja.resx                # 日本語
    ├── Strings.ru.resx                # ロシア語
    ├── Strings.de.resx                # ドイツ語
    ├── Strings.vi.resx                # ベトナム語
    ├── Strings.th.resx                # タイ語
    ├── Strings.ko.resx                # 韓国語
    ├── Strings.it.resx                # イタリア語
    └── Strings.zh-Hant.resx           # 繁体中文
```

## Key Architecture Decisions

### macOS版との差異
| 項目 | macOS | Windows |
|------|-------|---------|
| トレイ | NSStatusItem | NotifyIcon |
| メニュー | NSMenu | ContextMenuStrip |
| ウィンドウ列挙 | CGWindowListCopyWindowInfo | EnumWindows (P/Invoke) |
| リサイズ | AXUIElement | SetWindowPos (P/Invoke) |
| 権限 | Accessibility権限必要 | 不要 |
| 設定保存 | UserDefaults | JSON in AppData |
| 自動起動 | SMAppService | Registry Run key |
| スクリーンショット | SCScreenshotManager / CGWindowListCreateImage | PrintWindow (P/Invoke) |
| 多言語 | .lproj/Localizable.strings | .resx リソース |

### ウィンドウ列挙・リサイズ
- `EnumWindows` で全ウィンドウ列挙
- `DWMWA_CLOAKED` フィルタリングでUWP隠しウィンドウ除外
- `WS_CAPTION` + `WS_EX_TOOLWINDOW` でスタイルフィルタリング
- `SetWindowPos` でリサイズ実行（`SWP_NOMOVE | SWP_NOZORDER`）

### 設定永続化
- JSON ファイル: `%APPDATA%/WindowResize/settings.json`
- 自動起動: `HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run`

### 多言語対応 (i18n)
- .NET .resx リソースファイル方式
- `Strings.PropertyName` で呼び出し（強く型付けされたリソース）
- プリセットサイズ名（"Full HD", "XGA" 等）は翻訳しない

### メニュー構造
- NotifyIcon + ContextMenuStrip
- 左クリック・右クリックどちらでもメニュー表示
- DropDownOpening イベントで遅延ウィンドウ一覧取得

### プリセットサイズ
| サイズ | ラベル |
|--------|--------|
| 3840 x 2160 | 4K UHD |
| 2560 x 1440 | QHD |
| 1920 x 1080 | Full HD |
| 1680 x 1050 | WSXGA+ |
| 1366 x 768 | HD+ |
| 1280 x 720 | HD |
| 1024 x 768 | XGA |
| 800 x 600 | SVGA |

### スクリーンショット
- `PrintWindow` API (P/Invoke) で対象ウィンドウをキャプチャ
- `PW_RENDERFULLCONTENT` フラグでDWM合成ウィンドウ対応
- DPIスケーリング対応（125%/150%/200%表示）
- リサイズ成功後500ms待機してからキャプチャ（Mac版と同じ）
- ファイル名形式: `MMddHHmmss_AppName_WindowTitle.png`
- 設定項目:
  - `ScreenshotEnabled` — マスタートグル
  - `ScreenshotSaveToFile` — ファイル保存
  - `ScreenshotSaveFolderPath` — 保存先フォルダ（FolderBrowserDialogで選択）
  - `ScreenshotCopyToClipboard` — クリップボードコピー

### クロスコンパイル注意事項
- macOS上では `UseWindowsForms` SDK が利用不可
- `EnableWindowsTargeting=true` + `FrameworkReference` で代替
- `dotnet publish -r win-x64` でクロスビルド可能

## Conventions
- リソースキーは PascalCase: `MenuResize`, `SettingsWidth`, `AlertResizeFailedTitle`
- SettingsForm は閉じるとき Hide（破棄しない）
- メニューは SettingsChanged イベントで再構築
