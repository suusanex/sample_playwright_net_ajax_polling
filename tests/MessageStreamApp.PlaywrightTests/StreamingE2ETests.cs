using Microsoft.Playwright;

namespace MessageStreamApp.PlaywrightTests;

public sealed class StreamingE2ETests
{
    [Test]
    public async Task StreamingMode_DisplaysMessages()
    {
        // 前提条件：テスト用のホストインスタンスを作成する
        await using var host = new TestHost();
        
        // 前提条件：ホストをStreamingモードで起動する
        // TestHost.CreateConfig()でStreamingモードの設定を指定して、ホストを開始
        await host.StartAsync(TestHost.CreateConfig("Streaming"));

        // 前処理：Playwrightブラウザーの初期化（テスト用ブラウザーのセットアップ）
        using var playwright = await Playwright.CreateAsync();
        
        // 前処理：Chromiumブラウザーをヘッドレスモードで起動（UI表示なしで実行）
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        
        // 前処理：ブラウザーウィンドウ（ページ）を開く。BaseURLはホストのアドレスに設定
        var page = await browser.NewPageAsync(new BrowserNewPageOptions { BaseURL = host.BaseAddress.ToString() });

        // テスト処理：アプリケーションのホームページにアクセス
        await page.GotoAsync("/");
        
        // テスト処理：メッセージ要素が表示されるまで待機（最大5秒）
        // Streamingモードでは、WebSocketで継続的にメッセージを受け取るため、Pollingモードより早く表示される
        await page.WaitForSelectorAsync("#messages .message", new PageWaitForSelectorOptions { Timeout = 5000 });
        
        // テスト処理：表示されたメッセージの件数を取得
        var count = await page.Locator("#messages .message").CountAsync();

        // 結果判定：1件以上のメッセージが表示されていることを確認
        Assert.That(count, Is.GreaterThan(0));
    }
}
