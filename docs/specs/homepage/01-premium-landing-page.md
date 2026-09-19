# مواصفة الصفحة الرئيسية المميزة — متوسطة الفلاح الأهلية بمكة

**معرّف الميزة:** `HP-01`  
**الحالة:** مقترح جاهز للمراجعة — لا يبدأ التنفيذ قبل اعتماد العميل  
**التاريخ:** 2026-09-17  
**المالك:** Frontend / UI-UX  
**النطاق التقني:** Angular 17 standalone components + Angular Router + component-scoped CSS + PrimeIcons  
**المرجع البصري:** الواجهة الحالية المرفقة `image_37bf25.jpg`، ومكوّن الإنجازات والأصول الموجودة فعليًا في المشروع  
**لغة واتجاه الواجهة:** العربية أولًا، `lang="ar-SA"` و`dir="rtl"` إلزاميان  
**بوابة البدء:** موافقة صريحة على هذه المواصفة، وبالأخص قرار المسارات في §8 والنصوص المقترحة في §5

---

## 1. الرؤية والنتيجة المطلوبة

تحويل شاشة الدخول الحالية ذات العمودين إلى صفحة استقبال عامة حديثة تمثل هوية **متوسطة الفلاح الأهلية بمكة المكرمة**، وتحقق الآتي:

1. تقدم المدرسة ورؤيتها وإنجازاتها قبل طلب بيانات الدخول.
2. تنقل نموذج الدخول كاملًا من المشهد الأول إلى مساره المستقل الحالي.
3. تجعل **تسجيل الدخول** إجراءً أساسيًا واضحًا في شريط تنقل علوي ثابت.
4. تعيد تقديم أرقام الإنجازات وقصص التكريم الحالية ضمن قسم واسع ومقروء وقابل للوصول.
5. تحافظ بدقة على هوية الأخضر الزمردي والذهبي والأبيض، مع عمق بصري حديث دون إضافة ألوان دخيلة.
6. تعمل بسلاسة من هاتف بعرض 360px إلى شاشة مكتبية عريضة، وباتجاه RTL حقيقي لا شكلي.

هذه الميزة واجهة عامة فقط؛ لا تغيّر المصادقة أو الصلاحيات أو عقود الـ API أو منطق إعادة التوجيه بعد الدخول.

## 2. ما تم التحقق منه في النظام الحالي

- التطبيق Angular 17 ويستخدم standalone components وlazy route loading.
- المسار `/` يعيد التوجيه حاليًا إلى `/auth/school-login`.
- نموذج دخول المدرسة موجود في `features/auth/school-login` ويحتفظ داخليًا بحالات المدارس والتحميل والخطأ وإعادة التوجيه حسب الدور.
- شاشة الدخول تستورد `AchievementsShowcaseComponent` وتعرضه بجانب النموذج.
- مكوّن الإنجازات الحالي يحتوي البيانات والأصول المطلوبة بالفعل:
  - `317` طالبًا بالمدرسة.
  - `+1,970` مشاركة طلابية.
  - `19` مشروع إبداع.
  - `26` ميدالية متنوعة.
  - `9` مراكز متقدمة.
  - ثلاث قصص إنجاز، منها تكريم مدير عام التعليم وأولمبياد نسمو والشراكة مع هيئة تقويم التعليم والتدريب.
- الصور الحالية محفوظة تحت `frontend/src/assets/achievements/`، والشعار تحت `frontend/src/assets/Logo.png`؛ لا تعتمد الخطة على أصل خارجي جديد.
- التصميم العام يملك tokens جاهزة للأخضر (`--brand-*`) والذهبي (`--gold*`) والظلال والمسافات والخط (`--font-app`).
- خطا Tajawal وCairo محمّلان بالفعل في `src/index.html`.
- Tailwind متاح، لكن نمط المشروع الفعلي يعتمد design tokens وCSS مخصصًا لكل مكوّن، و`preflight` معطل.
- العنصر الجذري يضبط `dir="rtl"` و`lang="ar-SA"`، لكن الصفحة الجديدة ستصرح أيضًا بالاتجاه على حاويتها لحماية العزل والاختبارات.

## 3. النطاق

### 3.1 داخل النطاق

- صفحة عامة جديدة على `/`.
- شريط علوي sticky بزجاجية خفيفة، شعار، روابط داخل الصفحة، وزر **تسجيل الدخول**.
- Hero عربي مميز بخلفية هندسية إسلامية مجردة وتدرجات من لوحة المشروع نفسها.
- قسم الإنجازات: أرقام إحصائية + معرض/slider قصص التكريم + النوافذ التفصيلية الحالية بعد تحسينها.
- تذييل يحوي تعريف المدرسة، روابط الصفحة، رابط الدخول، وبيانات الموقع العامة.
- نقل عرض نموذج الدخول بصريًا إلى `/auth/school-login` مع الإبقاء على منطقه الحالي.
- حركة دخول تدريجية، micro-interactions، وحالات reduced-motion.
- نصوص عربية/إنجليزية متطابقة داخل ملفات i18n، مع كون العربية هي اللغة الظاهرة افتراضيًا.
- اختبارات route/component/accessibility الأساسية، ثم تحقق بصري حقيقي بأحجام الشاشات المحددة.

