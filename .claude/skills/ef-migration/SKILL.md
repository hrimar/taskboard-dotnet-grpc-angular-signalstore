---
name: ef-migration
description: Safely change the TaskBoard database schema with EF Core 8 and SQLite - entity + Fluent API configuration, generating and reviewing a migration, applying it. Use whenever a Domain entity, relationship or EF configuration changes.
argument-hint: "<PascalCaseMigrationName>"
---

# EF Core migration

`TaskBoardDbContext` lives in `src/TaskBoard.Infrastructure`; `src/TaskBoard.Api` is the startup project (it holds the connection string). Every `dotnet ef` command needs both flags.

## Steps
1. Change the entity in `src/TaskBoard.Domain/` and its `IEntityTypeConfiguration<T>` in `src/TaskBoard.Infrastructure/Persistence/Configurations/` (keys, max lengths, required, relationships, delete behavior). No data annotations on Domain entities.
2. `dotnet build TaskBoard.sln`.
3. Generate (name in PascalCase, describing the change, e.g. `AddTaskItemDueDate`):
   ```bash
   dotnet ef migrations add $ARGUMENTS --project src/TaskBoard.Infrastructure --startup-project src/TaskBoard.Api --output-dir Persistence/Migrations
   ```
4. **Review the generated `Up`/`Down`** before applying - and show the user:
   - any `DropColumn` / `DropTable` / column rename shown as drop + add (data loss);
   - new non-nullable columns on existing tables need a `defaultValue`;
   - SQLite cannot alter many column properties - EF rebuilds the table; make sure that is expected.
   If it is wrong, fix the model and regenerate (`dotnet ef migrations remove ...` with the same flags, only if not yet applied) - never hand-edit the migration.
5. Apply: `dotnet ef database update --project src/TaskBoard.Infrastructure --startup-project src/TaskBoard.Api`.
6. Verify from scratch: stop the API, delete `src/TaskBoard.Api/taskboard.db*`, run `database update` again - the full migration chain must build the schema on an empty database.
7. Update the mapping extensions and `.proto` if the new column is exposed through the API (see the `add-grpc-rpc` skill).

## Done when
Migration files + model snapshot + entity/configuration changes are ready to be committed together by the user (one commit), with a suggested message such as `Added <Name> migration ...`.
