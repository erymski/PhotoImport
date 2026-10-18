# iPhone 17e / iOS 26.6 — USB assessment

Assessment started 2026-10-18 at the user's request. Model and OS are user-reported; the backend identifies the device only as Apple iPhone. **Four selected complete-resource reads succeeded in both Automatic and Keep Originals. Device identifiers survived one reconnect, but all reported file persistent IDs changed. Production iPhone synchronization and original-asset identity are not yet validated.**

## Successful accessible session

After the user requested another attempt, the third fresh-process probe completed in **313.103 seconds (5 m 13 s)** with **5,217 still-image resources** and no surfaced errors: 2,510 JPG, 2,058 HEIC, 649 PNG. Every image had a known reported size. Authored capture dates were missing on all 5,217 (100%); separate PTP capture dates remain unavailable through the product adapter. The previously empty storage is historical evidence, not the current state. The precise unlock/trust action that restored access was not observed by the application.

The user reports **5,240 Library items** and transfer mode **Automatic**. A separate metadata-only resource census completed in 3.843 seconds, exposing 6,393 files in 96 directories beneath the device root:

| Extension | Files |
| --- | ---: |
| JPG | 2,510 |
| HEIC | 2,058 |
| PNG | 649 |
| MOV | 572 |
| MP4 | 60 |
| AAE | 540 |
| GIF / WEBP / DAT | 1 / 2 / 1 |

The product probe excludes videos, GIF and WEBP. Resource count is not asset count: 487 media resources use `IMG_E` names, and 31 same-folder normalized name groups contain both image and video resources. This is naming evidence only, not verified edit/Live Photo identity. Grouping by folder plus basename with `IMG_E` normalized to `IMG_` yields 5,334 groups, **not** the user's 5,240 Library items. Coverage therefore remains unreconciled; no files were declared missing or duplicates merely from these counts. The census and probe agree on the 5,217 supported still-image resources. Neither API traversal proves physical completeness.

## Selected complete-resource reads (Automatic)

The experiment rechecked phone serial and each selected path, size and persistent ID before opening its content. It used new ignored outputs, read to EOF, required reported/received/saved length agreement, flushed local files and recorded local SHA-256. No retries or broad content scan were performed. Four resources total **6,395,268 bytes** were copied:

| Sample | Bytes | Transfer seconds | Embedded capture candidate | Observations |
| --- | ---: | ---: | --- | --- |
| `IMG_8342.JPG` | 2,311,837 | 0.180 | EXIF `2026:10:11 15:53:17`, offset `+02:00` | JPEG 3088×2316; EXIF dimensions agree |
| `IMG_2510.HEIC` | 109,203 | 0.118 | EXIF `2026:05:11 23:48:19`, no original offset | HEIF primary image 1280×960, confirmed against Photos; EXIF 3264×2448 does not describe actual image dimensions |
| `IMG_8350.PNG` | 1,860,025 | 0.188 | EXIF `2026:10:14 08:42:30`, no original offset | PNG 1170×2532; screenshot classification needs user confirmation |
| `IMG_8344.MOV` | 2,114,203 | 0.219 | QuickTime metadata creation `2026-10-11 15:53:33 +02:00` | Video track 1920×1080; capture day confirmed against Photos, origin classification not independently established |

These are separate measured transfers, not one continuous bulk benchmark. All four streams reached EOF and matched lengths; container parsers recognized each corresponding format. The JPEG and HEIC retain Apple maker-note tag-format warnings. MOV metadata retains an unsupported location-accuracy-key warning. QuickTime movie-header Created is `2026-10-11 13:53:48` without a parsed offset, differing from the offset-bearing metadata date by more than just the two-hour offset (15 seconds); both yield the same unshifted calendar day in this sample. No general video time policy was inferred.

