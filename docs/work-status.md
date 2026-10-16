# Work status

Task tracking uses Markdown, per the user's instruction on 2026-10-15. Do not use Beads for this project.

## Milestone 1: metadata feasibility probe

Source: [product specification](specs.md), sections 3 and 11.

- [x] Review product specification and current repository.
- [x] Confirm .NET 10 SDK availability and review MediaDevices package compatibility.
- [x] Prepare [milestone design](superpowers/specs/2026-10-15-metadata-probe-design.md).
- [x] User review of written milestone design, approved on 2026-10-15.
- [x] Write and self-review [implementation plan](superpowers/plans/2026-10-15-metadata-probe.md).
- [x] User review of implementation plan; native execution selected on 2026-10-15.
- [x] Implement device discovery, metadata probe, console/JSON output, and automated tests using TDD.
- [x] Build and verify the CLI: 56 tests passed after hardware/review regressions; final full-solution Release build had 0 warnings/errors.
- [x] Write [physical-device assessment procedure](compatibility/README.md) and [backend evidence limits](compatibility/backend-notes.md).
- [x] Complete independent whole-change review and address important findings: reviewer found no outstanding Critical, Important, or Minor findings after the fix pass.
- [x] Test a real Android phone; record [Samsung S21 evidence](compatibility/android-samsung-sm-g991b.md): capture dates unavailable; current backend not accepted for sync.
- [x] Inspect all raw WPD properties and supported date properties for three camera objects in MTP and PTP mode: capture/creation dates are unsupported in both.
- [ ] Establish an acceptable capture-date metadata path within the agreed USB-only constraints; verify against EXIF/Picasa and complete Android folder coverage before synchronization.
- [ ] Deferred by the user on 2026-10-16: test a real iPhone and record compatibility evidence independently. Continue Android work without waiting for this.
- [x] Validate Android selected-file access with the separately approved one-JPEG full-read test: 3,002,932 bytes received, matching reported size; EXIF/dimensions verified. This establishes complete exposed-resource access for that JPEG, not independent bitwise/original-asset identity or other-format/iPhone access.

## Current findings

Implemented on `feat/metadata-probe`, using the MediaDevices 2.0.0 NuGet package. The original Samsung inventory was partial (13,156 images); a later scoped metadata run completed DCIM/Pictures (3,909 images, no surfaced errors). Three JPEG EXIF capture days match the user's Gallery comparison, and a separately approved full JPEG read succeeded. Ordinary WPD property dates remain insufficient. Device ID, PnP ID, and serial candidates were unchanged across one physical replug in MTP mode and a fresh process; a device-removal error was retained in the original JSON. See [Samsung assessment](compatibility/android-samsung-sm-g991b.md). Milestone 1 remains open for independent folder counts, other-format capture-date behavior and remaining platform evidence. iPhone is explicitly deferred. No probe process remains running.

Follow-up: three raw-property samples in MTP and PTP mode each advertised 16 properties, with creation/authored dates unsupported (`0x80070032`) and modification time supported. PTP did not solve the capture-date blocker. Device/PnP IDs change across USB modes, while the serial candidate stayed unchanged. The application still uses the supported public NuGet API; the private-reflection investigation remains a throwaway local artifact. The user requires normal USB transfer and declined ADB/USB debugging; preserve this constraint. No phone app, debugging connection, or synchronization engine was added. Content access is limited to the separately approved EXIF experiment and optional diagnostic below; no broader scan has been run.

## Android JPEG EXIF validation

iPhone testing is explicitly deferred and is not the reason Android work is blocked. Bounded JPEG reads now provide a capture-date path over normal USB for the tested samples; other formats, complete folder coverage, and complete-original access remain unverified.

Approved by the user on 2026-10-16 and completed: a throwaway EXIF investigation on one existing camera JPEG. The read-only MediaDevices session reported MTP mode. It read exactly 262,144 bytes in 16 calls from a 3,002,932-byte file, then closed the stream and parsed only the in-memory prefix using MetadataExtractor 2.9.3. EXIF DateTimeOriginal was `2026:10:15 19:17:14`, SubSecTimeOriginal `762`, and OffsetTimeOriginal `+02:00`; modification time was `2026-10-15 19:17:17`. The entire experiment took 38.741 seconds, including device discovery, connection, metadata access, reads, parsing, and cleanup; this is not a transfer-throughput measurement. No errors or parser warnings were reported. Only parsed metadata and diagnostics were saved in ignored `artifacts/android-s21-one-jpeg-exif.json`; no image bytes were saved and no full-file fallback was used. The isolated source is under ignored `artifacts/exif-spike/`; it built with 0 warnings/errors and execution exited 0.

