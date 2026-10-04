# S0 — خريطة المجال والمسؤولين

## المجالات والمعايير

الأسماء أدناه مأخوذة من فهرس JSON كما هي؛ الأرقام أعداد مراجع المصدر فقط. لا نعتمد أسماء الأشخاص أو حالات الاستيفاء الموروثة.

| المجال | المعيار / رمز مستقر | مراجع الفهرس | بنود المتتبع | صفوف JSON للنواقص |
|---|---|---|---|---|
| الإدارة المدرسية | 1.1 التخطيط | 61 | 4 | 11 |
| الإدارة المدرسية | 1.2 قيادة العملية التعليمية | 241 | 3 | 2 |
| الإدارة المدرسية | 1.3 المجتمع المدرسي | 854 | 4 | 70 |
| الإدارة المدرسية | 1.4 التطوير المؤسسي | 275 | 3 | 0 |
| الإدارة المدرسية | 1.5 حقوق المتعلم وحمايته | 32 | 0 | 0 |
| التعليم والتعلم | 2.1 بناء خبرات التعلم | 2427 | 3 | 56 |
| التعليم والتعلم | 2.2 تقويم التعلم | 94 | 7 | 4 |
| نواتج التعلم | 3.1 التحصيل التعليمي | 51 | 2 | 1 |
| نواتج التعلم | 3.2 التطور الشخصي والصحي | 396 | 4 | 1 |
| البيئة المدرسية | 4.1 المبنى المدرسي | 105 | 3 | 0 |
| البيئة المدرسية | 4.2 الأمن والسلامة | 81 | 3 | 0 |

المتتبع يغطي 10 رموز مميزة: المجموعة المسماة item-11-1/item-11-2 تحمل domainKey=2.2، ولا تمثل معيار 1.5. يحتفظ الاستيراد بمعيار 1.5 من الفهرس (32 مرجعًا)، ويسجل عدم وجود متطلب متتبع له بدل اختراع بند أو نقله من 2.2.

## مهام المعلم الحالية — 31 صفًا

هذا ربط مرشح بناءً على معنى المهمة، يحتاج مراجعة متطلبات المدرسة. لا يغير EvidenceTask أو يولد قرار اعتماد. المهام الحالية عامة؛ المتطلب الجديد له SchoolId/AcademicYearId ونسخة قالب. حدود الاختيار تحافظ على القواعد الحالية ولا تجعل كل مهمة متطلبًا عامًا لكل مدرسة.

