# Darksiders 2 Reborn PS3
> [!IMPORTANT]
> This project brings back the Online functions and the "Pay It Forward" trophy for Darksiders II.

A cross-platform custom server and launcher to restore the Platinum trophy and Online functionality for **Darksiders2 2** on the PlayStation 3.

|             BEFORE: Server Error              |             AFTER: Server Online             |
|:---------------------------------------------:|:--------------------------------------------:|
| <img src="Assets/ds2-before.png" width="700"> | <img src="Assets/ds2-after.png" width="700"> |

These screenshots are from Darksiders 2 using RPCS3.

## Download
**Please leave a star ⭐ if you found this repository helpful!**  
📅 **29/09/2026:** Release 1.0.0 is here!

You can download the compiled versions for your operating system here:  
**[Download Latest Release.](https://github.com/adamstark1/inFAMOUS-Reborn-PS3/releases)**

| UI Screenshot |
| :---: |
| <img src="Assets/darksiders2-reborn-ui.png" width="500"> |

## How to Run
> [!NOTE]
> The application was tested on Windows 11, macOS Tahoe and Ubuntu 22.04.
### Windows
1. Extract the downloaded `.zip` file or compile it yourself with `Compilers/build_windows.sh` (use [Git Bash](https://git-scm.com/install/) terminal).
2. Run `Darksiders2Reborn.Launcher.exe`.

### macOS
1. Extract the downloaded `.zip` file or compile it yourself with `Compilers/build_mac_silicon.sh` or `Compilers/build_mac_intel.sh`.
2. Double-click the `Darksiders II Reborn.app` bundle. 

### Linux
1. Extract the downloaded `.zip` file or compile it yourself with `Compilers/build_linux.sh` and run `install.sh`.
2. Run `Darksiders II Reborn` from application menu. 


## Setup Instructions

1. TBD

## Sponsors and Contributors
I'd like to give a huge thank you to everyone who helped with this repository. This projects improves overtime thanks to you!

❤️ **Sponsors:** [@ffv2-droid](https://github.com/ffv2-droid)  
🛠️ **Troubleshooting:** -

You can support my coding journey and revival projects on [GitHub Sponsors](https://github.com/sponsors/adamstark1).

## Tips
🔓 Fastest way to jailbreak your console:
- TBD

## Troubleshooting

>**"Port 53 or Port 80 in use / Server fails to start"**  
If the built-in port clearer fails, manually kill the conflicting processes:
* **macOS:** Open Terminal and run `sudo kill -9 $(sudo lsof -t -i :80)`.
* **Linux:** Open Terminal and run `sudo fuser -k 80/tcp 53/udp`.
* **Windows:** Open Command Prompt as Administrator and run `for /f "tokens=5" %a in ('netstat -aon ^| findstr :80 ^| findstr LISTENING') do taskkill /f /pid %a`.

Executing one of these commands will free up Port 80, and the Launcher will function properly.

>**"Permission denied for compiler .sh scripts"**  
Adjust permissions for these files manually:
* **macOS (Intel):** use `chmod +x build_mac_intel.sh` in Terminal.
* **macOS (Silicon):** use `chmod +x build_mac_silicon.sh` in Terminal.
* **Linux:** use `chmod +x build_linux.sh` in Terminal.
* **Windows:** use `chmod +x build_windows.sh` in Git Bash terminal.

>**"ERROR: Read-only file system : '/private/var/folders/.../d/Missions'"**  
This error can occur on macOS (Intel and Silicon):

Pressing the **Download & Extract** button may fail with an error due to Apple's [Gatekeeper](https://en.wikipedia.org/wiki/Gatekeeper_(macOS)) security feature. This can be fixed by moving the app bundle:
* Close the app first (`Darksiders II Reborn.app`) if it's already running.
* **Move** (*not copy*) the app to another location (e.g. `Desktop`) with a drag and drop.
* **Move** the app back to the previous location with a drag and drop.
* Reopen the app by double-clicking it and proceed as normal.

## Tech Stack

![C#](https://img.shields.io/badge/C%23-239120?style=for-the-badge&logo=c-sharp&logoColor=white)
![.NET Core](https://img.shields.io/badge/.NET_8-5C2D91?style=for-the-badge&logo=.net&logoColor=white)
![Avalonia UI](https://img.shields.io/badge/Avalonia_UI-844FBA?style=for-the-badge)
![macOS](https://img.shields.io/badge/macOS-000000?style=for-the-badge&logo=apple&logoColor=white)
![Windows](https://img.shields.io/badge/Windows-0078D6?style=for-the-badge&logo=windows&logoColor=white)

* **Backend:** ASP.NET Core (Kestrel web server)
* **DNS Routing:** Custom DNS proxy handling UDP port 53 traffic to reroute PSN URLs to the local host.