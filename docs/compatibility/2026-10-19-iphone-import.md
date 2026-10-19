# iPhone import — 19 October 2026

## Approved scope and policy

The user authorized production import to `C:\Users\eu\Pictures\Photos\_incoming\auto_iphone`, including **6 December 2025** and all later Windows-reported days. All supported photos and videos are included, including forwarded files and edited versions. The phone was left in Keep Originals mode following the earlier selected-resource experiments.

Version 1.1.0 adds explicit `iphoneWindowsDayV1`. It uses WPD `DateCreated` calendar fields as reported to Windows, without timezone conversion or EXIF/filename/modified-date fallback. This is the user's selected file-date policy, not a claim that all file dates are independently verified capture dates. Registration alias is `iPhone`, root `\Internal Storage`; the initial completed-day cutoff is 2025-12-05 to include 6 December itself. Private device identifiers are omitted.

Existing Android registrations retain `androidFilenameDayV1`. The S21 registration, including checkpoint 2026-10-18 with 5 boundary keys, matched the saved pre-registration configuration exactly after iPhone enrollment.

## Implementation verification

The Release suite passes **361 tests**, including 13 new policy/resume/Android-preservation cases. They cover the inclusive start date, invalid/missing dates without fallback, reported calendar fields with offsets, metadata-only dry run, tracked-file exclusion across changed IDs, pending resume after reconnection, changed timestamps, and a fresh ID change before reading. Review found a possible loss of an Android journal's known ID when refreshed inventory omitted it; a failing regression reproduced this, and candidate refresh is now limited to the iPhone policy.

Unfinished iPhone candidates require exact path, size and reported creation timestamp agreement before rebinding the current connection's ID. The ID is checked again immediately before a content read. Completed local copies retain length and SHA-256 recovery validation. These checks cannot prove original-asset identity or detect every same-path/same-size replacement.

## Preflight

Metadata-only dry run selected **1,835 files / 13,299,430,838 bytes**, with **0 skipped date/format entries** and **540 ignored nonmedia files**. The destination was empty; available space was about 245 GiB, above the selected bytes plus the 2 GiB reserve. The exact configured destination was verified before starting the copy.

Private logs and configuration evidence remain under Git-ignored `artifacts/`. MediaDevices can suppress native enumeration errors; successful API traversal does not establish an independent Photos Library census. Library asset counts and USB resource counts are not interchangeable. Earlier sample and connection evidence remains in the [iPhone assessment](iphone-17e-ios-26.md) and [selected follow-up](2026-10-19-iphone-follow-up.md).

## Production transfer and recovery

The import started on 19 October and finished on 20 October, local time. The first attempt completed 1,132 files / 5,343,333,872 bytes, then stopped with a Windows sharing violation before checkpoint commitment. Its next file remained journaled as planned with a full-length partial file. Reading the working journal with ordinary PowerShell `Get-Content` during monitoring was a plausible cause: an isolated Windows test reproduced the same atomic-replace failure with a reader lacking delete sharing, and replacement succeeded with delete sharing. The exact failing handle was not captured, so this is not a definitive attribution. Subsequent monitoring used destination directory metadata and did not open the working journal.

Normal resume verified/reused all 1,132 completed copies and copied the remaining 703 files / 7,956,096,966 bytes. The uncommitted partial was restarted according to the existing recovery policy. The final command exited 0 and committed checkpoint **2026-10-19, tracked, 3 boundary files**. There were **1,697 photos and 138 videos**, totaling **1,835 files / 13,299,430,838 bytes**. No phone content was modified or deleted.

A subsequent metadata-only dry run selected **0 files / 0 bytes**, with 0 skipped date/format entries and 540 ignored nonmedia files. Offline status reports no pending session for either phone; the S21 remains at 2026-10-18 with 5 boundary files. This is recovery from a local sharing error; it does not constitute a physical cable-pull test.

## Independent local verification

Fresh local reads verified all **1,835 final files** against archived source/transfer lengths and SHA-256 values, with **0 discrepancies**. These are local transfer hashes, not phone-side hashes. Selected days range from **2025-12-06 through 2026-10-19**, including **3 files on 6 December**. Extension counts: 1,460 JPG, 232 PNG, 5 HEIC, 102 MOV and 36 MP4. These are resources, not a reconciled Photos asset count.

The S21 registration still matches the pre-enrollment snapshot exactly. The current iPhone staging-session directory is absent after successful archival; the shared parent remains. No USB command is running. Private verification: `artifacts/iphone-production-verification-2026-10-20.json`.
