# 既存プロジェクトへのポーティングガイド: Playwright E2Eテスト統合とAjax通信方式

## 目的

このドキュメントは、サンプルプロジェクトで実装された以下の機能を、既存の本番プロジェクトに組み込む際の設計情報と実装ガイドラインをまとめたものです：

1. **HTTPストリーミング（NDJSON）とポーリングの切り替え可能な通信方式**
2. **ASP.NET Core WebApplicationFactory を用いた統合テスト**
3. **Playwright (.NET版) を用いたE2Eテスト**
4. **Jest を用いたJavaScript単体テスト**

---

## 機能要件の概要

### 必須機能

以下はすべて必須実装項目です：

- **FR-001**: サーバーは一定間隔でメッセージ（またはデータ）を生成する
- **FR-002**: 生成されたデータはサーバーサイドでバッファリングされる（メモリかストレージかは実装方針による）
- **FR-003**: クライアントはWebページを開くことでデータ配信を受信できる
- **FR-004**: 2種類の通信方式（HTTPストリーミング［NDJSON］とポーリング）をサポートする
- **FR-005**: 使用する通信方式は設定ファイルで選択可能である
- **FR-006**: クライアント側のデータ取得間隔は設定ファイルで調整可能である
- **FR-007**: サーバーはクライアントの取得タイミングまでデータをバッファリングし、取得時にまとめて配信する
- **FR-008**: クライアントは受信したデータを画面上に時系列順で追加表示する
- **FR-009**: 統合テストプロジェクトはWebApplicationFactoryを使用してサーバーロジックを検証する
- **FR-010**: E2EテストプロジェクトはPlaywright（.NET版）を使用してブラウザ動作を検証する
- **FR-011**: JSテストプロジェクトはJestを使用してクライアント側JavaScriptロジックを検証する
- **FR-012**: 各テストプロジェクトは最低1つ以上の実装済みテストケースを含む

### 成功基準

以下は本番環境での品質基準として参考にしてください：

- **SC-001**: ページを開いてから10秒以内に最初のデータが表示される
- **SC-002**: 設定された取得間隔に応じて、実際のデータ配信タイミングが±10%以内の精度で動作する
- **SC-003**: 通信方式を切り替えても、画面上のデータ表示結果が同等である（順序、内容が保証される）
- **SC-004**: 統合テスト、E2Eテスト、JSテストそれぞれが妥当な時間内に完了する
- **SC-005**: 各テストプロジェクトが独立して実行可能であり、相互に依存しない

---

## アーキテクチャ設計

### データフロー

```
[BackgroundService: データ生成]
    ↓ (定期生成: 設定可能な間隔)
  [データオブジェクト]
    ↓ (Enqueue)
[MessageBuffer: System.Threading.Channels.Channel<T>]
  - BoundedChannel (容量制限あり)
  - FullMode.DropOldest (容量超過時は最古データを破棄)
  - スレッドセーフ保証
    ↓ (DequeueAll)
[API Endpoint: ポーリング or ストリーミング]
    ↓ (HTTP Response: JSON or NDJSON)
[Client JavaScript]
    ↓ (DOM操作)
[Browser UI]
```

### コアコンポーネント

#### 1. データモデル

配信する最小単位のデータエンティティ。以下のプロパティを含むことを推奨：

| プロパティ | 型 | 説明 |
|-----------|-----|------|
| `Id` | `long` | シーケンス番号（単調増加） |
| `Timestamp` | `DateTime` (ISO8601 UTC) | 生成時刻 |
| `Content` | `string` | データ本文（または本番データに応じた構造） |

本番環境では、`Content`を実際のビジネスデータに置き換え、必要に応じて追加プロパティを定義してください。

#### 2. 設定クラス（StreamConfiguration相当）

アプリケーション全体の動作を制御する設定値。`appsettings.json`から読み込み：

