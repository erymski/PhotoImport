# Metadata Feasibility Probe Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers: subagent-driven-development or superpowers: executing-plans to implement this plan task-by-task. Steps use checkbox syntax for tracking.

**Goal:** Build a read-only USB metadata diagnostic CLI and establish a reproducible Android/iPhone compatibility assessment.

**Architecture:** A MediaDevices adapter exposes only discovery and metadata operations through an application-owned interface. A probe service traverses device storage and builds a report; a CLI selects the device and renders text/JSON. Physical-device evidence is recorded separately from automated correctness.

**Tech Stack:** C#, .NET 10 Windows console application, MediaDevices 2.0.0, System.Text.Json, xUnit.

**Spec:** [approved milestone design](../specs/2026-10-15-metadata-probe-design.md); [product specification](../../specs.md), especially sections 3, 4, 8, and 11.

## Global Constraints

- Platform: Windows 10/11, C# / .NET 10 console application.
- Never delete or modify media on the phone.
- List photo metadata **before** downloading photo contents.
- Perform no media-content downloads, writes to the phone, or changes to sync state.
- Preserve raw timestamp values and report whether timezone/offset information is present.
- Do not silently substitute file creation or modification time for capture time.
- Persistent WPD object handles must **not** be treated as stable identifiers across sessions.
- Still images: `.jpg`, `.jpeg`, `.heic`, `.heif`, `.png`, `.dng`, `.tif`, `.tiff`, matched case-insensitively.
- Keep task progress in this document and `docs/work-status.md`; do not use Beads.
- Do not include later synchronization, enrollment, checkpoint, or transfer implementations.

## Review Focus

1. Identical phone names: fail ambiguous selection and show device IDs; Task 4.
2. Folder aliases/cycles and multiple storage volumes: terminate and avoid duplicate objects without losing a second storage tree; Task 2.
3. API-converted dates: distinguish raw evidence from projections and retain wall-clock values unchanged; Tasks 1 and 3.
4. Disconnect or Ctrl+C after some observations: preserve a partial report and dispose the device; Tasks 2 and 4.
5. Existing JSON target or failed export: preserve existing files and return an output error; Task 4.

## File structure and shared contracts

Keep the existing root executable project. Set `TargetFramework` to `net10.0-windows` and `AssemblyName` to `phone-photo-sync`. Exclude `tests/**/*.cs` from its compile items. Create `tests/PhotoImport.Tests/PhotoImport.Tests.csproj` targeting the same framework and referencing the executable project; add it to `PhotoImport.slnx`. Use compatible stable Microsoft.NET.Test.Sdk, xunit, and xunit.runner.visualstudio packages, pinning the resolved versions at setup. Add recursive `bin/` and `obj/` ignores so test outputs stay untracked.

Application contracts below are independent of the library's API. All model types are public records in namespace `PhotoImport.Models`; interfaces are public in `PhotoImport.Devices`.

- `DeviceInfo(string Id, string? FriendlyName, string? Manufacturer, string? Model, string? Transport, IReadOnlyDictionary<string, string?> Identifiers)`: ID is a selection token, not a promise of stability. Console/JSON labels all identity candidates unverified.
- `DeviceEntry(string ObjectId, string Path, string Name, bool IsDirectory)`: session-scoped traversal object.
- `TimestampObservation(string Source, string? RawValue, string? RawType, string? DisplayValue, string OffsetPresence, string Availability, string? Error)`: offset values `present|absent|unknown`; availability values `raw|projected|missing|error`. Store dates as invariant strings, never timezone-normalized.
- `PhotoMetadata(string ObjectId, string? PersistentObjectId, string SourcePath, string FileName, ulong? SizeBytes, string? ContentType, string? Format, TimestampObservation DateAuthored, TimestampObservation DateCreated, TimestampObservation DateModified, TimestampObservation PtpCaptureDate, IReadOnlyList<string> Warnings)`.
- `ProbeError(string Operation, string? ObjectId, string? Path, string Message)`.
- `ProbeReport(DeviceInfo Device, DateTimeOffset StartedAt, TimeSpan EnumerationDuration, string CompletionStatus, IReadOnlyList<string> SearchedRoots, IReadOnlyList<PhotoMetadata> Photos, IReadOnlyList<ProbeError> Errors)`: status `complete|incomplete|cancelled`. Computed counts: `PhotoCount`, `WithCaptureDateCount`, `WithoutCaptureDateCount`, `WithAuthoredDateCount`, `WithPtpDateCount`; capture means a nonmissing/nonerror authored or PTP observation, never created/modified.
- `IPhotoDeviceProvider.Discover(CancellationToken cancellationToken) -> IReadOnlyList<DeviceInfo>`; `Open(DeviceInfo device, CancellationToken cancellationToken) -> IPhotoDevice`.
- `IPhotoDevice : IDisposable`: `GetStorageRoots(CancellationToken cancellationToken) -> IReadOnlyList<DeviceEntry>`; `GetChildren(DeviceEntry directory, CancellationToken cancellationToken) -> IEnumerable<DeviceEntry>`; `ReadPhotoMetadata(DeviceEntry file, CancellationToken cancellationToken) -> PhotoMetadata`. Review correction: child enumeration is streamed so observations survive later iterator failures.
- `DeviceDisconnectedException(string message, Exception? innerException = null)`: adapter signals terminal loss of connection; isolated read failures retain their original exceptions.

