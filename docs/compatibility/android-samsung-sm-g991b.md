# Samsung Galaxy S21 (SM-G991B)

Assessment date: 2026-10-16, Europe/Madrid. Backend: MediaDevices NuGet 2.0.0 / Windows WPD. Status: **ordinary MTP/PTP properties remain insufficient; tested JPEG/HEIC/DNG capture days agree with Gallery; one complete JPEG resource read verified**. DCIM/Pictures traversal subsequently completed through the API. Synchronization is not yet accepted: other-folder counts and unresolved-format policy remain open. Sample agreement does not establish format-wide support. iPhone is deferred.

## Observed

- Windows discovered a Samsung SM-G991B over USB with protocol `MTP: 1.00`.
- Friendly name, device ID, PnP ID, and a serial-number candidate were exposed. Personal identifiers are retained only in private local artifacts, not this report.
- The first probe encountered native error `0x800701b1` (device does not exist) while reading object properties. MediaDevices wrapped this error in AggregateException; the initial application version crashed before producing JSON.
- The adapter now normalizes the worker exception, preserves the native diagnostic, and exports a partial report with a nonzero exit when such an error is surfaced. Regression tests also cover retaining earlier observations and preserving cancellation through cleanup.
- An interactive run exported `artifacts/android-s21-console-test.json` after unplug/replug surfaced a device-removal error. The private JSON is Git-ignored and contains 13,156 photo observations plus the native failure diagnostic. Its completion status is `incomplete`.
- Metadata enumeration recorded 125.618 seconds before returning the partial result. This is partial-run performance, not the duration of a verified complete inventory. Console rendering and JSON export occur afterward.
- Observed extensions: 12,001 JPG, 1,129 PNG, and 26 DNG. No HEIC/HEIF was observed in this partial inventory.
- The actual `Internal storage/DCIM` subtree contributed 3,721 observations, including 3,601 under `DCIM/Camera`. Another 180 paths contained DCIM under Android trash locations; they are not evidence of additional live camera-folder coverage. The broader scan includes WhatsApp, app caches, and trash and must not be treated as a future import selection policy.
- The main `Internal storage/Pictures` root was not reached before interruption. Some 540 paths contained a Pictures segment elsewhere; these do not verify coverage of that root.
- Authored date: 0/13,156 available. Separate PTP date: unavailable through this adapter. Creation date: 0/13,156 available. Modification date: 13,156/13,156 available as projected wall-clock values. Capture-candidate missing rate is 100%, including the actual camera folder. Modification time was not substituted for capture time.
- Device ID, PnP ID, serial-number candidate, and model were unchanged across this unplug/replug and a fresh discovery process. This is one observed stability check, not a general guarantee.
- Tool-driven Ctrl+C did not yield a `cancelled` hardware report. The interactive run returned only after the USB request released following the physical replug. Actual keyboard Ctrl+C behavior remains unverified; automated tests cover cancellation and its race with device removal. The tool's shell exit was 1; the JSON's `incomplete` status and native error are the observed application evidence.

## Evidence still required

| Item | Status |
| --- | --- |
| Android version / driver version | Not collected |
| Identifier stability after unplug/replug and process restart | Unchanged in one observed replug/fresh-process check; broader stability unverified |
| Complete folder coverage / expected photo count | Scoped API traversal completed: DCIM 3,721 (Camera 3,601), Pictures 188; all-entry Camera count 3,615 matches user total, comprising 3,599 JPG + 2 DNG + 14 MP4 by extension; other folder comparisons pending |
| Actual authored-date availability / missing-date rate | 0 available / 13,156 observed; 100% missing, including 3,601 camera-folder images |
| Capture-date accuracy against EXIF/Picasa | Three bounded JPEG reads obtained EXIF dates; user Gallery comparison confirms day-level agreement, which meets the clarified precision requirement; seconds/offsets not independently verified |
| Timezone/local/UTC interpretation | Not verified; public API dates are projections |
| Enumeration duration and performance | 125.618 seconds for an incomplete run with 13,156 photo observations |
| No-content transfer | Normal probe calls no content APIs; separately approved EXIF command reads bounded JPEG prefixes; driver/USB observation pending |
| Original/full-resolution access | One complete exposed JPEG resource read: 3,002,932 bytes, 4032×2268, expected EXIF retained; independent original-asset identity unverified |