| プロパティ | 型 | バリデーション | 説明 |
|-----------|-----|---------------|------|
| `Mode` | `string` (enum: "Streaming", "Polling") | 必須 | 通信方式 |
| `MessageGenerationIntervalMs` | `int` | 10-10000 | データ生成間隔（ミリ秒） |
| `ClientFetchIntervalMs` | `int` | 100-30000 | クライアント取得間隔（ミリ秒） |
| `BufferCapacity` | `int` | 10-1000 | バッファ最大容量 |

**実装ポイント：**
- `Validate()`メソッドを実装し、起動時に設定値を検証する
- 不正な値の場合は`ArgumentOutOfRangeException`をスローする

#### 3. MessageBuffer（バッファ管理）

`System.Threading.Channels.Channel<T>`を使用したスレッドセーフなメッセージキュー。

**設計のポイント：**

- **`BoundedChannelOptions`を使用**：
  - `Capacity`: 設定値（例: 100）
  - `FullMode`: `BoundedChannelFullMode.DropOldest`（容量超過時は最古データを自動破棄）
  - `SingleReader`: `true`（単一読み取り最適化）
  - `SingleWriter`: `false`（複数書き込み対応）

- **APIの実装：**
  - `Enqueue(T message)`: `ChannelWriter.TryWrite(message)`でデータを追加
  - `DequeueAll()`: `ChannelReader.TryRead()`をループで呼び出し、キュー内の全データを一括取得

- **スレッド安全性**：
  - `Channel<T>`は.NET標準ライブラリでスレッドセーフが保証されているため、`Interlocked`などの手動排他制御は不要

**注意事項：**
- `Channel<T>`には直接的なカウントAPIがないため、バッファサイズの可視化が必要な場合はログで状態を把握する

#### 4. BackgroundService（データ生成）

`IHostedService`または`BackgroundService`を継承し、`ExecuteAsync`内で定期的にデータを生成してバッファに追加。

**実装のポイント：**

- `BackgroundService`（または`IHostedService`）を継承し、`ExecuteAsync`メソッドを実装
- `while (!stoppingToken.IsCancellationRequested)`ループで定期的なデータ生成を実現
- `Interlocked.Increment`を使用してスレッドセーフにIDを採番
- `Content`は`"Message #<id> at <timestamp:O>"`の英語文字列で生成し、テストで抽出しやすくする（UTC時刻）
- 生成間隔は設定値（`MessageGenerationIntervalMs`）から取得し、`Task.Delay`で待機
- ロギングで生成イベントを記録（デバッグ用）

**実装上の注意：**
- IDカウンターは`long`型のフィールド（初期値0）として管理
- メッセージオブジェクトを構築する際、`Timestamp`は`DateTime.UtcNow`を使用
- `Content`フィールドに採番IDを含める（テスト検証用）
- 本番環境では、実際のビジネスロジックに従ってデータを生成
- `CancellationToken`の検査を毎ループで実施し、アプリ停止時に確実に終了する

#### 5. APIコントローラー

**5.1 設定取得エンドポイント**

```
GET /api/config
```

- 現在の設定（通信方式、取得間隔等）をJSON形式で返却
- クライアントがこの情報を基に適切な通信方式を選択する

**5.2 ポーリングエンドポイント**

```
GET /api/messages/poll
```

- `MessageBuffer.DequeueAll()`を呼び出し、バッファ内の全データを配列で返却
- 返却後、バッファは空になる（再送なし）
- 空の場合は空配列`[]`を返す
- `Content-Type: application/json`

**5.3 ストリーミングエンドポイント**

```
GET /api/messages/stream
```

- NDJSON形式（改行区切りJSON）でデータをストリーミング配信
- `Content-Type: application/x-ndjson`
- `Transfer-Encoding: chunked`
- `HttpContext.RequestAborted`でクライアント切断を検知

**実装のポイント：**

