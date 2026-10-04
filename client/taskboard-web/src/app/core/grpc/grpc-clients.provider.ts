import { InjectionToken, type Provider } from '@angular/core';
import { GrpcWebFetchTransport } from '@protobuf-ts/grpcweb-transport';
import { environment } from '../../../environments/environment';
import { BoardServiceClient } from '../proto-gen/board.client';
import { LabelServiceClient } from '../proto-gen/label.client';
import { TaskItemServiceClient } from '../proto-gen/taskitem.client';

// One transport, shared by every service client - it just knows the API's base URL
// and how to frame gRPC-Web requests/responses over fetch(). The generated *ServiceClient
// classes are thin wrappers around it, one per proto `service`.
const transport = new GrpcWebFetchTransport({
  baseUrl: environment.apiUrl
});

// // BoardServiceClient is generated from the protobuf-ts when npm run proto:gen is called by tools/generate-proto.mjs script.
export const BOARD_CLIENT = new InjectionToken<BoardServiceClient>('BOARD_CLIENT');
export const LABEL_CLIENT = new InjectionToken<LabelServiceClient>('LABEL_CLIENT');
export const TASK_ITEM_CLIENT = new InjectionToken<TaskItemServiceClient>('TASK_ITEM_CLIENT');

// Registered once in app.config.ts, injected wherever a store/service needs to call the API -
// see https://angular.dev/guide/di for the injection token pattern this follows.
export const grpcClientProviders: Provider[] = [
  { provide: BOARD_CLIENT, useValue: new BoardServiceClient(transport) },
  { provide: LABEL_CLIENT, useValue: new LabelServiceClient(transport) },
  { provide: TASK_ITEM_CLIENT, useValue: new TaskItemServiceClient(transport) }
];
