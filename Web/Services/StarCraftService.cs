using System.Diagnostics;
using Microsoft.Win32;
using Shared;

namespace Web.Services;

public class StarCraftService
{
    private readonly MyStarcraftBot _starcraftBot;

    public StarCraftService(MyStarcraftBot starcraftBot)
    {
        _starcraftBot = starcraftBot;
    }

    private Process? _chaosLauncherProcess;
    private static readonly string _starcraftBasePath = Path.Combine(
        Directory.GetCurrentDirectory(),
        "..",
        "Starcraft"
    );

    public List<string> GetMapOptions()
    {
        var mapsDirectory = Path.Combine(_starcraftBasePath, "Maps");

        if (!Directory.Exists(mapsDirectory))
        {
            Console.WriteLine($"Warning: Maps directory not found at {mapsDirectory}");
            return new List<string>();
        }

        var mapFiles = Directory
            .GetFiles(mapsDirectory, "*.sc?", SearchOption.TopDirectoryOnly)
            .Select(path => $"maps/{Path.GetFileName(path)}")
            .OrderBy(name => name)
            .ToList();

        return mapFiles;
    }

    public void ConfigureChaosLauncher()
    {
        if (!OperatingSystem.IsWindows())
        {
            Console.WriteLine("Registry configuration is only supported on Windows.");
            return;
        }

        try
        {
            // Configure Launcher settings
            using (
                var launcherKey = Registry.CurrentUser.CreateSubKey(
                    @"Software\Chaoslauncher\Launcher"
                )
            )
            {
                if (launcherKey != null)
                {
                    launcherKey.SetValue(
                        "GameVersion",
                        "Starcraft 1.16.1",
                        RegistryValueKind.String
                    );
                    launcherKey.SetValue("Width", 100, RegistryValueKind.DWord);
                    launcherKey.SetValue("Height", 100, RegistryValueKind.DWord);
                    launcherKey.SetValue("StartMinimized", 0, RegistryValueKind.DWord);
                    launcherKey.SetValue("MinimizeOnRun", 0, RegistryValueKind.DWord);
                    launcherKey.SetValue("RunScOnStartup", 1, RegistryValueKind.DWord);
                    launcherKey.SetValue("AutoUpdate", 0, RegistryValueKind.DWord);
                    launcherKey.SetValue("WarnNoAdmin", 0, RegistryValueKind.DWord);
                    Console.WriteLine("Chaoslauncher Launcher settings configured.");
                }
            }

            // Configure enabled plugins
            using (
                var pluginsKey = Registry.CurrentUser.CreateSubKey(
                    @"Software\Chaoslauncher\PluginsEnabled"
                )
            )
            {
                if (pluginsKey != null)
                {
                    pluginsKey.SetValue(
                        "BWAPI 4.4.0 Injector [RELEASE]",
                        1,
                        RegistryValueKind.DWord
                    );
                    pluginsKey.SetValue("W-MODE 1.02", 1, RegistryValueKind.DWord);
                    Console.WriteLine("Chaoslauncher plugins configured.");
                }
            }

            // Configure StarCraft install path (requires admin privileges)
            try
            {
                var installPath = Path.GetFullPath(_starcraftBasePath);
                using (
                    var starcraftKey = Registry.LocalMachine.CreateSubKey(
                        @"SOFTWARE\WOW6432Node\Blizzard Entertainment\Starcraft"
                    )
                )
                {
                    if (starcraftKey != null)
                    {
                        starcraftKey.SetValue("InstallPath", installPath, RegistryValueKind.String);
                        Console.WriteLine($"StarCraft install path configured to: {installPath}");
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                Console.WriteLine(
                    "Warning: Unable to set StarCraft install path. Administrator privileges required."
                );
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error configuring Chaoslauncher registry: {ex.Message}");
        }
    }

    public void StartStarCraft(GamePreferences gamePreferences)
    {
        BwapiConfigService.ConfigureBwapiIni(gamePreferences);

        var startInfo = new ProcessStartInfo
        {
            FileName = Path.Combine(
                _starcraftBasePath,
                "BWAPI",
                "Chaoslauncher",
                "Chaoslauncher - MultiInstance.exe"
            ),
            WorkingDirectory = Path.Combine(_starcraftBasePath, "BWAPI", "Chaoslauncher"),
            UseShellExecute = false,
            Verb = "",
            CreateNoWindow = false,
        };

        _chaosLauncherProcess = Process.Start(startInfo);
    }

    private void CloseStarCraftWindow()
    {
        Console.WriteLine("Looking for StarCraft process...");
        try
        {
            // Use taskkill command which can force-terminate processes
            var processInfo = new ProcessStartInfo
            {
                FileName = "taskkill",
                Arguments = "/F /IM StarCraft.exe",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            var process = Process.Start(processInfo);
            if (process != null)
            {
                process.WaitForExit();
                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();

                Console.WriteLine($"taskkill output: {output}");
                if (!string.IsNullOrEmpty(error))
                {
                    Console.WriteLine($"taskkill error: {error}");
                }

                process.Dispose();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error using taskkill: {ex.Message}");
        }
    }

    public void StopChaosLauncher()
    {
        Console.WriteLine("Stopping chaoslauncherprocess");
        if (_chaosLauncherProcess != null && !_chaosLauncherProcess.HasExited)
        {
            _chaosLauncherProcess.Kill();
            _chaosLauncherProcess.WaitForExit();
            _chaosLauncherProcess?.Dispose();
            _chaosLauncherProcess = null;
            Console.WriteLine("Chaoslauncher process stopped.");
        }
    }

    public void StopAndReset()
    {
        Console.WriteLine("Disposing StarCraftService...");
        CloseStarCraftWindow();
        StopChaosLauncher();
    }
}
