# E2E Tests — Playwright

These tests require the application to be running locally.

## Prerequisites
1. Install Playwright browsers: `pwsh bin\Debug\net10.0\playwright.ps1 install`
2. Start the Aspire app: `dotnet run --project src/MeetingMinutes.AppHost`
3. Wait for Web to be available at http://localhost:5000

## Running Tests
```bash
# Run all E2E tests (app must be running)
dotnet test tests/MeetingMinutes.E2E/ --filter "Category=E2E"

# Skip auth-required tests
dotnet test tests/MeetingMinutes.E2E/ --filter "Category=E2E&Category!=E2E-Auth"
```

## Auth Tests
Tests in `E2E-Auth` category require a real authenticated session.
Set up a test identity provider or use cookie injection before enabling these.

## Test Coverage
- ✅ HomePage loads without error
- ✅ Navigation links present and functional
- ✅ Auth flow (unauthenticated users redirected or shown login)
- ⏭️ Upload flow (requires auth fixture)
- ⏭️ Jobs page (requires auth fixture)
- ⏭️ Job detail page (requires auth fixture and test data)

## Notes
- Tests run against live app at http://localhost:5000
- Chromium browser is launched headless for each test
- Screenshots captured on failure
- Traces captured on retry
