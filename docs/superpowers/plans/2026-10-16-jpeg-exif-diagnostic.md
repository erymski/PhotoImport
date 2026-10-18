# Optional JPEG EXIF Diagnostic Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers: executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** Inspect capture-date EXIF in bounded prefixes of explicitly selected Android JPEGs without altering the metadata-only probe.

**Architecture:** A JPEG segment reader consumes a forward-only stream and stops at the first complete EXIF APP1 segment, scan data, EOF, or the 256 KiB cap. MetadataExtractor parses only that in-memory EXIF segment. A separate opt-in CLI uses an explicit content capability on the existing read-only WPD session; the metadata-only interface retains no content methods.

**Tech Stack:** C#/.NET 10 Windows, MediaDevices 2.0.0, MetadataExtractor 2.9.3, System.Text.Json, xUnit.

**Spec:** [approved addendum](../specs/2026-10-16-android-jpeg-exif-addendum.md), [main specification](../../specs.md).

## Global Constraints

- Application cap: 256 KiB per selected object; driver read-ahead is unbounded by this guarantee.
- Save parsed fields and diagnostics only. No full-file fallback, phone writes, thumbnails, or sync state.
- Normal `probe` stays metadata-only; missing offsets stay unknown and no modification/filename fallback is permitted.
- Hardware validation is at most three selected camera JPEGs; no library-wide scan or other-format support.
- Preserve current checkout/feature branch and native execution, followed by one independent review. Use Markdown tracking, no Beads or Ponytail.

## Review Focus

- JPEG segment lengths can exceed remaining budget or reference truncated data: report an incomplete result without exceeding the cap.
- A successful prefix read can still contain duplicate EXIF blocks or multiple timestamp directories: do not silently choose conflicting capture fields.
- A device can disconnect during read or cleanup: retain earlier results and original failure/cancellation.
- Timestamp strings may contain invalid dates, subseconds, or offsets: retain raw evidence, never invent a valid timestamp.
- Output collision and invalid selection must fail before content access; explicit selection must never expand to recursive scanning.

---

### Task 1: Bounded JPEG EXIF reader and timestamp evidence

**Files:** Create `Services/JpegExifReader.cs`, `Models/JpegExifObservation.cs`, `tests/PhotoImport.Tests/JpegExifReaderTests.cs`, and a synthetic JPEG fixture helper under `tests/PhotoImport.Tests/Fakes/`. Modify `PhotoImport.csproj` for MetadataExtractor 2.9.3.

**Interfaces:** Produce `JpegExifReader.Read(Stream stream, CancellationToken token) -> JpegExifObservation`. Caller owns the stream. Observation carries bytes read, outcome, raw original/digitized dates, original/digitized subseconds and offsets, optional validated original timestamp, errors/warnings, and EXIF provenance. Expose the application byte cap in the report. Unoffset timestamps retain an unspecified local clock; do not manufacture UTC.

- [x] Write failing consumer tests with hand-built JPEG/EXIF fixtures. Assert literal date/subseconds/offset preservation and derived `2026-10-15T19:17:14.762+02:00`; missing offset has no inferred instant. Assert malformed date/offset/subseconds remain raw with no fabricated timestamp. Assert no EXIF reports missing, and non-JPEG input reports unsupported.
- [x] Add forward-only short-read fixtures. Assert truncated APP1 and oversize preceding segments report incomplete, reads never exceed 262,144 bytes, reading stops before scan data after a complete EXIF segment, cancellation interrupts between reads, and multiple conflicting capture directories are rejected as ambiguous.
- [x] Run `dotnet test PhotoImport.slnx -c Release --filter FullyQualifiedName~JpegExifReaderTests`; expected RED for the missing reader behavior. Add only the API skeleton needed to compile if necessary, then observe assertion failures.
- [x] Implement the segment reader with explicit length/budget checks. Use MetadataExtractor's EXIF reader on the complete in-memory EXIF APP1 payload only. Release buffers after parsing; never expose image bytes in observation/JSON. Stop on the first EXIF segment, and report that later segments were not inspected rather than claiming uniqueness across the whole file. Reject conflicting directories within the parsed segment.
- [x] Rerun the reader tests, then `dotnet test PhotoImport.slnx -c Release`; expected all pass. Commit reader and tests.

### Task 2: Explicit content capability and optional diagnostic CLI

**Files:** Create `Devices/IJpegContentDevice.cs`, `Devices/WpdReadStream.cs`, `Cli/JpegExifCli.cs`, `Models/JpegExifReport.cs`, and `tests/PhotoImport.Tests/JpegExifCliTests.cs`. Modify `Devices/WpdPhotoDevice.cs`, `Program.cs`, and help text in `Cli/ProbeCli.cs`. Extend test fakes only at the USB boundary.

