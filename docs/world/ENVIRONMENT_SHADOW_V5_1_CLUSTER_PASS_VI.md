# V5.1d — Cluster / Mass Shadow Pass

Fixes from the previous pass:

1. The previous `mass_shadow=ON` log was misleading because `mass_shadow_blob_v44.png` did not exist in the repo. V5.1d ships `v5_1/mass_shadow_blob_v51.png` and the mass system now logs its own READY line.
2. The six source shadows were previously mapped in the wrong order because extraction sorted by component Y. V5.1d maps the sheet by X: compact (left), medium (center), long (right).
3. Authored compact/medium/long masks are no longer stretched again on Y. Runtime preserves aspect ratio and rotates one set with the sun.
4. Tree contact AO is disabled; the authored footprint itself provides grounding.
5. Cluster mass remains soft and restrained, especially at night.

Expected startup log:

`[EnvironmentMassShadow2D] READY V5.1d | ... cluster_mass=... border_mass=...`
