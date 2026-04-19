# RVWorld (RomVaultWorld)

RomVault 3.x codebase (core engine, UI frontends, and supporting libraries).

## Projects

The main projects in this repo:

| Project | Purpose |
|---|---|
| `ROMVault` | WinForms UI |
| `ROMVault.Avalonia` | Avalonia UI |
| `RomVaultCore` | Core engine (scan, DAT update, fix planning/execution, DB/cache) |
| `DATReader` | DAT parsing/cleanup/writing library |
| `FileScanner` | Header detection + filesystem/container scanning |
| `CHDlib` | CHD reader + metadata + FLAC support |
| `Compress` | ZIP/7z + compression primitives |
| `TrrntZip` | TorrentZip implementation |
| `TrrntZipCMD` | CLI wrapper for TorrentZip |
| `TrrntZipUI` / `TrrntZipUICore` | UI wrapper for TorrentZip |
| `RomVaultCmd` | CLI entrypoints for RomVault workflows |
| `RVIO` | IO helpers (long paths, wrappers) |
| `SharedEnum`, `SortMethods`, `ByteSortedList`, `CodePage`, `Dark`, `RVZstdSharp` | Shared support libraries |

## Building

### Prerequisites

- .NET SDK (the projects currently target `net10.0`, so a .NET 10 SDK is required)
- Visual Studio 2022 (optional, for solution-based development)

### Build the Solution

From the repository root:

```powershell
dotnet build .\RVWorld.sln -c Release
```

### Run

Run the Avalonia UI:

```powershell
dotnet run --project .\ROMVault.Avalonia\ROMVault.Avalonia.csproj -c Release
```

Run the WinForms UI (Windows only):

```powershell
dotnet run --project .\ROMVault\ROMVault.csproj -c Release
```

## Building Documentation (Doxygen)

The API documentation is generated using Doxygen and written to `docs/html`.

### Prerequisites

- Install Doxygen
- Install Graphviz if you want diagrams/graphs
  - The Graphviz `dot` executable must be available, and `DOT_PATH` in `Doxyfile` may need to be updated to match your install location.

### Generate Docs

From the repository root:

```powershell
doxygen .\Doxyfile
```

Open:

- `docs/html/index.html`

If Doxygen reports warnings, a log is written to:

- `docs/doxygen-warnings.log`

### Enable Graphs/Diagrams

By default, expensive graphs (call/caller/include/directory graphs) are disabled in `Doxyfile` to keep generation fast and reliable.

To turn graphs back on:

1. Ensure Graphviz is installed and `DOT_PATH` points to the folder containing `dot.exe`.
2. Re-run `doxygen .\Doxyfile`.

If you upgrade Doxygen and see “obsolete tag” warnings, update the config with:

```powershell
doxygen -u .\Doxyfile
```

## Notes

- CHD media-type standards live in [CHD-STANDARD.md](CHD-STANDARD.md).
- `chdman` feature coverage in RomVault lives in [CHDMAN-ROMVAULT-INTEGRATION.md](CHDMAN-ROMVAULT-INTEGRATION.md).
- `RomVaultCmd` includes CHD tooling:
  - `RomVaultCmd -verifychd <path-to-chd> [optional-output-file]`
  - `RomVaultCmd -chdhealth <path-to-chd> [optional-output-file]`
