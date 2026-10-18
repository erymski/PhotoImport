# Repeatable Filename-Day Importer Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking. Native execution in the current checkout and one final independent review were already selected by the user.

**Goal:** After one-time registration, detect the connected registered Android phone, copy new camera photos/videos/screenshots using filename days, and safely preserve JSON progress across runs.

**Architecture:** Add a cohesive `Importing/` subsystem with pure selection, scoped metadata inventory, JSON state/journal persistence, full-stream transfers and an orchestrator. Keep existing diagnostics intact and add a separate media-content capability for the importer. Persist prepared/completed transfer evidence before advancing the checkpoint atomically.

**Tech Stack:** Windows, C#/.NET10, MediaDevices2.0.0, MetadataExtractor2.9.3 already installed; xUnit and real temporary files/fake device streams. No new packages.

**Spec:** `docs/superpowers/specs/2026-10-18-filename-importer-design.md` (approved by the user following commit13a3e7a). Read both spec and plan before implementation.

## Global Constraints

- Preserve current checkout/branch `feat/metadata-probe`; no worktree, merge or push. Markdown tracking; ignore Ponytail and do not use Beads.
- No phone reads, actual registration, production state writes or copy validation during implementation. Hardware acceptance is separate.
- Source roots: `\Internal storage\DCIM\Camera`, `\Internal storage\DCIM\Screenshots`, `\Internal storage\Samsung\Pictures\Camera Roll`; subsets/subfolders allowed; no arbitrary roots. Skip `.thumbnails` and `.trash`.
- Still extensions: `.jpg`, `.jpeg`, `.png`, `.heic`, `.heif`, `.dng`, `.tif`, `.tiff`. Video extensions: `.mp4`, `.mov`, `.m4v`, `.3gp`, `.3g2`, `.avi`, `.mkv`, `.webm`, `.mts`, `.m2ts`.
- Filename date patterns, calendar validity and whole initial cutoff-day exclusion follow the approved spec exactly. S21 initial cutoff is2026-09-03; registration still requires an explicit supplied cutoff.
- Unknown/invalid names are visible intentional skips; no EXIF/modification-time fallback. No EXIF content reads for selection; no conversion or phone changes.
- State: `%LOCALAPPDATA%\PhonePhotoSync\config.json`; per-device checkpoints, global exclusive writer lock, flushed atomic JSON replacement with configuration backup. Never silently migrate/reset state.
- Full streams to EOF, known nonzero sizes,2GiB free-space reserve, no overwrite, one stream at a time. No automatic retries or byte-range resume. SHA-256 is local verification, not phone-side proof.
- Existing diagnostic commands and bounded-reader limits remain intact. iPhone importer support deferred. Do not run or modify `C:\Projects\photo-sort`.
- Exit codes:0 success/intentional skips;2 arguments/config/selection;3 device/identity/root access;4 incomplete/source/transfer/verification;5 local I/O/state;130 precommit cancellation. A postcommit archival/report failure exits5 and explicitly reports committed progress.

## Review Focus

1. Two physically different phones with the same friendly name: never borrow a checkpoint; identity mismatch/ambiguity stops copying (Task2).
2. A collision appears between planning and final rename: preserve the existing file and select/record another destination without overwrite (Task5).
3. Power loss after rename or configuration replacement: prepared/ready journal evidence recovers without duplicate copies or checkpoint rollback (Tasks5–6).
4. A staging folder is replaced with a junction/reparse point: refuse using it to write outside the intended destination (Task5).
5. Unknown filename media coexist with valid new media: report skips while importing valid candidates; dry run creates no media/state/journal files (Tasks1,7).

## File map and shared types

