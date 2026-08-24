# V5.1d — Authored Tree Shadow Keys

Tree shadows use ONE authored set per tree and rotate continuously with `ShadowDirection2D`.
There is no 8-direction texture bake.

Per tree:

- `*_footprint_compact_v51.png` — noon / short shadow
- `*_footprint_medium_v51.png` — mid-length shadow
- `*_footprint_long_v51.png` — sunrise / sunset shadow

The authored aspect ratio is preserved. Runtime only applies uniform scale + rotation, so the masks are not crushed or stretched twice.

Selection:

- `lengthCurve < 0.25` -> compact
- `0.25 <= lengthCurve < 0.66` -> medium
- `lengthCurve >= 0.66` -> long

Cluster mass uses `mass_shadow_blob_v51.png` and is an underlay only. Individual authored shadows stay responsible for directional detail.
