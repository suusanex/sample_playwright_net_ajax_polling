# Tasks: ASP.NET Core 10 メッセージ配信サンプル（テスト戦略学習用）

**Input**: Design documents from `/specs/001-net-ajax-polling/`  
**Prerequisites**: plan.md (✓), spec.md (✓), research.md (✓), data-model.md (✓), contracts/ (✓), quickstart.md (✓)

**Note**: 手動でのGUIデバッグ実行を挟まず、GitHub Copilotで自動実行可能な前提。各テストはCIで正常に実行されることを確認。

## Format: `[ID] [P?] [Story] Description`

- **[P]**: 並行実行可能（異なるファイル、依存なし）
- **[Story]**: どのユーザーストーリーに属するか（例：US1, US2, US3, US4）
- ファイルパスは絶対パス相当で記載

---

## Phase 1: Setup（共有インフラ整備）

**Purpose**: プロジェクト初期化と基本構成

- [ ] T001 [P] Create project structure: `src/MessageStreamApp/`, `tests/MessageStreamApp.IntegrationTests/`, `tests/MessageStreamApp.PlaywrightTests/`, `tests/MessageStreamApp.JsTests/`
- [ ] T002 [P] Initialize ASP.NET Core 10 project `MessageStreamApp.csproj` with dependencies (Microsoft.AspNetCore.App, System.Collections.Concurrent, Microsoft.Playwright, Microsoft.AspNetCore.Mvc.Testing, xUnit, NUnit)
- [ ] T003 [P] Initialize .IntegrationTests project with xUnit, Microsoft.AspNetCore.Mvc.Testing dependencies
- [ ] T004 [P] Initialize .PlaywrightTests project with Playwright, NUnit dependencies  
- [ ] T005 [P] Initialize .JsTests project: `package.json`, Jest config, jsdom setup
- [ ] T006 [P] Create `appsettings.json` configuration schema in `src/MessageStreamApp/appsettings.json` with MessageStream section
- [ ] T007 [P] Create `.gitignore` and `README.md` at repository root

---

## Phase 2: Foundational（ブロッキング前提条件）

**Purpose**: すべてのユーザーストーリーで必須の共通インフラ実装

**⚠️ CRITICAL**: ユーザーストーリー作業開始前に完了必須

- [ ] T008 [P] Create `Message` model in `src/MessageStreamApp/Models/Message.cs` with Id (long), Timestamp (DateTime), Content (string) properties
- [ ] T009 [P] Create `StreamConfiguration` POCO in `src/MessageStreamApp/Models/StreamConfiguration.cs` with Mode, MessageGenerationIntervalMs, ClientFetchIntervalMs, BufferCapacity properties
- [ ] T010 [P] Create `MessageBuffer` wrapper class in `src/MessageStreamApp/Services/MessageBuffer.cs` using ConcurrentQueue with Enqueue, DequeueAll, Interlocked counter management
- [ ] T011 Create `MessageGeneratorService` (BackgroundService) in `src/MessageStreamApp/Services/MessageGeneratorService.cs` with ExecuteAsync implementing periodic message generation with Interlocked.Increment
- [ ] T012 Create `Program.cs` in `src/MessageStreamApp/` with DI registration, logging setup, StreamConfiguration binding from appsettings
- [ ] T013 [P] Create `wwwroot/index.html` in `src/MessageStreamApp/wwwroot/` with basic UI structure (config-display, messages divs)
- [ ] T014 [P] Create `wwwroot/app.js` in `src/MessageStreamApp/wwwroot/` with placeholder functions (init, appendMessage, startPolling, startStreaming)
- [ ] T015 Create JSON serialization setup in `Program.cs` using System.Text.Json for Message serialization

**Checkpoint**: 基本インフラ完成 - ユーザーストーリー実装開始可能

---

## Phase 3: User Story 1 - メッセージのリアルタイム配信と表示 (Priority: P1) 🎯 MVP

**Goal**: サーバーが生成したメッセージがブラウザ画面に順次追加表示される基本機能を完成させる

