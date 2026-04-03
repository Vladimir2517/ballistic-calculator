using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace RadarPlugin
{
    internal static class AlphaLauncher
    {
        [DllImport("user32.dll")]
        private static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        private const int SwRestore = 9;

        public static bool TryLaunchOrActivate(out string message)
        {
            message = null;

            try
            {
                var alphaPath = ResolveAlphaPath();
                if (string.IsNullOrWhiteSpace(alphaPath) || !File.Exists(alphaPath))
                {
                    message = "Не знайдено Alpha.exe.\n\n" +
                              "Вкажіть шлях через змінну середовища ALPHA_APP_PATH\n" +
                              "або покладіть Alpha в один із типових шляхів:\n" +
                              "- C:\\Projects\\Alpha\\Alpha.exe\n" +
                              "- C:\\Alpha\\Alpha.exe\n" +
                              "- %USERPROFILE%\\Documents\\Alpha\\Alpha.exe";
                    return false;
                }

                var processName = Path.GetFileNameWithoutExtension(alphaPath);
                var running = Process.GetProcessesByName(processName)
                    .FirstOrDefault(p =>
                    {
                        try
                        {
                            return string.Equals(p.MainModule.FileName, alphaPath, StringComparison.OrdinalIgnoreCase);
                        }
                        catch
                        {
                            return false;
                        }
                    });

                if (running != null)
                {
                    ActivateWindow(running);
                    message = "Alpha вже запущено.";
                    return true;
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = alphaPath,
                    Arguments = BuildArguments(),
                    WorkingDirectory = ResolveWorkingDirectory(alphaPath),
                    UseShellExecute = false
                };

                Process.Start(startInfo);
                message = "Alpha запущено.";
                return true;
            }
            catch (Exception ex)
            {
                message = "Не вдалося запустити Alpha:\n" + ex.Message;
                return false;
            }
        }

        private static string ResolveAlphaPath()
        {
            var envPath = (Environment.GetEnvironmentVariable("ALPHA_APP_PATH") ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(envPath))
            {
                return envPath;
            }

            var candidates = new[]
            {
                @"C:\Projects\Alpha\Alpha.exe",
                @"C:\Alpha\Alpha.exe",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Alpha", "Alpha.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Alpha", "Alpha.exe")
            };

            return candidates.FirstOrDefault(File.Exists);
        }

        private static string ResolveWorkingDirectory(string alphaPath)
        {
            var envWorkingDir = (Environment.GetEnvironmentVariable("ALPHA_WORKDIR") ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(envWorkingDir) && Directory.Exists(envWorkingDir))
            {
                return envWorkingDir;
            }

            return Path.GetDirectoryName(alphaPath) ?? Environment.CurrentDirectory;
        }

        private static string BuildArguments()
        {
            var rawArgs = (Environment.GetEnvironmentVariable("ALPHA_APP_ARGS") ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(rawArgs))
            {
                return rawArgs;
            }

            var builder = new StringBuilder();
            builder.Append("--map --connect-server");

            var serverUrl = (Environment.GetEnvironmentVariable("ALPHA_SERVER_URL") ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(serverUrl))
            {
                builder.Append(" --server ");
                builder.Append(Quote(serverUrl));
            }

            var profile = (Environment.GetEnvironmentVariable("ALPHA_PROFILE") ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(profile))
            {
                builder.Append(" --profile ");
                builder.Append(Quote(profile));
            }

            return builder.ToString();
        }

        private static string Quote(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        private static void ActivateWindow(Process process)
        {
            if (process == null)
            {
                return;
            }

            var handle = process.MainWindowHandle;
            if (handle == IntPtr.Zero)
            {
                return;
            }

            ShowWindowAsync(handle, SwRestore);
            SetForegroundWindow(handle);
        }
    }
}
