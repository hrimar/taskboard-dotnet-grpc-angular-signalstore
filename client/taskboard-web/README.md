# TaskboardWeb

Angular 18 client for TaskBoard. It talks to the .NET API over gRPC-Web, with clients generated from the `.proto` files in `src/TaskBoard.Contracts/Protos`. State lives in NgRx SignalStore stores.

This project was generated with [Angular CLI](https://github.com/angular/angular-cli) version 18.2.21.

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

## Code scaffolding

Run `ng generate component component-name` to generate a new component. You can also use `ng generate directive|pipe|service|class|guard|interface|enum|module`.

## Running end-to-end tests

Playwright tests live in `e2e/`. Once per machine, install the browser: `npx playwright install chromium`. Then:

```bash
npm run e2e                  # headless; starts its own API (:5180, fresh e2e/e2e.db) and ng serve (:4300)
npx playwright test --ui     # interactive mode, step through each test
npx playwright show-report   # HTML report of the last run
```

The test servers use their own ports and database, so the run never touches the dev API (:5063), `taskboard.db` or `ng serve` (:4200), and works while those are running. Requires the .NET 8 SDK, because the API is started with `dotnet run`.

## Further help

To get more help on the Angular CLI use `ng help` or go check out the [Angular CLI Overview and Command Reference](https://angular.dev/tools/cli) page.
