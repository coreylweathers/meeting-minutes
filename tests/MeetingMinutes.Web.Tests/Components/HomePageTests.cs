using Bunit;
using Bunit.TestDoubles;
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
        heading.TextContent.Should().Contain("Meeting Minutes");
    }
    
    [Fact]
    public void HomePage_Renders_GetStartedButton()
    {
        // Arrange & Act
        var cut = RenderComponent<Home>();
        
        // Assert
        var getStartedLink = cut.Find("a[href='/upload']");
        getStartedLink.Should().NotBeNull();
        getStartedLink.TextContent.Should().Contain("Get Started");
    }
    
    [Fact]
    public void HomePage_Renders_DescriptiveText()
    {
        // Arrange & Act
        var cut = RenderComponent<Home>();
        
        // Assert
        cut.Markup.Should().Contain("Upload your meeting videos");
        cut.Markup.Should().Contain("AI-generated transcripts");
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
