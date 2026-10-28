// PostToolUse hook (Edit|Write): after any .proto change, regenerate the TypeScript clients
// right away, so the Angular code never compiles against a stale contract and protoc syntax
// errors surface immediately instead of at the next `npm start`.
import { readFileSync } from 'node:fs';
import { execFileSync } from 'node:child_process';
import path from 'node:path';

const input = JSON.parse(readFileSync(0, 'utf8'));
const filePath = input.tool_input?.file_path ?? '';

if (!filePath.endsWith('.proto')) process.exit(0);

const projectDir = process.env.CLAUDE_PROJECT_DIR ?? input.cwd;
const generator = path.join(projectDir, 'client', 'taskboard-web', 'tools', 'generate-proto.mjs');

try {
  // Same argv-array approach as generate-proto.mjs itself: the repo path contains spaces.
  execFileSync(process.execPath, [generator], { encoding: 'utf8', stdio: 'pipe' });
} catch (error) {
  // Exit code 2 feeds stderr back to Claude, so it can fix the .proto it just broke.
  console.error(`proto:gen failed after editing ${path.basename(filePath)}:\n${error.stderr || error.message}`);
  process.exit(2);
}