No application device interface exposes download, resource stream, write, delete, or thumbnail access.

## Task 1: metadata evidence models and test harness

**Files:** modify `PhotoImport.csproj`, `PhotoImport.slnx`, `.gitignore`; create `Models/DeviceInfo.cs`, `Models/PhotoMetadata.cs` (including TimestampObservation), `Models/ProbeReport.cs` (including ProbeError), `Devices/IPhotoDevice.cs` (including provider, entry, and disconnected exception); create `tests/PhotoImport.Tests/PhotoImport.Tests.csproj`, `MetadataEvidenceTests.cs`, and `Fakes/FakePhotoDevice.cs` under the test directory.

**Interfaces:** produces all shared contracts above. The fake has in-memory roots, children, metadata, injected failures, and an `IsDisposed` flag; it must implement the same metadata-only interface.

- [x] Create the test project and solution wiring without implementing models. Add `MissingCaptureDateDoesNotUseCreatedOrModified`: a photo with missing authored/PTP dates and populated created/modified values has `WithCaptureDateCount == 0` and `WithoutCaptureDateCount == 1`.
- [x] Add `TimestampEvidenceRoundTripsWithoutConversion`: JSON round-trip preserves `2026-10-12T14:32:05`, explicit `2026-10-12T14:32:05+02:00`, raw type, unknown offset evidence, and null missing fields exactly. Add `ProjectedValueIsNotReportedAsRaw`: projected observations retain null RawValue/RawType.
- [x] Run `dotnet test tests/PhotoImport.Tests/PhotoImport.Tests.csproj --filter MetadataEvidenceTests`. Expect failure because production contracts are missing, rather than an unrelated restore error.
- [x] Implement the shared contracts, count calculations, and fake. Do not infer offset presence from a projected DateTime.Kind.
- [x] Repeat the filtered test command; expect all tests passing. Run `dotnet build PhotoImport.slnx`; expect success and no duplicate test compilation in the executable.
- [x] Commit the explicit Task 1 file set with message `feat: define metadata evidence contracts and tests`.

## Task 2: recursive metadata probe

**Files:** create `Services/MetadataProbe.cs`; create `tests/PhotoImport.Tests/MetadataProbeTests.cs` and extend the fake only as needed.

**Interfaces:** consumes Task 1 contracts. Produces `MetadataProbe.Run(IPhotoDevice device, DeviceInfo deviceInfo, CancellationToken cancellationToken) -> ProbeReport` in namespace `PhotoImport.Services`. Caller owns device disposal.

- [x] Add `FindsCameraAndPicturesAcrossStorageRoots`: fake volumes contain nested DCIM, Pictures, and a vendor photo folder; retain supported still images recursively, including uppercase extensions, and exclude video/text. Traverse accessible storage metadata to discover vendor photo folders rather than hard-code a single DCIM path. Record storage roots searched; never request thumbnails or content.
- [x] Add `RepeatedObjectAndCycleAreVisitedOnce`: aliases/cycles reuse object IDs and terminate with each image reported once; a distinct object on another volume remains included. Use session object IDs as visited keys; for absent/empty IDs use exact device paths and emit a diagnostic warning.
- [x] Add `DirectoryFailurePreservesOtherBranches`: one access-denied branch produces an error with operation/path/object and status `incomplete`, while another branch still contributes photos. Add `PropertyFailurePreservesPhoto`: a missing/failed date observation does not remove the image.
- [x] Add `DisconnectPreservesPartialInventory` and `CancellationPreservesPartialInventory`: after one discovered photo, injected disconnection returns `incomplete`, cancellation returns `cancelled`, and already observed photos remain. Add `EmptyDeviceCompletes` and `UnknownFormatKeepsImageWithWarning`.
- [x] Run `dotnet test tests/PhotoImport.Tests/PhotoImport.Tests.csproj --filter MetadataProbeTests`; expect missing service failures.
- [x] Implement an explicit traversal stack for directories, consume each file as yielded, maintain a visited set, per-branch errors, terminal disconnect/cancellation handling, and monotonic Stopwatch duration. Missing capture dates alone leave status `complete`; contradictory extension/format evidence adds a warning. No synchronization filtering.
- [x] Repeat the filtered tests; expect pass. Commit this task's files with message `feat: enumerate photo metadata with partial diagnostics`.

