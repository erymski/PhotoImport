# Filename importer hardware acceptance

## First production copy result — 2026-10-18

Following explicit user approval, product `sync --device S21` exited0:130files (120photos/screenshots +10videos),947,962,157bytes copied to the registered `C:\Users\eu\Pictures\Photos\_incoming\auto_s21`; no reused transfers. The unknown Office Lens filename remained a visible intentional skip. State committed2026-10-18,tracked boundary with4keys; offline status shows pending none.

Post-copy local verification independently checked all130saved files against the completed journal entries: destination containment, source-reported/received/local byte lengths and local SHA-256 all match. No failures, partial files or pending journals. This is not a phone-side content hash or original-asset identity proof. Evidence: `artifacts/s21-first-product-import-console-2026-10-18.txt`, `artifacts/s21-first-product-import-verification-2026-10-18.json`. Exact product-run elapsed time is not claimed because archived finish timing has the known reporting limitation. Post-enrollment physical unplug/replug, naturally interrupted production recovery and a subsequent real no-new-media run remain untested; fake-device/filesystem tests cover their logical transitions.

## Recorded registration/dry-run result — 2026-10-18

User supplied `C:\Users\eu\Pictures\Photos\_incoming\auto_s21`; created the missing directory, then registered aliasS21 with serial-based identity, completed cutoff2026-09-03 and all three approved roots after USB connection readiness. Product dry-run exited0:130eligible files (120photos/screenshots,10videos),947,962,157bytes; one unrecognized Office Lens filename skipped. Offline status afterward shows unchanged initial checkpoint and no pending journal; destination contains0items. No media content was copied. Evidence: `artifacts/s21-registered-dry-run-2026-10-18.txt`. Post-enrollment physical reconnect and actual product copying remain untested. No serial value is published here.

## Procedure

The registration/dry-run results above are actual product hardware observations. Steps below define remaining acceptance; completed registration should not be repeated. The successful126-file throwaway trial is separate [feasibility evidence](2026-10-18-filename-selection-trial.md). Implementation tests use fake devices and temporary local files.

1. Obtain the real user-selected existing destination and the chosen camera/screenshot roots. Do not substitute an example directory. Initial S21 completed day is2026-09-03. Confirm any intended real copy scope before a hardware copy run; implementation approval did not select that destination.
2. Connect/unlock the S21 with USB File transfer, keep it awake, and run `list-devices`. Register once using `init` with its exact ID/unique friendly name, alias, real destination, cutoff and roots. Init reads identity/root metadata and writes local configuration, with no media reads.
3. Run offline `status` and confirm destination/day/identity policy. Unplug/reconnect the phone, run discovery and `sync --dry-run`, and verify that enrollment still matches. A missing or changed identity must stop rather than reuse another phone's checkpoint.
4. Compare dry-run selected count/names/bytes/skips with the intended scope. It should not open content or change destination/config/journal. Unknown filename dates stay skipped and visible. Photos/videos on/before the initial completed day are excluded; don't expect actual new inventory to equal the historical126-file snapshot.
5. Under the separately selected hardware copy scope, run normal launch or `sync --device <alias>`. Check successful destinations, photo/video counts, bytes and checkpoint. Existing files must remain intact, with suffixes for collisions. Do not modify phone files or involve Picasa.
6. Repeat sync: previously completed boundary files should not transfer again. An approved later same-day media sample should be eligible; earlier late additions intentionally are not. No source mutations are needed merely to test this; fake-device tests cover the boundary behavior.
7. If an interruption naturally occurs, inspect `status`/pending journal, reconnect and rerun. Verify completed local files are reused only after hash/length checks, unfinished ones restart fully, and checkpoint stays unchanged until success. Do not deliberately unplug during a large real copy without agreeing on that experiment.

Record actual identity observations across reconnect, counts/skips, transferred/reused bytes, durations, source-reported/local lengths, pending/checkpoint outcomes and limitations. Avoid publishing serial numbers unnecessarily. No phone-side content hash comparison is claimed. A synchronous USB call may delay Ctrl+C; do not promise immediate interruption.
