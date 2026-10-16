# Proposed Android JPEG EXIF validation addendum

Status: proposed; awaiting user approval. The one-photo experiment is complete and does not authorize this extension.

## Evidence and purpose

The Samsung S21 exposes no authored date through the tested WPD MTP/PTP properties. A separately approved single-JPEG experiment obtained DateTimeOriginal, subseconds, and an explicit UTC offset from its first 256 KiB over MediaDevices. See the [phone assessment](../../compatibility/android-samsung-sm-g991b.md). This provides a candidate Android capture-date path while iPhone testing is deferred.

## Recommended scope

Keep the existing metadata-only probe as the default. Add a separately invoked, explicit JPEG EXIF diagnostic mode using MediaDevices 2.0.0 and MetadataExtractor 2.9.3. Opening a media stream reads content and is a deliberate exception to the original metadata-only requirement for this mode.

- Read each selected JPEG prefix into memory, with an application cap of 256 KiB per object. Stop earlier when the complete EXIF segment is available where practical. Report actual bytes read; driver read-ahead may exceed the cap on USB traffic.
- Save parsed capture fields and diagnostics only, never JPEG prefixes. No full-file fallback, thumbnail request, phone mutation, or synchronization state.
- Preserve DateTimeOriginal, SubSecTimeOriginal, and OffsetTimeOriginal as separate raw fields with source provenance. Derive a clock/offset only when valid. Missing offsets remain unknown; do not assume the PC's timezone. Modification time and filenames remain diagnostic comparisons, never capture-date fallbacks.
- Reject or report missing, invalid, ambiguous, and out-of-prefix metadata explicitly. Do not claim support for HEIC, DNG, PNG, or other formats from this JPEG result.
- Test parser bounds, timestamp fidelity, incomplete/malformed input, output protection, and partial failure handling with synthetic inputs before further USB reads.
- Validate an explicitly selected sample of at most three camera JPEGs spanning the earlier sampled periods, and compare with independent gallery/Picasa capture information. Record total duration and bytes separately. Further library-wide scans remain outside this validation step.

## Acceptance and limits

This extends milestone 1 diagnostics only. It does not enable importing or synchronization. Android acceptance still needs representative date accuracy, folder coverage, identity/cancellation evidence, and separately approved original-file access validation. One successful JPEG does not establish those properties. iPhone validation remains deferred by the user.

Approval authorizes implementing this optional JPEG diagnostic and the bounded three-photo validation described above. It does not authorize full downloads, unlimited content scans, or a sync engine. After approval, update the main specification and write the implementation plan before changing product behavior.
