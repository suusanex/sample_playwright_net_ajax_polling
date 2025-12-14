# API Contract: メッセージ配信サンプル

**Feature**: 001-net-ajax-polling  
**Date**: 2025-12-14  
**Protocol**: HTTP/1.1  
**Base URL**: `http://localhost:{port}/api`

---

## 共通仕様

### エラーレスポンス

すべてのエンドポイントで共通のエラーフォーマット（ProblemDetails準拠）

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Bad Request",
  "status": 400,
  "detail": "Invalid configuration: MessageGenerationIntervalMs must be between 10 and 10000"
}
```

### レート制限

なし（学習用途）

---

## エンドポイント一覧

### 1. GET /api/config

#### 概要
現在のストリーム設定を取得（クライアントが通信方式と間隔を知るために使用）

#### リクエスト

```http
GET /api/config HTTP/1.1
Host: localhost:5000
```

#### レスポンス

**成功時 (200 OK)**

```json
{
  "mode": "Streaming",
  "messageGenerationIntervalMs": 500,
  "clientFetchIntervalMs": 2000,
  "bufferCapacity": 100
}
```

**フィールド説明**

| Field | Type | Description |
|-------|------|-------------|
| `mode` | `string` | "Streaming" または "Polling" |
| `messageGenerationIntervalMs` | `number` | メッセージ生成間隔（ミリ秒） |
| `clientFetchIntervalMs` | `number` | クライアント取得間隔（ミリ秒） |
| `bufferCapacity` | `number` | バッファ最大容量 |

---

### 2. GET /api/messages/poll

#### 概要
ポーリング方式でメッセージを一括取得。バッファ内の全メッセージを配列で返し、バッファを空にする。

#### リクエスト

```http
GET /api/messages/poll HTTP/1.1
Host: localhost:5000
```

#### レスポンス

**成功時 (200 OK) - メッセージあり**

```json
[
  {
    "id": 123,
    "timestamp": "2025-12-14T10:30:45.123Z",
    "content": "Message #123 at 2025-12-14T10:30:45Z"
  },
  {
    "id": 124,
    "timestamp": "2025-12-14T10:30:45.623Z",
    "content": "Message #124 at 2025-12-14T10:30:45Z"
  }
]
```

**成功時 (200 OK) - メッセージなし（バッファ空）**

```json
[]
```

**フィールド説明**

配列の各要素は`Message`型（data-model.md参照）

**注意事項**
- 取得後、バッファから該当メッセージは削除される（再送なし）
- 空配列が返っても正常（次の生成まで待機）

---

### 3. GET /api/messages/stream

#### 概要
NDJSONストリーミング方式でメッセージを配信。クライアント取得間隔ごとに、バッファ内メッセージをまとめて改行区切りJSONで送信。

#### リクエスト

```http
GET /api/messages/stream HTTP/1.1
Host: localhost:5000
```

#### レスポンス

**成功時 (200 OK)**

```http
HTTP/1.1 200 OK
Content-Type: application/x-ndjson
Transfer-Encoding: chunked

{"id":123,"timestamp":"2025-12-14T10:30:45.123Z","content":"Message #123"}
{"id":124,"timestamp":"2025-12-14T10:30:45.623Z","content":"Message #124"}
{"id":125,"timestamp":"2025-12-14T10:30:46.123Z","content":"Message #125"}
...
```

**形式**
- 各行が1つの完全なJSONオブジェクト（`Message`型）
- 改行コード: `\n`（LF）
- `Content-Type: application/x-ndjson`
- ストリームは明示的に`FlushAsync`で送信
- クライアント切断時（`HttpContext.RequestAborted`）にストリーム終了

**タイミング**
- サーバーは`ClientFetchIntervalMs`ごとに、バッファ内の全メッセージをまとめて書き出し
- バッファが空の場合は、短時間待機（`MessageGenerationIntervalMs`程度）して再チェック
- 書き出し後、バッファから削除（ポーリング同様）

**注意事項**
- ストリームは長時間接続を維持（クライアント側で読み続ける必要あり）
- 2回目以降のバッチ配信も正常に動作すること（既知の落とし穴対策）

---

## クライアント側実装ガイド

### ポーリング実装例

```javascript
const config = await fetch('/api/config').then(r => r.json());
setInterval(async () => {
  const messages = await fetch('/api/messages/poll').then(r => r.json());
  messages.forEach(m => appendMessage(m));
}, config.clientFetchIntervalMs);
```

### ストリーミング実装例

```javascript
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
```

---

## テスト契約

### 統合テスト（WebApplicationFactory）で検証すべき項目

- `/api/config`: 設定が正しく返却されること
- `/api/messages/poll`: 
  - バッファに複数メッセージがある場合、全て配列で返却されること
  - 取得後、バッファが空になること
  - 空の場合は空配列が返ること
- `/api/messages/stream`:
  - レスポンスストリームを読み、NDJSON形式で複数メッセージが取得できること
  - 2回目のバッチ（インターバル経過後）も正常に配信されること

### E2Eテスト（Playwright）で検証すべき項目

- ポーリングモード: 画面にメッセージが定期的に追加されること
- ストリーミングモード: 画面にメッセージが定期的に追加されること
- 設定表示: 現在のモードと間隔が画面に表示されること

### JSテスト（Jest）で検証すべき項目

- NDJSON分割ロジック: 改行区切り文字列を正しくパースできること
- `appendMessage`関数: DOM要素が正しく生成・追加されること
- モック化した`fetch`で通信ロジックが動作すること
