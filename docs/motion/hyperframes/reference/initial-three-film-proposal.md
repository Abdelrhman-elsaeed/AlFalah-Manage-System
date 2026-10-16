# Al-Falah cinematic motion proposal

Prepared 16 October 2026. Status: **rendered previews for approval; no homepage integration authorized or performed**.

Open `review.html` to watch the three films, compare placements, and read the storyboards. All compositions are original HyperFrames/GSAP work. The requested `hyperframes-animation` skill was installed with the supplied `npx skills add` command, using noninteractive Codex/global options. The skill is installed for future turns as well; its instructions were read and applied in this turn. Supporting core, CLI, and keyframe instructions were also read.

## Product and audience

The repository contains an Angular 17 standalone application and an ASP.NET Core API split into Api, Application, Domain, Infrastructure, and Shared projects. The public homepage represents **متوسطة الفلاح الأهلية بمكة المكرمة**, rather than a general SaaS vendor selling subscriptions. There is no pricing, subscription purchase, or public trial journey in the homepage. The public action is signing in to the educational platform. Motion should bridge the school's educational identity and the platform's operational capability.

The review covered the public landing components and their existing motion/video behavior; global design tokens, typography, shared layout, i18n and routes; the feature/service inventory; dashboard and timetable screens and contracts; storage/evidence/readiness contracts; school-duty scope; and repository documentation. This is a product/design analysis, not an exhaustive security or correctness audit of every backend implementation.

| Audience | What they need to understand | Best visual proof |
| --- | --- | --- |
| School manager and delegated administrators | A clear overview, priorities, and access to operational tools | Actual leadership dashboard and decision panels |
| Teachers and educational supervisors | Timetables, visits, evaluations, evidence, and professional improvement | Readable timetable, distinct review and approval stages |
| Student affairs, secretary, security, and social workers | School-day operations with separate responsibilities | Coordinated workflows; no claim that all roles share all access |
| Guardians and prospective families | A trusted school that communicates and operates coherently | School identity remains dominant; calm motion supports the educational message |
| Main management | Visibility across schools and educational performance | Dashboard concept; no invented cross-school metrics |

### Feature map from the repository

- Identity and role-scoped school access; accounts, signatures, users, school roles, permissions, and delegation.
- School leadership dashboards; performance and visit analytics; Excel and PDF exports.
- Teachers, educational supervisors, evaluation rubrics, classroom visits, approval, reports, improvement plans, and follow-ups.
- Teacher evidence files, school file storage, linking/review, readiness, gaps, trackers, reports, digital index, imports, and visit archives. Deployment and provider activation vary; the films demonstrate implemented workflows and make no claim that every rollout gate is complete.
- Timetable management, timings, breaks, subjects, teaching assignments, generation/review, substitutions, versions, and publication. School duty is a separate workspace, with distribution, execution, delegation, and document/history functionality.
- Student affairs: daily presence/absence, morning/session delays, academic concerns, conduct and recognition, guardian communication, summons, social-work referrals, gate passes and controlled exit, oversight, and settings.
- Complaints, parent surveys, school records, achievement/news content, organization/staff presentation, and student report analysis. The films do not add AI claims or imply a blocked integration is live.

## Existing homepage and design system

Current order: sticky header → campus-video hero → vision pillars → school organization → achievements → news → footer. The hero says “نرعى التميّز، ونبني جيلاً يليق بمستقبل الوطن”. Its primary CTA enters the platform. The existing hero has a video/poster fallback, idle loading, reduced-motion and network checks, and visibility-based pause handling. The page also has a branded preloader, reveals, counters, and an organization showcase.

The identity is Arabic-first and genuinely RTL. Tajawal carries application and homepage typography, with Cairo as a fallback. The compact application uses ivory surfaces, green navigation, semantic status colors, thin borders, small radii, and gold emphasis. The logo combines a green mark, a gold quill, and a red book; red remains part of the supplied logo rather than becoming a new animation accent.

| Token | Value | Motion role |
| --- | --- | --- |
| Forest green / `--brand-950` | `#0b3f29` | Main stage and readable dark holds |
| Deep green / `--brand-900` | `#12563a` | Lighting and spatial depth |
| Educational green / `--brand-700` | `#0f7132` | Approved states and UI accents |
| Brand gold / `--gold` | `#e0b31b` | Rim lighting, guides, and emphasis |
| Gold highlight | `#f7d667` | Small light reflections and separators |
| Ivory surface | `#f8faf8` | Product and document surfaces |

### Creative decisions

The central motif is **the school's gold quill and a clear view of school life**. The first film uses the actual `school-pulse.svg` illustration, then reveals the real application. The second translates school-day structure into a spatial timetable. The third makes the distinction between a stored file and an approved piece of evidence explicit.

