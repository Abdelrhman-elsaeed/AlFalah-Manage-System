import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

interface GuideFeature {
  readonly number: string;
  readonly icon: string;
  readonly title: string;
  readonly benefit: string;
  readonly steps: readonly string[];
  readonly route: string;
  readonly action: string;
  readonly tone: string;
}

@Component({
  selector: 'app-officer-guide',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './officer-guide.component.html',
  styleUrl: './officer-guide.component.css'
})
export class OfficerGuideComponent {
  readonly features: readonly GuideFeature[] = [
    { number: '01', icon: 'pi-home', title: 'لوحة شؤون الطلاب', benefit: 'ترتب الأولويات اليومية وتجمع مؤشرات المدرسة في نقطة بداية واحدة.', steps: ['راجع بطاقات الأولوية', 'افتح العنصر المطلوب', 'عد للوحة للتأكد من انخفاض العدد'], route: '/student-affairs/officer', action: 'افتح لوحة المتابعة', tone: 'emerald' },
    { number: '02', icon: 'pi-file-import', title: 'استيراد سجل زاجل', benefit: 'يحوّل ملف الحضور إلى تأخرات صباحية قابلة للمراجعة دون إدخال يدوي.', steps: ['اختر ملف التصدير', 'راجع المعاينة والأخطاء', 'اعتمد الاستيراد مرة واحدة'], route: '/student-affairs/biometrics/zajel', action: 'اذهب للاستيراد', tone: 'blue' },
    { number: '03', icon: 'pi-file-export', title: 'تصدير نور الأسبوعي', benefit: 'ينشئ ملف الغياب الرسمي مع إبقاء الغياب بعذر ضمن السجل واستبعاده من العقوبة.', steps: ['حدد الأسبوع', 'راجع ملخص الحالات', 'نزّل ملف نور واحفظ رقم العملية'], route: '/student-affairs/noor-export', action: 'جهز التصدير', tone: 'violet' },
    { number: '04', icon: 'pi-verified', title: 'مراجعة الأعذار', benefit: 'تحسم مستندات أولياء الأمور وتحدّث أثر الغياب بصورة قابلة للتدقيق.', steps: ['افتح العذر المعلق', 'عاين ملف PDF', 'اقبل أو ارفض مع سبب واضح'], route: '/student-affairs/officer/excuses', action: 'راجع الأعذار', tone: 'amber' },
    { number: '05', icon: 'pi-ticket', title: 'تصاريح دخول الفصل', benefit: 'يعيد الطالب للحصة ضمن نافذة زمنية موثقة ويبلغ المعلم المستهدف.', steps: ['اختر الفصل', 'اختر الطالب أو ابحث عنه', 'حدد السبب والمدة ثم أصدر'], route: '/student-affairs/officer/entry-permits', action: 'أصدر تصريحًا', tone: 'cyan' },
    { number: '06', icon: 'pi-check-square', title: 'استئذانات الخروج', benefit: 'يفصل طلب ولي الأمر عن الاعتماد والتنفيذ عند البوابة لحماية الطالب.', steps: ['راجع بيانات الطلب وهوية المستلم', 'اعتمد أو ارفض مع السبب', 'تابع إقرار المعلم ثم تنفيذ الأمن'], route: '/student-affairs/gate-passes', action: 'راجع الطلبات', tone: 'rose' },
    { number: '07', icon: 'pi-share-alt', title: 'إنشاء وإسناد الإحالات', benefit: 'يحوّل الملاحظة إلى حالة متابعة فعلية ويحدد مسؤولها بوضوح.', steps: ['حدد الفصل والطالب', 'اكتب سببًا وحدد الأولوية', 'أسند للأخصائي الآن أو اتركها للانتظار'], route: '/student-affairs/officer/referrals', action: 'أنشئ إحالة', tone: 'emerald' },
    { number: '08', icon: 'pi-history', title: 'مراجعة أثر الأتمتة', benefit: 'تمنع القرارات الآلية القديمة من الاستمرار بعد تغير المؤشر أو تصحيح البيانات.', steps: ['قارن العدد الأصلي بالحالي', 'اختر إبقاء أو إلغاء أو إغلاق', 'سجل مبرر القرار'], route: '/student-affairs/officer/automation-reviews', action: 'راجع الأثر', tone: 'violet' },
    { number: '09', icon: 'pi-list', title: 'السجلات التشغيلية', benefit: 'يجمع التأخر والسلوك والأكاديميات والتكريمات في مركز بحث موحد.', steps: ['اختر نوع السجل', 'ابحث باسم الطالب أو الوصف', 'افتح الصف لمراجعة التفاصيل'], route: '/student-affairs/officer/operations', action: 'استكشف السجلات', tone: 'blue' },
    { number: '10', icon: 'pi-send', title: 'اعتماد إشعارات أولياء الأمور', benefit: 'يبقي الرسائل الحساسة تحت قرار بشري قبل وصولها للأسرة.', steps: ['راجع الواقعة والسياق', 'اعتمد الإرسال أو اخفِه', 'اكتب سبب الإخفاء عند الحاجة'], route: '/student-affairs/notification-approvals', action: 'راجع الإشعارات', tone: 'amber' },
    { number: '11', icon: 'pi-comments', title: 'مركز الرسائل', benefit: 'يتيح تواصلًا مباشرًا وآمنًا حول طالب محدد مع سجل تسليم واضح.', steps: ['ابدأ محادثة جديدة', 'اختر الفصل ثم الطالب وولي الأمر', 'اكتب الرسالة وتابع حالة التسليم'], route: '/student-affairs/messages', action: 'افتح الرسائل', tone: 'cyan' }
  ];
}
