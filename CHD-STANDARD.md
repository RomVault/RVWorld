# CHD Standardization (Media Types)

This document defines required and recommended standards for creating and interpreting CHD files across common disc-image media types. It is intended to be policy-level and stable over time.

## Global Requirements (All CHDs)

- CHD version: V5.
- Output artifact: one `.chd` per disc image set (one disc per file).
- Canonical tool: `chdman` (MAME). Other tools are acceptable if they produce equivalent CHD V5 output.
- Creation should be performed with the working directory set to the input directory so relative references resolve correctly (e.g., `.cue`/`.gdi` that refer to track files).
- Hunk size must be a multiple of the media’s sector or unit size (e.g., 2048 bytes for ISO/UDF data sectors).

## Verification Standards (All CHDs)

Verification must validate:

- Header: CHD version is V5.
- Container integrity: `chdman verify -i "<file.chd>"` succeeds (or an equivalent verifier).
- Informational: CHD internal SHA1/MD5 (data+metadata) are readable. These are not a substitute for member-level verification for track-based ecosystems.

## Disc-Equivalence vs File-Equivalence

CHD is a disc-layout container. Some ecosystems validate file-layout artifacts (e.g., exact `.cue/.gdi` text and exact track file byte streams). A CHD can represent the same disc while failing to reproduce those exact file bytes.

Two compatibility modes are recognized:

- Disc-equivalence mode (recommended for CHD storage): verification is performed against disc content/layout; original descriptor text and original track filenames are not required to be reconstructible.
- File-equivalence mode (strict): original `.cue/.gdi` and original per-track file bytes must be preserved; CHD alone is not sufficient.

## Media Types

### CD (Redump-style CUE + track files)

**Input expectations**
- Source type is a `.cue` referencing one or more track data files.

**Creation**
- Command: `chdman createcd`
- Input must be the `.cue` so CHD metadata tracks match the intended disc layout.

**Interpretation expectations**
- Track boundaries are defined by the disc layout (typically `INDEX 01` as the start of a track’s primary data).
- If computing per-track hashes for a file-based ecosystem:
  - Hash windows begin at `INDEX 01`.
  - `INDEX 00` / PREGAP is excluded from per-track hashes.
  - POSTGAP is removed from the end of the track hash window when present.
- Sector size per track is derived from track type/metadata:
  - Audio: 2352 bytes
  - Mode1/2048: 2048 bytes
  - Otherwise: inferred from descriptor/metadata (do not guess 2048 for audio)

**Notes**
- The original `.cue` text is not required to be reversible from CHD in disc-equivalence mode.

### GD-ROM (Dreamcast / Naomi-style GDI + tracks)

**Input expectations**
- Source type is a `.gdi` referencing track files.

**Creation**
- Command: `chdman createcd`
- Prefer `.gdi` over `.cue` where applicable so CHD metadata matches GD-ROM expectations.

**Interpretation expectations**
- Same track window rules as CD for any per-track hashing, but track layouts differ from standard CD (mixed-mode is common).

### DVD / ISO (Redump-style ISO)

**Input expectations**
- Source type is a single `.iso`.

**Creation**
- Command: `chdman createdvd`

**Interpretation expectations**
- The CHD must be treated as a DVD and interpreted as a single logical ISO stream.
- Logical sector size is 2048 bytes (`0x800`).
- Extraction reference: `chdman extractdvd` produces an ISO-equivalent logical image.

**Compression / hunks**
- Hunk size can be tuned for compression ratio vs random-read performance; larger hunks often compress better.
- When specifying a hunk size, it must be a multiple of 2048 bytes (DVD logical sector size).

### PSP (UMD ISO)

**Input expectations**
- Source type is a single PSP `.iso`.

**Creation**
- Command: `chdman createdvd -hs 2048`
- `-hs 2048` is required so hunks align to 2048-byte sectors.

**Interpretation expectations**
- Treat as DVD/ISO (logical 2048-byte sectors).

### Audio CD (Track files without descriptor)

**Input expectations**
- Track files exist without `.cue`/`.gdi`.

**Creation**
- Allowed only when track set is complete and a simple audio `.cue` can be synthesized.
- Command: `chdman createcd` using synthesized cue.

**Interpretation expectations**
- Same per-track hashing rules as CD audio if interoperating with file-based track hash ecosystems.

## Media Type Detection (When Input Type Is Unknown)

When interpreting a CHD without external context:

- If CD track metadata is present, treat the CHD as CD/GD-style and interpret by track layout.
- Otherwise treat the CHD as DVD/ISO and interpret as a single logical ISO stream with 2048-byte (`0x800`) sectors.

## Reference Commands (chdman)

- Verify container: `chdman verify -i "<file.chd>"`
- Inspect container: `chdman info -i "<file.chd>"`
- Extract DVD logical image: `chdman extractdvd -i "<file.chd>" -o "<image.iso>" -f`
- Extract CD descriptors/tracks: `chdman extractcd -i "<file.chd>" -o "<disc.cue>" -f`