## Task 3: read-only MediaDevices adapter

**Files:** modify `PhotoImport.csproj` to pin MediaDevices 2.0.0; create `Devices/WpdPhotoDevice.cs`, `Devices/WpdPhotoDeviceProvider.cs`, `Devices/WpdMetadataMapper.cs`; create `tests/PhotoImport.Tests/WpdMetadataMapperTests.cs`; create `docs/compatibility/backend-notes.md`.

**Interfaces:** provider and device implement Task 1 interfaces. `WpdMetadataMapper.ObserveTimestamp(string source, object? value, bool rawValueAvailable, string? error = null) -> TimestampObservation` maps metadata evidence. WPD connection and object conversion remain internal to the adapter. Do not invent or assume MediaDevices method names from this plan.

- [x] Inspect the pinned package's public API/XML and matching upstream source for discovery, read-access connection, storage roots, object ID/path, size, format/content type, all identity properties, authored/created/modified dates, PTP date, and raw property reads. Record exact methods and evidence limitations in backend-notes.md. Use official upstream source, not third-party examples. If read-only connection is unavailable, stop and revise the adapter approach before opening a phone.
- [x] Add `NullDateRemainsMissing`, `ProjectedDatePreservesWallClockAndUnknownOffset`, `RawOffsetDateRetainsOffset`, and `FailedPropertyRetainsError`: assert raw/projected/missing/error availability and invariant strings without UTC/local conversion. A projected DateTime retains no fabricated raw value; DateTimeOffset's offset is only raw evidence when `rawValueAvailable` is true.
- [x] Run `dotnet test tests/PhotoImport.Tests/PhotoImport.Tests.csproj --filter WpdMetadataMapperTests`; expect missing mapper failures.
- [x] Implement the mapper and adapter against the inspected API. Open read-only; retrieve properties independently so one unsupported property cannot discard the object or all identity evidence. Map unavailable PTP/raw properties explicitly to missing rather than inventing fallbacks. Use a narrow WPD property reader only where inspected API limitations require it; document any such addition before using it.
- [x] Ensure connection failures dispose any partially created session; dispose discovery handles; check cancellation between synchronous metadata calls. Translate verified disconnect HRESULTs to DeviceDisconnectedException and leave access/unsupported-property failures distinguishable.
- [x] Repeat mapper tests and run `dotnet build PhotoImport.slnx`; expect pass. Review adapter call sites against source to confirm no content-resource, thumbnail, download, mutation, or default write-access connection is used. Record that code inspection does not establish driver behavior on hardware.
- [x] Commit this task's files with message `feat: add read-only WPD metadata backend`.

## Task 4: CLI and text/JSON reports

**Files:** replace `Program.cs`; create `Cli/ProbeCli.cs`, `Reporting/ProbeReportWriter.cs`; create `tests/PhotoImport.Tests/ProbeCliTests.cs`, `tests/PhotoImport.Tests/ProbeReportWriterTests.cs`, and `tests/PhotoImport.Tests/Fakes/FakePhotoDeviceProvider.cs`.

**Interfaces:** `ProbeCli(IPhotoDeviceProvider provider, TextWriter output, TextWriter error)` exposes `Run(string[] args, CancellationToken cancellationToken) -> int`; it uses Task 2's probe. `ProbeReportWriter.WriteConsole(ProbeReport report, TextWriter output) -> void`; `WriteJson(ProbeReport report, string path) -> void`. The writer uses indented camelCase JSON, serializes nulls, and creates output with FileMode.CreateNew. `Program.Main(string[] args) -> int` wires the real provider and Ctrl+C token, unregistering the handler on exit.

