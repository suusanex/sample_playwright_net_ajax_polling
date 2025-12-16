# Data Model: メッセージ配信サンプル

**Feature**: 001-net-ajax-polling  
**Date**: 2025-12-14

## エンティティ定義

### Message（メッセージ）

サーバーが生成し、クライアントに配信する最小単位のデータ。

#### プロパティ

| Property | Type | Validation | Description |
|----------|------|------------|-------------|
| `Id` | `long` | 必須、>= 1 | シーケンス番号（生成順に単調増加） |
| `Timestamp` | `DateTime` (ISO8601) | 必須 | 生成時刻（UTC） |
| `Content` | `string` | 必須、1-500文字 | メッセージ本文（例: "Message #123 at 2025-12-14T10:30:45Z"） |

#### 状態遷移

なし（不変オブジェクト）

#### 関係

- 親エンティティ: なし（単独で存在）
- 子エンティティ: なし

#### JSON表現例

```json
{
  "id": 123,
  "timestamp": "2025-12-14T10:30:45.123Z",
  "content": "Message #123 at 2025-12-14T10:30:45Z"
}
```

---

### MessageBuffer（メッセージバッファ）

メモリ上に保持される未配信メッセージのキュー（概念エンティティ、永続化なし）。

#### プロパティ

| Property | Type | Validation | Description |
|----------|------|------------|-------------|
| `Channel` | `Channel<Message>` (Bounded) | - | メッセージのFIFOキュー（`System.Threading.Channels`） |
| `Writer` | `ChannelWriter<Message>` | - | メッセージ書き込み用（`Enqueue`操作） |
| `Reader` | `ChannelReader<Message>` | - | メッセージ読み取り用（`DequeueAll`操作） |
| `Capacity` | `int` | 固定値（例: 100） | バッファ上限（超過時は自動的に古いメッセージを破棄：`FullMode.DropOldest`） |

#### 操作

- **Enqueue(Message)**: `Writer.TryWrite()`でメッセージを追加。容量超過時は`FullMode.DropOldest`により自動的に最古メッセージを破棄
- **DequeueAll()**: `Reader.TryRead()`をループで呼び出し、キュー内の全メッセージを一括取得し、バッファを空にする
- Channel には直接的なカウント API がないため、バッファサイズ表示が必要な場合は `Enqueue`/`DequeueAll` のログなどで状態を把握する。

#### 状態遷移

1. **空**: メッセージなし
2. **蓄積中**: メッセージが追加されている
3. **払い出し済み**: DequeueAll後、再び空

#### 関係

- 含む: 複数の`Message`（0..n）

#### スレッド安全性

`System.Threading.Channels.Channel<T>`はスレッドセーフが言語仕様レベルで保証されています。複数スレッドからの同時アクセスも安全です（`Interlocked`による手動排他制御不要）。

---

### StreamConfiguration（ストリーム設定）

アプリケーション全体の動作を制御する設定値（appsettings.jsonから読み込み）。

#### プロパティ

| Property | Type | Validation | Description |
|----------|------|------------|-------------|
| `Mode` | `enum (Streaming, Polling)` | 必須 | 通信方式 |
| `MessageGenerationIntervalMs` | `int` | 10-10000 | メッセージ生成間隔（ミリ秒） |
| `ClientFetchIntervalMs` | `int` | 100-30000 | クライアント取得間隔（ミリ秒） |
| `BufferCapacity` | `int` | 10-1000 | バッファ最大容量 |

#### バリデーションルール

- `MessageGenerationIntervalMs < ClientFetchIntervalMs` を推奨（バッファに複数メッセージが貯まる）
- `BufferCapacity`が小さすぎると、メッセージロスが頻発（警告ログ出力）

#### JSON表現例（appsettings.json）

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

---

## データフロー図

```
[BackgroundService]
    ↓ (定期生成: MessageGenerationIntervalMs)
  Message
    ↓ (Enqueue / Writer.TryWrite())
[MessageBuffer (System.Threading.Channels.Channel<Message>)]
  - BoundedChannel with FullMode.DropOldest
  - Capacity: 100 (configurable)
  - スレッドセーフ保証
    ↓ (DequeueAll / Reader.TryRead() ループ)
[API Endpoint]
    ↓ (HTTP Response: JSON or NDJSON)
[Client JS]
    ↓ (DOM操作)
[Browser UI]
```

---

## 永続化方針

なし（すべてメモリ内で完結）。アプリ再起動時にバッファは空になる。

---

## テスト時の考慮事項

- **統合テスト**: `StreamConfiguration`を短い間隔（例: 10ms生成、50ms取得）に差し替えてテスト実行時間を短縮
- **E2Eテスト**: 実時間で動作するため、`ClientFetchIntervalMs`を1-2秒程度に設定し、待機時間を確保
- **単体テスト**: `Message`の生成とバリデーションのみテスト（バッファ操作は統合テストで検証）
