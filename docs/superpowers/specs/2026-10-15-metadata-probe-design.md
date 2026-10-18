# Milestone 1: metadata feasibility probe

Status: approved by the user on 2026-10-15. Implements milestone 1 of [the product specification](../../specs.md), sections 3, 4, 8, and 11.

## Purpose and acceptance

Provide a Windows CLI that shows what a connected phone exposes over USB before any synchronization engine is built. The user needs evidence that photo capture dates and phone identity can support the existing date-based import workflow, followed by manual organization in Picasa.

Deliver `list-devices` and `probe --device <id-or-name> [--output metadata.json]`. The probe reads metadata only, never media contents, and never touches synchronization configuration. Automated verification establishes CLI and probe behavior; physical Android and iPhone testing establishes compatibility independently. Shipping the CLI alone does not complete the milestone.

## Approach and alternatives

Use MediaDevices 2.0.0 behind a small metadata-only device boundary. This follows the product spec while allowing tests to supply fake devices. Keep the existing PhotoImport project and namespace; set the executable assembly name to `phone-photo-sync` and target `net10.0-windows`.

Direct WPD COM integration would provide more control but increases interop work before we know it is needed. Separate Android and iPhone libraries would add dependencies before feasibility evidence justifies them. Start with MediaDevices; add a narrowly scoped WPD property reader only if inspection shows a required property is otherwise inaccessible. An unsuccessful iPhone probe is a documented result, not evidence of Android failure or justification to invent capture dates.

## Commands and selection

- `list-devices`: show available portable devices, friendly name, manufacturer, model, transport, and all exposed device identifier properties. Identify USB transport when available; report unknown transport explicitly rather than guessing from a name.
- `probe --device <id-or-name>`: select an exact reported device ID or an unambiguous friendly name, connect with read access, discover storage/photo roots, enumerate photo metadata, and print the report.
- `probe --device <id-or-name> --output metadata.json`: additionally export the same report as indented JSON. Fail if the output file exists; do not overwrite unrelated files.
- `--help`: show these commands and phone unlock, Android file-transfer, and iPhone trust prerequisites.

Persistent aliases belong to later enrollment work. For this milestone a unique friendly name is the configuration-free selection convenience. Multiple matching names are an error with IDs listed for disambiguation. Empty discovery is a useful `list-devices` result; a requested but absent device is a probe error.

## Boundaries and files

- `Program.cs`: process entry, cancellation handling, and composition.
- `Cli/ProbeCli.cs`: argument validation, device selection, output, and exit codes.
- `Devices/IPhotoDevice.cs`: metadata-only session contract plus device discovery abstraction; disposal closes the connection. Do not expose a media-content method to the probe.
- `Devices/WpdPhotoDevice.cs`: MediaDevices adapter, explicit read-only connection, discovery and property reads, resource cleanup.
- `Models/DeviceInfo.cs`: reported device properties and identifier candidates, each labeled with its source and unverified stability.
- `Models/PhotoMetadata.cs`: object identity/path, name, size, content type, format, and separate authored/created/modified/PTP timestamp observations.
- `Models/ProbeReport.cs`: device, photo observations, errors, root coverage, elapsed enumeration duration, and counts.
- `Services/MetadataProbe.cs`: traversal, photo recognition, aggregation, and cancellation.
- `Reporting/ProbeReportWriter.cs`: table and JSON rendering.
- `tests/PhotoImport.Tests/`: xUnit tests and fake metadata devices. Exclude nested test source files from the executable project's default compile glob; add the test project to the solution.
- `docs/compatibility/README.md`: reproducible physical-device procedure and report format, with actual results recorded per phone after testing.

The eventual content-stream capability required by the product architecture is deferred until a separate, explicitly invoked transfer validation is designed. The probe cannot call a download API through its abstraction.

## Metadata fidelity

