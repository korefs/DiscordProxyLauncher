using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DiscordProxyLauncher
{
    internal static class DiscordService
    {
        public static Task CloseAsync()
        {
            return Task.Run((Action)CloseAllDiscordProcesses);
        }

        private static void CloseAllDiscordProcesses()
        {
            // Repete algumas vezes porque o Discord possui vários subprocessos
            // e algum deles pode reaparecer por um instante durante o encerramento.
            for (int attempt = 0; attempt < 3; attempt++)
            {
                Process[] processes = Process.GetProcessesByName("Discord");

                if (processes.Length == 0)
                    return;

                foreach (Process process in processes)
                {
                    try
                    {
                        if (!process.HasExited)
                        {
                            process.Kill();
                            process.WaitForExit(3000);
                        }
                    }
                    catch
                    {
                        // Um subprocesso pode desaparecer enquanto a lista é percorrida.
                    }
                    finally
                    {
                        process.Dispose();
                    }
                }

                System.Threading.Thread.Sleep(250);
            }
        }

        public static void Start()
        {
            string localAppData = Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData);

            string discordDirectory = Path.Combine(localAppData, "Discord");
            string updater = Path.Combine(discordDirectory, "Update.exe");

            if (File.Exists(updater))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = updater,
                    Arguments = "--processStart Discord.exe",
                    UseShellExecute = true,
                    WorkingDirectory = discordDirectory
                });

                return;
            }

            if (Directory.Exists(discordDirectory))
            {
                string executable = Directory
                    .EnumerateDirectories(discordDirectory, "app-*")
                    .Select(path => Path.Combine(path, "Discord.exe"))
                    .Where(File.Exists)
                    .OrderByDescending(File.GetLastWriteTimeUtc)
                    .FirstOrDefault();

                if (!string.IsNullOrEmpty(executable))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = executable,
                        UseShellExecute = true,
                        WorkingDirectory = Path.GetDirectoryName(executable)
                    });

                    return;
                }
            }

            // Fallback para instalações que registram discord:// no Windows.
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "discord://",
                    UseShellExecute = true
                });
            }
            catch
            {
                throw new FileNotFoundException(
                    "Não encontrei uma instalação do Discord neste computador.");
            }
        }

        public static async Task WaitUntilRunningAsync(TimeSpan timeout)
        {
            DateTime deadline = DateTime.UtcNow.Add(timeout);

            while (DateTime.UtcNow < deadline)
            {
                Process[] processes = Process.GetProcessesByName("Discord");
                bool running = processes.Length > 0;

                foreach (Process process in processes)
                    process.Dispose();

                if (running)
                    return;

                await Task.Delay(500);
            }

            throw new TimeoutException(
                "O Discord não iniciou dentro do tempo esperado.");
        }
    }
}
