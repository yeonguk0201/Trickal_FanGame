#!/usr/bin/env node
// PreToolUse hook: refuses `git commit` while on main and `git push` that targets main.
// main only changes through pull requests (AGENTS.md "커밋과 PR").
const { execSync } = require('child_process');

const PROTECTED = 'main';

let raw = '';
process.stdin.setEncoding('utf8');
process.stdin.on('data', (chunk) => (raw += chunk));
process.stdin.on('end', () => {
  let command = '';
  try {
    command = String(JSON.parse(raw).tool_input?.command ?? '');
  } catch {
    return;
  }

  const reason = findViolation(command);
  if (!reason) return;
  process.stdout.write(
    JSON.stringify({
      hookSpecificOutput: {
        hookEventName: 'PreToolUse',
        permissionDecision: 'deny',
        permissionDecisionReason: reason,
      },
    }),
  );
});

function currentBranch() {
  try {
    return execSync('git rev-parse --abbrev-ref HEAD', {
      cwd: process.env.CLAUDE_PROJECT_DIR || process.cwd(),
      stdio: ['ignore', 'pipe', 'ignore'],
    })
      .toString()
      .trim();
  } catch {
    return '';
  }
}

function findViolation(command) {
  // Each git invocation in a compound command is checked on its own.
  for (const part of command.split(/&&|\|\||[;|\n]/)) {
    const match = part.match(/\bgit\b(?:\s+-[^\s]+(?:\s+[^\s-][^\s]*)?)*\s+(commit|push)\b(.*)/);
    if (!match) continue;
    const [, verb, rest] = match;

    if (verb === 'commit') {
      if (currentBranch() === PROTECTED)
        return `${PROTECTED} 브랜치에는 직접 커밋하지 않습니다. dev/<주제> 브랜치를 만들어 커밋하고 PR로 병합하세요.`;
      continue;
    }

    const args = rest.trim().split(/\s+/).filter((arg) => arg && !arg.startsWith('-'));
    const refspecs = args.slice(1); // args[0] is the remote
    const targetsProtected = refspecs.some((ref) => {
      const target = ref.replace(/^\+/, '').split(':').pop().replace(/^refs\/heads\//, '');
      return target === PROTECTED;
    });
    if (targetsProtected || (refspecs.length === 0 && currentBranch() === PROTECTED))
      return `${PROTECTED} 브랜치로는 직접 푸시하지 않습니다. dev/<주제> 브랜치를 푸시하고 PR로 병합하세요.`;
  }

  return null;
}
