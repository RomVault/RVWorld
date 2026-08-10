# RomVault CHD preservation standard

CHD is a compressed disk-image format. RomVault uses it to save space without giving up the original data. chdman is MAME's command-line tool for creating, extracting, and checking CHDs.

This standard applies to RomVaultCore, RomVaultCmd, and the Windows application. Avalonia is not covered.

RomVault accepts a CHD as a replacement only when it can rebuild every represented file exactly.

- Every rebuilt file must have the expected byte count and every CRC32, MD5, and SHA-1 listed in the DAT.
- A valid CHD header, internal SHA-1, or successful chdman verify is useful, but is not proof that the original files can be rebuilt.
- Unknown, ambiguous, incomplete, lossy, excessively large, or untested input is left unchanged.
- The original remains available until the replacement has been installed, extracted, and compared with the DAT.
- Content is identified by size and digital fingerprints, not by filenames.

A DAT is a catalog of expected files, sizes, and fingerprints. A payload is original file data represented by the CHD. For ToSort matching, every payload needs an MD5 or SHA-1; CRC32 alone is not strong enough.

RomVault tests the exact chdman executable, media layout, and storage profile with real create/extract/hash samples before allowing it to write. Passing one kind of disc does not approve every kind.

## What a RomVault CHD contains

A new or upgraded CHD keeps its reconstruction information inside the CHD, not in a JSON companion file.

Think of the two records this way: RVEP is the storage label; it says which RomVault policy wrote the CHD. RVRM is the packing list; it identifies the original data and tells RomVault how to rebuild it. Keeping them separate means compression can change without changing content identity.

| Record | What it means |
| --- | --- |
| RVEP schema 1 | How the CHD was stored: the RomVault standard revision, Playback or Archive policy, and optional details about the tool that wrote it. |
| RVRM schema 1 | What original data the CHD represents and how to rebuild it, including any proven alternate form or exact extra data such as an SBI correction. |

Compression, data-block size, and hard-disk geometry are read from the CHD itself; they are not repeated in RVEP. Native CHD metadata remains the authority for disc tracks. RVRM does not store source filenames or descriptor text. RomVault regenerates supported CUE and GDI files using the names required by the current DAT. If a descriptor cannot be rebuilt safely, it remains a separate companion file.

Renaming the CHD, CUE/GDI/TOC, referenced tracks, or SBI does not change the RVRM record. Payload bytes still have to match exactly; harmless descriptor differences such as spacing, quoting, and filenames are compared by meaning.

Changing compression, hunk size, parent/standalone representation, or Playback/Archive policy must also leave RVRM byte-for-byte unchanged. Only an explicit DAT-aware metadata repair may migrate an older RVRM, and payload identity may never change during that repair.

## Supported media and profiles

RomVault supports CD, GD-ROM, DVD, PSP, raw byte streams, hard-disk images, and canonical LaserDisc AVI.

- **Playback** favors fast emulator access with smaller data blocks. Emulators must support the RomVault v1 codecs listed in the technical settings.
- **Archive** favors the strongest tested space saving.

Both profiles are lossless and reversible. Exact preservation always overrides a preferred compression setting.

Raw data uses a one-byte unit because RomVault cannot safely guess its sector layout. LaserDisc is accepted only when chdman's standard uncompressed AVI exactly matches the DAT; arbitrary or lossy AVI files are not claimed to be byte-reversible.

## Disc-image rules

RomVault has separate proof for common CD sector modes, audio and mixed-mode split CUEs, cdrdao TOC, TOSEC GDI, Redump GD-ROM CUE, Unicode paths, DVD/PSP ISO, raw data, hard disks, and canonical LaserDisc AVI.

Supported optical layouts retain track types, indexes, gaps, sector and subchannel formats, and GD-ROM padding. SBI subchannel corrections are embedded by role and fingerprint, not by filename.

RomVault deliberately leaves complex or uncertain layouts unchanged, including shared-file CUEs, non-BINARY wrappers, later indexes, catalog/ISRC/CD-Text data, explicit multisession boundaries, overlapping GDI tracks, and GDI files with non-zero offsets. The detailed list is in the technical reference.

Some DATs contain both CUE/BIN tracks and an ISO view. RomVault stores one CHD only when it can prove that the ISO can be rebuilt exactly from one data track. Otherwise the ISO remains independent; similar filenames are never treated as proof.

For example:

    Disc.cue + Disc (Track 01).bin -> Disc.cue.chd
    Install.iso                    -> Install.iso.chd
    Machine.img                    -> Machine.img.chd
    Side A.avi                     -> Side A.avi.chd

