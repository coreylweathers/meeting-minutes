// Meeting Minutes - AI-powered meeting transcription and summarization.
// Copyright (C) 2026 Corey Weathers
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.

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
