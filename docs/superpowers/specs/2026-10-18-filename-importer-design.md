# Repeatable Android filename-day importer

Date: 2026-10-18. Status: design and plan approved; implementation complete; subsequent reporting corrections and Android follow-up passed 341 Release tests and one additional bounded-change review. Real registration, imports, reconnect, same-day capture and cooperative recovery were exercised; see `docs/handoff.md` for current evidence and limits.

## Intent and evidence

The user wants to connect/unlock the phone in USB File transfer mode, start the utility, and have it detect the registered phone, load JSON settings/progress, copy new camera photos/videos and screenshots to the configured directory, then save progress. The user approved this workflow in conversation. Initial completed day for the S21 is **2026-09-03**. Day precision is sufficient; exact hours and seconds are unnecessary.

The [filename trial](../../compatibility/2026-10-18-filename-selection-trial.md) copied all 126 selected files, including 10 videos, with matching source-reported/received/local lengths: 903.10 MiB in 127.249 seconds, plus 24.385 seconds metadata enumeration. Of 4,009 comparable saved stills, 14 filename days differ from EXIF days; none change selection for this cutoff. Filename day is an explicit selection policy, not proof of capture day. The trial is throwaway evidence; this increment implements a maintained importer.

The selected approach is a repeatable importer with JSON checkpoints and a recovery journal. A manual-cutoff copy command would leave progress management to the user. Downloading into staging to discover EXIF dates would read older content unnecessarily and was not approved. Do not introduce that approach.

This design supersedes the older timestamp/EXIF-first importer proposals in `docs/specs.md` for this Android filename policy. Existing `probe`, `exif`, `plan`, and bounded `preview` contracts remain intact. iPhone importing is deferred.

## User commands

One-time registration:

```text
phone-photo-sync init --device <current-id-or-unique-name> --alias <name> --destination <absolute-PC-directory> --cutoff-day YYYY-MM-DD --root <absolute-device-folder> [--root ...]
```

Registration discovers and matches exactly one phone, reads identity metadata and validates the explicit roots. It opens no media content. The destination must already exist and be writable; do not create it from an illustrative default. Initial cutoff and roots are required. Initialization creates a new configuration or adds a distinct registration, preserving other registrations; it refuses duplicate aliases/identities and never resets an existing checkpoint. Destination and cutoff are supplied by the user at actual registration time; do not assume a real destination from examples. Adding a device is rejected while another import holds the configuration lock.

Normal use:

```text
phone-photo-sync
phone-photo-sync sync [--device <registered-alias>]
phone-photo-sync sync [--device <registered-alias>] --dry-run
phone-photo-sync status
```

With no arguments, run sync when configuration exists; otherwise print first-use instructions and exit without device discovery or state creation. `--help` remains help. Discover registered devices automatically; sync proceeds only when exactly one registration matches a connected phone. With several matches, require `--device`. An explicit alias must still match its saved physical-device identity. No match is an actionable error, never an import-all fallback. `status` reads only local settings/checkpoints/pending-session summaries, without USB discovery. Existing commands retain their routing and meanings.

Dry run enumerates metadata, applies filename selection and checks current destination collisions/available space. It shows eligible media, skipped names/reasons, estimated bytes and proposed destination names, including pending-session reuse candidates. It opens no media content, creates no destination files/reports, and changes no configuration, checkpoint or journal. Recovery hash verification and final name reservation happen only during real sync. Preview results do not promise that the destination or phone will remain unchanged afterward.

## Source and day policy

Registration requires one or more distinct, non-overlapping roots under the S21's supported camera/screenshot locations: `\Internal storage\DCIM\Camera`, `\Internal storage\DCIM\Screenshots`, `\Internal storage\Samsung\Pictures\Camera Roll`. A registration may choose a subset or subfolders within these roots. Reject whole storage, whole Pictures, app directories and arbitrary unrelated roots for this initial Android policy. Traverse chosen folders recursively; skip `.thumbnails` and `.trash` case-insensitively. Resolve canonical roots through the existing metadata-only device abstraction and validate every child path stays within scope. Directory cycles, duplicate file paths, missing roots or reported traversal errors make the inventory incomplete and prevent transfer/checkpoint changes. Preserve the backend's known limitation: MediaDevices may suppress native enumeration errors, so successful API traversal is not an independent physical census.

Still extensions: `.jpg`, `.jpeg`, `.png`, `.heic`, `.heif`, `.dng`, `.tif`, `.tiff`. Camera-video extensions: `.mp4`, `.mov`, `.m4v`, `.3gp`, `.3g2`, `.avi`, `.mkv`, `.webm`, `.mts`, `.m2ts`. Match extensions case-insensitively. Copy original exposed bytes; do not parse/transcode videos to establish eligibility. Listing an extension here establishes raw-copy selection, not container-metadata support. Nonmedia files are ignored with counts. Media under the approved camera roots is assumed camera-origin; detecting manually placed unrelated files is outside this folder-based policy.