### 3.2 خارج النطاق

- أي تغيير backend أو قاعدة بيانات أو endpoint.
- نظام CMS أو لوحة إدارة محتوى الإنجازات.
- جلب الإحصاءات أو الأخبار ديناميكيًا من API في هذه النسخة.
- إنشاء شعار جديد أو تعديل الهوية المؤسسية.
- إضافة ألوان أساسية جديدة، أو الوضع الداكن، أو تبديل الثيم.
- إعادة تصميم لوحة النظام الداخلية أو الـ shell بعد تسجيل الدخول.
- تغيير بيانات الدخول أو قواعد التحقق أو منطق التوجيه حسب الدور.
- حذف مسار مدير النظام العام `/auth/main-manager-login` أو دمجه مع دخول المدرسة.

## 4. سيناريوهات المستخدم — Spec Kit User Stories

### US-1 — اكتشاف المدرسة والانتقال للدخول (P1)

**كمستخدم للمنصة** أريد فهم هوية المدرسة سريعًا ثم الوصول لتسجيل الدخول، حتى لا يبدأ انطباعي الأول بنموذج إداري جاف.

**اختبار مستقل:** افتح `/` في جلسة غير مسجلة؛ يجب أن تظهر العلامة والرسالة الأساسية وزر الدخول في أول viewport، وأن ينقل الزر إلى `/auth/school-login`.

**معايير القبول:**

1. عند فتح `/` تظهر الصفحة الرئيسية ولا يحدث redirect تلقائي للدخول.
2. يظهر زر **تسجيل الدخول** في الشريط العلوي على سطح المكتب وداخل قائمة الهاتف.
3. تفعيل الزر بالماوس أو Enter ينقل إلى `/auth/school-login` دون إعادة تحميل كامل للتطبيق.
4. زر الـ CTA في Hero ينقل بسلاسة إلى قسم الإنجازات، لا إلى نموذج مخفي داخل الصفحة.

### US-2 — استعراض الإنجازات (P1)

**كزائر أو ولي أمر** أريد رؤية أرقام الإنجاز وقصص التكريم بوضوح، حتى أتعرف على أثر المدرسة ومكانتها.

**اختبار مستقل:** انتقل إلى `#achievements`؛ يجب أن تظهر الإحصاءات الخمس والقصة النشطة، مع إمكانية التنقل وفتح التفاصيل بلوحة المفاتيح.

**معايير القبول:**

1. تظهر القيم الخمس والتسميات العربية نفسها دون تغيير دلالي.
2. يعمل السابق/التالي والنقاط، ويتوقف التشغيل التلقائي عند hover أو focus أو فتح dialog.
3. يعمل Enter/Space على بطاقة إحصاء أو قصة قابلة للنقر.
4. dialog يملك عنوانًا مرتبطًا، focus trap، إغلاقًا بـ Escape، واستعادة focus إلى العنصر الذي فتحه.
5. لا تكون الألوان الوسيلة الوحيدة للتعبير عن الحالة أو التحكم.

### US-3 — استخدام الهاتف (P1)

**كمستخدم على الهاتف** أريد صفحة سريعة بلا قص أو تمرير أفقي، حتى أستطيع قراءة المحتوى والدخول بسهولة.

**اختبار مستقل:** اعرض الصفحة عند 360×800 و390×844؛ يجب ألا يتجاوز أي عنصر عرض الشاشة، وأن تبقى أهداف اللمس واضحة.

**معايير القبول:**

1. يتحول التنقل المركزي إلى زر قائمة معلّم نصيًا وسمعيًا.
2. تغلق القائمة بعد اختيار رابط، وعند Escape، وعند الانتقال لمسار آخر.
3. يصبح Hero عمودًا واحدًا ويظل العنوان والزر الأساسي ظاهرين دون ازدحام.
4. تتحول بطاقات الإحصاء إلى شبكة `2 + 2 + 1` أو مسار أفقي مضبوط، من دون تصغير النص إلى حجم غير مقروء.
5. لا يوجد horizontal overflow على الصفحة أو داخل dialogs.

### US-4 — الحركة المخففة وإتاحة لوحة المفاتيح (P2)

**كمستخدم يفضل تقليل الحركة أو يعتمد لوحة المفاتيح** أريد تجربة هادئة وقابلة للتوقع.

**اختبار مستقل:** فعّل `prefers-reduced-motion: reduce` وتنقل باستخدام Tab فقط.

