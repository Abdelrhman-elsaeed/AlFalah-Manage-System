# Figma Make prompt — Al-Falah School Manager Dashboard

Create a fully designed, interactive, high-fidelity dashboard prototype for the SCHOOL MANAGER of Al-Falah Schools (مدارس الفلاح), a Saudi school management and educational evaluation system.

This is a substantial visual redesign of an existing product. The stakeholder is a Saudi school principal who values achievement, clear control, rich visual presentation, and modern frontend experiences. The dashboard should make him feel: “I can see my school, understand what is happening, and act immediately.” It should create a memorable WOW moment and remain useful throughout a normal working day.

Build the actual interactive interface, not just a written design proposal. Use realistic Arabic content and coherent mock data. The prototype will become the official visual reference for implementation in an existing Angular 17 application with PrimeNG, Chart.js, Lucide/PrimeIcons, and an ASP.NET Core backend. If your prototype uses another framework, keep the visual system and interactions portable to that stack.

1. CREATIVE DIRECTION: SCHOOL PULSE

Design an executive school operations dashboard with a distinctive visual identity. Combine warm editorial surfaces, deep emerald sections, gold highlights, sophisticated charts, and a sculptural visual centerpiece.

The signature hero artwork should be an original “School Pulse” composition inspired by the existing logo’s golden feather, green circular forms, and open book. Imagine a dimensional golden feather rising above layered emerald and frosted surfaces, with four connected visual zones representing students, staff, educational quality, and follow-up. Use rich materials, realistic light, layered depth, soft shadows, and carefully composed perspective.

These zones are functional entry points: selecting a zone highlights its associated metrics and opens a relevant summary or detail drawer. Place readable Arabic labels in the interface beside the artwork, rather than embedding tiny text in a perspective texture. Add a small accessible legend and keyboard-selectable equivalents.

This is an abstract visualization of school operations, not a claim to represent the real school building. A literal 3D school with floors and classrooms is NOT required. Do not invent a floor plan. The purpose is dimensional storytelling with a functional relationship to the data.

Spend visual effort on one exceptional centerpiece, tailored charts, a clear achievements area, and polished interactions. Avoid a generic admin template with dozens of identical statistic cards. Avoid a full-screen decorative intro, gaming aesthetics, neon cyberpunk, constant particles, or decorative graphics that obscure the school’s actual situation.

2. EXACT BRAND IDENTITY

Use these existing application tokens as the source of truth:

Brand green:
- Brand 950: #0B3F29
- Brand 900: #12563A
- Brand 800: #0D6434
- Brand 700: #0F7132
- Brand 600: #168044
- Brand 500: #1E8E4E
- Brand 300: #55B978
- Brand 100: #D8EEE1
- Brand 50: #EDF7F1

