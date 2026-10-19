# Work status — 2026-10-20

Android and iPhone imports are implemented and exercised. See [handoff](handoff.md), [usage](../README.md) and [release notes](release-notes.md).

## Production state

| Alias | Destination | Checkpoint | Media |
| --- | --- | --- | --- |
| S21 | `C:\Users\eu\Pictures\Photos\_incoming\auto_s21` | 2026-10-18, tracked, 5 boundary keys | 131 files / 950,297,811 bytes |
| iPhone | `C:\Users\eu\Pictures\Photos\_incoming\auto_iphone` | 2026-10-19, tracked, 3 boundary keys | 1,835 files / 13,299,430,838 bytes |

Neither registration has pending state; no USB command is running. S21 registration matches its pre-iPhone-enrollment snapshot exactly. Its existing filename policy and roots are preserved.

The iPhone user explicitly approved all supported photos/videos, including forwarded and edited versions, by Windows WPD DateCreated day. Initial completed cutoff 2025-12-05 included 3 files from 6 December 2025. Production copied 1,697 photos and 138 videos. All final local lengths/SHA-256 values match archived transfer evidence. Repeat preview selects 0 files/0 bytes, with 0 date/format skips and 540 ignored nonmedia files. See the [production record](compatibility/2026-10-19-iphone-import.md).

## Implementation and validation

Version 1.1.0 adds explicit `iphoneWindowsDayV1` and rebinding of connection-local IDs for unfinished files after exact path/size/creation-timestamp agreement. Fresh ID validation still precedes content reads. Independent review found an Android candidate-refresh edge case; a failing regression reproduced it, and candidate refresh is now restricted to iPhone. **361/361 Release tests pass**.

The first attempt stopped after 1,132 completed files on a local sharing error without advancing the checkpoint. Normal resume verified/reused these copies and transferred the remaining 703. Monitoring stopped reading the active journal because ordinary readers can block atomic replacement. The final session archived successfully and its empty staging directory was removed.

Earlier Android reporting, cleanup, reconnect and cooperative recovery work remains complete: [Android follow-up](compatibility/2026-10-18-android-follow-up-validation.md). Earlier iPhone samples remain in the [assessment](compatibility/iphone-17e-ios-26.md) and [follow-up](compatibility/2026-10-19-iphone-follow-up.md). Private evidence stays Git-ignored under artifacts.

## Limits

Windows creation day is the explicit iPhone policy, not universal capture-day proof. There is no date fallback. Older late additions remain excluded. Local hashes are not phone-side hashes or proof of original-asset identity. MediaDevices traversal is not an independent Library census. The older October resource delta remains inconclusive; deletion/edit history is unknown and no old-photo search is pending.

Physical cable-pull/mid-native-read cancellation remains untested; local-error recovery and between-file cancellation are separate evidence. Native requests can delay cancellation. The user requested committing and pushing this implementation and its documentation.
