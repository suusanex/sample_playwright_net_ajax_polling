# Implementation Plan: ASP.NET Core 10 メッセージ配信サンプル（テスト戦略学習用）

**Branch**: `001-net-ajax-polling` | **Date**: 2025-12-14 | **Spec**: [spec.md](spec.md)  
**Input**: Feature specification from `/specs/001-net-ajax-polling/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/commands/plan.md` for the execution workflow.

## Summary

ASP.NET Core 10 を使用したメッセージ配信サンプルアプリケーション。サーバーがBackgroundServiceで定期的にメッセージを生成し、ConcurrentQueueでバッファリング。クライアントはHTTPストリーミング（NDJSON）またはポーリングでメッセージを取得し、ブラウザに表示。統合テスト（WebApplicationFactory）、E2Eテスト（Playwright for .NET）、JSテスト（Jest）の3層テスト戦略を実装し、各レイヤーの役割分担を学習する教材プロジェクト。

## Technical Context

**Language/Version**: C# / .NET 10  
**Primary Dependencies**: ASP.NET Core 10, Microsoft.Playwright, Microsoft.AspNetCore.Mvc.Testing, Jest, xUnit/NUnit  
**Storage**: N/A（インメモリのみ、永続化なし）  
**Testing**: WebApplicationFactory（統合）、Playwright for .NET（E2E）、Jest + jsdom（JS単体）  
**Target Platform**: Windows/macOS/Linux（開発環境）、Chrome/Edge ブラウザ  
**Project Type**: web（ASP.NET Core バックエンド + 素のJS フロントエンド）  
**Performance Goals**: メッセージ生成間隔 500ms、取得間隔 2秒、テスト実行時間 5秒以内  
**Constraints**: 
  - 単一サーバー・単一クライアント（マルチユーザー不要）
  - データ永続化なし（メモリ内完結）
  - 認証・認可・TLS不要（学習用途）
  - メッセージ配信の準リアルタイム性（数秒遅延は許容）  
**Scale/Scope**: バッファ容量 100件、メモリ消費 10MB以下、学習用途の最小構成

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

**Status**: Constitution file is template only - no specific constraints defined for this project.

**Evaluation**: 
- Constitution未定義のため、本プロジェクト独自の設計原則を適用
- テスト戦略の3層分離（統合・E2E・単体）を重視
- シンプルさ優先（YAGNI原則）、学習用途に最適化

**Post-Phase 1 Check**: 設計完了後も憲法違反なし（憲法未定義のため）

## Project Structure

### Documentation (this feature)

```text
specs/[###-feature]/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
src/
├── MessageStreamApp/                    # ASP.NET Core メインアプリ
│   ├── Controllers/
│   │   ├── ConfigController.cs         # GET /api/config
│   │   └── MessagesController.cs       # GET /api/messages/poll, /stream
│   ├── Services/
│   │   ├── MessageGeneratorService.cs  # BackgroundService（メッセージ生成）
│   │   └── MessageBuffer.cs            # ConcurrentQueue ラッパー
│   ├── Models/
│   │   ├── Message.cs                  # メッセージエンティティ
│   │   └── StreamConfiguration.cs      # 設定POCO
│   ├── wwwroot/
│   │   ├── index.html                  # クライアントUI
│   │   └── app.js                      # クライアントロジック（ポーリング/ストリーミング）
│   ├── appsettings.json                # 通信方式・間隔設定
│   ├── Program.cs                      # エントリポイント
│   └── MessageStreamApp.csproj

tests/
├── MessageStreamApp.IntegrationTests/   # 統合テスト
│   ├── ConfigApiTests.cs               # /api/config のテスト
│   ├── PollingApiTests.cs              # /api/messages/poll のテスト
│   ├── StreamingApiTests.cs            # /api/messages/stream のテスト
│   └── MessageStreamApp.IntegrationTests.csproj
├── MessageStreamApp.PlaywrightTests/    # E2Eテスト
│   ├── PollingE2ETests.cs              # ポーリング方式のブラウザテスト
│   ├── StreamingE2ETests.cs            # ストリーミング方式のブラウザテスト
│   └── MessageStreamApp.PlaywrightTests.csproj
└── MessageStreamApp.JsTests/            # JSテスト
    ├── app.test.js                     # クライアントロジックの単体テスト
    ├── package.json                    # Jest設定
    └── jest.config.js
```