| مهمة حالية | الاسم الحالي | معايير مرشحة | نوع المطابقة |
|---|---|---|---|
| CV-01 | البيانات الأساسية | 1.4 | مراجعة ملاءمة المحتوى؛ CV-01 بيانات شخصية ليست شاهدًا عامًا تلقائيًا |
| CV-02 | الإنجازات الشخصية | 1.4 | مراجعة ملاءمة المحتوى؛ CV-01 بيانات شخصية ليست شاهدًا عامًا تلقائيًا |
| CV-03 | التكريمات | 1.4 | مراجعة ملاءمة المحتوى؛ CV-01 بيانات شخصية ليست شاهدًا عامًا تلقائيًا |
| CV-04 | الخبرات | 1.4 | مراجعة ملاءمة المحتوى؛ CV-01 بيانات شخصية ليست شاهدًا عامًا تلقائيًا |
| CV-05 | الدورات التدريبية | 1.4 | مراجعة ملاءمة المحتوى؛ CV-01 بيانات شخصية ليست شاهدًا عامًا تلقائيًا |
| CV-06 | الرخصة المهنية | 1.4 | مراجعة ملاءمة المحتوى؛ CV-01 بيانات شخصية ليست شاهدًا عامًا تلقائيًا |
| CV-07 | الشهادات التدريبية | 1.4 | مراجعة ملاءمة المحتوى؛ CV-01 بيانات شخصية ليست شاهدًا عامًا تلقائيًا |
| CV-08 | ملف إنجاز المعلم | 1.4 | مراجعة ملاءمة المحتوى؛ CV-01 بيانات شخصية ليست شاهدًا عامًا تلقائيًا |
| PC-01 | تبادل الزيارات | 1.4 / 1.3؛ PC-01 إلى 2.2 | اختيار الرابط حسب المحتوى؛ لا نسخ أصل |
| PC-02 | الخبرات المهنية | 1.4 / 1.3؛ PC-01 إلى 2.2 | اختيار الرابط حسب المحتوى؛ لا نسخ أصل |
| PC-03 | مبادرات المعلم | 1.4 / 1.3؛ PC-01 إلى 2.2 | اختيار الرابط حسب المحتوى؛ لا نسخ أصل |
| EN-01 | الأنشطة الصفية | 2.1 | ربط مرشح يحتاج مطلبًا محددًا |
| EN-02 | تصميمات ورسومات | 2.1 | ربط مرشح يحتاج مطلبًا محددًا |
| EN-03 | عروض تقديمية | 2.1 | ربط مرشح يحتاج مطلبًا محددًا |
| EN-04 | مواقع ومنصات | 2.1 | ربط مرشح يحتاج مطلبًا محددًا |
| EN-05 | أوراق العمل | 2.1 | ربط مرشح يحتاج مطلبًا محددًا |
| RP-01 | الفاقد التعليمي | 3.1 / 2.1 | قد يخدم أكثر من معيار بمراجعة مستقلة |
| RP-02 | خطة الطلاب المتعثرين والضعاف | 3.1 / 2.1 | قد يخدم أكثر من معيار بمراجعة مستقلة |
| RP-03 | خطة الطلاب المتفوقين والموهوبين | 3.1 / 2.1 | قد يخدم أكثر من معيار بمراجعة مستقلة |
| AS-01 | اختبارات قصيرة لكل وحدة | 2.2 / 3.1 | الاختبارات للتقويم والتحليل للتحصيل حسب المحتوى |
| AS-02 | بحوث ومشاريع للطلاب | 2.2 / 3.1 | الاختبارات للتقويم والتحليل للتحصيل حسب المحتوى |
| AS-03 | تحليل النتائج | 2.2 / 3.1 | الاختبارات للتقويم والتحليل للتحصيل حسب المحتوى |
| SP-01 | تحفيز الطلاب وتشجيعهم | 2.1 / 2.2 / 1.3 | مراجعة بشرية وعزل بيانات الطلاب والأسر |
| SP-02 | سجل المتابعة للطلاب | 2.1 / 2.2 / 1.3 | مراجعة بشرية وعزل بيانات الطلاب والأسر |
| SP-03 | شواهد من التواصل الأسري | 2.1 / 2.2 / 1.3 | مراجعة بشرية وعزل بيانات الطلاب والأسر |
| SP-04 | عينة من أنشطة الطالب | 2.1 / 2.2 / 1.3 | مراجعة بشرية وعزل بيانات الطلاب والأسر |
| SP-05 | كشوف رصد الدرجات | 2.1 / 2.2 / 1.3 | مراجعة بشرية وعزل بيانات الطلاب والأسر |
| SP-06 | ملفات إنجاز الطالب | 2.1 / 2.2 / 1.3 | مراجعة بشرية وعزل بيانات الطلاب والأسر |
| CP-01 | استراتيجيات التعلم النشط وشواهد | 2.1 | خطط وممارسات التدريس؛ لا اعتماد تلقائي |
| CP-02 | الخطة الأسبوعية لكل فصل دراسي | 2.1 | خطط وممارسات التدريس؛ لا اعتماد تلقائي |
| CP-03 | توزيع المنهج | 2.1 | خطط وممارسات التدريس؛ لا اعتماد تلقائي |

الزيارات PC-01 لا تعني أن ملف الزيارة V2 يُنشر تلقائيًا لكل معيار. S5 يؤرشف PDF المعتمد في نطاق الزيارة؛ S3 ينشئ أي ربط شاهد لاحقًا بقرار مستقل وصلاحية مناسبة.

## البنود الـ36 — هوية مرجعية لكل بند

كل صف له بصمة كاملة في prototype-inventory.json. تمت مراجعة schema وحضور الحقول، لكن targetRelPath ليس هوية ملف ولا دليل بايتات. جميع الحالات المصدرية completed؛ تُحفظ كملاحظة تاريخية ولا تتحول Approved. نص المستند والمسؤول الشخصي يبقيان في المصدر المحلي، لا Git.

