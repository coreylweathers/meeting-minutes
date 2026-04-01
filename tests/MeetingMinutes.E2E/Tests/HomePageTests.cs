using Microsoft.Playwright;
using FluentAssertions;
using Xunit;

namespace MeetingMinutes.E2E.Tests;

[Trait("Category", "E2E")]
public class HomePageTests
{
    private const string BaseUrl = "http://localhost:5000";

    [Fact]
    public async Task HomePage_LoadsWithoutError()
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync();
        var page = await browser.NewPageAsync();
        
        await page.GotoAsync(BaseUrl);
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        
        var title = await page.TitleAsync();
        title.Should().NotBeNullOrEmpty();
        
        // No error pages
        var heading = page.Locator("h1");
        var headingText = await heading.TextContentAsync();
        headingText.Should().NotContain("Error");
    }

    [Fact]
    public async Task HomePage_ShowsGetStartedButton()
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync();
        var page = await browser.NewPageAsync();
        
        await page.GotoAsync(BaseUrl);
        
        var getStartedLink = page.Locator("a[href='/upload']");
        await getStartedLink.WaitForAsync();
        var isVisible = await getStartedLink.IsVisibleAsync();
        isVisible.Should().BeTrue();
    }
    
    [Fact]
    public async Task HomePage_ShowsNavigation()
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync();
        var page = await browser.NewPageAsync();
        
        await page.GotoAsync(BaseUrl);
        
        var navLinks = page.Locator("nav a");
        var count = await navLinks.CountAsync();
        count.Should().BeGreaterThan(0);
    }
}