**معايير القبول:**

1. تتوقف العدادات المتحركة، auto-slide، parallax، والانتقالات غير الضرورية أو تتحول إلى ظهور فوري.
2. ترتيب focus يطابق ترتيب القراءة RTL والمنطق البصري.
3. لكل رابط وزر focus ring واضح بنسبة تباين مناسبة.
4. رابط **تجاوز إلى المحتوى** هو أول عنصر focusable.

## 5. بنية الصفحة وتجربة المحتوى

### 5.1 Header / Navbar

ترتيب RTL المرئي:

- **اليمين:** شعار المدرسة داخل حاوية بيضاء رقيقة + النص المختصر **متوسطة الفلاح الأهلية**.
- **الوسط:** روابط anchor: **الرئيسية**، **رؤيتنا**، **منجزاتنا**، **تواصل معنا**.
- **اليسار:** زر ذهبي/أخضر بارز **تسجيل الدخول** مع `pi-sign-in`.

السلوك:

- `position: sticky; inset-block-start: 0;` مع طبقة زجاجية (`backdrop-filter`) وحد سفلي ذهبي شفاف.
- يبدأ فوق Hero بخلفية أهدأ، ثم يكتسب سطحًا أكثر وضوحًا وظلًا صغيرًا بعد التمرير.
- anchor النشط يتحدد عبر `IntersectionObserver`، مع underline ذهبي لا يعتمد على اللون وحده.
- ارتفاع هدف النقر لا يقل عن 44px.

### 5.2 Hero

**النص المقترح للاعتماد:**

- eyebrow: **تعليم راسخ برؤية تصنع المستقبل**
- العنوان: **نرعى التميّز، ونبني جيلاً يليق بمستقبل الوطن**
- الوصف: **في متوسطة الفلاح الأهلية بمكة المكرمة نصنع بيئة تعليمية تجمع القيم، والمعرفة، والابتكار؛ ليحقق كل طالب أفضل إمكاناته.**
- CTA أساسي: **استكشف منجزاتنا** → `#achievements`
- CTA ثانوي: **الدخول إلى المنصة** → `/auth/school-login`

التكوين البصري:

- grid متوازن: محتوى نصي في بداية القراءة RTL، وتكوين هوية بصري في الجانب المقابل.
- الشعار داخل medallion زجاجي، مع حلقات ذهبية هادئة وبطاقات مصغرة للأرقام الأهم، من دون تكرار slider كامل داخل Hero.
- خلفية `linear/radial-gradient` بدرجات `--brand-950` إلى `--brand-700` فقط، مع pattern هندسي إسلامي SVG/CSS منخفض التباين.
- خط ذهبي رفيع وتوهجات خفيفة؛ لا neon قوي ولا glass شديد يضعف القراءة.

### 5.3 شريط الثقة/الرؤية

قسم قصير أبيض أو `--brand-50` بين Hero والإنجازات، يثبت ثلاثة مرتكزات:

- **قيم أصيلة**
- **تعليم نوعي**
- **طموح وطني**

هذا القسم يحقق رابط **رؤيتنا** دون إضافة صفحة أو محتوى تسويقي طويل.

### 5.4 Achievements

- عنوان مستقل ومساحة بيضاء واسعة بدل حشر المحتوى بجانب نموذج الدخول.
- شريط الإحصاءات الخمس في بطاقات متساوية، مع icon دائري ذهبي/أخضر ورقم tabular واضح.
- الحفاظ على `AchievementsShowcaseComponent` كقاعدة، مع جعله presentation component قابلًا لاستقبال البيانات أو الاحتفاظ ببياناته الافتراضية مؤقتًا.
- slider بصورة بنسبة أبعاد ثابتة، overlay متدرج، badge، تاريخ، عنوان، وصف مختصر، وCTA تفصيلي.
- desktop: الصورة والنص داخل بطاقة سينمائية واسعة؛ tablet/mobile: نسبة صورة أقصر ونص متدفق أسفلها كي لا يغطي النص الوجوه.
- العدادات تبدأ مرة واحدة فقط عندما يدخل القسم viewport، لا فور boot إذا كان القسم خارج المشهد.
- الصور غير الأولى `loading="lazy"` و`decoding="async"`، مع أبعاد/aspect-ratio محجوزة لمنع layout shift.

### 5.5 Footer

أربعة أجزاء منطقية قابلة للانهيار إلى عمود واحد:

1. الشعار + اسم المدرسة + وصف قصير.
2. روابط الصفحة الداخلية.
3. روابط المنصة: **تسجيل الدخول** و**دخول المدير العام**.
4. الموقع: **مكة المكرمة، المملكة العربية السعودية** وحقوق النشر.

الخلفية `--brand-950`، النص أبيض/أبيض شفاف، والذهب يستخدم للعناوين والخطوط الدقيقة فقط.

