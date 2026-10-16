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
- [ ] Validate selected-file/original access through a separately invoked transfer test before accepting either backend for synchronization.

## Current findings

Implemented on `feat/metadata-probe`, using the MediaDevices 2.0.0 NuGet package. The Samsung S21 probe saved a partial report with 13,156 images in 125.618 seconds, including 3,601 actual camera-folder images; all lacked authored capture dates. Device ID, PnP ID, and serial candidates were unchanged across one physical replug in MTP mode and a fresh process. A physical device-removal error was retained in the JSON. Current decision: no-go for date-based synchronization through the tested MTP/PTP metadata paths. See [Samsung assessment](compatibility/android-samsung-sm-g991b.md). iPhone remains untested. Milestone 1 stays open pending reliable capture metadata, full coverage, remaining platform validation, and the separately invoked selected-file/original transfer test. No probe process remains running.

Follow-up: three raw-property samples in MTP and PTP mode each advertised 16 properties, with creation/authored dates unsupported (`0x80070032`) and modification time supported. PTP did not solve the capture-date blocker. Device/PnP IDs change across USB modes, while the serial candidate stayed unchanged. The application still uses the supported public NuGet API; the private-reflection investigation remains a throwaway local artifact. The user requires normal USB transfer and declined ADB/USB debugging; preserve this constraint. No phone app, debugging connection, or synchronization engine was added. The later one-photo EXIF experiment below is the only approved media-content-read exception.

## Next Android feasibility question

iPhone testing is explicitly deferred and is not the reason Android work is blocked. The remaining issue is obtaining an actual capture date over the permitted USB connection.

Approved by the user on 2026-10-16 and completed: a throwaway EXIF investigation on one existing camera JPEG. The read-only MediaDevices session reported MTP mode. It read exactly 262,144 bytes in 16 calls from a 3,002,932-byte file, then closed the stream and parsed only the in-memory prefix using MetadataExtractor 2.9.3. EXIF DateTimeOriginal was `2026:10:15 19:17:14`, SubSecTimeOriginal `762`, and OffsetTimeOriginal `+02:00`; modification time was `2026-10-15 19:17:17`. The entire experiment took 38.741 seconds, including device discovery, connection, metadata access, reads, parsing, and cleanup; this is not a transfer-throughput measurement. No errors or parser warnings were reported. Only parsed metadata and diagnostics were saved in ignored `artifacts/android-s21-one-jpeg-exif.json`; no image bytes were saved and no full-file fallback was used. The isolated source is under ignored `artifacts/exif-spike/`; it built with 0 warnings/errors and execution exited 0.

Driver-side read-ahead means the application cap does not establish a USB traffic cap. This one-photo result demonstrates a possible capture-date source, not library-wide accuracy, format coverage, original-file validation, or acceptable performance. The normal probe remains metadata-only. Further photo-content reads and shipping an EXIF scanning mode require approval of the [proposed Android JPEG addendum](superpowers/specs/2026-10-16-android-jpeg-exif-addendum.md). iPhone remains deferred.

## Review and implementation decisions

The independent whole-change reviewer inspected production code, tests, package XML/IL, and upstream source. The final verdict permits merging the diagnostic implementation; it does not certify physical compatibility. Review findings addressed: wrapped worker exceptions, loss of earlier sibling observations, cleanup overriding primary results/cancellation, and ownership of device handles during interrupted discovery. The reviewer did not rerun tests; execution evidence comes from this session.

A subsequent one-line cancellation/disconnect race correction was verified with a new test that failed with `incomplete` before the change and passed with `cancelled` afterward; the whole suite now passes 56/56. This targeted correction was made after the independent review. Tool-driven Ctrl+C was not a successful hardware cancellation demonstration; that limitation is recorded in the phone report.

- Use PowerShell and the Markdown ledger for Windows bookkeeping instead of Bash scripts; task bookkeeping is maintained manually.
- Test timestamp/JSON fidelity through mapper and report-writer consumers rather than trivial serialization-only tests; those consumer tests now pass.
- Report unavailable raw variants, separate PTP dates, format/content type, and package-hidden enumeration errors explicitly. Cost: these evidence gaps may require a WPD extension and prevent accepting a phone backend; a completed API traversal does not guarantee complete photo coverage.
- Stream child enumeration to preserve observations before errors/cancellation. Cost: callers must consume the iterator within the live device session.
- Read-only synchronous COM calls can delay Ctrl+C until they return. Compatibility reports must state this limitation; cancellation is never a claim of immediate driver interruption.

No review findings were deferred as minor issues. Physical coverage/date accuracy, reconnect stability, iPhone support, raw-data evidence gaps, and original-file access remain acceptance gates.
