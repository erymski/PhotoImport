# Current handoff — 2026-10-20

Current checkout is main; previous changes were pushed through be9a474. The user requested committing and pushing the iPhone implementation, version 1.1.0 and production documentation. Before committing run `dotnet csharpier format .`.

## Normal use and state

Run `bin\Release\net10.0-windows\phone-photo-sync.exe` with the registered phone connected, unlocked and awake. Android requires USB File transfer; iPhone requires Trust/photo access and was left in Keep Originals. If both phones are connected, select `sync --device iPhone` or `sync --device S21`. Preview with `--dry-run`; `status` is offline. Do not rerun init or reset checkpoints.

Real state/history is `C:\Users\eu\AppData\Local\PhonePhotoSync`. The sandbox redirects LOCALAPPDATA: run real importer commands outside it, using the appropriate tool approval for destination writes. Never publish serials or delete journals to clear an error. While copying, avoid ordinary Get-Content on working journals: readers without delete sharing can block atomic replacement. Monitor destination directory metadata or use delete-sharing readers.

| Alias | Destination | Policy | Checkpoint | Media |
| --- | --- | --- | --- | --- |
| S21 | `C:\Users\eu\Pictures\Photos\_incoming\auto_s21` | androidFilenameDayV1 | 2026-10-18, tracked, 5 boundary keys | 131 files / 950,297,811 bytes |
| iPhone | `C:\Users\eu\Pictures\Photos\_incoming\auto_iphone` | iphoneWindowsDayV1 | 2026-10-19, tracked, 3 boundary keys | 1,835 files / 13,299,430,838 bytes |

Both identities are serial-based; neither has pending state. No USB command is running. S21 registration exactly matches the saved pre-iPhone snapshot; roots remain DCIM/Camera, DCIM/Screenshots and Samsung/Pictures/Camera Roll. Do not restore its original cutoff 2026-09-03.

The user approved all supported iPhone photos/videos, including forwarded/edited versions, under `\Internal Storage`, selected by Windows WPD DateCreated calendar day without timezone conversion or fallback. Initial cutoff 2025-12-05 included 6 December itself (3 resources). Final copies: 1,697 photos and 138 videos. All local lengths/hashes match archived transfer evidence; repeat preview selected 0 files/0 bytes, 0 date/format skips, 540 ignored nonmedia files. The empty current staging-session directory was removed after archival. See [production record](compatibility/2026-10-19-iphone-import.md).

## Latest changes and evidence

Version 1.1.0 adds the explicit iPhone policy, root validation and reconnect ID rebinding. Unfinished entries need exact path/size/creation-timestamp agreement; MediaTransfer still checks the current ID before reading. Android retains known journal IDs if new inventory omits them. Independent review found that Android edge case; a failing regression reproduced it and the fix passed the full **361/361 Release suite**. See [release notes](release-notes.md).

The first production attempt completed 1,132 files before a local sharing error stopped it without committing. Ordinary journal polling was a plausible cause, reproduced separately as an atomic-replace sharing failure; monitoring changed to directory metadata. Normal resume verified/reused all 1,132 copies and copied the remaining 703, committed and archived successfully. This is not a cable-pull test.

Earlier iPhone full-read/transfer-mode/dimension/day evidence: [assessment](compatibility/iphone-17e-ios-26.md), [selected follow-up](compatibility/2026-10-19-iphone-follow-up.md). The old October resource delta remains inconclusive: the phone belongs to someone else, deletion/edit history is unknown and no old-photo search is pending. S21 reconnect, new same-day file and isolated cooperative-recovery evidence: [Android follow-up](compatibility/2026-10-18-android-follow-up-validation.md).

## Limits and workflow

Windows creation days are the explicit policy, not universal capture-day proof. Older late additions stay excluded. Matching metadata cannot detect every same-path/same-size replacement. Local hashes are not phone-side hashes/original-asset proof. MediaDevices traversal is not a physical Library census. Physical cable-pull/mid-native-read cancellation remains untested; synchronous native requests can delay cancellation.

Private evidence/media stays Git-ignored under artifacts. Use this native checkout, no new worktree. Do not use Beads. Do not run or modify `C:\Projects\photo-sort`. No implementation or hardware step remains for the authorized import. Archived handoff/work logs are historical, not instructions to repeat registration or copies.