Recognize a filename stem beginning with:

- `YYYYMMDD`, followed by end of stem, `_`, or `(`.
- `Screenshot_YYYYMMDD`, followed by end of stem, `-`, or `_`.
- `WP_YYYYMMDD`, followed by end of stem or `_`.

Prefix matching is case-insensitive; date digits are ASCII, parsed strictly as a Gregorian calendar date. Validate date only, without requiring valid time fields. No EXIF, modification time, PC timezone conversion or content-read fallback. Unknown/invalid filename dates are skipped and visible in console plus the real-run report. These intentional skips do not block checkpoint advancement. A new or renamed file later acquiring an older filename date remains excluded under the date-cutoff policy. No special hard-coded exclusions for the two legacy screenshots are needed: their valid filename days are old.

Unknown/nonpositive sizes for date-eligible entries, ambiguous identity/path association and unsafe output names are errors, not intentional date skips. Record them and refuse a new transfer session until corrected. Sort candidates by canonical source path using ordinal comparison for stable planning.

## Identity, configuration and checkpoint

Use `%LOCALAPPDATA%\PhonePhotoSync\config.json`, a new importer-specific schema, with an array of independent registrations. Persist schema version, generated registration ID, alias, absolute destination, canonical roots, identity policy and observations, `datePolicy=androidFilenameDayV1`, and checkpoint. Existing offline `DayCheckpoint` JSON files are inputs to the older planner, not importer configuration; never silently migrate them.

Prefer a nonempty serial number, combined with manufacturer/model, when registering. Require all enrolled identity fields to be available and match exactly on later runs, and require a unique connected match. If no serial is exposed, persist exact WPD device ID plus available manufacturer/model, label the identity policy as limited, and fail if that ID changes. Never fall back to friendly-name matching after enrollment or silently bind a different phone. Friendly name is a display/initial-selection aid. Do not claim physical identity stability beyond observations; hardware validation must check reconnect behavior. Registration ID remains fixed even if discovery uses a transport-specific ID.

Checkpoint contains `day`, `mode` and boundary keys. Initialization sets `mode=completedDay` with no keys: all days through the initial cutoff are excluded. After successful copying, set `mode=tracked`, day to the latest imported filename day, and keys to all verified imported candidates on that day. In tracked mode, select a file on the boundary day if its key is absent. Union previous keys when the latest day is unchanged. Earlier days remain excluded, including late-added older files. If nothing is imported, leave the checkpoint unchanged.

A key hashes the registered ID, canonical source path, known source byte size and filename day using an unambiguous serialized representation. It is a metadata fingerprint, not a content hash. Do not key the checkpoint by discovery-session WPD handles or only the last filename. Deleting/organizing already completed PC imports does not make old phone media eligible again. Same-path, same-size replacement cannot reliably be detected from these metadata alone; content deduplication/history is outside this increment.

Configuration updates use flushed temporary JSON and atomic same-directory replacement, with a backup of the previous valid file. Reject malformed/unsupported state without silently resetting it or auto-restoring a stale backup. Use one exclusive application lock in the state directory for init/sync; this serializes access to shared configuration and destinations across registrations. Status/dry-run acquire a consistent read view and refuse while a writer is active. No concurrent import worker pool is needed.

## Planning and full-file transfer

Separate pure filename parsing/selection from scoped metadata inventory, persistence, transfer and CLI orchestration. Reuse `DeviceFolderScope` and provider discovery. Add an importer media-content capability accepting supported photo/video paths; do not widen the bounded EXIF reader's accepted paths or limits. The metadata-only interface remains unable to open content. Avoid broad renaming of existing photo-oriented diagnostics.

Before transferring: validate state, identity, roots and complete inventory; construct the plan; validate destination access/free space; and durably save a pending-session plan. Require selected known bytes plus a 2 GiB free-space reserve; check reserve between files and during large writes. Free-space failure before transfer leaves the checkpoint untouched. If checks fail later, preserve successful files and the pending journal.

Preserve each original basename when it is a safe Windows filename. Reject traversal, rooted/alternate-stream names and invalid/reserved names rather than modifying source bytes or writing outside the destination. If a basename is occupied, choose a deterministic suffix based on the source key, with a numeric suffix if necessary. A pre-existing name alone is never evidence of import. Reserve/finalize without overwrite and handle races by choosing another unoccupied candidate and recording it before finalization. No speculative collision check authorizes replacing any existing file.

