# Handoff — 2026-10-17

## Resume here

The implementation is working against automated tests, but a complete Android Camera preview is blocked by intermittent WPD/MTP content-stream failures. Do not repeat the full folder scan or make speculative library changes. The next diagnostic should isolate repeated stream access **within one connection**, without recursive inventory traversal. The two successful standalone controls below used separate processes/connections and do not establish same-session stability.

Prepare a small throwaway reproduction using MediaDevices 2.0.0 and explicit Camera paths from the saved inventory. Keep the same 256 KiB per-photo cap, sequential reads, normal stream disposal, metadata-only output and stop-on-terminal-error behavior. Compare it with the existing inventory-plus-capture result. State the question and exact probe before running it under the selected Superpowers workflow. Existing Camera prefix-read authorization remains in force; check phone readiness rather than asking again for the same scope.

If repeated prefix closure is still implicated, propose a controlled comparison with complete-stream consumption. **Full-stream reads exceed the current exception and need separate approval with exact files/count/byte budget**, even if nothing is saved. Never silently drain the rest of a photo as a workaround. The cancellation logs are a hypothesis, not proof of the cause.

## User requirements and workflow

- Workspace `C:\Projects\PhotoImport`; current checkout, branch `feat/metadata-probe`. Preserve it; no merge or push requested.
- Use Markdown tracking, **not Beads**. Use Superpowers; ignore Ponytail. Native implementation and one final independent review were selected. Do not restart completed design/plan gates.
- Actual manually completed import cutoff: **2026-09-03**. Exclude that entire recorded capture day and earlier days. `2026-10-10` in old examples was only a diagnostic cutoff.
- Capture calendar day is sufficient; do not convert to PC timezone or infer dates from filenames/modification times. Preserve unknown/ambiguous evidence.
- Android: Samsung Galaxy S21 SM-G991B, friendly name `Eugene's S21`, normal USB File transfer. No ADB/USB debugging or phone app. iPhone deferred.
- The user authorized Camera-only bounded inspection with a maximum of 3,601 attempts and 256 KiB application reads per attempt. Smaller Camera controls fit that scope. Driver read-ahead is not bounded/measured. No automatic retries/reconnects, full-file fallback, photo saves or phone/import-state changes.
- A separate historical approval allowed exactly one full JPEG access test, already completed. It is not blanket full-read permission. Other-folder content reads also need scope agreement.
- `C:\Projects\photo-sort` was inspected read-only: it already uses MetadataExtractor on local files. Do not run its sorter; it can move/delete files and contains unrelated user changes.

## Completed code

- MediaDevices 2.0.0 / MetadataExtractor 2.9.3, .NET 10 Windows CLI.
- `probe`: metadata-only. `exif`: 1–3 explicit JPEG/HEIC/HEIF/DNG paths, bounded reads and schema-2 source provenance.
- `plan`: offline JSON preview with strict parsing, source association and completed/tracked same-day boundaries. No device provider or import-state writes.
- `preview`: explicit disjoint folder roots, required attempt limit, scoped inventory, bounded capture collection, saved inventory/capture/preview/run reports. Unknown/unsupported/unread rows stay unresolved.
- Independent live-preview review found two Important issues, both fixed in `182108f`: shared-object-ID case aliases must remain visible/unopened; late flush/close failures must correct owned report statuses. No second review. Detailed rulings are in `docs/work-status.md`.
- Hardware exposed missing terminal-error classifications. `369e185` adds timeout `0x80070079`, device failure `0x8007001F`, unreachable `0x80070141`, shutdown `0x802A0002` alongside existing removal codes. Native HRESULT or exact package diagnostic prefix only; preserve ordinary file errors and code-like filename text. Physical retry verified it stops at the first terminal failure, with earlier results retained.
- Last code verification: **260/260 Release tests**, build **0 warnings/errors** after `369e185`. Subsequent changes are documentation only. Do not claim that tests prove USB stability.

## Physical evidence

All paths below are relative to the repository and Git-ignored. Keep raw reports; do not overwrite them. No phone test remains running.

