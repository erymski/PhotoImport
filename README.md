# PhonePhotoSync

Windows 10/11, .NET 10 CLI for investigating USB phone photo metadata before implementing capture-date-based imports. Uses the MediaDevices 2.0.0 NuGet package.

Milestone 1's diagnostic CLI is implemented. Samsung S21 WPD properties exposed no authored capture dates in the original partial inventory. Optional bounded EXIF reads and the user's Gallery comparison verified capture days for three camera JPEGs. A later metadata-only run completed DCIM/Pictures through the API (3,909 images), and a separately approved full JPEG resource read passed byte-count, EXIF, and dimension checks. Independent folder counts and other-format capture dates remain open; the backend is not yet accepted for synchronization. iPhone validation is deferred. No synchronization commands are implemented yet.

Camera's 3,615 total files match the user's count: 3,599 JPG, 2 DNG, and 14 MP4 by extension. Separate HEIC/DNG prefix experiments found capture-day candidates; HEIC had maker-note parser errors, and PNG could not be fully parsed within the approved prefix. The product now supports bounded JPEG/HEIC/DNG capture-day reporting and an offline planning command; live validation of the new HEIC/DNG path remains separate from its automated tests.

```powershell
dotnet build PhotoImport.slnx --configuration Release
dotnet test PhotoImport.slnx

dotnet run --project PhotoImport.csproj -- list-devices
dotnet run --project PhotoImport.csproj -- probe --device "My Phone"
dotnet run --project PhotoImport.csproj -- probe --device "My Phone" --output metadata.json
```

Select an exact reported ID or a unique friendly name. An ambiguous name is rejected. JSON output requires a new filename in an existing writable directory.

The probe reads metadata only. It does not download images, open thumbnails, modify the phone, or create/change synchronization state. Reports keep authored, created, modified, and PTP observations separate; missing capture dates are never replaced by file dates. The package's public date properties are projections, so the source timezone and raw timestamp representation remain unknown. The current package API also leaves reported format/content type and a separate PTP date unavailable; reports mark those gaps explicitly.

For explicitly selected JPEG/HEIC/HEIF/DNG files, the separate `exif` command uses MetadataExtractor 2.9.3, following the existing `photo-sort` approach. It reads media content with a 256 KiB application cap per photo. JPEG stops after the first EXIF APP1 segment; HEIC/DNG are parsed only from a bounded memory prefix. It accepts one to three distinct absolute device paths and saves parsed metadata only:

```powershell
dotnet bin\Release\net10.0-windows\phone-photo-sync.dll exif --device "My Phone" --photo "\Internal storage\DCIM\Camera\example.jpg" --output artifacts\jpeg-exif.json
```

Create the output directory first and use a new report filename. Repeat `--photo` for up to three paths; there is no recursive content scan. Driver read-ahead may transfer more than the application cap. Reports retain EXIF `DateTime`, `DateTimeOriginal`, subseconds, offsets and associated directories, plus a separately validated capture calendar day. Invalid/missing exact-time offsets or subseconds do not discard a valid day, and no timezone conversion or fallback date is applied. Only unrelated Apple maker-note errors may be warnings for day resolution; capture/container parser errors stay unresolved. PNG/TIFF are not supported by this reader. Ctrl+C is checked between synchronous reads and may wait for a driver call.

For `exif`, exit 0 means valid capture days were obtained for every selected photo; it does not imply independent accuracy, valid exact timestamps, or known offsets. Exit 4 means unresolved capture days or partial failure. Codes 2/3/5/130 retain their argument/device/output/cancellation meanings. Schema-2 JSON includes canonical source paths, reported sizes and optional persistent IDs. Earlier reports remain diagnostic evidence but lack the provenance needed for complete offline planning.

The `plan` command reads an inventory and one or more capture reports entirely offline. Try the synthetic examples without connecting a phone:

```powershell
dotnet bin\Release\net10.0-windows\phone-photo-sync.dll plan --inventory examples\preview\inventory.json --capture-report examples\preview\capture.json --cutoff-day 2026-10-10
dotnet bin\Release\net10.0-windows\phone-photo-sync.dll plan --inventory examples\preview\inventory.json --capture-report examples\preview\capture.json --checkpoint examples\preview\checkpoint.json
```

The first selects only the 11 October example (200 bytes). A manual cutoff means everything through that day was already imported. The second uses an empty tracked boundary, selecting both examples (300 bytes); keys recorded by a future importer would exclude already imported files on that same day while allowing new ones. Older-day late additions remain excluded. Removing PC copies never changes selection.

Repeat `--capture-report` for additional saved samples and optionally add `--output <new.json>`. Every inventory row gets an eligible/older/alreadyImported/unresolved decision. Source device, canonical path, known size and available persistent IDs must agree; legacy reports, missing/invalid dates, unsupported formats, changed sources and partial inventories produce an incomplete preview, not a guessed result. Metadata fingerprints are not photo hashes, and snapshots do not revalidate the current phone. No device provider is constructed, destination files inspected, photos copied, or checkpoint/configuration files written. This preview is not accepted by an importer; enrollment, state persistence, live scanning and importing remain future work.

For `plan`, exit 0 means complete preview, 2 malformed/mismatched input, 4 incomplete/unresolved evidence, 5 local I/O failure, and 130 cancellation. Existing reports/inputs are never overwritten.

See the [physical-device verification procedure](docs/compatibility/README.md), [backend evidence limits](docs/compatibility/backend-notes.md), [product specification](docs/specs.md), and [work status](docs/work-status.md).
