using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Net;
using System.Threading.Tasks;
using Avalonia.Threading;
using System.Threading;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Darksiders2Reborn.Launcher.Services;

namespace Darksiders2Reborn.Launcher.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty] private string _currentStepText = "";
    [ObservableProperty] private bool _isOptional = false;
    [ObservableProperty] private string _stepDescription = "";
    [ObservableProperty] private string _primaryButtonText = "";
    [ObservableProperty] private string _terminalOutput = "Darksiders II Reborn Setup initialized...\n";
    [ObservableProperty] private bool _isReadmeLinkVisible = true;

    private int _step;
    private Process? _serverProcess;

    public MainWindowViewModel()
    {
        SetStep(1);
    }

    private string GetLocalIpAddress()
    {
        try
        {
            using Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
            socket.Connect("8.8.8.8", 65530);
            IPEndPoint? endPoint = socket.LocalEndPoint as IPEndPoint;
            return endPoint?.Address.ToString() ?? "127.0.0.1";
        }
        catch { return "127.0.0.1 (Offline)"; }
    }

    private void Log(string message)
    {
        Dispatcher.UIThread.Post(() => TerminalOutput += $"[{DateTime.Now:HH:mm:ss}] {message}\n");
    }

    [RelayCommand]
    private async Task CopyLogAsync()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow?.Clipboard != null)
        {
            await desktop.MainWindow.Clipboard.SetTextAsync(TerminalOutput);
        }
    }

    [RelayCommand]
    private async Task ExecutePrimaryActionAsync()
    {
        try
        {
            switch (_step)
            {
                case 1:
                    await KillPortsAsync();
                    SetStep(2);
                    break;
                case 2:
                    if (await PatchEbootAsync()) SetStep(3);
                    break;
                case 3:
                    StartServer();
                    break;
                case 4:
                    StopServer();
                    break;
            }
        }
        catch (Exception ex) { Log($"ERROR: {ex.Message}"); }
    }

    [RelayCommand]
    private void SkipStep()
    {
        SetStep(_step + 1);
    }

    [RelayCommand]
    private void OpenReadme()
    {
        Process.Start(new ProcessStartInfo { FileName = "https://github.com/adamstark1/Darksiders2-Reborn-PS3", UseShellExecute = true });
    }

    private void SetStep(int step)
    {
        _step = step;
        switch (_step)
        {
            case 1:
                CurrentStepText = "Step 1/3: Port Clearance";
                IsOptional = true;
                StepDescription = "Free up Port 53 (DNS), Port 80 (HTTP) and Port 443 (HTTPS).";
                PrimaryButtonText = "Kill Conflicting Ports";
                break;
            case 2:
                CurrentStepText = "Step 2/3: Patch EBOOT";
                IsOptional = true;
                StepDescription = "Select your decrypted EBOOT.elf (RPCS3: Utilities > Decrypt PS3 Binaries).";
                PrimaryButtonText = "Select & Patch EBOOT";
                break;
            case 3:
                CurrentStepText = "Step 3/3: Console DNS";
                IsOptional = false;
                IsReadmeLinkVisible = true;
                StepDescription = $"Go to your PS3 Network Settings and set Primary DNS to:\n\n{GetLocalIpAddress()}";
                PrimaryButtonText = "Start Server";
                break;
            case 4:
                CurrentStepText = "Server is Running";
                IsOptional = false;
                IsReadmeLinkVisible = false;
                StepDescription = "Server is active. Monitoring PS3 requests in the terminal below.";
                PrimaryButtonText = "Stop Server";
                break;
        }
    }

    private async Task<bool> PatchEbootAsync()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop || desktop.MainWindow is null)
        {
            return false;
        }

        var files = await desktop.MainWindow.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select the decrypted Darksiders II EBOOT (ELF)",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("PS3 EBOOT") { Patterns = new[] { "*.elf", "*.ELF", "*.bin", "*.BIN" } },
                FilePickerFileTypes.All
            }
        });

        if (files.Count == 0)
        {
            Log("No file selected.");
            return false;
        }

        string? path = files[0].TryGetLocalPath();
        if (path is null)
        {
            Log("The selected file is not a local file.");
            return false;
        }

        Log($"Patching {Path.GetFileName(path)}...");
        var result = await Task.Run(() => EbootPatcherService.Patch(path));
        Log(result.Message);
        if (result.OutputPath is not null) Log($"Patched file: {result.OutputPath}");

        return result.Status == EbootPatchStatus.Patched || result.Status == EbootPatchStatus.AlreadyPatched;
    }

    private void StartServer()
    {
        Log("Starting backend server...");
        string baseDir = AppContext.BaseDirectory;
        string exeName = OperatingSystem.IsWindows() ? "Darksiders2Reborn.exe" : "Darksiders2Reborn";

        string backendPath = Path.GetFullPath(Path.Combine(baseDir, "../Backend", exeName));

        if (!File.Exists(backendPath))
        {
            backendPath = Path.Combine(baseDir, exeName);

            if (!File.Exists(backendPath))
            {
                string devDebugPath = Path.GetFullPath(Path.Combine(baseDir, $"../../../../Darksiders2Reborn/bin/Debug/net8.0/{exeName}"));
                string devReleasePath = Path.GetFullPath(Path.Combine(baseDir, $"../../../../Darksiders2Reborn/bin/Release/net8.0/{exeName}"));
                string rootDebugPath = Path.GetFullPath(Path.Combine(baseDir, $"../../../../bin/Debug/net8.0/{exeName}"));
                string rootReleasePath = Path.GetFullPath(Path.Combine(baseDir, $"../../../../bin/Release/net8.0/{exeName}"));

                if (File.Exists(devDebugPath)) backendPath = devDebugPath;
                else if (File.Exists(devReleasePath)) backendPath = devReleasePath;
                else if (File.Exists(rootDebugPath)) backendPath = rootDebugPath;
                else if (File.Exists(rootReleasePath)) backendPath = rootReleasePath;
                else
                {
                    string dllPath = Path.Combine(baseDir, "Darksiders2Reborn.dll");
                    if (File.Exists(dllPath)) backendPath = dllPath;
                }
            }
        }

        if (!File.Exists(backendPath))
        {
            Log("Backend executable not found. Build the backend project first.");
            return;
        }

        var psi = new ProcessStartInfo
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(backendPath)
        };

        if (backendPath.EndsWith(".dll"))
        {
            psi.FileName = "dotnet";
            psi.Arguments = $"\"{backendPath}\"";
        }
        else
        {
            psi.FileName = backendPath;
        }

        try
        {
            _serverProcess = new Process { StartInfo = psi, EnableRaisingEvents = true };
            _serverProcess.OutputDataReceived += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(e.Data)) return;
                string line = e.Data.TrimStart();

                if (line.Contains("Microsoft.AspNetCore") ||
                    line.Contains("Microsoft.Hosting") ||
                    line.Contains("Now listening on:") ||
                    line.Contains("Application started.") ||
                    line.Contains("Hosting environment:") ||
                    line.Contains("Content root path:") ||
                    line.Contains("Overriding address(es)") ||
                    line.Contains("info: Darksiders2Reborn") ||
                    line.Contains("Building...") ||
                    line.Contains("warning CS") ||
                    line.Contains("warn: Microsoft") ||
                    line.Contains("lacks the subjectAlternativeName"))
                {
                    return;
                }

                if (!string.IsNullOrEmpty(line)) Log(line);
            };
            _serverProcess.ErrorDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) Log($"ERROR: {e.Data}"); };
            _serverProcess.Exited += (s, e) => { Log("Server process stopped."); Dispatcher.UIThread.Post(() => { if (_step == 4) SetStep(3); }); };

            _serverProcess.Start();
            _serverProcess.BeginOutputReadLine();
            _serverProcess.BeginErrorReadLine();
            SetStep(4);
        }
        catch (Exception ex) { Log($"Failed to launch server: {ex.Message}"); }
    }

    private void StopServer()
    {
        if (_serverProcess != null && !_serverProcess.HasExited)
        {
            Log("Stopping backend server...");
            try { _serverProcess.Kill(true); } catch { }
            _serverProcess.Dispose();
            _serverProcess = null;
            Log("Please leave a star ⭐ on this repository!");
        }
        SetStep(3);
    }

    private async Task KillPortsAsync()
    {
        Log("Clearing Port 53, Port 80 and Port 443...");
        if (OperatingSystem.IsMacOS())
        {
            await Process.Start(new ProcessStartInfo { FileName = "killall", Arguments = "-HUP mDNSResponder", RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true })!.WaitForExitAsync();
            await Process.Start(new ProcessStartInfo { FileName = "killall", Arguments = "httpd", RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true })!.WaitForExitAsync();
        }
        else if (OperatingSystem.IsWindows())
        {
            await Process.Start(new ProcessStartInfo { FileName = "net", Arguments = "stop sharedaccess", RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true })!.WaitForExitAsync();
        }
        Log("Ports cleared.");
    }
}