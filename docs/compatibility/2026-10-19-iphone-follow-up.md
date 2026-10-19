# iPhone follow-up — 2026-10-19

At the user's request, continued the [iPhone assessment](iphone-17e-ios-26.md) on checkout `0fdb3cd`. Today's first question was whether the four previously selected resources retain their reported file IDs across another connection, and whether those IDs remain stable between fresh processes within the current connection. No transfer-mode change was requested today; its current value is not observable through this diagnostic.

## Reported previous synchronization

The user reports that this iPhone was last synchronized on **6 December 2025 (`2025-12-06`)**. Preserve this as the starting date information for future iPhone import planning. An exact synchronization time and whether that entire calendar day was covered were not specified; no production cutoff/checkpoint or registration was created or changed by recording this date.

## Explicit metadata comparison

Fresh discovery returned the same serial, Windows device ID, PnP ID and protocol as the prior assessment. The new throwaway harness inspected metadata only for these four explicit paths; it opened no media content, performed no recursive inventory, and accessed no importer configuration/checkpoint or production destination:

| Sample | Path / reported size versus 18 October | Reported persistent ID versus 18 October | ID across today's two fresh processes |
| --- | --- | --- | --- |
| `IMG_8342.JPG` | Unchanged | Changed | Unchanged |
| `IMG_2510.HEIC` | Unchanged | Changed | Unchanged |
| `IMG_8350.PNG` | Unchanged | Changed | Unchanged |
| `IMG_8344.MOV` | Unchanged | Changed | Unchanged |

Both processes returned all four observations with no surfaced errors and no authored dates. First sample inspection took 6.664 seconds; second took 0.160 seconds, including the harness's timed discovery/connection and cleanup. Elapsed time is not USB traffic measurement. The second process matched the first process's serial and device ID; per-file paths, sizes and persistent IDs also matched.

This independently repeats the volatile-file-ID observation for these four samples while showing within-connection stability across these two processes. It does not establish stable content across days without a new content comparison, or explain the driver's reassignment mechanism. Saved path/size observations are useful for locating candidates, not sufficient proof of content identity. No production recovery check was bypassed or changed.

## Samples with known origin

The user previously suggested that `IMG_2510.HEIC` might have been forwarded rather than captured on this iPhone. That remains a hypothesis. Its matching Photos/primary dimensions and equal bytes across transfer modes establish resource access; it is not treated as a proven original camera capture from this phone.

For a separate controlled-origin check, the user was asked to create one ordinary camera photo, one screenshot and a short 1–2 second camera video, retain Keep Originals, and provide filenames and dimensions from Photos. The user supplied the following samples and corrected the initially mistyped photo name `IMG_8386` to `IMG_8363` after clarification:

| Sample / user classification | User-reported Photos dimensions | Exposed resource bytes | Complete transfer seconds | Embedded capture candidate |
| --- | --- | ---: | ---: | --- |
| `IMG_8363.JPG`, camera photo | 4284×5712 | 5,399,916 | 0.360 | EXIF `2026:10:19 23:20:42`, original offset `+02:00` |
| `IMG_8364.MOV`, camera video | 1920×1080 | 5,058,336 | 0.292 | QuickTime metadata creation `2026-10-19 23:20:47 +02:00` |
| `IMG_8366.PNG`, screenshot | 1170×2532 | 4,078,641 | 0.277 | EXIF `2026:10:19 23:22:33`, original offset absent |

All three exact paths were found under `\Internal Storage\202610_a`. The scoped metadata census took 111.845 seconds and exposed 272 files plus the folder itself, with no surfaced errors. A shallow storage-directory listing confirmed that `202610_a` was the only exposed October folder. It listed folder metadata only, not a full recursive library census. No file named `IMG_8386` was opened, and no candidate photo was substituted before the user's correction.

The selected-read harness rechecked phone serial, path, size and currently observed persistent ID before each resource open. New ignored destinations preserved prior evidence. All three reached EOF and matched reported/received/saved lengths: **14,536,893 bytes total**. Fresh offline SHA-256 calculations matched the transfer reports. These are three separate measured transfers, not an end-to-end throughput benchmark; hashes are local, not a phone-side comparison.

The JPEG stores 5712×4284 pixels and EXIF Orientation `6` (90° clockwise), producing displayed dimensions **4284×5712**, matching the user's Photos observation. JPEG EXIF dimensions agree with its encoded dimensions. JPEG EXIF and MOV metadata both identify `Apple` / `iPhone 17e`, corroborating the user-reported model in these newly selected resources. MOV's video track is 1920×1080 with rotation `0`; additional zero-dimension tracks are not mistaken for video dimensions. PNG dimensions are 1170×2532 with orientation `1`, matching Photos.

