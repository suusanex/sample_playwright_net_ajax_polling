# Research: メッセージ配信サンプル - 技術選定とアプローチ

**Feature**: 001-net-ajax-polling  
**Date**: 2025-12-14

## 技術スタックの決定

### サーバー側: ASP.NET Core 10

#### メッセージ生成機構

**Decision**: `IHostedService` / `BackgroundService` を使用

**Rationale**:
- ASP.NET Core標準の長時間実行タスク用API
- DIコンテナと統合されており、設定やロガー注入が容易
- `Task`ベースで実装でき、`CancellationToken`でクリーンシャットダウン可能
- テストでは`BackgroundService`を直接インスタンス化し、`ExecuteAsync`を呼び出すことで挙動を検証可能

**Alternatives considered**:
- `System.Threading.Timer`: DIとの統合が弱く、テスタビリティが低い
- 独自スレッド起動: ライフサイクル管理が煩雑

#### バッファ構造

**Decision**: `System.Collections.Concurrent.ConcurrentQueue<T>` + `Interlocked`でバッファ上限制御

**Rationale**:
- スレッドセーフなキュー（生成側と取得側が並行動作）
- FIFOでメッセージ順序を保証
- `Enqueue`/`TryDequeue`が軽量でロックフリー
- 上限制御は`Interlocked.Increment`でカウント管理し、溢れたメッセージは破棄（学習用途として許容）

**Alternatives considered**:
- `BlockingCollection<T>`: 本用途では待機が不要（メッセージ生成は止めない）
- `List<T>` + `lock`: 取得時に配列化が必要でオーバーヘッド大

#### ポーリングAPI

**Decision**: 通常のコントローラーアクション（`GET /api/messages/poll`）で、バッファを一括払い出し

**Rationale**:
- 実装がシンプル
- バッファから`TryDequeue`をループし、取得できた分を配列化してJSON返却
- 空の場合は空配列を返す（クライアント側で処理継続）
- 払い出し済みメッセージはバッファから削除（再送なし）

**Alternatives considered**:
- ロングポーリング: 学習用途として複雑すぎる

#### NDJSONストリーミングAPI

**Decision**: `GET /api/messages/stream` で `StreamWriter` + `Response.Body` を使用し、NDJSON形式で書き出し

**Rationale**:
- `Response.Body`にストリームライターで直接書き込み
- バッファ内のメッセージを改行区切りJSON（NDJSON）で出力
- クライアント取得間隔ごとに`await writer.FlushAsync()`で明示的にフラッシュ（flush忘れ対策）
- `HttpContext.RequestAborted`トークンで切断検知し、ループを抜ける

**Alternatives considered**:
- Server-Sent Events (SSE): NDJSONより制約が多く、学習対象外
- WebSocket: 双方向が不要な本用途ではオーバースペック

#### 設定スキーマ (appsettings.json)

**Decision**:
```json
{
  "MessageStream": {
    "Mode": "Streaming",  // "Streaming" or "Polling"
    "MessageGenerationIntervalMs": 500,
    "ClientFetchIntervalMs": 2000,
    "BufferCapacity": 100
  }
}
```

**Rationale**:
- `Mode`: 通信方式をサーバー側で切り替え（クライアントJSは設定値を受け取って動作変更）
- `MessageGenerationIntervalMs`: メッセージ生成間隔（テスト時に短縮可能）
- `ClientFetchIntervalMs`: クライアントの取得間隔目安（サーバーがストリーミング時の書き出しタイミングに使用）
- `BufferCapacity`: バッファ上限。学習用途として固定値でOK

**Alternatives considered**:
- 環境変数: appsettingsの方が視認性・変更容易性が高い

---

### クライアント側: 素のJavaScript（フレームワークなし）

#### 画面構成

**Decision**: シンプルなHTML + インラインJS（または別ファイルJS）

**Rationale**:
- 学習用途として、依存を最小化
- メッセージ表示領域（`<div id="messages">`）と設定表示領域（現在の方式・間隔）のみ
- メッセージは`<div>`要素として追記（`appendChild`）

**Alternatives considered**:
- React/Vue: 学習対象外で過剰

#### ポーリング実装

**Decision**: `setInterval`で定期実行し、`fetch` APIで`/api/messages/poll`を呼び出し

**Rationale**:
- ブラウザ標準API（Fetch + setInterval）
- 取得した配列を一括でDOMに追記（`messages.forEach(m => append(m))`）
- 間隔は設定値（例: 2秒）を使用

**Alternatives considered**:
- XMLHttpRequest: Fetch APIの方が現代的

#### NDJSONストリーミング実装

**Decision**: `fetch`の`ReadableStream`を使い、行ごとにパース＋バッチ蓄積してDOM追記

**Rationale**:
- `response.body.getReader()`でストリームを読み込み
- `TextDecoder`でバイナリ→文字列変換
- 改行（`\n`）で分割し、各行を`JSON.parse`
- 一定数（またはタイマー）でバッファをまとめてDOM追記（バッチ化でリフロー削減）
- ストリームが終了したら再接続（または終了）

**Alternatives considered**:
- EventSource (SSE): NDJSON以外の形式になるため不適

#### 通信方式切替

**Decision**: ページロード時に`/api/config`エンドポイントで設定を取得し、条件分岐

**Rationale**:
- `if (config.mode === "Streaming") { startStream(); } else { startPolling(); }`
- 描画ロジック（DOM追記）は共通関数化（`appendMessages(messages)`）
- 設定表示も同じ設定値で更新