Create production files under `Importing/`: `ImportModels.cs` (state/plan/result DTOs), `FilenameDayPolicy.cs` (classification/parser), `FilenameImportPlanner.cs` (pure filtering/key creation), `ImportIdentity.cs` (registration matching), `ScopedMediaInventory.cs` (enumeration), `ImportStateStore.cs` (atomic persistence/lock), `ImportDestination.cs` (safe paths/collisions), `MediaTransfer.cs` (single-file transfer/recovery), `ImportRunner.cs` (session/checkpoint lifecycle), `ImportCli.cs` (commands). Add `Devices/IMediaContentDevice.cs`; modify `Devices/WpdPhotoDevice.cs`, `Program.cs`, and existing help in `Cli/ProbeCli.cs`. Mirror production responsibilities in `tests/PhotoImport.Tests/`; add only importer-specific fakes under its `Fakes/` directory. Finish with README/spec/handoff/status updates.

Use namespace `PhotoImport.Importing`. DTOs serialize camelCase, dates as `yyyy-MM-dd`, UTC audit timestamps as ISO8601, SHA-256 as uppercase hex. Define these named contracts in Task1, adding persistence-only DTOs in Task4:

- `ImportCheckpoint(DateOnly Day, string Mode, IReadOnlyList<string> BoundaryKeys)`; modes `completedDay`, `tracked`.
- `ImportIdentityRecord(string Mode, string? SerialNumber, string DeviceId, string? Manufacturer, string? Model)`; modes `serial`, `deviceId`.
- `ImportRegistration(Guid Id, string Alias, string Destination, IReadOnlyList<string> Roots, ImportIdentityRecord Identity, string DatePolicy, ImportCheckpoint Checkpoint)`.
- `ImportConfiguration(int SchemaVersion, string Kind, IReadOnlyList<ImportRegistration> Devices)`; schema1, kind `filenameImporter`; do not accept older diagnostic JSON as this format.
- `ImportInventory(IReadOnlyList<PhotoMetadata> Files, IReadOnlyList<string> Errors, IReadOnlyList<string> Warnings, bool Complete)`; legacy `PhotoMetadata` can describe listed video objects without changing diagnostics.
- `ImportCandidate(PhotoMetadata Source, DateOnly Day, string Key, string Kind)`; kind `photo` or `video`.
- `ImportPlanRow(PhotoMetadata Source, DateOnly? Day, string? Key, string Decision, string Reason)`; decisions `eligible`, `older`, `alreadyImported`, `skipped`, `error`.
- `ImportPlan(IReadOnlyList<ImportPlanRow> Rows, IReadOnlyList<ImportCandidate> Candidates, IReadOnlyList<string> Errors)`; derived known byte total, skipped count and complete flag. Never sum through signed/unsigned overflow.

Use `long` for local bytes and reject device sizes exceeding `long.MaxValue`; sum checked. Existing `PhotoSourceInfo`/`PhotoReadHandle` describe revalidated source and stream. Do not reuse EXIF `DayPreviewPlanner` or reinterpret its source-provenance schema.

### Task1: Pure filename selection and checkpoint semantics

**Files:** Create `Importing/ImportModels.cs`, `FilenameDayPolicy.cs`, `FilenameImportPlanner.cs`; tests `FilenameDayPolicyTests.cs`, `FilenameImportPlannerTests.cs`.

**Interfaces:** `FilenameDayPolicy.Classify(string filename) -> string?`; `TryGetDay(string filename, out DateOnly day) -> bool`; `FilenameImportPlanner.Build(ImportRegistration registration, ImportInventory inventory) -> ImportPlan`; `CreateKey(Guid registrationId, PhotoMetadata source, DateOnly day) -> string`. Key is SHA-256 of unambiguously serialized registration ID/canonical path/positive size/day, independent of WPD handles.