**Independent Test**: ブラウザでページを開き、画面上にメッセージが時系列順に表示される。`/api/config`でモード確認、`/api/messages/poll`でメッセージ取得、DOMに追記されることを確認。

### Tests for User Story 1

- [ ] T016 [P] [US1] Contract test `/api/config` endpoint returns StreamConfiguration in `tests/MessageStreamApp.IntegrationTests/ConfigApiTests.cs` using WebApplicationFactory
- [ ] T017 [P] [US1] Contract test `/api/messages/poll` returns Message array (empty and with items) in `tests/MessageStreamApp.IntegrationTests/PollingApiTests.cs`
- [ ] T018 [P] [US1] Contract test `/api/messages/stream` returns NDJSON stream in `tests/MessageStreamApp.IntegrationTests/StreamingApiTests.cs`
- [ ] T019 [P] [US1] E2E test polling mode: Playwright opens page and waits for messages in `tests/MessageStreamApp.PlaywrightTests/PollingE2ETests.cs`
- [ ] T020 [P] [US1] E2E test streaming mode: Playwright opens page and waits for messages in `tests/MessageStreamApp.PlaywrightTests/StreamingE2ETests.cs`
- [ ] T021 [P] [US1] JS unit test NDJSON parsing: multiple lines split and parsed in `tests/MessageStreamApp.JsTests/app.test.js`
- [ ] T022 [P] [US1] JS unit test appendMessage: DOM element creation with jsdom in `tests/MessageStreamApp.JsTests/app.test.js`

### Implementation for User Story 1

- [ ] T023 Create `ConfigController.cs` in `src/MessageStreamApp/Controllers/ConfigController.cs` with GET /api/config endpoint returning StreamConfiguration
- [ ] T024 Create `MessagesController.cs` in `src/MessageStreamApp/Controllers/MessagesController.cs` stub with two action methods: Poll and Stream (implementations follow)
- [ ] T025 Implement Poll action in `MessagesController.cs`: GET /api/messages/poll returns all buffered messages as JSON array using DequeueAll()
- [ ] T026 Implement Stream action in `MessagesController.cs`: GET /api/messages/stream returns NDJSON with explicit FlushAsync calls, using HttpContext.RequestAborted for cancellation
- [ ] T027 Implement `appendMessage` function in `wwwroot/app.js`: creates DOM element and appends to #messages div
- [ ] T028 Implement polling logic in `wwwroot/app.js` startPolling(): setInterval fetch /api/messages/poll, parse array, call appendMessage for each
- [ ] T029 Implement streaming logic in `wwwroot/app.js` startStreaming(): fetch /api/messages/stream, ReadableStream with TextDecoder, line-by-line JSON.parse, batch append to DOM
- [ ] T030 Implement config fetch in `wwwroot/app.js` init(): GET /api/config, display mode and interval, conditionally call startPolling or startStreaming
- [ ] T031 Add error handling and logging in `MessagesController.cs` for null buffer, serialization failures
- [ ] T032 Add ILogger injection and logging in `MessageGeneratorService.cs` for message generation events
- [ ] T033 Add request logging in `Program.cs` middleware setup for debugging API calls
- [ ] T034 [P] Add console.log debug statements in `wwwroot/app.js` for fetch events, message parsing (optional - for development)
- [ ] T035 Run IntegrationTests: `dotnet test tests/MessageStreamApp.IntegrationTests` - all tests should PASS
- [ ] T036 Run PlaywrightTests: `dotnet test tests/MessageStreamApp.PlaywrightTests` with short intervals (10ms generation, 50ms fetch, 5s timeout) - all tests should PASS
- [ ] T037 Run JsTests: `cd tests/MessageStreamApp.JsTests && npm test` - all tests should PASS

**Checkpoint**: US1完成 - メッセージ配信・表示の基本機能が独立して機能

---

## Phase 4: User Story 2 - 通信方式の切り替え (Priority: P2)

