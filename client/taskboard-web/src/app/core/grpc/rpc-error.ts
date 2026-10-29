import { RpcError } from '@protobuf-ts/runtime-rpc';

// RpcError (thrown by every @protobuf-ts client call that fails) carries the gRPC
// status message our *GrpcService NotFound/InvalidArgument RpcExceptions set server-side -
// falling back to a generic message for anything else (network failure, CORS, ...).
export function toErrorMessage(error: unknown): string {
  return error instanceof RpcError ? error.message : 'Unexpected error calling the API.';
}
