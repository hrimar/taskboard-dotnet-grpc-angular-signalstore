// PreToolUse hook (Bash|PowerShell): the user reviews and commits all changes themselves.
// Instead of a hard deny, history-changing git commands always require explicit approval -
// this keeps the rule enforceable even when auto-approve modes are on, while still allowing
// the rare case where the user asks Claude to commit/push (e.g. a demo PR).
import { readFileSync } from 'node:fs';

const input = JSON.parse(readFileSync(0, 'utf8'));
const command = input.tool_input?.command ?? '';

// Global options may sit between `git` and the subcommand - `git -C "<repo path>" commit` is
// a common form here because the repo path contains spaces - so they are skipped explicitly.
const gitHistoryChange = /\bgit(\s+(-C|-c)\s+("[^"]*"|'[^']*'|\S+)|\s+--?[\w-]+(=\S+)?)*\s+(commit|push|reset|rebase|merge|revert)\b/;
const ghMerge = /\bgh\s+pr\s+merge\b/;

if (gitHistoryChange.test(command) || ghMerge.test(command)) {
  console.log(JSON.stringify({
    hookSpecificOutput: {
      hookEventName: 'PreToolUse',
      permissionDecision: 'ask',
      permissionDecisionReason: 'This project\'s rule: the user commits. Approve only if you asked Claude to do this.'
    }
  }));
}