- [x] **1. Write failing parser/planner tests.** Pin all spec prefixes/extensions, date-only WP names, invalid/leap dates, prefix delimiters and case-insensitive extensions. Pin these assertions:
  ```csharp
  Assert.False(FilenameDayPolicy.TryGetDay("20260230_120000.jpg", out _));
  Assert.True(FilenameDayPolicy.TryGetDay("WP_20150621_023.jpg", out var day));
  Assert.Equal(new DateOnly(2015, 6, 21), day);
  ```
  `CompletedDay_ExcludesSeptember3_SelectsSeptember4` asserts equality excluded; `TrackedDay_AllowsAbsentKey_KeepsKnownKeyExcluded` asserts same-day additions; `UnknownName_IsSkipped_WhileValidNewFileRemainsEligible` asserts unknown names do not make the plan incomplete. Unknown sizes block only otherwise eligible candidates; duplicate canonical paths and overflow fail planning. Same path/size/day under another registration has a different key.
- [x] **2. Run red:** `dotnet test PhotoImport.slnx --configuration Release --filter "FullyQualifiedName~FilenameDayPolicyTests|FullyQualifiedName~FilenameImportPlannerTests"`; expect new-contract compile errors or asserted missing behavior, not environment errors.
- [x] **3. Implement the named DTOs and pure methods.** Store canonical source paths, validate checkpoint mode/key shape/date policy, sort ordinally and preserve skip reasons. Reuse no EXIF reader. Reject inconsistent source basename/path association.
- [x] **4. Run the same tests green**, then the existing `DayPreviewPlannerTests` to establish its independent semantics remain intact.
- [x] **5. Commit only these files:** `feat: add filename-day import planning`.

### Task2: Registration identity and scoped media inventory

**Files:** Create `Importing/ImportIdentity.cs`, `ScopedMediaInventory.cs`; tests `ImportIdentityTests.cs`, `ScopedMediaInventoryTests.cs`; reuse existing metadata fakes.

**Consumes:** Task1 registration/inventory contracts; `IPhotoDeviceProvider`, `IPhotoDevice`, `DeviceInfo`, `DeviceFolderScope`.
**Produces:** `ImportIdentity.Enroll(DeviceInfo device) -> ImportIdentityRecord`; `Matches(ImportIdentityRecord identity, DeviceInfo device) -> bool`; `Select(IReadOnlyList<ImportRegistration> registrations, IReadOnlyList<DeviceInfo> devices, string? alias) -> (ImportRegistration Registration, DeviceInfo Device)` (throw a typed importer validation/device exception on no/ambiguous match). `ScopedMediaInventory.ValidateRoots(IReadOnlyList<string> roots) -> void`; `Read(IPhotoDevice device, IReadOnlyList<string> roots, CancellationToken token) -> ImportInventory`.

- [x] **1. Write failing tests.** `SameFriendlyName_DifferentSerial_DoesNotMatch`; `DuplicatePhysicalMatches_RequireUnambiguousSelection`; serial enrollment includes only available manufacturer/model and requires those fields later; fallback requires exact enrolled device ID. Missing serial on a serial-enrolled phone fails. Canonical folder tests include video enumeration, subfolder allowance, sibling-prefix escapes, missing root, nested/duplicate roots, cycle/duplicate-path errors, `.trash`/`.thumbnails` exclusion, corrupt child paths and enumeration/metadata exceptions. Assert no content capability is used.
- [x] **2. Run red:** `dotnet test PhotoImport.slnx --configuration Release --filter "FullyQualifiedName~ImportIdentityTests|FullyQualifiedName~ScopedMediaInventoryTests"`.
- [x] **3. Implement matching and enumeration.** Resolve through `DeviceFolderScope`; iterate allowed media metadata directly rather than broadening `MetadataProbe`'s photo-only filter. A repeated directory is an error, not evidence of complete coverage. Preserve device/backend warnings; never select by friendly name after registration. Add typed importer exceptions to `ImportModels.cs` for CLI mapping, with distinct config/device/incomplete categories.
- [x] **4. Run green**, plus `DeviceFolderScopeTests`/`MetadataProbeTests`. Check unscoped diagnostics retain prior traversal behavior.
- [x] **5. Commit:** `feat: add scoped media inventory and registered identity matching`.

### Task3: Separate full-media USB content capability

