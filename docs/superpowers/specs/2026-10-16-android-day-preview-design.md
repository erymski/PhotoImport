# Android capture-day reporting and offline dry run

Status: approved by the user on 2026-10-16. Native execution in the existing feature branch remains selected. Implementation-plan review is the next gate; no additional hardware reads are included in this implementation scope. The user has since made the Android phone available for a later agreed validation.

## Intent and scope

The user needs to avoid downloading old photographs; a capture calendar day is sufficient, without exact hours, seconds, or UTC conversion. Reuse MetadataExtractor, including the approach in `C:\Projects\photo-sort`, rather than writing an EXIF decoder. Continue Android development without the currently unavailable phone or deferred iPhone validation.

This increment extends the existing explicit capture diagnostic for HEIC/HEIF and DNG, then adds a local preview from saved metadata. It is not an importer, enrollment/configuration system, or automatic USB content scan. It does not declare milestone 1 accepted for every format or phone.

## Alternatives and recommendation

1. **Recommended: extend capture reports and add an offline planner.** It can be tested now, keeps phone reads explicit, and exposes missing-date cases before live scanning or importing.
2. Start a live phone-wide dry run. This needs a larger content-read exception, a connected phone, and decisions about incomplete formats. Defer it.
3. Implement only the filter as a library. This is small but leaves the user without an executable preview. Include a thin offline command instead.

## Capture-date policy

- A capture day comes from valid EXIF DateTimeOriginal calendar fields in the source file, without PC-timezone or UTC conversion. General EXIF DateTime remains a comparison field, not a fallback. Filenames, file modification/creation dates, ICC profile dates, and GPS dates are not capture-day substitutes.
- Preserve raw dates, offsets, subseconds and directory association for diagnostics. Missing or invalid offsets/subseconds do not invalidate an otherwise valid capture calendar day. Exact timestamp derivation remains optional and must not fabricate an instant.
- Conflicting original dates across relevant EXIF directories make the capture day ambiguous. Do not combine fields from separate directories. Disagreement solely in time/offset on the same calendar day is diagnostic evidence; it does not make that day ambiguous.
- JPEG retains its lazy first-EXIF-segment path and explicit first-segment limitation. HEIC/HEIF and DNG use MetadataExtractor on a bounded in-memory prefix. No custom format/EXIF decoder, unbounded parser access to the USB stream, or full-file fallback.
- Keep the 256 KiB application cap and 1–3 explicit selected paths for the `exif` diagnostic. Driver read-ahead may transfer more over USB. Extend the accepted extensions; retain existing JPEG command syntax and output fields where practical.
- Non-capture Apple maker-note errors may be retained as warnings when a valid, unambiguous EXIF capture day is available. Capture-directory errors, failed/incomplete container parsing, and unsupported metadata remain explicit unresolved outcomes. Do not blanket-ignore parser errors.
- PNG, TIFF, and other unproven formats remain visible as unresolved for planning. The PNG spike did not establish absence of dates; do not turn its prefix failure into a missing-metadata claim.

## Capture report provenance

Add a versioned capture-report schema with canonical source path, device selection ID, reported source size, and persistent object identifier when available for each observation. Obtain these facts at read time through the separate content capability, keeping `IPhotoDevice` metadata-only. Record a validated capture day independently from the optional exact timestamp.

An offline planner joins captures to inventory by the exact device ID, canonical source path, and source size; compare persistent IDs when both reports provide them. Missing provenance, source changes, duplicate/conflicting observations, and mismatched devices are unresolved or invalid inputs, never a silent match. This is best-effort snapshot association, not content-hash identity or phone enrollment. Paths and reported size can remain unchanged after content edits; display this evidence limit.

Legacy JPEG exports remain readable for diagnostic display but lack complete source-size provenance; they cannot silently become verified planning inputs. Existing throwaway spike JSON is not a supported capture-report schema. No media content is needed to validate these schema rules in tests.

## Offline dry-run command

```text
phone-photo-sync plan --inventory metadata.json --capture-report capture.json [--capture-report more.json] --cutoff-day YYYY-MM-DD [--output plan.json]
phone-photo-sync plan --inventory metadata.json --capture-report capture.json --checkpoint checkpoint.json [--output plan.json]
```