Driver-side read-ahead means the application cap does not establish a USB traffic cap. The normal probe remains metadata-only. The user approved the [Android JPEG addendum](superpowers/specs/2026-10-16-android-jpeg-exif-addendum.md), then proceeding with the [implementation plan](superpowers/plans/2026-10-16-jpeg-exif-diagnostic.md) after reviewing the existing photo-sort approach on 2026-10-16. Native execution in the existing feature branch remains selected. iPhone remains deferred.

- [x] Implement bounded JPEG inspection using MetadataExtractor for both segment navigation and EXIF decoding; report DateTime and DateTimeOriginal separately.
- [x] Add the explicitly invoked `exif` command with 1–3 selected paths, new-file JSON reservation, read-only USB content capability, cancellation/partial-report behavior, and no full-file fallback.
- [x] Verify 91 Release tests and solution build with 0 warnings/errors before hardware validation; final directory-association regression fix passes 93 tests and the Release build with 0 warnings/errors.
- [x] Run the approved three-photo diagnostic: all three capture clocks available, general/original EXIF dates agree, two explicit offsets and one unknown. Read 63,460 application bytes in 5.792 seconds; each photo stayed below 262,144 bytes. No image bytes saved or phone/state changes. Evidence: ignored `artifacts/android-s21-three-jpeg-exif.json` and the [phone assessment](compatibility/android-samsung-sm-g991b.md).
- [x] Obtain user Gallery comparison for the three samples at the required precision. The user reported that the other files were fine and Gallery did not display seconds; the first sample's filename/EXIF second difference was already discussed. On 2026-10-16 the user clarified that only the capture day is needed, not exact hours or seconds. Accept this as user-confirmed calendar-day agreement for the three samples, not independent second/subsecond/offset validation.
- [x] Complete one independent review and address the important finding. Reviewer found cross-directory capture-field mixing; regression tests for orphan offsets/subseconds failed before correction, then full suite passed 93/93. No Critical or Minor findings. Fix was author-verified; no second review or extra hardware reads were performed.

The three-photo hardware report was produced by implementation commit `98a77af`, before the later synthetic-directory regression fix. Its EXIF result and timing remain evidence for that run; the final build's additional directory-association handling was verified with synthetic input, not by repeating the approved phone reads.

Review rulings: later APP1 conflicts remain outside the explicitly disclosed first-segment scope, so this diagnostic cannot certify file-wide uniqueness. Gallery comparison now supports day-level accuracy for the three samples. Exact seconds/offsets were not independently verified and are not required by the clarified precision requirement. USB read-ahead, complete-original access and physical cancellation remain hardware/manual evidence gaps; automated tests do not substitute for them. No minor review findings were deferred. Keep `feat/metadata-probe` in place while remaining milestone acceptance gates are open.

Next filtering requirement: use the recorded capture calendar date without PC-timezone conversion. A missing offset is not itself a blocker for obtaining that date. No download selection code exists yet; adapt the old timestamp-checkpoint draft and explicitly resolve same-day additions before implementing filtering. This precision clarification authorizes neither extra media reads nor synchronization.

## Remaining Android hardware checks