| Evidence | Result |
| --- | --- |
| `artifacts/android-s21-camera-pictures-metadata.json` | Scoped metadata inventory: DCIM/Pictures 3,909 images; Camera 3,601. Package can suppress native enumeration errors. |
| `artifacts/android-s21-day-validation.json` | Product JPEG/HEIC/DNG day samples succeeded and match user-confirmed Gallery days. HEIC maker-note warnings do not discard its valid day. |
| `artifacts/android-s21-camera-live-preview-connected/` | Camera/max3: 3,601 inventory rows, three valid days, 28,669 application bytes; expected incomplete preview. |
| `artifacts/android-s21-camera-complete-preview/` | First full attempt: six valid days, 107,982 bytes. Old adapter continued to 3,601 attempts after transport failure; 3,595 unresolved, exit4. |
| `artifacts/android-s21-camera-complete-preview-retry/` | Corrected adapter: 25 valid days, 430,731 bytes; attempt26 timed out; exit3 and no later opens. Inventory60.352 s, capture116.242 s including63.877 s timeout. |
| `artifacts/android-s21-camera-awake-100/` | User said screen would stay awake. First GetStream unreachable, zero content bytes, exit3; inventory still completed. |
| `artifacts/android-s21-single-camera-control-2026-10-17.json` | Initial standalone control found no phone; zero attempts/bytes. |
| `artifacts/android-s21-single-camera-control-connected-2026-10-17.json` | After readiness confirmation: `20171027_130322.jpg` day2017-10-27, 474 bytes, exit0; total20.211 s. No folder inventory. |
| `artifacts/android-s21-single-camera-reopen-2026-10-17.json` | Same photo, second standalone process: 474 bytes, same day, exit0; total1.955 s. |
| `artifacts/android-s21-camera-awake-100-control-2026-10-17/` | Latest inventory-plus-capture batch: inventory3,601 in48.217 s; nine available days, 175,658 bytes; attempt10 (`20171105_215534.jpg`) timed out after63.214 s. Capture80.518 s, exit3; 3,592 unresolved. |

All observed dates in these early sorted Camera batches are older than the actual cutoff. **Zero eligible is not a usable full-library result**: newer files have not been assessed. The failing file changes between runs; previously failed files have succeeded later. This does not support one consistently corrupt file as the explanation.

The user confirmed screen locking during an earlier run. Later screen-awake checks still failed, so lock behavior does not fully explain or resolve the issue. Do not ask for another full scan merely because the screen is awake.

## Driver/source investigation

- A read-only PnP query reported the S21 WPD device present/Status OK after the awake failure. Device recognition does not prove resource-stream access.
- Existing MTP operational logging was enabled; DriverFrameworks operational logging was disabled. Neither configuration was changed.
- The earlier 25-success retry window contains exactly 25 event1004 cancellations of operation `0x1009`, followed by event1008 communication failure. Microsoft identifies `0x1009` as GetObject: [command reference](https://learn.microsoft.com/en-us/windows-hardware/drivers/image/ptp-required-commands). Cancelling a deliberately bounded read may be normal; the correlation is not causation.
- Upstream tagged [StreamWrapper](https://github.com/Bassman2/MediaDevices/blob/2.0.0/Src/MediaDevicesShare/Internal/StreamWrapper.cs) releases its COM stream on Dispose. [Item.OpenRead](https://github.com/Bassman2/MediaDevices/blob/2.0.0/Src/MediaDevicesShare/Internal/Item.cs) obtains the default resource stream and includes GC/finalizer handling before GetStream. Our wrapper/handle also disposes. No missing application disposal or proven fix was identified.
- Upstream [issue417](https://github.com/Bassman2/MediaDevices/issues/417) reported a related stream-release/resource-busy regression in a 2.0 preview. The maintainer fixed it before stable2.0.0, and a reporter confirmed stable2.0.0 fixed their case. Its `0x800700AA` symptom is not our timeout/unreachable reproduction. Do not treat it as proof, blindly downgrade, or reapply its fix.
- SDK `MediaFileInfo.Refresh()` exists, but no caching/performance change has been implemented or validated. Avoid mixing optimization with transport diagnosis.

## References and remaining work

- Full chronology/evidence: [Samsung assessment](compatibility/android-samsung-sm-g991b.md).
- Durable tracking and review rulings: [work status](work-status.md).
- Approved design/plan: `docs/superpowers/specs/2026-10-16-scoped-live-preview-design.md`, `docs/superpowers/plans/2026-10-16-scoped-live-preview.md` — implementation complete; do not redo it.
- Remaining: reproduce/isolate sustained stream failure, get a complete usable Camera preview, independently compare other-folder coverage and settle unresolved formats. PNG/TIFF remain unsupported/unresolved. Enrollment, importer, state persistence, retry journal and iPhone work are not implemented.
- Current authorization does not permit changing Windows drivers/services, installing a phone app, enabling debugging, sending upstream messages, full media reads or broadening content scope. No such actions have been taken.