**Structure Decision**: Web application構成を採用。サーバー側はASP.NET Coreの標準構成（Controllers/Services/Models）、クライアント側はwwwroot配下に静的ファイルとして配置。テストは機能別に3プロジェクトに分離し、各テストレイヤーの独立性を確保。

## Complexity Tracking

Constitution未定義のため、本セクションは該当なし。設計は学習用途に最適化されたシンプル構成を維持。

---

## 詳細設計（Design Decisions）

以下は spec の要件を満たすための **設計レベルの HOW（構成・責務・API・設定・テスト方針の具体化）** です。

---

### 1. サーバー側設計

#### 1.1 メッセージ生成機構

**実装方式**: `BackgroundService` を継承したホステッドサービス

**クラス**: `MessageGeneratorService : BackgroundService`

**責務**:
- アプリケーション起動時に自動開始、停止時にクリーンシャットダウン
- `MessageGenerationIntervalMs`ごとにメッセージを生成（`Task.Delay` + ループ）
- 生成したメッセージを `MessageBuffer` に `Enqueue`

**テスタビリティ工夫**:
- `IOptions<StreamConfiguration>` で設定を注入（テスト時に短い間隔を差し込める）
- `ExecuteAsync(CancellationToken)` をテストコードから直接呼び出し可能
- メッセージ生成ロジックを private メソッド化し、単体テスト可能（必要に応じて）

**シーケンス番号生成**: `Interlocked.Increment` で安全にカウントアップ

```csharp
// 疑似コード
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    while (!stoppingToken.IsCancellationRequested)
    {
        var msg = new Message
        {
            Id = Interlocked.Increment(ref _seqNo),
            Timestamp = DateTime.UtcNow,
            Content = $"Message #{_seqNo} at {DateTime.UtcNow:O}"
        };
        _buffer.Enqueue(msg);
        await Task.Delay(_config.MessageGenerationIntervalMs, stoppingToken);
    }
}
```

---

#### 1.2 バッファ構造

**実装**: `MessageBuffer` クラス（`ConcurrentQueue<Message>` ラッパー）

**責務**:
- メッセージの先入先出（FIFO）管理
- スレッドセーフな `Enqueue` / `DequeueAll`
- バッファ上限制御（容量超過時は最古メッセージを破棄）

**スレッド安全性**:
- `ConcurrentQueue<T>` はロックフリー（生成側・取得側の並行動作OK）
- カウント管理は `Interlocked.Increment` / `Interlocked.Decrement`

**バッファ上限制御**:
```csharp
public void Enqueue(Message msg)
{
    if (Interlocked.Increment(ref _count) > _capacity)
    {
        _queue.TryDequeue(out _); // 最古を破棄
        Interlocked.Decrement(ref _count);
        _logger.LogWarning("Buffer overflow: oldest message dropped");
    }
    _queue.Enqueue(msg);
}
```

**一括払い出し**:
```csharp
public List<Message> DequeueAll()
{
    var result = new List<Message>();
    while (_queue.TryDequeue(out var msg))
    {
        result.Add(msg);
        Interlocked.Decrement(ref _count);
    }
    return result;
}
```

---

#### 1.3 ポーリングAPI設計

**エンドポイント**: `GET /api/messages/poll`

**責務**:
- バッファから全メッセージを一括取得（`DequeueAll()`）
- JSON配列として返却
- 空の場合は空配列 `[]`

**フォーマット**: 標準JSONシリアライズ（`System.Text.Json`）

**払い出し後の扱い**: バッファから削除済み（再送なし）

```csharp
[HttpGet("poll")]
public IActionResult Poll()
{
    var messages = _buffer.DequeueAll();
    _logger.LogInformation("Polling: Returned {Count} messages", messages.Count);
    return Ok(messages);
}
```

---

#### 1.4 NDJSONストリーミングAPI設計

**エンドポイント**: `GET /api/messages/stream`

**責務**:
- `Response.Body` に `StreamWriter` で直接書き込み
- `ClientFetchIntervalMs` ごとに、バッファ内メッセージをまとめてNDJSON形式で出力
- 各メッセージを改行区切り（`\n`）で送信
- `FlushAsync` を明示的に呼び出し（flush忘れ対策）
- クライアント切断検知（`HttpContext.RequestAborted`）でループ終了

