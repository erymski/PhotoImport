# Scoped Android live preview

Status: written design approved by the user on 2026-10-16. Scope: explicit folders, a required photo-read limit, at most 256 KiB per supported photo, metadata/selection reports, and visible unresolved entries. Current-checkout native execution and one final independent review remain selected. Implementation and subsequent hardware testing are separate stages.

## Intent

Show which photographs in selected Android folders are newer than a capture-day cutoff without saving photographs or changing import state. Reuse the tested capture-day readers and offline planner. The three JPEG samples and one HEIC/DNG sample each have user-confirmed Gallery day agreement; this does not establish format-wide accuracy or complete enumeration.

The next useful result is a live, explicitly bounded folder preview. A full-library content scan would be slower and exceed the previously approved diagnostic scope. Continuing manual three-file batches would not produce a practical folder preview. Use the bounded live approach, with implementation tested offline before a separately approved hardware run.

## Command and scope

```text
phone-photo-sync preview --device <id-or-unique-name> --root <absolute-device-folder> [--root <folder> ...] --max-photos <positive-integer> (--cutoff-day YYYY-MM-DD | --checkpoint <local.json>) --output-dir <new-directory>
```

Every run requires an explicit device, at least one folder, a read limit, one boundary source and a new report directory. There is no default folder, unlimited mode or automatic phone-wide content scan. Device selection follows the existing exact-ID or unique-name rules. A supplied checkpoint must match the selected device before any content is opened. A manual cutoff uses the existing completed-day policy; a checkpoint may use completedDay or tracked boundaries.

Folder paths must be absolute device paths with no traversal components, wildcards or UNC syntax. Resolve them against the connected device, preserving canonical spelling. Reject duplicate or nested requested roots case-insensitively rather than count files twice. All roots must resolve to directories before content reading begins. A missing or ambiguous root prevents content reading. Ancestor directory listings needed to locate roots are allowed; metadata collection and recursive traversal remain confined to the resolved roots. Skip `.thumbnails` as the existing probe does.

The limit controls content-open attempts, including failures, rather than successful parses. It does not truncate inventory enumeration or hide remaining rows. Metadata enumeration can still take time. Before starting content reads, print inventory count, selected roots, configured limit and its maximum application-byte budget (`limit * 262144`). Driver read-ahead can exceed that budget in USB traffic. The limit is a positive 32-bit integer; budget arithmetic uses a wider integer.

## Inventory, capture and selection

1. Validate arguments, checkpoint structure and output location before USB discovery. Reserve a new output directory and its report filenames without overwriting anything. If the directory already exists, refuse even if it is empty. Retain this run's artifacts on later failure.
2. Discover and select one device, open one read-only session, resolve all roots, and verify checkpoint identity. Require the separate content capability before capture attempts.
3. Enumerate photo metadata recursively within the roots. Preserve all supported inventory extensions, including unresolved PNG/TIFF. Record roots, duration, errors and completeness using the existing inventory model. Metadata failures remain visible; ordinary subtree failures may retain other observations. Cancellation or a disconnect ends device work.
4. From the collected inventory, choose unique, known-size JPEG/JPEG-alias/HEIC/HEIF/DNG paths in ordinal canonical-path order. Do not use filenames, modification dates or apparent age to avoid a capture read. Duplicate paths and unknown sizes stay unresolved without content attempts. Attempt at most the configured number; no automatic retries or full-file fallback. Partial enumeration may still supply a partial preview of successfully collected objects unless the session has disconnected or cancellation occurred.
5. For each attempt, use the existing provenance-bearing content handle and `MediaExifReader`. Compare canonical path, size and available persistent IDs with the inventory before consuming bytes. A mismatch produces an unresolved observation; close the handle without parsing. Close every stream before starting another. Retain observations and errors when subsequent reads fail. Ordinary file failures may continue within the remaining attempt budget; a device disconnect stops further attempts.
6. Feed inventory and schema-2 capture evidence to the existing `DayPreviewPlanner`. Preserve its raw-evidence validation, source association and calendar-day rules. A reached limit leaves unread objects unresolved; it never makes a partial folder look complete. The current planner's cancellation behavior may leave rows unprocessed; report processed versus inventory totals explicitly.

