using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.WebSockets;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace AgentNotify {
    public sealed class ClientFailure : Exception {
        public bool Permanent;
        public bool NeedsTwoFactor;
        public ClientFailure(string message, bool permanent) : base(message) { Permanent = permanent; }
    }
    public static class Json {
        public static JavaScriptSerializer Codec { get { return new JavaScriptSerializer { MaxJsonLength = 4 * 1024 * 1024 }; } }
        public static string Text(IDictionary<string, object> data, string key) {
            object value;
            return data.TryGetValue(key, out value) && value != null ? Convert.ToString(value, CultureInfo.InvariantCulture) : "";
        }
        public static int Number(IDictionary<string, object> data, string key) {
            int result; return Int32.TryParse(Text(data, key), out result) ? result : 0;
        }
    }
    public sealed class Session {
        public string Secret { get; set; }
        public string DeviceId { get; set; }
        public string DeviceName { get; set; }
    }
    public sealed class ReceivedMessage {
        public long Id { get; set; }
        public string Title { get; set; }
        public string Body { get; set; }
        public string Url { get; set; }
        public long Date { get; set; }
        public int Priority { get; set; }
        public string Receipt { get; set; }
        public bool Acked { get; set; }
        public bool NeedsAlert { get; set; }
        public static ReceivedMessage Parse(IDictionary<string, object> data) {
            string id = Json.Text(data, "id_str");
            if (id.Length == 0) id = Json.Text(data, "id");
            long numericId;
            if (!Int64.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out numericId) || numericId <= 0)
                throw new ClientFailure("Invalid message identity; sync stopped without deleting messages.", true);
            long date; Int64.TryParse(Json.Text(data, "date"), out date);
            string title = Json.Text(data, "title");
            if (title.Length == 0) title = Json.Text(data, "app");
            return new ReceivedMessage { Id = numericId, Title = title, Body = Json.Text(data, "message"),
                Url = Json.Text(data, "url"), Date = date, Priority = Json.Number(data, "priority"),
                Receipt = Json.Text(data, "receipt"), Acked = Json.Number(data, "acked") == 1 };
        }
    }
    public sealed class InboxState {
        public string DeviceId { get; set; }
        public long ProcessedThrough { get; set; }
        public List<ReceivedMessage> Messages { get; set; }
        public InboxState() { Messages = new List<ReceivedMessage>(); }
    }
    public sealed class LocalStore {
        public readonly string DirectoryPath;
        private readonly object gate = new object();
        private InboxState state;
        public LocalStore(string directory) {
            DirectoryPath = directory;
            Directory.CreateDirectory(directory);
            var acl = new DirectorySecurity();
            acl.SetAccessRuleProtection(true, false);
            foreach (var sid in new[] { WindowsIdentity.GetCurrent().User, new SecurityIdentifier("S-1-5-18") })
                acl.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.FullControl,
                    InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
            new DirectoryInfo(directory).SetAccessControl(acl);
            state = File.Exists(Path.Combine(directory, "inbox.json"))
                ? Json.Codec.Deserialize<InboxState>(File.ReadAllText(Path.Combine(directory, "inbox.json"), Encoding.UTF8)) : new InboxState();
            if (state == null || state.Messages == null || state.ProcessedThrough < 0) throw new IOException("Invalid inbox state.");
        }
        private void AtomicWrite(string filename, string text) {
            string destination = Path.Combine(DirectoryPath, filename);
            string temp = destination + ".new";
            using (var file = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None)) {
                byte[] bytes = Encoding.UTF8.GetBytes(text); file.Write(bytes, 0, bytes.Length); file.Flush(true);
            }
            if (File.Exists(destination)) File.Replace(temp, destination, null);
            else File.Move(temp, destination);
        }
        public void SaveSession(Session session) {
            byte[] plain = Encoding.UTF8.GetBytes(Json.Codec.Serialize(session));
            try { AtomicWrite("session.json", Convert.ToBase64String(ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser))); }
            finally { Array.Clear(plain, 0, plain.Length); }
        }
        public void BindDevice(string deviceId) {
            lock (gate) {
                if (state.DeviceId != null && state.DeviceId != deviceId) {
                    string oldInbox = Path.Combine(DirectoryPath, "inbox.json");
                    if (File.Exists(oldInbox)) File.Copy(oldInbox, Path.Combine(DirectoryPath, "inbox-previous-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss") + "-" + Guid.NewGuid().ToString("N") + ".json"));
                    state = new InboxState();
                }
                state.DeviceId = deviceId;
                AtomicWrite("inbox.json", Json.Codec.Serialize(state));
            }
        }
        public Session ReadSession() {
            string path = Path.Combine(DirectoryPath, "session.json");
            if (!File.Exists(path)) return null;
            byte[] plain = ProtectedData.Unprotect(Convert.FromBase64String(File.ReadAllText(path)), null, DataProtectionScope.CurrentUser);
            try {
                Session session = Json.Codec.Deserialize<Session>(Encoding.UTF8.GetString(plain));
                if (session == null || String.IsNullOrWhiteSpace(session.Secret) || String.IsNullOrWhiteSpace(session.DeviceId))
                    throw new IOException("Invalid session.");
                return session;
            } finally { Array.Clear(plain, 0, plain.Length); }
        }
        public ReceivedMessage Stage(ReceivedMessage incoming, bool notify) {
            lock (gate) {
                ReceivedMessage existing = state.Messages.FirstOrDefault(m => m.Id == incoming.Id);
                if (existing != null) return existing;
                if (incoming.Id <= state.ProcessedThrough) return null;
                incoming.NeedsAlert = notify && incoming.Priority > -2;
                state.Messages.Add(incoming);
                AtomicWrite("inbox.json", Json.Codec.Serialize(state));
                return incoming;
            }
        }
        public void Complete(ReceivedMessage message) {
            lock (gate) {
                message.NeedsAlert = false;
                state.ProcessedThrough = Math.Max(state.ProcessedThrough, message.Id);
                // Preserve unalerted messages; cap ordinary stored history.
                state.Messages = state.Messages.OrderByDescending(m => m.Id)
                    .Where((m, i) => i < 200 || m.NeedsAlert || (m.Priority >= 2 && !m.Acked)).ToList();
                AtomicWrite("inbox.json", Json.Codec.Serialize(state));
            }
        }
        public List<ReceivedMessage> Snapshot() {
            lock (gate) { return state.Messages.OrderByDescending(m => m.Id).Select(m => Json.Codec.Deserialize<ReceivedMessage>(Json.Codec.Serialize(m))).ToList(); }
        }
        public void MarkAcknowledged(string receipt) {
            lock (gate) {
                foreach (var message in state.Messages.Where(m => m.Receipt == receipt)) message.Acked = true;
                AtomicWrite("inbox.json", Json.Codec.Serialize(state));
            }
        }
    }
    public interface IClientApi {
        Task<List<ReceivedMessage>> Download(Session session, CancellationToken token);
        Task DeleteThrough(Session session, long id, CancellationToken token);
        Task Acknowledge(Session session, string receipt, CancellationToken token);
    }
    public sealed class ClientApi : IClientApi, IDisposable {
        private readonly HttpClient http = new HttpClient();
        public ClientApi() {
            http.Timeout = TimeSpan.FromSeconds(25);
            http.DefaultRequestHeaders.UserAgent.ParseAdd("AgentNotify/0.1 (Windows; unofficial Open Client)");
        }
        private async Task<Dictionary<string, object>> Request(string route, Dictionary<string, string> values, bool get, CancellationToken token) {
            // The official GET endpoint puts the device secret in its query. Never log URIs or raw exceptions.
            string url = "https://api.pushover.net/1/" + route + ".json";
            try {
                using (var content = new FormUrlEncodedContent(values)) {
                    HttpResponseMessage response = get ? await http.GetAsync(url + "?" + await content.ReadAsStringAsync(), token)
                        : await http.PostAsync(url, content, token);
                    using (response) {
                        int status = (int)response.StatusCode;
                        if (status == 412) throw new ClientFailure("Enter the current two-factor code and try login again.", true) { NeedsTwoFactor = true };
                        if (status >= 400 && status < 500) throw new ClientFailure("Request rejected (HTTP " + status + "). Check login, device name and Desktop licensing before retrying.", true);
                        if (!response.IsSuccessStatusCode) throw new ClientFailure("Service unavailable; reconnecting with backoff.", false);
                        var data = Json.Codec.Deserialize<Dictionary<string, object>>(await response.Content.ReadAsStringAsync());
                        if (data == null || Json.Number(data, "status") != 1) throw new ClientFailure("The server rejected this operation. Check the account/device.", true);
                        return data;
                    }
                }
            } catch (ClientFailure) { throw; }
            catch (OperationCanceledException) { if (token.IsCancellationRequested) throw; throw new ClientFailure("Network request timed out. Check connectivity.", false); }
            catch { throw new ClientFailure("Network operation failed. Check connectivity.", false); }
        }
        public async Task<Session> LoginAndRegister(string email, string password, string twoFactor, string deviceName, CancellationToken token) {
            var fields = new Dictionary<string, string> { { "email", email }, { "password", password } };
            if (twoFactor.Length > 0) fields["twofa"] = twoFactor;
            var result = await Request("users/login", fields, false, token);
            fields.Clear();
            string secret = Json.Text(result, "secret");
            if (String.IsNullOrWhiteSpace(secret)) throw new ClientFailure("Login did not return a device session.", true);
            var registration = await Request("devices", new Dictionary<string, string> { { "secret", secret }, { "name", deviceName }, { "os", "O" } }, false, token);
            string id = Json.Text(registration, "id");
            if (String.IsNullOrWhiteSpace(id)) throw new ClientFailure("Device registration did not return an identity.", true);
            return new Session { Secret = secret, DeviceId = id, DeviceName = deviceName };
        }
        public async Task<List<ReceivedMessage>> Download(Session session, CancellationToken token) {
            var result = await Request("messages", new Dictionary<string, string> { { "secret", session.Secret }, { "device_id", session.DeviceId } }, true, token);
            object raw;
            if (!result.TryGetValue("messages", out raw) || !(raw is System.Collections.IEnumerable) || raw is string || raw is System.Collections.IDictionary)
                throw new ClientFailure("Invalid message list; nothing was deleted.", true);
            return ((System.Collections.IEnumerable)raw).Cast<object>().Select(item => ReceivedMessage.Parse((Dictionary<string, object>)item)).OrderBy(m => m.Id).ToList();
        }
        public async Task DeleteThrough(Session session, long id, CancellationToken token) {
            await Request("devices/" + Uri.EscapeDataString(session.DeviceId) + "/update_highest_message",
                new Dictionary<string, string> { { "secret", session.Secret }, { "message", id.ToString(CultureInfo.InvariantCulture) } }, false, token);
        }
        public async Task Acknowledge(Session session, string receipt, CancellationToken token) {
            await Request("receipts/" + Uri.EscapeDataString(receipt) + "/acknowledge", new Dictionary<string, string> { { "secret", session.Secret } }, false, token);
        }
        public void Dispose() { http.Dispose(); }
    }
    public sealed class Receiver {
        private readonly IClientApi api;
        private readonly LocalStore store;
        private readonly Session session;
        private readonly Action<ReceivedMessage> alert;
        private readonly Action<string> status;
        public Receiver(IClientApi api, LocalStore store, Session session, Action<ReceivedMessage> alert, Action<string> status) {
            this.api = api; this.store = store; this.session = session; this.alert = alert; this.status = status;
            store.BindDevice(session.DeviceId);
        }
        public static string LoginFrame(Session session) { return "login:" + session.DeviceId + ":" + session.Secret + "\n"; }
        public static int BackoffSeconds(int failures) { return (int)Math.Min(300, 5 * Math.Pow(2, Math.Min(6, Math.Max(0, failures - 1)))); }
        public static bool PermanentFrame(char signal) { return signal == 'A' || signal == 'E'; }
        public void RestoreEmergencyPrompts() {
            foreach (var message in store.Snapshot().Where(m => m.Priority >= 2 && !m.Acked)) alert(message);
        }
        public async Task Sync(bool notify, CancellationToken token) {
            var messages = await api.Download(session, token);
            foreach (var incoming in messages.OrderBy(m => m.Id)) {
                var message = store.Stage(incoming, notify);
                if (message == null) continue;
                message.Acked = message.Acked || incoming.Acked;
                if (message.NeedsAlert || (message.Priority >= 2 && !message.Acked)) alert(message);
                store.Complete(message);
            }
            // Only this device's downloaded queue is removed, after durable storage and processing.
            if (messages.Count > 0) await api.DeleteThrough(session, messages.Max(m => m.Id), token);
        }
        public async Task Run(CancellationToken token) {
            int failures = 0;
            bool first = true;
            try { RestoreEmergencyPrompts(); }
            catch { status("A saved emergency notification could not be opened. Inspect the inbox."); }
            while (!token.IsCancellationRequested) {
                DateTime connectedAt = DateTime.MinValue;
                try {
                    status("Connecting / 正在连接");
                    await Sync(!first, token);
                    first = false;
                    using (var socket = new ClientWebSocket()) {
                        socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(30);
                        using (var connecting = CancellationTokenSource.CreateLinkedTokenSource(token)) {
                            connecting.CancelAfter(TimeSpan.FromSeconds(25));
                            await socket.ConnectAsync(new Uri("wss://client.pushover.net/push"), connecting.Token);
                        }
                        byte[] login = Encoding.UTF8.GetBytes(LoginFrame(session));
                        try { await socket.SendAsync(new ArraySegment<byte>(login), WebSocketMessageType.Text, true, token); }
                        finally { Array.Clear(login, 0, login.Length); }
                        connectedAt = DateTime.UtcNow;
                        status("Connected / 已连接");
                        // Close the race between initial HTTP download and WebSocket subscription.
                        await Sync(true, token);
                        byte[] buffer = new byte[1024];
                        while (!token.IsCancellationRequested && socket.State == WebSocketState.Open) {
                            WebSocketReceiveResult frame;
                            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(token)) {
                                timeout.CancelAfter(TimeSpan.FromSeconds(100));
                                frame = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), timeout.Token);
                            }
                            if (frame.MessageType == WebSocketMessageType.Close) break;
                            bool reload = false;
                            for (int i = 0; i < frame.Count; i++) {
                                char signal = (char)buffer[i];
                                if (PermanentFrame(signal)) throw new ClientFailure(signal == 'A'
                                    ? "Another client is using this device session. Receiving stopped."
                                    : "Device authorization failed. Receiving stopped; check account or sign in again.", true);
                                if (signal == '!') await Sync(true, token);
                                if (signal == 'R') reload = true;
                            }
                            if (reload) break;
                        }
                    }
                } catch (ClientFailure error) {
                    status(error.Message);
                    if (error.Permanent) return;
                } catch (OperationCanceledException) { if (token.IsCancellationRequested) return; }
                catch { status("Connection interrupted / 连接中断"); }
                if (connectedAt != DateTime.MinValue && (DateTime.UtcNow - connectedAt).TotalSeconds >= 60) failures = 0;
                int seconds = BackoffSeconds(++failures);
                status("Retry in " + seconds + "s / 稍后重连");
                try { await Task.Delay(TimeSpan.FromSeconds(seconds), token); }
                catch (OperationCanceledException) { return; }
            }
        }
    }
}
