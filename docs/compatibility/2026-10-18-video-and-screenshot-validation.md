# Camera-video and screenshot date validation — 2026-10-18

The user approved a small validation spike: copy one camera video, inspect its embedded capture-day evidence using the existing `photo-sort` MetadataExtractor approach, and inspect screenshots already copied. No production feature was implemented. No app-cache reads, phone mutations or import/checkpoint writes were performed.

## Camera video

- Enumerated only direct children of `Internal storage/DCIM/Camera`; found14 video-extension entries. Selected the smallest nonempty MP4/MOV to limit this one-file validation.
- Selected `DCIM/Camera/20260909_082607.mp4`, reported **4,016,530 bytes**. The full stream reached EOF and received/saved/reported lengths agreed. Local SHA-256 saved privately; no source-side hash comparison.
- Enumeration **123.382 seconds**; transfer and local flush **0.298 seconds**; whole diagnostic **123.928 seconds**. This small sample is not a general video-throughput benchmark. Cleanup completed with no reported errors.
- MetadataExtractor detected MP4,1920×1080 video, approximately1.133 seconds duration. Its QuickTime Movie Header `Created` field parses as `2026-09-09T06:26:10` with `DateTimeKind.Unspecified`, candidate day **2026-09-09**. This is the same MP4 tag used by `photo-sort`; file modification time and filename are not capture-date evidence.
- The metadata header reports unsupported keys `com.android.capture.fps` and `com.samsung.android.utc_offset`. The offset value is not exposed by this parser. No PC-timezone shift was performed. A successful same-day comparison does **not** establish correct local capture days for videos near midnight; offset semantics/handling remain a production-design requirement. No custom MP4/EXIF decoder was created.
- The user independently confirmed that Gallery/details shows **9 September 2026** for this video. Day-level sample validation is complete. No MOV sample or midnight-offset case was tested; no general format-wide/video date acceptance claim.

## Saved screenshots

All83 saved screenshot files were JPEG, despite PNG being a plausible screenshot format elsewhere. No new screenshot or phone content reads were required; local lengths were checked before parsing.

| Evidence | Count |
| --- | ---: |
| Current production day resolver: available | 63 |
| Current resolver: invalid due to ICC-profile error | 19 |
| Current resolver: missing EXIF capture day | 1 |
| Independent valid EXIF calendar-day candidates, including ICC-warning files | 81 |
| No EXIF capture date at all | 2 |

All19 parser-error rows have the ICC error `year=0 month=0 day=0 hour=0 minute=0 second=0`, a colour-profile timestamp. Eighteen of them also have a valid EXIF DateTimeOriginal day. The existing resolver rejects any non-Apple-maker-note parser error globally, so those18 are rejected even though the date error is outside capture metadata. **Proposed bounded correction:** retain unrelated ICC errors as warnings while validating capture metadata independently; continue rejecting actual capture-field corruption/ambiguity. Not implemented or accepted as a policy change by this spike.

The two genuinely undated screenshots remain unresolved; neither filename nor the local modification timestamp is substituted. Their dates need an explicit policy or verified manual input before automated day selection can handle them. The user independently confirmed **18 January 2022** in Gallery/details for `DCIM/Screenshots/Screenshot_20220118-195311_Duolingo.jpg`, one of the ICC-warning examples. This validates the example's EXIF calendar day despite the unrelated ICC warning.

## Evidence and next decisions

All raw evidence is Git-ignored:

- `artifacts/media-date-validation/`: throwaway diagnostic source/project. Builds with zero warnings/errors; not a production API or importer.
- `artifacts/android-s21-one-camera-video-2026-10-18/`: source inventory, journal, full selected video and transfer report.
- `artifacts/android-s21-video-date-assessment-2026-10-18.json`: corrected local-only date assessment using explicit `photo-sort` MP4/MOV tags. The first transfer report's general date-name collector missed the tag named `Created`; its filesystem-date candidate must not be used. That collector was corrected and the saved video re-read locally, without another USB copy.
- `artifacts/android-s21-screenshot-dates-2026-10-18.json`: all83 local parser results, raw tag evidence and current resolver outcomes.
- `artifacts/android-s21-screenshot-day-assessment-2026-10-18.json`: diagnostic EXIF-only day evidence, preserving ICC errors separately. Production policy unchanged.
- `artifacts/android-s21-gallery-day-confirmation-2026-10-18.json`: records the user's independent day confirmation for both samples.

No diagnostic remains running. The earlier unlock question is obsolete because the transfer finished. Both Gallery day comparisons are complete. Next: the bounded capture-error policy correction, then video day/timezone and undated-file rules and the scoped resumable importer design. The correction should affect only `CaptureDayResolver`: keep ICC-profile errors as reported warnings when EXIF capture metadata is valid; preserve missing, impossible, conflicting or corrupt EXIF outcomes. Verify available/missing/conflicting/broken-EXIF cases with ICC warnings and replay the83 local screenshots without phone reads. Complete-stream staging plus a durable per-file date/journal cache is a recommendation, not yet an approved replacement for the original metadata-first requirement.