## 6. هيكل المكوّنات واستراتيجية التخطيط

```text
LandingPageComponent                         route: /
├── LandingHeaderComponent                  sticky nav + mobile menu
├── main
│   ├── LandingHeroComponent                headline + CTAs + brand visual
│   ├── VisionStripComponent                three value pillars
│   └── AchievementsSectionComponent
│       └── AchievementsShowcaseComponent   stats + slider + dialogs
└── LandingFooterComponent

SchoolLoginComponent                        route: /auth/school-login
└── Login form/brand only; no achievements column
```

### مسؤوليات المكوّنات

| المكوّن | المسؤولية | الحالة المحلية |
|---|---|---|
| `LandingPageComponent` | shell العام، landmarks، ترتيب الأقسام، SEO title | لا توجد حالة business |
| `LandingHeaderComponent` | روابط الصفحة، mobile menu، حالة scrolled/active section | Angular signals: `menuOpen`, `isScrolled`, `activeSection` |
| `LandingHeroComponent` | الرسالة والـ CTA والتكوين البصري | بلا state أو state عرضي فقط |
| `VisionStripComponent` | مرتكزات الرؤية الثلاثة | بيانات ثابتة/i18n |
| `AchievementsSectionComponent` | غلاف القسم والعنوان وتسليم البيانات | reveal/counter visibility فقط |
| `AchievementsShowcaseComponent` | slider، الأرقام، dialogs، keyboard behavior | signals الحالية بعد ضبطها للإتاحة |
| `LandingFooterComponent` | معلومات المدرسة والروابط | السنة المحسوبة فقط |

يمنع وضع الصفحة كلها في component واحد؛ الفصل أعلاه يبقي الوحدات عميقة وواضحة، من دون تفتيت كل عنصر زخرفي إلى مكوّن مستقل.

### استراتيجية التخطيط

- container موحد: `width: min(100% - 2rem, 1200px)` مع توسيع gap تدريجيًا بـ `clamp()`.
- CSS Grid للأقسام الثنائية وشبكة الإحصاءات، وFlex للـ navbar ومجموعات الأزرار.
- استخدام logical properties فقط: `margin-inline`, `padding-inline`, `inset-inline-*`, `border-inline-*`؛ لا `left/right` إلا عند معالجة أصل بصري لا يحمل معنى اتجاهيًا.
- breakpoints سلوكية وليست خاصة بجهاز: قرابة `1100px`, `768px`, `480px`.
- `min-width: 0` لكل grid/flex child يحمل نصًا أو صورة لمنع overflow العربي.

## 7. استراتيجية CSS والمؤثرات البصرية

### 7.1 النهج المعتمد

استخدام **component-scoped CSS + design tokens الحالية** هو الخيار الأساسي، وليس Tailwind utility-heavy، للأسباب الآتية:

- يطابق بنية المشروع الحالية ويمنع ازدواج مصادر الحقيقة.
- يسمح بضبط glassmorphism والـ pattern والـ responsive states بدقة.
- يقلل HTML noise ويحافظ على budgets الخاصة بـ Angular component styles.
- يعيد استخدام `--brand-*`, `--gold*`, `--shadow-*`, `--radius-*`, `--font-app` بدل ألوان عشوائية.

تضاف tokens عامة فقط إذا كانت قيمة مشتركة فعلًا، مثل:

- `--landing-header-height`
- `--landing-container`
- `--landing-glass-bg`
- `--landing-section-space`

### 7.2 أسماء classes المقترحة

- الصفحة: `.landing-page`, `.landing-container`, `.landing-section`
- الشريط: `.landing-header`, `.landing-header--scrolled`, `.landing-nav`, `.landing-nav__link`, `.landing-nav__login`, `.landing-nav__toggle`
- Hero: `.landing-hero`, `.landing-hero__content`, `.landing-hero__eyebrow`, `.landing-hero__title`, `.landing-hero__actions`, `.landing-hero__visual`, `.landing-hero__pattern`
- الرؤية: `.vision-strip`, `.vision-pillar`
- الإنجازات: `.achievements-section`, `.achievement-stat`, `.achievement-story`
- التذييل: `.landing-footer`, `.landing-footer__grid`, `.landing-footer__links`
- الحركة: `[data-reveal]`, `.is-revealed`

### 7.3 تفاصيل الإبهار البصري المنضبط

