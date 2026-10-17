# Android full-copy experiment — interrupted 2026-10-17

## Final scope correction — 2026-10-18

**Subsequent video clarification:** camera-recorded videos are also required in the product. This report's completed scope is still images only; it must not be read as completing video validation or excluding videos from the intended importer. Next validation must check a camera video's capture day and complete transfer. App-cache/downloaded/shared media remain excluded.

The user clarified that only camera captures and screenshots matter. **App caches/assets, stickers, game images, and downloaded/shared images are excluded. Stop their diagnostics; do not resume the broad remaining-storage copy.** The relevant saved-inventory still images are already copied:

| Selected root | Inventoried/copied | Saved bytes | Local length mismatches |
| --- | ---: | ---: | ---: |
| `DCIM/Camera` | 3,601 / 3,601 | 12,440,778,898 | 0 |
| `DCIM/Screenshots` | 83 / 83 | 40,947,365 | 0 |
| `Samsung/Pictures/Camera Roll` | 340 / 340 | 1,045,742,521 | 0 |

**The still-image copy experiment is complete for these selected roots.** Camera took1h3m34s in one connection. Other roots were copied in later phases; do not invent one continuous combined time. Verification is EOF and reported/saved lengths, not phone-side hashes or a refreshed physical-library census. Videos were not copied. Filtered local evidence: `artifacts/android-s21-camera-screenshot-copy-scope-2026-10-18.json`.

No test remains running. Existing unrelated artifact copies are retained without deletion and excluded from the selected results. The throwaway harness now allows only those three canonical roots. The proposed ID-based lookup optimization was never run and has been reverted; production code is unchanged.

Historical additional runs: `android-s21-full-copy-remaining-connected-2026-10-17` completed3,033 files before a source-association mismatch; `android-s21-full-copy-final-pass-2026-10-17` completedzero and recordedfour changed IDs plus one app-cache read failure; `android-s21-full-copy-final-remaining-2026-10-18` completed14 and recordedapp-cache failures plus a later missing Telegram cache path. Those out-of-scope failures do not invalidate the successful Camera/screenshot copies. The harness was adjusted to recognize library-wrapped per-file errors separately from terminal session and local output errors; this remains experimental, not a production fix.

**The remaining sections are history. Their suggestions to finish all-storage/app-cache copying are superseded by this source clarification.**

The user explicitly requested copying all photos and measuring the time, then requested interruption and a saved handoff. Complete local copies are authorized for this experiment, superseding its former prefix-only restrictions. No phone files or import/checkpoint state were changed.

## Latest result — evening continuation

**Bulk Camera copying succeeded:** all 3,601 Camera images from the saved inventory,12,440,778,898 bytes (11.59 GiB), in 3813.867 seconds (**1h3m34s**) in one connection. Zero Camera copy failures. Every saved file reached EOF and matched reported size. This establishes bulk full-file access; it does not prove the cause of earlier bounded-read failures.

| Scope | Complete files | Saved bytes | Measured time |
| --- | ---: | ---: | --- |
| Camera | 3,601 | 12,440,778,898 | 1h3m34s, including checks/flushes |
| Other DCIM | 121 | 60,464,868 | 15.88s after Camera |
| Pictures | 188 | 139,811,583 | 25.29s after other DCIM |
| Other storage, all sessions | 900 | 279,169,628 | Across sessions; includes app/trash images |
| **Combined** | **4,810** | **12,920,224,977 (12.03 GiB)** | Interrupted/resumed; no continuous total |

All 3,910 DCIM/Pictures inventory entries copied:12,641,055,349 bytes in 3855.042 seconds (**1h4m15s**) during the evening run. Follow-up verification found4,810 distinct successful source paths and **zero missing/length-mismatched completed copies**. No source-side hash comparison. Reused inventory does not cover new files created afterward.

Evening session completed 3,989 copies, then an Android app-cache lookup returned “not found” at attempt 3,990 (20:30:54+02:00). Follow-up discovery20:33 found **no S21**. The not-found result does **not** establish cache-file deletion: disconnection is also possible. No terminal USB timeout was reported. A temporary catch to continue on FileNotFoundException was reverted before further copying; preserve conservative stopping until the cause is understood.

**Stopped awaiting phone availability.**10,089 paths remain incomplete: two recorded failures and10,087 unattempted. Reconnect/readiness question sent. No product/phone/import-state changes.

Additional ignored evidence:

- `artifacts/android-s21-full-copy-resume-camera-first-2026-10-17/`: finalized session summary, journal, inventory and3,989 copies.
- `artifacts/android-s21-full-copy-remaining-2026-10-17/`: failed discovery, zero attempts/bytes, finalized summary.
- `artifacts/android-s21-full-copy-progress-2026-10-17.json`: aggregated scope counts/times, failures and local-length verification.

Next resume: NEW directory, original full inventory and **all three content-run journals**. Latest harness verifies prior successful lengths, excludes successes and recorded failures without automatic retry, and retains interrupted paths. Priority: Camera/otherDCIM/Pictures/other non-Android storage/Android assets (scope 0/1/2/3/4; previous evening version used3 for all other storage). `STOP` file requests cancellation between files; not physically tested yet. Missing app-cache path needs an availability check before any explicit retry; missing paths do not establish session health.

## Earlier interrupted results

