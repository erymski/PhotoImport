# PhonePhotoSync

Windows 10/11, .NET 10 CLI for investigating USB phone photo metadata before implementing capture-date-based imports. Uses the MediaDevices 2.0.0 NuGet package.

Milestone 1's diagnostic CLI is implemented. Android/iPhone compatibility and selected-file/original access are unverified until tested on physical devices. No synchronization commands are implemented yet.

```powershell
dotnet build PhotoImport.slnx --configuration Release
dotnet test PhotoImport.slnx

dotnet run --project PhotoImport.csproj -- list-devices
dotnet run --project PhotoImport.csproj -- probe --device "My Phone"
dotnet run --project PhotoImport.csproj -- probe --device "My Phone" --output metadata.json
```

Select an exact reported ID or a unique friendly name. An ambiguous name is rejected. JSON output requires a new filename in an existing writable directory.

The probe reads metadata only. It does not download images, open thumbnails, modify the phone, or create/change synchronization state. Reports keep authored, created, modified, and PTP observations separate; missing capture dates are never replaced by file dates. The package's public date properties are projections, so the source timezone and raw timestamp representation remain unknown. The current package API also leaves reported format/content type and a separate PTP date unavailable; reports mark those gaps explicitly.

See the [physical-device verification procedure](docs/compatibility/README.md), [backend evidence limits](docs/compatibility/backend-notes.md), [product specification](docs/specs.md), and [work status](docs/work-status.md).
