<!-- BẠN HÃY LÊN GITHUB.COM, BẤM NÚT EDIT HÌNH CÂY BÚT RỒI KÉO THẢ FILE cc.mp4 VÀO ĐÂY ĐỂ HIỂN THỊ VIDEO NHÉ -->

# BaoTools
<p>
  <img align="right" height="250" src="src/BaoToolsGui/icon.ico" />

  [Discord](https://discord.gg/baotools) • [Website](https://lua.tools) • [Git Mirror](https://git.lua.tools/baotools)
  
  A Windows desktop client for managing Steam manifest/lua configurations, built with WPF on .NET 8.
    
  BaoTools browses and installs manifest sources, edits stplug-in lua files (depot pinning,
  per-depot enable/disable), manages unlocker modes, and injects a companion plugin into Steam's
  store pages.
  
  It ships fully translated in 29 languages and auto-updates via Velopack.
  <br><sub>Found a translation error? Tell us about it over on [Discord](https://discord.gg/baotools)</sub>
</p>

## Requirements

- Windows 10/11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (the released installer bundles a
  check for the .NET 8 **Desktop Runtime** and installs it if missing; [building from source](https://github.com/madoiscool/BaoTools/blob/main/CONTRIBUTING.md#building-from-source--developing) needs
  the full SDK

## Installation
You can find release builds on the [baotools website](https://lua.tools/app) or in the [releases](https://github.com/madoiscool/BaoTools/releases/latest) tab. 

## Credits / Adjacent software

- [Millennium](https://steambrew.app/): the Steam plugin framework whose injection API this app
  polyfills when Millennium isn't installed
- [Velopack](https://velopack.io/): installer and auto-update framework

## Licence

MIT. See [LICENSE](LICENSE).
