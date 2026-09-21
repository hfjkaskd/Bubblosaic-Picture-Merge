# Changelog

All notable changes to Remote Image Delivery are documented in this file.

## [1.0.0] - 2026-07-29

### Added

- Reusable HIGH/LOW priority remote-image delivery runtime.
- Stable-key request coalescing and grouped prefetch support.
- Persistent cache with resumable partial downloads, atomic commits, image
  validation, optional SHA-256 and byte-count checks, and budget pruning.
- `RemoteImageDeliveryConfig` ScriptableObject.
- Editor-only assembly and **Remote Image Delivery Dashboard**.
- Structured configuration editing and default configuration creation.
- Default configuration path compatible with Resources-based adapters:
  `Assets/Resources/RemoteImageDeliveryConfig.asset`.
- Prominent controls for bundled seed count, remote preference after the seed,
  and foreground batch timeout.
- Live runtime queue, session-statistics, and searchable request-history views.
- Cache statistics, folder access, budget pruning, memory lookup clearing, and
  confirmed disk-cache deletion.
- Resolved URL preview and non-persistent GET diagnostics with content,
  integrity, and Unity texture-decode checks.