- ストリーミングエンドポイントは無限ループで動作（クライアント切断まで）
- `HttpContext.RequestAborted`トークンで切断を検知
- `MessageBuffer.DequeueAll()`でバッファを一括取得
- 取得したメッセージを1行1つのJSON形式（NDJSON）で出力
- JSON形式への変換は`JsonSerializer.Serialize`を使用
- 各メッセージ出力後、`StreamWriter.FlushAsync()`でバッファをクリア
- さらに`Response.Body.FlushAsync()`でHTTPレスポンスストリームをフラッシュ
- バッファが空の場合は短時間（生成間隔相当）待機して再チェック
- メッセージ出力後、クライアント取得間隔相当の`Task.Delay`を実施（空バッファ時も生成間隔待機→取得間隔待機の2段階でペースを揃える）

**注意事項：**
- `FlushAsync()`は必ず2段階（`StreamWriter`と`Response.Body`）で実施
- 2回目以降のバッチ配信も正常に動作することをテストで確認
- 設定で"Polling"モードが指定されている場合は400エラーレスポンスを返す
- 接続持続時間が長いため、メモリリークに注意

#### 6. フロントエンド（JavaScript）

**6.1 設定取得と通信方式の選択**

`init()`関数の実装ポイント：
- アプリ起動時（DOMContentLoaded）に呼び出される
- `/api/config`エンドポイントに GET リクエストを送信
- レスポンスを JSON パースして設定オブジェクトを取得
- 設定の`Mode`フィールドで通信方式を判定
- `Mode === "Streaming"`の場合は`startStreaming()`を呼び出し
- それ以外は`startPolling()`を呼び出し
- 設定を画面の指定要素に表示（ID: `config-display`など）
- JSONシリアライザーはサーバー側で`camelCase`に統一されるため、フロントエンドでも小文字開始のプロパティ名を前提に実装する

**6.2 ポーリング実装**

`startPolling()`関数の実装ポイント：
- 設定から取得した`clientFetchIntervalMs`を使用
- 内部に`pollOnce()`という非同期関数を定義
- `pollOnce()`は`/api/messages/poll`に GET リクエストを送信
- レスポンスを JSON の配列としてパース
- 配列の各要素に対して`appendMessage()`を呼び出し
- 初回は即座に`pollOnce()`を一度実行
- その後`setInterval()`で定期的に`pollOnce()`を実行
- エラーハンドリングは必須（ネットワークエラー、JSON パースエラー）

**6.3 ストリーミング実装**

`startStreaming()`関数の実装ポイント：
- `/api/messages/stream`に GET リクエストを送信（長時間接続）
- `response.body.getReader()`でストリームリーダーを取得
- `TextDecoder`を使用してバイナリデータを文字列に変換
- `while`ループで`reader.read()`を繰り返し呼び出し
- `done`フラグで終了を検知（クライアント切断時）
- デコード結果をバッファに蓄積
- 改行文字(`\n`)で分割してNDJSON形式をパース
- 未完の行（末尾の不完全なJSON）は次のループに持ち越す
- 完全な行ごとに`JSON.parse()`を実行
- パース成功後、`appendMessage()`を呼び出し
- 取得完了後もサーバー設定の`clientFetchIntervalMs`ぶん待機してから次の読み取りを行い、UI更新のペースをサーバーの設定と揃える（サンプル実装準拠）

**6.4 NDJSON分割ロジック**

`parseNdjsonLines()`関数の実装ポイント：
- バッファ文字列を`\n`で分割
- 末尾の不完全な行をポップして保持（次のループ用）
- 完全な行のみをループで処理
- 空行（`trim()`後に空）はスキップ
- 各行を`JSON.parse()`でパース
- 成功したオブジェクトを配列に追加
- パース後の配列と未完行の残りを返す
- エラーハンドリング: JSON パースエラーは適切にログ出力

**6.5 メッセージ表示**

`appendMessage()`関数の実装ポイント：
- ID `messages`の DOM 要素をコンテナとして取得
- 新しい`div`要素を作成
- CSS クラス`message`を付与
- メッセージの`timestamp`フィールドを Date オブジェクトにパース
- タイムスタンプを ISO8601 形式に変換し、`content`フィールドと共に表示文字列を作成
- 作成した要素をコンテナの子要素として追加
- エラーハンドリング: コンテナが見つからない場合の処理