**Files:** Create `Devices/IMediaContentDevice.cs`, tests `MediaSourcePathTests.cs`; modify `Devices/WpdPhotoDevice.cs`; create importer fake `tests/PhotoImport.Tests/Fakes/FakeImportDevice.cs` implementing metadata and new content capability.

**Consumes:** existing `PhotoReadHandle`, `PhotoSourceInfo`, `WpdOperation` and filename extension classification.
**Produces:** `IMediaContentDevice.OpenMediaRead(string sourcePath, CancellationToken token) -> PhotoReadHandle`; `MediaSourcePath.IsValid(string path) -> bool` in the new capability file. `WpdPhotoDevice` implements the interface.

- [x] **1. Write failing path tests.** Supported photos/videos with absolute canonical paths accepted; relative paths, traversal, alternate separators, unsupported extensions and malformed components rejected. Existing EXIF path validators still reject videos. Fake content records opened paths and supplies declared source metadata independently of stream bytes.
- [x] **2. Run red:** `dotnet test PhotoImport.slnx --configuration Release --filter "FullyQualifiedName~MediaSourcePathTests"`.
- [x] **3. Add the separate capability.** Validate importer paths, resolve `GetFileInfo`, verify returned canonical path, return current size/persistent ID with `WpdReadStream`. Share a private read helper where useful without weakening `OpenPhotoRead`/`OpenJpegRead`. Content capability never enrolls a device or performs selection.
- [x] **4. Run green**, plus `JpegExifReaderTests`, `MediaExifReaderTests`, `WpdReadStreamTests`. No physical USB invocation.
- [x] **5. Commit:** `feat: expose separate full-media read capability`.

### Task4: Locked JSON configuration and recoverable journal persistence

**Files:** Create `Importing/ImportStateStore.cs`; extend `ImportModels.cs`; tests `ImportStateStoreTests.cs` and shared fixture `Fakes/ImportFixture.cs` (temporary state/destination directories, registration and fake device builders).

**Produces:** `ImportStateStore(string stateDirectory, Action<ImportWritePoint>? fault = null)` with `AcquireLock() -> IDisposable`, `ReadConfiguration() -> ImportConfiguration?`, `WriteConfiguration(ImportConfiguration configuration) -> void`, `ReadPending(Guid registrationId) -> PendingImport?`, `WritePending(PendingImport pending) -> void`, `Archive(PendingImport pending, ImportRunResult result) -> void`. Operations require caller-held lock; `AcquireReadLock()` opens an existing lock for a consistent read view without creating/writing it. Init creates state/lock only when real registration begins.

Define `PendingImport` with schema/session/registration IDs, enrolled identity/destination/roots/date policy/base checkpoint, file entries, stage (`copying`, `readyToCommit`, `committed`) and optional exact new checkpoint. `PendingImportFile` stores source metadata/day/key/kind, final/partial paths, stage (`planned`, `prepared`, `completed`), verified length/hash. `ImportRunResult` stores counts/bytes/skips/warnings/errors, old/new checkpoints, started/finished timestamps, `CheckpointCommitted` and `ExitCode` using the Global Constraints mapping. These are persistence DTOs, not device objects. `ImportWritePoint` provides internal test fault hooks immediately before/after atomic replacement for config/journal and before archive; production defaults to no hook. If no state directory exists, status/dry-run first-use handling returns without acquiring/creating a read lock.

