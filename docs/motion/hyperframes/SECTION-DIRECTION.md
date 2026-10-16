# نظام الفلاح — سكشن حي يحكي الفائدة

The deliverable is a responsive HTML/CSS/SVG/Three.js section, driven by the deterministic HyperFrames GSAP timeline. Following the user's explicit approval, it is integrated into the actual homepage after the hero and before the vision section. See REVIEW.md for current verification and regeneration instructions.

## Design decision

The animation earns its place by explaining why someone would choose the system: organized records, less administrative effort, student safety, and a connected care process. The chronological school day remains, with fewer words and a faster 47-second cycle. Benefits lead the copy; product features supply the evidence.

Brand tokens: forest `#0b3f29`, secondary forest `#12563a`, white-green `#f8faf8`, ivory `#f5f4ed`, gold `#e0b31b`, gold highlight `#f7d667`. Tajawal remains the only display and text family. Whole Arabic words stay joined. No per-letter DOM splitting.

Native section composition:

```text
full-width background; content max-width 1200px
┌───────────────────────────────────────────────────┐
│    live school / explanatory graphic   benefit copy│
│    with its own camera framing         rotating text│
│                                       drawn marker │
│  pause / replay       chronological scene progress  │
└───────────────────────────────────────────────────┘
```

The page uses the unchanged vision component as a sizing reference. A ResizeObserver copies its actual current height into the animation wrapper. This matches the vision section at every viewport rather than imposing a video aspect ratio. The measured desktop reference is 468.71875px at widths 1440, 1600 and 1914; mobile layouts reflow into a single column.

## Distinctive movement

- The school's gold quill inspires a new vector pen that writes the opening system name. The original school logo is retained intact.
- The logo sits on an extruded enamel-and-gold coin with a complete turn and a timed reflection. The former white construction lines are removed from the markup.
- Selected headline phrases live on real CSS 3D cube faces. A vertical quarter-turn replaces one phrase with the next while preserving Arabic shaping.
- Broad gold SVG marker strokes draw behind key words in sync with their text. They are drawn paths, not a static accent rectangle.
- Other scenes use a word portal, staggered grid reveal, clock iris, document expansion and linked-line reveal. Cube rotation does not become the only transition.
- Perspective and camera framing belong to the visual column; HTML text remains sharp and responsive. The composition is rebuilt for the short section rather than cropping the old video.
- Cube depth offsets neutralize the front plane's perspective magnification, with measured horizontal clearance for both Arabic text faces. The care-to-guardian transition opens from the phone portrait without a giant word zoom. The gate permit belongs to the copy column so it cannot cover the school gate.

## Story and benefits

| Time | School-day moment | Benefit and proof |
| --- | --- | --- |
| 0–4.5 | Opening | System and full school name; organizing the day means more time for the student. |
| 4–8.8 | Staff readiness | Attendance and duty allocation clarify responsibilities. |
| 8.3–13.1 | Student arrival | Arrival/lateness records and a separate absence roster support follow-up. |
| 12.6–17.4 | Morning assembly | Students, teacher and duty staff form an organized school day. |
| 16.9–22.1 | Lessons | A clear schedule and a separate duty plan reduce coordination effort. |
| 21.6–27.55 | Student care | Teacher observation reaches the counselor; guardian communication follows review. |
| 26.6–31.3 | Guardian portal | A real existing feature: children's overview, messages, notifications and requests. |
| 30.8–36.4 | Pickup | Request and approval, recipient verification, crossing and an actual exit record. |
| 35.9–41.5 | Files and reports | Organized permissions and versions; approved visit reports can be archived as PDF when archive integration is configured. |
| 41–47 | Brand promise | Less administrative effort, organized records, student safety and care. |

The prototype is illustrative and does not invent performance percentages, live biometric synchronization, automatic approval, or guaranteed safety. Student and guardian examples are synthetic.

## Review against the brief

The previous proposal was a 16:9 video with explanatory text alongside repeated dioramas. This version uses the actual section height, native HTML typography, an opening derived from the school quill, two distinct audience benefits, and direct evidence for the paperwork promise. The existing school dioramas are retained because the user liked the visual direction, with new framing suited to the shorter stage.

## Web behavior

The story begins when the section is visible, pauses outside the viewport or in a hidden tab, and loops every 47 seconds indefinitely. A native DOM copy of the initial lockup bridges the end back to the same opening pose. Visitors can pause, restart or jump to a scene. Reduced-motion users see the complete static promise at 45.5 seconds by default and can choose to play. The Three.js renderer loads lazily, uses a bounded resolution, and has vector fallback illustrations. The Angular component owns timeline, observers, events, animation frames and GPU resources and releases them on navigation. No video element or encoded media is used in the section.
