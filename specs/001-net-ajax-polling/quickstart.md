# Quickstart: メッセージ配信サンプル

**Feature**: 001-net-ajax-polling  
**Date**: 2025-12-14

---

## 前提条件

- .NET 10 SDK がインストール済み
- Node.js / npm がインストール済み（JSテスト実行用）
- PowerShell（Windows）またはBash（macOS/Linux）

---

## プロジェクト構成（実装後）

```
sample_playwright_net_ajax_polling/
├── src/
│   ├── MessageStreamApp/              # メインアプリ（ASP.NET Core）
│   │   ├── Controllers/
│   │   │   ├── ConfigController.cs   # GET /api/config
│   │   │   └── MessagesController.cs # GET /api/messages/poll, /stream
│   │   ├── Services/
│   │   │   ├── MessageGeneratorService.cs  # BackgroundService
│   │   │   └── MessageBuffer.cs            # ConcurrentQueue wrapper
│   │   ├── Models/
│   │   │   ├── Message.cs
│   │   │   └── StreamConfiguration.cs
│   │   ├── wwwroot/
│   │   │   ├── index.html             # クライアントUI
│   │   │   └── app.js                 # クライアントロジック
│   │   ├── appsettings.json
│   │   └── Program.cs
│   └── ...
├── tests/
│   ├── MessageStreamApp.IntegrationTests/    # 統合テスト
│   ├── MessageStreamApp.PlaywrightTests/     # E2Eテスト
│   └── MessageStreamApp.JsTests/             # JSテスト（Jest）
└── ...
```

---

## 1. アプリケーションのビルドと実行

### ビルド

```powershell
cd src/MessageStreamApp
dotnet build
```

### 実行

```powershell
dotnet run
```

**起動後の確認**:
- コンソールに `Now listening on: http://localhost:5000` のようなメッセージが表示される
- ブラウザで `http://localhost:5000` にアクセスし、メッセージが画面に追加されることを確認

---

## 2. 通信方式の切り替え

### appsettings.json の編集

```json
{
  "MessageStream": {
    "Mode": "Streaming",  // "Streaming" または "Polling" に変更
    "MessageGenerationIntervalMs": 500,
    "ClientFetchIntervalMs": 2000,
    "BufferCapacity": 100
  }
}
```

### 設定反映

アプリを再起動（Ctrl+C → `dotnet run`）

---

## 3. テストの実行

### 統合テスト（WebApplicationFactory）

```powershell
cd tests/MessageStreamApp.IntegrationTests
dotnet test
```

**検証内容**:
- `/api/config`: 設定が正しく返却される
- `/api/messages/poll`: バッファリング→払い出し→空配列
- `/api/messages/stream`: NDJSONストリームの読み取り

### E2Eテスト（Playwright）

```powershell
cd tests/MessageStreamApp.PlaywrightTests
dotnet test
```

**検証内容**:
- 実ブラウザ（Chromium）でメッセージが画面に追加される
- ポーリング/ストリーミング両方式で動作確認

**注意**: 初回実行時に Playwright ブラウザのインストールが必要な場合あり
```powershell
pwsh bin/Debug/net10.0/playwright.ps1 install
```

### JSテスト（Jest）

```powershell
cd tests/MessageStreamApp.JsTests
npm install
npm test
```

**検証内容**:
- NDJSON分割・パース
- DOM操作（jsdom）
- 通信ロジック（fetchモック）

---

## 4. 開発時の確認ポイント

### ログ出力

`Program.cs`でロガーを設定しているため、コンソールにメッセージ生成・配信のログが出力される。

例:
```
info: MessageStreamApp.Services.MessageGeneratorService[0]
      Generated message #123
info: MessageStreamApp.Controllers.MessagesController[0]
      Polling: Returned 5 messages
```

### デバッグ

- Visual Studio / VS Code でデバッグ実行
- ブレークポイントを `MessagesController` や `MessageGeneratorService` に設置
- バッファの状態（`CurrentCount`）を監視

### ブラウザ開発者ツール

- Network タブ: `/api/messages/poll` または `/stream` のリクエスト・レスポンスを確認
- Console タブ: クライアントJSのログ（`console.log`）を確認

---

## 5. トラブルシューティング

### ストリーミングでメッセージが届かない

**原因**: `FlushAsync`忘れ

**解決**: `MessagesController.Stream`で以下を確認
```csharp
await writer.WriteLineAsync(json);
await writer.FlushAsync();
await Response.Body.FlushAsync();
```

### 2回目の取得で失敗する

**原因**: バッファの払い出しロジックが不完全

**解決**: 
- ポーリング: `TryDequeue`のループで全件取得
- ストリーミング: バッファが空の場合は`await Task.Delay`で待機し、再チェック

### テストが不安定（時間依存）

**原因**: CI環境での実行速度差

**解決**:
- 統合テスト: 間隔を短く（10ms生成、50ms取得）、マージン付き待機（100ms）
- E2E: `page.WaitForSelector`で要素出現を待つ（タイムアウト5秒）

---

## 6. 次のステップ

実装が完了したら、以下を試してください:

1. **パラメータ実験**: `appsettings.json`で間隔を変えて動作を観察
2. **バッファ上限テスト**: `BufferCapacity`を小さく（例: 10）し、メッセージロスを確認
3. **長時間実行**: ブラウザを開いたまま数分間放置し、メモリリークがないか監視
4. **テスト追加**: 自分でシナリオを追加し、テストを拡張

---

## 参考リンク

- [ASP.NET Core - BackgroundService](https://learn.microsoft.com/ja-jp/aspnet/core/fundamentals/host/hosted-services)
- [WebApplicationFactory](https://learn.microsoft.com/ja-jp/aspnet/core/test/integration-tests)
- [Playwright for .NET](https://playwright.dev/dotnet/)
- [Jest](https://jestjs.io/)