**Goal**: HTTPストリーミング（NDJSON）とポーリングの2つの通信方式を設定で切り替え可能にする

**Independent Test**: `appsettings.json`で Mode を "Streaming" と "Polling" に変えて各方式をテスト。ブラウザでメッセージが正常に配信される。統合テストで両方式が同等の配信結果を得られることを確認。

### Tests for User Story 2

- [ ] T038 [P] [US2] Contract test polling mode: multiple poll calls return correct messages in `tests/MessageStreamApp.IntegrationTests/PollingApiTests.cs` (verify 2nd call gets new messages, buffer empties)
- [ ] T039 [P] [US2] Contract test streaming mode: 2nd batch of messages delivered after ClientFetchIntervalMs in `tests/MessageStreamApp.IntegrationTests/StreamingApiTests.cs`
- [ ] T040 [P] [US2] E2E test mode switching: verify polling.json vs streaming.json config changes behavior in `tests/MessageStreamApp.PlaywrightTests/ConfigSwitchingE2ETests.cs`

### Implementation for User Story 2

- [ ] T041 [P] Create appsettings.Polling.json in `src/MessageStreamApp/appsettings.Polling.json` with Mode="Polling" configuration
- [ ] T041 [P] Create appsettings.Streaming.json in `src/MessageStreamApp/appsettings.Streaming.json` with Mode="Streaming" configuration
- [ ] T042 Add validation in `Program.cs` for StreamConfiguration (MessageGenerationIntervalMs 10-10000, ClientFetchIntervalMs 100-30000, BufferCapacity 10-1000)
- [ ] T043 Update `MessagesController.Stream` to handle mode-aware timing: ClientFetchIntervalMs for batch writes, MessageGenerationIntervalMs for empty buffer wait
- [ ] T044 Verify `wwwroot/app.js` config mode handling works correctly: /api/config mode value routes to correct transport (startPolling vs startStreaming)
- [ ] T045 Add test data setup in integration test base class: WebApplicationFactory.WithWebHostBuilder overrides with short intervals (10ms generation, 50ms fetch)
- [ ] T046 Add WebDriverWait equivalent in Playwright tests: increase timeout to 5s for polling mode (slower than streaming)
- [ ] T047 [P] Update integration tests to verify 2+ consecutive poll calls work correctly (buffer resets, new messages added)
- [ ] T048 [P] Update integration tests to verify streaming mode delivers multiple batches (buffer not exhausted after 1st batch)
- [ ] T049 Run all tests with Mode="Polling": `dotnet test` in both IntegrationTests and PlaywrightTests - all should PASS
- [ ] T050 Run all tests with Mode="Streaming": modify test setup to use Streaming config, all tests should PASS
- [ ] T051 Verify test execution time: integration tests complete in <5s, Playwright tests in <10s per mode

**Checkpoint**: US2完成 - ポーリング/ストリーミング両方式が切り替え可能で、各方式で独立して機能

---

## Phase 5: User Story 3 - メッセージ取得間隔の調整 (Priority: P2)

**Goal**: クライアント側のメッセージ取得間隔を設定で変更し、バッファリング動作やUI更新頻度の変化を確認できる

**Independent Test**: `appsettings.json`で ClientFetchIntervalMs を 500, 2000, 5000 など複数値に変えてテスト。実際のメッセージ配信タイミングが設定値に応じて変わることを確認。

### Tests for User Story 3

- [ ] T052 [P] [US3] Contract test with 500ms fetch interval: poll returns multiple buffered messages in `tests/MessageStreamApp.IntegrationTests/PollingApiTests.cs`
- [ ] T053 [P] [US3] Contract test with 5000ms fetch interval: poll returns larger batches (multiple generations) in `tests/MessageStreamApp.IntegrationTests/PollingApiTests.cs`
- [ ] T054 [P] [US3] E2E test UI update frequency changes with interval: observe DOM updates slower with longer interval in `tests/MessageStreamApp.PlaywrightTests/IntervalE2ETests.cs`
- [ ] T055 [P] [US3] Integration test config endpoint reflects interval change in response in `tests/MessageStreamApp.IntegrationTests/ConfigApiTests.cs`

