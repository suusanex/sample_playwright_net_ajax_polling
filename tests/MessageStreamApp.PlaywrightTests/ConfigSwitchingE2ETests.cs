using Microsoft.Playwright;

namespace MessageStreamApp.PlaywrightTests;

public sealed class ConfigSwitchingE2ETests
{
    [Test]
    public async Task DisplaysModeFromConfig_Polling()
    {
        await using var host = new TestHost();
        await host.StartAsync(TestHost.CreateConfig("Polling"));

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        var page = await browser.NewPageAsync(new BrowserNewPageOptions { BaseURL = host.BaseAddress.ToString() });

        await page.GotoAsync("/");
        await page.WaitForSelectorAsync("#config-display", new PageWaitForSelectorOptions { Timeout = 5000 });
        var text = await page.Locator("#config-display").InnerTextAsync();

        Assert.That(text, Does.Contain("Polling"));
    }

    [Test]
    public async Task DisplaysModeFromConfig_Streaming()
    {
        await using var host = new TestHost();
        await host.StartAsync(TestHost.CreateConfig("Streaming"));

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        var page = await browser.NewPageAsync(new BrowserNewPageOptions { BaseURL = host.BaseAddress.ToString() });

        await page.GotoAsync("/");
        await page.WaitForSelectorAsync("#config-display", new PageWaitForSelectorOptions { Timeout = 5000 });
        var text = await page.Locator("#config-display").InnerTextAsync();

        Assert.That(text, Does.Contain("Streaming"));
    }
}
