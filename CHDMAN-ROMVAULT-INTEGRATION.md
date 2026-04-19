# chdman Feature Coverage (RomVault)

This document maps `chdman` features to what RomVault currently uses, and highlights candidate features to hook up next.

Reference: https://docs.mamedev.org/tools/chdman.html

## Current Usage (Implemented)

RomVault uses `chdman` primarily for:

- Creating CHDs from disc sources:
  - `createcd` (CUE/GDI)
  - `createdvd` (ISO, including PSP with `-hs 2048`)
- Extracting CHD contents:
  - `extractcd` (CD/GDI path fallback and some export workflows)
  - `extractdvd` (DVD/ISO fallback and some export workflows)
- Inspecting CHDs:
  - `info` (logical size preflight; and some informational checks)

RomVault also has streaming-based scanning/export for some cases (no `chdman` extraction), using CHD metadata and the logical stream reader.

## Feature Matrix

chdman command | What it does | RomVault status | Notes / where used
---|---|---|---
`info` | Displays CHD info including version, hunk size, compression, data/metadata SHA1 | Used | Scan/extract preflight and parsed in CHD health reporting
`verify` | Verifies CHD integrity (can optionally `--fix`) | Used | Creation pipeline runs verify before merge/cleanup
`createcd` | Create CHD from CD inputs (incl. cue/gdi) | Used | Fix pipeline creates CD/GDI CHDs
`createdvd` | Create CHD from DVD ISO inputs | Used | Fix pipeline creates DVD/PSP CHDs
`createraw` | Create CHD from raw data | Not used | Possible future for non-disc media
`createhd` | Create CHD from hard disk images | Not used | Would be required if adding HDD CHDs
`createld` | Create CHD from laserdisc inputs | Not used | Would be required if adding LD CHDs
`extractcd` | Extract CD layout to cue + track files | Used | Scan fallback and export
`extractdvd` | Extract ISO-equivalent logical image | Used | Scan fallback and export
`extractraw` | Extract raw media image | Not used | Potential for generic workflows
`extracthd` | Extract hard disk image | Not used | HDD CHD support would need this
`extractld` | Extract laserdisc content | Not used | LD CHD support would need this
`addmeta` | Add metadata tags | Not used | Generally avoided for workflows that need stable, rename-friendly artifacts
`delmeta` | Delete metadata tags | Not used | See `addmeta`
`dumpmeta` | Dump metadata tags | Used | Included (truncated) in CHD health diagnostics output
`listtemplates` | List metadata templates | Not used | Candidate for tooling UX

## Standardization Targets (Per Disc Type)

These are the `chdman` choices RomVault should standardize around:

### CD (CUE)
- Command: `createcd`
- Working directory: directory containing the `.cue`
- Required: `-f` overwrite
- Optional: `-np <count>` from `Settings.ChdNumProcessors` (0 = auto)
- Recommended: use a CD-appropriate compression profile (lossless)

### GD-ROM (GDI)
- Command: `createcd`
- Working directory: directory containing the `.gdi`
- Required: `-f`
- Optional: `-np <count>` from `Settings.ChdNumProcessors` (0 = auto)
- Recommended: prefer `.gdi` over `.cue` where the platform expects GD-ROM metadata

### DVD / ISO
- Command: `createdvd`
- Required: `-f`
- Required: hunk size must be a multiple of 2048 bytes (DVD logical sector size)
- Optional: `-np <count>` from `Settings.ChdNumProcessors` (0 = auto)
- Recommended: set `-hs` to a larger value than the default if compression ratio is prioritized (trade-off: random read performance)

### PSP / ISO
- Command: `createdvd -hs 2048`
- Required: `-hs 2048` (ensures hunks align to 2048-byte sectors)
- Required: `-f`
- Optional: `-np <count>` from `Settings.ChdNumProcessors` (0 = auto)

## Recently Implemented

- `verify` is now run after CHD creation; creation is treated as failed if verify fails.
- `info` parsing in CHD health now surfaces compression, hunk size, unit size, and logical size.
- `dumpmeta` is collected in CHD health output (truncated for readability).
- `-np` support is wired into CHD creation via `Settings.ChdNumProcessors`.

