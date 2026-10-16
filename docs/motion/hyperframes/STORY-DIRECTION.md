# نظام الفلاح — من أول حضور إلى آخر أثر

The revised film follows one chronological school day. It replaces the 50-second review film. The other two early studies and the captured timetable screen remain excluded from the current review. No homepage integration is authorized yet.

Brand: the existing Al-Falah logo, Tajawal Arabic typography, forest green `#0b3f29`, warm ivory `#f5f4ed`, and gold `#e0b31b`. The audience is school leadership, teachers, secretarial staff, student affairs, and security. Large Arabic messages carry the story; illustrations and information graphics explain the roles.

## Storyboard and choreography

| Film time | School time | Story | Composition and handoff |
| --- | --- | --- | --- |
| 0–5.45 | — | نظام الفلاح: يوم كامل في إيقاع واحد | Oversized Arabic opening and logo. Zoom through actual Arabic letter geometry into the next shot. |
| 4.5–11.55 | 06:40 | Staff readiness and absence | Ivory information graphic with 24 illustrative staff marks, including an outlined absence. «حضور» becomes the next scene's mask. |
| 10.55–18.1 | 06:50 | Student arrival and biometric timestamps | Close moving camera around a reader; vector fingerprint draws in. Zajel file import connects to lateness follow-up. Circular reveal into the school exterior. |
| 17.15–24.1 | 07:00 | Morning assembly | Front view rises into a courtyard overhead; pupils settle into three rows. Row geometry motivates the staggered column reveal. |
| 23.15–31.5 | 07:30 | Lessons and time | Clock hands accelerate; a diagrammatic schedule expands into a grid. «حصّة» provides another typographic portal. |
| 30.5–38.6 | 08:15 | Classroom, absence and observations | Front-to-side camera, empty chair, attendance marks and a teacher observation. A narrow line expands into student follow-up. |
| 37.6–45.7 | 09:10 | Counselor and guardian relationship | Opposite-side office view, paper handoff, and teacher → counselor → guardian graphic. Clock-centred circular mask advances the day. |
| 44.7–52.8 | 12:45 | Dismissal and security | Camera passes across the gate. Request, approval, verification, crossing, and actual exit confirmation appear in order. Permit paper expands into the archive. |
| 51.8–59.8 | 13:00 | File storage | Archive below the headline; a separate digital file index, versions and permission copy. Centre shutter reveals the signature. |
| 58.8–64 | — | System signature | Logo and large system name; organization, follow-up and saved work. Held ending. |

Scene overlaps are intentional. The 64-second duration leaves room to read Arabic while the cameras and information graphics continue moving. This is a user-started narrative film, with a held ending. It does not require an automatic loop.

## Feature fidelity

- Zajel import exists in `frontend/src/app/features/student-affairs/biometric-import` and `backend/AlFalah.Application/StudentAffairs/Biometrics/Handlers/ImportZajelBiometricCommandHandler.cs`. The film says file import, not live device synchronization. Arrival timestamps support lateness review; daily absence is recorded independently.
- Staff attendance and daily student absence are handled through the secretary's attendance workflows. Teacher academic observations are separate.
- The student affairs officer reviews follow-up and approves guardian communication. The arrow to the guardian is shown after review.
- A permit approval does not itself record departure. Security verification and crossing precede the actual exit record in the film.
- File storage shows school/teacher files, organization, preview, download, versions and scoped access. The displayed sample report names do not imply automatic report generation.
- All people, counts, clocks, school architecture and records are illustrative. No real student data is used.

## Proposed homepage placement

Place one large, user-started player in a «يومك مع نظام الفلاح» section immediately after the opening hero. The story gives context before the feature details. Use a static poster and `preload="none"`, an MP4 mobile source, desktop WebM with MP4 fallback, and native playback controls after activation. The HTML/Three.js composition is an authoring preview; only an encoded film would be delivered to the homepage after approval.

## Source and verification

- `build-story.cjs`, `story-scenes.cjs`, `story-style.css`, `story-motion.js`, `story-world.mjs` produce `alfalah-story/`.
- Shared school geometry is read from `school-world.mjs`; cameras and all choreography are replaced by the story source.
- All assets and fonts are local. The single paused GSAP timeline and Three.js seek handler derive state from absolute time.
- `inspect-story.cjs` records shot/transition stills and tests A → B → A seek equality.
- HyperFrames check, runtime snapshots, encoded-frame review, complete media decode and browser playback checks are saved under `reference/story-*`.
- Everything is isolated under `docs/motion/hyperframes/`.
