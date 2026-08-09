# RomVault CHD preservation standard

This document defines the CHD containers that RomVault writes and accepts for preservation. The implementation is in `RomVaultCore`, `RomVaultCmd`, and the WinForms application. Avalonia is intentionally out of scope.

## Non-negotiable preservation invariant

A CHD replaces DAT payloads only after RomVault can extract every represented payload and reproduce its exact byte count and every available DAT CRC32, MD5, and SHA-1. A strong payload hash (MD5 or SHA-1) is required for content-addressed ToSort matching. `chdman verify`, a valid CHD header, and the CHD's internal SHA-1 are useful container checks, but never substitute for DAT payload parity.

Every supported input dialect is capability-probed with a real create/extract/hash fixture before that `chdman` executable may write it. Unknown, ambiguous, lossy, truncated, excessively large, or unproved inputs fail closed and leave the source untouched.

## Standard profile v1

New and upgraded CHDs carry their reconstruction data inside the CHD, not in JSON sidecars:

- `RVEP` schema 1 identifies `rvworld-v1` and records the media family, Playback/Archive policy, codecs, hunk and unit sizes, exact HDD geometry when relevant, `chdman` version, writer capability revision, and SHA-256 of the executable.
- `RVRM` schema 1 records the source dialect, create/extract modes, exact descriptor bytes, payload names, sizes and hashes, proven alternate-view recipes, and authenticated auxiliary files such as SBI corrections.

There are no pre-release migration schemas. Schema 1 is the complete initial contract: every payload requires SHA-256, every embedded auxiliary is hashed, and a SHA-256 integrity trailer covers all manifest data. Any other schema number is rejected. Parsing is bounded to a 64 MiB manifest, 16 MiB descriptor or auxiliary, 1,000 tracks, 16 views, and 64 auxiliaries. It rejects unsafe paths, duplicate members, invalid hash lengths, corrupt integrity data, descriptor graphs without payloads, and metadata traversal. A descriptor is retained beside a CHD only when the separate CUE/GDI/TOC sidecar preference is enabled. Diagnostic logs remain opt-in and are unrelated to reconstruction.

| Family | Canonical round trip | Playback | Archive |
| --- | --- | --- | --- |
| CD | exact CUE/BIN or TOC payload through `createcd`; split or native exact reconstruction | `cdzs,cdzl,cdfl`, 19,584-byte hunks | normally `cdlz,cdzl,cdfl`, 1,047,744-byte hunks |
| GD-ROM | TOSEC GDI or Redump CUE through same-dialect split `createcd` / `extractcd -sb` | `cdzs,cdzl,cdfl`, 19,584-byte hunks | `cdlz,cdzl,cdfl`, 1,047,744-byte hunks |
| DVD | ISO through `createdvd` / `extractdvd` | `zstd`, 4,096-byte hunks | `lzma`, 1,048,576-byte hunks |
| PSP | ISO through `createdvd` / `extractdvd` | `zstd`, 2,048-byte hunks | `lzma`, 1,048,576-byte hunks |
| Raw | byte stream through `createraw -us 1` / `extractraw` | `zstd`, 4,096-byte hunks | `lzma`, 1,048,576-byte hunks |
| Hard disk | sector image through exact-geometry `createhd` / `extracthd` | `zstd`, 4,096-byte hunks | `lzma`, 1,048,576-byte hunks |
| LaserDisc | canonical AVI through `createld` / `extractld` | `avhu`, one frame per hunk | `avhu`, one frame per hunk |

Playback deliberately establishes Zstandard as the RomVault v1 standard: `cdzs` is the primary optical codec and `zstd` is used for non-optical byte/sector media. Emulator support does not silently downgrade a v1 file to the older zlib convention. Emulators that consume the v1 standard need to support these lossless CHD codecs.

MODE1/2048 and Unicode CUE use the verified `cdzl,cdfl` 19,584-byte layout for Archive. MODE2/2352, mixed/complex CUE, and TOC also omit `cdlz` in Archive because chdman 0.289 did not reliably decode those fixtures with CD-LZMA; they retain the large Archive hunk unless the dialect requires the smaller one. Preservation takes precedence over nominal compression settings.

Raw uses a one-byte unit because RomVault cannot safely infer sector semantics. LaserDisc's AVHUFF encoding and frame hunks are inherent to CHD, so Playback and Archive have the same physical encoding while remaining profile-convertible.

`createld` decodes input video and `extractld` emits MAME's canonical uncompressed YUY2/PCM AVI. RomVault therefore accepts LaserDisc creation only when the extracted canonical AVI itself matches the DAT payload. Arbitrary or lossy AVI containers are not claimed to be byte-reversible.

## Exact optical dialects, SBI, and Dreamcast