Gold:
- Main gold: #E0B31B
- Gold 600: #C99508
- Gold 700: #946800
- Gold 800: #704D00
- Highlight: #F7D667
- Gold surface: rgba(224,179,27,0.13)
- Gold border: rgba(201,149,8,0.46)
- Signature gradient: linear-gradient(135deg,#F7D667 0%,#E0B31B 48%,#C99508 100%)

Surfaces:
- Page background: #F5F4EF
- Main surface: #FFFFFF
- Secondary surface: #F7F9F7
- Border: #E2E5DF
- Strong border: #CBD2C9

Typography colors:
- Strong text: #1D2B23
- Body text: #3F4D45
- Muted text: #67756D
- Dim text: #94A098
- Text on deep green: #FFFFFF

Semantic colors:
- Success: #239A55
- Warning: #E0B31B
- Danger: #D33B3B
- Information: #2676A8

Keep green and gold dominant. Use blue and red for meaning, not as competing brand themes. Use dark gold text on pale gold surfaces where bright gold would have insufficient contrast. Do not use dim text for essential content.

Use the supplied Al-Falah logo without redrawing, distorting, or replacing it. The logo contains a golden feather, green forms, and a red open book. Preserve the logo’s original colors; its red does not need to become a dominant UI color. If the asset is unavailable, reserve a clean logo slot rather than inventing a replacement.

3. ARABIC, RTL, AND TYPOGRAPHY

- The actual UI must be Arabic first, with correct RTL behavior throughout.
- Use Tajawal as the primary font, with Cairo as the fallback.
- Use intentional hierarchy: strong Arabic headings, readable body copy, quiet metadata, and prominent tabular numerals.
- Suggested sizing: page title 28–32px, section headings 20–24px, body 14–16px, key numbers 30–44px.
- Keep the main navigation on the RIGHT. Mirror directional navigation icons where appropriate. Keep dates, times, percentages, and mixed Arabic/Latin content readable using appropriate bidirectional handling.
- Format example dates for a Saudi school using Asia/Riyadh. Allow an optional secondary Hijri date, using proper calendar conversion rather than a manually invented equivalent.
- Use concise Arabic labels such as “نبض المدرسة”, “غياب الموظفين”, “غياب الطلاب”, “آخر الزيارات”, “استدعاءات أولياء الأمور”, “قرارات تحتاج انتباهك”, and “إنجازات المدرسة”.
- Add an English language switch as a secondary control; fully design the Arabic experience first.

4. INFORMATION ARCHITECTURE

Create a complete school-manager dashboard, scoped to ONE school. Do not turn it into a multi-school super-admin dashboard.

Design a 1440px desktop composition, a wider 1920px version, a 1024px tablet adaptation, and a 390px mobile adaptation. Use a consistent grid, spacing tokens, and reusable components. Suggested spacing: 4, 8, 12, 16, 24, and 32px. Use 16–24px radii for prominent redesigned panels and smaller radii for controls. Keep shadows soft and green-tinted.

Organize navigation into:
- Overview / نظرة عامة
- School operations / تشغيل المدرسة
- Student affairs / شؤون الطلاب
- Visits and educational quality / الزيارات والجودة التعليمية
- Staff / الكادر المدرسي
- Improvement plans / خطط التحسين
- Achievement files and evidence / ملفات الإنجاز والشواهد
- School files / مساحة الملفات
- Reports and analysis / التقارير والتحليل
- Settings / الإعدادات

Use collapsible navigation groups and relevant badges. Include the school name, user avatar, role, notifications, search, language control, manual refresh, and a compact export menu with PDF and Excel.

Provide filters for today, this week, this month, and a custom range. Add academic year and semester, with classroom/stage/subject filters inside relevant sections. Clearly distinguish today’s operational metrics from analytics for the selected period. A monthly filter must not silently change the meaning of a “today” metric.

The main overview should answer, in order:
1) What is happening in my school?
2) What requires my attention?
3) What are we achieving?
4) Where can I inspect the details?

Keep secondary detail in tabs, expanders, and drawers. Every major module must be discoverable; every record does not need to be loaded or displayed on the overview.

5. FIRST VIEWPORT

Create a striking but compact hero, approximately 280–340px tall on desktop, with Arabic introduction and the school name on the right and the dimensional School Pulse artwork on the left.

Show a factual one-sentence briefing, for example: “الحضور الطلابي 92.9٪، و5 زيارات بانتظار الاعتماد، و3 استدعاءات مجدولة اليوم.” The briefing must be derived from the displayed dataset. Do not label it AI unless an actual AI feature supports it.

Include a clear snapshot timestamp such as “آخر تحديث 08:45 ص”. A “live” state should only mean data is actually being refreshed. Provide fresh, updating, stale, and unavailable states.

Place a concise strip of six primary indicators beneath the hero:
- Student attendance rate and recording completeness.
- Absent staff today, with unrecorded staff distinguished.
- Absent students today, with excused and unexcused breakdown.
- Visits awaiting the manager’s approval.
- Pending guardian summons, with today’s appointments as a separate subcount.
- Open follow-up items, with explicit categories and no unexplained combined score.

Each indicator must open its relevant filtered detail view. Use category-sensitive visuals and small trend charts only where comparative data exists. Keep the primary indicators and the beginning of the attention area visible within a normal desktop viewport.

6. DECISIONS AND URGENT ATTENTION

Build “قرارات تحتاج انتباهك” as a compact queue ranked by severity and urgency. Use priority, category, short description, count, and one appropriate action.

Include pending visit approvals, unresolved complaints, staff absence requiring timetable attention, repeated student absence alerts, upcoming guardian appointments, and student affairs exceptions where the manager has access.

