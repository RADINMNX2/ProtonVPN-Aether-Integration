# ProtonVPN-Aether-Integration

**A completely free, no-account VPN client for Windows** — built on the original, unchanged modern Proton VPN UI, powered by the [Aether](https://github.com/CluvexStudio/Aether) tunnelling engine (MASQUE / WireGuard / WARP) instead of Proton's paid subscription backend.

## Why

Proton VPN's official Windows app is excellent, but requires an account, a subscription, and accesses Proton's backend. This project strips out **all** account/login/subscription/server-list backend dependency so the app is **100% free with no account**, while keeping the original modern Proton UI untouched. The tunnel goes through the free Aether engine, and the architecture is extensible so other free transports (Siphon, etc.) can be added later.

## How it works

- **UI preserved**: the entire `UI\Main\` tree (connection card, sidebar, countries, settings) is retained unchanged.
- **No account**: a fake "always-free, anonymous" session is injected at the DI level so the app never talks to the Proton backend. `MainWindowViewNavigator` goes straight to the main window.
- **Free engine**: a new `VpnProtocol.Aether` is wired through the service's `TunnelOrchestrator` → `AetherConnection` → `aether.exe` (Aether engine), which provides local SOCKS5 and a real **Wintun TUN** full-tunnel bridge.
- **Extensible**: the connection layer is protocol-agnostic, so Siphon and other free transports can be added as additional `IVpnConnection` implementations.

## Repository layout

```
.github/workflows/   CI: validate → engine (Rust) → service (.NET WinAppSDK)
aether/              Vendored Aether engine (v1.8.0) + quiche, with the TUN bridge
src/                 The win-app fork (C# / WinAppSDK) with all Aether + no-account changes
Resources/           aether.exe + wintun.dll (built by CI)
scripts/             build-engine.ps1, fetch-wintun.ps1
CORE_VERSION         Pinned engine core version
```

## Building

Local machines generally lack the Windows toolchain, so the recommended path is the **GitHub Actions** pipeline (`.github/workflows/`), which builds the Rust engine, builds the .NET service, and publishes artifacts:

```bash
# Rust engine -> aether.exe (Windows x86_64)
./scripts/build-engine.ps1 -Target x86_64-pc-windows-msvc

# .NET WinAppSDK service -> app artifacts
dotnet build src/ProtonVPN.App.sln -c Release -p:Platform=x64 -p:RuntimeIdentifier=win-x64
```

## License

- Application code: **GPL-3.0** (derived from the upstream win-app fork).
- Tunnelling engine: [Aether](https://github.com/CluvexStudio/Aether) — **AGPL-3.0**.
