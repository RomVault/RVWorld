# CHD (V5 + zstd)

See also: [CHD-STANDARD.md](CHD-STANDARD.md) for policy-level standards per media type.

## Goal

Support creating V5 CHDs with zstd for disc-based Redump media during fixing, with correct command selection based on source type:

- `.cue` / `.gdi` sources: `chdman createcd`
- `.iso` sources: `chdman createdvd`
- PSP `.iso` sources: add `-hs 2048`

Ensure CHD metadata matches expectations by using the correct input type (metadata differs between cue/gdi/iso).

## CUE Caveats

CHD is not a faithful container for all information present in an original `.cue`:

- Track filenames referenced in the original cue are not preserved in the CHD. A CHD can be created *from* a cue, but you cannot reliably recreate the original cue contents *from* the CHD later.
- Cue “catalog” metadata and other cue details may be lost when converting to CHD.
- Avoid storing cue information in CHD custom metadata as a workaround. That would require rewriting CHD contents during renames/retag operations, which is not desirable.

Practical implications for RomVault:

- Treat a CHD as satisfying the DAT by matching the track data hashes, not by attempting to regenerate the original cue verbatim.
- Any “virtual cue” generated for matching should be considered an implementation detail for hashing/mapping, not a reversible representation of the source set.

When using CHD container mode, `.cue` / `.gdi` entries from the DAT are treated as non-required (non-collected) because they cannot be faithfully reproduced from the CHD.

## CHD As A Container (Required Behavior)

RomVault already understands “container formats” (Zip/7z) by scanning them as directories with child entries and matching those child entries against DAT file lists.

CHD must be treated the same way for disc sets:

- A single `Game.chd` should be able to satisfy DATs that list multiple source files (e.g. `Game.cue` + track `.bin` files, or `Game.gdi` + track files).
- The inverse must also work: if a DAT expects those source files, and we have a CHD, RomVault should be able to extract and write the expected files.

This is separate from “CHD-only DATs”. If the DAT itself expects `*.chd`, then CHD behaves like any other single file. The container behavior matters when the DAT expects `cue/gdi + tracks` but the user chose to store the set as `*.chd`.

### How Containers Work Today (Zip/7z)

The current container pipeline is:

- Scan: [FileScanner/FileScan.cs](FileScanner/FileScan.cs) uses an `ICompress` implementation (`StructuredZip` / `SevenZ`) and produces a directory-like [FileScanner/ScannedFile.cs](FileScanner/ScannedFile.cs) with child entries.
- Match/Fix: the core fixer and “find fixes” logic matches missing DAT entries to available sources via file grouping and/or archived child entries.

CHD must plug into this same mental model: “CHD is an archive of track files”.

### Implementation

#### Read / Scan Side (CHD → virtual track files)

This is implemented by treating CHD as an archive type:

 - File types: `FileType.CHD` (container) and `FileType.FileCHD` (member) in [SharedEnum/FileType.cs](SharedEnum/FileType.cs).
 - Scanner: CHDs are scanned as containers in [RomVaultCore/Scanner/Populate.cs](RomVaultCore/Scanner/Populate.cs) via `ScanChdContainer`:
   - DVD/ISO: hashes via a streaming reader (no temporary ISO) when enabled; otherwise falls back to `extractdvd`.
   - CD/GDI: hashes per-track via streaming when CHD metadata provides a track layout; otherwise falls back to `extractcd`.
   - When a DAT expects `.cue/.gdi`, a synthetic descriptor can be generated from CHD metadata and verified against the DAT; if it doesn’t match, scanning falls back to `extractcd` for descriptor fidelity.

When streaming is not available (disabled or metadata not present), this uses **extract-to-cache** (`extractcd`/`extractdvd`) for correctness.

To avoid repeated extraction and hashing on rescans, scan results are cached as a small JSON sidecar under the cache directory.

Cache validity is keyed by:

- CHD path + size + timestamp
- expected descriptor type (`dvd` / `cue` / `gdi` / `trust`)
- mapping fingerprint (hashing/mapping algorithm version)
- settings fingerprint (CHD-relevant settings and applicable per-DAT CHD rule fields)
- tool fingerprint (when available: `chdman` version + CHD library version)

Track mapping:

- Recognizes multiple filename conventions for track numbers, not just “(Track NN)”.
- Uses size-based mapping when DAT member sizes are present and unambiguous.
- Falls back to order-based mapping when filenames don’t encode track indices and sizes aren’t sufficient.
- If `extractcd` produces a single shared BIN but the DAT expects per-track files, a fallback “slice and hash” mode is used.

