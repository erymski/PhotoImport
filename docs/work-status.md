# Work status — 2026-10-18

The Android filename-day importer is implemented, registered and exercised on the S21. See [current handoff](handoff.md) for state and [README](../README.md) for usage.

## Completed

- [x] USB metadata investigation and selected full-file feasibility tests.
- [x] Explicit scope: camera photos/videos and screenshots; no app-cache importing.
- [x] Filename-day selection, registered identity, safe full copies, journal retries and atomic day/boundary checkpoints.
- [x] Production import: 130 files/947,962,157 bytes verified, then a repeat run copied 0 bytes.
- [x] Fix archive finish timing, distinct dry-run destination proposals and ignored-nonmedia counts.
- [x] User unplug/replug followed by successful matching of existing registration.
- [x] New same-day Camera photo copied once; boundary keys 4 → 5; later preview selects 0 files.
- [x] Real-USB cooperative cancellation between files and resume in isolated state/output; one verified copy reused, one copied, production state unchanged.
- [x] Historical instructions archived; current handoff replaced with actionable state.
- [x] Full Release suite 341/341 passing; clean build; one independent bounded-change review with no findings.
- [x] Empty current-session staging directory removed after successful archival; incomplete/nonempty/other sessions preserved, cleanup failure warns. Seven new filesystem/recovery tests; Release suite 348/348 passing.

## Current production state

S21 destination `C:\Users\eu\Pictures\Photos\_incoming\auto_s21`: 131 media files/950,297,811 bytes. Checkpoint 2026-10-18, tracked, 5 boundary keys. No pending production session or active command. One legacy Office Lens filename remains intentionally skipped because its date prefix is unrecognized.

## Remaining / explicit limits

- iPhone validation started: 5,217 supported images / 6,393 resources inventoried; four selected photo/video resources fully read in Automatic and Keep Originals with identical hashes. One reconnect retained device identity but changed all file persistent IDs. User confirmed JPEG/PNG/MOV sample days and HEIC 1280×960 against Photos. General date policy, original identity and Library coverage remain open; current Android source/date rules are not accepted for iPhone. See [iPhone assessment](compatibility/iphone-17e-ios-26.md).
- Bump the application version when iPhone support is added or validated; include the support/validation change in the release notes.
- Physical cable-pull/mid-native-read interruption is not covered by the cooperative recovery test; automated failure/partial-read coverage and real between-file cancellation are separate evidence.
- Filename-policy, backend completeness and identity/content-evidence limits remain documented; no additional Android implementation task is pending here.

Detailed evidence: [follow-up](compatibility/2026-10-18-android-follow-up-validation.md), [hardware acceptance](compatibility/filename-importer-validation.md). Older milestones/decisions are in the [archived work log](archive/2026-10-18-work-status-before-reporting-cleanup.md), which is historical and must not be used as a remaining-work list. No merge/push requested.
