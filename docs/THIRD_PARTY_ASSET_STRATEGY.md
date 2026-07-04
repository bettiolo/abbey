# Third-Party Asset Strategy

The goal is to unblock gameplay work without turning the project into an asset
production exercise. Use off-the-shelf assets for replaceable prototype art, and
reserve the Blender pipeline for assets that define the game's identity.

## Policy

- Use third-party assets for gameplay placeholders, readable props, terrain dressing,
  generic villagers, crates, walls, trees, rocks, camp objects, and other non-signature
  art.
- Keep custom generated assets for identity pieces: the ruined abbey silhouette, bell
  tower, Black Hound, hound chain, signature nightmares, sacred flame language, and any
  asset that needs to match the art bible exactly.
- Prefer CC0 or permissive assets with clear provenance. Do not rely on assets whose
  license cannot be recorded in the repo.
- Do not import whole asset packs unless the pack is intentionally small. Stage only the
  files the prototype needs.
- Do not edit third-party source files directly. If Unity needs scale, materials,
  colliders, or composition, wrap the source model in a prefab or editor-generated
  replacement object.
- Do not feed third-party assets into AI generation or texture generation workflows.
- Do not let replacement art block the core loop. If a third-party asset is missing, the
  scene builder must still fall back to generated art or primitives.

## Source Priority

1. **Kenney CC0 assets** for environment primitives and simple props. These are easy to
   download directly, lightweight, and suitable for temporary gameplay art.
2. **Quaternius CC0 assets** for stylized 3D characters, medieval modular pieces, and
   fantasy props. These fit the low-poly direction and provide Unity-ready FBX files.
3. **Paid store packs** only for a clear bottleneck where CC0 sources are not good
   enough. Record the invoice, license, permitted seats, and redistribution limits before
   committing anything.
4. **Custom Blender generation** when an asset is an identity asset or when the gameplay
   needs a very specific silhouette that store assets do not cover.

## Staged Packs

Current selected files live under `unity/Assets/ThirdParty/` and are documented in
`unity/Assets/ThirdParty/README.md`.

Staged now:

- Kenney Nature Kit, CC0: trees, rocks, campfire, logs, canoe.
- Quaternius Medieval Village MegaKit Standard, CC0: crate, wagon, abbey wall pieces,
  round tower roof.
- Quaternius Universal Base Characters Standard, CC0: male and female base humanoids.

Downloaded but not fully staged:

- Quaternius full archive copies were downloaded to `/tmp` while preparing this import.
  Raw archives are not committed.
- Quaternius Stylized Nature MegaKit and Fantasy Props MegaKit are good future sources,
  but their Itch free download flow did not expose direct file buttons reliably from the
  agent environment. Keep them on the shortlist for a manual browser download if needed.

## Prototype Replacement Intent

Safe replacement targets:

- `campfire_t1`: Kenney `campfire_stones.fbx`
- `storage_pile_t1`: Kenney `log_stackLarge.fbx`
- `shipwreck_hull`: Kenney `canoe.fbx` until a better wrecked ship source is staged
- `shipwreck_crate_closed`: Quaternius `Prop_Crate.fbx`
- `shipwreck_barrel`: Quaternius `Prop_Wagon.fbx` until a barrel is available
- `forest_tree_01`: Kenney `tree_pineDefaultA.fbx`
- `rock_cluster_01`: Kenney `rock_largeA.fbx`
- `abbey_wall_broken`: Quaternius `Wall_UnevenBrick_Straight.fbx`
- `villager_lowpoly`: Quaternius `Superhero_Male_FullBody.fbx`

Keep generated for now:

- `bellkeeper_lowpoly`, because the Bellkeeper is an identity character.
- `black_hound_lowpoly` and `hound_chain`, because the beast bond depends on a custom
  silhouette.
- `bell_tower_ruined`, because it anchors the abbey silhouette.
- `lantern_post_t1`, until a compatible lamp or torch prop is staged.
- `shelter_t1`, until a proper shelter prefab can be assembled from modular pieces.

Note: folder and text `.meta` files are committed for stable Unity ownership. FBX
`ModelImporter` metadata is left to the local Unity editor because the agent environment
does not have Unity available to produce or validate importer settings.

## Workflow

1. Add or choose the source pack.
2. Record pack name, source URL, license, download date, and selected files in
   `unity/Assets/ThirdParty/README.md`.
3. Stage only the selected runtime files under `unity/Assets/ThirdParty/`.
4. Add wrapper prefabs or scene-builder mappings that reference the staged source files
   by path.
5. Run `./tools/check_all.sh`. Unity import validation still requires a local Unity
   editor, as documented in `docs/VERIFICATION_STATUS.md`.
