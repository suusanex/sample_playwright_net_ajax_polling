using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
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

    [Test]
    public async Task StreamingMode_DeliversSequentialMessageIds()
    {
        // テスト時：Streamingの設定でホストを起動して連続するIDが取得されることを確認
        await using var host = new TestHost();
        await host.StartAsync(TestHost.CreateConfig("Streaming"));

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        var page = await browser.NewPageAsync(new BrowserNewPageOptions { BaseURL = host.BaseAddress.ToString() });

        await page.GotoAsync("/");
        await page.WaitForFunctionAsync("() => document.getElementById('config-display')?.textContent?.includes('Streaming')", new PageWaitForFunctionOptions { Timeout = 10000 });

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