The public package API does not expose raw timestamp variants, a separate PTP date, or reported photo format/content type, and may hide some native enumeration failures. A clean probe alone cannot establish inventory completeness. See [backend notes](backend-notes.md) and the [acceptance procedure](README.md).

## Decision

Date-based synchronization is not yet accepted for this phone. Ordinary WPD properties remain insufficient, while the separately approved bounded EXIF diagnostic provides user-confirmed capture days for three camera JPEGs and one HEIC/DNG sample each. Scoped folder traversal and one complete JPEG resource read pass; other-folder comparisons and PNG capture-date policy remain open. Do not use modification timestamps as an implicit fallback.

## Raw WPD follow-up: MTP and PTP

The user retained normal USB transfer as a requirement and declined ADB/USB debugging. They selected the phone's Transferring images / PTP mode for the comparison. No ADB commands were run and no phone app was installed.

A throwaway diagnostic using the pinned MediaDevices package inspected three existing camera objects spanning 2017, 2023, and 2026. It ran `IPortableDeviceProperties.GetValues` with a null property-key collection, `GetSupportedProperties`, and `GetPropertyAttributes` on the package's own worker thread. It inspected metadata only, with no resource streams, thumbnails, downloads, setters, or mutations. Private reflection was used only in the ignored diagnostic; it is not a supported application backend or retained product implementation.

Both MTP and PTP returned 16 supported properties per sampled object:

| WPD property | Property ID in object-properties GUID | Advertised | Attribute HRESULT |
| --- | --- | --- | --- |
| Date created | 18 | No | `0x80070032` (not supported) |
| Date modified | 19 | Yes | `0x00000000` (success) |
| Date authored | 20 | No | `0x80070032` (not supported) |

Modification time was returned as raw `VT_DATE`, preserving the native OLE-date double. A vendor-mapped MTP `DateAdded` property (`0xDC4E`) was a raw `VT_LPWSTR` string equal to modification time for every sample; it is not accepted as capture time. Format and content-type GUIDs were available at this raw layer, confirming that the public package surface hides some metadata, but it was not hiding the missing authored/creation timestamps in these samples.

| Sample period | Raw modification clock, no offset | Raw OLE-date double | MTP DateAdded |
| --- | --- | --- | --- |
| 2026 | `2026-10-15 19:17:17` | `46310.80366898148` | `20261015T191717` |
| 2023 | `2023-02-25 04:20:53` | `44982.18116898148` | `20230225T042053` |
| 2017 | `2017-10-27 12:03:22` | `43035.502337962964` | `20171027T120322` |

The values and support results were the same in both modes. Source paths and persistent object identifiers resolved to the same three objects. The PTP endpoint reported `PTP: 1.00` rather than `MTP: 1.00`. Device/PnP IDs changed when switching mode; the serial-number candidate stayed unchanged. Therefore the earlier ID stability observation applies only to a reconnect in the same mode, not across USB modes.

Private evidence: `artifacts/android-s21-raw-properties-verified.json`, `artifacts/android-s21-ptp-raw-properties.json`, and `artifacts/android-s21-ptp-devices.txt`. The throwaway diagnostic is under ignored `artifacts/raw-wpd-spike/`. The application project excludes artifact source files from compilation.

This does not prove that every possible Samsung vendor command lacks capture information. It establishes that broadening ordinary WPD property reads does not supply a usable capture timestamp for the tested photos. A raw WPD reader alone is consequently not a demonstrated solution. At this point in the investigation no content-read exception had been approved; the later EXIF exceptions and day-level requirement clarification are recorded below.