Gap handling:

- Hashing windows begin at `INDEX 01` for each track. `INDEX 00`/PREGAP is not included in the track hash. POSTGAP is removed from the end of the track if present.
- For per-track TYPEs, sector size selection is per-track (e.g., MODE1/2048 vs AUDIO/2352) to compute correct byte ranges.

Compression profile:

- Adds “Auto (CD/DVD/PSP)” selection that picks the appropriate codec set based on the input type and platform hints.

Diagnostics:

- `Settings.ChdScanDebugEnabled` writes a per-CHD log file (expected members, extracted members, and mapping decisions) next to the scan cache entries.
- `Settings.ChdScanCacheEnabled` can be disabled for troubleshooting.
- `Settings.ChdStrictCueGdi` forces `.cue/.gdi` to remain required (not recommended; CHD cannot faithfully reproduce original cue/gdi contents).
- Extraction preflight checks available free space in the cache drive (using `chdman info` logical size when available) before running `extractcd/extractdvd`.
- WinForms UI provides `Verify CHD Container...` on the right-click context menu of a `.chd` in the game list.
- Avalonia UI provides `Verify CHD Container...` on the right-click context menu of a `.chd` in the ROM list.
- UI provides `Verify CHD Parity (Stream vs Extract)...` on a `.chd` to compare streaming hashes against extract-based hashes.
- If `chdman` is upgraded/downgraded, cached CHD scan results are invalidated when the tool version can be detected.

Streaming mode:

- DVD hashing runs in streaming mode (no extraction to ISO) using CHDSharpLib’s logical stream reader. Toggle: `Settings.ChdStreamingEnabled`.
- CD/GDI hashing can also run in streaming mode when CHD metadata provides a track layout; otherwise it falls back to `chdman extractcd`.
- When a `.cue/.gdi` descriptor is expected, a synthetic descriptor is generated from metadata and verified; mismatches fall back to `extractcd` for fidelity.
- For CHDs scanned without a DAT expectation list (e.g. `ToSort`), RomVault auto-detects disc type:
  - If CD track metadata is present, scan as CD.
  - Otherwise scan as DVD and treat the logical stream as an ISO (2048-byte / `0x800` sectors).

Commandline verification:

- `RomVaultCmd -verifychd <path-to-chd> [optional-output-file]` extracts and hashes the CHD contents and prints a summary.
- `RomVaultCmd -chdhealth <path-to-chd> [optional-output-file]` produces a unified health report (container info, scan mode, expected hash outcome, and parity when applicable).

### Alternative Implementation (nkit track hashing)

Nanook’s nkit tooling already contains logic to:

- Read CHDs
- Split tracks
- Hash track data in-memory to match DAT contents

If that C# code is available for reuse, it can replace the extract-to-cache approach to avoid temporary disk I/O and improve correctness for edge cases.

Note: CHD “internal” SHA1/MD5 (data+metadata) is not enough to match track-based DATs; you need per-track hashes.

#### Write / Fix Side (track files ↔ CHD)

Two directions are supported:

- **Source tracks → CHD**
  - From `.cue`/`.gdi`: `chdman createcd`
  - From `.iso`: `chdman createdvd`
  - PSP `.iso`: `createdvd -hs 2048`
- **CHD container creation in fix pipeline**
  - Missing `Game.chd` containers are built from available `.cue/.gdi/.iso` sources via [RomVaultCore/FixFile/FixAChd.cs](RomVaultCore/FixFile/FixAChd.cs) and [RomVaultCore/FixFile/Utils/CreateChdFromDisc.cs](RomVaultCore/FixFile/Utils/CreateChdFromDisc.cs).
  - For Audio CD style sets with only track files (no `.cue/.gdi/.iso`), RomVault can synthesize a simple audio CUE from the track files and run `chdman createcd`.
  - Only create a `.chd` when the set is fully available from sources:
    - No files → do not attempt CHD.
    - Partial files → do not attempt CHD; leave files in their configured archive strategy (e.g., uncompressed, zip, zstd).
    - All files present → create CHD.
- **Export tracks from CHD (opt-in)**
  - This is intentionally not automatic in CHD container mode (it defeats container storage).
  - WinForms: right-click a `.chd` in the game list → `Export Tracks from CHD...`
  - Avalonia: right-click a `.chd` in the ROM list → `Export Tracks from CHD...`
  - The export step uses streaming for DVD/ISO, and for CD/GDI when metadata is available; otherwise falls back to `extractcd`/`extractdvd`. Outputs are named to match the DAT and verified against expected hashes when available.
  - Advanced: `Settings.ChdExportTracksOnFix` can be enabled to allow exporting during fixing when a DAT expects source tracks and a CHD exists in the same folder.

