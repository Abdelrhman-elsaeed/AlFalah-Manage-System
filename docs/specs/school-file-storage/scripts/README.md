# S0 — أدوات جرد وقياس قابلة للإعادة

تشغل من جذر المستودع. اقرأ [تقرير القيود والنتائج](../baseline/README.md) قبل استخدام الأرقام. لا تشغّل البروتوتايب أو API للحصول على هذه القياسات: بدء API قد يهاجر/يزرع SQL ويشغّل reconciliation.

## جرد المصادر

```powershell
python docs/specs/school-file-storage/scripts/inventory-prototype.py --source 'C:\Users\abdelrhman\Downloads\Telegram Desktop\[DEVELOPER_SYSTEM_CORE - DO_NOT_MODIFY]\[DEVELOPER_SYSTEM_CORE - DO_NOT_MODIFY]'
```

Python 3.9+، standard library فقط. يقرأ ملفات .py/.html/.json/.csv/.md في المجلد المرجعي قراءة ساكنة ولا يستورد كودها. الأصل المفحوص BASE_DIR هو والد core وفق تنظيم الحزمة المقدمة. المخرج `prototype-inventory.json` و`source-manifest.md`، مع فحص أن ملفات المصدر لم تتغير أثناء القراءة. الملفات الأصلية ومساراتها وأسماء المعلمين لا تُنسخ للمخرجات؛ التفصيل يتتبع source ordinal/hash. التوقيت في المخرجات سيتغير عند إعادة القياس.

## قاعدة Development المحلية

```powershell
powershell -NoProfile -File docs/specs/school-file-storage/scripts/measure-runtime-baseline.ps1
```

يقرأ appsettings.Development.json المحلي، يرفض SQL بعيدًا، ويستخدم fixed SELECT statements فقط. المخرج `runtime-baseline.json`؛ لا startup أو audit writes أو migrations/seeding. لا يطبع connection string أو أسماء أشخاص أو محتوى ملفات أو معرفات Google. `SchoolKey/TeacherKey/YearKey/TaskKey` مفاتيح SQL محلية للفصل بين المجموعات؛ ليست معرفات Drive.

## مراقب Drive

```powershell
dotnet build docs/specs/school-file-storage/scripts/DriveBaseline --verbosity quiet
dotnet --roll-forward Major docs/specs/school-file-storage/scripts/DriveBaseline/bin/Debug/net8.0/DriveBaseline.dll 'D:\AlFalah-Manage-System'
```

على جهاز يملك ASP.NET Core 8 يمكن تشغيل `dotnet run --project docs/specs/school-file-storage/scripts/DriveBaseline -- 'D:\AlFalah-Manage-System'` مباشرة. المشروع أداة S0 خارج solution المنتج ويستعمل المكتبات الموجودة؛ لا حزم جديدة أو تغيير target/runtime للتطبيق.

المراقب يقرأ LocalDB الحالي ومفاتيح Data Protection الموجودة باستخدام نفس application name/purpose، مع DisableAutomaticKeyGeneration. NullLogger يمنع تسريب رسائل provider/Google. لا SaveChanges أو استضافة API أو seed. gate شبكة يسمح فقط HTTPS GET إلى www.googleapis.com/drive/v3 وPOST token exchange إلى oauth2.googleapis.com/token؛ يمنع الرفع/التعديل/الحذف. لا تنزيل محتوى الملفات. OAuth token exchange يكتب cache في الذاكرة فقط.

يفحص جذر كل اتصال مفعّل ويقرأ كل صفحات المجلدات حتى 300 طلب HTTP و90 ثانية لكل مدرسة؛ يوقف الدوران بالمجلدات. يطابق IDs في الذاكرة مع ledger ويحسب الملفات غير المفهرسة ومجموعات منح المعلمين. لا يستنتج سنة لملف غير مفهرس؛ تفاصيل المدرسة/المعلم/السنة للشواهد المسجلة في قياس SQL. أسماء الملفات ومعرفات Drive وأجسام HTTP والاستثناءات التفصيلية لا تخرج. الحساب Complete فقط، وما تعذر أو اكتمل جزئيًا تكون أرقامه null.

المخرج `drive-baseline.json`. Unavailable/Partial نتيجة صالحة للمراقبة لكنها ليست اتصالًا ناجحًا أو أرقامًا صفرية. النتيجة الحالية فشل فك تشفير credential قبل HTTP؛ إعادة التشغيل وحدها لا تصلح الاتصال. تتطلب رؤية شاملة أن credential نفسها تستطيع قراءة كل شجرة الجذر؛ لا يمكن للأداة عد ملفات لا يسمح Google لها برؤيتها.