| Source key | ترتيب | معيار المصدر | الربط الجديد / الاستثناء |
|---|---|---|---|
| item-1-1 | 1 | 1.1 التخطيط | PrototypeImportRow → EvidenceRequirement؛ NeedsResponsibleMapping / ReferenceOnly |
| item-1-2 | 2 | 1.1 التخطيط | PrototypeImportRow → EvidenceRequirement؛ NeedsResponsibleMapping / ReferenceOnly |
| item-1-3 | 3 | 1.1 التخطيط | PrototypeImportRow → EvidenceRequirement؛ NeedsResponsibleMapping / ReferenceOnly |
| item-1-4 | 4 | 1.1 التخطيط | PrototypeImportRow → EvidenceRequirement؛ NeedsResponsibleMapping / ReferenceOnly |
| item-2-1 | 5 | 1.2 قيادة العملية التعليمية | PrototypeImportRow → EvidenceRequirement؛ NeedsResponsibleMapping / ReferenceOnly |
| item-2-2 | 6 | 1.2 قيادة العملية التعليمية | PrototypeImportRow → EvidenceRequirement؛ NeedsResponsibleMapping / ReferenceOnly |
| item-2-3 | 7 | 1.2 قيادة العملية التعليمية | PrototypeImportRow → EvidenceRequirement؛ NeedsResponsibleMapping / ReferenceOnly |
| item-3-1 | 8 | 1.3 المجتمع المدرسي | PrototypeImportRow → EvidenceRequirement؛ NeedsResponsibleMapping / ReferenceOnly |
| item-3-2 | 9 | 1.3 المجتمع المدرسي | PrototypeImportRow → EvidenceRequirement؛ NeedsResponsibleMapping / ReferenceOnly |
| item-3-3 | 10 | 1.3 المجتمع المدرسي | PrototypeImportRow → EvidenceRequirement؛ NeedsResponsibleMapping / ReferenceOnly |
| item-3-4 | 11 | 1.3 المجتمع المدرسي | PrototypeImportRow → EvidenceRequirement؛ NeedsResponsibleMapping / ReferenceOnly |
| item-4-1 | 12 | 1.4 التطوير المؤسسي | PrototypeImportRow → EvidenceRequirement؛ NeedsResponsibleMapping / ReferenceOnly |
| item-4-2 | 13 | 1.4 التطوير المؤسسي | PrototypeImportRow → EvidenceRequirement؛ NeedsResponsibleMapping / ReferenceOnly |
| item-4-3 | 14 | 1.4 التطوير المؤسسي | PrototypeImportRow → EvidenceRequirement؛ NeedsResponsibleMapping / ReferenceOnly |
| item-5-1 | 15 | 2.1 بناء خبرات التعلم | PrototypeImportRow → EvidenceRequirement؛ NeedsResponsibleMapping / ReferenceOnly |
| item-5-2 | 16 | 2.1 بناء خبرات التعلم | PrototypeImportRow → EvidenceRequirement؛ NeedsResponsibleMapping / ReferenceOnly |
| item-5-3 | 17 | 2.1 بناء خبرات التعلم | PrototypeImportRow → EvidenceRequirement؛ NeedsResponsibleMapping / ReferenceOnly |
| item-6-1 | 18 | 2.2 تقويم التعلم | PrototypeImportRow → EvidenceRequirement؛ NeedsResponsibleMapping / ReferenceOnly |
| item-6-2 | 19 | 2.2 تقويم التعلم | PrototypeImportRow → EvidenceRequirement؛ NeedsResponsibleMapping / ReferenceOnly |
| item-6-3 | 20 | 2.2 تقويم التعلم | PrototypeImportRow → EvidenceRequirement؛ NeedsResponsibleMapping / ReferenceOnly |
| item-6-4 | 21 | 2.2 تقويم التعلم | PrototypeImportRow → EvidenceRequirement؛ NeedsResponsibleMapping / ReferenceOnly |
| item-6-5 | 22 | 2.2 تقويم التعلم | PrototypeImportRow → EvidenceRequirement؛ NeedsResponsibleMapping / ReferenceOnly |
| item-7-1 | 23 | 3.1 التحصيل التعليمي | PrototypeImportRow → EvidenceRequirement؛ NeedsResponsibleMapping / ReferenceOnly |
| item-7-2 | 24 | 3.1 التحصيل التعليمي | PrototypeImportRow → EvidenceRequirement؛ NeedsResponsibleMapping / ReferenceOnly |
| item-8-1 | 25 | 3.2 التطور الشخصي والصحي | PrototypeImportRow → EvidenceRequirement؛ NeedsResponsibleMapping / ReferenceOnly |
| item-8-2 | 26 | 3.2 التطور الشخصي والصحي | PrototypeImportRow → EvidenceRequirement؛ NeedsResponsibleMapping / ReferenceOnly |
| item-8-3 | 27 | 3.2 التطور الشخصي والصحي | PrototypeImportRow → EvidenceRequirement؛ NeedsResponsibleMapping / ReferenceOnly |
| item-8-4 | 28 | 3.2 التطور الشخصي والصحي | PrototypeImportRow → EvidenceRequirement؛ NeedsResponsibleMapping / ReferenceOnly |
| item-9-1 | 29 | 4.1 المبنى المدرسي | PrototypeImportRow → EvidenceRequirement؛ NeedsResponsibleMapping / ReferenceOnly |
| item-9-2 | 30 | 4.1 المبنى المدرسي | PrototypeImportRow → EvidenceRequirement؛ NeedsResponsibleMapping / ReferenceOnly |
| item-9-3 | 31 | 4.1 المبنى المدرسي | PrototypeImportRow → EvidenceRequirement؛ NeedsResponsibleMapping / ReferenceOnly |
| item-10-1 | 32 | 4.2 الأمن والسلامة | PrototypeImportRow → EvidenceRequirement؛ NeedsResponsibleMapping / ReferenceOnly |
| item-10-2 | 33 | 4.2 الأمن والسلامة | PrototypeImportRow → EvidenceRequirement؛ NeedsResponsibleMapping / ReferenceOnly |
| item-10-3 | 34 | 4.2 الأمن والسلامة | PrototypeImportRow → EvidenceRequirement؛ NeedsResponsibleMapping / ReferenceOnly |
| item-11-1 | 35 | 2.2 تقويم التعلم | PrototypeImportRow → EvidenceRequirement؛ NeedsResponsibleMapping / ReferenceOnly |
| item-11-2 | 36 | 2.2 تقويم التعلم | PrototypeImportRow → EvidenceRequirement؛ NeedsResponsibleMapping / ReferenceOnly |