Record source path, filename, nullable byte size, content type and format, session WPD object ID, persistent object identifier where exposed, and each timestamp property independently. Do not use WPD object IDs as stable device or photo identities.

For each timestamp record property source, original value and runtime/variant type when accessible, a diagnostic display value, offset presence (`present`, `absent`, or `unknown`), availability, and any read error. If MediaDevices returns only a converted `DateTime`, label it as an API-projected value: it does not prove that the raw transport value or original timezone is available. Inspect the package implementation before deciding whether a lower-level property read is necessary. Unavailable raw values must remain explicitly unavailable.

Do not call `ToLocalTime` or `ToUniversalTime`, replace missing authored/PTP dates with created/modified dates, or select a synchronization date policy. Count photos with an exposed authored or PTP capture-date candidate separately from photos without one. A candidate is not yet a validated capture timestamp; show per-source counts so authored and PTP coverage can be assessed independently.

## Enumeration and errors

Discover accessible storage roots and recursively identify photo roots, including `DCIM` and `Pictures` under each storage. Record roots searched and folders rejected or inaccessible. Use an explicit traversal stack and visited object identifiers to avoid duplicate traversal or cycles. Retain supported still images with case-insensitive extensions from the product spec; retain reported format evidence and flag contradictions rather than silently discarding diagnostic evidence. Do not enumerate thumbnails or request previews.

Continue past isolated folder or property errors, retaining partial observations and structured errors with operation, path/object, and diagnostic text. A disconnect or cancellation stops enumeration. A failed enumeration must never appear to be a complete inventory.

Exit codes: `0` completed command, `2` invalid arguments or ambiguous selection, `3` device discovery/connection failure, `4` incomplete enumeration, `5` report output failure, `130` cancellation. Missing capture dates alone do not fail this diagnostic command. With `--output`, export partial reports with completion status and errors when possible, even if enumeration fails; preserve cancellation as a non-success outcome.

## Verification

Automated tests will cover exact ID and unique-name selection; duplicate names; absent devices; recursive discovery across storage roots; case-insensitive extensions; duplicate/cyclic folders; missing and contradictory metadata; timestamp preservation without timezone conversion; access denied; disconnect; cancellation/disposal; JSON nulls and provenance; occupied output paths; and command exit codes. Fake devices expose metadata only so probe tests cannot transfer image content.

Build and test the full solution, then exercise help, invalid arguments, device discovery, and an available-device probe. Library inspection must confirm the actual access mode and metadata operations; fake tests alone cannot prove hardware behavior.

Each physical-phone report records date, phone/OS, backend version, USB/trust setup, identifier candidates before and after reconnect/restart, folder coverage, authored/PTP/created/modified semantics, EXIF/Picasa comparisons, timestamp offset evidence, missing-date rate, counts, duration, and evidence about content transfer. Use representative photos from multiple days, close timestamps, screenshots, edited/shared images, and JPEG/HEIC where available. Original/full-resolution access remains unverified until a separately authorized transfer test; do not claim it from metadata size alone.

## Scope and open evidence

No sync engine, enrollment, checkpoint changes, configuration store, retry journal, or automatic compatibility claims in this milestone. Phone identity stability, timestamp accuracy, iPhone support, and original access remain empirical questions. Record a platform as untested until a real device has been assessed and as blocked if its evidence fails the product spec's exit criteria.

## Sources reviewed

- Product specification: `docs/specs.md`.
- MediaDevices repository: https://github.com/Bassman2/MediaDevices
- Pinned package and runtime compatibility: https://www.nuget.org/packages/MediaDevices/2.0.0
- WPD object properties: https://learn.microsoft.com/en-us/windows/win32/wpd_sdk/object-properties
- WPD device properties: https://learn.microsoft.com/en-us/windows/win32/wpd_sdk/device-properties

The precise MediaDevices property surface and raw-value fidelity still require source/package inspection during implementation; no unsupported API signatures are assumed by this design.
