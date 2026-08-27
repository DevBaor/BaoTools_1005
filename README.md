<p align="center">
  <video height="336" src="cc.mp4" controls="controls"></video>
</p>

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

## Statistics
<div>
  <img src="https://img.shields.io/github/downloads/madoiscool/baotools/BaoTools-win-Setup.exe?displayAssetName=true&style=for-the-badge" />
  <img src="https://img.shields.io/github/downloads/madoiscool/baotools/BaoTools-win-Portable.zip?displayAssetName=true&style=for-the-badge" />
</div>

<a href="https://www.star-history.com/?repos=madoiscool%2Fbaotools&type=date&legend=top-left">
 <picture>
   <source media="(prefers-color-scheme: dark)" srcset="https://api.star-history.com/chart?repos=madoiscool/baotools&type=date&theme=dark&legend=top-left&sealed_token=1SX6CDP2N0Emx5IbGfQmEz4TxM11iXtfLKL9K1utRzINJPEDv55f5XEYjliBUB1No6wbcWbMs-cSzO65OC7kAlMLAHJXjqmDoeRCM6hVtW9xd7fyg8cr2DG4gATwkgym1JvgPs4_PeGi6XMAm7_2CVXU9UxRLBW_GP4-Qmd3-AosSRCM1Nkm7dEr2_Ut" />
   <source media="(prefers-color-scheme: light)" srcset="https://api.star-history.com/chart?repos=madoiscool/baotools&type=date&legend=top-left&sealed_token=1SX6CDP2N0Emx5IbGfQmEz4TxM11iXtfLKL9K1utRzINJPEDv55f5XEYjliBUB1No6wbcWbMs-cSzO65OC7kAlMLAHJXjqmDoeRCM6hVtW9xd7fyg8cr2DG4gATwkgym1JvgPs4_PeGi6XMAm7_2CVXU9UxRLBW_GP4-Qmd3-AosSRCM1Nkm7dEr2_Ut" />
   <img alt="Star History Chart" src="https://api.star-history.com/chart?repos=madoiscool/baotools&type=date&legend=top-left&sealed_token=1SX6CDP2N0Emx5IbGfQmEz4TxM11iXtfLKL9K1utRzINJPEDv55f5XEYjliBUB1No6wbcWbMs-cSzO65OC7kAlMLAHJXjqmDoeRCM6hVtW9xd7fyg8cr2DG4gATwkgym1JvgPs4_PeGi6XMAm7_2CVXU9UxRLBW_GP4-Qmd3-AosSRCM1Nkm7dEr2_Ut" />
 </picture>
</a>

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
