using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Security;
using System.Text;
using Microsoft.Win32;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace AgentNotify {
    public static class WindowsIntegration {
        public const string AppId = "Personal.AgentNotify";
        public const string Protocol = "personalagentnotify";
        // Protocol-only activation uses the documented stub CLSID registration.
        // No COM server is registered; every toast opens the app's own URL protocol.
        public static readonly Guid ToastActivator = new Guid("4d36a2d9-b740-46cd-b175-4eb8fa534d11");
        public static string TestPhase = "initialization";
        public static readonly uint OpenMessage = RegisterWindowMessage("Personal.AgentNotify.OpenInbox");
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern int SetCurrentProcessExplicitAppUserModelID(string appId);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern uint RegisterWindowMessage(string name);
        [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
        [DllImport("ole32.dll")] private static extern int PropVariantClear(ref PropVariant value);
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern void SHChangeNotify(uint eventId, uint flags, string item1, IntPtr item2);

        public static void OpenExistingInbox() { PostMessage(new IntPtr(0xffff), OpenMessage, IntPtr.Zero, IntPtr.Zero); }
        public static void Register(string executable) {
            TestPhase = "process identity";
            Marshal.ThrowExceptionForHR(SetCurrentProcessExplicitAppUserModelID(AppId));
            string shortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Agent Notify.lnk");
            object shell = new ShellLink();
            TestPhase = "Start Menu shortcut";
            try {
                var link = (IShellLinkW)shell;
                link.SetPath(executable);
                link.SetArguments("--open-inbox");
                link.SetDescription("Agent Notify - unofficial Pushover Open Client");
                link.SetWorkingDirectory(Path.GetDirectoryName(executable));
                link.SetIconLocation(executable, 0);
                var property = new PropertyKey { FormatId = new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), Id = 5 };
                var value = new PropVariant { Type = 31, Pointer = Marshal.StringToCoTaskMemUni(AppId) };
                try { ((IPropertyStore)shell).SetValue(ref property, ref value); }
                finally { PropVariantClear(ref value); }
                property.Id = 26;
                value = new PropVariant { Type = 72, Pointer = Marshal.AllocCoTaskMem(16) };
                Marshal.StructureToPtr(ToastActivator, value.Pointer, false);
                try { ((IPropertyStore)shell).SetValue(ref property, ref value); ((IPropertyStore)shell).Commit(); }
                finally { PropVariantClear(ref value); }
                ((IPersistFile)shell).Save(shortcut, true);
            } finally { Marshal.FinalReleaseComObject(shell); }
            SHChangeNotify(0x00000002, 0x00001005, shortcut, IntPtr.Zero);
            SHChangeNotify(0x00002000, 0x00001005, Path.GetDirectoryName(shortcut), IntPtr.Zero);
            // Desktop notification discovery also uses the current user's AUMID registry entry.
            TestPhase = "desktop notification identity";
            using (var identity = Registry.CurrentUser.CreateSubKey("Software\\Classes\\AppUserModelId\\" + AppId)) {
                identity.SetValue("DisplayName", "Agent Notify");
                identity.SetValue("ShowInSettings", 1, RegistryValueKind.DWord);
                identity.SetValue("CustomActivator", ToastActivator.ToString("B"));
            }
            TestPhase = "activation protocol";
            string command = "\"" + executable + "\" --open-inbox";
            string registryPath = "Software\\Classes\\" + Protocol;
            using (var existing = Registry.CurrentUser.OpenSubKey(registryPath + "\\shell\\open\\command")) {
                string current = existing == null ? null : existing.GetValue("") as string;
                if (current != null && !String.Equals(current, command, StringComparison.OrdinalIgnoreCase))
                    throw new IOException("Another installation owns the notification activation protocol. Preserve it and reconcile the installation.");
            }
            using (var key = Registry.CurrentUser.CreateSubKey(registryPath)) {
                key.SetValue("", "URL:Agent Notify"); key.SetValue("URL Protocol", "");
                using (var icon = key.CreateSubKey("DefaultIcon")) icon.SetValue("", "\"" + executable + "\",0");
                using (var action = key.CreateSubKey("shell\\open\\command")) action.SetValue("", command);
            }
        }
        public static bool StartsAtLogin() {
            using (var key = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run"))
                return key != null && key.GetValue("PersonalAgentNotify") != null;
        }
        public static void SetStartup(bool enabled, string executable) {
            using (var key = Registry.CurrentUser.CreateSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run")) {
                if (enabled) key.SetValue("PersonalAgentNotify", "\"" + executable + "\" --background");
                else key.DeleteValue("PersonalAgentNotify", false);
            }
        }
        public static string ToastXml(ReceivedMessage message) {
            string silent = message.Priority <= -1 ? "<audio silent=\"true\"/>" : "";
            return "<toast activationType=\"protocol\" launch=\"" + Protocol + ":inbox\"><visual><binding template=\"ToastGeneric\"><text>" +
                SecurityElement.Escape(message.Title ?? "Agent Notify") + "</text><text>" + SecurityElement.Escape(message.Body ?? "") +
                "</text></binding></visual>" + silent + "</toast>";
        }
        public static bool NeedsAttention(ReceivedMessage message) {
            return message.Priority >= 1 || (message.Title != null && message.Title.IndexOf("Action needed:", StringComparison.OrdinalIgnoreCase) >= 0);
        }
        public static string ToastTag(long id) {
            using (var sha = System.Security.Cryptography.SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(id.ToString()))).Replace("-", "").Substring(0, 16);
        }
        public static void Toast(ReceivedMessage message) {
            TestPhase = "create native notifier";
            var notifier = ToastNotificationManager.CreateToastNotifier(AppId);
            TestPhase = "check notification setting";
            NotificationSetting setting;
            try { setting = notifier.Setting; }
            catch (Exception error) {
                // Identity-less desktop apps have no settings entry until their first toast.
                // Let Show establish it; retain all other failures and explicit user blocks.
                if (error.HResult != unchecked((int)0x80070490)) throw;
                setting = NotificationSetting.Enabled;
            }
            if (setting != NotificationSetting.Enabled)
                throw new ClientFailure("Windows notifications for Agent Notify are disabled. Enable them in Windows settings and reconnect.", true);
            var document = new XmlDocument(); document.LoadXml(ToastXml(message));
            TestPhase = "build native toast";
            var notification = new ToastNotification(document);
            notification.Priority = NeedsAttention(message) ? ToastNotificationPriority.High : ToastNotificationPriority.Default;
            notification.Group = "AgentNotify";
            notification.Tag = ToastTag(message.Id);
            notification.ExpirationTime = DateTimeOffset.Now.AddDays(1);
            TestPhase = "show native toast";
            notifier.Show(notification);
        }

        [ComImport, Guid("00021401-0000-0000-C000-000000000046")] private class ShellLink { }
        [ComImport, Guid("000214F9-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellLinkW {
            void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder file, int max, IntPtr data, uint flags);
            void GetIDList(out IntPtr list); void SetIDList(IntPtr list);
            void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder text, int max);
            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string text);
            void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder path, int max);
            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string path);
            void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder arguments, int max);
            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string arguments);
            void GetHotkey(out short key); void SetHotkey(short key); void GetShowCmd(out int command); void SetShowCmd(int command);
            void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder location, int max, out int index);
            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string location, int index);
            void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string path, uint reserved);
            void Resolve(IntPtr window, uint flags); void SetPath([MarshalAs(UnmanagedType.LPWStr)] string path);
        }
        [StructLayout(LayoutKind.Sequential)] private struct PropertyKey { public Guid FormatId; public uint Id; }
        [StructLayout(LayoutKind.Explicit, Size = 24)] private struct PropVariant {
            [FieldOffset(0)] public ushort Type;
            [FieldOffset(8)] public IntPtr Pointer;
        }
        [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IPropertyStore {
            void GetCount(out uint count); void GetAt(uint index, out PropertyKey key); void GetValue(ref PropertyKey key, out PropVariant value);
            void SetValue(ref PropertyKey key, ref PropVariant value); void Commit();
        }
    }
}
