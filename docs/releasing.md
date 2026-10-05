# Release procedure

1. Update `app.json`, C# assembly/file version, CHANGELOG and user-facing docs. Review changes and confirm no private account, path, inbox, key or log is staged.
2. Run `scripts/test.ps1`; inspect relevant UI when changing it. A CI test is offline and must not use Pushover credentials. Perform live checks on a dedicated own-account device when needed, without publishing its state.
3. From a clean package workspace, run `scripts/package.ps1`. It uses an explicit source allow-list and never packages the app's LocalAppData directories. Inspect ZIP entry names and `SHA256SUMS.txt`.
4. Commit reviewed source and tag its version. Upload the ZIP/checksum to a GitHub Release for that exact commit. Artifacts are unsigned; do not claim Authenticode signing or bit-for-bit reproducible builds. A new .NET Framework compiler invocation can produce a different binary hash from the same sources.
5. Download/check the published ZIP and verify installation in an isolated directory without touching account/notification registration; validate a real per-user install separately. Record actual CI/release/device outcomes in project_handoff.

The Windows workflow builds, runs offline tests and produces an artifact on pushes/pull requests/manual dispatch. It does not register a Pushover device or send cloud messages. Public releases are maintainer actions, not automatic uploads from fork PRs. Release credentials are managed by GitHub/the maintainer and never placed in this repository.