Distinguish actions the manager can perform from items the manager can only oversee. For example, “مراجعة الزيارة” can open the approval workflow; a restricted student case may only expose an aggregate or permitted summary.

When no urgent items exist, show a polished calm state: “لا توجد قرارات عاجلة حاليًا”. A zero count must look deliberate, not broken.

7. STAFF ATTENDANCE AND ABSENCE

Create “حضور وانصراف الموظفين” with present, absent, excused, and unrecorded counts according to the application’s actual statuses.

The expanded view should include employee name, role, recorded attendance status, date, permitted notes, and a link to the relevant attendance record. Support a date filter and compact period trends.

Connect staff absence to timetable coverage when suitable data is available: affected periods, substitution status, and unresolved coverage. Treat coverage summaries as a proposed data extension if they are not supplied by existing APIs. Do not invent a staff lateness metric if only attendance statuses are available.

8. STUDENT ATTENDANCE AND DAILY DISCIPLINE

Create “حضور الطلاب” with present, unexcused absent, excused absent, and unrecorded states. Display both attendance rate and recording completeness, each with its denominator explained in a tooltip.

Use a classroom attendance heatmap with clear labels and clickable cells. Include a 7-day or 30-day trend, a concise highest-absence classroom list, and repeated-absence alerts. Selecting a classroom opens a filtered student list.

Student detail rows should show name, classroom, current attendance status, repeated-absence count for the stated period, excuse review status, and the permitted follow-up action.

Provide related summary access to morning arrival delays, session delays, pending absence excuses, classroom entry permits, and Zajel import / weekly Noor export workflows when authorized. Keep lateness separate from the attendance denominator unless the data model explicitly combines them.

9. RECENT CLASSROOM VISITS AND EVALUATION

Make “آخر الزيارات” a prominent module. These are instructional observation visits, not building visitor logs.

Show the latest five visits with teacher, evaluator, subject, classroom when available, visit date, visit category, workflow status, published result when available, and “عرض التقرير”. Include a dedicated “بانتظار اعتمادك” filter.

Below or within the quality tab, provide:
- Visits during the selected period.
- Status distribution using the actual workflow labels.
- Teacher visit coverage, with unique teachers visited / active teachers.
- Subject performance comparison.
- Evaluator workload and performance summaries.
- Strongest and weakest evaluation domains or standards where available.
- Teachers needing professional support, with the specific weakest domain or standard explaining the flag.

Use approved/published results for performance analysis. Preserve the backend’s score scale, maximum, and precision; do not mix raw scores and percentages. Do not call the visit approval rate “school quality”. Avoid averaging incompatible legacy and newer evaluation formats without an agreed normalization rule.

10. GUARDIAN SUMMONS AND STUDENT FOLLOW-UP

Create “استدعاءات أولياء الأمور” as a conditional operational module. If there are no active summons, show an elegant compact empty state.

Show pending, attended, under observation, and improved categories using the actual workflow. Present today’s appointments and the next upcoming appointments, with student, classroom, guardian label, appointment time, priority, assigned staff member, and a permitted details action.

Separate appointment attendance/no-show information from the main summon lifecycle. Do not invent lifecycle statuses to represent appointment outcomes.

Add summaries for student referrals, open cases, critical behavior incidents, academic concerns, and positive student recognition. Show aggregate information on the overview and sensitive details only where explicitly authorized.

Important existing-product constraint: summon management currently belongs to the social-worker workflow. The new manager overview may propose authorized summary access, but must not imply that school managers automatically have permission to create, resolve, or manage those summons. Keep new oversight access clearly documented as an implementation requirement.

11. TIMETABLE AND DAILY SCHOOL OPERATIONS

Create a compact “اليوم الدراسي” view with the current period, time remaining calculated from the bell schedule, the next period, breaks, the published timetable state, and substitution history or coverage where available.

Include access to timetable timings, subjects, teaching assignments, teacher settings, review/analysis, substitutions, printing, and timetable settings.

Show unresolved conflicts or affected lessons only when backed by data. Provide “عرض الجدول” and “مراجعة الاحتياطي” actions. The period countdown must run locally from a received schedule, not trigger network requests every second.

12. IMPROVEMENT, ACHIEVEMENTS, AND SCHOOL QUALITY

