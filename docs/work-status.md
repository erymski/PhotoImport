# Work status

Task tracking uses Markdown, per the user's instruction on 2026-10-15. Do not use Beads for this project.

## Milestone 1: metadata feasibility probe

Source: [product specification](specs.md), sections 3 and 11.

- [x] Review product specification and current repository.
- [x] Confirm .NET 10 SDK availability and review MediaDevices package compatibility.
- [x] Prepare [milestone design](superpowers/specs/2026-10-15-metadata-probe-design.md).
- [x] User review of written milestone design, approved on 2026-10-15.
- [x] Write and self-review [implementation plan](superpowers/plans/2026-10-15-metadata-probe.md).
- [x] User review of implementation plan; native execution selected on 2026-10-15.
- [x] Implement device discovery, metadata probe, console/JSON output, and automated tests using TDD.
- [x] Build and verify the CLI: 55 tests passed after hardware/review regressions; Release verification and final evidence recorded below.
- [x] Write [physical-device assessment procedure](compatibility/README.md) and [backend evidence limits](compatibility/backend-notes.md).
- [x] Complete independent whole-change review and address important findings: reviewer found no outstanding Critical, Important, or Minor findings after the fix pass.
- [ ] Test a real Android phone; record compatibility evidence.
- [ ] Test a real iPhone; record compatibility evidence independently.
- [ ] Validate selected-file/original access through a separately invoked transfer test before accepting either backend for synchronization.

## Current findings

Implemented on `feat/metadata-probe`, using the MediaDevices 2.0.0 NuGet package. Initial discovery found no phone; a later Samsung S21 connection was discovered over USB/MTP. The first real probe exposed a wrapped device-removal exception and failed before export; regression fixes now preserve partial results and native diagnostics, and a real retry is running. See [Samsung assessment](compatibility/android-samsung-sm-g991b.md). iPhone remains untested. Milestone 1 remains open until actual-phone timestamp/identity/coverage validation and the separately invoked selected-file/original transfer test.

## Review and implementation decisions

The independent whole-change reviewer inspected production code, tests, package XML/IL, and upstream source. The final verdict permits merging the diagnostic implementation; it does not certify physical compatibility. Review findings addressed: wrapped worker exceptions, loss of earlier sibling observations, cleanup overriding primary results/cancellation, and ownership of device handles during interrupted discovery. The reviewer did not rerun tests; execution evidence comes from this session.

- Use PowerShell and the Markdown ledger for Windows bookkeeping instead of Bash scripts; task bookkeeping is maintained manually.
- Test timestamp/JSON fidelity through mapper and report-writer consumers rather than trivial serialization-only tests; those consumer tests now pass.
- Report unavailable raw variants, separate PTP dates, format/content type, and package-hidden enumeration errors explicitly. Cost: these evidence gaps may require a WPD extension and prevent accepting a phone backend; a completed API traversal does not guarantee complete photo coverage.
- Stream child enumeration to preserve observations before errors/cancellation. Cost: callers must consume the iterator within the live device session.
- Read-only synchronous COM calls can delay Ctrl+C until they return. Compatibility reports must state this limitation; cancellation is never a claim of immediate driver interruption.

No review findings were deferred as minor issues. Physical coverage/date accuracy, reconnect stability, iPhone support, raw-data evidence gaps, and original-file access remain acceptance gates.