- glassmorphism: `background` شبه شفاف + `backdrop-filter: blur(...) saturate(...)` مع fallback صلب عند عدم الدعم.
- الظلال: طبقتان خفيفتان من الأخضر الداكن/الذهبي الشفاف، لا ظلال سوداء ثقيلة.
- hover: انتقال `transform: translateY(-2px)` ورفع shadow فقط للعناصر التفاعلية.
- الأزرار: gradient أخضر دقيق أو سطح ذهبي محسوب، مع contrast موثق في hover/focus/disabled.
- pattern: SVG inline منخفض الكلفة أو pseudo-elements، لا صورة tiled كبيرة ولا animation مستمر مشتت.
- scroll reveal: `IntersectionObserver` يضيف `.is-revealed`; الحركة تعتمد `opacity/transform` فقط.
- micro-interactions: لمعان زر خافت مرة واحدة، نبضة محدودة حول الشعار، وprogress indicator للـ slider.
- `@media (prefers-reduced-motion: reduce)` يلغي smooth scroll وauto-play والعدادات والحركات الانتقالية.

### 7.4 الخط والتدرج الهرمي

- `Tajawal` هو الخط الأساسي الحالي، و`Cairo` fallback.
- Hero H1: `clamp(2.1rem, 5vw, 4.5rem)`, وزن 800، line-height عربي مريح.
- Section H2: `clamp(1.7rem, 3vw, 2.6rem)`, وزن 800.
- body: 1rem–1.125rem، وزن 400/500.
- الأرقام: وزن 800 مع `font-variant-numeric: tabular-nums`.
- لا يستخدم uppercase/letter-spacing الغربي على النص العربي.

## 8. استراتيجية المسارات وإدارة الحالة

### 8.1 القرار المقترح

| المسار | السلوك بعد التنفيذ |
|---|---|
| `/` | lazy-load لـ `LandingPageComponent`، عام بلا guard |
| `/auth/school-login` | يبقى صفحة دخول المدرسة المستقلة، عامة بلا guard |
| `/auth/main-manager-login` | يبقى كما هو، عامة بلا guard |
| المسارات الداخلية | تبقى خلف `authGuard` بلا تغيير |
| `**` | لا يعاد توجيهه إلى صفحة الدخول تلقائيًا؛ المقترح redirect إلى `/` ما لم يعتمد لاحقًا Not Found مستقلة |

زر **تسجيل الدخول** يستخدم `routerLink="/auth/school-login"`. لن نستخدم modal في النسخة الأولى للأسباب التالية:

- نموذج الدخول يحمل school lookup وحالات loading/error/validation، ويستحق URL واضحًا.
- الصفحة المستقلة أفضل للـ deep linking وسجل المتصفح ومديري كلمات المرور وautocomplete.
- لا نكرر form state داخل landing page ولا نخاطر بفقده عند إغلاق modal.
- يبقى فصل العرض العام عن المصادقة واضحًا وقابلًا للاختبار.

### 8.2 إدارة الحالة

- لا تضاف NgRx أو مكتبة state جديدة.
- signals محلية لحالات presentation في header وslider/dialogs.
- `AuthService` وReactive Forms الحاليان يبقيان المصدر الوحيد لحالة الدخول.
- `IntersectionObserver` service/directive صغيرة قابلة لإعادة الاستخدام ممكنة فقط إن تكرر reveal في ثلاثة مكونات أو أكثر؛ وإلا تبقى داخل الصفحة مع cleanup صريح في `ngOnDestroy`.
- أي timer في slider يُلغى عند destroy، وعند إخفاء tab (`visibilitychange`) لتقليل العمل غير الضروري.

### 8.3 انتقال نموذج الدخول

1. يحذف `AchievementsShowcaseComponent` من imports/template الخاص بـ `SchoolLoginComponent`.
2. تتحول صفحة الدخول إلى card مركزي أو layout علامة + form مضغوط، مع الحفاظ على جميع controls والرسائل والمنطق الحالي verbatim.
3. لا تتغير دالة `redirectByRole` ولا endpoints ولا validators.
4. يضاف رابط واضح للعودة إلى الصفحة الرئيسية.
5. يبقى رابط **دخول مدير المدارس العام** كما هو.

## 9. المتطلبات الوظيفية

- **FR-001:** يجب أن يعرض `/` الصفحة الرئيسية العامة دون مصادقة.
- **FR-002:** يجب أن يحتوي الشريط العلوي على الشعار والروابط وزر **تسجيل الدخول**.
- **FR-003:** يجب أن ينقل زر الدخول إلى `/auth/school-login` عبر Angular Router.
- **FR-004:** يجب ألا يظهر نموذج username/password/school داخل الصفحة الرئيسية.
- **FR-005:** يجب أن يحتوي Hero على عنوان ورسالة وCTA ظاهرين في أول viewport على desktop الشائع.
- **FR-006:** يجب أن يعمل كل nav link كـ anchor إلى section موجود ذي `scroll-margin-block-start` مناسب للشريط الثابت.
- **FR-007:** يجب عرض الإحصاءات الخمس بالقيم الحالية والتسميات العربية المعتمدة.
- **FR-008:** يجب عرض قصص الإنجاز الثلاث الحالية وصورها دون كسر gallery/modal behavior.
- **FR-009:** يجب أن يوفر slider تنقلًا يدويًا ولوحة مفاتيح وإيقافًا تلقائيًا عند التفاعل.
- **FR-010:** يجب أن يحتوي footer على اسم المدرسة ومكة وروابط الصفحة والدخول.
- **FR-011:** يجب أن تبقى صفحتا دخول المدرسة والمدير العام قابلتين للوصول مباشرة.
- **FR-012:** يجب نقل أي نص جديد إلى `ar.json` و`en.json` مع parity وعدم تكرار namespaces.
- **FR-013:** يجب الحفاظ على `dir="rtl"` على الصفحة وكل dialogs.
- **FR-014:** يجب أن تغلق قائمة الهاتف وdialogs بـ Escape وتستعيد focus بصورة صحيحة.
- **FR-015:** يجب ألا يؤدي أي تفاعل presentation إلى طلب backend جديد باستثناء school lookup عند فتح صفحة الدخول نفسها.