---

## テスト戦略

### テストレイヤーの役割分担

| テストレイヤー | 担当範囲 | ツール | 目的 |
|---------------|---------|-------|------|
| **統合テスト** | サーバー側APIとバッファリングロジック | WebApplicationFactory + xUnit | HTTPリクエスト・レスポンスの検証 |
| **E2Eテスト** | ブラウザでのエンドツーエンド動作 | Playwright (.NET) + NUnit | クライアント・サーバー統合動作の検証 |
| **JSユニットテスト** | クライアント側JavaScriptロジック | Jest + jsdom | 関数レベルの単体テスト |

### 1. 統合テスト（WebApplicationFactory）

**対象：**
- `/api/config`: 設定が正しく返却されること
- `/api/messages/poll`: 
  - バッファに複数メッセージがある場合、全て配列で返却されること
  - 取得後、バッファが空になること
  - 空の場合は空配列が返ること
- `/api/messages/stream`:
  - レスポンスストリームを読み、NDJSON形式で複数メッセージが取得できること
  - 2回目のバッチ（インターバル経過後）も正常に配信されること

**実装のポイント：**

- テストクラスは`IClassFixture<WebApplicationFactory<Program>>`を実装
- `WebApplicationFactory`のコンストラクタで`WithWebHostBuilder`を使用し、テスト用設定を注入
- `ConfigureAppConfiguration`デリゲートで、`AddInMemoryCollection`により設定値をオーバーライド
- テスト用設定値は本番より大幅に短縮（例: 生成10ms、取得50ms）
- テストメソッドは`[Fact]`（xUnit）または`[Test]`（NUnit）で装飾
- `CreateClient()`でテスト用HttpClientを作成
- `GetAsync/PostAsync`でエンドポイントに リクエスト送信
- `EnsureSuccessStatusCode()`でステータス確認
- `ReadFromJsonAsync<T>`でレスポンスを型にデシリアライズ
- `Assert.NotNull`、`Assert.Equal`などで検証

**注意点：**
- `Program.cs`で`public partial class Program { }`を定義し、テストから参照可能にする
- テスト用設定で間隔を短縮し、実行時間を最小化する
- ストリーミングのテストでは、2回目のバッチ配信まで待機する実装が必要
- テスト間でサーバー状態が独立していることを確認

### 2. E2Eテスト（Playwright）

**対象：**
- ポーリングモード: 画面にメッセージが定期的に追加されること
- ストリーミングモード: 画面にメッセージが定期的に追加されること
- 設定表示: 現在のモードと間隔が画面に表示されること
- **メッセージのドロップ検証**: 複数回の取得を通じて、サーバーが送信したすべてのメッセージがクライアントに到達すること

**実装のポイント：**

**テスト初期化（SetUp）：**
- `[SetUp]`（NUnit）で各テストメソッド実行前に呼び出される
- `Playwright.CreateAsync()`で Playwright インスタンスを生成
- `_playwright.Chromium.LaunchAsync()`でブラウザプロセスを起動
- `Headless = true`で UI 表示なしで実行
- `_browser.NewPageAsync()`でテスト用ページコンテキストを作成

**基本的なメッセージ表示テスト：**
- テストメソッドは`[Test]`で装飾
- `_page.GotoAsync(_baseUrl)`でテスト用サーバーにナビゲート
- `_page.Locator("#config-display").TextContentAsync()`で設定表示要素のテキストを取得
- `_page.WaitForSelectorAsync(".message", new() { Timeout = 5000 })`で CSS セレクタの要素出現を待機
- `_page.Locator(".message").AllAsync()`ですべてのメッセージ要素を取得
- 要素数の検証

