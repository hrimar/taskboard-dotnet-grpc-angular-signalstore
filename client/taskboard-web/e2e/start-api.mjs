// Playwright webServer for the API: a fresh SQLite file on its own port for every e2e run.
// The database is deleted here and not in playwright.config.ts because the config is also
// evaluated in every test worker - it would delete the file under the running server.
import { spawn } from 'node:child_process';
import { rmSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const repoRoot = resolve(here, '../../..');
const dbPath = resolve(here, 'e2e.db');

// -shm/-wal are SQLite's side files; a stale one next to a new database would be read back.
for (const suffix of ['', '-shm', '-wal']) rmSync(dbPath + suffix, { force: true });

// Release, not the default Debug: a running dev API (`dotnet run`) locks bin/Debug on Windows,
// so a Debug build here would fail exactly when both are meant to run side by side.
const api = spawn(
  'dotnet',
  ['run', '--project', resolve(repoRoot, 'src/TaskBoard.Api'), '--configuration', 'Release', '--no-launch-profile'],
  {
    stdio: 'inherit',
    env: {
      ...process.env,
      // --no-launch-profile: launchSettings.json would pin the dev port 5063.
      ASPNETCORE_ENVIRONMENT: 'Development',
      ASPNETCORE_URLS: 'http://localhost:5180',
      ConnectionStrings__TaskBoard: `Data Source=${dbPath}`,
      Cors__AllowedOrigins__0: 'http://localhost:4300',
      Database__MigrateOnStartup: 'true'
    }
  }
);

// Playwright stops this script at the end of the run; pass that on so dotnet does not linger.
for (const signal of ['SIGINT', 'SIGTERM']) process.on(signal, () => api.kill(signal));
api.on('exit', (code) => process.exit(code ?? 0));