- [x] **1. Write failing filesystem tests.** Initial config roundtrip; unsupported kind/schema, malformed dates/keys, duplicate aliases/IDs/identities, nonabsolute destination, noncanonical roots and invalid journal ownership rejected. Second lock holder fails. Atomic-replace failure leaves prior config readable; backup equals previous valid config. Corrupt config is never auto-restored/reset. Journal write failure retains last durable snapshot. `ReadOnlyView_DoesNotCreateStateDirectoryOrFiles` pins no-init behavior.
- [x] **2. Run red:** `dotnet test PhotoImport.slnx --configuration Release --filter "FullyQualifiedName~ImportStateStoreTests"`.
- [x] **3. Implement validation and atomic writes** using same-directory unique temp, flush-to-disk then no-overwrite initial move or `File.Replace` for existing config. Backup previous config; no backup rollback on read. Write journals with the same replacement discipline. Archive names include session ID and cannot overwrite prior history; archival is retryable if its matching archive already exists. Do not store test fault hooks in JSON.
- [x] **4. Run green** using isolated temporary directories; no writes to actual `%LOCALAPPDATA%`. Assert failure fixtures preserve unrelated files.
- [x] **5. Commit:** `feat: persist locked importer configuration and recovery journals`.

### Task5: Collision-safe full transfer and single-file recovery

**Files:** Create `Importing/ImportDestination.cs`, `MediaTransfer.cs`; tests `ImportDestinationTests.cs`, `MediaTransferTests.cs`.

**Consumes:** Tasks1,3,4 contracts. `ImportDestination(string destination)` provides `ChooseFinalPath(ImportCandidate candidate) -> string`, `CreatePartialPath(Guid sessionId, string key) -> string`, `ValidateOwnedPaths(PendingImport session, PendingImportFile file) -> void`. `MediaTransfer(Func<string,long>? freeBytes = null, Action<TransferPoint>? fault = null)` provides `Copy(IMediaContentDevice device, PendingImport session, PendingImportFile file, Action<PendingImport> persist, CancellationToken token) -> PendingImport` and `TryRecover(PendingImport session, PendingImportFile file, Action<PendingImport> persist, CancellationToken token) -> PendingImport`.

- [x] **1. Write failing destination/stream tests.** Preserve original safe basename; deterministic key suffix then numeric collision suffix. Reserved Windows names, ADS/traversal/rooted names fail safely. `CollisionAfterPreparedJournal_PreservesExistingFile_UsesNewRecordedPath` injects a racing file. `StagingJunction_IsRejected` uses a Windows junction where available (skip only with an explicit unsupported-environment reason); ordinary staging tested everywhere. Stream tests assert EOF bytes/size/local hash, source path/size/PUID mismatch, stream/flush errors, initial2GiB reserve plus selected bytes, per-file/during-write reserve, cancellation and no overwrite. Prepared final/partial with matching hash recover without opening USB; changed/missing output requires fresh copy; wrong hash never grants reuse.
- [x] **2. Run red:** `dotnet test PhotoImport.slnx --configuration Release --filter "FullyQualifiedName~ImportDestinationTests|FullyQualifiedName~MediaTransferTests"`.
- [x] **3. Implement destination and transfer methods.** Guard owned staging paths/reparse points on use; never remove unrelated outputs. Persist planned paths, stream once to owned `.partial` with incremental SHA-256 and flushed exact lengths, persist prepared evidence, finalize without overwrite, persist completed. `TransferPoint` hooks after prepared, after rename and after completed let tests simulate process death. Persist a changed final path before retrying a colliding rename. A restart may remove/restart only its own unverified partial; no ranged transfer, retry loop or full-stream fallback in bounded readers.
- [x] **4. Run green.** Assert partially copied files never appear as completed media. Verify cleanup does not mask a more important failure; all streams dispose on failure/cancellation.
- [x] **5. Commit:** `feat: copy full media with verified crash recovery`.

### Task6: Import orchestration, resume reconciliation and checkpoint commit

**Files:** Create `Importing/ImportRunner.cs`; tests `ImportRunnerTests.cs`, `ImportCommitRecoveryTests.cs`.

**Consumes:** prior identity/inventory/planner/store/transfer APIs. `ImportRunner(ImportStateStore store, MediaTransfer transfer, Func<DateTimeOffset>? utcNow = null)` provides `Run(IPhotoDeviceProvider provider, string? alias, CancellationToken token) -> ImportRunResult`; `DryRun(IPhotoDeviceProvider provider, string? alias, CancellationToken token) -> ImportDryRunResult`. Define `ImportDryRunResult` with matched registration/device, plan, proposed names, space estimate, warnings and pending reuse candidates (unverified). Runner owns lock, opened device and session lifecycle; caller owns provider.

