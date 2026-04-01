using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using MeetingMinutes.Web.Shared;
using Xunit;

namespace MeetingMinutes.Web.Tests.Components;

public class LoginDisplayTests : TestContext
{
    [Fact]
    public void LoginDisplay_ShowsLoginButtons_WhenNotAuthenticated()
    {
        // Arrange
        this.AddTestAuthorization().SetNotAuthorized();
        
        // Act
        var cut = RenderComponent<LoginDisplay>();
        
        // Assert
        var microsoftLink = cut.Find("a[href='/api/auth/login/microsoft']");
        microsoftLink.Should().NotBeNull();
        microsoftLink.TextContent.Should().Contain("Sign in with Microsoft");
        
        var googleLink = cut.Find("a[href='/api/auth/login/google']");
        googleLink.Should().NotBeNull();
        googleLink.TextContent.Should().Contain("Sign in with Google");
    }
    
    [Fact]
    public void LoginDisplay_ShowsUserName_WhenAuthenticated()
    {
        // Arrange
        this.AddTestAuthorization().SetAuthorized("TestUser");
        
        // Act
        var cut = RenderComponent<LoginDisplay>();
        
        // Assert
        cut.Markup.Should().Contain("Hello, TestUser!");
    }
    
    [Fact]
    public void LoginDisplay_ShowsLogoutButton_WhenAuthenticated()
    {
        // Arrange
        this.AddTestAuthorization().SetAuthorized("TestUser");
        
        // Act
        var cut = RenderComponent<LoginDisplay>();
        
        // Assert
        var logoutLink = cut.Find("a[href='/api/auth/logout']");
        logoutLink.Should().NotBeNull();
        logoutLink.TextContent.Should().Contain("Log out");
    }
    
    [Fact]
    public void LoginDisplay_DoesNotShowLoginButtons_WhenAuthenticated()
    {
        // Arrange
        this.AddTestAuthorization().SetAuthorized("TestUser");
        
        // Act
        var cut = RenderComponent<LoginDisplay>();
        
        // Assert
        cut.FindAll("a[href='/api/auth/login/microsoft']").Should().BeEmpty();
        cut.FindAll("a[href='/api/auth/login/google']").Should().BeEmpty();
    }
}
