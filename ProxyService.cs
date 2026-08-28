using Microsoft.Win32;
using System;
using System.Linq;
using System.Runtime.InteropServices;

namespace DiscordProxyLauncher
{
    internal static class ProxyService
    {
        private const string RegistryPath =
            @"Software\Microsoft\Windows\CurrentVersion\Internet Settings";

        private const int InternetOptionRefresh = 37;
        private const int InternetOptionSettingsChanged = 39;
        private const int InternetOptionProxySettingsChanged = 95;

        [DllImport("wininet.dll", SetLastError = true)]
        private static extern bool InternetSetOption(
            IntPtr hInternet,
            int dwOption,
            IntPtr lpBuffer,
            int dwBufferLength);

        internal sealed class ProxySnapshot
        {
            public bool ProxyEnableExists { get; private set; }
            public object ProxyEnable { get; private set; }
            public RegistryValueKind? ProxyEnableKind { get; private set; }
            public bool ProxyServerExists { get; private set; }
            public object ProxyServer { get; private set; }
            public RegistryValueKind? ProxyServerKind { get; private set; }

            public ProxySnapshot(
                bool proxyEnableExists,
                object proxyEnable,
                RegistryValueKind? proxyEnableKind,
                bool proxyServerExists,
                object proxyServer,
                RegistryValueKind? proxyServerKind)
            {
                ProxyEnableExists = proxyEnableExists;
                ProxyEnable = proxyEnable;
                ProxyEnableKind = proxyEnableKind;
                ProxyServerExists = proxyServerExists;
                ProxyServer = proxyServer;
                ProxyServerKind = proxyServerKind;
            }
        }

        public static ProxySnapshot Capture()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryPath, false))
            {
                if (key == null)
                    throw new InvalidOperationException(
                        "Não foi possível acessar as configurações de proxy do Windows.");

                string[] valueNames = key.GetValueNames();

                bool enableExists = valueNames.Contains("ProxyEnable", StringComparer.OrdinalIgnoreCase);
                bool serverExists = valueNames.Contains("ProxyServer", StringComparer.OrdinalIgnoreCase);

                return new ProxySnapshot(
                    enableExists,
                    enableExists ? key.GetValue("ProxyEnable") : null,
                    enableExists ? (RegistryValueKind?)key.GetValueKind("ProxyEnable") : null,
                    serverExists,
                    serverExists ? key.GetValue("ProxyServer") : null,
                    serverExists ? (RegistryValueKind?)key.GetValueKind("ProxyServer") : null);
            }
        }

        public static void Enable(string host, int port)
        {
            if (string.IsNullOrWhiteSpace(host))
                throw new ArgumentException("O endereço do proxy está vazio.", "host");

            if (port < 1 || port > 65535)
                throw new ArgumentOutOfRangeException("port", "Porta inválida.");

            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryPath, true))
            {
                if (key == null)
                    throw new InvalidOperationException(
                        "Não foi possível alterar as configurações de proxy do Windows.");

                key.SetValue("ProxyEnable", 1, RegistryValueKind.DWord);
                key.SetValue("ProxyServer", host + ":" + port, RegistryValueKind.String);
                key.Flush();
            }

            NotifySettingsChanged();
        }

        public static void Restore(ProxySnapshot snapshot)
        {
            if (snapshot == null)
                throw new ArgumentNullException("snapshot");

            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryPath, true))
            {
                if (key == null)
                    throw new InvalidOperationException(
                        "Não foi possível restaurar as configurações de proxy do Windows.");

                if (snapshot.ProxyEnableExists)
                {
                    key.SetValue(
                        "ProxyEnable",
                        snapshot.ProxyEnable,
                        snapshot.ProxyEnableKind ?? RegistryValueKind.DWord);
                }
                else
                {
                    key.DeleteValue("ProxyEnable", false);
                }

                if (snapshot.ProxyServerExists)
                {
                    key.SetValue(
                        "ProxyServer",
                        snapshot.ProxyServer,
                        snapshot.ProxyServerKind ?? RegistryValueKind.String);
                }
                else
                {
                    key.DeleteValue("ProxyServer", false);
                }

                key.Flush();
            }

            NotifySettingsChanged();
        }

        private static void NotifySettingsChanged()
        {
            InternetSetOption(IntPtr.Zero, InternetOptionSettingsChanged, IntPtr.Zero, 0);
            InternetSetOption(IntPtr.Zero, InternetOptionProxySettingsChanged, IntPtr.Zero, 0);
            InternetSetOption(IntPtr.Zero, InternetOptionRefresh, IntPtr.Zero, 0);
        }
    }
}
