# Current handoff — 2026-10-19

## Ready for normal Android use

Current checkout is `main`; the prior changes were committed and pushed as `0fdb3cd` at the user's request. The registered S21 workflow is implemented and physically exercised: connect/unlock, select USB File transfer, keep awake and run `bin\Release\net10.0-windows\phone-photo-sync.exe`. With no arguments it syncs the unique connected registered phone. `--help` prints help; `sync --device S21 --dry-run` previews without media reads/state changes; `status` is offline. Several connected matches require an explicit registered alias.

- Alias: `S21`, serial-based identity. Do not publish the enrolled serial or rerun init/reset progress.
- Destination: `C:\Users\eu\Pictures\Photos\_incoming\auto_s21`.
- Roots: internal-storage `DCIM/Camera`, `DCIM/Screenshots`, `Samsung/Pictures/Camera Roll`.
- Current checkpoint: **2026-10-18, tracked mode, 5 boundary keys**. Initial completed cutoff was 2026-09-03; do not restore it.
- Incoming media: **131 files, 950,297,811 bytes**. No pending production journal. No command running.
- State/history: `%LOCALAPPDATA%\PhonePhotoSync\`. An interrupted run resumes with verified local evidence; never discard its journal merely to clear an error.

## Latest completed work

The user reports the iPhone's last synchronization date as **6 December 2025 (`2025-12-06`)**. Retain it for future iPhone import planning; no exact time/full-day completion was specified and no iPhone registration/checkpoint has been written.

Manual follow-up on missing `IMG_8124`: the user could not find it and clarified that the phone belongs to someone else, with unknown deletion/edit history. The older resource delta is inconclusive, not a proven backend failure or verified deletion. No further older-photo search is pending. The three new selected samples remain the controlled evidence for complete reads, dimensions and embedded dates.

On 19 October, resumed iPhone experiments: four prior metadata samples retain paths/sizes but have different IDs from the prior connection; two fresh processes within today's connection agree on IDs. User-selected new camera photo `IMG_8363.JPG`, video `IMG_8364.MOV` and screenshot `IMG_8366.PNG` were fully read (14,536,893 bytes), with matching EOF/lengths/local hashes, Photos dimensions and embedded capture-day candidates for 19 October. JPEG orientation explains encoded 5712×4284 versus displayed 4284×5712; JPEG/MOV metadata reports iPhone 17e. The initial photo name `IMG_8386` was corrected by the user before any photo content read. Only October folder metadata was enumerated, not the full library. Its delta includes three new samples and 16 unavailable prior resources; user is unsure about intervening edits/deletions. One old missing path also failed direct metadata lookup while a fresh positive control succeeded, so do not assume physical deletion or complete census. See [follow-up](compatibility/2026-10-19-iphone-follow-up.md). Previous HEIC camera origin remains unverified. No production state changes or USB command running.

Empty staging-session cleanup is implemented after successful archival, including commit-recovery archival. Only the current empty session directory is removed; the `.phone-photo-sync` parent, other sessions and nonempty directories are preserved. Parent directory handles prevent junction redirection during cleanup. Cleanup I/O/access failures warn without changing successful import status. Seven new filesystem/recovery tests bring the Release suite to 348/348. No production USB run or cleanup of historical directories was performed for this increment.

All three deferred reporting issues are fixed: capture completion time before archiving; share filename reservation logic between dry run and real planning; count ignored nonmedia entries in preview/console/history without reading their metadata/content. Existing historical archives remain unchanged. **341/341 Release tests pass**, including five reporting regressions; clean Release build. One independent review of this bounded increment found no issues and did not rerun tests or hardware checks.

Hardware follow-up after the user's reported unplug/replug:

1. Existing registration matched; no re-enrollment needed.
2. Exactly one new same-day Camera photo, `20261018_152453.jpg`, 2335654 bytes, selected and copied. Its local hash matches history; previous 130 files remained excluded. Checkpoint day stayed 2026-10-18; boundary keys increased 4 → 5.
3. Subsequent metadata-only preview selected 0 files/0 bytes, with the one known Office Lens unknown-date filename still visibly skipped.
4. Isolated real-USB recovery probe copied two previously imported camera JPEGs, 2241122 bytes total. Cooperative cancellation **between complete files** left one completed entry and the isolated cutoff unchanged. Resume verified/reused that file, copied only the second, committed isolated progress and cleared pending state. Exactly 2 USB media opens across both runs; both local length/hash checks passed. Production config hash remained unchanged by this probe. All test copies/state are ignored artifacts, not additional incoming media.

Evidence/procedure: [Android follow-up](compatibility/2026-10-18-android-follow-up-validation.md), [acceptance record](compatibility/filename-importer-validation.md), and ignored `artifacts/s21-android-follow-up-verification-2026-10-18.json` / `artifacts/s21-isolated-recovery-2026-10-18/result.json`.

## Limits and remaining scope

- iPhone validation started: see [iPhone assessment](compatibility/iphone-17e-ios-26.md). 5,217 supported images / 6,393 total resources inventoried; four JPEG/HEIC/PNG/MOV samples reached EOF and matched sizes in Automatic and Keep Originals, with identical hashes across the switch/reconnect. Device serial/Windows IDs stayed unchanged, but all 6,393 file persistent IDs changed. User confirmed Photos dimensions 1280×960 for HEIC (matching transferred image, despite EXIF 3264×2448) and JPEG/PNG/MOV sample days. Library-count reconciliation, general date policy and volatile source-ID handling remain open. No command running, registration or production iPhone support claimed. Android filename policy needs no EXIF/video timezone parsing.
- When iPhone support is added or validated, bump the application version and document that support/validation in the release notes.
- A physical cable pull or cancellation inside a native USB read was **not** tested by the cooperative recovery probe. Automated tests exercise failure/partial-file recovery; a blocked native call can delay cancellation. Do not promise immediate interruption or label this a cable-pull test.
- Filename days can differ from EXIF days. Unknown names are skipped/reported; older late additions and same-path/same-size content replacements retain the documented policy limits.
- MediaDevices may suppress native enumeration errors. API traversal is not an independent physical census. Local SHA-256 verification is not a phone-side hash comparison/original-asset proof.
- Commit-finalization-only recovery does not rescan inventory or reconstruct original session counters; a previously written matching archive is preserved.

No Android implementation tasks remain from this follow-up. Do not repeat prior bulk-copy/registration/design gates. Future hardware operations need a concrete validation question; preserve production state/media. The user requested committing and pushing the 19 October experimental documentation; private evidence and media stay Git-ignored.

## Workflow and history

Use Superpowers, ignore Ponytail, Markdown tracking rather than Beads. Native execution/current checkout and one final independent review per increment were selected. Do not run or modify `C:\Projects\photo-sort`; it was read-only reference material and can move/delete media.

The old handoff's running/pending instructions are archived as historical context only: [prior handoff](archive/2026-10-18-handoff-before-reporting-cleanup.md), [prior work log](archive/2026-10-18-work-status-before-reporting-cleanup.md). This current handoff supersedes those instructions. Read [README](../README.md) for operation, not the archived setup/probe plans.
