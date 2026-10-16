# Physical phone metadata verification

## Current evidence

Initial discovery reported no phone. On 2026-10-16, a Samsung Galaxy S21 (SM-G991B) was assessed over USB/MTP: a partial run recorded 13,156 images in 125.618 seconds, including 3,601 actual camera-folder images, with no authored capture dates exposed. Its device/serial identifier candidates stayed unchanged in one unplug/replug and fresh-process check. See the [Android compatibility report](android-samsung-sm-g991b.md). The current backend is not accepted for synchronization. iPhone remains untested; selected-file/original access and complete inventory coverage remain unverified.

Subsequent evidence: three bounded JPEG EXIF reads matched user-confirmed Gallery capture days. A scoped metadata-only run completed DCIM/Pictures with 3,909 images (3,721 DCIM, 188 Pictures) and no surfaced errors; independent folder counts are pending. A separately approved one-JPEG complete-resource read received the reported 3,002,932 bytes, with local EXIF/dimension and hash checks. Original-asset identity and other-format capture-date behavior remain unverified. The local JPEG stays under ignored artifacts; this was not a product download command.

Automated tests cover metadata-only probing and bounded JPEG EXIF reading using synthetic data and fake USB boundaries. They cannot establish USB driver behavior, capture timestamp correctness, stable physical identity, or actual enumeration coverage. See [backend inspection](backend-notes.md) for public API limitations. iPhone testing is explicitly deferred by the user; continue Android work independently.

## Optional bounded EXIF assessment

The separately invoked `exif` command reads media-object content under an explicit diagnostic exception; it does not change `probe`. The [day-preview design](../superpowers/specs/2026-10-16-android-day-preview-design.md) extends the original JPEG diagnostic to HEIC/HEIF/DNG. Use one to three explicit absolute supported device paths and a new JSON output path:

```powershell
dotnet bin\Release\net10.0-windows\phone-photo-sync.dll exif --device "My Phone" --photo "\Internal storage\DCIM\Camera\example.jpg" --output artifacts\selected-exif.json
```

Only a prefix is read (at most 262,144 bytes per selected file at the application boundary). JPEG stops after the first EXIF APP1 segment; HEIC/DNG parsing sees only a bounded memory prefix. The driver can read ahead; USB bytes were not measured. MetadataExtractor performs format navigation and EXIF decoding. No image bytes are saved, no full-file fallback is used, and no phone files or sync state are changed. Later metadata is outside the observation; conflicting capture days remain ambiguous. Unsupported/missing/invalid/truncated input is explicit in per-photo outcomes.

Compare raw `DateTime` (the field used by `photo-sort`) with `DateTimeOriginal`, then independently compare capture calendar days against Gallery/Picasa details. Exact hours/seconds/offset agreement is not required by the user's day-level policy. An unknown offset stays unknown and invalid exact-time fields do not discard a valid day. Unrelated Apple maker-note errors can be warnings for day resolution; other parser errors remain unresolved. Exit 0 requires valid capture days for all selected photos but does not certify independent correctness or an exact instant. Exit 4 covers unresolved dates and incomplete reads; other exit codes match the table below. Earlier observations survive failure/cancellation where report export succeeds.

For local `plan` usage, see the [README](../../README.md). It performs no USB calls. Existing unversioned hardware exports lack schema-2 source provenance and therefore cannot silently produce a complete preview. An offline compatibility run against the 3,909-image Samsung inventory and the three old JPEG observations classified every row as unresolved: 3 legacy observations, 3,902 without capture reports, and 4 unsupported PNGs. This is expected evidence handling, not loss of the earlier day-level hardware validation. New capture reports still need an agreed hardware scope.

The approved hardware run used exactly three existing camera JPEGs spanning 2017, 2023, and 2026. No broader content scan is authorized by that run. Full original-file access, folder coverage, missing-date policy, independent accuracy, and deferred iPhone support remain separate acceptance checks.

## Run a metadata-only assessment

1. Connect one phone using a data-capable USB cable and unlock it. On Android select USB file transfer/MTP. On iPhone install the required Apple device drivers and accept the phone's trust prompt. Ensure the representative originals are locally available rather than iCloud-only.
2. Build the CLI, then list devices:

   ```powershell
   dotnet build PhotoImport.slnx --configuration Release
   dotnet bin\Release\net10.0-windows\phone-photo-sync.dll list-devices
   ```

