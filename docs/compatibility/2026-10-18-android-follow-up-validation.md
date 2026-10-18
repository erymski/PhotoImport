# Android reporting and hardware follow-up — 2026-10-18

User approved the remaining Android follow-up work and confirmed physical unplug/replug, File transfer/unlocked state, and one new Camera photo. Production checkpoint/media were preserved except for the explicitly tested new-photo import.

## Reporting correction

Review scope rulings: hardware evidence and test execution are supplied by this session, not the reviewer. Already-committed journal finalization skips inventory and does not reconstruct original session counters; an existing matching archive remains untouched, so this is not a newly inferred zero-file inventory. Broader collision-allocation changes were outside this correction; final no-overwrite moves still resolve occupied destinations, and preview names remain advisory if filesystem conditions change. No additional review was requested.

Five regressions cover normal/no-candidate/commit-recovery archival timing, distinct dry-run proposals for duplicate basenames, and nonmedia counts through archive/CLI without inspecting nonmedia metadata/content. Four initial cases failed before the correction; the additional commit-recovery case pins the shared archive helper. Full Release suite 341/341 passing; Release build 0 warnings/errors. One independent read-only review found no findings; it did not run tests or hardware. Existing config/pending schemas remain unchanged; old archives omit the optional count safely and remain preserved, including their historical timestamp limitations.

## Reconnect and same-day selection

- Existing serial-based registration matched after the user's reported unplug/replug; no reset/init required.
- Preview selected only `DCIM/Camera/20261018_152453.jpg`, 2,335,654 bytes. Other already imported media stayed excluded.
- Product sync copied 1 photo/0 videos/2,335,654 bytes, then committed the same day 2026-10-18 with 5 boundary keys (previously 4).
- Verified the new local file's length and SHA-256 against history. Incoming folder now contains 131 media files/950,297,811 bytes; no pending production session.
- Subsequent metadata-only dry run selected 0 files/0 bytes. The same unknown Office Lens filename remained a visible intentional skip; actual scoped inventory reported 0 nonmedia files.

Evidence: ignored `artifacts/s21-android-follow-up-verification-2026-10-18.json`; actual production config/history under `%LOCALAPPDATA%\PhonePhotoSync`. No enrolled serial is included here.

## Real USB recovery with isolated state

Throwaway helper: `artifacts/android-recovery-validation/`. It uses the product `ImportRunner`, `ImportStateStore`, `MediaTransfer` and real WPD metadata/streams, with a filter exposing only two previously imported Camera JPEGs. It writes only to a new ignored `artifacts/s21-isolated-recovery-2026-10-18/` destination/state, never production settings or phone files. The two selected files total 2,241,122 bytes.

The helper cancels through the existing cancellation token immediately after the first full file is durably marked completed. It does not unplug the phone or interrupt its stream. First run: exit 130, one completed entry of 2, isolated cutoff still 2026-09-03, only 1 USB media open. Resume: exit 0, 1 reused/1 copied, 2 USB media opens total, both local length/hash checks match, isolated checkpoint committed and pending journal cleared. Production config hash before/after the entire probe is identical.

Evidence: ignored `artifacts/s21-isolated-recovery-2026-10-18/{cancelled,result}.json` and its isolated `state/history/`. Do not merge this isolated registration/journal into real settings or count its copies as production incoming media.

This verifies cooperative import cancellation **between full-file transfers** and real USB-backed resume. It does not establish cable-pull handling or cancellation inside a blocking native read. Those scenarios remain separate from this evidence; a native request may delay cancellation. iPhone remains deferred.
