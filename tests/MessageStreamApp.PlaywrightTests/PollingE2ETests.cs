using Microsoft.Playwright;

namespace MessageStreamApp.PlaywrightTests;

public sealed class PollingE2ETests
{
    [Test]
    public async Task PollingMode_DisplaysMessages()
    {
        // 前提条件：テスト用のホストインスタンスを作成する
        await using var host = new TestHost();
        
        // 前提条件：ホストをPollingモードで起動する
        // TestHost.CreateConfig()でPollingモードの設定を指定して、ホストを開始
        await host.StartAsync(TestHost.CreateConfig("Polling"));

        // 前処理：Playwrightブラウザーの初期化（テスト用ブラウザーのセットアップ）
        using var playwright = await Playwright.CreateAsync();
        
        // 前処理：Chromiumブラウザーをヘッドレスモードで起動（UI表示なしで実行）
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        
        // 前処理：ブラウザーウィンドウ（ページ）を開く。BaseURLはホストのアドレスに設定
        var page = await browser.NewPageAsync(new BrowserNewPageOptions { BaseURL = host.BaseAddress.ToString() });

        // テスト処理：アプリケーションのホームページにアクセス
        await page.GotoAsync("/");
        
        // テスト処理：ページ上の条件（Pollingモードの確認）が満たされるまで待機（最大10秒）
        // WaitForFunctionAsyncでカスタム条件を指定：config-displayがPollingを含むことを確認
        await page.WaitForFunctionAsync("() => document.getElementById('config-display')?.textContent?.includes('Polling')", new PageWaitForFunctionOptions { Timeout = 10000 });
        
        // テスト処理：config-display要素のテキストを取得し、テストログに出力
        var configText = await page.Locator("#config-display").InnerTextAsync();
        TestContext.Progress.WriteLine($"Config display: {configText}");
        
        // テスト処理：メッセージ要素が表示されるまで待機（最大10秒）
        // Pollingモードでは、クライアント側が定期的にサーバーにメッセージをリクエストするため、表示に時間がかかる可能性がある
        await page.WaitForSelectorAsync("#messages .message", new PageWaitForSelectorOptions { Timeout = 10000 });
        
        // テスト処理：表示されたメッセージの件数を取得
        var count = await page.Locator("#messages .message").CountAsync();

        // 結果判定：1件以上のメッセージが表示されていることを確認
        Assert.That(count, Is.GreaterThan(0));
    }
}
