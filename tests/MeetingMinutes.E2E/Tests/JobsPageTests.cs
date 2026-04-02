using Microsoft.Playwright;
using FluentAssertions;
using Xunit;

namespace MeetingMinutes.E2E.Tests;

[Trait("Category", "E2E")]
public class JobsPageTests
{
    private const string BaseUrl = "http://localhost:5000";

    // Auth tests removed - auth feature deleted from project
}
