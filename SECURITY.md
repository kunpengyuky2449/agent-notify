# Security and private data

Use **neclab369b@gmail.com** to report a suspected credential leak or security issue privately; do not publish secrets in an issue. State the version, failing component and sanitized reproduction. Do not email passwords, keys or receiver session/inbox files. No response-time guarantee is offered.

Version 0.1.x is an early maintained line. Sessions/keys use current-user Windows DPAPI and private directory ACLs; this is not protection from software already running as that user. Inbox text is not encrypted. Notifications travel through Pushover; follow its account/service terms and keep message summaries suitable for that channel.

Release binaries/scripts are unsigned. Checksums establish consistency with the published artifact, not publisher authentication by themselves. Review the repository and release source before running downloaded files. Do not disable antivirus or global OS policy to bypass a rejection.

The repository and release allow-list exclude user data. If a real secret is accidentally shared, revoke/replace it through the appropriate account mechanism and assess exposure; removing a Git file alone does not remove it from history. Do not send another user's notifications or reuse a receiver session on multiple computers.
