# PhonePhotoSync

Windows 10/11, .NET 10 CLI to import new Android camera photos, videos and screenshots over USB using filename dates. Uses MediaDevices 2.0.0. Existing metadata/EXIF diagnostic commands remain available.

Build the Release executable once from the repository root:

```powershell
dotnet build PhotoImport.slnx --configuration Release
```

Before creating a commit, format the repository with `dotnet csharpier format .`.

Open a PowerShell in `bin\Release\net10.0-windows`; the executable examples below assume that is the current directory. Connect and unlock the Android phone, select **Transferring files**, and keep it awake. Configure once with your phone name, an existing destination directory and the last day already imported. For the S21, the completed initial day is 2026-09-03. Replace the example destination with your real folder:

```powershell
.\phone-photo-sync.exe init --device "Eugene's S21" --alias S21 --destination "D:\Photos\Incoming" --cutoff-day 2026-09-03 --root "\Internal storage\DCIM\Camera" --root "\Internal storage\DCIM\Screenshots" --root "\Internal storage\Samsung\Pictures\Camera Roll"
```

Then run the utility normally:

```powershell
.\phone-photo-sync.exe
# Optional preview and saved progress:
.\phone-photo-sync.exe sync --dry-run
.\phone-photo-sync.exe status
```

Normal launch detects the uniquely matching registered phone, reads its JSON progress, copies eligible full files without overwriting existing files, and updates the checkpoint only after successful copying. Use `sync --device S21` if multiple registered phones are connected. Registration uses serial/manufacturer/model when available, otherwise an exact device ID; a changed/missing identity stops automatic matching. Friendly names alone never choose a saved checkpoint. `init` adds registrations and never resets an existing one.

Dates come from recognized `YYYYMMDD...`, `Screenshot_YYYYMMDD...`, or `WP_YYYYMMDD...` filenames. Invalid/unknown names are skipped and listed. No EXIF or modification-time fallback is used for importing. The initial cutoff excludes that entire day; later checkpoints track already imported files on the newest day so additional same-day media can be copied. Late-added files with older dates remain excluded, and organizing/deleting old PC copies never triggers a fresh import. Folder scope determines camera/screenshot origin: manually placing unrelated dated media there also makes it eligible. iPhone importing is deferred.

Dry run lists selected files, proposed destination names and estimated bytes using metadata (filenames, paths, sizes, identifiers), with no photo/video content reads or destination/state writes. Pending-file hash reuse is verified only during a real sync. Collision names may change if the destination changes afterward.

Settings, checkpoints and recovery journals are under `%LOCALAPPDATA%\PhonePhotoSync\`; `status` reads them without accessing USB. A failed/interrupted copy keeps its pending journal and verified completed files. Reconnect and rerun to resume; incomplete files restart from byte zero. Existing unrelated outputs are preserved with collision suffixes. Source/settings conflicts require inspection of the reported journal location; do not delete journals merely to clear an error. Temporary media stays in `.phone-photo-sync` under the destination. After successful archival, the importer removes only its current session directory if empty; the shared parent and other session directories remain. A cleanup I/O/access failure produces a warning without failing the completed import. Never point this directory at unrelated files or manually edit its contents during a run. Free space must cover remaining selected bytes plus a 2 GiB reserve.

Checkpoint replacement is atomic and keeps the prior valid configuration as `config.json.bak`; malformed state is rejected rather than silently restored/reset. If progress commits but final archival fails, the CLI explicitly reports that commitment; rerun to finish recovery.

| Exit code | Meaning |
| --- | --- |
| `0` | Success, including reported filename skips |
| `2` | Argument or configuration error |
| `3` | Device, identity or source-root error |
| `4` | Incomplete evidence or transfer failure |
| `5` | Local I/O or state error |
| `130` | Cancellation before checkpoint commitment |

A synchronous native USB request may delay cancellation.

Implementation is covered by 348 passing fake-device/filesystem/diagnostic tests; real production registration/reconnect validation remains separate. The prior throwaway filename trial copied 126 files (116 photos/screenshots, 10 videos), 903 MiB in 2 min 7 sec plus 24 sec metadata inventory. It did not create importer state. Full transfer checks compare reported/received/local lengths; local recovery SHA-256 is not a phone-side hash or proof of original-asset identity. MediaDevices can suppress native enumeration errors, so API completion does not prove a physical census. See the [acceptance procedure](docs/compatibility/filename-importer-validation.md).

Reporting records completion time before archiving, reserves distinct proposed names during dry run, and includes ignored nonmedia counts. Historical archives are preserved unchanged. Reconnect, new same-day media and isolated real-USB cancellation/resume observations are recorded in the handoff.

## Diagnostic commands and earlier evidence

Samsung S21 WPD metadata exposed no authored capture dates. Optional EXIF diagnostics and Gallery comparisons verified sample JPEG/HEIC/DNG days. Those diagnostics retain their original provenance/limits independently of the filename importer.

Camera's 3,615 total files match the user's count: 3,599 JPG, 2 DNG, and 14 MP4 by extension. The bounded product diagnostic returned capture days for one HEIC and DNG each, both confirmed against Gallery. HEIC retains unrelated maker-note errors; PNG remains unresolved. Sample agreement does not prove format-wide support.

```powershell
dotnet build PhotoImport.slnx --configuration Release
dotnet test PhotoImport.slnx
dotnet csharpier format .

