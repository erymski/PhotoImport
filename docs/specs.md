# PhonePhotoSync — Functional and Technical Specification

**Status:** Draft 1.0  
**Platform:** Windows 10/11, C# / .NET 10 console application  
**Transport:** USB  
**Primary integration:** Windows Portable Devices (WPD), initially via MediaDevices 2.x

## 1. Purpose

Build a small, reliable Windows command-line tool that imports only new photographs from a connected Android phone or iPhone. The tool replaces the first three steps of the existing workflow: downloading all phone photos, checking the previous import cutoff, and deleting older downloads. Organizing the imported photos in Picasa remains a manual step.

A photo is considered *old* when its capture timestamp is at or before the device's saved import checkpoint, even if its previous PC copy has since been deleted. **The local filesystem and the Picasa database are not used to determine whether a photo is old.**

**Precision clarification from the user (2026-10-16):** downloading decisions need only the photo's capture calendar day; exact hours, seconds, subseconds, and UTC instants are not required. Use the date recorded in capture metadata without shifting it into the PC's timezone. Preserve finer fields for diagnostics, but their independent verification is not an acceptance gate for day-based selection. The timestamp-based state/filter examples below predate this clarification and must be adapted before synchronization is implemented. In particular, the treatment of newly encountered photos on an already imported day needs an explicit boundary policy; do not silently skip them merely by truncating the existing timestamp comparison.

## 2. Goals and non-goals

### Goals
- Support multiple individually registered phones, with an independent checkpoint for each.
- List photo metadata **before** downloading photo contents.
- Transfer only eligible new photos to a configurable Windows directory.
- Keep state in human-readable JSON under `%LOCALAPPDATA%\PhonePhotoSync\`.
- Allow preview (`--dry-run`), diagnostic inspection, and safe retries.
- Never delete or modify media on the phone.
- Preserve imported files without overwriting existing files.

### Not in version 1
- Automatically importing into or modifying the Picasa library.
- Reading or repairing Picasa's own database.
- Full duplicate detection by content hash or photo similarity.
- Wi-Fi sync or downloading iCloud-only assets.
- Video and Live Photo motion-component import (may be added later).
- Background service, GUI, or mobile companion app.

## 3. Development phase 1 — mandatory metadata feasibility test

**This phase must be completed before implementing the synchronization engine.** Its purpose is to establish which data is actually available from each phone without downloading the media object.

### 3.1 Diagnostic CLI

Implement the following commands first:

```text
phone-photo-sync list-devices
phone-photo-sync probe --device <device-id-or-alias>
phone-photo-sync probe --device <device-id-or-alias> --output metadata.json
```

The probe must:
1. Enumerate connected portable devices and display friendly name, manufacturer, model, transport, and all available device identifiers.
2. Open the selected device in read-only mode.
3. Enumerate photographs recursively in accessible camera/photo folders, including Android `DCIM` and `Pictures` where present.
4. For each object, inspect **metadata only**: source path, filename, byte size, media type/format, WPD object ID, date authored, date created, date modified, and any available PTP capture date.
5. Preserve raw timestamp values and report whether timezone/offset information is present.
6. Display a concise table and, optionally, export JSON. Clearly mark missing fields.
7. Record enumeration time, total photo count, counts with/without capture dates, and any access errors.
8. Perform no media-content downloads, writes to the phone, or changes to sync state.

**Approved Android diagnostic exception (2026-10-16):** the separately invoked JPEG EXIF diagnostic described in the [approved addendum](superpowers/specs/2026-10-16-android-jpeg-exif-addendum.md) may read a prefix of explicitly selected JPEG objects into memory, capped at 256 KiB per object at the application boundary. Driver read-ahead may exceed this limit on USB traffic. Save parsed metadata and diagnostics only, with no full-file fallback, phone mutations, or sync-state changes. The ordinary `probe` remains metadata-only. Hardware validation of this extension is limited to at most three selected camera JPEGs. iPhone testing is deferred; Android progress does not depend on it.

The user subsequently authorized the separate full-resource access test for only camera JPEG `20261015_191713.jpg` (3,002,932 bytes), saved under ignored local artifacts and checked for byte count, local SHA-256, EXIF and dimensions. That test succeeded. It is a one-file validation exception, not a general download or synchronization command, and does not broaden the ordinary probe or bounded-reader scope.

A later separately approved feasibility experiment read at most 256 KiB each from one HEIC, one DNG, and one PNG, using in-memory MetadataExtractor parsing and saving only parsed evidence. HEIC/DNG capture-day candidates were found (HEIC with maker-note errors); PNG parsing could not complete within that prefix. This one-per-format exception does not authorize full-file fallbacks, further content scans, or extending the JPEG-only product command. See the [Samsung report](compatibility/android-samsung-sm-g991b.md).

Example diagnostic output:

```text
Device: My Samsung (Android / MTP)
Files: 2,431 | Capture date available: 2,420 | Missing: 11