A standalone .raw file is treated as Raw or Hard Disk only when that family is selected because .raw is also used for optical tracks.

## How RomVault changes files safely

Creation and conversion follow the same basic process:

1. Check space, source relationships, layout support, and the selected chdman's proven abilities.
2. Build a temporary candidate and add fresh RVEP and RVRM records.
3. Compress and verify the candidate without changing the source.
4. Install it while retaining the recovery record and original backup.
5. Extract every represented payload and compare it with the DAT.
6. Commit only after that comparison succeeds; otherwise restore the original.

The **Convert and upgrade CHDs while fixing** option can move a standard CHD between Playback and Archive through a verified uncompressed intermediate.

A recovery journal makes interrupted work repeatable and safe. An active operation cannot be recovered by another RomVault process, and unfinished cleanup remains recorded. Temporary transaction files use short names so long CHD filenames do not become even longer chdman paths.

If a source or temporary path is still too long, RomVault stops before changing anything and asks for a shorter RomRoot or mapped path.

### Read-only collections

A scan or verification operation that can stream directly creates no temporary workspace.

When extraction is needed, RomVault first tries a private workspace beside the CHD. If that location is read-only or too long for chdman, it uses a private per-user temporary location. It checks space on the location actually selected and stops safely if no suitable location exists.

RomVault deletes only the exact workspace created for that operation. Workspace cleanup failures are reported. Transaction artifacts remain in the recovery journal until their cleanup succeeds.

## Verification and health

RomVault initially checks a CHD through external chdman extraction. Its faster built-in reader is trusted for direct streaming only after both methods produce the same fingerprint for the uncompressed data. That proof normally expires after 30 days.

Scrubbing has three levels:

- **container:** chdman verification and safe metadata reading;
- **native:** container checks plus a complete built-in read;
- **full:** native checks plus standard external extraction and exact comparison.

The optional health database is stored in LocalAppData, not beside the ROMs. It stores verification details and a redacted path token, never the original absolute path. The scheduler checks failed items first, then changed or expired items, then items never fully checked. Planning is read-only and running can be bounded and resumed.

## Optional features

- **Conversion queues:** large Playback/Archive conversions can be planned, stopped, and resumed. RomVault rechecks file identity, space, metadata, and extracted content before each replacement.
- **Recovery volumes:** an .rvpar file protects CHDs without changing them or making them depend on the recovery volume. It can rebuild up to two missing or corrupt CHDs in each protected block group. Rebuilt files remain separate candidates until normal verification passes.
- **Parent/child CHDs:** optional child files save space but depend on the exact parent. RomVault proves the relationship and standalone reconstruction before installation.
- **ToSort matching:** a differently named CHD is considered only when family, payload count, sizes, and strong fingerprints give one unambiguous match. It is still extracted and checked before moving.
- **Debug logs:** off by default. Enabling **Write CHD scan debug logs** creates __RomVault.chdlogs under the ToSort cache only when a CHD is scanned.

CHD operations return structured error codes and phases. User-facing diagnostics redact absolute paths.

The sections above describe the user-visible standard. The remainder records the exact technical contract.

---

## Technical reference

### Metadata rules and limits

RVEP schema 1 identifies `rvworld-v1` and records only its revision and `playback` or `archive` policy. A writer capability revision, chdman version, and executable SHA-256 may be added as provenance. Family, codecs, hunk/unit sizes, and HDD geometry are not stored in RVEP; RomVault reads those facts from RVRM and native CHD headers/tags.

The canonical text form is:

    schema=1;profile=rvworld-v1;revision=1;storage=archive[;writer=N][;chdman=V][;toolsha256=64-hex]

RVRM schema 1 records typed media and reconstruction-recipe IDs, then an ordered identity for every payload: number, byte size, CRC32, MD5, SHA-1, and SHA-256. The complete fixed set prevents the same content producing different metadata merely because one DAT supplied more hashes than another. Alternate views use typed recipe IDs. Auxiliary data uses typed roles plus exact bytes. It does not repeat RVEP data, detailed native CHD layout, filenames, basenames, descriptor text, or tool provenance.

The operational code may temporarily carry DAT names, descriptor text, and tool details, but serialization first converts it to a separate persistent RVRM record that has no fields for them.

RVRM schema 1 has a required-feature bitmap and length-delimited sections for core payloads, alternate views, and auxiliaries. Readers ignore the meaning of unknown optional sections but preserve their exact bytes through rewrites. Unknown required sections or feature bits are rejected. Recipe and role strings are compact numeric IDs on disk, so presentation wording cannot change identity.

