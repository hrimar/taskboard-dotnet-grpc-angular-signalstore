// PreToolUse hook (Bash|PowerShell): the user reviews and commits all changes themselves.
// Instead of a hard deny, history-changing git commands always require explicit approval -
// this keeps the rule enforceable even when auto-approve modes are on, while still allowing
// the rare case where the user asks Claude to commit/push (e.g. a demo PR).
import { readFileSync } from 'node:fs';

const input = JSON.parse(readFileSync(0, 'utf8'));
const command = input.tool_input?.command ?? '';

if (/\bgit\s+(commit|push|reset|rebase|merge|revert)\b/.test(command)) {
  console.log(JSON.stringify({
    hookSpecificOutput: {
      hookEventName: 'PreToolUse',
      permissionDecision: 'ask',
      permissionDecisionReason: 'This project\'s rule: the user commits. Approve only if you asked Claude to do this.'
    }
  }));
}
