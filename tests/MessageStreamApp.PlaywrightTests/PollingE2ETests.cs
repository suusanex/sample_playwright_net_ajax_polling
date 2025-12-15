using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
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

    [Test]
    public async Task PollingMode_DeliversSequentialMessageIds()
    {
        await using var host = new TestHost();
        await host.StartAsync(TestHost.CreateConfig("Polling"));

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        var page = await browser.NewPageAsync(new BrowserNewPageOptions { BaseURL = host.BaseAddress.ToString() });

        await page.GotoAsync("/");
        await page.WaitForFunctionAsync("() => document.getElementById('config-display')?.textContent?.includes('Polling')", new PageWaitForFunctionOptions { Timeout = 10000 });

        const int minimumMessages = 12;
        await page.WaitForFunctionAsync($"() => document.querySelectorAll('#messages .message').length >= {minimumMessages}", new PageWaitForFunctionOptions { Timeout = 20000 });

        var messageTexts = await page.Locator("#messages .message").AllInnerTextsAsync();
        Assert.That(messageTexts.Count, Is.GreaterThanOrEqualTo(minimumMessages));

        var idPattern = new Regex("Message #(?<id>\\d+)", RegexOptions.Compiled);
        var ids = messageTexts.Select(text =>
        {
            var match = idPattern.Match(text);
            Assert.That(match.Success, Is.True, $"Failed to parse ID from message text: {text}");
            return long.Parse(match.Groups["id"].Value, CultureInfo.InvariantCulture);
        }).ToArray();

        for (var i = 1; i < ids.Length; i++)
        {
            Assert.That(ids[i], Is.EqualTo(ids[i - 1] + 1), $"Message IDs should be contiguous, but gap detected between {ids[i - 1]} and {ids[i]}");
        }
    }
}
