using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AgentNotify {
    public static class Program {
        [STAThread] public static int Main(string[] arguments) {
            System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;
            if (arguments.Length > 0 && arguments[0] == "--self-test") return SelfTests.Run(arguments[1]);
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            if (arguments.Length > 0 && arguments[0] == "--render-preview") { SelfTests.Render(arguments[1]); return 0; }
            try {
                string executable = Application.ExecutablePath;
                if (arguments.Contains("--register-only")) { WindowsIntegration.Register(executable); return 0; }
                if (arguments.Length > 0 && arguments[0] == "--toast-test") {
                    WindowsIntegration.Register(executable);
                    var testMessage = new ReceivedMessage { Id = DateTime.UtcNow.Ticks, Title = "Agent Notify 本地测试 / Local test",
                        Body = "这是原生 Windows 通知，无需打开浏览器。\nThis is a native Windows notification; no browser is needed." };
                    WindowsIntegration.Toast(testMessage);
                    WindowsIntegration.TestPhase = "query own notification history";
                    bool inHistory = false;
                    for (int attempt = 0; attempt < 10 && !inHistory; attempt++) {
                        Thread.Sleep(250);
                        inHistory = Windows.UI.Notifications.ToastNotificationManager.History.GetHistory(WindowsIntegration.AppId)
                            .Any(notification => notification.Tag == WindowsIntegration.ToastTag(testMessage.Id));
                    }
                    WindowsIntegration.TestPhase = "read established notification setting";
                    var setting = Windows.UI.Notifications.ToastNotificationManager.CreateToastNotifier(WindowsIntegration.AppId).Setting;
                    File.WriteAllText(arguments[1], "Native toast API accepted. Matching notification in Windows history: " + inHistory + ". Notification setting: " + setting + ". User-visible popup remains unverified.");
                    return inHistory ? 0 : 2;
                }
                bool created;
                using (var mutex = new Mutex(true, "Local\\PersonalAgentNotify-" + WindowsIdentity.GetCurrent().User.Value, out created)) {
                    if (!created) { WindowsIntegration.OpenExistingInbox(); return 0; }
                    try {
                        WindowsIntegration.Register(executable);
                        var store = new LocalStore(StoreDirectory());
                        using (var api = new ClientApi()) using (var context = new TrayContext(store, api)) {
                            if (arguments.Contains("--open-inbox")) context.ShowInbox();
                            Application.Run(context);
                        }
                    } finally { mutex.ReleaseMutex(); }
                }
                return 0;
            } catch (Exception error) {
                if (arguments.Length > 1 && arguments[0] == "--toast-test") {
                    File.WriteAllText(arguments[1], "FAIL: native toast smoke test at " + WindowsIntegration.TestPhase + ": " + error.GetType().Name + " (0x" + error.HResult.ToString("X8") + ").");
                    return 1;
                }
                MessageBox.Show("Agent Notify could not start. Check the local installation and Windows notification settings. No account secrets were printed.", "Agent Notify", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
        }
        public static string StoreDirectory() {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string current = Path.Combine(home, "AgentNotify", "receiver");
            string legacy = Path.Combine(home, "AgentToolkit", "agent-notify");
            return !File.Exists(Path.Combine(current, "session.json")) && File.Exists(Path.Combine(legacy, "session.json")) ? legacy : current;
        }
    }
    public sealed class LoginForm : Form {
        private readonly TextBox email = new TextBox();
        private readonly TextBox password = new TextBox { UseSystemPasswordChar = true };
        private readonly TextBox twoFactor = new TextBox();
        private readonly TextBox name = new TextBox();
        private readonly Label status = new Label { AutoSize = false, Height = 52, ForeColor = Color.DarkRed };
        private readonly Button login = new Button { Text = "Sign in / 登录", Width = 150 };
        private readonly CheckBox startup = new CheckBox { Text = "Start at Windows sign-in / 登录 Windows 后启动", Checked = true, AutoSize = true };
        private readonly ClientApi api;
        private readonly LocalStore store;
        public Session Result;
        public LoginForm(ClientApi api, LocalStore store, string previewDeviceName = null) {
            this.api = api; this.store = store;
            Text = "Agent Notify — First setup / 首次设置"; Font = new Font("Segoe UI", 10);
            AutoScaleMode = AutoScaleMode.Dpi; ClientSize = new Size(570, 470); FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false; StartPosition = FormStartPosition.CenterScreen;
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20), ColumnCount = 1, RowCount = 13 };
            layout.Controls.Add(new Label { Text = "Unofficial Pushover Open Client; not released or supported by Pushover.\n使用现有 Desktop 授权，手机和发送脚本无需修改。", AutoSize = true, MaximumSize = new Size(515, 0) });
            AddField(layout, "Personal Pushover email / 个人账户邮箱", email);
            AddField(layout, "Account password / 账户密码（不保存）", password);
            AddField(layout, "Two-factor code, if enabled / 两步验证码（如已启用）", twoFactor);
            AddField(layout, "New receiver device name / 新接收设备名称", name);
            string machine = Regex.Replace(Environment.MachineName.ToLowerInvariant(), "[^a-z0-9_-]", "");
            name.Text = previewDeviceName ?? ("agent-" + machine.Substring(0, Math.Min(machine.Length, 12)) + "-" + Guid.NewGuid().ToString("N").Substring(0, 4));
            layout.Controls.Add(startup); layout.Controls.Add(status); layout.Controls.Add(login);
            Controls.Add(layout); AcceptButton = login; login.Click += async delegate { await SignIn(); };
            FormClosing += delegate(object sender, FormClosingEventArgs args) { if (!login.Enabled) args.Cancel = true; };
        }
        private static void AddField(TableLayoutPanel layout, string label, TextBox field) {
            layout.Controls.Add(new Label { Text = label, AutoSize = true }); field.Dock = DockStyle.Top; layout.Controls.Add(field);
        }
        private async Task SignIn() {
            if (String.IsNullOrWhiteSpace(email.Text) || password.Text.Length == 0 || !Regex.IsMatch(name.Text, "^[A-Za-z0-9_-]{1,25}$")) {
                status.Text = "Fill email/password. Device name: 1–25 letters, digits, _ or -."; return;
            }
            login.Enabled = false; status.Text = "Signing in and registering this Windows receiver…";
            try {
                Result = await api.LoginAndRegister(email.Text, password.Text, twoFactor.Text, name.Text, CancellationToken.None);
                store.SaveSession(Result);
                WindowsIntegration.SetStartup(startup.Checked, Application.ExecutablePath);
                password.Clear(); twoFactor.Clear(); DialogResult = DialogResult.OK; login.Enabled = true; Close();
            } catch (ClientFailure error) {
                status.Text = error.Message;
                if (error.NeedsTwoFactor) twoFactor.Focus();
                else { password.Clear(); twoFactor.Clear(); }
            } catch { password.Clear(); twoFactor.Clear(); status.Text = "Local configuration could not be saved. Check directory access before retrying."; }
            finally { login.Enabled = true; }
        }
    }
    public sealed class InboxForm : Form {
        private readonly ListView list = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, MultiSelect = false };
        private readonly TextBox body = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical };
        private readonly Button link = new Button { Text = "Open message link / 打开消息链接", Dock = DockStyle.Bottom, Height = 34, Enabled = false };
        public Action OpenRequested;
        public bool AllowClose;
        public InboxForm() {
            Text = "Agent Notify — Inbox / 收件箱"; Font = new Font("Segoe UI", 10); AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(760, 520); StartPosition = FormStartPosition.CenterScreen;
            list.Columns.Add("Received / 接收", 150); list.Columns.Add("Title / 标题", 550);
            var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 260 };
            split.Panel1.Controls.Add(list); split.Panel2.Controls.Add(body); split.Panel2.Controls.Add(link); Controls.Add(split);
            list.SelectedIndexChanged += delegate {
                var message = list.SelectedItems.Count == 0 ? null : list.SelectedItems[0].Tag as ReceivedMessage;
                body.Text = message == null ? "" : message.Title + Environment.NewLine + Environment.NewLine + message.Body;
                Uri uri; link.Enabled = message != null && Uri.TryCreate(message.Url, UriKind.Absolute, out uri)
                    && (uri.Scheme == "http" || uri.Scheme == "https") && String.IsNullOrEmpty(uri.UserInfo);
                link.Tag = link.Enabled ? message.Url : null;
            };
            link.Click += delegate {
                if (link.Tag != null) try { Process.Start(new ProcessStartInfo((string)link.Tag) { UseShellExecute = true }); } catch { MessageBox.Show("Link could not be opened.", "Agent Notify"); }
            };
            FormClosing += delegate(object sender, FormClosingEventArgs args) { if (!AllowClose) { args.Cancel = true; Hide(); } };
            var unusedHandle = Handle;
        }
        protected override void WndProc(ref Message message) {
            if ((uint)message.Msg == WindowsIntegration.OpenMessage && OpenRequested != null) OpenRequested();
            base.WndProc(ref message);
        }
        public void RefreshMessages(List<ReceivedMessage> messages) {
            long selected = list.SelectedItems.Count == 0 ? 0 : ((ReceivedMessage)list.SelectedItems[0].Tag).Id;
            list.BeginUpdate(); list.Items.Clear();
            foreach (var message in messages) {
                string time;
                try { time = DateTimeOffset.FromUnixTimeSeconds(message.Date).LocalDateTime.ToString("MM-dd HH:mm:ss"); } catch { time = ""; }
                var item = new ListViewItem(new[] { time, message.Title }); item.Tag = message; list.Items.Add(item);
            }
            list.EndUpdate();
            if (list.Items.Count > 0) {
                var chosen = list.Items.Cast<ListViewItem>().FirstOrDefault(item => ((ReceivedMessage)item.Tag).Id == selected) ?? list.Items[0];
                chosen.Selected = true;
            }
        }
    }
    public sealed class TrayContext : ApplicationContext {
        private readonly LocalStore store;
        private readonly ClientApi api;
        private readonly InboxForm inbox = new InboxForm();
        private readonly NotifyIcon tray;
        private readonly ToolStripMenuItem status = new ToolStripMenuItem("Not connected / 未连接") { Enabled = false };
        private readonly ToolStripMenuItem startup = new ToolStripMenuItem("Start at Windows sign-in / 登录后启动") { CheckOnClick = true };
        private readonly Dictionary<string, Form> urgent = new Dictionary<string, Form>();
        private readonly System.Windows.Forms.Timer startTimer = new System.Windows.Forms.Timer { Interval = 250 };
        private CancellationTokenSource cancellation;
        private Task receiverTask;
        private Session session;
        private bool stopping;
        private bool restarting;
        public TrayContext(LocalStore store, ClientApi api) {
            this.store = store; this.api = api; inbox.OpenRequested = ShowInbox;
            var menu = new ContextMenuStrip(); menu.Items.Add(status);
            menu.Items.Add("Open inbox / 打开收件箱", null, delegate { ShowInbox(); });
            menu.Items.Add("Windows notification settings / Windows 通知设置", null, delegate {
                try { Process.Start(new ProcessStartInfo("ms-settings:notifications") { UseShellExecute = true }); }
                catch { SetStatus("Open Windows Settings > System > Notifications."); }
            });
            menu.Items.Add("Local notification test / 本地通知测试", null, delegate { try { WindowsIntegration.Toast(new ReceivedMessage { Id = DateTime.UtcNow.Ticks,
                Title = "Agent Notify 原生测试 / Native test", Body = "浏览器关闭也能显示。\nThis notification does not depend on a browser." }); } catch { SetStatus("Windows notification test failed. Check settings."); } });
            menu.Items.Add("Reconnect / 重新连接", null, async delegate { await Restart(false); });
            menu.Items.Add("Sign in as a new device / 重新登录新设备", null, async delegate { await Restart(true); });
            startup.Checked = WindowsIntegration.StartsAtLogin();
            startup.CheckedChanged += delegate { try { WindowsIntegration.SetStartup(startup.Checked, Application.ExecutablePath); } catch { SetStatus("Startup setting could not be saved."); } };
            menu.Items.Add(startup);
            menu.Items.Add("About / 关于", null, delegate { MessageBox.Show("Agent Notify 0.1\nUnofficial Pushover Open Client; not released or supported by Pushover.\nUses your Desktop license. Message receipt is not approval of any task.\n退出程序后停止电脑接收；Android 手机独立接收。", "Agent Notify"); });
            menu.Items.Add("Exit / 退出", null, async delegate { await StopAndExit(); });
            tray = new NotifyIcon { Icon = SystemIcons.Information, Text = "Agent Notify", ContextMenuStrip = menu, Visible = true };
            tray.DoubleClick += delegate { ShowInbox(); };
            startTimer.Tick += async delegate { startTimer.Stop(); await Restart(false); };
            startTimer.Start();
        }
        public void ShowInbox() { inbox.RefreshMessages(store.Snapshot()); inbox.Show(); inbox.WindowState = FormWindowState.Normal; inbox.Activate(); }
        private void SetStatus(string text) {
            if (stopping || inbox.IsDisposed) return;
            if (inbox.InvokeRequired) { inbox.BeginInvoke((Action)(() => SetStatus(text))); return; }
            status.Text = text; tray.Text = text.Length > 60 ? "Agent Notify — check tray status" : "Agent Notify: " + text.Substring(0, Math.Min(47, text.Length));
            if (inbox.Visible) inbox.RefreshMessages(store.Snapshot());
        }
        private void Alert(ReceivedMessage message) {
            if (stopping) throw new OperationCanceledException();
            if (inbox.InvokeRequired) { inbox.Invoke((Action)(() => Alert(message))); return; }
            if (message.Priority >= 2 && !message.Acked) { ShowUrgent(message); return; }
            WindowsIntegration.Toast(message);
            if (inbox.Visible) inbox.RefreshMessages(store.Snapshot());
        }
        private void ShowUrgent(ReceivedMessage message) {
            if (String.IsNullOrWhiteSpace(message.Receipt)) throw new ClientFailure("An emergency message lacks a receipt; sync stopped without deletion.", true);
            if (urgent.ContainsKey(message.Receipt)) { urgent[message.Receipt].Show(); return; }
            var form = new Form { Text = "Agent Notify — Urgent / 紧急通知", Font = new Font("Segoe UI", 10), ClientSize = new Size(580, 320), TopMost = true, StartPosition = FormStartPosition.CenterScreen };
            form.Controls.Add(new TextBox { Text = message.Title + "\r\n\r\n" + message.Body, Multiline = true, ReadOnly = true, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Vertical });
            var button = new Button { Text = "Acknowledge notification / 确认已看到通知", Dock = DockStyle.Bottom, Height = 42 }; form.Controls.Add(button); button.BringToFront();
            button.Click += async delegate {
                button.Enabled = false;
                try { await api.Acknowledge(session, message.Receipt, CancellationToken.None); store.MarkAcknowledged(message.Receipt); form.Close(); }
                catch { button.Enabled = true; MessageBox.Show("Acknowledgement failed; no task approval was recorded.", "Agent Notify"); }
            };
            form.FormClosed += delegate { urgent.Remove(message.Receipt); };
            urgent[message.Receipt] = form; form.Show();
        }
        private async Task Restart(bool newLogin) {
            if (stopping || restarting) return;
            restarting = true;
            try {
                if (cancellation != null) { cancellation.Cancel(); if (receiverTask != null) await receiverTask; cancellation.Dispose(); cancellation = null; }
                if (newLogin) foreach (var form in urgent.Values.ToArray()) form.Close();
                session = newLogin ? null : store.ReadSession();
                if (session == null) {
                    using (var login = new LoginForm(api, store)) {
                        if (login.ShowDialog() != DialogResult.OK) { SetStatus("Sign-in required / 需要登录"); return; }
                        session = login.Result; startup.Checked = WindowsIntegration.StartsAtLogin();
                    }
                }
                if (stopping) return;
                cancellation = new CancellationTokenSource();
                var receiver = new Receiver(api, store, session, Alert, SetStatus);
                receiverTask = Task.Run(() => receiver.Run(cancellation.Token));
            } catch { SetStatus("Local session or inbox could not be opened. Sign in again or inspect local state."); }
            finally { restarting = false; }
        }
        private async Task StopAndExit() {
            stopping = true; startTimer.Stop();
            if (cancellation != null) { cancellation.Cancel(); if (receiverTask != null) await receiverTask; }
            tray.Visible = false;
            foreach (var form in urgent.Values.ToArray()) form.Close();
            inbox.AllowClose = true; inbox.Close(); ExitThread();
        }
        protected override void Dispose(bool disposing) {
            if (disposing) { stopping = true; startTimer.Dispose(); tray.Dispose(); inbox.Dispose(); if (cancellation != null) { cancellation.Cancel(); cancellation.Dispose(); } }
            base.Dispose(disposing);
        }
    }
}