**NDJSON形式**: 1行 = 1メッセージの完全JSON（改行コードは LF）

**書き出しタイミング**:
- `ClientFetchIntervalMs` ごとにバッファをチェック
- バッファが空の場合は、短時間待機（`MessageGenerationIntervalMs`）して再チェック
- メッセージがある場合は全件を書き出し

**キャンセル/切断時の扱い**: `RequestAborted.IsCancellationRequested` で検知し、ループを抜ける

```csharp
[HttpGet("stream")]
public async Task Stream()
{
    Response.ContentType = "application/x-ndjson";
    await using var writer = new StreamWriter(Response.Body);
    
    while (!HttpContext.RequestAborted.IsCancellationRequested)
    {
        var messages = _buffer.DequeueAll();
        if (messages.Count > 0)
        {
            foreach (var msg in messages)
            {
                var json = JsonSerializer.Serialize(msg);
                await writer.WriteLineAsync(json);
            }
            await writer.FlushAsync();
            await Response.Body.FlushAsync(); // ASP.NET Core内部バッファもflush
            _logger.LogInformation("Streaming: Sent {Count} messages", messages.Count);
        }
        else
        {
            await Task.Delay(_config.MessageGenerationIntervalMs, HttpContext.RequestAborted);
        }
        await Task.Delay(_config.ClientFetchIntervalMs, HttpContext.RequestAborted);
    }
}
```

**既知の落とし穴対策**:
- **flush忘れ**: `writer.FlushAsync()` と `Response.Body.FlushAsync()` の両方を呼ぶ
- **2回目以降の配信失敗**: 待機ロジック（`Task.Delay`）でバッファに新メッセージが貯まるまで待つ
- **最後の1件しか取れない**: `DequeueAll()` でバッファ内全件を取得→書き出し

---

#### 1.5 構成ファイル設計

**ファイル**: `appsettings.json`

**スキーマ**:
```json
{
  "MessageStream": {
    "Mode": "Streaming",
    "MessageGenerationIntervalMs": 500,
    "ClientFetchIntervalMs": 2000,
    "BufferCapacity": 100
  }
}
```

**POCO**: `StreamConfiguration` クラス

```csharp
public class StreamConfiguration
{
    public string Mode { get; set; } = "Streaming"; // "Streaming" or "Polling"
    public int MessageGenerationIntervalMs { get; set; } = 500;
    public int ClientFetchIntervalMs { get; set; } = 2000;
    public int BufferCapacity { get; set; } = 100;
}
```

**バリデーション**: `IOptions<StreamConfiguration>` で取得後、起動時に範囲チェック（10-10000ms など）

**クライアントへの配信**: `/api/config` エンドポイントで設定を返却

```csharp
[HttpGet("config")]
public IActionResult GetConfig()
{
    return Ok(_config.Value);
}
```

---

### 2. クライアント側設計

#### 2.1 画面構成

**ファイル**: `wwwroot/index.html`

**UI要素**:
- `<div id="config-display">`: 現在の通信方式・間隔を表示
- `<div id="messages">`: メッセージをリスト表示（縦スクロール）
- スタイル: 最小限（プレーンHTML、CSSは任意で少量のみ）

**例**:
```html
<!DOCTYPE html>
<html>
<head>
    <title>Message Stream</title>
</head>
<body>
    <h1>Message Stream Sample</h1>
    <div id="config-display">Loading config...</div>
    <h2>Messages</h2>
    <div id="messages"></div>
    <script src="app.js"></script>
</body>
</html>
```

---

#### 2.2 ポーリング実装

**ファイル**: `wwwroot/app.js`

**責務**:
- `setInterval` で `ClientFetchIntervalMs` ごとに `/api/messages/poll` を呼び出し
- 取得した配列を `forEach` でDOM追記

**疑似コード**:
```javascript
async function startPolling(intervalMs) {
    setInterval(async () => {
        const messages = await fetch('/api/messages/poll').then(r => r.json());
        messages.forEach(m => appendMessage(m));
    }, intervalMs);
}

function appendMessage(msg) {
    const div = document.createElement('div');
    div.textContent = `[${msg.timestamp}] ${msg.content}`;
    document.getElementById('messages').appendChild(div);
}
```

---

#### 2.3 NDJSONストリーミング実装