### Implementation for User Story 3

- [ ] T056 [P] Create appsettings.Fast.json in `src/MessageStreamApp/appsettings.Fast.json` with ClientFetchIntervalMs=500
- [ ] T057 [P] Create appsettings.Slow.json in `src/MessageStreamApp/appsettings.Slow.json` with ClientFetchIntervalMs=5000
- [ ] T058 Update `wwwroot/app.js` startPolling(): use config.clientFetchIntervalMs from /api/config instead of hardcoded value
- [ ] T059 Update `wwwroot/app.js` streaming loop: use config.clientFetchIntervalMs for Task.Delay between batch writes (server-side hint)
- [ ] T060 Add interval validation: warn in logs if ClientFetchIntervalMs < MessageGenerationIntervalMs (creates buffer buildup)
- [ ] T061 [P] Test with Fast config: verify UI updates ~2x per second with ClientFetchIntervalMs=500
- [ ] T062 [P] Test with Slow config: verify UI updates ~1x per 5 seconds with ClientFetchIntervalMs=5000
- [ ] T063 Run tests with multiple interval values: confirm config.clientFetchIntervalMs is read and applied correctly
- [ ] T064 Verify no memory leaks: monitor buffer size stays within BufferCapacity even with fast generation + slow fetch

**Checkpoint**: US3完成 - 取得間隔の調整が機能し、異なる間隔で動作を観察可能

---

## Phase 6: User Story 4 - 各テストレイヤーの実装と実行 (Priority: P1)

**Goal**: 統合テスト・E2Eテスト・JSユニットテストそれぞれが正常に実行でき、各レイヤーで適切な検証が行われる

**Independent Test**: 各テストプロジェクトを個別に実行し、すべてのテストが成功することを確認。テストカバレッジ（API、ブラウザ動作、JS関数）が包括的であることを検証。

### Tests for User Story 4

- [ ] T065 [P] [US4] Full integration test suite: test config, poll, stream endpoints with WebApplicationFactory in `tests/MessageStreamApp.IntegrationTests/` (≥6 tests)
- [ ] T066 [P] [US4] Full E2E test suite: polling and streaming modes in real browser in `tests/MessageStreamApp.PlaywrightTests/` (≥4 tests)
- [ ] T067 [P] [US4] Full JS test suite: NDJSON parsing, DOM append, config switching with Jest in `tests/MessageStreamApp.JsTests/` (≥5 tests)

### Implementation for User Story 4

- [ ] T068 Ensure all 6+ integration test cases cover: config endpoint, poll empty/filled buffer, poll 2nd call, stream single batch, stream multiple batches, error scenarios
- [ ] T069 Ensure all 4+ E2E test cases cover: page load, polling message display, streaming message display, config display
- [ ] T070 Ensure all 5+ JS test cases cover: NDJSON line splitting, JSON.parse success/failure, appendMessage DOM creation, init config routing, startPolling/startStreaming decision
- [ ] T071 Add test documentation: each test should have clear AAA (Arrange-Act-Assert) comments and describe what is verified
- [ ] T072 Add CI-compatible test setup: no manual browser interaction required, all waits use timeout mechanisms (page.WaitForSelector 5s, Task.Delay with margin)
- [ ] T073 Verify test isolation: each test can run independently, no shared state between tests
- [ ] T074 Run full test suite: `dotnet test tests/` (all projects) - all tests PASS
- [ ] T075 [P] Run test suite with different modes: set environment to Polling, then Streaming, all tests still PASS
- [ ] T076 Verify test execution time targets: IntegrationTests <5s, PlaywrightTests <10s per mode, JsTests <5s

**Checkpoint**: US4完成 - すべてのテストレイヤーが実装され、CI環境で自動実行可能

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: 複数ユーザーストーリーに影響する改善と最終検証

