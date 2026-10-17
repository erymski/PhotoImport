# Samsung Galaxy S21 (SM-G991B)

**2026-10-17 full-copy update:** all 3,601 Camera images (11.59 GiB) copied successfully in one connection in 1h 3m 34s; all 3,910 DCIM/Pictures inventory entries completed in 1h 4m 15s. Total retained across sessions: 4,810 files, 12.03 GiB, with completed-file local lengths verified. Remaining storage is unfinished; the phone was absent at the last discovery. This establishes bulk full-file access, while bounded-preview stability remains unresolved. See [experiment results and resume instructions](2026-10-17-full-copy-experiment.md).

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

## Complete Camera preview attempt and terminal-session handling

The user subsequently authorized Camera-only inspection capped at 3,601 attempts and provided the actual completed-day cutoff **2026-09-03**. Earlier 2026-10-10 cutoffs were diagnostic examples. This approval covers bounded metadata reporting, not photo downloads or import-state changes. The maximum application budget was 943,980,544 bytes (900.25 MiB); driver read-ahead remained unmeasured.

The run on `182108f` enumerated 3,601 images with no surfaced metadata errors in 2.041 seconds. Capture collection took 204.867 seconds. Six JPEG prefixes returned available capture days, reading 107,982 application bytes total. Their days were 2017-10-27, twice 2017-10-29 and three times 2017-10-30. The seventh content-open attempt timed out; later requests reported device failure, unreachable device and a shutdown session. The process finished with native exit 4 and 3,601 attempted paths: six older rows and 3,595 unresolved rows. No new-day absence can be inferred from zero eligible rows. No image bytes were saved or phone/checkpoint/destination files changed.

Observed error sequence: `0x80070079` semaphore timeout on GetStream, `0x8007001F` device failure, `0x80070141` unreachable device, then `0x802A0002` shutdown during EnumObjects (3,478 occurrences). These were translated to ordinary IOException by the old adapter, so collection continued through its attempt budget instead of recognizing a terminal session. The reports correctly preserved earlier observations and incomplete selection, but the manifest's empty session-error list and exit 4 do not diagnose the underlying loss of a usable session. No physical reason for the interruption was established; do not attribute it to the lock screen, cable or a corrupt file without evidence. The progress text was buffered in 4 KiB chunks, so initially empty console output did not identify which call was waiting.

