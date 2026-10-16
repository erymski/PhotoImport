# Samsung Galaxy S21 (SM-G991B)

Assessment date: 2026-10-16, Europe/Madrid. Backend: MediaDevices NuGet 2.0.0 / Windows WPD. Status: **no-go for synchronization through the tested MTP and PTP metadata paths**. Capture dates were unavailable for every image in the inventory sample, including 3,601 camera-folder images; a raw-property follow-up confirmed unsupported capture/creation properties for three camera objects in both USB modes. Complete inventory coverage remains unverified.

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
| Complete folder coverage / expected photo count | Not verified |
| Actual authored-date availability / missing-date rate | 0 available / 13,156 observed; 100% missing, including 3,601 camera-folder images |
| Capture-date accuracy against EXIF/Picasa | Not verified |
| Timezone/local/UTC interpretation | Not verified; public API dates are projections |
| Enumeration duration and performance | 125.618 seconds for an incomplete run with 13,156 photo observations |
| No-content transfer | Application calls no media/thumbnail APIs; independent driver/USB observation pending |
| Original/full-resolution access | Not tested; separate selected-file transfer validation required |

The public package API does not expose raw timestamp variants, a separate PTP date, or reported photo format/content type, and may hide some native enumeration failures. A clean probe alone cannot establish inventory completeness. See [backend notes](backend-notes.md) and the [acceptance procedure](README.md).

## Decision

Do not implement or enable date-based synchronization for this phone using this backend yet. The raw WPD/PTP follow-up below did not uncover a capture-date property, so adding a raw-property reader alone is not a demonstrated solution. A different permitted metadata path or an explicitly revised requirement would need validation against EXIF/Picasa and complete folder coverage before accepting a capture-date policy. Do not use modification timestamps as an implicit fallback.

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

This does not prove that every possible Samsung vendor command lacks capture information. It establishes that broadening ordinary WPD property reads does not supply a usable capture timestamp for the tested photos. A raw WPD reader alone is consequently not a demonstrated solution. Synchronization remains blocked under the existing capture-date and metadata-only rules; the specification has not been relaxed.

Reference-source context, not a claim about Samsung's exact firmware: Android's reference [MTP property handler](https://raw.githubusercontent.com/aosp-mirror/platform_frameworks_base/master/media/java/android/mtp/MtpPropertyGroup.java) supplies modification time for both DateModified and DateAdded, and its [object-info implementation](https://raw.githubusercontent.com/aosp-mirror/platform_frameworks_base/master/media/java/android/mtp/MtpDatabase.java) fills both object-info time slots from modification time. These behaviors are consistent with the observations. Microsoft documents [reading all WPD properties](https://learn.microsoft.com/en-us/windows/win32/wpd_sdk/retrieving-properties-for-a-single-object) separately from media-content access.