- [ ] T077 [P] Update repository README.md with project overview, tech stack, folder structure, build/test/run commands from quickstart.md
- [ ] T078 [P] Add XML documentation comments to all public classes/methods in `src/MessageStreamApp/` (Models, Services, Controllers)
- [ ] T079 [P] Add JSDoc comments to key functions in `wwwroot/app.js` (init, appendMessage, startPolling, startStreaming)
- [ ] T080 Code cleanup: remove debug console.log statements from `wwwroot/app.js` (optional: keep behind feature flag)
- [ ] T081 [P] Add .editorconfig for consistent code style across C# and JS projects
- [ ] T082 [P] Create developer guide in `docs/DEVELOPMENT.md`: setup, build, run, test, troubleshooting commands
- [ ] T083 Verify appsettings.json defaults are reasonable: MessageGenerationIntervalMs=500, ClientFetchIntervalMs=2000, BufferCapacity=100, Mode="Streaming"
- [ ] T084 Test all error paths: invalid config values, null buffer, network disconnect (Playwright cancel), JS JSON.parse failures
- [ ] T085 [P] Create example test commands script: `scripts/run-all-tests.ps1` and `scripts/run-all-tests.sh` for CI/local execution
- [ ] T086 Validate quickstart.md instructions: follow all steps end-to-end, verify app runs and tests pass
- [ ] T087 Final cross-story integration: run all tests with all 3 intervals (Fast/Normal/Slow) and all 2 modes (Polling/Streaming) - 6 combinations total
- [ ] T088 [P] Update specification documents: record any assumptions/decisions made during implementation in research.md addendum
- [ ] T089 Performance check: monitor memory usage during long test run (Playwright 2+ minutes), verify <50MB delta
- [ ] T090 [P] Create GitHub Actions CI workflow (if using GitHub): `.github/workflows/dotnet-test.yml` with test execution on push
- [ ] T091 Final validation: all tasks marked complete, all tests pass, CI pipeline succeeds, quickstart works end-to-end

**Checkpoint**: プロジェクト完成 - すべてのユーザーストーリーが統合され、CI環境で自動実行可能、ドキュメント完備

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: 依存なし - 即開始可能
- **Foundational (Phase 2)**: Setup完了に依存 - **すべてのUS作業をブロック**
- **User Stories (Phase 3-6)**: Foundational完了に依存 - その後並行実行可能
  - US1（P1/MVP）: Foundational完了後開始
  - US2（P2）: Foundational完了後、US1と並行実行可能
  - US3（P2）: Foundational完了後、US1/US2と並行実行可能
  - US4（P1）: 他US完了後の最終統合テスト
- **Polish (Phase 7)**: すべてのUS完了後実行

### Within Each User Story Dependencies

- Tests (TXX): 実装前に作成・FAIL確認（TDD）
- Models (TXX): 実装前に作成
- Services (TXX): Models完了後
- Controllers/API (TXX): Models+Services完了後
- 統合テスト実行 (TXX): すべての実装完了後

### Parallel Opportunities (チーム複数メンバー時)

**Phase 1**: T001-T007 すべて [P] 並行実行可能
**Phase 2**: 
- Models: T008-T009 [P] 並行実行可能
- Services: T010-T011 順序あり（T010→T011）
- Config: T012-T014 並行実行可能
- Serialization: T015（最後）

**Phase 3-6**: 
- 各USのテスト (T0XX) [P] 並行実行可能
- 各US間: US1/US2/US3 並行実行可能（独立）
- US4: 他US完了後

**例: 3名チームの場合**
```
Developer A: Phase 1+2 基盤整備 (全員の前提条件)
Developer B: US1 実装 (P1 MVP)
Developer C: US2+US3 実装（並行）
全員: Phase 4 US4 統合テスト検証
全員: Phase 7 Polish（共同作業）
```

---

## Parallel Example: Phase 1 & 2