RomVault detects and separately proves MODE1/2048, MODE1/2352, MODE2/2352, audio-only and mixed data/audio CUE layouts, complex CUE layouts with index/pregap/postgap/flags, Unicode CUE paths, cdrdao TOC with MODE2_RAW/RW_RAW subchannels, TOSEC GDI, Redump GD-ROM CUE, ISO, PSP ISO, raw, HDD, and canonical AVI. The selected executable must pass the exact dialect in the selected storage profile; family-level success is not enough.

The original CUE, GDI, or TOC bytes are authenticated in RVRM. Unicode CUEs are presented to tools through temporary ASCII aliases without changing the embedded original descriptor or payload names. A same-stem SBI file is embedded with role `sbi-subchannel-correction`, emitted only when requested by the DAT, and hash-checked on scan/export. TOC payloads, including interleaved 2,448-byte RW_RAW frames, are reconstructed from the canonical CHD logical stream and verified against the authenticated source hash rather than relying on a potentially lossy descriptor translation.

Current chdman handles Redump Dreamcast CUEs directly. The removed historical `-rp` and `-ap` switches are neither required nor emitted. RomVault's probe converts a GDI fixture to Redump CUE, creates a CHD from that CUE, extracts it again with split tracks, and compares every resulting payload.

## Playback and Archive

- Playback favors smaller hunks and inexpensive codecs for emulator access.
- Archive favors the strongest verified storage savings.

Both policies use the same embedded reconstruction contract and are fully reversible. When **Convert and upgrade CHDs while fixing** is enabled, RomVault can cross-convert an existing standardized CHD through an uncompressed verified intermediate. A newer tool is used only after its executable-specific capability matrix passes; the original is retained until final extraction and DAT parity succeed.

Large collection conversions use a persistent XML queue. Planning classifies every CHD as current, pending, or blocked, binds each job to the source file and CHD identities, records a conservative temporary-space estimate, and refuses files without a current authenticated manifest. Running is bounded and resumable, rejects sources changed since planning, saves after each item, preflights free space, preserves the logical/raw CHD SHA-1, verifies both embedded contracts, and replaces the source only after the final container succeeds. Queue status and diagnostics redact source paths.

For HDDs, Auto selects conventional exact 16x63, 16x32, or 4x17 geometry for Playback when the sector count factors exactly. Otherwise it uses exact `sectors,1,1`; Archive always defaults to this canonical exact geometry. No mode may pad or truncate the source. Geometry changes materialize the raw disk, recreate it with explicit `-ss` and `-chs`, validate GDDD metadata, and prove logical parity before installation.

## Toolchain discovery and upgrades

RomVault discovers configured chdman paths, the application/current directories, and PATH. Each candidate is identified by version and SHA-256 and cached only for that exact executable plus probe schema. Selection prefers an explicitly pinned SHA-256; otherwise it chooses the newest validated writer capable of the requested family, dialect, and profile.

Every CHD records the writer version and executable hash. A newer validated writer revision can trigger an upgrade; the optional broad recompression policy also permits recompression for any newer validated encoder. Containers written by a newer tool are not silently downgraded. Multiple installed versions can be compared with `-testchdmans` and reported with `-chdtools full`.

## Equivalent multi-view sets

Some DAT sets contain both CUE/BIN tracks and an ISO view of the same disc. RomVault stores one CHD only when the ISO is provably derivable from a single MODE1/2048 track, or from the 2,048-byte user-data area of a single MODE1/2352 track. The generated ISO must match the DAT size and every available DAT hash before its bounded view recipe and hashes are embedded in `RVRM`.

Scanning and export extract the optical tracks, regenerate the ISO view sector by sector, and verify all members. If equivalence cannot be proved, the ISO remains an independent media root. An unrelated ISO in the same Redump IBM PC set is therefore never swallowed by a CUE graph merely because the names are similar.

Other roots are always independent, for example:

```text
Disc.cue + Disc (Track 01).bin -> Disc.cue.chd
Install.iso                    -> Install.iso.chd
Machine.img                    -> Machine.img.chd
Side A.avi                     -> Side A.avi.chd
notes.txt                      -> notes.txt
```

Standalone `.raw` is treated as Raw or HDD only when that family is explicitly selected because `.raw` is also a common optical-track extension.

## Transactional fixing and recovery

Creation and conversion use a recoverable journaled transaction:

1. Validate space, source graph, dialect, and selected tool capability.
2. Create a staged uncompressed container or exact raw HDD intermediate.
3. Embed fresh `RVEP` and `RVRM` metadata.
4. Recompress to Playback or Archive.
5. Run non-mutating `chdman verify`.
6. Extract through the family-specific path and compare every DAT payload.
7. Install atomically; restore the original from backup on any failure.

Recovery handles interruption after the journal, staging, metadata, manifest, final verification, backup, or installation phases. Fault-injection self-tests exercise each boundary. CHD operations return structured error codes and phases; user-facing diagnostics redact absolute paths.

## Verification, health, and scrubbing

