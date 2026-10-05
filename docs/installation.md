# Install, update, move PCs and remove

[中文概览](../README.zh-CN.md) · [Account setup](pushover-setup.md) · [Troubleshooting](troubleshooting.md)

## Install a release

1. Download `AgentNotify-<version>-windows-x64.zip` and `SHA256SUMS.txt` from [Releases](https://github.com/kunpengyuky2449/agent-notify/releases).
2. Optionally compare the ZIP's `Get-FileHash -Algorithm SHA256` result against the published checksum. It checks file integrity; an unsigned release is not authenticated by a checksum alone.
3. Extract the ZIP. Run `Install.cmd` as your normal Windows user. The package is unsigned; Windows may show a downloaded-file/unknown-publisher prompt. Inspect the source and release before deciding whether to run it. Do not disable antivirus or change a machine-wide execution policy.
4. Open Agent Notify from Start; log into Pushover locally and choose startup behavior. The installer itself does not enter keys/passwords or enable sign-in startup.

Installed program and sender tools: `%LOCALAPPDATA%\Programs\AgentNotify`. The Start Menu shortcut is **Agent Notify**, and the native notification identity is `Personal.AgentNotify`. Windows can take time to index a new shortcut. If Start search is delayed, run the installed `AgentNotify.exe` directly or open `shell:programs` to find its shortcut. Pinning it to Start's home area is a separate user action.

## Build from source

Git is required to clone/update the source; it is not required to use a release.

```powershell
git clone https://github.com/kunpengyuky2449/agent-notify.git
cd agent-notify
.\scripts\test.ps1
.\install.ps1 -Launch
```

If Windows blocks running the reviewed script, use a process-scoped command:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\install.ps1 -Launch
```

The build uses the locally installed .NET Framework 4.8 compiler and Windows WinRT metadata. It does not download an SDK or NuGet dependencies. Missing compiler/metadata is reported explicitly; use a published release on a supported normal Windows installation or install the required Windows/.NET components intentionally.

## Upgrade

1. Obtain and extract the new release into a different folder. For source users, run `git pull --ff-only`, then `scripts/test.ps1` to rebuild.
2. Exit the running app from its tray menu. Review the installed version and its hash:

   ```powershell
   Get-FileHash "$env:LOCALAPPDATA\Programs\AgentNotify\AgentNotify.exe" -Algorithm SHA256
   ```

3. From the **new package/source**, run `install.ps1 -ExpectedInstalledSha256 <the-reviewed-installed-hash> -Launch`.

A differing existing executable is preserved unless its reviewed hash is supplied. The previous executable is kept alongside the installation. User data lives outside the program directory and is preserved. The installer also preserves modified managed support files rather than silently overwriting them. Do not run the installer from the installed directory itself.

## Use another PC

Install independently under your normal Windows account. Log into Pushover to register a **new receiver device and session**. If you also send from that PC, configure its sender keys locally. Multiple desktop devices are covered by the same Desktop platform license according to [Pushover's pricing](https://pushover.net/pricing).

Do not copy encrypted session/key files, sender ledgers or active receiver sessions between PCs. The inbox is not a synchronization service. Device delivery is independent; registering a PC does not make the other PC's inbox or replies travel with it. Check startup, Windows priority permission and real display modes on each PC.

## Remove

Exit the tray app, then run `Uninstall.cmd` from the release/source or installed folder. Removal verifies its manifest/hashes, removes only its owned shortcut/startup/protocol identity and known program files, and preserves unexpected/modified files. Receiver inbox, encrypted session and sender configuration are **kept**. Unused backup executables can be reviewed and deleted separately.

If you no longer use the device, remove it separately from your Pushover account. If you also want local data erased, review the storage locations in [architecture](architecture.md) and remove only the intended app directory. This installer never deletes your account or other devices.

## 安装要点

普通用户安装，无需管理员。源码用户先测试构建，发布包用户解压后双击安装。每台电脑重新本地登录/配置，不复制 DPAPI 会话或密钥。游戏横幅需要系统允许；卸载保留个人数据。无法访问 GitHub、下载失败或安装步骤不清楚时联系 **neclab369b@gmail.com**，不发送密码或密钥。
