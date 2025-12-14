using Microsoft.Playwright;

namespace MessageStreamApp.PlaywrightTests;

public sealed class PollingE2ETests
{
    [Test]
    public async Task PollingMode_DisplaysMessages()
    {
        await using var host = new TestHost();
        await host.StartAsync(TestHost.CreateConfig("Polling"));

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        var page = await browser.NewPageAsync(new BrowserNewPageOptions { BaseURL = host.BaseAddress.ToString() });

        await page.GotoAsync("/");
        await page.WaitForSelectorAsync("#messages .message", new PageWaitForSelectorOptions { Timeout = 5000 });
        var count = await page.Locator("#messages .message").CountAsync();

        Assert.That(count, Is.GreaterThan(0));
    }
}
