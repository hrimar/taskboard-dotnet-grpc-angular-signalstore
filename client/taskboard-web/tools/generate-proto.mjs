// Generates TypeScript message + client code from the shared .proto contracts.
//
// Runs protoc via a Node child_process argument array instead of a shell command string.
// This matters specifically because this repo's path contains a space
// ("gRPC API with Angular") - a shell string command breaks that path apart at the
// space no matter how it's quoted on Windows, while an argv array never goes through
// shell parsing at all, so spaces in paths are simply not a problem.
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { mkdirSync } from 'node:fs';
import path from 'node:path';

const clientRoot = path.dirname(path.dirname(fileURLToPath(import.meta.url)));

const protocBin = path.join(
  clientRoot,
  'node_modules',
  'grpc-tools',
  'bin',
  process.platform === 'win32' ? 'protoc.exe' : 'protoc'
);

const pluginBin = path.join(
  clientRoot,
  'node_modules',
  '.bin',
  process.platform === 'win32' ? 'protoc-gen-ts.cmd' : 'protoc-gen-ts'
);

const protosDir = path.resolve(clientRoot, '..', '..', 'src', 'TaskBoard.Contracts', 'Protos');
const outDir = path.join(clientRoot, 'src', 'app', 'core', 'proto-gen');

// Add new .proto files here as they're introduced (greet.proto is Api-only test scaffolding
// and deliberately left out - the Angular app never calls it).
const protoFiles = ['board.proto', 'label.proto', 'taskitem.proto'].map((f) =>
  path.join(protosDir, f)
);

mkdirSync(outDir, { recursive: true });

execFileSync(
  protocBin,
  [
    `--plugin=protoc-gen-ts=${pluginBin}`,
    `--ts_out=${outDir}`,
    `--proto_path=${protosDir}`,
    ...protoFiles
  ],
  { stdio: 'inherit' }
);

console.log(`Generated TypeScript proto code into ${outDir}`);
