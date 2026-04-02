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
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http;
using Xunit;

namespace MeetingMinutes.Web.Tests.Components;

public class UploadPageTests : TestContext
{
    [Fact]
    public void UploadPage_DoesNotRequire_Authorization()
    {
        // Arrange - auth removed from project
        var authorizeAttribute = typeof(Upload)
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), false)
            .FirstOrDefault();
        
        // Assert
        authorizeAttribute.Should().BeNull("Upload page should NOT have [Authorize] attribute after auth removal");
    }
    
    [Fact]
    public void UploadPage_Renders_TitleInput()
    {
        // Arrange
        var mockHttpClient = new HttpClient(new MockHttpMessageHandler()) { BaseAddress = new Uri("http://localhost") };
        Services.Add(ServiceDescriptor.Singleton(mockHttpClient));
        
        // Act
        var cut = RenderComponent<Upload>();
        
        // Assert
        var titleInput = cut.Find("input#title");
        titleInput.Should().NotBeNull();
        titleInput.GetAttribute("placeholder").Should().Contain("meeting title");
    }
    
    [Fact]
    public void UploadPage_Renders_FileInput()
    {
        // Arrange
        var mockHttpClient = new HttpClient(new MockHttpMessageHandler()) { BaseAddress = new Uri("http://localhost") };
        Services.Add(ServiceDescriptor.Singleton(mockHttpClient));
        
        // Act
        var cut = RenderComponent<Upload>();
        
        // Assert
        var fileInput = cut.Find("input#file");
        fileInput.Should().NotBeNull();
        fileInput.GetAttribute("type").Should().Be("file");
        fileInput.GetAttribute("accept").Should().Contain("video");
    }
    
    [Fact]
    public void UploadPage_Renders_SubmitButton()
    {
        // Arrange
        var mockHttpClient = new HttpClient(new MockHttpMessageHandler()) { BaseAddress = new Uri("http://localhost") };
        Services.Add(ServiceDescriptor.Singleton(mockHttpClient));
        
        // Act
        var cut = RenderComponent<Upload>();
        
        // Assert
        var submitButton = cut.Find("button[type='submit']");
        submitButton.Should().NotBeNull();
        submitButton.TextContent.Should().Contain("Upload Video");
    }
    
    [Fact]
    public void UploadPage_SubmitButton_IsDisabled_WhenNoFileSelected()
    {
        // Arrange
        var mockHttpClient = new HttpClient(new MockHttpMessageHandler()) { BaseAddress = new Uri("http://localhost") };
        Services.Add(ServiceDescriptor.Singleton(mockHttpClient));
        
        // Act
        var cut = RenderComponent<Upload>();
        
        // Assert
        var submitButton = cut.Find("button[type='submit']");
        submitButton.HasAttribute("disabled").Should().BeTrue();
    }
    
    [Fact(Skip = "PageTitle component renders to document head, not testable in bUnit")]
    public void UploadPage_HasCorrectPageTitle()
    {
        // bUnit doesn't render <PageTitle> to DOM. PageTitle affects document head at runtime.
        // Arrange
        var mockHttpClient = new HttpClient(new MockHttpMessageHandler()) { BaseAddress = new Uri("http://localhost") };
        Services.Add(ServiceDescriptor.Singleton(mockHttpClient));
        
        // Act
        var cut = RenderComponent<Upload>();
        
        // Assert
        var pageTitle = cut.Find("title");
        pageTitle.TextContent.Should().Contain("Upload");
    }
}

// Mock HTTP message handler to prevent real HTTP calls
internal class MockHttpMessageHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
    }
}
