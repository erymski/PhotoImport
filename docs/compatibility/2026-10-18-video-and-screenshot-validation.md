# Camera-video and screenshot date validation — 2026-10-18

The user approved a small validation spike: copy one camera video, inspect its embedded capture-day evidence using the existing `photo-sort` MetadataExtractor approach, and inspect screenshots already copied. A subsequent bounded ICC-warning correction is recorded below. No app-cache reads, phone mutations or import/checkpoint writes were performed.

## Camera video

- Enumerated only direct children of `Internal storage/DCIM/Camera`; found 14 video-extension entries. Selected the smallest nonempty MP4/MOV to limit this one-file validation.
- Selected `DCIM/Camera/20260909_082607.mp4`, reported **4,016,530 bytes**. The full stream reached EOF and received/saved/reported lengths agreed. Local SHA-256 saved privately; no source-side hash comparison.
- Enumeration **123.382 seconds**; transfer and local flush **0.298 seconds**; whole diagnostic **123.928 seconds**. This small sample is not a general video-throughput benchmark. Cleanup completed with no reported errors.
- MetadataExtractor detected MP4,1920×1080 video, approximately 1.133 seconds duration. Its QuickTime Movie Header `Created` field parses as `2026-09-09T06:26:10` with `DateTimeKind.Unspecified`, candidate day **2026-09-09**. This is the same MP4 tag used by `photo-sort`; file modification time and filename are not capture-date evidence.
- The metadata header reports unsupported keys `com.android.capture.fps` and `com.samsung.android.utc_offset`. The offset value is not exposed by this parser. No PC-timezone shift was performed. A successful same-day comparison does **not** establish correct local capture days for videos near midnight; offset semantics/handling remain a production-design requirement. No custom MP4/EXIF decoder was created.
- The user independently confirmed that Gallery/details shows **9 September 2026** for this video. Day-level sample validation is complete. No MOV sample or midnight-offset case was tested; no general format-wide/video date acceptance claim.

## Saved screenshots

All 83 saved screenshot files were JPEG, despite PNG being a plausible screenshot format elsewhere. No new screenshot or phone content reads were required; local lengths were checked before parsing.

| Resolver result | Before ICC correction | After correction |
| --- | ---: | ---: |
| Available EXIF calendar day | 63 | 81 |
| Invalid due to unrelated ICC-profile error | 19 | 0 |
| Missing EXIF capture day | 1 | 2 |

All 19 affected rows have the ICC error `year=0 month=0 day=0 hour=0 minute=0 second=0`, a colour-profile timestamp. Eighteen also have a valid EXIF DateTimeOriginal day. The old resolver rejected any non-Apple-maker-note parser error globally. **Implemented bounded correction:** ICC-profile errors are preserved as warnings while capture metadata is validated independently; actual capture corruption, impossible dates and conflicting days remain failures. Five behavioral regressions failed before the fix, then all 265 Release tests passed. Local replay of all 83 saved files yielded 81 available days and two missing dates; no phone reads were needed.

The two genuinely undated screenshots are now explicit user-directed exclusions from this experiment: `Screenshot_20180401-214909_Facebook.jpg` and `Screenshot_20190214-150714_Instagram.jpg`. Copies were preserved; no filename or filesystem timestamp is substituted. This is not a new blanket production rule to discard all undated media. The user independently confirmed **18 January 2022** in Gallery/details for `DCIM/Screenshots/Screenshot_20220118-195311_Duolingo.jpg`, validating that example's EXIF day despite its ICC warning.

The user initially suggested a filename fallback based on `photo-sort`, then directed skipping the undated files instead. Read-only inspection of the current `C:\Projects\photo-sort` checkout found metadata extraction only; its caller skips on failure. No fallback parser was found there, no filename parser was added here, and that repository's unrelated user changes were left untouched.

## Evidence and next decisions

All raw evidence is Git-ignored:

- `artifacts/media-date-validation/`: throwaway diagnostic source/project. Builds with zero warnings/errors; not a production API or importer.
- `artifacts/android-s21-one-camera-video-2026-10-18/`: source inventory, journal, full selected video and transfer report.
- `artifacts/android-s21-video-date-assessment-2026-10-18.json`: corrected local-only date assessment using explicit `photo-sort` MP4/MOV tags. The first transfer report's general date-name collector missed the tag named `Created`; its filesystem-date candidate must not be used. That collector was corrected and the saved video re-read locally, without another USB copy.
- `artifacts/android-s21-screenshot-dates-2026-10-18.json`: all 83 local parser results, raw tag evidence and current resolver outcomes.
- `artifacts/android-s21-screenshot-day-assessment-2026-10-18.json`: diagnostic EXIF-only day evidence, preserving ICC errors separately. Production policy unchanged.
- `artifacts/android-s21-gallery-day-confirmation-2026-10-18.json`: records the user's independent day confirmation for both samples.
- `artifacts/android-s21-screenshot-dates-after-icc-fix-2026-10-18.json`: full offline replay using the corrected production resolver, 81 available/two missing.
- `artifacts/android-s21-undated-screenshot-exclusions-2026-10-18.json`: exact source paths, saved lengths and explicit skip decisions for the two undated screenshots. Files are retained and import state is untouched.

No diagnostic remains running. Both Gallery comparisons and the bounded ICC correction are complete. Next: resolve video day/timezone policy and present the scoped resumable importer design, including the explicit two-file exclusions. Complete-stream staging plus a durable per-file date/journal cache remains a recommendation, not an approved replacement for the original metadata-first requirement.