**メッセージドロップ検証テスト：**
- テスト開始後、十分な時間（3秒以上）待機してメッセージ蓄積を待つ
- `_page.Locator(".message").AllAsync()`ですべてのメッセージ要素を取得
- 各要素の`TextContentAsync()`でテキスト内容を取得
- 正規表現パターン（例: `@"Message #(\d+)"`）でテキストからID を抽出
- `Regex.Match`と`long.TryParse`でID を数値に変換
- 抽出したID をリストに追加

**ID 連続性検証：**
- 抽出したID をソート
- ソート結果が元のリストと一致することで昇順を確認
- `for`ループで隣接要素のID 差分が常に1であることを確認
- ID に欠番がある場合はアサーション失敗

**ストリーミングモード専用テスト：**
- ストリーミングモード（NDJSON）でも同じドロップ検証ロジックを適用
- 複数バッチのメッセージが配信される際のドロップを検知可能

**マルチモードテスト：**
- 設定表示からモード名を取得し、テスト名に含める
- ポーリング/ストリーミング両モードで独立実行

**メッセージドロップ検証のポイント：**

1. **ID採番戦略**
   - サーバー側は`Message`エンティティの`Id`フィールドに単調増加のシーケンス番号を割り当てる
   - `Content`フィールドにも`Message #123`形式でIDを含める（画面表示用）

2. **テスト用設定**
   - 生成間隔を短縮（例: 100ms）してテスト時間内に多くのメッセージを生成
   - 取得間隔を適切に設定（例: 500ms）してバッファに複数メッセージが蓄積
   - テスト時間を十分に長く（3-5秒以上）して複数回のバッチ取得を実現

3. **検証項目**
   - **順序性確認**: 表示されたIDが昇順であることを確認
   - **連続性確認**: IDに欠番がないこと（ドロップがないこと）を確認
   - **各モード独立テスト**: ポーリングモードとストリーミングモードで同じ検証を実施

4. **正規表現によるID抽出**
   - 画面のメッセージテキストから正規表現でIDを抽出（例: `Regex.Match(text, @"Message #(\d+)")`）
   - パースエラーは適切にハンドルする

5. **バッファオーバーフロー時の検証**
   - `BufferCapacity`を超えるメッセージが生成される場合、`FullMode.DropOldest`により最古メッセージが破棄される
   - テストでは意図的にバッファを超過させ、IDの欠番が`BufferCapacity`相当であることを確認できるようにする（オプション）

**注意点：**
- テスト用のWebサーバーを起動する必要がある（`TestHost`クラスを実装）
- `WaitForSelectorAsync`のタイムアウトは十分に長く設定する（ポーリング間隔より長く）
- Headlessモードで実行し、CIで自動化可能にする
- 初回実行時はPlaywrightブラウザが自動ダウンロードされる
- メッセージドロップ検証テストはポーリング/ストリーミング両モードで実行する
- テスト間でUIのメッセージが重複しないよう、各テスト前にページをリロードするか、新しいコンテキストを作成する

### 3. JSユニットテスト（Jest）

**対象：**
- NDJSON分割ロジック: 改行区切り文字列を正しくパースできること
- `appendMessage`関数: DOM要素が正しく生成・追加されること
- モック化した`fetch`で通信ロジックが動作すること

**実装のポイント：**

**jest.config.js 設定：**
- `testEnvironment: 'jsdom'`でブラウザ環境をシミュレート
- `collectCoverage: true`でカバレッジ情報を収集
- `coverageDirectory: 'coverage'`で結果出力先を指定

**テストファイル構成：**
- テストディレクトリに`*.test.js`または`*.spec.js`ファイルを配置
- `require()`または`import`で対象の関数/モジュールを読み込み
- `describe`ブロックでテストグループを作成
- `test`（または`it`）でテストケースを定義

**NDJSON分割関数のテスト：**
- `parseNdjsonLines`関数を`describe`ブロックで囲む
- 入力: 複数行のNDJSON 文字列＋未完行
- 期待結果: 完全な行数と残り文字列を検証
- `expect(messages).toHaveLength(n)`で配列長を確認
- `expect(obj.property).toBe(value)`で個別プロパティを検証