- [x] **1. Write failing lifecycle tests.** Successful photos+videos produce tracked newest-day keys; unchanged day unions boundary keys; later same-day file eligible; no-op preserves checkpoint. Transfer/inventory/state errors/cancellation before commit preserve base checkpoint and completed journal. Missing completed source with valid local copy may commit; missing/changed unfinished source blocks. New eligible files discovered on resume join a durably expanded plan. Changed policy/identity/destination/base checkpoint refuses resume. Test all Review Focus crash points: prepared/rename/completed from Task5 plus ready journal, config replacement and archive.
  ```csharp
  Assert.Equal(baseCheckpoint, store.ReadConfiguration()!.Devices.Single().Checkpoint);
  Assert.NotNull(store.ReadPending(registration.Id)); // after injected precommit failure
  ```
  `ConfigAlreadyCommitted_ArchivesWithoutOpeningContent` asserts no duplicate USB reads; `UnexpectedCheckpoint_RefusesRecovery` prevents rollback. Cancellation arriving after committed replacement reports committed success; archive/report failure reports committed state with local-I/O status. No assertion equates local hashes to phone hashes.
- [x] **2. Run red:** `dotnet test PhotoImport.slnx --configuration Release --filter "FullyQualifiedName~ImportRunnerTests|FullyQualifiedName~ImportCommitRecoveryTests"`.
- [x] **3. Implement lifecycle.** Complete inventory/plan before journal or content. Resume pending first; retain verified completed entries absent from current phone, revalidate unfinished entries, merge newly eligible entries by metadata key, and persist before content. Check total budget for remaining transfers. Write ready-to-commit with exact old/new checkpoint; revalidate outputs before committing a resumed ready journal; atomic config replacement is the commit point; archive afterward. Do not blindly recompute a different target for an already committed ready journal. Return honest outcomes for cleanup/archival errors. Archive no-candidate reports without changing checkpoints; intentional filename skips are not transfer errors.
- [x] **4. Run green**, including deterministic failure injection for every persistent transition. Assert `DryRun` never calls media open, local hash recovery, state writes or destination creation; proposed journal reuse remains explicitly unverified until real sync.
- [x] **5. Commit:** `feat: orchestrate resumable imports and atomic day checkpoints`.

### Task7: First-use registration, everyday CLI and status

**Files:** Create `Importing/ImportCli.cs`; modify `Program.cs`, `Cli/ProbeCli.cs`; tests `ImportCliTests.cs`, `ImportProgramRoutingTests.cs`.

**Consumes:** Tasks1–6. `ImportCli(TextWriter output, TextWriter error, ImportStateStore store, MediaTransfer transfer)` provides `Run(string[] args, CancellationToken token, Func<IPhotoDeviceProvider> providerFactory) -> int`. Introduce an internal `Program.Run` overload with injected state directory for routing tests; existing callers use default `%LOCALAPPDATA%\PhonePhotoSync`. Create provider lazily only for init/sync/dry-run, never status/help/no-config first-use instructions.

