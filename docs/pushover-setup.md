# Pushover account and keys / 账户与密钥

Pushover is a separate notification service that delivers messages to registered devices. Agent Notify supplies a native Windows receiver and an optional local sender. Each user supplies their own account and license. Software sharing does not share the maintainer's account or API Token.

## Official pages

| Purpose | Official page |
| --- | --- |
| Service introduction / 简介 | [pushover.net](https://pushover.net/) |
| Register / 注册 | [Create an account](https://pushover.net/signup) |
| Login and User Key / 登录与 User Key | [Account dashboard](https://pushover.net/) |
| Create sender application / 创建发送应用 | [Register an application](https://pushover.net/apps/build) |
| Manage sender applications / 管理应用 | [Your applications](https://pushover.net/apps) |
| Price and trial / 价格与试用 | [Pricing](https://pushover.net/pricing) |
| Phone clients / 手机客户端 | [Android](https://pushover.net/clients/android), [iOS](https://pushover.net/clients/ios) |
| Desktop license / Desktop 授权 | [Desktop](https://pushover.net/clients/desktop) |
| API reference / API 文档 | [Message API](https://pushover.net/api), [Open Client API](https://pushover.net/api/client) |

The app uses the Desktop license/trial. Individual receiving platforms are charged separately; check the linked pricing page for the current amount before paying. Purchases and subscription decisions are performed by the user.

## Register and receive / 注册并接收

1. Open the signup page and create an account using your own email/password. Complete the site's email verification if requested.
2. Install the official phone app and sign into the same account. Permit notifications and choose a phone device name.
3. Install Agent Notify and log in locally. Its login registers a new Desktop Open Client device. Use a unique device name on every PC; keep each receiver session on that PC.
4. For Windows reception, the account email/password and optional two-factor code are needed once. The password is discarded; the receiving session is stored encrypted. A sending User Key/API Token pair cannot replace this receiver login.

原生 Windows 接收器不要求网页客户端常驻。如果仍同时打开网页推送，可能收到两份电脑通知；原生接收验证完成后可以关闭网页客户端。试用/购买和设备管理仍在 Pushover 官方账户中完成。

## Find the User Key / 找到 User Key

Sign into [pushover.net](https://pushover.net/) with your personal account. The dashboard shows **Your User Key**. Copy your personal key into the local setup prompt when asked. A delivery-group key is a different identifier; this application's setup uses your own personal User Key.

User Key identifies the receiving account. It is not your password and is not the application's API Token. Do not paste it into GitHub, agent chats or shared screenshots.

## Create the API Token / 取得 API Token

1. While signed in, open [Register an application](https://pushover.net/apps/build).
2. Give it a recognizable name, for example **Personal Agent Notifications**. This identifies your sender application; it is different from registering a receiving device.
3. Fill any required form fields and accept the site's applicable terms. A description can explain your personal notification use. A homepage URL is optional; leave it blank for a personal application, or use this repository URL if useful. Do not place credentials in either field.
4. Submit the form. On the application's detail page, copy **API Token/Key** into the local sender setup prompt. You can return through [Your applications](https://pushover.net/apps).

“User Key” 是你的接收账户标识，“API Token/Key” 是你创建的发送应用凭据。别人使用这个开源 app 时应创建自己的发送应用，不使用维护者的密钥。

## Configure the sender

Double-click `scripts/setup-pushover.cmd` in the installed folder or source/release directory. It asks for the User Key and API Token with hidden input, validates them and sends **one explicit device test**. Leave the console open to read the result. Its execution-policy change applies only to this process, not the machine.

Equivalent commands:

```powershell
.\scripts\notify.ps1 -Mode Configure
.\scripts\notify.ps1 -Mode Validate
.\scripts\notify.ps1 -Mode Send -Project personal -Task setup -Event test `
  -EventId setup-first-test -Message 'Please check the phone and Windows notification.'
```

Configure preserves an existing configuration instead of overwriting it. Each PC/Windows identity needs its own local setup; Windows DPAPI credentials cannot be transferred by copying files. Confirm the phone with its screen locked and Windows with another app focused. Then test the actual game/display mode if you rely on those banners.

If registration times out, inspect your account's device/application lists before trying to create another item. If a send outcome is uncertain, check devices before choosing a new EventId. For account, payment or official-client problems use [Pushover support](https://support.pushover.net/); for this app or GitHub access contact **neclab369b@gmail.com**.
