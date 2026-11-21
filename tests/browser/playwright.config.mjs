import { defineConfig } from '@playwright/test';

const applicationOnly = process.argv.includes('--project=application') ||
  process.argv.some((arg, index) => arg === '--project' && process.argv[index + 1] === 'application');

export default defineConfig({
  testDir: '.',
  timeout: 30000,
  use: {
    viewport: { width: 1280, height: 720 }, trace: 'retain-on-failure',
    launchOptions: process.env.CHROMIUM_EXECUTABLE_PATH
      ? { executablePath: process.env.CHROMIUM_EXECUTABLE_PATH } : {}
  },
  projects: [
    { name: 'infrastructure', testMatch: 'infrastructure.spec.mjs', use: { baseURL: 'http://127.0.0.1:5187' } },
    { name: 'application', testMatch: 'application.spec.mjs', use: { baseURL: process.env.APP_BASE_URL ?? 'http://127.0.0.1:5188' } }
  ],
  webServer: applicationOnly ? undefined : {
    command: 'python3 -m http.server 5187 --bind 127.0.0.1 --directory ../TerritoryCapture.BrowserHarness/bin/Release/net10.0/publish/wwwroot',
    url: 'http://127.0.0.1:5187',
    reuseExistingServer: false
  }
});