## مصفوفة 145 صفًا

كل صف JSON مفهرس برقم وبصمة وcategory/role/importance/status في الجرد، دون الاسم أو المسار. المفتاح الخارجي هو (source resource hash, ordinal)، لا rel_path وحده. 89 مسارًا مميزًا، 56 تكرار مسار، 46 صفًا مكررًا تمامًا. تكرار المسار بين مطالب أو مسؤولين قد يكون مقصودًا؛ التكرار التام يعرض في preview للتسوية، ولا تضاعف الروابط المعتمدة.

النسخة المضمنة matrix192Data تحمل أيضًا 145 صفًا لكن 145 مسارًا مميزًا ولا تطابق JSON الخارجي. لا ندمج النسختين أو نختار واحدة بصمت. S6 يعرض resource hashes والاختلافات في معاينة الاستيراد؛ عدد المصدر واسمه جزء من التدقيق. 56 صفًا في JSON تصنيفها لا يظهر في مسارها: لا يمكن حسم المعيار من اسم المجلد وحده.

## المسؤول الوظيفي لا يساوي دور صلاحيات

| قيمة role في المصدر | التعيين إلى هوية المنصة | أثر الوصول |
|---|---|---|
| أمين مصادر التعلم / المحضر | مستخدم نشط في المدرسة يختاره المدير صراحة؛ لا مطابقة باسم شخص | التكليف لا يمنح Storage.ManageSchool أو ReviewEvidence؛ التفويض سجل مستقل |
| إدارة المدرسة | مستخدم نشط في المدرسة يختاره المدير صراحة؛ لا مطابقة باسم شخص | التكليف لا يمنح Storage.ManageSchool أو ReviewEvidence؛ التفويض سجل مستقل |
| الموجه الصحي | مستخدم نشط في المدرسة يختاره المدير صراحة؛ لا مطابقة باسم شخص | التكليف لا يمنح Storage.ManageSchool أو ReviewEvidence؛ التفويض سجل مستقل |
| الموجه الطلابي | مستخدم نشط في المدرسة يختاره المدير صراحة؛ لا مطابقة باسم شخص | التكليف لا يمنح Storage.ManageSchool أو ReviewEvidence؛ التفويض سجل مستقل |
| رائد النشاط الطلابي | مستخدم نشط في المدرسة يختاره المدير صراحة؛ لا مطابقة باسم شخص | التكليف لا يمنح Storage.ManageSchool أو ReviewEvidence؛ التفويض سجل مستقل |
| مدير المدرسة | مدير المدرسة الفعلي من بيانات المنصة | التكليف لا يمنح Storage.ManageSchool أو ReviewEvidence؛ التفويض سجل مستقل |
| مسؤول الأمن والسلامة والصيانة | مستخدم نشط في المدرسة يختاره المدير صراحة؛ لا مطابقة باسم شخص | التكليف لا يمنح Storage.ManageSchool أو ReviewEvidence؛ التفويض سجل مستقل |
| معلم المادة / التخصص | مستخدم نشط في المدرسة يختاره المدير صراحة؛ لا مطابقة باسم شخص | التكليف لا يمنح Storage.ManageSchool أو ReviewEvidence؛ التفويض سجل مستقل |
| وكيل الشؤون التعليمية | مستخدم نشط في المدرسة يختاره المدير صراحة؛ لا مطابقة باسم شخص | التكليف لا يمنح Storage.ManageSchool أو ReviewEvidence؛ التفويض سجل مستقل |

