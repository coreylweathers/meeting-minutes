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

using Bunit;
using FluentAssertions;
using MeetingMinutes.Web.Pages;
using Xunit;

namespace MeetingMinutes.Web.Tests.Components;

public class HomePageTests : TestContext
{
    [Fact]
    public void HomePage_Renders_MainHeading()
    {
        // Arrange & Act
        var cut = RenderComponent<Home>();
        
        // Assert
        var heading = cut.Find("h1");
        heading.Should().NotBeNull();
        heading.TextContent.Should().Contain("Insights");
    }
    
    [Fact]
    public void HomePage_Renders_GetStartedButton()
    {
        // Arrange & Act
        var cut = RenderComponent<Home>();
        
        // Assert
        var getStartedLink = cut.Find("a[href='/upload']");
        getStartedLink.Should().NotBeNull();
        getStartedLink.TextContent.Should().Contain("Upload Recording");
    }
    
    [Fact]
    public void HomePage_Renders_DescriptiveText()
    {
        // Arrange & Act
        var cut = RenderComponent<Home>();
        
        // Assert
        cut.Markup.Should().Contain("Transform chaotic conversation");
        cut.Markup.Should().Contain("AI");
    }
    
    [Fact(Skip = "PageTitle component renders to document head, not testable in bUnit")]
    public void HomePage_HasCorrectPageTitle()
    {
        // bUnit doesn't render <PageTitle> to DOM. PageTitle affects document head at runtime.
        // Arrange & Act
        var cut = RenderComponent<Home>();
        
        // Assert
        var pageTitle = cut.Find("title");
        pageTitle.Should().NotBeNull();
        pageTitle.TextContent.Should().Contain("Home");
    }
}