**Interfaces:** `IJpegContentDevice.OpenJpegRead(string sourcePath, CancellationToken token) -> Stream`; caller owns stream. `WpdPhotoDevice` implements this separate interface using `GetFileInfo`/`OpenRead` in its existing GenericRead session. The returned wrapper permits only reads and normalizes package worker exceptions through `WpdOperation`; writes/seek/length changes are unsupported. `JpegExifCli(IPhotoDeviceProvider provider, TextWriter output, TextWriter error).Run(string[] args, CancellationToken token) -> int` consumes Task 1's reader. `Program` routes only `exif` to this handler.

Command: `phone-photo-sync exif --device <exact-id-or-unique-name> --photo <absolute-device-path.jpg> [--photo <path.jpeg> ...] --output <new.json>`. Require 1–3 distinct JPEG paths, an explicit device and output. Validate arguments and reserve output using CreateNew before content access. Reject unsupported backends explicitly. Exact device IDs win over unique case-insensitive names, matching existing probe semantics.

- [x] Write failing CLI tests: missing/duplicate options, four photos, duplicate paths, relative/non-JPEG paths, output collisions and malformed output paths cause no content reads; ambiguous names show IDs. A normal probe never opens the content capability.
- [x] Test synthetic success through the real reader/JSON: output contains raw dates, provenance, byte counts/cap, warnings that USB traffic is not bounded, per-file elapsed time, total elapsed time, and no prefix/image-byte fields.
- [x] Test first success followed by read failure, cancellation, and cleanup failure: earlier observations survive, streams/session close, and cleanup does not replace cancellation. Test wrapped USB failures normalize instead of crashing. Exit codes: 0 all capture clocks valid, 2 arguments/ambiguity, 3 connection/backend, 4 partial/missing/invalid capture data, 5 output, 130 cancellation.
- [x] Run `dotnet test PhotoImport.slnx -c Release --filter FullyQualifiedName~JpegExifCliTests`; expected RED for missing behavior after compiling against a skeleton if needed.
- [x] Implement handler, read-only wrapper and routing; show the explicit content-read warning in help/output. Do not add content methods to `IPhotoDevice`, scanning, import state, or full-download behavior.
- [x] Run `dotnet test PhotoImport.slnx -c Release` and `dotnet build PhotoImport.slnx -c Release`; expected all tests pass and 0 build warnings/errors. Commit the CLI/capability and tests.

### Task 3: Three-photo validation, documentation, and review

**Files:** Modify `README.md`, `docs/work-status.md`, `docs/compatibility/android-samsung-sm-g991b.md`, and `docs/compatibility/README.md`. Keep hardware JSON under ignored `artifacts/`.

**Interfaces:** Consume the explicit `exif` CLI from Task 2. Select at most the three previously sampled 2017/2023/2026 camera JPEG paths from existing local evidence; do not enumerate/read extra content to choose samples.

- [x] Document command, application cap/read-ahead distinction, JPEG-only scope, failure/offset policy, and the unchanged metadata-only default.
- [x] Run Release `exif` once for the three selected photos and inspect saved JSON. Expected: per-photo raw EXIF fields or explicit failure; byte count <=262,144 each; no image bytes saved. Record actual outcomes rather than assuming all three work. If phone unavailable, retain completed automated work and request connection without changing transport constraints.
- [x] Request independent gallery/Picasa dates for the selected photos if not already available; record that comparison as pending until supplied. EXIF matching filename is not independent validation. Follow-up: user Gallery comparison supplied; calendar-day agreement accepted under the user's clarified day-only requirement. Exact seconds/offsets were not independently verified and are not required.
- [x] Update acceptance status honestly: folder coverage, other formats, identity/cancellation, original-file access, and deferred iPhone validation are not established by this command.
- [x] Run full Release tests/build and `git diff --check`, then commit documentation. Expected all tests pass, build 0 warnings/errors, diff clean.
- [x] Run one independent review of this addendum's full implementation range, focused on the five Review Focus cases. Fix important findings with regression tests RED → GREEN and rerun the suite. Do not merge/push or claim milestone acceptance while hardware gates remain open. Result: one important cross-directory timestamp association finding fixed with two RED → GREEN regression cases; final suite 93/93, build 0 warnings/errors. No Critical or Minor findings. User Gallery comparison later confirmed the required day-level precision; hardware reads were not repeated after the synthetic regression fix.

## Plan self-review

All approved addendum requirements map to Tasks 1–3. The optional capability preserves the metadata-only interface and the CLI limits selection to three explicit paths. The first complete EXIF APP1 strategy is intentional: report its limited scope and detect conflicting parsed directories; do not read later image data to prove global uniqueness. Native execution remains the user's selected method. User approved proceeding after inspecting photo-sort on 2026-10-16. Segment navigation and EXIF decoding both reuse MetadataExtractor; report both DateTime and DateTimeOriginal.