File             Size       Capture date          Created                Modified
IMG_1234.jpg     3.2 MB     2026-10-12 14:32:05   2026-10-12 14:32:06   2026-10-12 14:32:06
IMG_1235.heic    2.8 MB     2026-10-13 09:15:22   2026-10-13 09:15:22   2026-10-13 09:15:22
```

### 3.2 Manual verification

Test at least one actual Android phone and one actual iPhone, independently:
- Verify that device identification remains sufficiently stable across unplug/replug and application restarts.
- Compare reported capture dates with the dates shown by Picasa or verified from EXIF for representative photos.
- Include photos from different days, photos taken close together, screenshots, edited/shared pictures, and HEIC/JPEG if available.
- Check whether enumeration includes all expected camera folders and files.
- Measure metadata enumeration speed and confirm no image contents are transferred.
- For iPhone, verify USB trust/driver setup, original-file access, and whether its photo dates are actually exposed through the chosen API.
- Document any difference between capture time, file creation time, modification time, and local/UTC interpretation.

**Exit criterion:** A phone backend is accepted for synchronization only when it can reliably enumerate relevant files, identify that phone, return an appropriate capture timestamp for supported photos, and read a selected file. The last item may be validated by a separate, explicitly invoked test download after the metadata-only probe. An Android success does not imply iPhone compatibility.

If `MediaDevices` is insufficient on iPhone, investigate a separate iPhone backend using WPD/PTP or an iOS-specific library. Do not silently substitute file creation or modification time for capture time.

## 4. Technology and architecture

- **Language/runtime:** C#, .NET 10, Windows-targeted console app.
- **Device abstraction:** `IPhotoDevice`, implemented initially with `MediaDevices` / WPD for MTP/PTP.
- **Configuration/state:** `System.Text.Json`; no database is required.
- **Filesystem:** `System.IO`, streamed downloads, temporary files and atomic state updates.
- **Logging:** console plus plain-text log files.

Suggested components:

```text
PhonePhotoSync/
  Program.cs
  Cli/
  Configuration/
  Devices/
    IPhotoDevice.cs
    WpdPhotoDevice.cs
    IPhonePhotoDevice.cs       # only if needed after phase 1
  Models/
    DeviceInfo.cs
    PhotoInfo.cs
  Services/
    MetadataProbe.cs
    SyncService.cs
    DownloadService.cs
  Storage/
    JsonStateStore.cs
