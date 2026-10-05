using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;

namespace AgentNotify {
    public static class SelfTests {
        private sealed class FakeApi : IClientApi {
            public List<ReceivedMessage> Messages = new List<ReceivedMessage>();
            public long Deleted;
            public int Deletes;
            public bool FailDelete;
            public Task<List<ReceivedMessage>> Download(Session session, CancellationToken token) {
                return Task.FromResult(Messages.Select(m => Json.Codec.Deserialize<ReceivedMessage>(Json.Codec.Serialize(m))).ToList());
            }
            public Task DeleteThrough(Session session, long id, CancellationToken token) {
                if (FailDelete) throw new ClientFailure("mock temporary error", false);
                Deleted = id; Deletes++; return Task.FromResult(0);
            }
            public Task Acknowledge(Session session, string receipt, CancellationToken token) { return Task.FromResult(0); }
        }
        private static void Assert(bool condition, string label) { if (!condition) throw new Exception("Failed: " + label); }
        private static ReceivedMessage Message(long id, int priority) {
            return new ReceivedMessage { Id = id, Title = "测试 / Test <&>", Body = "中英文 Unicode & <xml>\nEnglish line", Priority = priority, Date = 1791223200, Receipt = "dummy-receipt" };
        }
        private static async Task Cases(string home) {
            var store = new LocalStore(home);
            var session = new Session { Secret = "dummy-test-secret", DeviceId = "dummy-device", DeviceName = "test-device" };
            store.SaveSession(session);
            Assert(!File.ReadAllText(Path.Combine(home, "session.json")).Contains(session.Secret), "DPAPI ciphertext only");
            Assert(store.ReadSession().Secret == session.Secret, "DPAPI round trip");
            Assert(new DirectoryInfo(home).GetAccessControl().AreAccessRulesProtected, "protected directory ACL");
            var data = Json.Codec.Deserialize<Dictionary<string, object>>("{\"id_str\":\"9007199254740993\",\"id\":9007199254740993,\"title\":\"\",\"app\":\"Example\",\"message\":\"中文\",\"priority\":0}");
            Assert(ReceivedMessage.Parse(data).Id == 9007199254740993L && ReceivedMessage.Parse(data).Title == "Example", "exact 64-bit identity and title fallback");
            var list = Json.Codec.Deserialize<Dictionary<string, object>>("{\"status\":1,\"messages\":[{\"id\":1}]}");
            Assert(((System.Collections.IEnumerable)list["messages"]).Cast<object>().Count() == 1, "official message-array decoding");
            var xml = new XmlDocument(); xml.LoadXml(WindowsIntegration.ToastXml(Message(1, -1)));
            Assert(xml.SelectSingleNode("/toast/visual/binding/text[2]").InnerText.Contains("中英文"), "Unicode/XML escaping");
            Assert(xml.SelectSingleNode("/toast/audio").Attributes["silent"].Value == "true", "low-priority sound suppression");
            Assert(WindowsIntegration.NeedsAttention(new ReceivedMessage { Title = "[voice] Action needed: choose a voice", Priority = 0 }), "agent decision requests use high Windows priority without changing phone quiet hours");
            Assert(!WindowsIntegration.NeedsAttention(new ReceivedMessage { Title = "[voice] Complete: demo", Priority = 0 }), "ordinary completion does not escalate desktop priority");
            Assert(Receiver.LoginFrame(session) == "login:dummy-device:dummy-test-secret\n", "WebSocket login protocol");
            Assert(Receiver.PermanentFrame('A') && Receiver.PermanentFrame('E') && !Receiver.PermanentFrame('R'), "permanent signals stop reconnect");
            Assert(Receiver.BackoffSeconds(1) == 5 && Receiver.BackoffSeconds(20) == 300, "bounded exponential backoff");
            int notifications = 0;
            var api = new FakeApi();
            var receiver = new Receiver(api, store, session, m => notifications++, text => { });
            api.Messages.Add(Message(1, 0)); await receiver.Sync(false, CancellationToken.None);
            Assert(notifications == 0 && api.Deleted == 1 && store.Snapshot().Count == 1, "startup backlog stored without alert flood");
            api.Messages = new List<ReceivedMessage> { Message(3, 0), Message(2, 0) };
            api.FailDelete = true;
            try { await receiver.Sync(true, CancellationToken.None); } catch (ClientFailure) { }
            Assert(notifications == 2, "new messages processed in identity order");
            api.FailDelete = false; await receiver.Sync(true, CancellationToken.None);
            Assert(notifications == 2 && api.Deleted == 3, "acknowledgement failure does not duplicate local alerts");
            var restarted = new LocalStore(home);
            var restartReceiver = new Receiver(api, restarted, session, m => notifications++, text => { });
            await restartReceiver.Sync(true, CancellationToken.None);
            Assert(notifications == 2, "restart deduplication");
            api.Messages = new List<ReceivedMessage> { Message(4, -2) }; await restartReceiver.Sync(true, CancellationToken.None);
            Assert(notifications == 2 && api.Deleted == 4, "lowest priority stored without popup");
            bool alertFailed = false;
            var failing = new Receiver(api, restarted, session, m => { alertFailed = true; throw new Exception("mock toast failure"); }, text => { });
            api.Messages = new List<ReceivedMessage> { Message(5, 0) };
            int deletesBefore = api.Deletes;
            try { await failing.Sync(true, CancellationToken.None); } catch (Exception) { }
            Assert(alertFailed && api.Deletes == deletesBefore && restarted.Snapshot().Any(m => m.Id == 5 && m.NeedsAlert), "toast failure retains recoverable message and does not delete server queue");
            await restartReceiver.Sync(true, CancellationToken.None);
            Assert(notifications == 3 && api.Deleted == 5, "pending alert recovery");
            api.Messages = new List<ReceivedMessage> { Message(6, 2) };
            await restartReceiver.Sync(true, CancellationToken.None); await restartReceiver.Sync(true, CancellationToken.None);
            Assert(notifications == 5, "emergency repeats remain visible until explicit acknowledgement");
            for (long id = 7; id < 212; id++) { var ordinary = restarted.Stage(Message(id, 0), false); restarted.Complete(ordinary); }
            Assert(restarted.Snapshot().Any(m => m.Id == 6), "unacknowledged emergency survives ordinary history limit");
            restartReceiver.RestoreEmergencyPrompts();
            Assert(notifications == 6, "saved emergency prompts restored after restart");
            restarted.MarkAcknowledged("dummy-receipt"); await restartReceiver.Sync(true, CancellationToken.None);
            Assert(notifications == 6, "explicit acknowledgement stops repeated local emergency prompts");
            var another = new Session { Secret = "second-dummy", DeviceId = "another-device", DeviceName = "another" };
            var changedDevice = new Receiver(api, restarted, another, m => notifications++, text => { });
            api.Messages = new List<ReceivedMessage> { Message(1, 0) }; await changedDevice.Sync(true, CancellationToken.None);
            Assert(notifications == 7 && Directory.GetFiles(home, "inbox-previous-*.json").Length == 1, "device replacement resets identity namespace and preserves old inbox");
        }
        private static string TestDirectory() { return Path.Combine(Path.GetTempPath(), "agent-notify-test-" + Guid.NewGuid().ToString("N")); }
        private static void Cleanup(string home) {
            string full = Path.GetFullPath(home);
            string root = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(full).StartsWith("agent-notify-test-")) throw new Exception("Unsafe test cleanup path.");
            if (Directory.Exists(full)) Directory.Delete(full, true);
        }
        public static int Run(string output) {
            string home = TestDirectory();
            try { Cases(home).GetAwaiter().GetResult(); File.WriteAllText(output, "PASS: offline encryption, ACL, protocol, durable inbox, deduplication, backoff, Unicode, priorities, failure recovery and device replacement. No HTTP or WebSocket connections."); return 0; }
            catch (Exception error) { File.WriteAllText(output, "FAIL: " + error.Message); return 1; }
            finally { Cleanup(home); }
        }
        public static void Render(string outputPrefix) {
            string home = TestDirectory();
            try {
                using (var api = new ClientApi()) using (var login = new LoginForm(api, new LocalStore(home), "agent-example-pc")) {
                    login.StartPosition = FormStartPosition.Manual; login.Location = new Point(-20000, -20000);
                    login.Show(); Application.DoEvents(); login.PerformLayout();
                    using (var bitmap = new Bitmap(login.Width, login.Height)) { login.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save(outputPrefix + "-setup.png"); }
                    login.Hide();
                }
                using (var inbox = new InboxForm()) {
                    inbox.StartPosition = FormStartPosition.Manual; inbox.Location = new Point(-20000, -20000);
                    inbox.RefreshMessages(new List<ReceivedMessage> { Message(1, 0) }); inbox.Show(); Application.DoEvents(); inbox.PerformLayout();
                    using (var bitmap = new Bitmap(inbox.Width, inbox.Height)) { inbox.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save(outputPrefix + "-inbox.png"); }
                    inbox.Hide();
                }
            } finally { Cleanup(home); }
        }
    }
}