Capture-day policy is unchanged: DateTimeOriginal calendar date, no PC-timezone conversion, filename/modification-time fallback or borrowed directory fields. Preserve exact-time diagnostics separately. Unrelated Apple maker-note errors may be warnings for an otherwise valid day. PNG/TIFF remain unresolved with no content read. No parser receives unbounded access to a USB stream.

## Components and compatibility

Keep the normal `probe` metadata-only, `exif` limited to 1–3 explicit paths, and `plan` strictly offline. Add a scoped inventory service over `IPhotoDevice` rather than granting content access to the metadata interface. Navigation uses existing storage roots and child enumeration; do not require a new package or private WPD reflection.

Add a live-preview orchestrator that owns traversal, capture scheduling, attempt accounting and session lifecycle, with a thin command parser. Reuse the existing content capability, readers, capture/report models and pure planner. Share small capture-operation helpers where needed to preserve ownership/error handling, without relaxing the existing explicit diagnostic's limit. No parallel USB operations, caching, resume system or background work.

## Reports and failures

Write `inventory.json`, `capture.json` and `preview.json` in the new directory, using the existing inventory shape, capture schema 2 and preview schema 1. Add a small `run.json` manifest with schema version 1, requested/resolved roots, configured limit, attempted count, application bytes read, limit-reached flag, run status and session/output errors. This distinguishes deliberately unattempted files from attempted failures without changing the offline input schemas. Unattempted paths are not fabricated capture observations. The capture requested-path list describes attempted selections, including failures; the manifest explains budget exhaustion.

Capture completeness is incomplete if an attempted capture has no valid day, enumeration was partial, cleanup failed or readable supported objects remained unattempted after the limit. An invalid exact timestamp with an available day is not itself a capture failure. The preview is complete only if enumeration completed and every inventory object has a valid planner decision. Unsupported/missing-date objects remain unresolved. Empty folders may produce a complete zero-selection preview. Zero content attempts is valid when nothing is readable or supported; it does not imply completeness.

Cancellation stops scheduling work, preserves completed observations and exports partial reports where possible. Mark the run cancelled and return 130 even if export or cleanup also fails. Synchronous COM calls may delay cancellation until they return; no immediate interruption guarantee. Cleanup errors must not replace the primary error or cancellation, and must prevent a complete-run claim.

Use exits 0 for a complete preview, 2 for invalid arguments/checkpoint/device mismatch, 3 for device selection/session/root-resolution failures, 4 for partial or unresolved previews, 5 for local report/checkpoint I/O failure and 130 for cancellation. When failures overlap, cancellation wins, then local I/O failure, then fatal device failure; preserve all diagnostics. A later output failure may leave some reserved files empty or partial; the run is unsuccessful and filenames must not imply an atomic bundle. Export errors go to stderr as well as the manifest if writable. Never overwrite or delete previous reports.

## Verification and boundaries

Use fake metadata/content devices and synthetic media for TDD. Cover root resolution and boundary-safe containment, nested/duplicate/missing roots, unsupported formats, unknown sizes, duplicate paths, stable attempt order, failed-open accounting, limit exhaustion, source changes before reads, partial enumeration, cancellation/disconnect, stream/session cleanup and output races. Assert literal selected/unresolved paths and totals. Confirm malformed arguments, wrong checkpoint identity and output collisions cause zero content reads. Verify the total application read bound and absence of content access outside selected roots. Reopen generated reports through the real offline readers/planner to establish interoperability. Retain all existing tests and run a Release build plus one final independent review.

No hardware reads are included in implementation. After verification, propose a separate small Camera-folder run with an explicit limit and cutoff, stating the application-byte bound and driver-read-ahead caveat. Broader content-read permission is not implied by design approval. Independent other-folder counts and unresolved-format policy remain acceptance work. iPhone stays deferred.

No full media downloads, destination naming, importer, enrollment, checkpoint/configuration updates, pending journal, cache, Picasa integration, ADB, phone writes or background scan are included. Saved source metadata is snapshot association, not content identity or proof that the phone is unchanged.