**DOM操作関数のテスト：**
- `beforeEach`で DOM を初期化（`document.body.innerHTML`リセット）
- テスト用メッセージオブジェクトを作成
- `appendMessage`関数を呼び出し
- `document.getElementById`で追加要素を取得
- `expect(element.children).toHaveLength(n)`で子要素数を確認
- `expect(text).toContain(substring)`で表示内容を検証

**`fetch`のモック化：**
- `global.fetch = jest.fn()`で`fetch`を Jest モックに置き換え
- `jest.fn().mockResolvedValue()`で成功レスポンスをモック
- `jest.fn().mockRejectedValue()`でエラーレスポンスをモック

**Node.js 環境判定：**
- `app.js`に`if (typeof module !== "undefined") { module.exports = {...}; }`を追加
- ブラウザ環境ではエクスポートを実行しない
- テスト環境（Node.js）では関数を外部に公開

**注意点：**
- jsdom を使用するため、実ブラウザとは異なる動作がある可能性
- `fetch`のモック実装を確実に行う（ポリフィル不要）
- テスト間での副作用（グローバル変数汚染）を回避

---

## 依存ライブラリとバージョン

### サーバー側（.NET）

- **.NET SDK**: 10.0以上（.NET 8以降で動作可能だが、本番環境のバージョンに合わせる）
- **Microsoft.AspNetCore.App**: フレームワークに含まれる
- **System.Threading.Channels**: .NET標準ライブラリ（追加パッケージ不要）

**テストプロジェクト用パッケージ：**

```xml
<!-- 統合テスト -->
<PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.0" />
<PackageReference Include="xunit" Version="2.9.0" />
<PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />

<!-- E2Eテスト -->
<PackageReference Include="Microsoft.Playwright" Version="1.49.0" />
<PackageReference Include="NUnit" Version="4.2.2" />
<PackageReference Include="NUnit3TestAdapter" Version="4.6.0" />
```

### クライアント側（JavaScript）

```json
{
  "devDependencies": {
    "jest": "^29.7.0",
    "jest-environment-jsdom": "^29.7.0"
  }
}
```

---

## 設定ファイルの構成

### appsettings.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "MessageStream": {
    "Mode": "Polling",
    "MessageGenerationIntervalMs": 500,
    "ClientFetchIntervalMs": 2000,
    "BufferCapacity": 100
  }
}
```

### 環境別設定（オプション）

```json
// appsettings.Polling.json
{
  "MessageStream": {
    "Mode": "Polling",
    "ClientFetchIntervalMs": 1000
  }
}

// appsettings.Streaming.json
{
  "MessageStream": {
    "Mode": "Streaming",
    "ClientFetchIntervalMs": 2000
  }
}

