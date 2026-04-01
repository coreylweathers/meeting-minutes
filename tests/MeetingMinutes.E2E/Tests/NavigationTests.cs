using Microsoft.Playwright;
using FluentAssertions;
using Xunit;

namespace MeetingMinutes.E2E.Tests;

[Trait("Category", "E2E")]
public class NavigationTests
{
    private const string BaseUrl = "http://localhost:5000";

    [Fact]
    public async Task NavMenu_ContainsHomeLink()
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync();
        var page = await browser.NewPageAsync();
        
        await page.GotoAsync(BaseUrl);
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        
        var homeLink = page.Locator("a[href='/']");
        var count = await homeLink.CountAsync();
        count.Should().BeGreaterThan(0);
    }
    
    [Fact]
    public async Task NavMenu_ContainsUploadLink()
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync();
        var page = await browser.NewPageAsync();
        
        await page.GotoAsync(BaseUrl);
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        
        var uploadLink = page.Locator("a[href='/upload']");
        var count = await uploadLink.CountAsync();
        count.Should().BeGreaterThan(0);
    }
    
    [Fact]
    public async Task Navigation_ToUpload_Works()
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync();
        var page = await browser.NewPageAsync();
        
        await page.GotoAsync(BaseUrl);
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        
        var uploadLink = page.Locator("a[href='/upload']").First;
        await uploadLink.ClickAsync();
        
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        
        // Should navigate to upload page (or login if auth is required)
        var currentUrl = page.Url;
        (currentUrl.Contains("/upload") || currentUrl.Contains("login") || currentUrl.Contains("signin"))
            .Should().BeTrue();
    }
}
