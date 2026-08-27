
# BaoTools
<p>
  <img align="right" height="250" alt="hachimi" src="https://github.com/user-attachments/assets/33aa8bb5-df11-4b33-b5d6-9a8350495c8b" />

  [Discord](https://discord.gg/baotools) • [Website](https://DevBaor.github.io/BaoTools_1005) • [Git Mirror](https://github.com/DevBaor/BaoTools_1005)
  
  A Windows desktop client for managing Steam manifest/lua configurations, built with WPF on .NET 8.
    
  BaoTools browses and installs manifest sources, edits stplug-in lua files (depot pinning,
  per-depot enable/disable), manages unlocker modes, and injects a companion plugin into Steam's
  store pages.
  
  It ships fully translated in 29 languages and auto-updates via Velopack.
  <br><sub>Found a translation error? Tell us about it over on [Discord](https://discord.gg/baotools)</sub>
</p>

## Statistics & Download
<div>
  <a href="https://github.com/DevBaor/BaoTools_1005/releases/latest/download/BaoTools_Setup.exe">
    <img src="https://img.shields.io/github/downloads/DevBaor/BaoTools_1005/BaoTools_Setup.exe?displayAssetName=true&style=for-the-badge" />
  </a>
  <a href="https://github.com/DevBaor/BaoTools_1005/releases/latest/download/BaoTools.exe">
    <img src="https://img.shields.io/github/downloads/DevBaor/BaoTools_1005/BaoTools.exe?displayAssetName=true&style=for-the-badge" />
  </a>
</div>

## Requirements

- Windows 10/11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (the released installer bundles a
  check for the .NET 8 **Desktop Runtime** and installs it if missing; [building from source](https://github.com/DevBaor/BaoTools_1005/blob/main/CONTRIBUTING.md#building-from-source--developing) needs
  the full SDK

## Installation
You can find release builds on the [baotools website](https://DevBaor.github.io/BaoTools_1005) or in the [releases](https://github.com/DevBaor/BaoTools_1005/releases/latest) tab. 

## Credits / Adjacent software

- [Millennium](https://steambrew.app/): the Steam plugin framework whose injection API this app
  polyfills when Millennium isn't installed
- [Velopack](https://velopack.io/): installer and auto-update framework

## Licence

MIT. See [LICENSE](LICENSE).