كل متطلب ينتمي لمدرسة وسنة صريحتين. المدرسة/السنة ليستا مستنتجتين من الاسم أو مسار البروتوتايب. غياب مسؤول مطابق يولد UnresolvedResponsible ويوقف تفعيل الصف؛ لا ينشئ حسابًا أو تفويضًا تلقائيًا. أسماء الأشخاص المسجلة في personGroup/responsible لا تُزرع في المنصة.

## تنفيذ S4 — تنقيح القالب دون استيراد تاريخي

القالب المضمن v1 ينقل البنود الـ36 بنفس SourceKey وترتيبها ومعيارها وبصمة صف المصدر، مع النص الوظيفي والدور وسبب الأهمية ومسار مرجعي منقح وإجراء تجهيز وربط ومراجعة. item-11-1/2 يبقيان في 2.2؛ 1.5 ظاهر بلا بند مخترع. هوية المصدر ومساره والنص ثابتة بعد النسخ، ونطاق المدرسة/السنة/النسخة لا يغير لاحقًا.

في S4 ينشئ المدير مطالب فارغة نشطة إلزامية قابلة للتكليف، حتى قبل تعيين عضو أو تاريخ استحقاق؛ تبقى غير مستوفاة. يختلف هذا عن تفعيل صف **استيراد تاريخي S6** الذي يحتاج تسوية المسؤول/الأصل قبل التطبيق. لا تُستنتج هوية المستخدم من الاسم ولا تُمنح صلاحية بالتكليف. جميع الحالات الابتدائية NotStarted وبلا روابط، ولا يُورث completed.

المهام الأصلية الموجودة تبقى في EvidenceTask مرة واحدة مع OriginalTaskId وCode الدقيقين في كتالوج S3. البنود الجديدة لا تنشئ مهام أخرى. CandidateTaskCodesJson يسجل ترشيحات صريحة فقط، تحتاج مراجعة المحتوى والربط المستقل في S3. كتالوج S3 اختياري أوليًا، وبنود القالب 36 هي المقام الأولي؛ لا تُجمع مطالب تمثل المعنى نفسه تلقائيًا. 145 صفًا تحفظ ordinal وبصمة الصف/المورد ReferenceOnly؛ لا StoredFile أو قرار أو byte من المصدر. [القالب](../../../../backend/AlFalah.Application/Storage/Templates/self-evaluation-v1.json)، [دليل الاختبارات](../verification/s4-self-evaluation-and-reports.md).