Every RVRM ends with an unkeyed SHA-256 integrity checksum over all preceding RVRM bytes. This detects corruption; it is not a signature and does not authenticate who created the metadata. Parsing also rejects duplicate numbers/sections, incomplete fixed hashes, invalid typed IDs, corrupt lengths, excessive counts, and trailing data.

Schema 1 is the initial contract for both records; there is no public migration schema before it. Unsupported schema numbers are rejected rather than guessed. An RVEP profile revision that this build does not understand remains visible in diagnostics, but RomVault will not apply current compression or geometry rules to it and leaves the CHD unchanged.

The complete RVRM, including its integrity trailer, is limited to 16,777,215 bytes so it fits CHD's 24-bit metadata length. Each auxiliary is non-empty and at most 15 MiB. Schema 1 allows 32 sections, 1,000 payloads per identity list, one proven alternate view, and 64 auxiliaries.

### Exact storage settings

Hunks are the blocks CHD compresses and reads as units.

| Media | Proven round trip | Playback | Archive |
| --- | --- | --- | --- |
| CD | Exact CUE/BIN or TOC through createcd and split/native reconstruction | cdzs, cdzl, cdfl; 19,584-byte hunks | Normally cdlz, cdzl, cdfl; 1,047,744-byte hunks |
| GD-ROM | TOSEC GDI or Redump CUE through matching createcd and extractcd -sb | cdzs, cdzl, cdfl; 19,584-byte hunks | cdlz, cdzl, cdfl; 1,047,744-byte hunks |
| DVD | ISO through createdvd and extractdvd | zstd; 4,096-byte hunks | lzma; 1,048,576-byte hunks |
| PSP | ISO through createdvd and extractdvd | zstd; 2,048-byte hunks | lzma; 1,048,576-byte hunks |
| Raw | Byte stream through createraw -us 1 and extractraw | zstd; 4,096-byte hunks | lzma; 1,048,576-byte hunks |
| Hard disk | Sector image through exact-geometry createhd and extracthd | zstd; 4,096-byte hunks | lzma; 1,048,576-byte hunks |
| LaserDisc | Canonical AVI through createld and extractld | avhu; one frame per hunk | avhu; one frame per hunk |

Playback v1 requires Zstandard: cdzs for optical media and zstd for other byte/sector media. RomVault does not silently substitute older zlib settings.

Known chdman 0.289 exceptions:

- MODE1/2048 Archive uses cdzl, cdfl with 19,584-byte hunks.
- Unicode CUE Archive also uses cdzl, cdfl with 19,584-byte hunks.
- MODE2/2352, mixed CUE, TOC, and supported complex-layout CUE Archive omit cdlz.
- The last group keeps the large Archive hunk; only MODE1/2048 and Unicode CUE use the smaller one.

Unicode paths use temporary ASCII aliases without changing stored metadata. LaserDisc Playback and Archive both use AVHUFF with one frame per hunk.

### Exact optical contract

RomVault separately tests MODE1/2048, MODE1/2352, MODE2/2352, audio-only and mixed data/audio split CUE, Unicode CUE paths, cdrdao TOC with MODE2_RAW/RW_RAW subchannels, TOSEC GDI, and Redump GD-ROM CUE.

For supported split-file BINARY CUE and GDI sets, every referenced track payload must exist, be non-empty, and contain a whole number of sectors. RomVault preserves physical INDEX 00, synthetic pregap, postgap, track type/subtype, sector/subchannel format, and GD-ROM padding.

Redump GD-ROM CUEs require the standard single-density marker before track 01 and high-density marker before track 03. RomVault rebuilds them from native CHGD/LBA geometry.

V1 rejects:

- shared-file CUEs and non-BINARY wrappers;
- FLAGS, INDEX 02 and later, catalog, ISRC, and CD-Text fields;
- conflicting or unreferenced index data;
- explicit multisession boundaries and unsupported track modes;
- overlapping GDI geometry and non-zero GDI file offsets.

CUE/GDI text is regenerated from native metadata using current DAT names. TOC data, including interleaved 2,448-byte RW_RAW frames, is rebuilt from the logical stream and fingerprint-checked. Presentation stays in a sidecar when it cannot be regenerated losslessly.

A same-stem SBI is stored with role sbi-subchannel-correction, emitted using the current DAT name when requested, and checked during scan/export.

Current chdman handles Redump Dreamcast CUE directly; removed -rp and -ap switches are not used. RomVault's probe converts GDI to Redump CUE, creates and re-extracts a CHD with split tracks, and compares all payloads.

An alternate ISO view is permitted only from one MODE1/2048 track or the 2,048-byte user-data area of one MODE1/2352 track. It is embedded only after its generated size and every DAT fingerprint match. Scan and export rebuild it sector by sector and check every member.

