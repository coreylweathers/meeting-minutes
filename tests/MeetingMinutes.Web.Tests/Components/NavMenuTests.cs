using Bunit;
using FluentAssertions;
using MeetingMinutes.Web.Layout;
using Xunit;

namespace MeetingMinutes.Web.Tests.Components;

public class NavMenuTests : TestContext
{
    [Fact]
    public void NavMenu_Renders_HomeLink()
    {
        // Arrange & Act
        var cut = RenderComponent<NavMenu>();
        
        // Assert
        var homeLink = cut.Find("a[href='/']");
        homeLink.Should().NotBeNull();
        homeLink.TextContent.Should().Contain("Home");
    }
    
    [Fact]
    public void NavMenu_Renders_UploadLink()
    {
        // Arrange & Act
        var cut = RenderComponent<NavMenu>();
        
        // Assert
        var uploadLink = cut.Find("a[href='/upload']");
        uploadLink.Should().NotBeNull();
        uploadLink.TextContent.Should().Contain("Upload");
    }
    
    [Fact]
    public void NavMenu_Renders_JobsLink()
    {
        // Arrange & Act
        var cut = RenderComponent<NavMenu>();
        
        // Assert
        var jobsLink = cut.Find("a[href='/jobs']");
        jobsLink.Should().NotBeNull();
        jobsLink.TextContent.Should().Contain("Jobs");
    }
    
    [Fact]
    public void NavMenu_Contains_AllExpectedLinks()
    {
        // Arrange & Act
        var cut = RenderComponent<NavMenu>();
        
        // Assert
        var links = cut.FindAll("a");
        links.Should().HaveCount(3, "NavMenu should have Home, Upload, and Jobs links");
    }
}