- Inventory: **14,899 supported image files, 14,979,433,059 reported bytes (13.95 GiB)**, in **509.590 seconds (8m30s)**. One image has unknown/zero reported size. JPG/JPEG, HEIC/HEIF, PNG, DNG, TIFF/TIF included; videos and `.thumbnails` directories excluded. API completion does not prove physical completeness: MediaDevices can suppress enumeration errors.
- Scope is broader than Gallery: Android 10,515; DCIM 3,722 (Camera 3,601); Pictures 188; Samsung 447; Download 10; Snapseed 6; WhatsApp 11. Includes app assets and exposed trash. Ordinal ordering put those before Camera.
- First session: **288 complete copies, 148,283,071 bytes**, in **107.331 seconds**. Attempt289 failed opening `\Internal storage\Android\data\com.google.android.gms\files\gmsnet2.jpg` (reported159 bytes), GetStream `0x80004005`. The initial harness stopped on every error. This is a generic per-file failure, not the earlier terminal timeout. Including inventory:617.258 seconds.
- Continuation reused inventory, excluding all 289 attempted paths and preserving earlier copies. Adjusted harness logs individual MediaDeviceException failures and continues, stopping on terminal session failures/cancellation or other exceptions. **533 complete copies, 104,862,297 bytes**, no new reported failures; last completion at **257.5 seconds** of copying. Process terminated at user request during attempt 534; no partial file existed in that continuation when inspected. This was forced termination, not graceful cancellation; COM cleanup was not guaranteed.
- Combined: **821 complete copies, 253,145,368 bytes (241.4 MiB)** retained. Local counts/lengths match flushed success journals. Each completion required EOF and received/saved/reported-length agreement where source size was known. No phone-side hash comparison or original-asset identity proof.
- **No Camera files reached. No complete-library success or total-time estimate is established.** Small app assets dominate the timings; do not extrapolate Camera throughput. Hundreds of full-stream opens per connection succeeded, beyond earlier prefix batches, but that does not prove the cause or a fix.
- Experiment stopped. No product-code changes. Throwaway harness built with zero warnings/errors; prior product verification remains260/260 tests after369e185.

## Preserved evidence (Git-ignored)

- `artifacts/full-copy-experiment/FullCopyExperiment.csproj` and `Program.cs.txt`: throwaway harness referencing MediaDevices2.0.0 and product WpdOperation classification.
- `artifacts/android-s21-full-copy-2026-10-17/`: full `inventory.json`, flushed `events.jsonl`, completed `summary.json`, 288 copies and failed attempt's zero-byte `.partial`.
- `artifacts/android-s21-full-copy-continuation-2026-10-17/`: filtered inventory, events, 533 copies. **Use `interruption.json`; `summary.json` is unfinished after termination.**
- Numeric-index filenames avoid collisions. Journals map source paths to destinations. Preserve all evidence and files; never overwrite them.

## Continue later

### Resumed at user request, 2026-10-17 evening

Run directory: `artifacts/android-s21-full-copy-resume-camera-first-2026-10-17/`.
The harness now combines both earlier journals, verifies saved successful lengths, excludes 821 completed paths and one known failed path without retrying it, and leaves the interrupted path eligible. Camera, remaining DCIM and Pictures are copied before the remaining storage:14,077 files/14,726,287,532 reported bytes. A `STOP` file in this run directory requests cancellation **between files**, followed by normal summary writing and device cleanup; an active synchronous USB call can still delay stopping. Per-file completion records now include timestamp, cumulative copy seconds and scope 0=Camera/1=otherDCIM/2=Pictures/3=other. No product changes.

Initial checkpoint:100 full Camera copies/456,954,132 bytes in 182.2 seconds, no copy error. This is progress evidence, not a final compatibility result. Consult the run's flushed `events.jsonl` and finalized `summary.json` for subsequent results; never treat an empty summary from an active run as final.

**Camera completed in the same session:**3,601 files, zero failures, **12,440,778,898 bytes (11.59 GiB),3813.867 seconds (1h3m34s)**. Final Camera completion2026-10-17T20:29:06.9788271+02:00. This includes per-file source lookups, full streaming, local flushes and length checks; it is not an optimized USB-only throughput benchmark. Other storage continued afterward. This physically establishes successful complete-file copying for the saved Camera inventory, while the cause of prior prefix failures remains unproven.

The instructions below describe safe resumption after any later interruption; the old continuation-mode caveat refers to the previous version, now superseded by the combined-journal logic above.

1. Confirm availability/unlocked/File transfer. Full-copy experiment authorization is already given.
2. Reuse the first inventory. **Prioritize DCIM/Camera, remaining DCIM and Pictures before app assets**, reporting each scope separately. Initial ordinal ordering delayed the relevant photo-copy measurement.
3. Use a NEW ignored destination. Combine both journals and exclude only successful source paths whose existing saved lengths match. Keep failures/interrupted paths visible. Current continuation mode excludes all `fileStart` paths from one journal, including failed/interrupted attempts: **do not reuse it unchanged as general resume logic**. Do not automatically retry the known unreadable app file.
4. Recheck source identity/size; retain per-file/overall timings, EOF/length verification, individual failure reports, terminal-session stop and disk reserve. No overwrite, phone writes, state writes or speculative library fix.
5. Add a stop-file check between files before the next long run so interruption can finalize reports and clean up. Synchronous COM calls can still delay stopping. Today's termination does not validate graceful cancellation.
6. Production metadata preview remains bounded. Changing it to full reads requires a separate behavior decision; this authorization covers the experiment.
