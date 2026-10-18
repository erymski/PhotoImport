# Filename-selected copy trial — 2026-10-18

The user approved trying filename dates to select copies, following the question “why for copying we cannot rely on filename?” This is an experiment, not an implemented importer. No EXIF content reads are needed to select candidates. It does not approve staging unknown-date files, changing checkpoints, or modifying the phone.

## Selection rules

- Only explicitly scoped camera/screenshot roots: `DCIM/Camera`, `DCIM/Screenshots`, and `Samsung/Pictures/Camera Roll` in internal storage.
- Include camera videos as well as still images/screenshots; exclude app caches and downloaded/shared-media folders.
- Recognize an initial `YYYYMMDD` followed by end of stem, `_`, or `(`; `Screenshot_YYYYMMDD` followed by end, `-`, or `_`; and `WP_YYYYMMDD` followed by end or `_`.
- Validate the calendar date. Select dates strictly after **2026-09-03**, excluding that whole cutoff day. Hours and seconds are irrelevant.
- Report and skip invalid/unrecognized names. Do not silently use EXIF, modification time, or full downloads to guess their dates.
- Preserve media bytes; use a new ignored destination, no overwrite and no import-state changes.

Renamed files or names such as `IMG_1234` cannot establish a date under these rules. A filename date is selection evidence, not an independently verified capture date.

## Offline result

Saved inventory: 4,024 scoped still images plus 14 Camera videos. Filename selection produces **124 candidates: 114 photos/screenshots and 10 videos, 946,387,560 bytes (902.55 MiB)**. 3,913 are on/before the cutoff; one name is unrecognized: `Office Lens_20160714_135129_processed(1).jpg`.

Nine synthetic filename/cutoff/calendar checks and two excluded-root checks passed, including cutoff equality, leap days, invalid dates, unknown names, and a similarly prefixed sibling folder. This checks the throwaway selector, not USB reliability.

Evidence: `artifacts/android-s21-filename-selection-saved-refined-2026-10-18/{selection,selected-inventory}.json`; selector `artifacts/filename-selection-trial/Select-Media.ps1`.

Offline comparison against the existing local EXIF-day catalog: **4,009 comparable still images; 3,995 matching days, 14 differing days, zero differing selection decisions at this cutoff**. All 14 differences are older than the cutoff in both sources. Therefore filename dates do not always equal EXIF capture days, although the differences do not affect this particular selection. Causes were not investigated. The 15 missing-EXIF stills and videos are not covered by this comparison. Evidence: `artifacts/filename-selection-trial/saved-exif-comparison-2026-10-18.json`.

## Live status

Initial fresh scoped metadata discovery found zero connected S21 devices. After the user connected/unlocked it, the first scoped lookup returned Camera not found. A subsequent diagnostic logged the exposed `Internal storage` root and successfully completed enumeration; the earlier lookup failure's cause is unproven. Failed reports were preserved.

**Live trial completed successfully:** current metadata inventory's saved measurement is **24.385 seconds** (console including report writing: 24.464 seconds), 4,040 entries, `apiCompleted=true`, no reported errors, no content reads. Filename selection: **126 candidates (116 photos/screenshots, 10 videos), 946,969,983 bytes (903.10 MiB)**; 3,913 older/cutoff and one unknown name skipped. The two additional candidates compared with the saved inventory are photos.

Copy destination/journal: `artifacts/android-s21-filename-copy-2026-10-18/`. Current inventory: `artifacts/android-s21-filename-storage-check-2026-10-18.json`. Selection: `artifacts/android-s21-filename-selection-live-2026-10-18/`. Copying was started with the existing full-copy harness and an empty prior-events journal, so previous experiment copies do not suppress this trial. Progress is flushed per file. A `STOP` file in the copy destination requests stopping between files; a synchronous USB call can delay it. Product implementation remains a later step.

All 126 selected files reached EOF successfully in one copy session: **127.249 seconds (2 m 7 s), 7.10 MiB/s**, no reported errors, no cancellation. Metadata enumeration plus copying measured 151.634 seconds (about 2m32s), excluding offline selection/process/report overhead. Separate local verification confirms exact selected source-path coverage, all 126 local lengths equal received and inventoried lengths, no duplicate completed paths and no partial files. See `summary.json`, `events.jsonl` and `local-verification.json` in the copy destination. No phone-side hash comparison was performed.

No process remains running. No phone files or import/checkpoint state were changed. This validates filename-selected full copying for this Android inventory/session, not a production importer, iPhone support, or guaranteed future USB reliability. Next product work is a scoped filename-first importer design with collision handling, resume/journal and explicit checkpoint rules. Staging unknown-date files is not approved. The legacy copier console says “photos” and “no date filtering”: these refer to its input rows and copying stage; videos are present in the input and filename filtering already occurred in the separate selector.
