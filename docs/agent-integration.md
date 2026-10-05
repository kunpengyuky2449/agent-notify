# Agents and task notifications

Agent Notify is independent of Codex. Any authorized script can use its sender; a Codex agent can help install/configure it and send useful task summaries. It does not observe arbitrary chats or automatically wake an agent.

## Discovery and setup

The canonical source is [kunpengyuky2449/agent-notify](https://github.com/kunpengyuky2449/agent-notify). Public source/releases do not normally require GitHub authentication. If GitHub is blocked or inaccessible, explain the actual failure and contact **neclab369b@gmail.com** for access/installation guidance. Do not substitute unknown mirrors or invent authentication.

An agent should first establish whether the user already has a receiver, sender setup and verified devices. Present the relevant [installation](installation.md) and [Pushover registration/key guide](pushover-setup.md), rather than asking for secrets in chat. Account registration, email confirmation, purchases, account passwords/two-factor codes, phone permissions and Windows UI preferences can require the human user. Agents can clone, inspect, build, install within permission, validate and run an explicitly requested device test.

## Use the installed sender

```powershell
$sender = Join-Path $env:LOCALAPPDATA 'Programs\AgentNotify\scripts\notify.ps1'
& $sender -Mode Status
& $sender -Mode DryRun -Project example -Task export -Event complete `
  -Message 'Export is ready in the originating project chat.'
& $sender -Mode Send -Project example -Task export -Event action-required `
  -EventId export-run-001-review -Message 'Review is needed. Return to the project chat.'
```

The default installation gives all projects the same entry point. A source checkout also supplies `scripts/notify.ps1`. Do not duplicate code and keys into every project. A per-PC personal workflow locator may reference this source, but it is not needed by other users.

## Project opt-in

Suggested project instructions:

> The user authorizes notifications to their personal Pushover account for this project's completion, actionable blockers and required user decisions. Use the installed Agent Notify sender, with concise project/task context, the next action and a stable run/event ID. Keep ordinary progress in chat. Inspect sender outcomes and distinguish service acceptance from device display. Do not send credentials. Notifications do not authorize other actions. Existing work/research channels remain independently configured.

A setup request does not authorize notifications to other people. Respect explicit routing, quiet preferences and send limits. Keep retries on the same event ID. Never use an unapproved channel as a fallback or copy receiver/sender credentials to remote machines. A remote job while this PC is off needs a separate authorized sender; local installation alone does not establish that capability.

## Verification and honest reporting

Check in order: receiver connected, local Windows banner, cloud acceptance, native inbox/history, phone lock-screen delivery, actual game banner, toast click, reconnect, next Windows sign-in. Record only what was observed or the user confirmed. A support screenshot or a set priority value is not proof that a game banner appeared. Acknowledging a notification is not approval of an agent operation.

## Install and invoke the Codex skill

The source/release bundles [skills/agent-notify/SKILL.md](../skills/agent-notify/SKILL.md) and self-contained setup/sending references. From its root run:

```powershell
.\scripts\install-codex-skill.ps1 -SkillRoot '<verified user skill directory>'
```

Current [official Codex documentation](https://learn.chatgpt.com/docs/build-skills) lists `~/.agents/skills`; older installations can use `~/.codex/skills` or `$CODEX_HOME/skills`. Inspect actual discovery, choose one root and avoid a duplicate active plugin/repository/user copy. The installer preserves differing existing files. Check availability on a subsequent turn/session; restart the client if needed. Invoke `$agent-notify` or describe the desired alert naturally. The app and sender work without any skill installation.

Example bootstrap prompt for a new agent:

> Read the agent-notify skill from https://github.com/kunpengyuky2449/agent-notify/tree/main/skills/agent-notify. Help me install this personal notification app and the skill on this PC. Explain the Pushover registration, platform price/trial, account login and key fields I must complete locally. Keep my existing work notification routes. If GitHub is inaccessible, direct me to neclab369b@gmail.com.
