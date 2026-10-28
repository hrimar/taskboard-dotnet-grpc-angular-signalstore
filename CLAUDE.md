# CLAUDE.md

## Project

TaskBoard is a mini Kanban app (Boards → Tasks → Labels). It is a **learning project** for gRPC on .NET 8 and Angular 18 + NgRx SignalStore, and an exam project for an AI-Assisted Development course. Clear, well-explained code matters more than feature count. Roadmap: `README.md`.

## Working rules

- **Never run `git commit` / `git push`** unless the user explicitly asks for that specific action. The user reviews and commits everything. End each step by proposing a commit message in the existing style ("Added …", "Implemented …", "Moved …").
- Guided mode: explain *what / why / how* for every non-trivial change. Explanations to the user in Bulgarian; code, comments and docs in English.
- Comments in this codebase explain *why* (protocol quirks, framework pitfalls), not *what*. Keep that density and tone.

## Commands

Backend (repo root; .NET 8 SDK pinned in `global.json`):
```bash
dotnet build TaskBoard.sln
dotnet run --project src/TaskBoard.Api --launch-profile http     # http://localhost:5063
# EF Core migrations: DbContext lives in Infrastructure, Api is the startup project
dotnet ef migrations add <Name> --project src/TaskBoard.Infrastructure --startup-project src/TaskBoard.Api --output-dir Persistence/Migrations
dotnet ef database update --project src/TaskBoard.Infrastructure --startup-project src/TaskBoard.Api
```

Frontend (`client/taskboard-web`, Angular 18.2, Node 20):
```bash
npm install
npm start                 # runs proto:gen first (prestart), then ng serve on :4200
npm run build             # runs proto:gen first (prebuild)
npm run proto:gen         # regenerate TS clients from the .proto files
npm test                  # Karma/Jasmine
npx ng test --watch=false --browsers=ChromeHeadless --include src/app/boards/**/*.spec.ts   # single spec / folder
```

CI (`.github/workflows/ci.yml`) builds and tests both sides on every PR. The backend job also runs `dotnet test` (xUnit project `tests/TaskBoard.Api.Tests`: mapping unit tests + gRPC integration tests over `WebApplicationFactory<Program>` with an in-memory SQLite database per test); Playwright tests are planned.

## Architecture

**Contract-first.** `src/TaskBoard.Contracts/Protos/*.proto` (package `taskboard.v1`, C# namespace `TaskBoard.Contracts.V1`) is the single source of truth for both sides:
- C#: compiled once in `TaskBoard.Contracts.csproj` (`GrpcServices="Both"` – the C# clients are used by the integration tests); other projects get the types via ProjectReference.
- TypeScript: `client/taskboard-web/tools/generate-proto.mjs` runs protoc + `@protobuf-ts/plugin` into `src/app/core/proto-gen/` (gitignored, regenerated on every start/build). **A new `.proto` file must be added to the `protoFiles` list in that script.**
- Never hand-edit `proto-gen/` or `Persistence/Migrations/`. Keep proto changes backward-compatible (don't renumber or reuse field numbers; enums start with a `*_UNSPECIFIED = 0`).

**Server layers** (`src/`): `Api` → `Application` → `Domain`, plus `Infrastructure` (EF Core). Domain has no dependencies.
- `Api/Services/*GrpcService.cs` override the generated `*Service.*ServiceBase`, use `TaskBoardDbContext` directly and signal errors with `RpcException` + proper `StatusCode` (`NotFound`, `InvalidArgument`, …). Registered with `MapGrpcService<>` in `Program.cs`.
- `Application/Mapping/*MappingExtensions.cs` hold all proto ↔ domain conversion (`ToProto()`, `ToDomain()`, `ApplyTo(entity)` for updates). Domain and proto types share names, so these files use `using Domain… =` aliases. Mappings that need DB access (board existence, label resolution) stay in the service.
- Two partial-update styles exist: `UpdateBoardRequest` uses `optional` fields; `UpdateTaskItemRequest` uses `google.protobuf.FieldMask`.
- Timestamps are stored as UTC `DateTime`; mapping uses `DateTime.SpecifyKind(..., Utc)` before `Timestamp.FromDateTime`. Task labels are many-to-many and only mapped when loaded with `.Include(t => t.Labels)`.
- Infrastructure: `TaskBoardDbContext` applies every `IEntityTypeConfiguration<T>` in `Persistence/Configurations/`. SQLite file `taskboard.db` (relative `Data Source`, gitignored).

**Browser ↔ gRPC.** Browsers can't speak native gRPC, so the API uses **gRPC-Web** (`app.UseGrpcWeb(DefaultEnabled = true)`):
- Kestrel `Protocols: Http1AndHttp2` (appsettings.json) – the browser uses HTTP/1.1 over plain HTTP, native clients HTTP/2 on the same port.
- CORS origins come from `Cors:AllowedOrigins` (appsettings.Development.json → `http://localhost:4200`); the policy must expose `Grpc-Status`, `Grpc-Message`, `Grpc-Encoding` or the client can't read call status.
- Client API URL: `src/environments/environment*.ts` (`apiUrl`), swapped via `fileReplacements` in angular.json.

**Client** (`client/taskboard-web/src/app`, standalone components, Angular Material):
- `core/grpc/grpc-clients.provider.ts`: one `GrpcWebFetchTransport` shared by all generated clients, exposed as DI tokens `BOARD_CLIENT`, `LABEL_CLIENT`, `TASK_ITEM_CLIENT` (registered in `app.config.ts`). Inject tokens, never `new` a client.
- State: NgRx SignalStore per feature in `<feature>/data-access/*.store.ts` (see `boards/data-access/boards.store.ts` as the reference pattern): `withEntities<T>()` + `withState` for `loading`/`error` + `withMethods` with `rxMethod`. Client calls return Promises – wrap with `from(client.x(req).response)`, and always handle results with `tapResponse` (a raw error would kill the `rxMethod` stream). Map errors through `RpcError.message`.
- `AppComponent` is still a temporary smoke test; `app.routes.ts` is empty – feature routes/components are the next step.