## 10. المتطلبات غير الوظيفية

- **NFR-001 — Accessibility:** استهداف WCAG 2.2 AA للتباين، focus، semantic landmarks، labels، keyboard، و44×44px touch targets.
- **NFR-002 — Performance:** لا مكتبة animation أو carousel جديدة؛ JavaScript العرضي محدود، ولا asset جديد كبير دون تبرير.
- **NFR-003 — Stability:** حجز أبعاد الصور لمنع CLS؛ Hero لا ينتظر API كي يرسم.
- **NFR-004 — RTL:** لا physical spacing properties في ملفات landing الجديدة إلا باستثناء موثق.
- **NFR-005 — Responsive:** لا horizontal page scroll عند 360، 390، 768، 1024، 1440px.
- **NFR-006 — Motion:** كل حركة اختيارية قابلة للإلغاء عبر `prefers-reduced-motion`.
- **NFR-007 — Browser:** دعم النسخ الحديثة من Chrome/Edge/Safari/Firefox؛ glass effect له fallback واضح.
- **NFR-008 — Maintainability:** لا inline styles للتصميم، لا hex colors جديدة خارج tokens إلا داخل SVG pattern المشتق من tokens/موثق.
- **NFR-009 — SEO basics:** عنوان صفحة عربي ووصف meta مناسب؛ H1 واحد فقط وتسلسل headings صحيح.
- **NFR-010 — Build:** يجب أن ينجح production build وألا يتجاوز component style budget؛ إذا اقترب CSS من الحد يقسم حسب المكوّنات أعلاه.

## 11. البيانات والمحتوى

لا يوجد domain model أو API جديد. في الإصدار الأول:

- تبقى stats/stories بيانات presentation محلية typed كما هي حاليًا.
- يمكن نقلها إلى `landing-content.ts` أو inputs ثابتة إذا أدى ذلك إلى فصل المحتوى عن سلوك slider بوضوح.
- لا تُخزن HTML strings؛ النصوص plain text وتعرض Angular escaped.
- أي أرقام أو عبارات جديدة غير موجودة في المرجع تحتاج اعتماد العميل قبل التنفيذ.
- النصوص المقترحة في §5 ليست ادعاءات رقمية جديدة؛ الأرقام الخمس فقط هي المنقولة من الواجهة الحالية.

## 12. الحالات الحدّية

- فشل تحميل صورة قصة: يظهر surface أخضر يحمل الشعار/أيقونة وصياغة alt سليمة، ولا تنهار أبعاد البطاقة.
- JavaScript بطيء: المحتوى الأساسي والعنوان وروابط الدخول تظل ظاهرة؛ reveal لا يترك العناصر `opacity: 0` دائمًا.
- `backdrop-filter` غير مدعوم: navbar يستخدم خلفية `--brand-950` شبه صلبة.
- نص عربي أطول بسبب i18n: الأزرار تسمح بالتمدد، والعناوين لا تقطع إلا الملخصات المصرح بها.
- فتح route fragment مباشرة مثل `/#achievements`: بعد الاستقرار ينتقل للقسم مع offset الشريط.
- تكرار الضغط على slider controls: لا تنشأ timers متعددة.
- تغيير tab أو reduced-motion أثناء التشغيل: يتوقف auto-play بأمان.
- شاشة قصيرة landscape: قائمة الهاتف قابلة للتمرير ولا تحجب زر الدخول.
- failure في school lookup لا يؤثر على landing page؛ يظهر فقط داخل route الدخول وفق السلوك الحالي.

## 13. خطة التنفيذ المرحلية

### المرحلة 0 — اعتماد المواصفة والـ copy

- اعتماد مسار `/` العام والاحتفاظ بصفحة دخول مستقلة.
- اعتماد نصوص Hero وروابط navbar/footer.
- تأكيد أن الأرقام والقصص الحالية هي المصدر المعتمد لهذه النسخة.

**بوابة الخروج:** لا يوجد نص أو قرار route أو نطاق غامض.

