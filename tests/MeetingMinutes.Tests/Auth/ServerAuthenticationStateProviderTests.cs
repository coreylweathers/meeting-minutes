using System.Security.Claims;
using MeetingMinutes.Web.Auth;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;
using FluentAssertions;

namespace MeetingMinutes.Tests.Auth;

public class ServerAuthenticationStateProviderTests
{
    private readonly Mock<IHttpContextAccessor> _mockHttpContextAccessor;
    private readonly ServerAuthenticationStateProvider _provider;

    public ServerAuthenticationStateProviderTests()
    {
        _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        _provider = new ServerAuthenticationStateProvider(_mockHttpContextAccessor.Object);
    }

    [Fact]
    public async Task GetAuthenticationStateAsync_ShouldReturnAuthenticatedUser_WhenUserIsAuthenticated()
    {
        // Arrange
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "user-123"),
            new Claim(ClaimTypes.Name, "Test User"),
            new Claim(ClaimTypes.Email, "test@example.com")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        var mockHttpContext = new Mock<HttpContext>();
        mockHttpContext.Setup(x => x.User).Returns(principal);

        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(mockHttpContext.Object);

        // Act
        var result = await _provider.GetAuthenticationStateAsync();

        // Assert
        result.Should().NotBeNull();
        result.User.Should().NotBeNull();
        result.User.Identity.Should().NotBeNull();
        result.User.Identity!.IsAuthenticated.Should().BeTrue();
        result.User.FindFirst(ClaimTypes.NameIdentifier)?.Value.Should().Be("user-123");
        result.User.FindFirst(ClaimTypes.Name)?.Value.Should().Be("Test User");
        result.User.FindFirst(ClaimTypes.Email)?.Value.Should().Be("test@example.com");
    }

    [Fact]
    public async Task GetAuthenticationStateAsync_ShouldReturnAnonymousUser_WhenUserIsNotAuthenticated()
    {
        // Arrange
        var identity = new ClaimsIdentity(); // No authentication type = not authenticated
        var principal = new ClaimsPrincipal(identity);

        var mockHttpContext = new Mock<HttpContext>();
        mockHttpContext.Setup(x => x.User).Returns(principal);

        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(mockHttpContext.Object);

        // Act
        var result = await _provider.GetAuthenticationStateAsync();

        // Assert
        result.Should().NotBeNull();
        result.User.Should().NotBeNull();
        result.User.Identity.Should().NotBeNull();
        result.User.Identity!.IsAuthenticated.Should().BeFalse();
    }

    [Fact]
    public async Task GetAuthenticationStateAsync_ShouldReturnAnonymousUser_WhenHttpContextIsNull()
    {
        // Arrange
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns((HttpContext?)null);

        // Act
        var result = await _provider.GetAuthenticationStateAsync();

        // Assert
        result.Should().NotBeNull();
        result.User.Should().NotBeNull();
        result.User.Identity.Should().NotBeNull();
        result.User.Identity!.IsAuthenticated.Should().BeFalse();
    }

    [Fact]
    public async Task GetAuthenticationStateAsync_ShouldReturnAnonymousUser_WhenUserIsNull()
    {
        // Arrange
        var mockHttpContext = new Mock<HttpContext>();
        mockHttpContext.Setup(x => x.User).Returns((ClaimsPrincipal)null!);

        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(mockHttpContext.Object);

        // Act
        var result = await _provider.GetAuthenticationStateAsync();

        // Assert
        result.Should().NotBeNull();
        result.User.Should().NotBeNull();
        result.User.Identity.Should().NotBeNull();
        result.User.Identity!.IsAuthenticated.Should().BeFalse();
    }

    [Fact]
    public async Task GetAuthenticationStateAsync_ShouldReturnAnonymousUser_WhenIdentityIsNull()
    {
        // Arrange
        var mockIdentity = new Mock<ClaimsIdentity>();
        mockIdentity.Setup(x => x.IsAuthenticated).Returns(false);
        
        var principal = new ClaimsPrincipal(mockIdentity.Object);

        var mockHttpContext = new Mock<HttpContext>();
        mockHttpContext.Setup(x => x.User).Returns(principal);

        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(mockHttpContext.Object);

        // Act
        var result = await _provider.GetAuthenticationStateAsync();

        // Assert
        result.Should().NotBeNull();
        result.User.Should().NotBeNull();
        result.User.Identity!.IsAuthenticated.Should().BeFalse();
    }

    [Fact]
    public async Task GetAuthenticationStateAsync_ShouldHandleMissingClaims()
    {
        // Arrange - User authenticated but no claims
        var identity = new ClaimsIdentity(Array.Empty<Claim>(), "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        var mockHttpContext = new Mock<HttpContext>();
        mockHttpContext.Setup(x => x.User).Returns(principal);

        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(mockHttpContext.Object);

        // Act
        var result = await _provider.GetAuthenticationStateAsync();

        // Assert
        result.Should().NotBeNull();
        result.User.Identity!.IsAuthenticated.Should().BeTrue();
        result.User.FindFirst(ClaimTypes.NameIdentifier).Should().BeNull();
        result.User.Claims.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAuthenticationStateAsync_ShouldPreserveClaims_FromHttpContext()
    {
        // Arrange
        var customClaims = new[]
        {
            new Claim("custom_claim", "custom_value"),
            new Claim("role", "admin"),
            new Claim("tenant_id", "tenant-456")
        };
        var identity = new ClaimsIdentity(customClaims, "OAuth");
        var principal = new ClaimsPrincipal(identity);

        var mockHttpContext = new Mock<HttpContext>();
        mockHttpContext.Setup(x => x.User).Returns(principal);

        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(mockHttpContext.Object);

        // Act
        var result = await _provider.GetAuthenticationStateAsync();

        // Assert
        result.User.FindFirst("custom_claim")?.Value.Should().Be("custom_value");
        result.User.FindFirst("role")?.Value.Should().Be("admin");
        result.User.FindFirst("tenant_id")?.Value.Should().Be("tenant-456");
    }
}
