using Microsoft.Playwright;
using FluentAssertions;
using Xunit;

namespace MeetingMinutes.E2E.Tests;

[Trait("Category", "E2E")]
public class JobsPageTests
{
    private const string BaseUrl = "http://localhost:5000";

    [Fact]
    public async Task JobsPage_RequiresAuthentication()
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync();
        var page = await browser.NewPageAsync();
        
        await page.GotoAsync($"{BaseUrl}/jobs");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        
        var currentUrl = page.Url;
        
        // Should redirect to login or show login UI
        var isRedirected = !currentUrl.Contains("/jobs");
        var hasLoginButton = await page.Locator("a[href*='login'], a[href*='signin']").CountAsync() > 0;
        
        (isRedirected || hasLoginButton).Should().BeTrue("jobs page should require authentication");
    }
}
