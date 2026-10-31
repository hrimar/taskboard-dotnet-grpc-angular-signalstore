# TaskBoard web client

Angular 18 client for TaskBoard. It talks to the .NET API over gRPC-Web, with clients generated from the `.proto` files in `src/TaskBoard.Contracts/Protos`. State lives in NgRx SignalStore stores.

## Structure

```
src/app/
  core/grpc/        # shared gRPC-Web transport + DI tokens (BOARD_CLIENT, LABEL_CLIENT, TASK_ITEM_CLIENT), RpcError helper
  core/proto-gen/   # generated clients (gitignored, never edit by hand)
  boards/           # data-access (BoardsStore), feature (list, detail page), ui (form dialog)
  tasks/            # data-access (TasksStore, WatchBoard adapter), feature (Kanban board), ui (card, form dialog)
  labels/           # data-access (LabelsStore)
  shared/ui/        # reusable confirm dialog
e2e/                # Playwright specs, shared helpers and the test API start script
```

## Prerequisites

- Node 20
- `npm install` (once, and after `package.json` changes)
- The API running for anything that loads data: `dotnet run --project src/TaskBoard.Api --launch-profile http` from the repository root (http://localhost:5063)

## Development server

Run `npm start` and navigate to `http://localhost:4200/`. The application reloads automatically when you change a source file.

Use the npm scripts, not `ng serve` / `ng build` directly: `npm start` and `npm run build` first run `npm run proto:gen`, which generates the TypeScript gRPC clients into `src/app/core/proto-gen/` (gitignored). Without it a fresh checkout does not compile.

## Build

Run `npm run build`. The build artifacts are stored in the `dist/` directory.

## Running unit tests

Run `npm test` to execute the unit tests via [Karma](https://karma-runner.github.io) in watch mode.

For a single headless run, as the CI does:

```bash
npx ng test --watch=false --browsers=ChromeHeadless
```

To run only one spec or folder, add `--include`, for example `--include 'src/app/boards/**/*.spec.ts'`.

Run `npm run proto:gen` first if `src/app/core/proto-gen/` does not exist yet (`npm start` / `npm run build` do it for you).

## Running end-to-end tests

Playwright tests live in `e2e/`. Once per machine, install the browser: `npx playwright install chromium`. Then:

```bash
npm run e2e                  # headless; starts its own API (:5180, fresh e2e/e2e.db) and ng serve (:4300)
npx playwright test --ui     # interactive mode, step through each test
npx playwright show-report   # HTML report of the last run
```

The test servers use their own ports and database, so the run never touches the dev API (:5063), `taskboard.db` or `ng serve` (:4200), and works while those are running. Requires the .NET 8 SDK, because the API is started with `dotnet run`.