The user subsequently confirmed that the Photos days match for JPEG `IMG_8342` (2026-10-11), PNG `IMG_8350` (2026-10-14) and MOV `IMG_8344` (2026-10-11), and that Photos reports HEIC `IMG_2510` as **1280×960**. This independently corroborates those three calendar days and the HEIC primary-image dimensions. It does not establish every format, camera-only origin or the cause/history of the HEIC's inconsistent EXIF dimension fields. Screenshot classification and a separate explicit HEIC capture-day comparison are not inferred from these replies. [Apple documents possible HEIF/HEVC conversion during USB import](https://support.apple.com/en-gb/116944); the narrow comparison of both transfer modes is recorded below.

## Raw WPD metadata check

The existing throwaway pinned-package reflection helper inspected all available raw WPD properties on the three selected image objects without opening their content. Each returned created/modified properties (IDs 18/19 under `ef6b490d-5cd8-437a-affc-da8b60ee4a3c`) as raw `VT_DATE`/OLE date values. Authored-date property 20 attributes returned `0x80070032` on all three. Raw values and keys are preserved privately. Created/modified wall-clock values agree with the three embedded EXIF date/time strings, but this sample agreement does **not** authorize treating creation/modification dates as capture dates. Raw OLE date availability does not establish source timezone semantics.

The serial, device ID and PnP ID agree with the earlier processes. The physical reconnect comparison is recorded below. Persistent IDs are association observations, not original-content hashes.

## Keep Originals and reconnect comparison

The user replied ready after instructions to select Keep Originals and physically reconnect while unlocked. Fresh discovery retained the same serial, device ID, PnP ID and protocol as the successful Automatic-mode probe. This is one reconnect on the same Windows setup, not a guarantee across ports, drivers or OS changes.

The new metadata-only census took **306.677 seconds (5 m 07 s)** and again exposed 6,393 files / 6,490 entries including directories and the device root, with no surfaced errors. Both saved censuses have identical path membership and reported file sizes: zero added/removed paths, zero changed sizes. However, **all 6,393 file `PersistentUniqueId` values changed**. The mode switch and reconnect occurred together; their effects were not isolated. These IDs cannot serve as stable cross-connection source identity in this observed workflow. Matching paths/sizes do not prove unchanged content for the entire census.

The same four explicitly selected paths were rebound to their freshly observed IDs after verifying unchanged paths/sizes. A new manifest preserves that association; earlier source checks were not weakened. In a fresh process, the selected-read harness rechecked serial/path/size/current ID and copied only those samples to a new ignored directory:

| Sample | Bytes | Keep Originals transfer seconds | SHA-256 versus Automatic |
| --- | ---: | ---: | --- |
| `IMG_8342.JPG` | 2,311,837 | 0.227 | Identical |
| `IMG_2510.HEIC` | 109,203 | 0.097 | Identical |
| `IMG_8350.PNG` | 1,860,025 | 0.164 | Identical |
| `IMG_8344.MOV` | 2,114,203 | 0.193 | Identical |

All four reached EOF and matched reported/received/saved lengths. Fresh offline verification recomputed hashes of **both** copy sets and checked them against their transfer reports. All four before/after byte comparisons agree despite changed persistent IDs. This establishes equality of these exposed resources, not a phone-side hash comparison or format-wide proof that Automatic never converts media.

The HEIC primary-image/EXIF dimension discrepancy and embedded capture candidates are unchanged because the bytes are identical. The user's Photos check confirms that **1280×960 is also the displayed phone image size**. Thus this sample's transferred primary dimensions agree with Photos; EXIF's3264×2448 is not a reliable description of the actual image size. No evidence ties the discrepancy to USB transfer or Automatic mode. The cause of the inconsistent EXIF dimensions is unknown. Three other sample days are independently confirmed as recorded above; general format behavior and camera/screenshot origin remain outside that confirmation.

Product implication from code inspection: the current Android filename-day policy cannot extract a day from ordinary `IMG_####` names, and `ImportRunner.SameSource` rejects changed nonempty persistent IDs in saved source associations. Production iPhone importing/recovery was **not** run. An iPhone integration needs a verified date policy and handling of these volatile IDs; this experiment does not justify removing source-validation checks or registering the phone with the existing Android workflow.

## Initial metadata experiments