- [x] Add argument tests for help, unknown command/flag, duplicate flags, missing values, empty device selector, absent `--device`, `--output` on list-devices, and invalid output path. Usage/ambiguity returns 2; invalid/unwritable output returns 5. Help returns 0 without discovery.
- [x] Add `ExactIdWinsBeforeFriendlyName`, `UniqueNameSelectsDevice`, `DuplicateNameShowsIdsAndReturns2`, `AbsentDeviceReturns3`, and `EmptyDiscoveryListsNoDevicesAndReturns0`. Names compare ordinal-ignore-case; IDs compare ordinal exactly. Discovery/connection exceptions return 3 with readable diagnostics.
- [x] Add output tests for all metadata fields, explicit missing markers, timestamp source and uncertainty, unverified identity candidates, format warnings, roots, elapsed time, photo and per-source capture counts, and completion/errors. Console photo rows include filename, size, authored, PTP, created, and modified; JSON contains the full evidence.
- [x] Add `ExistingOutputFileRemainsByteIdentical` (return 5), `PartialReportExportsAndReturns4`, `CancelledProbeExportsPartialAndReturns130`, `DeviceDisposedAfterSuccessFailureAndCancellation`, and `OutputFailureReturns5`. Cancellation retains exit 130 even if exporting its partial report fails; write the export failure to stderr. Other export failures return 5.
- [x] Run `dotnet test tests/PhotoImport.Tests/PhotoImport.Tests.csproj --filter "FullyQualifiedName~ProbeCliTests|FullyQualifiedName~ProbeReportWriterTests"`; expect failures because CLI/writer are missing.
- [x] Implement parsing before device access, selection, scoped disposal, report rendering/export, and exit mapping. Only explicitly supplied `--output` writes a report. Do not read or create `%LOCALAPPDATA%\PhonePhotoSync` state. Missing dates alone return 0 when enumeration succeeds.
- [x] Repeat filtered tests; expect pass. Run `dotnet test PhotoImport.slnx` and `dotnet build PhotoImport.slnx --configuration Release`; expect all tests/build passing.
- [x] Smoke-test `dotnet run --project PhotoImport.csproj -- --help`, `dotnet run --project PhotoImport.csproj -- invalid-command`, and `dotnet run --project PhotoImport.csproj -- list-devices`; expect exit 0, 2, and a truthful discovery outcome respectively. Do not claim device compatibility from successful discovery alone.
- [x] Commit this task's files with message `feat: expose metadata probe CLI and diagnostic reports`.

## Task 5: physical-device acceptance procedure and results

**Files:** create `docs/compatibility/README.md`; add `docs/compatibility/android-<tested-model>.md` and `docs/compatibility/iphone-<tested-model>.md` only for phones actually tested; update `docs/work-status.md`.

**Interfaces:** consumes the shipped CLI and JSON schema. Produces written evidence against product specification section 3.2. No new download command or import engine.

- [x] Write executable examples for build, list-devices, probe, and probe with a new JSON output path. Explain unlock/file-transfer/trust setup, unknown/missing fields, exit codes, and how to avoid committing private full-device reports. Treat raw probe exports as local evidence unless the user explicitly wants to share them.
- [x] Define a repeatable comparison sheet: phone/model/OS, date/backend, transport, identifiers across unplug/replug and app restart, folder and expected-photo counts, timestamp samples against EXIF/Picasa, timezone interpretation, missing-date rate, duration, driver/conversion observations, and no-content-transfer evidence. Cover multiple days, closely timed photos, screenshots, shared/edited files, JPEG/HEIC when present.
- [ ] Run the metadata probe on an available Android phone and an available iPhone independently. If a phone is unavailable or requires physical unlock/trust, record that limitation and request the physical action; do not fabricate results or mark that acceptance step done.
- [ ] Document per-phone status `untested`, `blocked`, or `metadata verified; selected-file/original access pending` using actual evidence. If MediaDevices cannot expose reliable iPhone capture metadata, record the failure and propose a separate backend investigation rather than substituting created/modified times.
- [x] Keep selected-file/original transfer validation explicitly pending until a separate transfer test is invoked. Neither metadata tests nor reported file size proves original transfer capability.
- [ ] Verify README commands against the implemented CLI, run final solution tests/build after any code correction, update status with exact evidence and remaining hardware gates, and commit documentation with message `docs: add phone metadata compatibility procedure and evidence`.

## Plan review and execution

Self-review: every approved milestone requirement maps to Tasks 1–5; shared signatures are consistent; each Review Focus item has an owning test. Physical compatibility and original access remain evidence gates, not automated-test claims. The scope does not implement later milestones.

Recommended execution: **native**. The five tasks share a small set of contracts and one backend; implementing them in this session keeps interface changes coherent. Run the required fresh whole-change review before claiming implementation complete. Subagent execution is available if the user prefers independent implementation/review per task.

Approved by the user on 2026-10-15 with native execution in the current checkout on feat/metadata-probe. Tasks 1–4 are implemented and verified. Task 5 produced a Samsung S21 partial assessment on 2026-10-16: capture timestamps unavailable, current backend not accepted for sync. iPhone, complete coverage, verified capture metadata, and selected-file/original access remain pending. See docs/work-status.md and docs/compatibility/android-samsung-sm-g991b.md for evidence and remaining work.