Native logical streaming is an optimization, never the initial authority. It is enabled for a container only after a current external chdman extraction has matched the native logical SHA-256. The default parity lifetime is 30 days; expiry forces a fresh external comparison.

Scan and verification extractions use a temporary workspace beside the physical source CHD. A CHD reached through a mapped ToSort therefore consumes scratch space on that mapped storage, never in the primary ToSort cache. RomVault removes the workspace after the operation and fails closed if it cannot create or safely preflight that source-local workspace.

The optional persistent health database lives in RomVault's LocalAppData area, not beside the ROM. It stores a redacted path token, filename, family/profile, tool hash, verification method, hashes, result, and time. It never stores the original absolute path. Scrubbing supports:

- `container`: chdman verification plus bounded metadata traversal;
- `native`: container checks plus a complete native logical read;
- `full`: native verification plus canonical external extraction and parity.

Optical and LaserDisc full scrubs use their canonical reconstruction report; DVD, PSP, Raw, and HDD additionally compare native and external logical SHA-256 directly.

The bounded scrub scheduler ranks failed containers first, then containers whose identity changed or whose external parity expired, and finally never-verified containers. Planning is read-only. Running performs only the selected maximum number of full scrubs, updates the health record after each item, and can be resumed on the next maintenance run.

## Collection recovery volumes

Optional `.rvpar` collection recovery volumes protect independent CHDs without creating parent dependencies or modifying a CHD. Each volume contains authenticated manifest, file, block, and volume hashes plus two Reed-Solomon recovery shards per group. Verification identifies missing/corrupt members; recovery can rebuild up to two missing or corrupt CHDs in each block stripe and always writes separate candidate files for normal CHD/DAT verification before installation.

## Parent CHDs

Normal fixing writes independent CHDs. Parent/child storage is optional and explicit because losing or changing a parent can make a child unusable. Before a child is installed, RomVault verifies the parent SHA relationship, materializes a temporary standalone copy, and compares both raw CHD identity and logical content with the standalone source. Graph validation rejects missing parents, cycles, self-parenting, and overwrite attempts.

The CLI can verify a pair, create a reversible child, materialize a standalone CHD, or validate a complete graph. Emulators may use the smaller child; archival workflows can always recover the independent form when the declared parent is present.

## ToSort matching

A differently named ToSort CHD becomes a candidate only when the media families and payload counts agree, every payload has a unique size plus strong-hash match, and any strongly hashed descriptor has one exact match. RomVault still extracts and compares the candidate to the destination DAT before moving it. Ambiguous or hashless content does not qualify.

## Diagnostics and command-line tools

CHD diagnostic-file creation is opt-in and off by default. Enabling **Write CHD scan debug logs** creates `__RomVault.chdlogs` under the ToSort cache only when a CHD is scanned. Disabling it creates no directory and no log file.

```text
RomVaultCmd -testchdman [path-to-chdman]
RomVaultCmd -testchdmans <chdman-path> [more-paths...]
RomVaultCmd -chdtools [full]
RomVaultCmd -chdscrub <container|native|full> <path> [report]
RomVaultCmd -chdparent verify <child> <parent>
RomVaultCmd -chdparent create <standalone> <parent> <output>
RomVaultCmd -chdparent standalone <child> <parent> <output>
RomVaultCmd -chdparent graph <chd...>
RomVaultCmd -chdhealthdb
RomVaultCmd -chdschedule <plan|run> <root> [maximum-items]
RomVaultCmd -chdparity create <collection> <volume> [group-size] [block-KiB]
RomVaultCmd -chdparity verify <volume> <collection>
RomVaultCmd -chdparity repair <volume> <collection> <output>
RomVaultCmd -chdconvert plan <playback|archive> <root> <queue.rvchdqueue>
RomVaultCmd -chdconvert run <queue.rvchdqueue> [maximum-items]
RomVaultCmd -chdconvert status <queue.rvchdqueue>
```

The preservation matrix covers both profiles for every family, all supported CUE/GDI/TOC dialects, Redump Dreamcast, authenticated manifest compatibility, Unicode and adversarial optical inputs, exact SBI embedding, parser bounds, interrupted-transaction recovery, equivalent multi-view reconstruction, health-record redaction, exact HDD geometry, two-file collection recovery, transactional cross-profile conversion, parent materialization, mixed-media DAT partitioning, ToSort rejection, and the logging default. V1 conformance additionally requires repeated `zstd` and `cdzs` encodes to be byte-identical and requires chdman extraction, the native logical reader, and CHDSharpLib decoding to agree on the source bytes.

## Upstream basis

The command behavior and limits follow current MAME documentation and source:

- <https://docs.mamedev.org/tools/chdman.html>
- <https://github.com/mamedev/mame/blob/master/src/tools/chdman.cpp>
- <https://github.com/mamedev/mame/blob/master/src/lib/util/chd.h>
- <https://github.com/mamedev/mame/blob/master/src/lib/util/cdrom.cpp>