Design “إنجازات المدرسة” as a visually distinctive gold-accented section with a dimensional achievement emblem, clear progress visualization, and meaningful milestones.

Include completed improvement plans, approved evaluations, documented follow-ups, student recognitions, and improvements supported by comparative data. Show active/completed/cancelled plans, follow-up coverage, and latest recorded progress. Do not relabel a progress score as a percentage unless it actually is one.

If a target exists, show the current value, target, period, and progress. If no configured target exists, show the measured result without inventing a target. Public school awards can appear only when supplied as content.

Include subject/evaluation performance trends and strengths. Keep “teachers needing support” phrased constructively. Give the manager achievement and control without turning professional development into public shaming or arbitrary gamification.

13. EVIDENCE, FILES, COMPLAINTS, AND COMMUNICATION

Provide compact summaries and drill-down access for:
- Teacher achievement files and the evidence tracking matrix.
- Required versus submitted evidence and missing submissions, when requirements are known.
- School file workspace: overview, files, evidence, evaluation readiness, archived visits, and administration.
- Active complaints and their follow-up state, without exposing complaint content to roles that cannot access it.
- Parent surveys: draft/published/closed state and submission counts. Display survey averages using their actual 1–4 scale if a supported aggregate exists; do not fabricate a satisfaction percentage.
- Office hours management and documented exceptions.
- Gate-pass audit and operational exceptions.
- Messaging audit metadata, preserving private message content restrictions.
- Student report analyzer, as a clear entry point to the existing tool.
- Student follow-up records and relevant notification summaries where authorized.
- Staff directory, teachers, supervisors, secretaries, and school user/role management as permitted navigation links.

Give these modules concise entry points under secondary tabs or an organized module directory. Avoid repeating the same metric in multiple oversized cards.

14. CHARTS AND INTERACTION

Use charts that fit their questions:
- Attendance: trend lines and classroom heatmaps.
- Visits: horizontal comparisons and compact workflow distributions.
- Educational quality: domain comparison bars with sample size and score scale.
- Improvement: progress bars and milestone timelines.
- Activity: a chronological feed with category, time, actor, and details.

Provide axes, units, period labels, legends, tooltips, and data/table alternatives. Use consistent semantic colors. Keep actual data charts flat and accurate; reserve dimensional effects for the signature artwork and achievement illustrations.

Make filters, tabs, navigation, chart selections, drawers, search, view-all links, and export interactions work in the prototype. Use a mock export success state if no real export exists, and document that behavior in the handoff. No dead buttons.

Keep scroll position and selected filters when drawers close. Provide visible focus states, Escape-to-close, correct focus restoration, and usable touch interactions. Never make hover the only way to access essential information.

15. MOTION THAT FEELS PREMIUM

Give motion a purpose:
- Initial reveal: 40–70ms stagger, completed within about 700ms.
- Counters: one brief 500–800ms reveal on first load, ending at exact values.
- Hover/focus: subtle lift, lighting change, or up to 2 degrees of perspective tilt.
- Chart transitions: approximately 350–600ms.
- Tabs and drawers: approximately 180–280ms.
- Hero: a slow, restrained light or depth movement, paused off-screen.
- Selecting a School Pulse zone: smoothly highlight the connected interface section.
- Optional achievement celebration only after a deliberate user action or a genuinely new achievement; no repeating confetti.

Use cubic-bezier(.2,.72,.25,1) for standard motion and cubic-bezier(.2,.8,.2,1) for emphasized motion. Respect prefers-reduced-motion, with a complete static experience. Do not replay entrance animations whenever data refreshes. Do not continuously animate numbers, shake alerts, or distract users while they read.

16. SMART DATA LOADING AND PERFORMANCE

This design must anticipate a real system with many modules. Showing the entire school must not require an independent request for every card.

For this prototype, use a centralized mock snapshot and local interactions. Do not connect to production services. Structure data access behind a replaceable adapter.