**責務**:
- `fetch('/api/messages/stream')` の `ReadableStream` を読み込み
- `TextDecoder` でバイナリ→文字列変換
- 改行（`\n`）で分割し、各行を `JSON.parse`
- バッチ単位でDOM追記（バッファに貯めてから一括追記、または逐次追記）

**疑似コード**:
```javascript
async function startStreaming() {
    const response = await fetch('/api/messages/stream');
    const reader = response.body.getReader();
    const decoder = new TextDecoder();
    let buffer = '';

    while (true) {
        const { done, value } = await reader.read();
        if (done) break;

        buffer += decoder.decode(value, { stream: true });
        const lines = buffer.split('\n');
        buffer = lines.pop(); // 未完行を保持

        lines.forEach(line => {
            if (line.trim()) {
                const message = JSON.parse(line);
                appendMessage(message);
            }
        });
    }
}
```

---

#### 2.4 通信方式切替

**責務**:
- ページロード時に `/api/config` を呼び出し、設定を取得
- `mode` に応じて `startPolling()` または `startStreaming()` を実行
- 設定表示も同じデータで更新

**疑似コード**:
```javascript
async function init() {
    const config = await fetch('/api/config').then(r => r.json());
    document.getElementById('config-display').textContent = 
        `Mode: ${config.mode}, Interval: ${config.clientFetchIntervalMs}ms`;

    if (config.mode === 'Streaming') {
        await startStreaming();
    } else {
        startPolling(config.clientFetchIntervalMs);
    }
}

init();
```

**責務分離**:
- **transport層**: ポーリング/ストリーミングの通信ロジック
- **render層**: `appendMessage()` でDOM操作（共通化）

---

### 3. テスト設計

#### 3.1 IntegrationTests（WebApplicationFactory）

**プロジェクト**: `MessageStreamApp.IntegrationTests`

**責務**:
- サーバー側APIの動作検証（HTTPリクエスト→レスポンス）
- バッファリング・払い出しロジックの検証

**テストケース**:

1. **ConfigApiTests**:
   - `/api/config` を呼び出し、設定が正しく返却されることを検証

2. **PollingApiTests**:
   - サーバー起動→メッセージ生成を待つ→`/api/messages/poll` 呼び出し
   - 複数メッセージが配列で返却されることを確認
   - 2回目の呼び出しで、新しいメッセージが取得できることを確認（既知の落とし穴対策）
   - バッファが空の場合、空配列が返ることを確認

3. **StreamingApiTests**:
   - `/api/messages/stream` を呼び出し、`HttpClient` でストリームを読み取り
   - NDJSON形式で複数行が取得できることを確認
   - 各行が有効なJSONであることを検証
   - 2回目のバッチ（`ClientFetchIntervalMs` 経過後）も正常に配信されることを確認

**設定差し替え**:
- `WebApplicationFactory.WithWebHostBuilder` で `appsettings` をテスト用に上書き
- 生成間隔: 10ms、取得間隔: 50ms（テスト高速化）

**待機戦略**:
- `await Task.Delay(100)` などでマージンを持って待機（CI環境でのタイミングずれ対策）

---

#### 3.2 PlaywrightTests（.NET）

**プロジェクト**: `MessageStreamApp.PlaywrightTests`

**責務**:
- 実ブラウザでのE2E検証（クライアント・サーバー統合動作）

**テストケース**:

1. **PollingE2ETests**:
   - `appsettings.json` を "Polling" に設定してサーバー起動
   - Playwrightでページにアクセス
   - `page.WaitForSelectorAsync("#messages div")` でメッセージ要素の出現を待つ
   - メッセージが複数追加されることを確認（要素数カウント）

2. **StreamingE2ETests**:
   - `appsettings.json` を "Streaming" に設定してサーバー起動
   - 同様にメッセージ要素の出現を確認

**サーバー起動方法**:
- `WebApplicationFactory` は使わず、実Kestrelサーバーをランダムポートで起動
- テストコード内で `WebApplication.CreateBuilder()` → `app.RunAsync()` を非同期実行
- `IHostApplicationLifetime` または `CancellationTokenSource` でテスト終了時に停止

```csharp
// 疑似コード
var builder = WebApplication.CreateBuilder();
builder.WebHost.UseUrls("http://localhost:0"); // ランダムポート
var app = builder.Build();
var server = app.RunAsync(cts.Token);
var port = app.Urls.First().Split(':').Last(); // ポート取得
```

