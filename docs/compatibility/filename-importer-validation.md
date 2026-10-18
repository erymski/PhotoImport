# Filename importer hardware acceptance — current record

The product importer has been registered and exercised on the S21. Historical metadata/EXIF investigations and the throwaway full-copy trial remain separate feasibility evidence. iPhone validation is deferred.

| Check | Result |
| --- | --- |
| Registration | S21 serial identity; user-selected destination and roots saved |
| Initial production import |130 files: 120 photos/screenshots, 10 videos; 947,962,157 bytes; all local sizes/hashes verified |
| Repeat import |0 copied, 0 bytes, checkpoint unchanged |
| Physical reconnect | User unplugged/reconnected; existing registration matched without re-enrollment |
| New same-day photo |1 photo/2,335,654 bytes copied, day unchanged; boundary keys 4 → 5 |
| Following preview |0 eligible files/0 bytes; one unknown filename skip remains |
| Cooperative recovery | Isolated real-USB two-file import cancelled between complete files; resume reused 1 and copied 1; no production-state changes |

Current production destination: `C:\Users\eu\Pictures\Photos\_incoming\auto_s21`; 131 media files/950,297,811 bytes. Checkpoint 2026-10-18, tracked, 5 boundary keys; no pending production journal. Do not rerun init/reset cutoff to repeat acceptance tests.

Detailed latest evidence: [Android follow-up](2026-10-18-android-follow-up-validation.md). Earlier product evidence: ignored `artifacts/s21-first-product-import-console-2026-10-18.txt`, `artifacts/s21-first-product-import-verification-2026-10-18.json`, `artifacts/s21-registered-dry-run-2026-10-18.txt`. The initial registration/dry run selected 130 files; that historical count is not a promise about future inventory.

## Evidence limits

- Recovery probe cancellation was between full-file transfers, not physical cable removal or cancellation inside a blocking native read. It used real USB media and the production importer with isolated settings/output. Automated tests cover additional failure/partial-file transitions.
- Local SHA-256 verifies saved content against journal evidence, not against an independently computed phone-side hash or original-asset identity.
- MediaDevices can suppress native enumeration errors; completed traversal is not an independent physical census.
- Unknown filenames remain visible skips. Older late additions and same-path/same-size replacements retain the documented filename/date policy limits.
- Old archives are preserved, including their pre-fix timestamp limitations. Newly written reports use corrected finish timing.

## Future validation procedure

Normal use is already configured: connect/unlock/File transfer, keep awake and launch the utility; use dry run to inspect selection without content reads/state writes. Do not repeat bulk experiments without a specific new question. Any deliberate cable-pull/mid-read failure experiment needs its own small, explicit scope and should use isolated state/output. Preserve source media and never discard production recovery journals to clear an error. Record actual results and limitations, without publishing enrolled serials. Read [current handoff](../handoff.md) for the authoritative next step; archived logs are history.
