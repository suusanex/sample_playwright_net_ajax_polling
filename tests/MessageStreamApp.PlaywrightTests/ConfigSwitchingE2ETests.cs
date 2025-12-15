using Microsoft.Playwright;

namespace MessageStreamApp.PlaywrightTests;

public sealed class ConfigSwitchingE2ETests
{
    [Test]
    public async Task DisplaysModeFromConfig_Polling()
    {
        // 前提条件：テスト用のホストインスタンスを作成する
        await using var host = new TestHost();
        
        // 前提条件：ホストをPollingモードで起動する
        // ここではTestHost.CreateConfig()でPollingモードの設定を指定して、ホストを開始する
        await host.StartAsync(TestHost.CreateConfig("Polling"));

        // 前処理：Playwrightブラウザーの初期化（テスト用ブラウザーのセットアップ）
        using var playwright = await Playwright.CreateAsync();
        
        // 前処理：Chromiumブラウザー をヘッドレスモードで起動（UI表示なしで実行）
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        
        // 前処理：ブラウザーウィンドウ（ページ）を開く。BaseURLはホストのアドレスに設定
        var page = await browser.NewPageAsync(new BrowserNewPageOptions { BaseURL = host.BaseAddress.ToString() });

        // テスト処理：アプリケーションのホームページにアクセス
        await page.GotoAsync("/");
        
        // テスト処理：ページが読み込まれてconfig-displayが表示されるまで待機（最大5秒）
        await page.WaitForSelectorAsync("#config-display", new PageWaitForSelectorOptions { Timeout = 5000 });
        
        // テスト処理：config-display要素のテキストを取得
        var text = await page.Locator("#config-display").InnerTextAsync();

        // 結果判定：取得したテキストが"Polling"を含むことを確認
        Assert.That(text, Does.Contain("Polling"));
    }

    [Test]
    public async Task DisplaysModeFromConfig_Streaming()
    {
        // 前提条件：テスト用のホストインスタンスを作成する
        await using var host = new TestHost();
        
        // 前提条件：ホストをStreamingモードで起動する
        await host.StartAsync(TestHost.CreateConfig("Streaming"));

        // 前処理：Playwrightブラウザーの初期化（テスト用ブラウザーのセットアップ）
        using var playwright = await Playwright.CreateAsync();
        
        // 前処理：Chromiumブラウザーをヘッドレスモードで起動（UI表示なしで実行）
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        
        // 前処理：ブラウザーウィンドウ（ページ）を開く。BaseURLはホストのアドレスに設定
        var page = await browser.NewPageAsync(new BrowserNewPageOptions { BaseURL = host.BaseAddress.ToString() });

        // テスト処理：アプリケーションのホームページにアクセス
        await page.GotoAsync("/");
        
        // テスト処理：ページが読み込まれてconfig-displayが表示されるまで待機（最大5秒）
        await page.WaitForSelectorAsync("#config-display", new PageWaitForSelectorOptions { Timeout = 5000 });
        
        // テスト処理：config-display要素のテキストを取得
        var text = await page.Locator("#config-display").InnerTextAsync();

        // 結果判定：取得したテキストが"Streaming"を含むことを確認
        Assert.That(text, Does.Contain("Streaming"));
    }
}