- [x] Prepare an ignored metadata-only harness around the existing `MetadataProbe` to scope enumeration to `Internal storage/DCIM` and `Internal storage/Pictures`. It uses only `IPhotoDevice`, not the optional content interface, saves a private JSON report and console counts, and has a two-minute cooperative timeout. Build succeeded with 0 warnings/errors. Source: `artifacts/folder-coverage/`.
- [x] Run the scoped metadata-only inventory after the user reconnected/unlocked the S21: `complete`, 3,909 images, 72.308 seconds, zero surfaced errors. DCIM: 3,721; Camera within DCIM: 3,601; Pictures: 188. Extensions: 3,898 JPG, 2 JPEG, 3 HEIC, 2 DNG, 4 PNG. No content APIs called. Evidence: ignored `artifacts/android-s21-camera-pictures-metadata.json`. Independent Camera photo-count comparison requested and pending; the package can hide native enumeration failures.
- [x] User separately approved the one-photo full-read test, completed for only `DCIM/Camera/20261015_191713.jpg`. Reported, received, and saved lengths all 3,002,932 bytes; JPEG dimensions 4032×2268; expected EXIF values retained; no parser/cleanup errors or warnings. Transfer plus local flush: 0.176 seconds. Local SHA-256 was independently recomputed with Get-FileHash and matches the diagnostic report. Evidence: ignored `artifacts/android-s21-original-access.json`; saved photo: ignored `artifacts/original-access/20261015_191713.jpg`. No phone/state writes. This is the first saved media copy. A local hash alone is not independent proof of bitwise equality with the original phone asset.
- [ ] Await approval for a separate three-format prefix feasibility test: one existing HEIC, DNG, and PNG from the new inventory, cap 256 KiB application bytes per object, parse in-memory data with MetadataExtractor, save only metadata, no full-file fallback. Selected metadata candidates: Telegram HEIC (2,778,405 bytes), camera DNG (18,883,776 bytes), Pictures PNG (2,914,035 bytes). No content from these candidates has been opened. This is a proposed throwaway feasibility test, not authorization to extend the product reader or perform a library-wide scan.

The initial attempt found no connected phone, but the user's reconnect resolved that. Preserve USB-only operation, no ADB, and all content scope limits. iPhone remains deferred. No new product behavior was added for these hardware checks.

## Review and implementation decisions

### Existing photo-sort implementation inspected on 2026-10-16

The user pointed to `C:\Projects\photo-sort`. Its `app/DateExtractor.cs` already uses MetadataExtractor 2.9.0 with `ImageMetadataReader.ReadMetadata(file)` on local files. JPEG/HEIC use EXIF IFD0 `TagDateTime`; MOV uses QuickTime metadata creation date and MP4 uses the movie-header created date. `Program.cs` enumerates an existing local incoming directory, so this is date extraction after files are on disk, not a USB metadata backend. The tests list JPEG, HEIC, MOV, and MP4 samples and assert calendar dates only, not full timestamps, offsets, or bounded reads. Tests were inspected, not executed. Existing changes in that repository were left untouched.

Reuse this established MetadataExtractor approach rather than implementing an EXIF decoder. IFD0 DateTime is a different tag from DateTimeOriginal. The later approved diagnostic reports both; they agree for the three tested JPEGs, while only two have offsets. No content reads were performed during the photo-sort source inspection itself; the subsequent three-photo USB run was separately authorized. The user's Gallery comparison and clarified day-only requirement satisfy day-level validation for these samples; exact-time/offset verification is not required for day-based selection.

The independent whole-change reviewer inspected production code, tests, package XML/IL, and upstream source. The final verdict permits merging the diagnostic implementation; it does not certify physical compatibility. Review findings addressed: wrapped worker exceptions, loss of earlier sibling observations, cleanup overriding primary results/cancellation, and ownership of device handles during interrupted discovery. The reviewer did not rerun tests; execution evidence comes from this session.

A subsequent one-line cancellation/disconnect race correction was verified with a new test that failed with `incomplete` before the change and passed with `cancelled` afterward; the whole suite now passes 56/56. This targeted correction was made after the independent review. Tool-driven Ctrl+C was not a successful hardware cancellation demonstration; that limitation is recorded in the phone report.

- Use PowerShell and the Markdown ledger for Windows bookkeeping instead of Bash scripts; task bookkeeping is maintained manually.
- Test timestamp/JSON fidelity through mapper and report-writer consumers rather than trivial serialization-only tests; those consumer tests now pass.
- Report unavailable raw variants, separate PTP dates, format/content type, and package-hidden enumeration errors explicitly. Cost: these evidence gaps may require a WPD extension and prevent accepting a phone backend; a completed API traversal does not guarantee complete photo coverage.
- Stream child enumeration to preserve observations before errors/cancellation. Cost: callers must consume the iterator within the live device session.
- Read-only synchronous COM calls can delay Ctrl+C until they return. Compatibility reports must state this limitation; cancellation is never a claim of immediate driver interruption.

No review findings were deferred as minor issues. Physical coverage/date accuracy, reconnect stability, iPhone support, raw-data evidence gaps, and original-file access remain acceptance gates.
