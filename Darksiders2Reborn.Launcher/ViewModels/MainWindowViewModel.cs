using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Sockets;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using System.Collections.Generic;

namespace Darksiders2Reborn.Launcher.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private const string GitHubClientId = "Ov23liuE2dntdLRDsJNi";

    [ObservableProperty] private string _currentStepText = "";
    [ObservableProperty] private bool _isOptional = false;
    [ObservableProperty] private string _stepDescription = "";
    [ObservableProperty] private string _primaryButtonText = "";
    [ObservableProperty] private string _secondaryButtonText = "";
    [ObservableProperty] private string _terminalOutput = "Darksiders II Reborn Setup initialized...\n";
    [ObservableProperty] private bool _isReadmeLinkVisible = true;
    
    [ObservableProperty] private bool _isPrimaryButtonVisible = true;
    [ObservableProperty] private bool _isSkipButtonVisible = true;
    [ObservableProperty] private bool _isPlatformButtonsVisible = false;
    [ObservableProperty] private bool _isPrimaryEnabled = true;

    [ObservableProperty] private bool _isWaiting;
    [ObservableProperty] private double _waitProgress;
    [ObservableProperty] private string _waitRemainingText = "";

    [ObservableProperty] private bool _isGithubPopupVisible;
    [ObservableProperty] private string _githubUserCode = "";
    [ObservableProperty] private string _githubVerificationUri = "";
    [ObservableProperty] private string _githubAuthMessage = "Waiting for authorization...";

    private bool _isOfwSelected = true;
    private bool _isRpcs3Selected = false;

    private int _step;
    private Process? _serverProcess;
    private CancellationTokenSource? _githubPollingCts;
    private CancellationTokenSource? _waitCts;
    private string? _cachedAccessToken;
    private string? _cachedDeviceCode;
    private int _cachedInterval;
    private DateTime _codeExpiration;

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
                case 3:
                    await ProcessUnlockAsync();
                    break;
                case 4:
                    StopServer();
                    break;
            }
        }
        catch (Exception ex) { Log($"ERROR: {ex.Message}"); }
    }

    [RelayCommand]
    private void SelectOfw()
    {
        _isOfwSelected = true;
        _isRpcs3Selected = false;
        SetStep(3);
    }

    [RelayCommand]
    private void SelectRpcs3()
    {
        _isOfwSelected = false;
        _isRpcs3Selected = true;
        SetStep(3);
    }

    [RelayCommand]
    private void SkipStep()
    {
        if (IsWaiting)
        {
            Log("[INFO] Server startup cancelled.");
            _waitCts?.Cancel();
            IsWaiting = false;
            SetStep(3);
        }
        else
        {
            if (_step == 1) SetStep(2);
            else if (_step == 3) SetStep(2);
        }
    }

    [RelayCommand]
    private void OpenReadme()
    {
        Process.Start(new ProcessStartInfo { FileName = "https://github.com/adamstark1/Darksiders2-Reborn-PS3", UseShellExecute = true });
    }

    [RelayCommand]
    private async Task CopyGithubCodeAsync()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow?.Clipboard != null)
        {
            await desktop.MainWindow.Clipboard.SetTextAsync(GithubUserCode);
            GithubAuthMessage = "Code copied to clipboard!";
        }
    }

    [RelayCommand]
    private void OpenGithubAuth()
    {
        if (!string.IsNullOrEmpty(GithubVerificationUri))
        {
            Process.Start(new ProcessStartInfo { FileName = GithubVerificationUri, UseShellExecute = true });
        }
    }

    [RelayCommand]
    private void SkipGithubAuth()
    {
        _githubPollingCts?.Cancel();
        IsGithubPopupVisible = false;
        Log("GitHub authorization skipped.");
    }

    private void SetStep(int step)
    {
        _step = step;
        switch (_step)
        {
            case 1:
                CurrentStepText = "Step 1/3: Port Clearance";
                IsOptional = true;
                IsPrimaryButtonVisible = true;
                IsSkipButtonVisible = true;
                IsPlatformButtonsVisible = false;
                IsReadmeLinkVisible = true;
                IsPrimaryEnabled = true;
                SecondaryButtonText = "Skip";
                StepDescription = "Free up Port 53 (DNS), Port 443 and Port 8443 (HTTPS).";
                PrimaryButtonText = "Kill Conflicting Ports";
                break;
            case 2:
                CurrentStepText = "Step 2/3: Select Platform";
                IsOptional = false;
                IsPrimaryButtonVisible = false;
                IsSkipButtonVisible = false;
                IsPlatformButtonsVisible = true;
                IsReadmeLinkVisible = true;
                StepDescription = "Choose your platform to start the backend server.";
                break;
            case 3:
                CurrentStepText = "Step 3/3: Start Server";
                IsOptional = false;
                IsPrimaryButtonVisible = true;
                IsSkipButtonVisible = true;
                IsPlatformButtonsVisible = false;
                IsReadmeLinkVisible = true;
                IsPrimaryEnabled = true;
                SecondaryButtonText = "Back";
                if (_isOfwSelected)
                {
                    StepDescription = $"Go to your PS3 Network Settings and set Primary DNS to:\n\n{GetLocalIpAddress()}";
                }
                else
                {
                    StepDescription = "Run patch_eboot.sh on your EBOOT.elf, place it in your RPCS3 game folder, and start the emulator.";
                }
                PrimaryButtonText = "Start Server";
                break;
            case 4:
                CurrentStepText = "Server is Running";
                IsOptional = false;
                IsPrimaryButtonVisible = true;
                IsSkipButtonVisible = false;
                IsPlatformButtonsVisible = false;
                IsReadmeLinkVisible = false;
                IsPrimaryEnabled = true;
                StepDescription = $"Server is active. Monitoring {(_isOfwSelected ? "PS3 Console" : "RPCS3")} requests in the terminal below.";
                PrimaryButtonText = "Stop Server";
                break;
        }
    }

    private async Task ProcessUnlockAsync()
    {
        IsPrimaryEnabled = false;
        Log("Initiating GitHub Device Flow authorization...");
        bool isStarred = await AuthenticateAndCheckStarAsync();

        if (isStarred)
        {
            Log("[SUCCESS] Star ⭐ verified! Starting server immediately...");
            StartServerProcess();
        }
        else
        {
            Log("[WARNING] Star missing or bypassed. Starting timer...");
            await ApplyDelayAndProceedAsync();
        }
    }

    private async Task ApplyDelayAndProceedAsync()
    {
        IsWaiting = true;
        SecondaryButtonText = "Cancel";
        _waitCts = new CancellationTokenSource();

        // Matematikai elrejtés: 150 << 2 = 600 másodperc = 10 perc
        TimeSpan totalWait = TimeSpan.FromSeconds(150 << 2);
        DateTime startTime = DateTime.UtcNow;
        DateTime endTime = startTime.Add(totalWait);

        try
        {
            while (DateTime.UtcNow < endTime)
            {
                _waitCts.Token.ThrowIfCancellationRequested();

                TimeSpan remaining = endTime - DateTime.UtcNow;
                WaitRemainingText = $"Time remaining: {(int)remaining.TotalMinutes:D2}:{remaining.Seconds:D2}";
                WaitProgress = 100.0 - (remaining.TotalSeconds / totalWait.TotalSeconds * 100.0);

                await Task.Delay(1000, _waitCts.Token);
            }

            IsWaiting = false;
            StartServerProcess();
        }
        catch (OperationCanceledException)
        {
            IsPrimaryEnabled = true;
        }
    }

    private async Task<bool> AuthenticateAndCheckStarAsync()
    {
        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Accept", "application/json");

            if (!string.IsNullOrEmpty(_cachedAccessToken))
            {
                using var authClient = new HttpClient();
                authClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_cachedAccessToken}");
                authClient.DefaultRequestHeaders.Add("User-Agent", "Darksiders2Reborn-App");

                var starResp = await authClient.GetAsync("https://api.github.com/user/starred/adamstark1/Darksiders2-Reborn-PS3");
                
                if (starResp.StatusCode == System.Net.HttpStatusCode.NoContent)
                {
                    return true;
                }
                if (starResp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    _cachedAccessToken = null;
                }
                else
                {
                    return false;
                }
            }

            if (string.IsNullOrEmpty(_cachedDeviceCode) || DateTime.UtcNow > _codeExpiration)
            {
                var requestData = new FormUrlEncodedContent(new[] { new KeyValuePair<string, string>("client_id", GitHubClientId) });
                var response = await client.PostAsync("https://github.com/login/device/code", requestData);
                if (!response.IsSuccessStatusCode) return false;

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);

                _cachedDeviceCode = doc.RootElement.GetProperty("device_code").GetString()!;
                GithubUserCode = doc.RootElement.GetProperty("user_code").GetString()!;
                GithubVerificationUri = doc.RootElement.GetProperty("verification_uri").GetString()!;
                _cachedInterval = doc.RootElement.GetProperty("interval").GetInt32();
                
                int expiresIn = doc.RootElement.GetProperty("expires_in").GetInt32();
                _codeExpiration = DateTime.UtcNow.AddSeconds(expiresIn - 60);
            }

            GithubAuthMessage = "Enter code in the opened browser window...";
            IsGithubPopupVisible = true;

            _githubPollingCts = new CancellationTokenSource();
            string? accessToken = null;

            while (!_githubPollingCts.IsCancellationRequested && DateTime.UtcNow < _codeExpiration)
            {
                await Task.Delay(_cachedInterval * 1000, _githubPollingCts.Token);

                var pollData = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("client_id", GitHubClientId),
                    new KeyValuePair<string, string>("device_code", _cachedDeviceCode),
                    new KeyValuePair<string, string>("grant_type", "urn:ietf:params:oauth:grant-type:device_code")
                });

                var pollResp = await client.PostAsync("https://github.com/login/oauth/access_token", pollData);
                var pollJson = await pollResp.Content.ReadAsStringAsync();

                if (pollJson.Contains("access_token"))
                {
                    using var pollDoc = JsonDocument.Parse(pollJson);
                    accessToken = pollDoc.RootElement.GetProperty("access_token").GetString();
                    break;
                }
                if (pollJson.Contains("authorization_pending")) continue;
                if (pollJson.Contains("slow_down"))
                {
                    _cachedInterval += 5;
                    continue;
                }
                
                _cachedDeviceCode = null;
                break;
            }

            IsGithubPopupVisible = false;

            if (string.IsNullOrEmpty(accessToken)) return false;

            _cachedAccessToken = accessToken;

            using var authClient2 = new HttpClient();
            authClient2.DefaultRequestHeaders.Add("Authorization", $"Bearer {_cachedAccessToken}");
            authClient2.DefaultRequestHeaders.Add("User-Agent", "Darksiders2Reborn-App");

            var starResp2 = await authClient2.GetAsync("https://api.github.com/user/starred/adamstark1/Darksiders2-Reborn-PS3");
            return starResp2.StatusCode == System.Net.HttpStatusCode.NoContent;
        }
        catch (TaskCanceledException)
        {
            return false;
        }
        catch (Exception ex)
        {
            Log($"[AUTH ERROR] {ex.Message}");
            IsGithubPopupVisible = false;
            return false;
        }
    }

    private void StartServerProcess()
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
            IsPrimaryEnabled = true;
            return;
        }

        string arguments = _isRpcs3Selected ? "--rpcs3" : "";

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
            psi.Arguments = $"\"{backendPath}\" {arguments}".Trim();
        }
        else
        {
            psi.FileName = backendPath;
            psi.Arguments = arguments;
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
        catch (Exception ex) 
        { 
            Log($"Failed to launch server: {ex.Message}");
            IsPrimaryEnabled = true;
        }
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
        Log("Clearing Port 53, Port 443 and Port 8443...");
        if (OperatingSystem.IsMacOS())
        {
            await Process.Start(new ProcessStartInfo { FileName = "killall", Arguments = "-HUP mDNSResponder", RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true })!.WaitForExitAsync();
        }
        else if (OperatingSystem.IsWindows())
        {
            await Process.Start(new ProcessStartInfo { FileName = "net", Arguments = "stop sharedaccess", RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true })!.WaitForExitAsync();
            await Process.Start(new ProcessStartInfo { FileName = "net", Arguments = "stop http /y", RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true })!.WaitForExitAsync();
        }
        Log("Ports cleared.");
    }
}