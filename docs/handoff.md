# Handoff — 2026-10-18

## Current result

The user clarified the source scope: camera photos, camera-recorded videos and screenshots. **Videos are required**, superseding the earlier version-1 exclusion, but have not been copied or validated. Exclude app caches, stickers, game assets and downloaded/shared images. Do not resume all-storage copying or app-cache debugging. The still-image copy experiment is complete for the selected roots in the saved inventory:

| Root | Copied | Saved bytes |
| --- | ---: | ---: |
| `DCIM/Camera` | 3,601 | 12,440,778,898 |
| `DCIM/Screenshots` | 83 | 40,947,365 |
| `Samsung/Pictures/Camera Roll` | 340 | 1,045,742,521 |
| Total | 4,024 | 13,527,468,784 |

All selected still-image files reached EOF and matched reported/saved sizes. Follow-up local length checks found zero mismatches. Camera completed in one connection in **1h 3m 34s (11.59 GiB)**; other roots were copied in separate phases, so there is no measured continuous combined time. This is full exposed-resource access, without a phone-side hash comparison or original-asset identity proof. The saved inventory was not refreshed. Videos remain required, unimplemented and unvalidated.

No test remains running. No phone files or import/checkpoint state were changed. Unrelated earlier artifact copies were preserved and excluded from the selected result; do not delete them without instruction.

## Evidence and scope

- [Experiment report](compatibility/2026-10-17-full-copy-experiment.md): final scope, selected results, timings and historical broader attempts. Its old all-storage resume instructions are explicitly superseded.
- `artifacts/android-s21-camera-screenshot-copy-scope-2026-10-18.json`: filtered inventory/completion counts and local length verification.
- Copies and flushed journals are under ignored `artifacts/android-s21-full-copy-*/` directories. Journal entries map each source path to its saved destination.
- Throwaway harness: `artifacts/full-copy-experiment/Program.cs.txt`, built with zero warnings/errors. It now allows only the three canonical roots above. The proposed ID-based lookup optimization was never run and was reverted. Cooperative `STOP`-file support exists between files but was not physically tested.
- App-cache attempts encountered generic GetStream failures, source-ID mismatches and missing paths. Some missing-path errors coincided with later discovery finding no phone. They do not invalidate successful Camera/screenshot copying, and their causes are not established.
- [Specification section 8](specs.md#8-media-scope-and-output-behavior) records the source clarification. An image extension alone does not establish eligibility. Folder scope must be explicit, not the entire Pictures tree or whole storage.

## Product implementation and remaining decisions

Next recommended increment: validate capture-day extraction and a complete transfer for a camera video, using the MP4/MOV MetadataExtractor logic in `C:\Projects\photo-sort\app\DateExtractor.cs` as a reference. Validate screenshot dates from the already copied files as well. Then design the scoped importer around complete streams, local metadata and a durable per-file journal/date cache, so successful known files are reused and older media is not repeatedly copied. Apply the day cutoff2026-09-03, preserve unresolved dates, and advance checkpoints only after verified success. The staging/cache approach is a recommendation, not an approved or implemented replacement for the current metadata-first contract. No new hardware reads were performed while recording this requirement.

Branch `feat/metadata-probe`, current checkout. No merge or push requested. Product code was not changed by the copy experiment.

- MediaDevices 2.0.0 / MetadataExtractor 2.9.3 / .NET 10 Windows.
- `probe`: metadata-only. `exif`: bounded explicit JPEG/HEIC/HEIF/DNG diagnostic. `plan`: offline date selection. `preview`: explicit roots, bounded capture attempts, partial evidence reports. No production importer or checkpoint persistence yet.
- Last product verification: **260/260 Release tests**, zero build warnings/errors after `369e185`. Later experiment changes are ignored harness/documentation work, not a USB-stability guarantee from tests.
- Bounded prefix-reading Camera previews previously failed after 6–25 successful reads. Full Camera streaming subsequently succeeded for all 3,601 inventoried files. The partial-read pattern remains a hypothesis for earlier failures, not a proven cause or implemented fix. Do not silently drain full streams inside the bounded product preview.
- Future product selection must honor the clarified camera/screenshot roots. A production move to full copies/local EXIF parsing needs an explicit behavior decision and the selected Superpowers workflow; the experiment does not implement it.
- Capture **day** is sufficient. Actual completed import cutoff: **2026-09-03**, excluding that whole day and earlier days. Do not infer capture dates from filenames or modification times.
- iPhone is deferred. Counts/format-wide coverage and unresolved metadata remain acceptance limits. See [Samsung assessment](compatibility/android-samsung-sm-g991b.md) and [work status](work-status.md).

## Workflow constraints

Use Superpowers; ignore Ponytail. Use Markdown tracking, not Beads. Preserve current checkout/feature branch, native execution and one final independent review for product increments. Do not restart prior completed design gates. The full-copy experiment was explicitly authorized; no further cache work is wanted.

`C:\Projects\photo-sort` was inspected read-only and already uses MetadataExtractor on local files. Do not run its sorter: it can move/delete files and has unrelated user changes. The S21 uses ordinary USB File transfer; no ADB, phone app or USB debugging. No hardware request is needed until a specific new validation is justified.
