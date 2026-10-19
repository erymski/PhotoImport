# Release notes

## 1.1.0 — 2026-10-19

Added explicit iPhone import policy `iphoneWindowsDayV1`: select all supported photos and videos under Internal Storage by the Windows-reported `DateCreated` calendar day. Forwarded files and edited versions are included; missing/invalid dates are reported without fallback. Existing Android registrations retain their filename policy.

iPhone pending transfers can resume after connection-local file IDs change, provided path, size and exact creation timestamp still match. The new connection's ID remains checked immediately before reading. Completed local copies retain length/SHA-256 recovery checks. Android pending candidates preserve their original known IDs even if later inventory omits them.

The Release suite passes 361 tests, including 13 iPhone-policy and Android-preservation cases. Hardware evidence and limitations are recorded in the [iPhone assessment](compatibility/iphone-17e-ios-26.md) and [19 October follow-up](compatibility/2026-10-19-iphone-follow-up.md). Sample date/dimension checks and full-resource reads do not establish a complete Photos Library census or universal original-asset identity.

Production import completed on 20 October: 1,835 files / 13,299,430,838 bytes, including 6 December 2025. All final local lengths/hashes matched transfer evidence; normal resume reused 1,132 copies after a local sharing error, and subsequent preview selected 0 files. See the [production record](compatibility/2026-10-19-iphone-import.md).