### Hard-disk geometry

Playback Auto uses exact 16x63, 16x32, or 4x17 geometry when the sector count factors evenly; otherwise it uses sectors,1,1. Archive defaults to sectors,1,1.

RomVault never pads or truncates. Geometry changes materialize the raw disk, recreate it with explicit sector size and geometry, validate GDDD metadata, and prove logical equality before installation.

### Tool selection and upgrades

RomVault searches configured chdman paths, the application/current directories, and PATH. Each executable is identified by version and SHA-256. Probe results are cached only for that exact binary and probe schema.

An explicitly pinned SHA-256 wins; otherwise RomVault chooses the newest validated writer that supports the requested family, layout, and profile. A newer validated writer revision can trigger an upgrade, and optional broad recompression can use any newer validated encoder. Files written by a newer tool are not silently downgraded.

Use -testchdmans to compare installed tools and -chdtools full for details.

### Queue contract

Planning marks each CHD current, pending, or blocked; binds the job to source-file and CHD identities; estimates temporary space conservatively; and rejects files without a current integrity-checked manifest.

Running is bounded and resumable. It rejects changed sources, saves after every item, checks space, preserves logical/raw CHD SHA-1 and the exact canonical RVRM, verifies RVEP and RVRM, and replaces only after extracted content passes. Reports redact source paths.

### Transaction and recovery contract

The full transaction is:

1. Validate space, source graph, layout, and tool capability.
2. Create an uncompressed stage or exact raw HDD intermediate.
3. Embed fresh RVEP and RVRM.
4. Recompress to Playback or Archive.
5. Run non-mutating chdman and profile checks.
6. Install atomically while retaining the journal and original backup.
7. Extract through the family-specific path and compare every DAT payload.

Journal updates use same-directory atomic replacement with a recoverable backup. A journal-path-specific global process lock covers same-user Windows sessions. A live-operation lease prevents recovery of active work and is released when the operation ends.

Rollback is checkpointed, repeatable, and retained until owned artifacts are removed. The journal binds a candidate to its canonical RVRM SHA-256, container SHA-1, and raw-data SHA-1, and records the original or backup identity separately. A fully DAT-verified installed replacement wins only when all expected identities still match; before that point, a verified original or backup wins over an unverified candidate. Tests inject interruption at every declared boundary, including unavailable files, foreign replacements, corrupt candidates, and failed cleanup.

Compact random transaction names preserve same-volume replacement and Windows path compatibility. Exact chdman paths and free space are checked before work begins.

### Verification details

Native streaming is enabled only after external chdman extraction matches its logical SHA-256 and normally expires after 30 days.

Optical and LaserDisc full scrubs use the canonical reconstruction report. DVD, PSP, Raw, and Hard Disk also compare native and external logical SHA-256 directly.

The health database records a redacted path token, filename, family/profile, tool fingerprint, method, content fingerprints, result, and time.

### Recovery volumes and parents

An .rvpar volume contains integrity-checked manifest, file, block, and volume fingerprints plus two Reed-Solomon shards per group. Repair writes separate candidates for normal verification.

Before a child CHD is installed, RomVault verifies the parent SHA relationship, creates a temporary standalone copy, and compares both raw CHD identity and logical content. It rejects missing parents, cycles, self-parenting, and overwrites.

For V5 CHDs, the child's `Parent SHA1` must match the parent's header `SHA1`, not its `Data SHA1`. RomVault indexes standalone CHDs first, then resolves children by that identity across RomRoot, ToSort, and configured parent paths. Any chdman operation that reads a child supplies the parent with `-ip`; `-op` is used only when deliberately creating a parented output.

### Command-line reference

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

### Conformance tests

The test matrix covers both profiles and every supported family/layout, filename independence and renaming, schema rejection, fixed hash identities, typed recipe round trips, optional/required extension handling, Unicode and hostile input, SBI integrity, parser limits, RVRM invariance during conversion, recovery at every fault boundary, alternate ISO views, exact HDD geometry, health-record redaction, collection repair, profile conversion, parent materialization, mixed-media DAT separation, ToSort rejection, and logging defaults.

Repeated zstd and cdzs encodes of the same input must be byte-identical. chdman extraction, RomVault's native reader, and CHDSharpLib must agree on the source bytes.

### Upstream basis

- <https://docs.mamedev.org/tools/chdman.html>
- <https://github.com/mamedev/mame/blob/master/src/tools/chdman.cpp>
- <https://github.com/mamedev/mame/blob/master/src/lib/util/chd.h>
- <https://github.com/mamedev/mame/blob/master/src/lib/util/cdrom.cpp>