An offline regression reproduced the classification defect. The adapter now treats those four observed codes as terminal WPD session failures for native COM HRESULTs or the package's exact diagnostic prefix, alongside its existing removal codes. The next file is not attempted; prior observations survive and live preview returns device exit 3. Access-denied errors remain recoverable, and code-like filename text is not a classifier. This conservative rule may end a run on a transient transport timeout; it does not reconnect, retry or claim to repair USB stability. Code meanings were checked against Microsoft's [Win32 errors](https://learn.microsoft.com/en-us/windows/win32/debug/system-error-codes--0-499-) and [COM errors](https://learn.microsoft.com/en-us/windows/win32/com/com-error-codes-10).

Verification after correction: 260 Release tests passed; build 0 warnings/errors. Eight new terminal-classification/integration cases failed before the change; three negative-classification controls already passed. Physical verification of the correction remains pending. No additional phone reads were performed after the failed run. Private evidence: `artifacts/android-s21-camera-complete-preview/` and console text, plus `artifacts/android-s21-camera-complete-preview-offline-recheck.json`. Offline replay returned exit 4 and identical per-file decisions. No preview process remains running. The full Camera capture-day check remains incomplete, and the backend is not accepted for synchronization.

## Retry after unlock: terminal handling verified, USB stability still open

After the user confirmed the S21 was unlocked with File transfer, the authorized Camera preview was retried on `369e185`, using a new output directory, the same 3,601-attempt maximum and actual cutoff 2026-09-03. No scope expansion, automatic reconnect or per-file retry was added.

Inventory completed through the API with 3,601 images in 60.352 seconds. Collection retained 25 available JPEG capture days and read 430,731 application bytes. The 26th attempt, `20171224_114434.jpg`, returned GetStream semaphore timeout `0x80070079` after 63.877 seconds. Collection duration was 116.242 seconds, including that wait. The corrected adapter stopped immediately after this failure: manifest attemptedCount=26, limitReached=false, incomplete status and an explicit terminal-session error; native live exit 3. No later photos were opened. All 25 obtained days precede the cutoff: 25 older, zero eligible and 3,576 unresolved rows. No photo bytes were saved or phone/import state changed.

The earlier failure at `20171030_192743.jpg` succeeded in this retry. This does not establish the physical cause of the interruptions, but it does not support attributing both runs to one consistently unreadable file. The user confirmed that the screen turned off or locked during the latest run. Lock-related interruption is therefore a plausible hypothesis, not an established cause. A shorter Camera check capped at 100 attempts with the screen deliberately kept awake is proposed within the existing Camera authorization, after a physical reconnect and unlock. It keeps the same per-photo cap and cutoff; no device command has been started for it. A complete usable Camera preview remains blocked on USB/session reliability.

Offline replay returned exit 4, as expected for saved incomplete evidence, and reproduced identical per-file rows. This differs from the live exit 3 because offline planning has no active device session. Private evidence: `artifacts/android-s21-camera-complete-preview-retry/`, its console text and `artifacts/android-s21-camera-complete-preview-retry-offline.json`. Earlier reports were preserved. All are Git-ignored, and the preview process has ended. This physically verifies stop-on-timeout and partial-evidence preservation; it does not verify full-folder capture-date coverage or sustained USB reliability.

## Screen-awake check and driver investigation

The user confirmed readiness after the requested reconnect/unlock and agreed to keep the screen awake for a Camera check capped at 100 attempts. The run began on 2026-10-16 at 17:34:32 +02:00 using the unchanged cutoff 2026-09-03. Metadata traversal completed with 3,601 images, zero surfaced errors, in 2.235 seconds. The first Camera photo, `20171027_130322.jpg`, failed GetStream with `0x80070141` (device unreachable) after 0.370 seconds. Native exit 3; one attempt, zero application content bytes, limit not reached, and an explicit terminal-session error. The screen-awake intervention did not restore content access in this session; it does not establish that screen locking never contributes to other failures. Evidence: ignored `artifacts/android-s21-camera-awake-100/` and console text. No media/state changes or automatic retry occurred.

A subsequent read-only Windows PnP query reported the S21 WPD device present with Status OK. This describes device recognition, not successful resource-stream access. The existing MTP operational log was enabled; DriverFrameworks operational logging was disabled and was not changed. In the four-minute window beginning with the earlier retry at 17:21:32 +02:00, the MTP log contains 25 event-1004 cancellations for operation `0x1009`, then one event-1008 communication/status failure for the same operation. The cancellations align with the 25 retained prefix observations. Microsoft's [command reference](https://learn.microsoft.com/en-us/windows-hardware/drivers/image/ptp-required-commands) identifies `0x1009` as GetObject.

Inference: ending a prefix read before the whole resource finishes may be relevant to repeated GetObject cancellation/session behavior. Cancellations may also be expected when deliberately closing a bounded read; this correlation does not establish a driver, phone or library defect. The upstream [2.0.0 StreamWrapper source](https://github.com/Bassman2/MediaDevices/blob/2.0.0/Src/MediaDevicesShare/Internal/StreamWrapper.cs) releases its COM stream on disposal, and our handle/wrapper calls disposal. Its [OpenRead source](https://github.com/Bassman2/MediaDevices/blob/2.0.0/Src/MediaDevicesShare/Internal/Item.cs) requests the default resource stream. No missing application disposal or proven source-level fix was identified, and no library or product changes were made from this hypothesis.

On 2026-10-17 a control using the existing `exif` command selected only that previously tested JPEG, without Camera inventory traversal. It found no connected selected phone and exited 3 with zero attempts/content bytes. Its new report is ignored `artifacts/android-s21-single-camera-control-2026-10-17.json`. The user has been asked to connect/unlock for the same bounded one-photo control; no further content access has started. This control is intended to distinguish standalone stream access from the inventory-plus-capture sequence, not to retry a full folder or permit full-file reads. Actual sustained-read root cause and complete Camera preview remain unresolved.