Composition follows Arabic reading direction: right-aligned headlines and right-to-left evidence flow. Headlines use Tajawal ExtraBold; UI uses Regular/Medium/Bold. Motion uses a finite, paused timeline, perspective transforms, staggered arrivals, camera settling, and long reading holds. Fine gold edges and controlled reflections provide depth; supporting areas stay quiet.

The initial generic direction—an abstract constellation of interchangeable SaaS cards—was rejected during design review. Real school screens, the existing quill, lesson structure, and the evidence approval boundary make this direction specific to Al-Falah. No stock footage, invented awards, fake testimonials, or artificial performance improvements were introduced.

## Films and storyboards

All three films are silent, 16 seconds, 1600×900 at 30 fps in the masters. Silence is intentional for homepage use. The initial and final states match for a calm loop rather than ending on black. Source files use local fonts, GSAP, and brand assets; rendering requires no external asset requests.

### 1. School Pulse — نبض المدرسة

**Placement:** a contained 16:9 product showcase immediately after the existing hero and before the vision strip. The school's current campus hero and login CTA remain the primary opening experience.

**Impact:** connects the recognizable school identity to real product capability. The quill becomes the visual bridge between education and school leadership. A restrained camera reveal gives the dashboard depth, then decision/tool panels draw attention to what the manager can do.

| Time | Shot |
| --- | --- |
| 0–2.4s | Quill illustration and Arabic opening: “كل التفاصيل. رؤية واحدة.” |
| 2.4–5.3s | Typography releases; the real dashboard rises from a shallow 3D angle and a light sweep crosses its surface |
| 5.3–8.5s | Camera settles almost front-on; priority and management panels arrive on separate foreground layers |
| 8.5–11.8s | Stable reading hold: real screen, decisions, tools |
| 11.8–13.8s | Foreground panels leave; dashboard recedes |
| 13.8–16s | Original quill/title composition returns for the loop seam |

The dashboard is an actual browser screenshot from the existing Angular page, with demonstration API responses. The foreground cards are a motion interpretation of existing dashboard functions. Numbers 12 and 3 are labeled synthetic demonstration values, not school results.

### 2. A Day in Harmony — يوم دراسي بتناغم

**Placement:** an optional platform-capabilities section after `#vision` and before `#organization`. This works best if the owner wants the public page to explain the platform to staff as well as introduce the school to families.

**Impact:** turns dense daily operations into a legible spatial composition. A timetable settles toward the viewer, the duty panel arrives alongside it, and the film resolves into the actual timetable screen.

| Time | Shot |
| --- | --- |
| 0–2.7s | Angled lesson grid, right-to-left day structure, coordinated cell reveal |
| 2.7–5.8s | Separate school-duty panel enters; timetable remains readable beside it |
| 6.2–8s | Transition to the real published timetable UI with a controlled reflective sweep |
| 8–11.2s | Readable product hold |
| 11.6–14.4s | Closing statement: “وضوح في كل حصة. تنظيم في كل يوم.” |
| 14.4–16s | Return to the initial timetable pose |

The animated lesson grid is an editorial simplification, not a proposed app redesign. The actual screen follows it. Timetable and duty remain explicitly independent workspaces; the film does not imply automatic synchronization or a shared permission model.

### 3. Evidence to Insight — من الشواهد إلى الرؤية

**Placement:** an optional quality-and-evidence section after `#organization` and before `#achievements`. It gives the public achievements section a grounded introduction: work is documented and reviewed before it informs readiness.

**Impact:** a clear right-to-left chain of paper, human review, and readiness reporting. Stacked sheets, small differences in depth, and staggered progress fills give an administrative workflow a deliberate cinematic rhythm.

| Time | Shot |
| --- | --- |
| 0–2.1s | Evidence document fans into a small paper stack |
| 2.1–5.2s | A guide connects the document to the review panel; linking, review, and approval appear separately |
| 5.2–7.2s | Readiness report arrives and its approved-requirement bars fill |
| 7.2–10.8s | Hold on the complete workflow; synthetic values and the upload/approval distinction are visible |
| 11.3–14.3s | Closing statement: “أثر موثق. رؤية أوضح.” |
| 14.3–16s | Restore the opening document stack and empty downstream states |

This is a stylized explanation of implemented storage/evidence/readiness behavior, not an actual screen recording. Ratios are demonstration values and are not represented as an official school evaluation or a complete reproduction of the production template.

## Recommended placement strategy

Approve **School Pulse** as the primary candidate. The second and third films are optional supporting stories. Putting all three into consecutive autoplay sections would compete with the school's existing media and content. If all are approved, use one primary playing film and lazy, user-controlled secondary showcases. Do not turn them into new loading gates or full-screen introductions.

