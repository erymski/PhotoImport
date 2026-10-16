# PhonePhotoSync

Windows 10/11, .NET 10 CLI for investigating USB phone photo metadata before implementing capture-date-based imports. Uses the MediaDevices 2.0.0 NuGet package.

Milestone 1's diagnostic CLI is implemented. Samsung S21 WPD properties exposed no authored capture dates in the original partial inventory. Optional bounded EXIF reads and the user's Gallery comparison verified capture days for three camera JPEGs. A later metadata-only run completed DCIM/Pictures through the API (3,909 images), and a separately approved full JPEG resource read passed byte-count, EXIF, and dimension checks. Independent folder counts and other-format capture dates remain open; the backend is not yet accepted for synchronization. iPhone validation is deferred. No synchronization commands are implemented yet.

```powershell
dotnet build PhotoImport.slnx --configuration Release
dotnet test PhotoImport.slnx

dotnet run --project PhotoImport.csproj -- list-devices
dotnet run --project PhotoImport.csproj -- probe --device "My Phone"
dotnet run --project PhotoImport.csproj -- probe --device "My Phone" --output metadata.json
```

Select an exact reported ID or a unique friendly name. An ambiguous name is rejected. JSON output requires a new filename in an existing writable directory.

The probe reads metadata only. It does not download images, open thumbnails, modify the phone, or create/change synchronization state. Reports keep authored, created, modified, and PTP observations separate; missing capture dates are never replaced by file dates. The package's public date properties are projections, so the source timezone and raw timestamp representation remain unknown. The current package API also leaves reported format/content type and a separate PTP date unavailable; reports mark those gaps explicitly.

For explicitly selected JPEGs, the separate `exif` command uses MetadataExtractor 2.9.3, following the existing `photo-sort` approach. It reads media content into memory with a 256 KiB application cap per photo and stops after the first EXIF APP1 segment where available. It accepts one to three distinct absolute device JPEG paths and saves parsed metadata only:

```powershell
dotnet bin\Release\net10.0-windows\phone-photo-sync.dll exif --device "My Phone" --photo "\Internal storage\DCIM\Camera\example.jpg" --output artifacts\jpeg-exif.json
```

Create the output directory first and use a new report filename. Repeat `--photo` for up to three paths; there is no recursive content scan. Driver read-ahead may transfer more than the application cap. The command reports EXIF `DateTime` alongside `DateTimeOriginal`, subseconds, and offset evidence. Only valid `DateTimeOriginal` is a capture candidate; no general-date, filename, modification-date, or PC-timezone fallback is used. An absent offset leaves the clock unqualified and the instant unknown. The first EXIF segment is the scope of the observation; later segments are not inspected. HEIC and other formats are outside this diagnostic. Ctrl+C is checked between synchronous reads and may wait for a driver call.

For `exif`, exit 0 means valid capture clocks were obtained for every selected photo; it does not imply independent accuracy or known offsets. Exit 4 means missing/invalid/ambiguous data or partial failure. Codes 2/3/5/130 retain their argument/device/output/cancellation meanings. JSON keeps earlier results on failure; inspect per-photo outcomes and warnings.

See the [physical-device verification procedure](docs/compatibility/README.md), [backend evidence limits](docs/compatibility/backend-notes.md), [product specification](docs/specs.md), and [work status](docs/work-status.md).