.\phone-photo-sync.exe list-devices
.\phone-photo-sync.exe probe --device "My Phone"
.\phone-photo-sync.exe probe --device "My Phone" --output metadata.json
```

Select an exact reported ID or a unique friendly name. An ambiguous name is rejected. JSON output requires a new filename in an existing writable directory.

The probe reads metadata only. It does not download images, open thumbnails, modify the phone, or create/change synchronization state. Reports keep authored, created, modified, and PTP observations separate; missing capture dates are never replaced by file dates. The package's public date properties are projections, so the source timezone and raw timestamp representation remain unknown. The current package API also leaves reported format/content type and a separate PTP date unavailable; reports mark those gaps explicitly.

For explicitly selected JPEG/HEIC/HEIF/DNG files, the separate `exif` command uses MetadataExtractor 2.9.3, following the existing `photo-sort` approach. It reads media content with a 256 KiB application cap per photo. JPEG stops after the first EXIF APP1 segment; HEIC/DNG are parsed only from a bounded memory prefix. It accepts one to three distinct absolute device paths and saves parsed metadata only:

```powershell
.\phone-photo-sync.exe exif --device "My Phone" --photo "\Internal storage\DCIM\Camera\example.jpg" --output artifacts\jpeg-exif.json
```

Create the output directory first and use a new report filename. Repeat `--photo` for up to three paths; there is no recursive content scan. Driver read-ahead may transfer more than the application cap. Reports retain EXIF `DateTime`, `DateTimeOriginal`, subseconds, offsets and associated directories, plus a separately validated capture calendar day. Invalid/missing exact-time offsets or subseconds do not discard a valid day, and no timezone conversion or fallback date is applied. Unrelated Apple maker-note and ICC-profile errors are retained as day-resolution warnings; capture/container parser errors stay unresolved. PNG/TIFF are not supported by this reader. Ctrl+C is checked between synchronous reads and may wait for a driver call.

For `exif`, exit 0 means valid capture days were obtained for every selected photo; it does not imply independent accuracy, valid exact timestamps, or known offsets. Exit 4 means unresolved capture days or partial failure. Codes 2/3/5/130 retain their argument/device/output/cancellation meanings. Schema-2 JSON includes canonical source paths, reported sizes and optional persistent IDs. Earlier reports remain diagnostic evidence but lack the provenance needed for complete offline planning.

The `plan` command reads an inventory and one or more capture reports entirely offline. Try the synthetic examples without connecting a phone:

```powershell
.\phone-photo-sync.exe plan --inventory examples\preview\inventory.json --capture-report examples\preview\capture.json --cutoff-day 2026-10-10
.\phone-photo-sync.exe plan --inventory examples\preview\inventory.json --capture-report examples\preview\capture.json --checkpoint examples\preview\checkpoint.json
```

The first selects only the 11 October example (200 bytes). A manual cutoff means everything through that day was already imported. The second uses an empty tracked boundary, selecting both examples (300 bytes); keys recorded by a future importer would exclude already imported files on that same day while allowing new ones. Older-day late additions remain excluded. Removing PC copies never changes selection.

Repeat `--capture-report` for additional saved samples and optionally add `--output <new.json>`. Every inventory row gets an eligible/older/alreadyImported/unresolved decision. Source device, canonical path, known size and available persistent IDs must agree; legacy reports, missing/invalid dates, unsupported formats, changed sources and partial inventories produce an incomplete preview, not a guessed result. Metadata fingerprints are not photo hashes, and snapshots do not revalidate the current phone. No device provider is constructed, destination files inspected, photos copied, or checkpoint/configuration files written. This EXIF-evidence preview is separate from filename-day importing; the importer uses its registered settings and its own filename plan.

For `plan`, exit 0 means complete preview, 2 malformed/mismatched input, 4 incomplete/unresolved evidence, 5 local I/O failure, and 130 cancellation. Existing reports/inputs are never overwritten. Its offline checkpoint/report format is separate from importer configuration and is never silently migrated.

The live `preview` command inventories explicitly chosen device folders recursively, then attempts bounded capture-day reads in canonical path order. Both the folder and the attempt limit are required:

```powershell
.\phone-photo-sync.exe preview --device "My Phone" --root "\Internal storage\DCIM\Camera" --max-photos 10 --cutoff-day 2026-10-10 --output-dir artifacts\camera-preview
```

The example opens at most ten supported photos (at most 2,621,440 application bytes). Failed opens consume attempts; there are no automatic retries. Driver read-ahead may transfer more over USB. Repeat `--root` for disjoint folders; duplicates/nested roots are rejected. The limit applies to capture attempts, so metadata enumeration still covers every photo in the chosen folders and may take time. Unknown sizes, duplicate paths, PNG/TIFF and unread files remain unresolved; reaching the limit never declares the folder fully previewed. No filename or modification-date guess skips a read. Use `--checkpoint <local.json>` instead of `--cutoff-day` for tracked same-day boundaries.

The output directory's parent must already exist; the final directory must be new, even if an existing directory is empty. The command saves `inventory.json`, schema-2 `capture.json`, `preview.json` and a `run.json` manifest with scope, attempt/byte counts, limit status and errors. The first two reports can be reused by offline `plan`. Device/root failures can leave empty reserved files; consult the manifest and command exit. Reports are not an atomic bundle. The run closes report handles before returning; late cancellation or close failures correct this run's capture/preview/manifest statuses by replacing only its newly created reports. Earlier evidence is retained. If correction itself fails, stderr and the nonzero exit remain authoritative; temporary metadata reports may remain. Ctrl+C is checked between synchronous device calls and may wait for a driver response.

For `preview`, exits are 0 complete, 2 invalid arguments/checkpoint/device mismatch, 3 device/root failure or disconnect, 4 unresolved/partial evidence, 5 local I/O failure, 130 cancellation. Cancellation takes priority over simultaneous output/cleanup errors. The command saves metadata only, with no photographs, destination lookup, phone changes or checkpoint/configuration updates. A separately approved S21 Camera/max-three run inventoried 3,601 images and read three prefixes (28,669 application bytes); unread rows remained unresolved, and offline reopening reproduced the decisions. See the [hardware assessment](docs/compatibility/android-samsung-sm-g991b.md). This limited validation does not authorize a broad content scan.

See the [physical-device verification procedure](docs/compatibility/README.md), [backend evidence limits](docs/compatibility/backend-notes.md), [product specification](docs/specs.md), and [work status](docs/work-status.md).
