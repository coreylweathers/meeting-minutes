using Microsoft.Playwright;
using FluentAssertions;
using Xunit;

namespace MeetingMinutes.E2E.Tests;

[Trait("Category", "E2E-Auth")]
public class UploadFlowTests
{
    private const string BaseUrl = "http://localhost:5000";

    [Fact(Skip = "Requires authenticated session — set up auth fixture first")]
    public async Task UploadPage_ShowsFilePickerWhenAuthenticated()
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync();
        var page = await browser.NewPageAsync();
        
        // TODO: set up authenticated session cookie
        await page.GotoAsync($"{BaseUrl}/upload");
        
        var fileInput = page.Locator("input[type='file']");
        await fileInput.WaitForAsync();
        var isVisible = await fileInput.IsVisibleAsync();
        isVisible.Should().BeTrue();
    }
    
    [Fact(Skip = "Requires authenticated session")]
    public async Task UploadPage_ShowsTitleInput()
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync();
        var page = await browser.NewPageAsync();
        
        // TODO: authenticated session
        await page.GotoAsync($"{BaseUrl}/upload");
        
        var titleInput = page.Locator("input#title");
        await titleInput.WaitForAsync();
        var isVisible = await titleInput.IsVisibleAsync();
        isVisible.Should().BeTrue();
    }
    
    [Fact(Skip = "Requires authenticated session and valid file upload")]
    public async Task UploadPage_SubmitsVideoFile()
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync();
        var page = await browser.NewPageAsync();
        
        // TODO: authenticated session
        // TODO: Upload a test video file, expect success message and redirect to job detail
    }
}
