import { defineConfig, devices } from '@playwright/test';

// Full-stack E2E — see docs/development/e2e-testing.md for the decision
// record (QA-01) and docs/architecture/decisions/... for why Playwright.
//
// Both the API and the pos dev server are started here so `npx playwright
// test` works standalone. Docker (PostgreSQL) is NOT started — it is a
// prerequisite, the same way it is for `dotnet run`. If it isn't up, the API
// webServer fails fast with a clear error rather than hanging.
const apiBaseUrl = process.env.BRASA_API_BASE_URL ?? 'http://localhost:5216';
const posBaseUrl = process.env.BRASA_POS_BASE_URL ?? 'http://localhost:5173';
const adminBaseUrl = process.env.BRASA_ADMIN_BASE_URL ?? 'http://localhost:5174';

export default defineConfig({
  testDir: './tests',
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  workers: process.env.CI ? 1 : undefined,
  reporter: 'html',
  use: {
    baseURL: posBaseUrl,
    trace: 'on-first-retry',
  },

  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],

  webServer: [
    {
      command: 'dotnet run --project ../../backend/Brasa.Api --no-build',
      url: `${apiBaseUrl}/health`,
      reuseExistingServer: !process.env.CI,
      timeout: 60_000,
      env: { ASPNETCORE_ENVIRONMENT: 'Development' },
    },
    {
      command: 'npm run dev -- --port 5173 --strictPort',
      cwd: '../pos',
      url: posBaseUrl,
      reuseExistingServer: !process.env.CI,
      timeout: 30_000,
    },
    {
      command: 'npm run dev -- --port 5174 --strictPort',
      cwd: '../admin',
      url: adminBaseUrl,
      reuseExistingServer: !process.env.CI,
      timeout: 30_000,
    },
  ],
});
