# Handoff — 2026-10-18

## Current result

`CaptureDayResolver` now preserves valid EXIF days despite unrelated ICC-profile errors, retaining those errors as warnings. Missing, impossible, conflicting and corrupt EXIF dates remain unresolved. Five regressions failed before the correction; **265/265 Release tests pass**. Offline replay of all 83 saved screenshots resolves 81 dates. The user directed skipping the two remaining undated legacy screenshots; they are explicitly recorded in `artifacts/android-s21-undated-screenshot-exclusions-2026-10-18.json`. No files were deleted, no filename fallback was added, and this does not create a blanket policy to discard every undated file.

One independent review found no Critical/Important/Minor code issues. The exact-time outcome remaining invalid is intentionally separate from capture-day validity and was checked through the planner. Product importer policy remains outside this bounded correction; docs were updated separately. Hardware/replay/test results come from this session, not the read-only reviewer. No second review was performed.

The user confirmed both sample days in Gallery: camera video `20260909_082607.mp4` → 9 September 2026, screenshot `Screenshot_20220118-195311_Duolingo.jpg` → 18 January 2022. The video fully copied (4,016,530 bytes), among 14 inventoried MP4 entries. Samsung video UTC-offset metadata is not exposed by the parser, so midnight/day-boundary handling remains an importer-design decision. See [video/screenshot evidence](compatibility/2026-10-18-video-and-screenshot-validation.md). No diagnostic is running; no further phone reads are needed for the completed ICC fix.

The user clarified the source scope: camera photos, camera-recorded videos and screenshots. **Videos are required**, superseding the earlier version-1 exclusion. One MP4 transfer has now been validated as described above; general video-day policy is still open. Exclude app caches, stickers, game assets and downloaded/shared images. Do not resume all-storage copying or app-cache debugging. The still-image copy experiment is complete for the selected roots in the saved inventory:

| Root | Copied | Saved bytes |
| --- | ---: | ---: |
| `DCIM/Camera` | 3,601 | 12,440,778,898 |
| `DCIM/Screenshots` | 83 | 40,947,365 |
| `Samsung/Pictures/Camera Roll` | 340 | 1,045,742,521 |
| Total | 4,024 | 13,527,468,784 |

All selected still-image files reached EOF and matched reported/saved sizes. Follow-up local length checks found zero mismatches. Camera completed in one connection in **1h 3m 34s (11.59 GiB)**; other roots were copied in separate phases, so there is no measured continuous combined time. This is full exposed-resource access, without a phone-side hash comparison or original-asset identity proof. The saved still-image inventory was not refreshed. Video product support remains unimplemented, with one successful transfer sample and unresolved capture-day/timezone policy.

No test remains running. No phone files or import/checkpoint state were changed. Unrelated earlier artifact copies were preserved and excluded from the selected result; do not delete them without instruction.

## Evidence and scope

- [Experiment report](compatibility/2026-10-17-full-copy-experiment.md): final scope, selected results, timings and historical broader attempts. Its old all-storage resume instructions are explicitly superseded.
- `artifacts/android-s21-camera-screenshot-copy-scope-2026-10-18.json`: filtered inventory/completion counts and local length verification.
- Copies and flushed journals are under ignored `artifacts/android-s21-full-copy-*/` directories. Journal entries map each source path to its saved destination.
- Throwaway harness: `artifacts/full-copy-experiment/Program.cs.txt`, built with zero warnings/errors. It now allows only the three canonical roots above. The proposed ID-based lookup optimization was never run and was reverted. Cooperative `STOP`-file support exists between files but was not physically tested.
- App-cache attempts encountered generic GetStream failures, source-ID mismatches and missing paths. Some missing-path errors coincided with later discovery finding no phone. They do not invalidate successful Camera/screenshot copying, and their causes are not established.
- [Specification section 8](specs.md#8-media-scope-and-output-behavior) records the source clarification. An image extension alone does not establish eligibility. Folder scope must be explicit, not the entire Pictures tree or whole storage.

## Product implementation and remaining decisions

Next: present the scoped importer design, including camera videos, video timezone/day-boundary handling and the two explicit undated-screenshot exclusions. The Gallery comparison and ICC correction are complete. Then present the scoped importer design: complete streams, local metadata and a durable per-file journal/date cache, so successful known files are reused and older media is not repeatedly copied. Apply cutoff **2026-09-03**, preserve unresolved dates, and advance checkpoints only after verified success. The staging/cache approach is a recommendation, not an approved or implemented replacement for the original metadata-first contract. The one-video/screenshot spike has been executed; do not repeat it without a specific new question.

Branch `feat/metadata-probe`, current checkout. No merge or push requested. Product code was not changed by the copy experiment.

- MediaDevices 2.0.0 / MetadataExtractor 2.9.3 / .NET 10 Windows.
- `probe`: metadata-only. `exif`: bounded explicit JPEG/HEIC/HEIF/DNG diagnostic. `plan`: offline date selection. `preview`: explicit roots, bounded capture attempts, partial evidence reports. No production importer or checkpoint persistence yet.
- Last product verification: **265/265 Release tests** after the ICC-warning correction, plus the offline 83-screenshot replay. Tests do not prove general USB stability.
- Bounded prefix-reading Camera previews previously failed after 6–25 successful reads. Full Camera streaming subsequently succeeded for all 3,601 inventoried files. The partial-read pattern remains a hypothesis for earlier failures, not a proven cause or implemented fix. Do not silently drain full streams inside the bounded product preview.
- Future product selection must honor the clarified camera/screenshot roots. A production move to full copies/local EXIF parsing needs an explicit behavior decision and the selected Superpowers workflow; the experiment does not implement it.
- Capture **day** is sufficient. Actual completed import cutoff: **2026-09-03**, excluding that whole day and earlier days. Do not infer capture dates from filenames or modification times.
- iPhone is deferred. Counts/format-wide coverage and unresolved metadata remain acceptance limits. See [Samsung assessment](compatibility/android-samsung-sm-g991b.md) and [work status](work-status.md).

## Workflow constraints

Use Superpowers; ignore Ponytail. Use Markdown tracking, not Beads. Preserve current checkout/feature branch, native execution and one final independent review for product increments. Do not restart prior completed design gates. The full-copy experiment was explicitly authorized; no further cache work is wanted.

`C:\Projects\photo-sort` was inspected read-only and already uses MetadataExtractor on local files. Do not run its sorter: it can move/delete files and has unrelated user changes. The S21 uses ordinary USB File transfer; no ADB, phone app or USB debugging. No hardware request is needed until a specific new validation is justified.