**待機戦略**:
- `page.WaitForSelector` でタイムアウト5秒程度
- 明示的な時刻チェックは避ける（タイミング依存の不安定化を回避）

**間隔設定**:
- E2Eテストは実時間で動作するため、`ClientFetchIntervalMs` を1-2秒程度に設定

---

#### 3.3 JsTests（Jest）

**プロジェクト**: `MessageStreamApp.JsTests`

**責務**:
- クライアント側JavaScriptロジックの単体テスト（DOM操作以外はモック/jsdomで検証）

**テストケース**:

1. **NDJSON分割・パースのテスト**:
   - 複数行のNDJSON文字列を入力
   - 改行区切りで分割し、各行を `JSON.parse` できることを確認

2. **`appendMessage` 関数のテスト（jsdom）**:
   - jsdomで仮想DOM構築
   - `appendMessage(msg)` を呼び出し、`#messages` に要素が追加されることを確認

3. **通信ロジックのモックテスト**:
   - `fetch` をモック化（`jest.mock`）
   - ポーリング: モックが指定間隔で呼ばれることを確認
   - ストリーミング: モックがストリームを返し、データがパースされることを確認

4. **方式切替ロジックのテスト**:
   - 設定値（`mode: "Polling"`）で `startPolling` が呼ばれることを確認
   - 設定値（`mode: "Streaming"`）で `startStreaming` が呼ばれることを確認

**Jest設定**:
```json
// jest.config.js
module.exports = {
  testEnvironment: 'jsdom',
  transform: {}
};
```

---

### 4. ログ/デバッグしやすさ

**ログ出力**:
- `ILogger<T>` で標準ログ出力
- メッセージ生成時: `LogInformation("Generated message #{Id}", msg.Id)`
- バッファ操作時: `LogWarning("Buffer overflow")`、`LogInformation("Polling: Returned {Count} messages")`
- ストリーミング送信時: `LogInformation("Streaming: Sent {Count} messages")`

**デバッグ支援**:
- Visual Studio / VS Code のデバッガーでブレークポイント設置
- バッファの `CurrentCount` をウォッチ
- ブラウザ開発者ツールのNetworkタブでリクエスト・レスポンス確認

---

### 5. 既知の落とし穴と対策

#### 5.1 ストリーミングのflush忘れ

**問題**: `StreamWriter.WriteLineAsync` だけでは、バッファに溜まってクライアントに届かない

**対策**: 
- `await writer.FlushAsync()` を明示的に呼ぶ
- `await Response.Body.FlushAsync()` も呼ぶ（ASP.NET Core内部バッファ対策）

#### 5.2 2回目の取得失敗・最後の1件しか取れない

**問題**: 
- ポーリング: 払い出し後のバッファクリアが不完全
- ストリーミング: バッファが空になった後、新メッセージ生成まで待機ロジックがない

**対策**:
- ポーリング: `TryDequeue` をループし、取得した分を配列化（削除済みなので再送なし）
- ストリーミング: バッファが空の場合は `await Task.Delay` で待機し、再チェック

**統合テストで検証**: 2回目の呼び出しで新しいメッセージが正常に取得できることを確認

#### 5.3 テストの時間依存性と不安定化

**問題**: 生成間隔・取得間隔に依存するテストは、CI環境で不安定

**対策**:
- 統合テスト: 間隔を短く（10ms生成、50ms取得）、マージン付き待機（100ms）
- E2E: `page.WaitForSelector` で要素出現を待つ（タイムアウト5秒）
- 単体テスト: 時間依存ロジックはモック/スタブで置き換え

#### 5.4 バッファの競合

**問題**: 生成側と取得側が同時にバッファ操作し、データ競合

**対策**: `ConcurrentQueue` + `Interlocked` で排他制御

#### 5.5 クライアント切断検知

**問題**: ストリーミング中にクライアントがページを閉じても、サーバーがループを続ける

**対策**: `HttpContext.RequestAborted` トークンを監視し、キャンセルされたらループを抜ける

---

## まとめ

この設計により、spec の全要件を満たし、3層テスト戦略（統合・E2E・単体）を実現します。既知の落とし穴に対する対策を組み込み、学習用途として理解しやすく、デバッグしやすい構成としています。

次のフェーズ（tasks）で、この設計に基づいた実装手順を具体化します。