3. Use an exact selection ID or unique friendly name. A persistent registered alias is not available in this milestone.

   ```powershell
   dotnet bin\Release\net10.0-windows\phone-photo-sync.dll probe --device "My Phone"
   New-Item -ItemType Directory -Force artifacts | Out-Null
   dotnet bin\Release\net10.0-windows\phone-photo-sync.dll probe --device "My Phone" --output artifacts\phone-first.json
   ```

   Choose a new filename on each run; the CLI never overwrites existing reports. Full exports contain personal filenames and device identifiers. `artifacts/` is ignored by Git; keep private probe exports there and include only deliberate, sanitized evidence in written reports.

4. Compare actual roots and photo counts against the phone/Explorer. The probe starts at the device root and recursively examines exposed storage, including DCIM, Pictures, and other vendor photo folders. `.thumbnails` directories and non-still-image extensions are excluded. A complete traversal through the package API is not proof that the driver exposes every photo.
5. Compare authored/capture candidates with trusted EXIF or Picasa dates from existing PC copies. Cover different days, photos close together, screenshots, shared/edited images, and HEIC/JPEG where available. Do not download images through the probe to perform this comparison. Record both correct and missing/unexpected results.
6. Unplug/replug, unlock/trust again if needed, and rerun list-devices and probe in a fresh application process. Compare every candidate device identifier, source path, filename, size, and timestamp. Object IDs are session handles; changing IDs alone is not evidence that photo content changed.
7. Record whether timezone information is actually available. `projected` dates with `offsetPresence: unknown` preserve API wall-clock fields and cannot establish original local/UTC semantics. `rawValue: null` means the raw value is inaccessible through this adapter. File creation/modification dates never count as capture candidates.
8. Record elapsed enumeration time, candidate coverage/missing-date rate, access errors, and evidence that operations were metadata-only. Code inspection establishes that this application calls no content API; independently observing driver/USB behavior provides stronger evidence. Do not infer no-content transfer from speed alone.
9. Test the other phone independently. Android success does not establish iPhone support. If reliable timestamps or identity are unavailable, mark that phone blocked and investigate a narrow WPD property reader or alternative backend before synchronization work.

Ctrl+C stops between synchronous metadata calls, keeps observations already returned to the probe, and exports a partial report when possible. A blocked driver call may delay cancellation. A failed JSON write can leave an incomplete newly created report; preserve it for diagnosis or choose a fresh path on retry. No photo or sync-state recovery is needed because the probe transfers no media and changes no sync state.

## Exit codes

| Code | Meaning |
| --- | --- |
| 0 | Command completed through the API; missing dates can still be present |
| 2 | Invalid arguments or ambiguous device selection |
| 3 | Device absent, discovery failed, or connection failed |
| 4 | Enumeration incomplete; inspect errors and available partial observations |
| 5 | JSON output failed, including an occupied filename |
| 130 | Cancelled; remains 130 if exporting the cancelled report also fails |

## Written report format

Create one sanitized Markdown report per actually tested phone. Record:

| Evidence | Required observation |
| --- | --- |
| Assessment | Date; untested, blocked, or metadata verified with transfer validation pending |
| Phone | Model, OS version, Android/iPhone, USB/trust/driver setup |
| Backend | MediaDevices 2.0.0 and application commit |
| Identity | Identifier properties observed and stability across reconnect/process restart; redact personal values as needed |
| Coverage | Exposed roots, expected/observed still-image counts, excluded folders, and errors |
| Capture dates | Authored and PTP availability separately; representative EXIF/Picasa comparisons |
| Other dates | Created/modified differences; shared, edited, screenshot, JPEG/HEIC behavior |
| Time semantics | Raw/projected evidence; offset presence; verified local/UTC interpretation or unresolved ambiguity |
| Missing dates | Count, total, missing rate, and which samples are affected |
| Performance | Enumeration seconds, photo count, and metadata-only evidence |
| Original access | Unverified until a separate selected-file transfer test; note any driver format-conversion setting |
| Decision | Accepted evidence and remaining reasons this backend cannot yet be accepted for synchronization |

A separate, explicitly invoked transfer validation must establish that a selected original can be read completely. This CLI has no full-file download command; neither reported filename/size nor a successfully read EXIF prefix proves complete original access.
