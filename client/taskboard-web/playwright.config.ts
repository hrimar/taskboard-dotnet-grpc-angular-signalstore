import { defineConfig, devices } from '@playwright/test';

const isCI = !!process.env['CI'];

// Separate ports from the dev servers (API 5063, ng serve 4200), so the e2e run gets its own
// database and can run while the dev servers are up. See e2e/start-api.mjs.
export const apiUrl = 'http://localhost:5180';
const webUrl = 'http://localhost:4300';

export default defineConfig({
  testDir: 'e2e',
  // Tests share one database per run; they stay independent by using unique board names.
  fullyParallel: true,
  forbidOnly: isCI,
  retries: isCI ? 1 : 0,
  workers: isCI ? 1 : undefined,
  reporter: isCI ? [['github'], ['html', { open: 'never' }]] : [['html', { open: 'never' }]],
  use: {
    baseURL: webUrl,
    trace: 'on-first-retry'
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
  webServer: [
    {
      command: 'node e2e/start-api.mjs',
      // "/" is the plain MapGet in Program.cs: it answers only once Kestrel listens.
      url: `${apiUrl}/`,
      timeout: 120_000,
      reuseExistingServer: !isCI
    },
    {
      // npm start would serve the dev configuration on 4200; proto:gen is what prestart adds.
      command: 'npm run proto:gen && npx ng serve --configuration e2e --port 4300',
      url: webUrl,
      timeout: 180_000,
      reuseExistingServer: !isCI
    }
  ]
});