The current school homepage should remain focused on families and school identity. A future dedicated platform section or platform landing page would be the strongest home for a full three-film product narrative. That broader page change is not part of this delivery.

## Web delivery and proposed integration behavior

- Keep master renders for review. Deliver separate H.264 MP4 and VP9 WebM encodes at 1280×720/30 fps, plus a smaller 640×360 H.264 option and WebP posters. Final measured sizes are in `reference/delivery-manifest.json`.
- Use posters as the initial state, reserve `aspect-ratio:16/9`, and keep meaningful headings/descriptions in HTML beside the videos. The video is supporting product illustration; it must not be the only way to learn a feature.
- Start with `preload="none"`, `muted`, and `playsinline`. Load near the viewport after critical content. Pause offscreen or in a hidden tab, and avoid concurrent playback.
- Respect `prefers-reduced-motion`, Save-Data, and slow connections by keeping a poster and offering deliberate playback. Include a visible pause/play control whenever motion plays automatically.
- Use a dedicated mobile source; preserve the entire composition rather than cropping away Arabic text. Dense UI is illustrative on small screens; adjacent HTML should carry the explanation.
- No animation library, font, render code, or new dependency needs to enter the production Angular bundle. This proposal uses encoded video assets.
- Do not claim homepage performance has been measured: no integration has occurred. File sizes and decoding were checked locally; LCP, real-device playback, and network behavior must be checked after an approved integration.

## Sources and reproduction

### Final verification and measured delivery

All three final compositions passed HyperFrames lint, runtime, layout, and contrast gates. Each retains one advisory about using a nested single-scene structure in Studio; it is not a rendering failure. Twelve delivered video files (three masters, three desktop MP4s, three desktop WebMs, three mobile MP4s) were fully decoded without errors. Decoded master frames were visually reviewed, including the opening, product scene, closing, and loop seam.

| Film | Desktop MP4 | Desktop WebM | Mobile MP4 | WebP poster |
| --- | --- | --- | --- | --- |
| School Pulse | 1.153 MB | 0.886 MB | 0.320 MB | 44 KB |
| A Day in Harmony | 1.378 MB | 1.234 MB | 0.357 MB | 48 KB |
| Evidence to Insight | 0.856 MB | 0.547 MB | 0.248 MB | 43 KB |

All video variants are 16 seconds at 30 fps with no audio stream. A low-resolution first/last-frame comparison measured mean RGB seam differences of 0.197, 0.217, and 0.188 out of 255 respectively. The endpoint compositions match; the small residual includes compression and the final sampled frame occurring 1/30 second before the mathematical endpoint.

The review gallery passed browser playback checks at 1440px desktop and 390px mobile widths, with no horizontal overflow or browser errors. The mobile source selected correctly at 640×360; desktop selected 1280×720. No autoplay was present, including with reduced motion enabled. These are local Chromium checks; production integration, other browsers, and real-device LCP/network performance remain outside this approval-only delivery.

Detailed evidence: `reference/final-verification.json`, `reference/review-browser-check.json`, `reference/delivery-manifest.json`, the three `*-check.json` reports, and decoded proof frames. `git diff` confirmed that frontend and backend application source remain unchanged.

### Reproduction

Primary repository references: `frontend/src/styles/design-tokens.css`, `frontend/src/index.html`, `frontend/src/app/app.routes.ts`, `frontend/src/app/features/landing/**`, `frontend/src/app/features/dashboards/**`, `frontend/src/app/features/timetables/**`, `frontend/src/app/features/storage/**`, `frontend/src/app/core/models/dashboard.models.ts`, `frontend/src/app/core/models/school-manager-operations.models.ts`, backend controller/feature inventory, `README.md`, `CONTEXT.md`, and the homepage/storage/duty specifications.

HyperFrames: https://github.com/heygen-com/hyperframes — CLI version **0.8.163**. GSAP **3.14.2** is pinned and stored locally. Tajawal fonts are from the Google Fonts repository; their license is included with the assets. Existing logo, quill illustration, and screenshot content come from this repository.

Run from this directory:

```powershell
node build-compositions.cjs
$env:HYPERFRAMES_BROWSER_PATH='C:/Program Files/Google/Chrome/Application/chrome.exe'
npx hyperframes@0.8.163 check pulse
npx hyperframes@0.8.163 preview pulse
npx hyperframes@0.8.163 render pulse --quality looks --workers 2 --output renders/01-school-pulse-master.mp4
```

Repeat for `harmony` and `evidence`. `capture-reference.cjs` captures the unchanged local application with intercepted demonstration responses. It never authenticates to or writes to the backend. All new project files are under `docs/motion/hyperframes`; no homepage, application source, deployment configuration, or application dependency was changed.

The user explicitly requested actual preview renders before approval. Those renders are authorized. **Integration is pending explicit approval of the final videos and their placements.**