- [x] **1. Write failing CLI tests.** Exact command grammar from spec: `init` requires device/alias/destination/cutoff/at least one root; repeated `--root` allowed, duplicate scalar/unknown options rejected. First init creates schema1/kind config, registration ID/identity and completed-day checkpoint; additional init preserves other registrations; duplicates refuse. No args with config syncs exactly one matching registration; without config prints guidance without discovery/state creation. Alias is registered selection, not a discovery friendly-name fallback. Status is offline and includes pending state. `DryRun_UnknownNamesReported_NoWritesOrContent` snapshots filesystem and stream-open counts. Root/identity/config/I/O/cancellation errors map to prescribed exits; postcommit error explicitly says progress committed. Existing diagnostic routing/help continue working.
- [x] **2. Run red:** `dotnet test PhotoImport.slnx --configuration Release --filter "FullyQualifiedName~ImportCliTests|FullyQualifiedName~ImportProgramRoutingTests"`.
- [x] **3. Implement init/status/CLI formatting and routing.** One-time init resolves initial ID/unique name only, validates actual destination write access using a uniquely owned create/delete probe and roots, enrolls identity and atomically adds registration under lock. Never reset prior registration/checkpoint. Normal sync prints eligible/skipped counts, bytes and final progress; distinguish photos/videos, warnings, incomplete work and committed-after-error outcomes. Keep diagnostic command routing and `--help` provider-free where existing behavior permits; no separate automatic preview before every real sync.
- [x] **4. Run green**, plus existing CLI test classes to catch routing regressions. Actual phone registration/destination selection is not part of these tests.
- [x] **5. Commit:** `feat: add automatic filename-day sync and first-use setup`.

### Task8: Documentation, complete verification and one independent review

**Files:** Update `README.md`, `docs/specs.md`, `docs/handoff.md`, `docs/work-status.md`; add `docs/compatibility/filename-importer-validation.md` (manual acceptance procedure, not invented hardware results).

**Interfaces:** Public commands/state/recovery behavior from Tasks1–7. No new product APIs.

- [x] **1. Document registration and daily use.** Show explicit destination/alias placeholders and2026-09-03 as the S21 cutoff example; explain filename dates/unknown skips, same-day handling, older late additions, no-overwrite names, identity limitations, state location and pending journal. Replace stale EXIF-first importer statements with links to the implemented filename policy while preserving diagnostic provenance/limits. Document process blocking, crash recovery, postcommit archival errors and manual inspection when pending state cannot reconcile. Mark iPhone unimplemented.
- [x] **2. Run full verification:** `dotnet test PhotoImport.slnx --configuration Release`, `dotnet build PhotoImport.slnx --configuration Release --no-restore`, `git diff --check`. Require all existing and new tests passing,0 build warnings/errors and no whitespace errors; report actual test count, not a projection from265. Do not broaden hardware work or repeatedly rerun unchanged passing checks.
- [x] **3. Request one final independent whole-increment review** through `superpowers:requesting-code-review` (the user's preserved native execution choice includes this review). Review checkpoint/recovery invariants, source/destination containment, no-content dry run and command/state compatibility. Fix material findings with regressions and rerun affected checks; no automatic review loop/per-task agents.
- [x] **4. Update handoff/status with actual evidence**, remaining limitations and the exact next hardware acceptance step. Keep actual destination/registration approval separate from implementation. No full-copy replay just to demonstrate the UI.
- [x] **5. Commit docs/final fixes with explicit paths;** leave the feature branch ready for user review, with no merge or push.

## Plan self-review and execution handoff

Coverage: intent/command use→Tasks7–8; root/date policy→Tasks1–2; identity→Task2; persistence/locking→Task4; full transfer/free space/path safety→Task5; resume/checkpoint ordering→Task6; exits/dry run→Tasks6–7; acceptance/regressions/review→Task8. Review Focus cases each have an owning behavioral test. Shared type names and capabilities above are the contracts; add small internal helpers as needed without changing the approved behavior. Fault hooks are internal test seams, not CLI features or serialized data.

Self-review completed for coverage, step specificity, contract consistency, recovery ordering, review-focus tests and plan proportion. Written-plan review approved; all implementation tasks complete. Final verification:336/336 Release tests, clean build; one independent review followed by one fix pass. Deferred reporting minors are recorded in docs/handoff.md. Preserve native execution in the current checkout; do not ask the user to choose execution/worktree again. After approval, read `superpowers:executing-plans` and `superpowers:test-driven-development`, then begin Task1 with a fresh baseline check. No implementation begins before that written-plan review.