For the implementation handoff, specify this proposed loading strategy:
- One compact aggregated overview response for critical counts, the briefing, priority summaries, and bounded recent items. This is a proposed backend capability, not an assertion that such an endpoint already exists.
- Server-side aggregate counts and small top-N lists rather than downloading complete records and counting them in the browser.
- Load detailed analytics and lists only when their tab, panel, or drawer is opened. Paginate longer lists.
- Cache by school, permissions, academic period, and filters. Share cached data between widgets, deduplicate in-flight requests, and prevent overlapping refreshes.
- Separate frequent operational summaries from slower analytical summaries. A 60–120 second operational refresh and a 5–15 minute analytical freshness window are proposed starting points, to be tuned after measurement.
- Refresh only when visible and needed. Pause background polling when the browser tab is hidden. Refresh affected data after a successful action.
- Keep previously loaded content visible during refresh. Use local skeletons only for panels that have no data yet.
- A failed secondary panel must not block the entire dashboard. Provide panel-level retry and an honest stale/unavailable state.
- A single HTTP request does not automatically mean cheap database work: document bounded queries, suitable indexes, caching/precomputed aggregates, and query-cost measurement as backend considerations.

Do not expose implementation language such as “cache”, “API”, or “lazy loading” in the manager’s normal UI. Use simple labels such as “جارٍ التحديث”, “آخر تحديث”, and “تعذر تحديث هذا القسم”.

Keep the hero visually rich but technically lightweight. Prefer optimized pre-rendered artwork plus layered SVG/CSS interaction, or one small optimized 3D scene only if necessary. Avoid a separate WebGL canvas in every card. Provide a static fallback, reduce scene complexity on mobile, and defer optional effects until critical content has appeared. Preserve readability even if the visual asset fails.

17. CONSISTENT MOCK DATA AND HONEST STATES

Use one coherent sample school dataset. Example student snapshot:
- 720 active students.
- 658 present.
- 38 absent without an accepted excuse.
- 12 absent with an accepted excuse.
- 12 not yet recorded.
- 708 attendance records: recording completeness 708/720 = 98.3%.
- Attendance rate among recorded students: 658/708 = 92.9%.

Repeat these numbers consistently across the hero, metrics, charts, drawers, and classroom totals. Display student lateness separately. Staff counts must reconcile with staff totals. Every pending-approval count must match the relevant mocked queue.

Distinguish zero, not yet recorded, unavailable, loading, and stale values. Show an em dash and explanation for unavailable data, rather than pretending the value is zero. Do not fabricate a composite school-health score, a percentage trend without historical data, a target, or an AI insight.

Provide designs for:
- A normal operational day.
- A busy day with urgent items.
- No pending summons.
- A school still recording attendance.
- Initial loading.
- Refreshing while keeping existing data.
- Partial data failure and stale data.
- Empty/new-school analytics.
- Reduced-motion mode.

18. RESPONSIVE DESIGN AND HANDOFF

On tablet, collapse navigation and reduce chart columns. On mobile, lead with the briefing, key attendance indicators, and urgent decisions; turn the dimensional hero into a compact optimized visual, use vertically stacked modules, and open detail drawers as full-screen sheets. Keep touch targets at least 44px and avoid horizontal page overflow.

Provide reusable components for the application shell, filters, KPI indicators, priority items, attendance summaries, chart panels, recent visits, summons, achievements, activity rows, and detail drawers. Include component states, color/spacing/type tokens, chart color mappings, motion specifications, and exact Arabic labels. Use editable vector layers and reusable components wherever the environment supports them; clearly identify raster artwork assets separately.

In a separate implementation handoff, distinguish:
1) Existing system capabilities.
2) New aggregate summaries needed for the redesigned dashboard.
3) Permission/access work needed for manager oversight.
4) Mock-only interactions and unavailable data requirements.

Do not place those engineering notes in the manager’s normal experience.

FINAL QUALITY BAR

The result should feel specific to Al-Falah: confident Saudi school leadership, green-and-gold educational identity, strong Arabic typography, memorable dimensional graphics, accurate information, and smooth interactions. Aim for an exceptional first impression and a dashboard that remains fast, legible, and actionable every day.

Start by delivering the complete Arabic desktop overview with its signature hero, primary metrics, attention queue, student attendance, staff absence, recent visits, guardian summons, and achievements. Then complete the secondary tabs, working drill-down interactions, responsive adaptations, and implementation handoff. Do not stop at a beautiful hero with unfinished dashboard content.