### المرحلة 1 — Foundation + Routing

- إنشاء feature folder ومكوّن الصفحة والحاويات الأساسية.
- تعديل routes: `/` landing وwildcard إلى `/` مع إبقاء auth routes.
- إضافة مفاتيح i18n المتطابقة.
- إضافة tokens landing المشتركة الضرورية فقط.

**بوابة الخروج:** `/` و`/auth/school-login` يعملان مباشرة، وguards الداخلية بلا تغيير.

### المرحلة 2 — Header + Hero + Vision

- تنفيذ الشريط sticky وحالتي desktop/mobile.
- تنفيذ Hero والخلفية الهندسية والـ CTAs.
- تنفيذ anchors وactive section وskip link.
- تطبيق reduced-motion من البداية.

**بوابة الخروج:** أول fold مكتمل ومتجاوب وقابل للوحة المفاتيح.

### المرحلة 3 — Achievements refactor

- نقل عرض `AchievementsShowcaseComponent` من صفحة الدخول إلى قسم landing.
- تحسين stat grid وslider/dialogs وإدارة focus والتشغيل التلقائي.
- تشغيل العدادات عند دخول viewport مرة واحدة.
- ضبط الصور للـ responsive/lazy loading ومنع layout shift.

**بوابة الخروج:** القيم والقصص الحالية تعمل على desktop/mobile مع keyboard.

### المرحلة 4 — Login simplification + Footer

- تبسيط layout صفحة الدخول دون تغيير Reactive Form أو AuthService.
- إضافة العودة للرئيسية والإبقاء على دخول المدير العام.
- تنفيذ footer وروابطه.

**بوابة الخروج:** لا يظهر نموذج الدخول على `/`، ومسار الدخول يحتفظ بكل السلوك السابق.

### المرحلة 5 — Verification + Polish

- unit/component tests والمسارات.
- فحص آلي/static لـ RTL والـ overflow والـ i18n parity.
- screenshots فعلية للأحجام 1440×900، 1024×768، 768×1024، 390×844، 360×800.
- keyboard-only وreduced-motion وفشل الصور.
- production build وفحص budgets.

**بوابة الخروج:** جميع معايير §15 و§16 ناجحة، وتوثق النتائج في spec kit قبل الإغلاق.

## 14. خطة الاختبارات

### 14.1 Unit / Component

- `LandingHeaderComponent`: فتح/إغلاق قائمة الهاتف، Escape، الرابط النشط، scrolled state cleanup.
- `AchievementsShowcaseComponent`: next/previous/wrap، pause/resume، timer cleanup، reduced-motion، dialog open/close/focus restore.
- `SchoolLoginComponent`: regression للاستمارة، school loading، validation، success/error، redirect حسب الدور.
- routes: `/` يحمل landing، auth routes عامة، والمسار المحمي ما زال يمر عبر `authGuard`.

### 14.2 Accessibility

- landmarks: header/nav/main/section/footer.
- H1 واحد وتسلسل headings.
- tab order، visible focus، skip link، mobile menu semantics.
- dialog name/description/focus trap/restore.
- فحص contrast للنصوص والأزرار فوق الأخضر والتدرجات والصور.
- التأكد أن animation لا تمنع القراءة مع reduced motion.

### 14.3 Visual / Responsive

| الحجم | المطلوب فحصه |
|---|---|
| 1440×900 | first fold، توازن Hero، navbar كامل، عرض slider |
| 1024×768 | انتقال grid، عدم تزاحم links/cards |
| 768×1024 | tablet nav، stat grid، dialogs |
| 390×844 | قائمة الهاتف، CTA، 2+2+1 stats، footer |
| 360×800 | أصغر عرض مستهدف، عدم overflow، touch targets |

### 14.4 Performance / Build

```bash
cd frontend
npm test -- --watch=false
npm run build
```

إضافة فحص يدوي/آلي لـ:

- عدم وجود صور بلا `width/height` أو `aspect-ratio`.
- عدم وجود timers/observers بعد destroy.
- عدم وجود مكتبات runtime جديدة.
- عدم وجود أخطاء console أو طلبات أصول 404.
- عدم تجاوز budgets الحالية.

## 15. معايير القبول النهائية

1. الصفحة الرئيسية العامة تظهر على `/`، وLogin منفصل على `/auth/school-login`.
2. الأقسام الأربعة المطلوبة موجودة: Navbar، Hero، Achievements، Footer.
3. اللون الأساسي أخضر زمردي مع ذهب وأبيض فقط، مع درجات/شفافية مشتقة من tokens الحالية.
4. الصفحة RTL كاملة؛ الترتيب، الأسهم، المسافات، وحركة slider منطقية للعربية.
5. القيم `317`, `+1,970`, `19`, `26`, `9` ظاهرة بدقة.
6. قصص وصور الإنجازات الحالية محفوظة وقابلة للاستعراض.
7. نموذج الدخول غير موجود في main view ولا يوجد duplicate form state.
8. جميع روابط الدخول تعمل، بما فيها مدير المدارس العام من الموضع المناسب.
9. keyboard وEscape وfocus وreduced-motion تعمل وفق المتطلبات.
10. لا horizontal overflow في المقاسات المحددة.
11. production build والاختبارات ناجحة، وi18n parity محفوظة.
12. لا تغيير backend/database/auth contract ولا regression في redirects حسب الدور.

