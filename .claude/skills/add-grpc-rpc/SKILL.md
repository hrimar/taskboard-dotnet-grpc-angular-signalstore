---
name: add-grpc-rpc
description: End-to-end workflow for adding or changing a gRPC RPC in TaskBoard - .proto contract, C# service, Application mapping, TypeScript codegen, SignalStore method and tests. Use when a feature needs a new endpoint, a new field on a message, or a changed request/response.
argument-hint: "<Service>.<RpcName> - short description"
---

# Add or change a gRPC RPC

Work through the layers in this order - each step compiles against the previous one. Delegate backend steps to the `dotnet-backend` agent and frontend steps to `angular-frontend` when the change is large.

## 1. Contract - `src/TaskBoard.Contracts/Protos/<entity>.proto`
- Add the `rpc` to the `service` block and a dedicated `<RpcName>Request` / `<RpcName>Response` (or return the entity / `google.protobuf.Empty`). Never reuse another RPC's request message.
- Follow the file's existing conventions: `snake_case` fields, `google.protobuf.Timestamp` for dates, `optional` for "not set" vs. default, `FieldMask` for partial updates of large messages, `page_size` / `page_token` / `next_page_token` for lists.
- Backward compatibility: only add fields with new numbers; removed fields become `reserved`; enum zero value is `*_UNSPECIFIED`.
- Server streaming: `rpc X(XRequest) returns (stream XEvent);`.
- New `.proto` file? Add it to `protoFiles` in `client/taskboard-web/tools/generate-proto.mjs`.
- The `proto-codegen` hook regenerates the TS clients after the edit - fix any protoc error it reports before going on.

## 2. Build the contract
`dotnet build src/TaskBoard.Contracts` - generates `<Service>Base` with the new virtual method.

## 3. Mapping - `src/TaskBoard.Application/Mapping/<Entity>MappingExtensions.cs`
Add `ToProto` / `ToDomain` / `ApplyTo` for new messages or fields. DB lookups (existence checks, label resolution) do not belong here.

## 4. Service - `src/TaskBoard.Api/Services/<Entity>GrpcService.cs`
- `public override async Task<TResponse> RpcName(TRequest request, ServerCallContext context)`.
- Validate input first -> `RpcException(new Status(StatusCode.InvalidArgument, "..."))`; missing entity -> `NotFound`.
- Pass `context.CancellationToken` to EF Core calls.
- New service class? Register it with `app.MapGrpcService<T>()` in `Program.cs`.
- `dotnet build TaskBoard.sln`.

## 5. Manual check
Run the API (`dotnet run --project src/TaskBoard.Api --launch-profile http`) and call the RPC from Postman / grpcurl, including one error path.

## 6. Frontend
- `npm run proto:gen` (already done by the hook) - the client method appears on `<Service>Client`.
- Add the store method in `<feature>/data-access/<feature>.store.ts` following the `signalstore-feature` skill.

## 7. Tests
Hand off to the `test-engineer` agent: integration test for the RPC (happy path + each status code), store spec for the new method.

## Done when
Solution builds, `npm run build` passes, tests are green, and the user has a suggested commit message. Do not commit.