Reference-source context, not a claim about Samsung's exact firmware: Android's reference [MTP property handler](https://raw.githubusercontent.com/aosp-mirror/platform_frameworks_base/master/media/java/android/mtp/MtpPropertyGroup.java) supplies modification time for both DateModified and DateAdded, and its [object-info implementation](https://raw.githubusercontent.com/aosp-mirror/platform_frameworks_base/master/media/java/android/mtp/MtpDatabase.java) fills both object-info time slots from modification time. These behaviors are consistent with the observations. Microsoft documents [reading all WPD properties](https://learn.microsoft.com/en-us/windows/win32/wpd_sdk/retrieving-properties-for-a-single-object) separately from media-content access.

## Approved one-JPEG EXIF experiment

On 2026-10-16 the user explicitly authorized one JPEG media-stream read as an exception to the metadata-only rule, capped at 256 KiB at the application boundary. The normal probe was unchanged. The isolated diagnostic used MediaDevices 2.0.0 with GenericRead access and MetadataExtractor 2.9.3 parsing an in-memory prefix after the device stream closed. No image bytes were saved, no full-file fallback was attempted, and no phone files or synchronization state were changed.

| Observation | Result |
| --- | --- |
| Reported protocol | MTP: 1.00 |
| Camera JPEG | `20261015_191713.jpg` |
| Source length | 3,002,932 bytes |
| Application bytes read | 262,144 bytes, 16 reads |
| EXIF DateTimeOriginal / DateTimeDigitized | `2026:10:15 19:17:14` / same |
| EXIF SubSecTimeOriginal / SubSecTimeDigitized | `762` / same |
| EXIF OffsetTimeOriginal / OffsetTime | `+02:00` / same |
| EXIF OffsetTimeDigitized | Absent |
| WPD projected modification time | `2026-10-15 19:17:17`, no offset |
| Entire experiment duration | 38.741 seconds, including discovery and cleanup |
| Outcome | Capture fields found; exit 0; no errors or parser warnings |

The fields express a capture timestamp of `2026-10-15T19:17:14.762+02:00`. It differs from the projected modification time and from the filename clock; neither is used as capture-time truth. Independent gallery/Picasa agreement remains unverified. The measured duration cannot isolate USB reading throughput. Driver read-ahead was not measured and could exceed the application cap.

Private evidence: `artifacts/android-s21-one-jpeg-exif.json`; source: ignored `artifacts/exif-spike/`. The diagnostic built with 0 warnings/errors. This demonstrates feasibility for one JPEG, not library-wide reliability, other-format support, or complete original-file access. The metadata-only WPD property path remains insufficient. The user subsequently approved the [JPEG validation addendum](../superpowers/specs/2026-10-16-android-jpeg-exif-addendum.md) and the three-photo check below.

## Optional JPEG diagnostic: three-photo validation

The implemented `exif` command used MediaDevices 2.0.0 and MetadataExtractor 2.9.3 on the same three previously sampled camera paths. Both JPEG segment navigation and EXIF decoding use MetadataExtractor; no custom EXIF decoder was introduced. The session reported MTP: 1.00. Each read stopped after the first EXIF APP1 segment, with no media bytes saved or full-file fallback. The command exited 0 and exported `completionStatus: complete`.

| Camera JPEG | EXIF DateTime and DateTimeOriginal (equal) | Original subseconds | Original offset | Application bytes read | Per-photo seconds |
| --- | --- | --- | --- | --- | --- |
| `20261015_191713.jpg` | `2026:10:15 19:17:14` | `762` | `+02:00` | 16,468 | 2.095 |
| `20230225_072051.jpg` | `2023:02:25 07:20:51` | `0761` | `+04:00` | 46,518 | 1.755 |
| `20171027_130322.jpg` | `2017:10:27 13:03:22` | Absent | Absent | 474 | 1.836 |

Total: 63,460 application bytes in 5.792 seconds including discovery, connection, reads, parsing, and session cleanup. These measurements do not isolate USB throughput or establish actual USB traffic; driver read-ahead remains unmeasured. All samples had valid DateTimeOriginal clocks. The 2017 sample has no offset, so no UTC instant was derived. Later segments were not inspected. The only parser warnings were the first-segment scope notice and the expected unknown-offset notice; no errors occurred.

The general `DateTime` policy used by the existing `photo-sort` project and `DateTimeOriginal` agree for these samples; no broader equivalence is claimed. WPD modification clocks from the previous raw-property assessment differ from the EXIF clocks, notably the 2023 sample (`04:20:53` versus EXIF `07:20:51 +04:00`). Neither WPD clocks nor filenames are accepted as capture truth.

Private JSON: `artifacts/android-s21-three-jpeg-exif.json`. The user checked Gallery details and reported the other files were fine, while Gallery did not display seconds. On 2026-10-16 they clarified that capture-day precision is sufficient and exact hours/seconds are unimportant. Record the three samples as user-confirmed at calendar-day precision. The first sample's filename encodes `19:17:13` while EXIF reports `19:17:14.762`; that difference does not affect its date. No independent second/subsecond/offset agreement is claimed or required for day-based selection.

The user was directed to Internal storage → DCIM → Camera on the phone to locate the selected files; no local photo copies were created by this diagnostic. Successful prefix access and the Gallery comparison establish a candidate day-level JPEG capture-date path for these samples, not complete-original access, complete photo coverage, other-format support, or synchronization acceptance. The 2017 sample's unknown offset does not prevent using its recorded capture calendar date; no PC-timezone conversion is needed. The draft checkpoint policy still needs explicit same-day-boundary handling before filtering is implemented. iPhone validation remains deferred.

Hardware implementation: `98a77af`. Independent review later identified that separate EXIF directories could incorrectly lend an orphan offset/subsecond to another directory's date. That was corrected by preserving per-directory capture tuples, reporting ambiguity and retaining the separate raw evidence. Two regression cases failed before the correction; the final full suite passes 93 tests and the Release solution builds with 0 warnings/errors. The phone reads were not repeated after this synthetic-data correction, and the original hardware JSON predates the added `captureDirectories` field.

## Scoped DCIM/Pictures metadata traversal

After reconnect/unlock, a throwaway metadata-only harness called the existing production probe with roots limited to `Internal storage/DCIM` and `Internal storage/Pictures`. It uses only the metadata-only interface, never the optional content capability. The run returned `complete`, 3,909 still-image observations, 72.308 seconds, and zero surfaced errors. The same package-hidden-enumeration-error limitation still applies; independent counts are requested rather than inferred from this status.

| Scope | Still-image observations |
| --- | --- |
| DCIM, recursive | 3,721 |
| DCIM/Camera, within that total | 3,601 |
| Pictures, recursive | 188 |
| Both roots combined | 3,909 |

Formats: 3,898 JPG, 2 JPEG, 3 HEIC, 2 DNG, and 4 PNG. The HEIC files were found under Pictures and were outside the earlier partial inventory. `.thumbnails` remains excluded. Camera/DCIM counts agree with the earlier partial scan, providing repeatability evidence, not independent physical completeness. The main Pictures root is now covered through the API for the first time. Private evidence: `artifacts/android-s21-camera-pictures-metadata.json`; harness: `artifacts/folder-coverage/`. No new shipping code was needed.

## Separately approved complete-file access test

The user explicitly approved copying only the previously inspected `20261015_191713.jpg` to a new local artifact. A read-only MediaDevices session reported MTP: 1.00. Source metadata reported 3,002,932 bytes; the stream returned exactly that count followed by EOF, and the saved file length matched. Local MetadataExtractor inspection found a JPEG of 4032×2268 pixels with the expected DateTime/DateTimeOriginal `2026:10:15 19:17:14`, original subseconds `762`, and offset `+02:00`. No errors or warnings were reported. The command exited 0.

Transfer plus file flush/stream cleanup took 0.176 seconds. The diagnostic's pre-report-export elapsed measure was 0.861 seconds. A SHA-256 of the local saved bytes was independently recomputed with PowerShell and matched the report. No phone-side hash was available: this verifies complete access to the reported JPEG resource and local size/metadata integrity, not independent bitwise equality to the original asset or absence of driver conversion for every format.

The one local photo is under ignored `artifacts/original-access/20261015_191713.jpg`; private JSON is `artifacts/android-s21-original-access.json`. Both paths were confirmed Git-ignored. The experiment used a new `.partial` destination and finalized it only after verification; no phone files or sync state were modified. It does not authorize copying any other media. Source: ignored `artifacts/original-access-test/` (build 0 warnings/errors after correcting a filesystem type-name qualification). Other formats, independent folder counts, and physical cancellation still need evidence.

## Camera item-count reconciliation

The user reported 3,615 items in Camera, including 12 videos. A metadata-only enumeration of every direct Camera entry returned exactly 3,615 files and no directories in 2.086 seconds, with no surfaced errors:

| Extension | Files |
| --- | --- |
| JPG | 3,599 |
| DNG | 2 |
| MP4 | 14 |
| Total | 3,615 |

This explains the supported-image count of 3,601 without missing supported extensions: the directory has 14 `.mp4` entries rather than the 12 videos initially reported. Video classification here is by extension, not content inspection. The total independently matches the user's directory count; it does not certify all file types or every other photo folder. No content was opened for this reconciliation. Private evidence: `artifacts/android-s21-camera-all-entries.json`.

## Separately approved HEIC/DNG/PNG prefix experiment

The user approved one existing file per format with a 256 KiB application cap, metadata-only output, and no full-file fallback. MediaDevices 2.0.0 opened read-only media streams; each prefix was closed before MetadataExtractor 2.9.3 parsed a seekable memory buffer. Exactly 262,144 application bytes were read per object, 786,432 total. No image/thumbnail bytes were saved, no separate thumbnail API was called, and no phone files or state were changed. Driver read-ahead may exceed the cap on actual USB traffic.

| Sample | Reported size | Capture evidence from prefix | Parser outcome | Seconds |
| --- | --- | --- | --- | --- |
| Telegram HEIC | 2,778,405 | DateTime and DateTimeOriginal `2026:03:01 13:34:39`, original offset `+01:00`, subseconds `917` | Date candidates found; two Apple maker-note errors | 1.695 |
| Camera DNG | 18,883,776 | DateTime and DateTimeOriginal `2018:06:26 19:57:06`; no offset obtained | Parsed, no errors | 1.624 |
| Pictures PNG | 2,914,035 | No date obtained | Prefix parse failed on a chunk skip beyond available data | 79.620 |

The HEIC errors were `Invalid TIFF tag format code 16` for Apple maker-note tags `0x0017` and `0x0025`. ICC profile dates were also present but are not capture dates. The valid-looking EXIF day candidate is not general HEIC acceptance; maker-note-error policy and independent Gallery agreement remain unverified. The DNG has a usable-looking EXIF day candidate but still needs independent comparison. Neither candidate relies on filename or WPD modification time.

The PNG error was `Unable to skip. Requested 65536 bytes but only 43566 remained.` The generic PNG reader could not finish parsing the bounded prefix. This does not prove the complete PNG lacks capture metadata, and no full-file fallback was attempted. Product PNG capture-date support remains unresolved.

Total experiment duration: 85.188 seconds including Camera metadata enumeration and cleanup. The PNG step waited with essentially idle CPU; the user reported the phone was unavailable during that wait. It subsequently returned, both JSON reports were saved, and no USB process remains active. The measurements do not identify the exact blocking call or isolate USB throughput. Exit 0 indicates all three attempts completed without a fatal/session error, not success for every parser.

Private evidence: `artifacts/android-s21-three-format-prefix.json`; source: ignored `artifacts/format-feasibility/` (build 0 warnings/errors). This throwaway test did not authorize extending the product or further reads. The later approved implementation and validation are recorded below. iPhone remains deferred.

## Product capture-day diagnostic and offline preview validation

On 2026-10-16 the user separately approved one run on the same JPEG, HEIC and DNG, capped at 256 KiB per file in the application, saving only metadata, followed by a local preview against the saved inventory. Implementation commit `5de95ae` completed the three-file diagnostic with exit 0 in 13.052 seconds. No full-file fallback, saved media bytes, phone modifications or sync-state changes occurred. Driver read-ahead was not measured or bounded.

| Selected source | Capture day | Application bytes | Day status |
| --- | --- | --- | --- |
| DCIM/Camera/20261015_191713.jpg | 2026-10-15 | 16,468 | available |
| Pictures/Telegram/IMG_20260301_150441_005.HEIC | 2026-03-01 | 262,144 | available |
| DCIM/Camera/20180626_195706.dng | 2018-06-26 | 262,144 | available |

Total application bytes: 540,756. The schema-2 report contains canonical paths, reported sizes and persistent identifiers obtained at read time. HEIC retains the two Apple maker-note format errors as warnings for day resolution; its exact-timestamp outcome remains invalid. The independent day outcome is available under the approved policy. DNG retains an unknown offset without inventing a UTC instant. On 2026-10-16 the user confirmed both Gallery capture days match: HEIC 1 March 2026 and DNG 26 June 2018. JPEG day agreement was established previously. This validates calendar days for these samples, not exact times, offsets or every file of either format.

The offline `plan` matched all three observations to the existing 3,909-image inventory. With cutoff 2026-10-10, the JPEG is eligible (3,002,932 reported bytes), HEIC/DNG are older, and the other 3,906 rows remain unresolved. The preview correctly exits 4/incomplete because most inventory objects have no capture reports and PNG remains unsupported. This checks real saved-report interoperability and source association, not a complete-library preview or freshness/content identity. No new inventory or broader media scan was performed.

Private evidence: `artifacts/android-s21-day-validation.json`, `artifacts/android-s21-day-validation-preview.json` and its console text. No USB process remains active. Sample day comparison is complete. Remaining gates include other-folder coverage and unresolved-format policy; iPhone remains deferred.

## Separately approved limited Camera live preview

On 2026-10-16 the user authorized the proposed Camera-folder preview with at most three content-open attempts, at most 262,144 application bytes per attempted photo, reports only, and no full-file fallback or phone/state changes. The first attempt found no selected device and exited 3: its manifest records zero attempts and zero application bytes. The user then confirmed the S21 was connected/unlocked; the retry used a new output directory, preserving the first attempt's reports.

Implementation commit `182108f` resolved only `\Internal storage\DCIM\Camera` and completed its metadata inventory through the API: 3,601 images in 40.659 seconds with zero surfaced errors. This matches the previous supported-image count and earlier total-file reconciliation; it does not prove the package exposed every native enumeration error or certify other folders.

Exactly three supported objects were attempted in ordinal canonical-path order:

| Camera file | Reported bytes | Capture day | Application bytes read |
| --- | --- | --- | --- |
| 20171027_130322.jpg | 678,759 | 2017-10-27 | 474 |
| 20171029_074659.jpg | 4,167,145 | 2017-10-29 | 27,721 |
| 20171029_083849.jpg | 815,934 | 2017-10-29 | 474 |

All three day outcomes were available, with no per-file errors. Total application reads were 28,669 bytes, below the approved 786,432-byte maximum; actual USB traffic/read-ahead was not measured. No photograph bytes were saved. The first sample's day was previously compared with Gallery; the other two observations are not new independent Gallery confirmations, and filename agreement is not used as evidence.

At cutoff 2026-10-10, all three inspected objects are older and zero are eligible. The remaining 3,598 inventory rows are unresolved because they were not read. The manifest records limitReached=true, three attempts, no run errors and incomplete status; native exit 4 is expected. Zero selected does not mean the Camera folder contains no newer photos. No checkpoint, destination or phone file was changed.

Reopening the generated inventory/capture files with the compiled offline `plan` command also returned native exit 4 and produced identical per-file rows, processed count and unresolved count. No further USB calls were needed. Private evidence: `artifacts/android-s21-camera-live-preview-connected/` and console text; offline recheck `artifacts/android-s21-camera-live-preview-offline-recheck.json`. The initial device-not-found manifest remains in `artifacts/android-s21-camera-live-preview/`. All are Git-ignored. No test process remains running. This validates the scoped live path and its limit on this run; broader capture scanning, other-folder comparisons and unresolved-format policy remain separate work. iPhone is deferred.
