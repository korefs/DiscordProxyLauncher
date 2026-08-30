using Microsoft.Win32;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Threading.Tasks;

namespace DiscordProxyLauncher
{
    internal static class ProxyService
    {
        private const string ValidationUrl = "https://discord.com/api/v10/gateway";
        private const string ProxyListUrl =
            "https://proxyfreeonly.com/api/free-proxy-list" +
            "?limit=10&page=1&sortBy=lastChecked&sortType=desc&country=US";

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

        internal sealed class ProxyEndpoint
        {
            public string Host { get; private set; }
            public int Port { get; private set; }

            public ProxyEndpoint(string host, int port)
            {
                Host = host;
                Port = port;
            }

            public override string ToString()
            {
                return Host + ":" + Port;
            }
        }

        [DataContract]
        private sealed class ProxyApiItem
        {
            [DataMember(Name = "ip")]
            public string Ip { get; set; }

            [DataMember(Name = "port")]
            public string Port { get; set; }

            [DataMember(Name = "protocols")]
            public string[] Protocols { get; set; }
        }

        public static Task<bool> IsAvailableAsync(
            ProxyEndpoint endpoint,
            TimeSpan timeout)
        {
            if (endpoint == null)
                throw new ArgumentNullException("endpoint");

            return Task.Run(() => IsAvailable(endpoint, timeout));
        }

        public static Task<IList<ProxyEndpoint>> FetchProxiesAsync(TimeSpan timeout)
        {
            return Task.Run(() => FetchProxies(timeout));
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

        private static bool IsAvailable(ProxyEndpoint endpoint, TimeSpan timeout)
        {
            try
            {
                int timeoutMilliseconds = (int)Math.Max(
                    1000,
                    Math.Min(int.MaxValue, timeout.TotalMilliseconds));

                HttpWebRequest request = WebRequest.CreateHttp(ValidationUrl);
                request.Method = "GET";
                request.Proxy = new WebProxy(endpoint.Host, endpoint.Port);
                request.Timeout = timeoutMilliseconds;
                request.ReadWriteTimeout = timeoutMilliseconds;
                request.UserAgent = "DiscordProxyLauncher/1.3";
                request.KeepAlive = false;

                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                {
                    string content = reader.ReadToEnd();

                    return response.StatusCode == HttpStatusCode.OK &&
                        content.IndexOf(
                            "gateway.discord.gg",
                            StringComparison.OrdinalIgnoreCase) >= 0;
                }
            }
            catch
            {
                return false;
            }
        }

        private static IList<ProxyEndpoint> FetchProxies(TimeSpan timeout)
        {
            int timeoutMilliseconds = (int)Math.Max(
                1000,
                Math.Min(int.MaxValue, timeout.TotalMilliseconds));

            HttpWebRequest request = WebRequest.CreateHttp(ProxyListUrl);
            request.Method = "GET";
            request.Accept = "application/json";
            request.UserAgent = "DiscordProxyLauncher/1.3";
            request.AutomaticDecompression =
                DecompressionMethods.GZip | DecompressionMethods.Deflate;
            request.Timeout = timeoutMilliseconds;
            request.ReadWriteTimeout = timeoutMilliseconds;

            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            {
                if (response.StatusCode != HttpStatusCode.OK)
                    throw new InvalidOperationException(
                        "O serviço de proxies respondeu com o status " +
                        (int)response.StatusCode + ".");

                DataContractJsonSerializer serializer =
                    new DataContractJsonSerializer(typeof(ProxyApiItem[]));

                using (Stream stream = response.GetResponseStream())
                {
                    ProxyApiItem[] items =
                        (ProxyApiItem[])serializer.ReadObject(stream);

                    return (items ?? new ProxyApiItem[0])
                        .Take(10)
                        .Where(IsSupportedProxy)
                        .Select(item => new ProxyEndpoint(
                            item.Ip,
                            int.Parse(item.Port)))
                        .GroupBy(proxy => proxy.ToString(), StringComparer.OrdinalIgnoreCase)
                        .Select(group => group.First())
                        .ToList();
                }
            }
        }

        private static bool IsSupportedProxy(ProxyApiItem item)
        {
            IPAddress parsedAddress;
            int parsedPort;

            return item != null &&
                IPAddress.TryParse(item.Ip, out parsedAddress) &&
                int.TryParse(item.Port, out parsedPort) &&
                parsedPort >= 1 &&
                parsedPort <= 65535 &&
                item.Protocols != null &&
                item.Protocols.Any(protocol =>
                    string.Equals(protocol, "http", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(protocol, "https", StringComparison.OrdinalIgnoreCase));
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
