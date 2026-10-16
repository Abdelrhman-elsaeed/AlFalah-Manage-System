# Asset provenance

- `logo.png`: existing `frontend/src/assets/Logo.png`; retained unchanged.
- `school-pulse.svg`: existing `frontend/src/assets/dashboard/school-pulse.svg`; retained unchanged.
- `dashboard.png` and `timetable.png`: captured from the unchanged local Angular application using the browser-only synthetic responses in `capture-reference.cjs`. No operational student records or real account credentials are used.
- `Tajawal-*.ttf`: Google Fonts repository, `ofl/tajawal`; license text in `OFL-Tajawal.txt`.
- `gsap.min.js`: GSAP 3.14.2, downloaded from the pinned jsDelivr package URL in `fetch-assets.py`. Its copyright and license URL are preserved in the source header: https://gsap.com/standard-license.
- `three/`: Three.js 0.181.2, pinned local copies of `build/three.module.js`, `build/three.core.js`, and `examples/jsm/geometries/RoundedBoxGeometry.js` from the upstream npm/jsDelivr release. MIT license text is included at `three/LICENSE`.
- School, classroom, guidance office, gate, archive and characters: original procedural geometry in `school-world.mjs`. The school is an illustrative diorama, not a surveyed reconstruction of the actual campus. No generated photographic people, third-party 3D models, or operational records are included.

The source compositions use local copies of these assets for deterministic rendering. Production integration, if approved, uses the encoded films and posters rather than shipping these authoring dependencies in the application.