Backend: MediaDevices 2.0.0, application checkout `f835a52`, Windows USB. Discovery succeeded with manufacturer Apple Inc., generic Apple iPhone model, USB transport, and reported protocol `MTP: 1.00`. Serial, device ID and PnP ID were available. Private values are retained only in ignored artifacts.

Two separate application processes connected successfully but returned zero images, zero capture-date observations and no surfaced errors. Enumeration took 0.060 and 0.012 seconds. These are not complete physical-library assessments: MediaDevices can suppress native enumeration errors, and zero rows cannot establish date support or missing-date rates.

A separate shallow metadata-only harness exposed the device root and `Internal Storage`, with no children beneath storage. It used only read-access connections and directory metadata enumeration, with no media-content APIs. It reported no surfaced errors. This narrows the failure to exposed photo access before capture parsing or copying; it does not identify the cause. No photo/video reads, phone writes, importer registrations, checkpoint changes or production destination writes were performed.

In these initial empty-storage runs, identifiers agreed between fresh application processes in the same physical connection. At that stage, reconnect stability was unverified. The user was asked to reconnect while unlocked and accept Trust/Allow, following [Apple's transfer procedure](https://support.apple.com/en-gb/120267). The subsequent accessible-session and reconnect results supersede that initial limitation; independent Library coverage is still unreconciled.

## Evidence and acceptance limits

Private, Git-ignored evidence:

- `artifacts/iphone-first-2026-10-18.json`
- `artifacts/iphone-second-2026-10-18.json`
- `artifacts/iphone-shallow-first-2026-10-18.json`
- `artifacts/iphone-access-check/`: throwaway shallow metadata harness.
- `artifacts/iphone-third-2026-10-18.json`: successful product still-image probe.
- `artifacts/iphone-all-resource-metadata-2026-10-18.json` and `.log`: metadata-only census including video/sidecar resources.
- `artifacts/iphone-selected-samples-2026-10-18.json` and `artifacts/iphone-selected-copy-2026-10-18/`: explicit image manifest, copies and length/hash/metadata report.
- `artifacts/iphone-selected-video-2026-10-18.json`, `artifacts/iphone-selected-video-copy-2026-10-18/`, `artifacts/iphone-selected-video-dates-2026-10-18.json`: explicit video selection, complete copy and local metadata.
- `artifacts/iphone-raw-three-properties-2026-10-18.json`: raw image-object properties.
- `artifacts/iphone-automatic-local-verification-2026-10-18.json`: fresh offline verification of all four saved lengths/hashes and their recorded EOF observations.
- `artifacts/iphone-keep-originals-resource-metadata-2026-10-18.json` / `.log` and `artifacts/iphone-keep-originals-metadata-comparison-2026-10-18.json`: post-reconnect census and path/size/persistent-ID comparison.
- `artifacts/iphone-keep-originals-selected-samples-2026-10-18.json` and `artifacts/iphone-keep-originals-selected-copy-2026-10-18/`: fresh explicit manifest and four post-reconnect copies/reports.
- `artifacts/iphone-transfer-mode-comparison-2026-10-18.json`: fresh before/after local hashes, EOF/length verification and changed sample IDs.
- `artifacts/iphone-keep-originals-discovery-2026-10-18.txt` and `artifacts/iphone-reconnect-identity-comparison-2026-10-18.json`: fresh-process device identifier comparison.
- `artifacts/iphone-user-confirmation-2026-10-18.json`: user-confirmed Photos dimensions and three sample calendar days, separately attributed from application observations.
- `artifacts/iphone-selected-access/`, `artifacts/iphone-raw-properties/`: throwaway diagnostic sources/builds; product code unchanged.

Three selected calendar days and the HEIC primary dimensions are independently corroborated against Photos. General capture-day accuracy, physical Library coverage, camera/screenshot classification and original-asset identity remain unverified. One reconnect retained device identifiers while changing all reported file persistent IDs. Full access to the four resources and before/after equality are established separately from original identity. No production iPhone support/version claim is justified yet. Diagnostics performed no phone writes, importer registrations/checkpoint changes or production destination writes; the user changed the phone transfer setting as instructed. Android production state remains outside this assessment. No experiment remains running. No commit or push was requested.