## Inputs / Outputs

- Inputs: Redump-style disc sets stored as files on disk (typically in `ToSort`) containing:
  - CD: `.cue` + one or more `.bin`, or `.gdi` + track files
  - DVD: `.iso`
 - Output: A single `.chd` per set, created with:
  - CHD version: 5
  - Compression: selected per DAT rule (`ChdCompressionType`: Auto/Normal/CD/DVD/PSP)

## Detection Rules

### When to attempt CHD creation

- Only when a DAT rule path has `DiscArchiveAsCHD = true`.
- Use `ChdCompressionType` on the same DAT rule to select the compression profile (Auto/Normal/CD/DVD/PSP).
- Only when the destination file expected by the DAT ends with `.chd`.
- Only when a matching disc source exists with the same base name:
  - `Game Name.chd` can be created from `Game Name.cue` / `Game Name.gdi` / `Game Name.iso`.

### Source selection priority

Use the input type that produces the intended metadata:

1. Prefer `.gdi` for platforms that commonly use GD-ROM images:
   - Arcade - Namco - Sega - Nintendo - Triforce
   - Arcade - Sega - Chihiro
   - Arcade - Sega - Naomi
   - Arcade - Sega - Naomi 2
   - Sega - Dreamcast
2. Otherwise prefer `.cue` if present.
3. Otherwise use `.iso`.

## chdman Invocation Standards

### Common flags

- Always overwrite existing output: `-f`
- Use the compression profile selected via `ChdCompressionType`.
- Run in the directory containing the input, so relative track references resolve correctly.

### Commands

CD images (CUE or GDI):

```text
chdman createcd -i "<input.cue|input.gdi>" -o "<output.chd>" <compression> -f
```

DVD images (ISO):

```text
chdman createdvd -i "<input.iso>" -o "<output.chd>" <compression> -f
```

PSP (ISO):

```text
chdman createdvd -i "<input.iso>" -o "<output.chd>" <compression> -hs 2048 -f
```

## Verification Requirements

After creation, verify:

- CHD version is V5.
- CHD “internal” SHA1/MD5 (data+metadata, as reported by CHD tooling) matches DAT expectations.
- The created file is scan-able by RomVault and is marked as present.
- Source files are only cleaned up after verification succeeds.

If verification fails:

- Treat as a failed fix.
- Do not keep a mismatching CHD as a “fixed” file.
- Delete any partially-created/failed `.chd`, and keep the original source files intact.

## Configuration

- `chdman.exe` lookup:
  - Place `chdman.exe` in the RomVault working directory (next to the executable), otherwise it will fall back to resolving `chdman.exe` via `PATH`.

## Outstanding Work / Considerations

### Track Hash Fidelity (CDDA / multi-track CD)

Real-world data shows some DATs expect byte-for-byte track `.bin` images. A CHD created from a cue can represent the same disc while still failing to reproduce those exact track file bytes when scanning/extracting:

- The container is a disc layout; the DAT is often a file-layout. Track boundary/pregap representation can differ even when the disc is equivalent.
- Some tooling emits transformed track bytes (e.g., sample packing/endianness for audio) that are not reversible to the original file bytes without additional information.

Current posture:

- Provide an explicit mode to treat the CHD as satisfying the expected track files when track hashes cannot be reproduced (`Settings.ChdTrustContainerForTracks`).
- Keep a “strict mode” option for users who require original cue/gdi + track file fidelity (at the cost of not being able to store as CHD).

Future improvements:

- Add a per-DAT or per-system toggle (Audio CD vs GD-ROM vs mixed-mode) so strictness can be chosen by ruleset.
- Record and surface the reason for mismatch (“pregap/layout mismatch”, “audio transform”, “descriptor mismatch”) in scan debug logs and UI.

### CHD Scan Stability / Performance

- Ensure scan fallbacks do not loop between streaming and extract paths. Each scan should try at most once per strategy.
- Prefer a memory-only hashing path when feasible to avoid repeated disk extraction.
- Expand scan caching keys to include tool versions and relevant settings (e.g., `ChdTrustContainerForTracks`, streaming enabled, strict descriptor mode) to prevent stale cache reuse.

### DVD CHD Compression Ratio vs ZSTD Zip

It is expected that a DVD `.chd` can be significantly larger than a `ZSTD Solid` `.zip` of the same ISO, even though CHD is lossless:

- Zip/7z can do “whole file” or “solid” compression across large spans of data.
- CHD uses hunk-based compression. By default, `createdvd` uses a small hunk size (4 KiB), which can reduce compression ratio on some images.

Mitigations to try:

- Increase DVD hunk size (`Settings.ChdDvdHunkSizeKiB`). Larger hunks may improve compression ratio at the cost of random-read performance.
- Prefer `-c zstd` only for DVD if compatibility allows, and benchmark ratios per content type (DVD-Video often contains already-compressed data and won’t shrink much).

Notes:

- Larger CHDs than zipped ISOs does not imply data loss; it usually implies different compression strategy/constraints.
- For archival size above all else, a `ZSTD Solid` archive of the ISO may remain smaller than CHD for some DVDs.

### Path Mapping Robustness

Some configurations use virtual roots like `RomRoot\...` and `ToSort\...`. Any CHD operation that shells out to `chdman` must use absolute filesystem paths:

- Normalize paths and guard against malformed/concatenated paths before invoking tools.
- Apply the same path resolution for Fix and Scan pipelines to keep behavior consistent.

### UI / UX

- Surface CHD actions in both views where users expect them:
  - WinForms: CHD verify/export should appear in the game list context menu.
  - Settings: keep CHD options grouped in a dedicated section to avoid layout overlap.
- When a set is considered satisfied “by container trust” rather than by per-track hash match, the UI shows `[Trusted Container]` and the CHD tooltip/status includes a trust indicator.

### CHDs Outside DAT Context (ToSort / Unknown CHDs)

RomVault currently treats CHD as a file first, and only goes “inside” when verifying a CHD against a DAT that expects track files. As CHD adoption grows, users will also place CHDs into `ToSort` that are not yet associated with any DAT entry.

Key questions:

- How should RomVault represent a CHD that is scanned outside the context of a DAT?
- Should “contents” (tracks / descriptor) be visible in the UI tree, or remain internal?
- Can ToSort CHDs be used as fix sources for other CHDs / zips / track-based sets?

Proposed direction (keeps the DB stable while enabling future fixes):

- **In-DAT sets where the DAT expects `*.chd`**: treat CHD as a single collectible file. Track contents are an implementation detail used for optional verification/export.
- **In-DAT sets where the DAT expects `cue/gdi + tracks` but storage is CHD**: treat CHD as a container for matching and export, but the DAT’s original `cue/gdi` should not be considered faithfully reproducible from CHD (see “CUE Caveats”).
- **ToSort / Unknown CHDs**: do not expand CHD into permanent child entries in the main DB tree by default. Instead, store “virtual track entries” in the CHD scan cache (JSON) as a content index:
  - Track layout (track types, frames, INDEX 00/01 boundaries where applicable)
  - Per-track size + CRC/SHA1/MD5 as RomVault defines them for matching
  - Optional synthetic descriptor hash (layout-based) when applicable

This allows:

- Marking ToSort CHDs as `Delete` when a fully matching CHD already exists in RomRoot and the ToSort CHD is not needed for any fix.
- Marking ToSort CHDs as `NeededForFix` when their cached “virtual track entries” match missing DAT members elsewhere (future work).

Notes:

- CHD is not a “raw copy” source for other CHDs. Even if a ToSort CHD contains matching tracks, fixes will still typically involve extraction/streaming and rebuild, similar to 7z cache behavior.
- If the UI ever shows CHD contents for ToSort, prefer an opt-in “details” view over expanding the primary tree to avoid clutter and ambiguity between “real files” vs “derived virtual members”.

### Cue / Descriptor Policy (When CHD Is The Stored Artifact)

When users adopt CHD as the stored artifact, cue/gdi handling needs explicit policy options (per DAT rule or global setting):

- **Don’t collect cues**: CHD is the collected artifact; cue/gdi are treated as auxiliary inputs only.
- **Cues next to CHD**: keep cue/gdi alongside the CHD for convenience, without treating them as authoritative for hashing/verification.
- **Cues in a subdir**: keep cue/gdi in a dedicated folder (e.g. `_cues`) to reduce clutter in RomRoot.

Similarly, incomplete disc sets may benefit from a policy (e.g. keep incomplete sources in a dedicated subdir) so users can distinguish “kept for later completion” from “ready to archive as CHD”.

### Tooling / Diagnostics

- A single “CHD health” report is available (CLI: `RomVaultCmd -chdhealth`) that includes:
  - container info (version/compression)
  - whether scanning used streaming or extraction
  - whether track hashes match DAT or fell back to “trust container”
- Make CHD scan debug logs easy to locate from the UI (open folder, copy path).