For each selected candidate, revalidate canonical path, size and any available persistent object ID against this session's inventory. Open one source stream and read fully to EOF into a unique `.partial` under an importer-owned staging subdirectory of the destination. Compute SHA-256 while writing; flush to disk. Require received/local/reported lengths to agree before finalizing. No automatic retries or byte-range resumes: an incomplete file restarts from byte zero on a later run. Keep phone access read-only and one stream at a time.

The pending journal records a generated session ID, registered identity, destination, policy/roots, base checkpoint, source snapshot, selected keys/days, planned final/partial paths and each file's progress. Persist each transition durably. Before moving a verified partial to its final name, record a `prepared` entry with byte length and local hash. Then move without overwrite and record `completed`. This ordering makes a crash between rename and completion recording recoverable. Do not treat a final file without the session's prepared/completed evidence as a prior import.

On a file/device/disk error or Ctrl+C, stop further transfers, retain completed files and the journal, and leave the checkpoint unchanged. Preserve partials for diagnosis; they are never treated as complete media. Report the failing path and recovery instructions. Cancellation is checked between synchronous calls and reads; a blocked native USB call may delay it. No watchdog is claimed to safely interrupt COM.

## Retry and commit protocol

The next real sync resumes a pending session before starting another one for that registration. Refuse silent changes to its policy, destination, roots, identity or base checkpoint. Re-enumerate metadata. Verify completed/prepared destinations using recorded length and SHA-256 before skipping their transfers. A valid prepared final can become completed; a valid prepared partial can be finalized without another USB read. Missing or changed local output loses reuse eligibility; preserve unrelated existing files and plan a fresh, collision-safe copy. Remove only a journal-owned incomplete partial when restarting that transfer; never sweep arbitrary files or other sessions.

Unfinished sources must still match their recorded identity/size/path; missing or changed unfinished sources block successful completion with actionable diagnostics. Verified already copied sources may have disappeared from the phone without requiring another transfer. Include newly eligible current-inventory entries in the resumed plan before computing the new checkpoint. Persist the expanded plan before opening new content. Existing verified journal entries retain their dates/keys, so completed work is not lost merely because a source disappears. Do not automatically discard a pending session that cannot be reconciled; report its location for deliberate recovery.

When every selected entry is verified and no transfer/inventory error remains, durably record a ready-to-commit journal containing the exact old/new checkpoints. Atomically update configuration, then mark/archive that journal as committed. On restart, if configuration already equals its intended new checkpoint, finish archival without recopying. If it still equals the base checkpoint, revalidate outputs before committing. Any other checkpoint/config mismatch is an error. Cancellation observed before configuration replacement prevents advancement; once replacement has committed, the run is complete even if cancellation arrives during final reporting/archival. Do not report a committed checkpoint as unchanged.

Archive the completed journal and a concise run report under the state directory for traceability; never delete media when archiving. Reports include skipped names, per-file outcomes/destinations, counts, bytes, timing and old/new checkpoint. No empty/no-candidate run advances the checkpoint. Experiment copies are not automatically imported into production state, and this design does not authorize writing actual registration state or repeating USB validation during implementation.

## Errors and acceptance

CLI exits: 0 success (including intentional filename skips, visibly summarized); 2 arguments/config/schema/ambiguous-selection error; 3 device/identity/root access failure; 4 incomplete inventory/source evidence/transfer or verification failure; 5 local output/state I/O failure; 130 cancellation before commit. A committed run with an archival/report failure reports that checkpoint commitment explicitly and exits 5; retry must use the commit-recovery rules. Prior checkpoints remain unchanged for every failure before the commit point.

Automated tests cover parser boundaries/calendar validity, all scoped media types, root exclusions, completed/tracked day selection, same-day additions, independent device state and no-content dry-run. Transfer tests use fake streams/devices and temporary filesystem locations: EOF/size mismatch, source change, disk failure, cancellation, collision/no overwrite, malformed state, exclusive locking, failed atomic replacement, and crashes at prepared/rename/completed/checkpoint/archive transitions. Verify retry hash checks, missing/modified outputs, disappeared unfinished versus completed sources, and added candidates. Existing 265-test diagnostic suite must continue passing. Product increments use the already selected current checkout/native execution and one final independent review.

Hardware acceptance is separate: register only with a user-supplied real destination, check saved identity across reconnect, preview the intended folders/cutoff, then explicitly scope any production copy validation. Reuse the successful 126-file trial as feasibility evidence; do not repeat it automatically. iPhone and general EXIF/video-timezone semantics do not gate this explicitly Android filename policy, and are not claimed supported by it.