Require exactly one of `--cutoff-day` or `--checkpoint`. This command reads local JSON only: it performs no USB discovery, content reads, destination-file lookup, configuration writes, downloads, or checkpoint updates. Route it before constructing the live device provider. Optional JSON output must be a new file, with normal output-path validation and race protection. Refuse input/output path aliasing.

Use one inventory per selected device, and allow repeated capture reports for explicitly collected samples. Ignore no inventory photos silently: classify each as eligible, already imported/older, or unresolved, with a reason. Capture entries that have no matching inventory object are reported as input problems. A partial inventory or missing/invalid capture metadata makes the preview incomplete, while still showing valid candidate rows. Only a complete, internally valid set can produce a complete preview. Snapshot completeness is through the package API, not independent physical completeness.

Output the cutoff, inventory scope, per-file decision, selected count, selected known bytes, unknown-size count, unresolved count, and overall completeness. Do not invent target names, infer duplicate imports from local files, or claim that a partial preview is ready for real downloading. No importer accepts this report yet.

Exit codes: 0 complete preview; 2 malformed arguments/input/schema/checkpoint or mismatched devices; 4 incomplete inventory, unresolved dates/provenance, or conflicting observations; 5 local input/output I/O failure; 130 cancellation. Preserve a useful partial result where input validity allows it; never overwrite an existing report.

## Day cutoff and same-day additions

A manually supplied cutoff means **everything through that calendar day was already imported**. For example, `--cutoff-day 2026-10-10` excludes 10 October and earlier, selecting known dates from 11 October onward. This matches the user's existing written-day history.

A later importer will need a different boundary state after importing only part of an open calendar day. Define that state now for pure filtering tests, without creating or updating it automatically:

```json
{
  "schemaVersion": 1,
  "deviceId": "exact-inventory-device-id",
  "cutoffDay": "2026-10-10",
  "boundaryMode": "tracked",
  "boundaryKeys": ["opaque-source-fingerprint"]
}
```

Modes are `completedDay` and `tracked`. `completedDay` excludes every photo on or before the cutoff and requires an empty key list. `tracked` excludes older days and already-recorded keys on the cutoff day, but includes newly encountered keys on that same day. Newer days are eligible in both modes. This prevents a later run from losing new same-day photos once an importer writes tracked boundary state.

Derive boundary keys deterministically from the exact device ID, canonical source path, reported size and capture day, using unambiguous serialization and a fingerprint. They are metadata fingerprints, not hashes of photo content. Require enough source evidence to form a key; surface ambiguity. Renames or source edits can change a key and cause a later re-import; that limitation is explicit. Deleting a local PC copy never changes eligibility. A photo added later with an older capture day remains skipped, matching the original date-cutoff workflow.

The offline preview reports keys so its behavior is inspectable. It does not advance the cutoff, write a checkpoint, or claim that any files have been imported. State persistence, enrollment, crash recovery, live scanning and the real importer remain later work.

## Verification and acceptance

Use synthetic JPEG/HEIC/DNG metadata fixtures or library-produced directories to test real extraction/resolution paths without downloading additional media. Cover calendar-day agreement despite time/offset differences, invalid dates, unrelated maker-note errors versus capture errors, directory association, truncated prefixes, short reads, and the application cap. Keep existing JPEG/CLI/resource-ownership tests passing.

Test the offline command with hand-authored inventory/capture/checkpoint fixtures: older/equal/newer days, completed versus tracked boundary days, two devices, missing provenance, changed sizes, duplicate paths/conflicting dates, unsupported PNG, partial inventories, malformed/schema-invalid inputs, output collisions, unknown sizes and cancellation. Assert selected files and reported reasons, not just serializers. Confirm it never invokes a device provider or alters source/checkpoint files.

Run the complete Release test suite and solution build, then one independent review using the already selected native execution method. No real phone access is part of this implementation run. Subsequent hardware validation of HEIC/DNG dates and any broader live scanning needs its own agreed scope when the phone is available. iPhone remains deferred.
