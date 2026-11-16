# CLAUDE.md

Project instructions for Claude Code. The tool-agnostic guide for all AI agents is `agents/AGENTS.md`. It is imported below so it is in context in every session:

@agents/AGENTS.md

## Claude Code specifics

- The working agreements above apply. In particular, git is read-only: inspect freely, but never commit or run anything that changes files, the index, refs or config.
- Use a subagent for broad codebase or file-system searches, so the main context stays small.
- When BeamNG.drive runs with `-enablemcp` and the project's `beamng` MCP server is available, verify levels yourself with its tools (see `agents/verification.md`).
- When you execute a plan step, finish with a summary, verification evidence, follow-ups and a suggested commit message.