```bash
# Phase 1: すべて並行実行
Task T001: Create folder structure
Task T002: Create .csproj files  
Task T003: Init IntegrationTests project
Task T004: Init PlaywrightTests project
Task T005: Init JsTests project
Task T006: Create appsettings.json
Task T007: Create .gitignore

# Phase 2 Models: 並行実行
Task T008: Create Message model
Task T009: Create StreamConfiguration model

# Phase 2 Services: 順序あり
Task T010: Create MessageBuffer → 完了後
Task T011: Create MessageGeneratorService

# Phase 2 Config: 並行実行
Task T012: Create Program.cs
Task T013: Create index.html
Task T014: Create app.js skeleton
Task T015: JSON serialization
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

**推奨**: CI自動実行前提のため、すべてのStoryを完成させてから最初のテスト実行

1. **Phase 1**: Setup完了
2. **Phase 2**: Foundational完了
3. **Phase 3**: User Story 1完了
4. **実行**: `dotnet test tests/` - すべてテストPASS確認
5. **Phase 4**: User Story 2完了
6. **Phase 5**: User Story 3完了
7. **Phase 6**: User Story 4完了
8. **最終実行**: すべてのテストが全モード・全間隔でPASS
9. **Phase 7**: Polish完了
10. **CI検証**: GitHub Actions等でテスト自動実行確認

### 逐次実行（1人開発の場合）

1. Phase 1+2: 基盤整備 完了
2. Phase 3 US1: 実装 → テスト実行 → PASS
3. Phase 4 US2: 実装 → テスト実行 → PASS
4. Phase 5 US3: 実装 → テスト実行 → PASS
5. Phase 6 US4: テスト実行 → すべてPASS
6. Phase 7: Polish → CI検証完了

### チーム並行実行（複数メンバー）

1. 全員: Phase 1+2
2. 開発者A: Phase 3 US1
3. 開発者B: Phase 4 US2
4. 開発者C: Phase 5 US3
5. 全員: Phase 6 US4 (統合テスト)
6. 全員: Phase 7 Polish

---

## Test Execution Validation

すべてのテストが **CI環境で自動実行可能** であることを確認:

```bash
# Integration Tests (WebApplicationFactory, インメモリサーバー)
cd tests/MessageStreamApp.IntegrationTests
dotnet test --logger "console;verbosity=normal"
# Expected: すべてのテストが PASS

# Playwright Tests (実ブラウザ、Chromium自動起動)
cd tests/MessageStreamApp.PlaywrightTests
dotnet test --logger "console;verbosity=normal"
# Expected: すべてのテストが PASS
# 注: 初回実行時は Playwright ブラウザ自動ダウンロード

# JS Tests (Jest, jsdom)
cd tests/MessageStreamApp.JsTests
npm test
# Expected: すべてのテストが PASS
```

---

## 成功基準チェックリスト

実装完了時に以下をすべて確認:

- [ ] Phase 1: すべてのSetupタスク完了、プロジェクト構成確認
- [ ] Phase 2: MessageBuffer, MessageGeneratorService正常動作確認
- [ ] Phase 3: ポーリング・ストリーミング両方式でメッセージ配信・表示確認
- [ ] Phase 4: ポーリング/ストリーミング切り替え確認
- [ ] Phase 5: 複数の取得間隔で動作確認
- [ ] Phase 6: 統合テスト ≥6件、E2Eテスト ≥4件、JSテスト ≥5件すべてPASS
- [ ] Phase 7: 全テスト実行 <30秒以内、メモリ使用量 <50MB、CI通過
- [ ] README.md, 開発ガイド、テスト実行スクリプト作成
- [ ] 手動デバッグなし: すべてのテストがCI自動実行で成功

---

## Notes

- **[P] マーク**: 異なるファイル、依存なし = 並行実行可能
- **[Story] ラベル**: ユーザーストーリーの追跡性確保
- **手動確認なし**: すべてのテストはプログラマティック（自動化）
- **各USは独立**: 各ストーリーは単独でテスト・デプロイ可能
- **コミット**: 各タスク完了後（または論理的グループ化単位で）