**Alternatives considered**:
- 静的HTML埋め込み: 動的切替ができないため不適

---

## テスト技術選定

### 統合テスト: WebApplicationFactory (xUnit)

**Decision**: `Microsoft.AspNetCore.Mvc.Testing`パッケージを使用

**Rationale**:
- ASP.NET Core標準のインメモリサーバーテスト
- 実HTTPクライアントでAPIを呼び出し、レスポンスを検証
- `ConfigureWebHost`で設定を差し替え（テスト専用の短い間隔など）
- ポーリングAPI、ストリーミングAPIの両方をテスト可能

**Test Strategy**:
- バッファリング: メッセージ生成後、バッファに蓄積されることを確認
- ポーリング: 複数メッセージを取得し、払い出し後のバッファ状態を検証
- ストリーミング: レスポンスストリームを読み、NDJSON形式のデータを検証

**Alternatives considered**:
- 実Kestrelサーバー起動: インメモリより遅く不安定

### E2Eテスト: Playwright for .NET (NUnit/xUnit)

**Decision**: `Microsoft.Playwright`パッケージを使用

**Rationale**:
- 実ブラウザ（Chromium/Firefox/WebKit）での動作確認
- ページアクセス→メッセージ表示を検証
- `page.WaitForSelectorAsync`でDOMの変化を待機
- 両方式（ポーリング/ストリーミング）を実ブラウザで検証

**Test Strategy**:
- ポーリング: 画面にメッセージが追加されることを検証
- ストリーミング: 同様にメッセージが追加されることを検証
- 間隔設定: 設定変更後の動作を確認（時間依存テストは最小限に）

**Test Environment**:
- WebApplicationFactoryは使わず、実Kestrelサーバーをランダムポートで起動
- テストコード内で`WebApplication.CreateBuilder()`→`app.Run()`を非同期実行
- Playwrightから`http://localhost:{port}`でアクセス
- テスト終了時にサーバーを停止

**Alternatives considered**:
- Selenium: Playwrightの方が新しく、安定性・速度で優位

### JSユニットテスト: Jest

**Decision**: Jest + jsdom

**Rationale**:
- Node.js環境でJavaScriptロジックを単体テスト
- `jsdom`でDOM操作をエミュレート
- `fetch`のモックで通信部分を分離
- テスト対象: NDJSON分割ロジック、メッセージパース、DOM追記ロジック、方式切替ロジック

**Test Strategy**:
- NDJSON分割: 複数行の文字列を行ごとにパースできることを検証
- DOM追記: `appendMessages`がDOM要素を追加することを検証（jsdom）
- 方式切替: 設定値に応じた分岐が正しいことを検証
- 通信部分は`fetch`をモック化し、レスポンスをシミュレート

**Alternatives considered**:
- Vitest: Jestと互換性あり、どちらでも可（Jestが実績豊富）

---

## 既知の落とし穴と対策

### 1. ストリーミングのflush忘れ

**Problem**: `StreamWriter.WriteLineAsync`だけでは、バッファに溜まってクライアントに届かない

**Solution**: 明示的に`await writer.FlushAsync()`を呼ぶ。さらに`Response.Body.FlushAsync()`も呼ぶ（ASP.NET Coreの内部バッファ対策）

### 2. 2回目の取得失敗・最後の1件しか取れない

**Problem**: 
- ポーリング: 払い出し後のバッファクリアが不完全で、古いメッセージが再送されたり、新しいメッセージが取得できない
- ストリーミング: ループ内でバッファが空になった後、新メッセージ生成まで待機ロジックがなく、空のループで終了してしまう

**Solution**:
- ポーリング: `TryDequeue`をループし、取得した分を配列化。バッファから削除済みなので再送なし
- ストリーミング: バッファが空の場合は`await Task.Delay`で待機（生成間隔と同程度）し、再度チェック。クライアント切断検知で抜ける

### 3. テストの時間依存性と不安定化

**Problem**: 生成間隔や取得間隔に依存するテストは、CI環境で不安定（タイミングずれ）

**Solution**:
- 統合テスト: 間隔を短く設定（例: 10ms生成、50ms取得）し、十分なマージンで待機（`await Task.Delay(100)`）
- E2Eテスト: `page.WaitForSelector`で要素出現を待つ（タイムアウト5秒程度）。明示的な時刻チェックは避ける
- 単体テスト: 時間依存ロジックはモックやスタブで置き換え

### 4. バッファの競合

**Problem**: 生成側と取得側が同時にバッファ操作し、データ競合

**Solution**: `ConcurrentQueue`を使用（ロックフリー）。カウント管理は`Interlocked`で排他制御

### 5. クライアント切断検知

**Problem**: ストリーミング中にクライアントがページを閉じても、サーバーがループを続ける

**Solution**: `HttpContext.RequestAborted`トークンを監視し、キャンセルされたらループを抜ける

---

## まとめ

- **サーバー**: BackgroundService + ConcurrentQueue + ASP.NET Core標準API（Fetch/Stream）
- **クライアント**: 素のJS（Fetch + setInterval/ReadableStream）
- **テスト**: WebApplicationFactory（統合）、Playwright（E2E）、Jest（単体）
- **設定**: appsettings.jsonで方式・間隔を切替
- **対策**: flush明示、待機ロジック、時間マージン、切断検知を実装

これらの技術選定により、シンプルかつテスタブルな学習用サンプルを実現できます。