### サンプル実装の補足（移植時に考慮すべき動作）
- `StreamConfiguration.Validate()`は`Mode`の空チェックのみで列挙値までは検証しないため、移植先でモードを列挙型や明示チェックにする場合は追加バリデーションを実装すること
- JSONシリアライザーは`camelCase`・`JsonStringEnumConverter`を使用し、`DefaultIgnoreCondition = Never`でnullを除外しない。API応答のプロパティ名や列挙値の文字列表現を合わせること
- `Program`に`public partial class Program { }`を定義しており、`WebApplicationFactory`から参照できるようにしている。移植先でもテストプロジェクトから参照できるよう同等のpartialクラス宣言を配置する
- パイプライン初期化時、`ClientFetchIntervalMs < MessageGenerationIntervalMs`の場合に警告ログを出力する（バッファ増大の気付き用）。同様の警告が必要なら移植先でもログを入れる
- ストリーミングエンドポイントは`Mode`が`Polling`の場合に400を返し、その他のモード文字列はそのままストリーミングを開始する（移植先で厳格にする場合はモードをチェックして早期エラーにする）
- バッファは`BoundedChannelFullMode.DropOldest`で最古を破棄し、書き込み拒否時に警告ログを出す。容量超過時の扱いを変える場合はこのポリシーも移植先で明示する
- フロントエンド`startPolling()`は初回即時ポーリング→`setInterval`で定期実行し、重複実行を避けるためのフラグを持つ。`startStreaming()`はサーバー設定に合わせて取得間隔ぶん待機しながらNDJSONを処理する
```

---

## トラブルシューティング

### よくある問題と解決策

#### 1. ストリーミングで2回目のバッチが配信されない

**原因**: `FlushAsync()`が不足している、またはバッファが再利用されていない

**解決策**: 
- `StreamWriter.FlushAsync()`と`Response.Body.FlushAsync()`の両方を呼び出す
- ループ内で`DequeueAll()`を毎回呼び出し、バッファを空にする

#### 2. Playwrightテストがタイムアウトする

**原因**: クライアント取得間隔が長すぎる、またはメッセージが生成されていない

**解決策**:
- テスト用設定で間隔を短縮する（例: 生成10ms、取得50ms）
- `WaitForSelectorAsync`のタイムアウトを十分に長く設定する（5秒以上）

#### 3. JSテストでfetchがundefinedエラー

**原因**: jsdom環境では`fetch`が標準提供されない

**解決策**:
- `global.fetch = jest.fn()`でモックを実装する
- または`node-fetch`をインストールして`global.fetch`に割り当てる

#### 4. バッファが溢れてデータがロストする

**原因**: 生成間隔が短すぎる、または取得間隔が長すぎる

**解決策**:
- `BufferCapacity`を増やす
- `MessageGenerationIntervalMs`と`ClientFetchIntervalMs`のバランスを調整する
- `FullMode.DropOldest`により最古データが自動破棄されることをログで確認する

---

## リファレンス

### サンプルプロジェクトの構成

```
sample_playwright_net_ajax_polling/
├── src/MessageStreamApp/
│   ├── Models/
│   │   ├── Message.cs
│   │   └── StreamConfiguration.cs
│   ├── Services/
│   │   ├── MessageBuffer.cs
│   │   └── MessageGeneratorService.cs
│   ├── Controllers/
│   │   ├── ConfigController.cs
│   │   └── MessagesController.cs
│   ├── wwwroot/
│   │   ├── index.html
│   │   └── app.js
│   ├── Program.cs
│   └── appsettings.json
├── tests/
│   ├── MessageStreamApp.IntegrationTests/
│   ├── MessageStreamApp.PlaywrightTests/
│   └── MessageStreamApp.JsTests/
└── specs/001-net-ajax-polling/
    ├── spec.md
    ├── data-model.md
    └── contracts/api.md
```

### 外部リソース

- **System.Threading.Channels**: [公式ドキュメント](https://learn.microsoft.com/en-us/dotnet/api/system.threading.channels)
- **WebApplicationFactory**: [公式ドキュメント](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests)
- **Playwright .NET**: [公式サイト](https://playwright.dev/dotnet/)
- **Jest**: [公式サイト](https://jestjs.io/)

---

## まとめ

本ドキュメントでは、HTTPストリーミング（NDJSON）とポーリングの切り替え可能な通信方式、およびそれを検証する3層のテスト戦略（統合テスト・E2Eテスト・JSテスト）の設計情報を提供しました。

実装にあたっては、以下の点に注意してください：

1. **`System.Threading.Channels`を使用したスレッドセーフなバッファ管理**
2. **NDJSON形式でのストリーミング配信（`FlushAsync`の徹底）**
3. **設定ファイルによる通信方式と間隔の柔軟な切り替え**
4. **3層のテスト戦略による包括的な品質保証**

具体的なソースコードの実装方法については、開発担当者の判断に委ねられますが、本ドキュメントで示した設計パターンとAPIインターフェースに従うことで、一貫性のある実装が可能です。
