# Samsung Galaxy S21 (SM-G991B)

Assessment date: 2026-10-16, Europe/Madrid. Backend: MediaDevices NuGet 2.0.0. Status: **blocked pending completed metadata assessment**. This phone is not yet accepted for synchronization.

## Observed

- Windows discovered a Samsung SM-G991B over USB with protocol `MTP: 1.00`.
- Friendly name, device ID, PnP ID, and a serial-number candidate were exposed. Personal identifiers are retained only in private local artifacts, not this report.
- The first probe encountered native error `0x800701b1` (device does not exist) while reading object properties. MediaDevices wrapped this error in AggregateException; the initial application version crashed before producing JSON.
- The adapter now normalizes the worker exception, preserves the native diagnostic, and exports a partial report with a nonzero exit when such an error is surfaced. Regression tests also cover retaining earlier observations and preserving cancellation through cleanup.
- A corrected metadata-only retry was started. Its result is pending.

## Evidence still required

| Item | Status |
| --- | --- |
| Android version / driver version | Not collected |
| Identifier stability after unplug/replug and process restart | Not verified |
| Complete folder coverage / expected photo count | Not verified |
| Actual authored-date availability / missing-date rate | Awaiting retry report |
| Capture-date accuracy against EXIF/Picasa | Not verified |
| Timezone/local/UTC interpretation | Not verified; public API dates are projections |
| Enumeration duration and performance | Awaiting retry report |
| No-content transfer | Application calls no media/thumbnail APIs; independent driver/USB observation pending |
| Original/full-resolution access | Not tested; separate selected-file transfer validation required |

The public package API does not expose raw timestamp variants, a separate PTP date, or reported photo format/content type, and may hide some native enumeration failures. A clean probe alone cannot establish inventory completeness. See [backend notes](backend-notes.md) and the [acceptance procedure](README.md).