## 16. مقاييس نجاح قابلة للقياس

- **SC-001:** يصل 100% من مختبري السيناريو إلى صفحة الدخول من الـ navbar خلال تفاعل واحد.
- **SC-002:** تظهر هوية المدرسة، H1، وCTA الأساسي في أول viewport عند 1440×900 و390×844.
- **SC-003:** تنجح سيناريوهات keyboard الأساسية دون استخدام pointer.
- **SC-004:** صفر overflow أفقي في أحجام الاختبار الخمسة.
- **SC-005:** صفر console errors وasset 404s في `/` و`/auth/school-login`.
- **SC-006:** CLS بصري غير ملحوظ بسبب حجز أبعاد الصور؛ الهدف الهندسي `< 0.1` عند قياس Lighthouse في بيئة مستقرة.
- **SC-007:** لا تضاف dependency frontend جديدة لتنفيذ الحركة أو slider.
- **SC-008:** تتوقف الحركة التلقائية بالكامل عند تفضيل تقليل الحركة.

## 17. الملفات المتوقعة عند التنفيذ

### ملفات جديدة

- `frontend/src/app/features/landing/landing-page.component.ts|html|css`
- `frontend/src/app/features/landing/components/landing-header/...`
- `frontend/src/app/features/landing/components/landing-hero/...`
- `frontend/src/app/features/landing/components/vision-strip/...`
- `frontend/src/app/features/landing/components/achievements-section/...`
- `frontend/src/app/features/landing/components/landing-footer/...`
- ملفات `.spec.ts` المقابلة حسب مسؤوليات الاختبار في §14

### ملفات معدلة

- `frontend/src/app/app.routes.ts`
- `frontend/src/app/features/auth/school-login/school-login.component.ts|html|css`
- `frontend/src/app/shared/components/achievements-showcase/achievements-showcase.component.ts|html|css`
- `frontend/src/assets/i18n/ar.json`
- `frontend/src/assets/i18n/en.json`
- `frontend/src/styles/design-tokens.css` فقط عند الحاجة إلى tokens مشتركة مثبتة
- `docs/README.md`
- `docs/14-DECISIONS-AND-DEVIATIONS.md`
- هذه المواصفة لتحديث حالة التنفيذ ونتائج التحقق

## 18. المخاطر والاحتواء

| الخطر | الأثر | الاحتواء |
|---|---|---|
| المبالغة في glass/glow تضعف القراءة | جودة بصرية شكلية فقط | حد أقصى للشفافية والblur + contrast check على كل surface |
| CSS كبير يتجاوز Angular budget | فشل production build | تقسيم المكوّنات، reuse tokens، وعدم تكرار pattern rules |
| slider تلقائي مزعج أو غير متاح | تجربة سيئة وإخفاق WCAG | pause on hover/focus، controls واضحة، reduced-motion، focus management |
| كسر login أثناء فصله بصريًا | عطل حرج | عدم لمس AuthService/form/redirect logic + regression specs |
| تحميل الصور يبطئ الصفحة | LCP/CLS سيئ | eager لأول صورة مرئية فقط، lazy للباقي، dimensions ثابتة، لا أصل جديد كبير |
| anchors تختفي خلف sticky header | محتوى يبدو مقصوصًا | `scroll-margin-block-start` موحد واختبار direct fragments |
| النصوص التسويقية غير معتمدة | نزاع محتوى | اعتماد نص §5 في المرحلة 0 قبل كتابة الواجهة |

## 19. أسئلة الاعتماد

يعد اعتماد هذه المواصفة موافقة على الافتراضات التالية:

1. تكون الصفحة الرئيسية العامة على `/`، والدخول صفحة مستقلة لا modal.
2. تستخدم نصوص Hero المقترحة في §5.2.
3. تبقى بيانات الإنجازات الحالية ثابتة محليًا في النسخة الأولى.
4. يؤدي wildcard إلى `/` بدل صفحة الدخول حتى إنشاء صفحة 404 لاحقًا.
5. لا تضاف أقسام عامة أخرى مثل الأخبار الكاملة، الكادر التعليمي، الخريطة، أو نموذج تواصل في هذه المرحلة.

أي تعديل على هذه النقاط يحدث في المواصفة أولًا، ثم يبدأ التنفيذ بعد إعادة اعتمادها.