```

The device interface must provide device identity, enumeration of photo metadata, and opening a media-content stream for one selected object. Synchronization logic must not depend on the device protocol. Persistent WPD object handles must **not** be treated as stable identifiers across sessions.

## 5. Configuration and state

Location: `%LOCALAPPDATA%\PhonePhotoSync\config.json` (normally under the user's profile). A single JSON file is sufficient for version 1.

```json
{
  "schemaVersion": 1,
  "destination": "D:\\Photos\\Incoming",
  "devices": [
    {
      "alias": "My Samsung",
      "deviceId": "verified-stable-android-id",
      "lastImportedCaptureTime": "2026-10-10T18:30:00",
      "boundaryKeys": [],
      "captureDateSource": "dateAuthored"
    },
    {
      "alias": "My iPhone",
      "deviceId": "verified-stable-iphone-id",
      "lastImportedCaptureTime": "2026-09-25T12:00:00",
      "boundaryKeys": [],
      "captureDateSource": "ptpCaptureDate"
    }
  ]
}
```

Rules:
- `alias` is for the user; `deviceId` must be derived from a device identifier confirmed stable in phase 1. Device name alone is insufficient for automatic matching.
- `destination` is the staging folder from which the user will later organize photos in Picasa. Multiple phones may share it; filename collisions are handled safely.
- `lastImportedCaptureTime` is **a capture-date cutoff**, not the time the app was last run. It may be initialized manually from the user's existing written sync history. If it is missing, `sync` must refuse to run until the user explicitly sets an initial cutoff; do not assume an import-all operation.
- `boundaryKeys` tracks photos already imported at exactly the stored timestamp to handle multiple shots in the same second. An empty array at initial migration means “treat photos at the initial cutoff as already imported.”
- `captureDateSource` must be selected only after phase 1 validates its semantics for that device. The values above are illustrative, not guarantees of supported properties.
- A source timestamp without timezone information is compared consistently as the phone's local wall-clock value. Do not silently convert it to the PC's local timezone or to UTC. The selected normalization must match the verified dates used for the initial cutoff.
- The application may use a small separate `pending.json` journal for crash recovery; it is internal runtime state, not a photo archive.
- State updates must be atomic (write and flush a temporary JSON file, then replace the previous file). Preserve a backup of the previous valid configuration.

## 6. CLI contract

```text
phone-photo-sync init
phone-photo-sync list-devices
phone-photo-sync probe --device "My Samsung" [--output metadata.json]
phone-photo-sync sync --device "My Samsung" --dry-run
phone-photo-sync sync --device "My Samsung"
phone-photo-sync status
```

- `init`: create an example config without overwriting an existing one.
- `list-devices`: print recognized USB phones and identifying properties.
- `probe`: run the phase 1 metadata inspection; no sync-state changes.
- `sync --dry-run`: enumerate, validate and show eligible files, estimated total size, and proposed target names. No downloads or state changes.
- `sync`: perform the selected phone's import.
- `status`: show registered phones, checkpoints, destination, and any unfinished import session.
- Require explicit device selection when more than one eligible device is attached. Fail clearly when no registered device matches.
- Support Ctrl+C cancellation without advancing the checkpoint.

## 7. Synchronization algorithm

For a selected registered phone:

1. Load and validate JSON configuration; acquire a per-device lock to prevent concurrent runs.
2. Connect to the matched physical device and verify its identity.
3. Enumerate supported photo objects and obtain metadata **without reading image contents**.
4. Normalize capture timestamps using the verified per-device policy.
5. Stop with an actionable error if required metadata is absent or ambiguous. Do not advance the checkpoint. A later explicit policy for undated files may be added, but is not in version 1.
6. Select candidates where `captureTime > lastImportedCaptureTime`. For `captureTime == lastImportedCaptureTime`, select only objects not already represented in `boundaryKeys`; at an initial manually set checkpoint with no boundary keys, skip equality.
7. Build an import plan before downloading. Show count and estimated bytes.
8. For every candidate, reserve a nonconflicting filename, stream to a `.partial` file in the destination directory, finalize only after a successful transfer, and record completion in a durable pending-session journal.
9. On any transfer, verification, or device error, report partial success and **do not advance** the checkpoint. Successful files remain on disk; a retry uses the journal to avoid recopying completed items when the recorded file is still present and valid.
10. Only after all selected files complete successfully, update `lastImportedCaptureTime` to the maximum capture timestamp actually imported, update `boundaryKeys` for all imported objects at that exact timestamp, clear the completed journal, and atomically save state. If nothing was imported, leave the checkpoint unchanged.
11. Print the final summary and write a log entry.

A `boundaryKey` is a best-effort fingerprint made from the registered device ID, stable source path, filename, reported size and capture timestamp. It is **not** a content hash. Its stability must be checked in phase 1. Any mismatch or ambiguous identification is reported rather than silently overwritten.

### Important limitation of date-based synchronization

A photo **added to the phone later but carrying a capture time earlier than the checkpoint will be skipped**. This deliberately matches the existing manual workflow. It is not possible to solve that case reliably with a timestamp-only checkpoint; an import-history database or a deliberate lookback policy would be needed.

## 8. Media scope and output behavior

- Version 1 targets still-image files: `.jpg`, `.jpeg`, `.heic`, `.heif`, `.png`, `.dng`, `.tif`, `.tiff`, subject to what phase 1 proves is available. Extensions are matched case-insensitively and cross-checked with the reported media format when possible.
- Search all configured photo roots recursively; do not assume the Android camera folder is the only source.
- Keep the original filename and file format whenever possible; no automatic resizing, recompression, format conversion, or metadata editing.
- Never overwrite an existing destination file. If a target filename is occupied by an unrelated file, generate a deterministic unique suffix.
- An existing filename alone does not prove the file was previously imported. Only a verified pending-session record permits a no-transfer resume for a candidate newer than the checkpoint.
- Do not delete source photos or move them on the phone.
- iPhone import may require Apple device drivers, phone unlock/trust confirmation, and locally available iCloud originals. Media format conversion by the phone/driver must be detected during phase 1; prefer original-file transfer when possible.

## 9. Errors, observability, and data integrity

- Validate destination path, write permissions and available disk space before starting transfers.
- Log device alias, start/end time, checkpoint before/after, enumerated count, selected count, downloaded count, skipped count, bytes transferred, and per-file failures.
- Keep `.partial` files isolated from finished images; remove or safely resume them on the next run according to documented behavior.
- Validate successful stream completion and, where reliable, the expected byte size. Do not assume the device's reported size is authoritative if USB import converts file formats.
- Distinguish configuration errors, device-not-found/errors, missing-date/errors, and transfer/errors through clear messages and nonzero exit codes.
- Never change the checkpoint after a partial or unverified import.
- Never overwrite user media, delete on-device media, or modify Picasa data.

## 10. Testing and acceptance criteria

### Automated tests
- Cutoff filtering: `<`, `==`, and `>` checkpoint timestamps.
- Multiple photos sharing the checkpoint timestamp.
- Independent checkpoints for two registered phones.
- Missing/invalid timestamps and inconsistent timezone information.
- Interrupted transfer, retry, stale `.partial` file, and missing previously completed destination file.
- Filename collisions, invalid filename characters, case-insensitive extensions.
- Failed JSON update, invalid config and concurrent invocation.
- Empty device, zero new photos, device disconnect, and low disk space.

### Acceptance tests with real devices
- Android can be enumerated via USB; photo dates match verified capture dates without content transfer.
- iPhone can be enumerated via USB; photo dates match verified capture dates without content transfer. If not, iPhone support remains blocked pending a tested backend.
- `--dry-run` identifies the same date-filtered candidates that the manual workflow would retain.
- A real import saves expected photos to the chosen destination and updates only that phone's checkpoint.
- Removing an imported photo from the PC and running sync again does not cause a re-download merely because the file is absent.
- Failed/interrupted imports do not lose eligible photos and do not advance the checkpoint.
- Importing, moving or deleting photos in Picasa after a completed run does not break subsequent date-based imports.

## 11. Delivery milestones

1. **Metadata feasibility probe (go/no-go for each platform):** device discovery, metadata-only listing, timestamp/identity validation on real Android and iPhone devices.
2. **Core configuration and filtering:** JSON, enrollment, date normalization, candidate selection, and `--dry-run`.
3. **Reliable importer:** streamed USB downloads, filename collision handling, atomic checkpoint update, retry journal, and logs.
4. **End-to-end verification:** real-device tests, disconnect/retry tests, and compatibility with the manual Picasa organization step.

The first milestone must produce a short written compatibility report for each tested phone: device/OS version, backend, stable identifier, observed timestamp fields, accuracy, missing-date rate, enumeration performance, and whether full-resolution/original files are accessible.

## 12. Relevant technical references

- MediaDevices library: https://github.com/Bassman2/MediaDevices
- MediaDevices NuGet package: https://www.nuget.org/packages/MediaDevices/2.0.0
- Windows Portable Devices object properties: https://learn.microsoft.com/en-us/windows/win32/wpd_sdk/object-properties
- Windows Portable Devices device properties: https://learn.microsoft.com/en-us/windows/win32/wpd_sdk/device-properties
- Apple — Transfer photos from your iPhone or iPad: https://support.apple.com/en-us/120267
