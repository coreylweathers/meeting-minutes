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
