# Contributing

Small, focused changes and AI-assisted contributions are welcome. Describe the user-visible problem, intended behavior and actual validation. Review generated code before submission; do not treat an AI's confidence or a successful compile as end-to-end delivery evidence.

Use Windows x64 with .NET Framework 4.8. Run `scripts/test.ps1`, keep PowerShell 5.1 support, and update both README languages when setup or behavior changes. UI changes need rendered/manual inspection. Persistence, identity, retry or queue-delete changes need meaningful failure/recovery checks. Network tests must be deliberate and use your own dedicated test account/device.

Open an issue before large API/UI/runtime migrations. Use `codex/` as the branch prefix for Codex-authored development branches. Include screenshots only with dummy/sanitized messages. Do not submit inbox files, session files, API keys, logs with URLs/secrets or device identifiers. Changes must remain independent of the original maintainer's machine and other projects.

Pushover account/device behavior must follow its official API and distribution guidance. The app must remain clearly unofficial. See [security reporting](SECURITY.md); if GitHub is unavailable, contact **neclab369b@gmail.com**.