Offline use of the existing `CaptureDayResolver` resolved both JPEG and PNG to **2026-10-19**. JPEG retains two unrelated Apple maker-note tag-format warnings; its capture/container fields parse. PNG retains a 2022 ICC profile timestamp, which is not used as capture time; its EXIF day resolves without warnings. This PNG check used local complete-file metadata parsing, not the product's JPEG/HEIC-only bounded USB diagnostic.

MOV movie-header Created is `2026-10-19 21:20:47` with parser kind Unspecified; the offset-bearing metadata creation field is `2026-10-19 23:20:47 +02:00`. The two-hour difference is consistent in this sample, and both yield 19 October here. MOV metadata retains unsupported location-accuracy and playback-intent-key warnings. This is not a midnight/timezone-boundary test or an implemented general video-day policy.

A subsequent metadata-only read of the three explicit paths found no authored dates. WPD created and modified wall-clock fields matched the corresponding local embedded capture times above for all three; path/size/persistent-ID checks also agreed within the current connection. This is sample agreement, not permission to substitute created/modified for capture dates generally. The user supplied dimensions and origins in response to the new-sample request; exact clock-time agreement in Photos was not separately asserted.

## October resource delta and unavailable older path

Compared with the saved 18 October census, this folder contained exactly the three new named resources and lacked 16 previously exposed resources, including AAE/edited-name companions: 285 old files + 3 additions − 16 unavailable entries = 272 current files. These are resource counts, not independently verified Library asset counts. No whole-phone or other-month census was repeated.

When asked about deletion, hiding or editing since the prior run, the user answered **not sure**. The reason for the delta is therefore unknown. A narrowly selected metadata-only `GetFileInfo` check of previously exposed `IMG_8124.JPG` also returned not found. A fresh-process positive control immediately afterwards successfully resolved the new `IMG_8363.JPG` with matching serial/path/size/current ID. This establishes present availability of the phone and of the positive control, not physical deletion of the missing resource. Both lookups use the same backend; they are not an independent physical-library census. No missing file content was opened or automatically retried.

For an independent check, the user was asked whether `IMG_8124.JPG` is currently in the ordinary Library, Hidden or Recently Deleted. The old probe's projected created value is `2026-10-06T21:24:48`; it was supplied only as a search aid, not asserted as a verified capture timestamp. The user could not find the photo and explained that the phone belongs to someone else, so intervening library deletions/changes are unknown. This closes the requested manual check with an inconclusive outcome; it does not prove deletion, a USB omission or complete search coverage of all Photos categories. The historical resource delta cannot serve as a controlled completeness test. No further search or retry of these older resources is pending; the three new explicitly selected sample results remain valid independently of this delta.

The new JPEG, PNG and MOV checks establish complete exposed-resource access, matching user-reported resolution and usable embedded day candidates for these selected samples. HEIC camera-origin/original coverage, general date policy, cross-connection source association and Library coverage remain separate acceptance limits. Product code and production importer state were not changed.

## Private evidence

All detailed identifiers remain Git-ignored:

- `artifacts/iphone-sample-metadata/`: metadata-only diagnostic source/build.
- `artifacts/iphone-explicit-metadata-first-2026-10-19.json`
- `artifacts/iphone-explicit-metadata-second-2026-10-19.json`
- `artifacts/iphone-explicit-identity-comparison-2026-10-19.json`
- `artifacts/iphone-october-named-samples-2026-10-19.json`, `artifacts/iphone-storage-folders-2026-10-19.json`, and `artifacts/iphone-october-named-samples-delta-2026-10-19.json`: scoped folder metadata and comparison with the prior snapshot.
- `artifacts/iphone-user-named-camera-photo-2026-10-19.json` and `artifacts/iphone-user-named-camera-photo-copy-2026-10-19/`: explicit corrected photo manifest, full copy and report.
- `artifacts/iphone-user-named-video-screenshot-2026-10-19.json` and `artifacts/iphone-user-named-video-screenshot-copy-2026-10-19/`: explicit video/screenshot manifest, full copies and report.
- `artifacts/iphone-known-origin-verification-2026-10-19.json`: fresh local length/hash checks and oriented JPEG dimensions.
- `artifacts/iphone-user-named-camera-capture-day-2026-10-19.json`, `artifacts/iphone-user-named-screenshot-capture-day-2026-10-19.json`, and `artifacts/iphone-user-named-video-dates-2026-10-19.json`: offline embedded capture-day observations.
- `artifacts/iphone-known-origin-wpd-metadata-2026-10-19.json`: metadata comparison for the three new explicit resources.
- `artifacts/iphone-missing-resource-direct-lookup-2026-10-19.json` and `artifacts/iphone-missing-resource-present-control-2026-10-19.json`: unavailable older-path observation and positive control.

No production iPhone import or support/version claim is made. Library-count reconciliation and general capture-day/source-identity policy remain open. No USB command remains running after these completed experiments. The user requested committing and pushing this documentation follow-up; private diagnostic artifacts remain excluded.
