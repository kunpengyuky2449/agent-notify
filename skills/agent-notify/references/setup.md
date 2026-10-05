# Setup knowledge and user guidance

Canonical source: https://github.com/kunpengyuky2449/agent-notify
Downloads: https://github.com/kunpengyuky2449/agent-notify/releases
English/Chinese README and guides are in that repository. If access fails, give the failure and contact **neclab369b@gmail.com**; a public clone/release ordinarily needs no GitHub login. Do not assume every network failure is a missing credential. Do not replace the source with an unknown mirror.

## Prepare the app

Use a fresh directory selected for this PC:

```powershell
git clone https://github.com/kunpengyuky2449/agent-notify.git
Set-Location agent-notify
git status --short
.\scripts\test.ps1
.\install.ps1
```

For an existing checkout, inspect remote, branch and local changes before updating. Use `git pull --ff-only` when the user requested an update and the tree is clean; preserve local modifications/divergent branches for reconciliation. Do not reset another person's changes. For non-developers, prefer the extracted release ZIP and `Install.cmd` instead of a compiler workflow.

Normal per-user install: `%LOCALAPPDATA%\Programs\AgentNotify`, Start → Agent Notify. Windows x64/.NET Framework 4.8 and Windows PowerShell 5.1 required. No Python, Node.js or Codex is needed by the app. Windows 11 is exercised; another physical PC and Windows 10 need their own tests. Read repository `docs/installation.md` for guarded upgrade/removal. Do not stop a working receiver merely to install a skill.

## Explain human steps

Use this sequence only for missing stages. In Chinese, an appropriate introduction is: “Agent Notify 把同一条个人 Pushover 消息送到手机和 Windows 原生通知。软件免费；Pushover 的平台授权单独付费。账户和密钥在你自己的界面输入，安装后各项目共用一个发送入口。”

| Human step | Link or screen | Agent can prepare |
| --- | --- | --- |
| Create/verify personal account | https://pushover.net/signup | Explain the service and registration fields |
| Install official phone client; allow notifications | https://pushover.net/clients/android or https://pushover.net/clients/ios | Link the correct platform; ask OS only if unknown |
| Activate receiving platform trial/license | https://pushover.net/pricing | Verify current price and explain separate Android/iOS/Desktop licensing before purchase |
| Sign in to Windows receiver | Start → Agent Notify; account email/password, optional 2FA, unique device name | Install/register the local launcher; do not type or obtain the user's secret |
| Obtain User Key | Sign into https://pushover.net/ ; account dashboard **Your User Key** | Explain that it identifies the receiving account |
| Obtain app API Token | https://pushover.net/apps/build ; create own app, then open its details **API Token/Key** | Explain name and optional Description/URL; optional fields may be left blank for personal use |
| Save sender configuration | App/source `scripts/setup-pushover.cmd`, run as normal configuring user | Launch/show instructions; masked key entry stays local |
| Permit desktop/game banners | Settings → System → Notifications → Agent Notify; enable banners, sound, Notification Center; add app under Set priority notifications | Explain that Notification Center “Top” priority is distinct from the Do Not Disturb allow-list |

Pricing checked 2026-10-05: individual US$4.99 one time per receiving platform, 30-day trial. Android + Desktop US$9.98 before tax. Open Client uses Desktop license. Recheck https://pushover.net/pricing and https://pushover.net/api/client before advising a purchase; do not automate payment. Multiple devices on the same platform use the same individual platform license. App API sending includes the service's free quota; consult current pricing for extra capacity.

Receiver account login and sender User Key/API Token are different. A receiver session does not configure sending, and sending keys do not sign in the receiver. On each new PC, enter credentials locally; do not copy another PC's DPAPI files. Fresh data: `%LOCALAPPDATA%\AgentNotify\receiver` and `...\sender`; the original prototype is also recognized at legacy AgentToolkit locations without copying secrets.

## Establish readiness

After human setup, check sender Status and, when authorized, Validate; check receiver Connected. Offer one deliberately requested test with a stable event ID. Verify phone, native inbox and actual desktop/game banner separately. Browser closure does not stop the native receiver; app exit, PC sleep/offline and Windows automatic game/full-screen Do Not Disturb rules can prevent immediate banners. High toast priority is not a guaranteed override. No need to repeat already user-confirmed tests without a relevant change.

Guide users through only the unverified step. Include local paths/commands suitable for their PC; do not assume the maintainer's drive or name. Support reports should omit passwords, keys, session files and private inbox text.

## Teach another Codex installation

Read https://learn.chatgpt.com/docs/build-skills and inspect the actual client roots. Current official user root is `~/.agents/skills`; some clients, including the original app setup, supply `~/.codex/skills` or `$CODEX_HOME/skills`. Choose one verified root; no provider cache or `.system` writes.

From source or release:

```powershell
.\scripts\install-codex-skill.ps1 -SkillRoot '<verified user skill directory>'
```

The installer refuses changed/existing same-name copies rather than overwriting them. The personal Agent Toolkit can instead manage a mirrored copy through its reviewed plan/receipt workflow; do not activate both copies. Verify discovery in the next Codex session/turn, refresh/restart only if needed. The user can invoke `$agent-notify` or ask naturally for task notifications. Other projects need no duplicated keys or source. For permanent use, record the user's routing/authorization in that project's instructions, using the repository's `docs/agent-integration.md` opt-in example.
