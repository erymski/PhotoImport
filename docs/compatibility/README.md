# Physical phone metadata verification

## Current evidence

On 2026-10-15, the Release CLI's `list-devices` command completed with exit 0 and reported no portable devices in the Windows user session. No Android or iPhone has been tested. Neither platform is accepted for synchronization. Selected-file and original/full-resolution transfer access are untested.

Automated tests cover application behavior using metadata-only fake devices. They cannot establish USB driver behavior, capture timestamp correctness, stable physical identity, or actual enumeration coverage. See [backend inspection](backend-notes.md) for public API limitations.

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

A separate, explicitly invoked transfer validation must establish that a selected original can be read. This CLI intentionally has no download command, and reported filename/size does not prove original access.
