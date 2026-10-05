# Agent Notify

[English](README.md) · [安装、升级、卸载](docs/installation.md) · [Pushover 注册与密钥](docs/pushover-setup.md) · [Codex 接入](docs/agent-integration.md)

一个轻量 Windows 托盘通知应用：agent 的任务完成、遇到需要你处理的阻碍、等待你决定时，通过 Pushover 同时提醒 Windows 和手机。Windows 使用原生横幅和通知中心，浏览器可以完全关闭。

这是 **Pushover 的非官方 Open Client**，不由 Pushover 发布或支持。软件按 [MIT 许可证](LICENSE)开源，是一个通过 AI 辅助编程开发、持续完善的个人项目。每位使用者需要自己的 Pushover 账户；仓库不提供公用账户或后台转发服务器。

## 第一次使用

1. [注册 Pushover](https://pushover.net/signup)。要手机推送，安装官方 [Android](https://pushover.net/clients/android) 或 [iOS](https://pushover.net/clients/ios) 应用，在同一个账户下登记手机。
2. 从 [Releases](https://github.com/kunpengyuky2449/agent-notify/releases) 下载 Windows ZIP，解压后双击 `Install.cmd`。在普通 Windows 用户下安装，无需管理员。也可以按[指南](docs/installation.md)从源码构建。
3. 从开始菜单打开 **Agent Notify**，在本地窗口输入账户邮箱、密码及验证码。这里不用 User Key/API Token。选一个唯一设备名，选择是否登录 Windows 后启动。
4. Windows“系统 → 通知”中开启 Agent Notify 的横幅、通知中心和声音。游戏中也需要提醒时，在 **Set priority notifications → Add apps** 中加入它，并在实际游戏里测试。
5. 如果还要让 agent/脚本发送消息，按[密钥教程](docs/pushover-setup.md)取得 User Key 和自己的应用 API Token，运行 `scripts/setup-pushover.cmd`。凭据只在本地填写。

关闭收件箱后程序仍在托盘接收；右键菜单 **Exit** 才会停止电脑接收。手机端独立工作。通知要求你回到原任务处理，点击或确认通知不等于批准 agent 执行操作。

## 价格和平台

Agent Notify 本身免费。Pushover 个人使用目前每个接收平台一次性 **US$4.99**，试用 **30 天**；Android + Desktop 合计 **US$9.98**，税费另计。这个非官方接收器使用已有的 Desktop 授权，不另加一份接收器费用。以 [Pushover 官方价格](https://pushover.net/pricing)为准；核对日期为 2026-10-05。

目标环境是 Windows x64、.NET Framework 4.8 和安装所需的 Windows PowerShell 5.1。已在原始 Windows 11 电脑上验证桌面/游戏横幅、声音和 Android 收到消息；Windows 10、其他电脑和其他游戏尚需各自验证。运行不需要 Codex、Python、Node.js 或常驻浏览器。

## 给 agent 和脚本使用

发送工具是 `scripts/notify.ps1`，事件类型为 `complete`、`action-required`、`failed`、`test`。每条具体事件使用稳定的 EventId，重试时保留相同 ID。默认一小时最多十次尝试；重复或结果不确定的事件会抑制重发，不会自动排队。

```powershell
& .\scripts\notify.ps1 -Mode Send -Project demo -Task analysis `
  -Event action-required -EventId demo-run-001-review `
  -Message '结果已准备好。请回到原项目聊天确认下一步。'
```

`queued` 表示 Pushover 接受请求，实际横幅和锁屏手机显示要通过实测确认。新 agent 的接入说明见 [Codex 指南](docs/agent-integration.md)。

## 数据与已知限制

账户密码不保存；接收会话和发送密钥用当前 Windows 用户的 DPAPI 加密，不能复制到另一台电脑后直接使用。收件箱正文保存在私有目录内，正文不加密。安装、卸载和源码同步都不应携带这些用户数据。

系统“请勿打扰”、游戏/全屏自动规则、独占全屏显示模式都可能挡住横幅。未确认的紧急通知会恢复提示，普通启动积压消息不会逐条弹窗。电脑离线、休眠或退出程序时不能立即提醒。初版只处理文本，不支持自定义声音、附件、HTML 渲染和手机端到端加密消息。

遇到问题先看[故障排查](docs/troubleshooting.md)。功能建议和复现问题可发到 [GitHub Issues](https://github.com/kunpengyuky2449/agent-notify/issues)。无法访问 GitHub 或需要安装指引时联系 **neclab369b@gmail.com**；请勿发送密钥、密码、会话文件或私人消息正文。
