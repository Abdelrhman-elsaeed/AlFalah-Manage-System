import { AfterViewInit, Component, DestroyRef, ElementRef, inject, signal } from '@angular/core';

interface StaffMember {
  readonly id: number;
  readonly name: string;
  readonly role: string;
  readonly specialty: string;
}

interface StaffDepartment {
  readonly id: string;
  readonly title: string;
  readonly shortTitle: string;
  readonly icon: string;
  readonly members: readonly StaffMember[];
}

@Component({
  selector: 'app-organization-section',
  standalone: true,
  templateUrl: './organization-section.component.html',
  styleUrls: ['./organization-section.component.css']
})
export class OrganizationSectionComponent implements AfterViewInit {
  readonly chartOpen = signal(false);
  readonly openDepartments = signal<ReadonlySet<string>>(new Set());

  readonly director: StaffMember = {
    id: 1,
    name: 'ناصر جروض جار الله الزهراني',
    role: 'مدير المدرسة',
    specialty: 'بكالوريوس في اللغة العربية'
  };

  readonly deputies: readonly StaffMember[] = [
    {
      id: 2,
      name: 'محيسن معيوف معيوف الحساني',
      role: 'وكيل المدرسة',
      specialty: 'ماجستير في الحسبة'
    },
    {
      id: 3,
      name: 'عبدالاله ونيس عامر الشريف',
      role: 'وكيل المدرسة',
      specialty: 'بكالوريوس شريعة'
    }
  ];

  readonly departments: readonly StaffDepartment[] = [
    {
      id: 'school-support',
      title: 'الإدارة والخدمات الطلابية',
      shortTitle: 'الإدارة والخدمات',
      icon: 'pi-building-columns',
      members: [
        { id: 4, name: 'حمزه فهد عطيه الذبياني', role: 'إداري / سكرتير', specialty: 'بكالوريوس شريعة' },
        { id: 5, name: 'موسى فهد عطيه الذبياني', role: 'موجه طلابي', specialty: 'دكتوراه في الفقه' },
        { id: 6, name: 'أحمد محمد حسن الكحيلي', role: 'رائد نشاط', specialty: 'بكالوريوس في التاريخ' },
        { id: 7, name: 'وسيم صالح احمد حلواني', role: 'محضر مختبر', specialty: 'بكالوريوس في الفيزيا' }
      ]
    },
    {
      id: 'islamic',
      title: 'قسم التربية الإسلامية',
      shortTitle: 'التربية الإسلامية',
      icon: 'pi-moon',
      members: [
        { id: 8, name: 'مشاري عطيه محمد اللحياني', role: 'معلم تربية اسلامية', specialty: 'بكالوريوس شريعة' },
        { id: 9, name: 'عبدالرحمن خالد عبدالرحمن العمودي', role: 'معلم تربية اسلامية', specialty: 'بكالوريوس في الكتاب والسنة' },
        { id: 10, name: 'علي محمد علي مباركي', role: 'معلم تربية اسلامية', specialty: 'ماجستير في الشريعة' },
        { id: 11, name: 'حسام سليمان محمد خياط', role: 'معلم تربية اسلامية', specialty: 'بكالوريوس في الكتاب والسنة' }
      ]
    },
    {
      id: 'arabic',
      title: 'قسم اللغة العربية',
      shortTitle: 'اللغة العربية',
      icon: 'pi-book',
      members: [
        { id: 12, name: 'محمد عدنان دخيل الله زيني', role: 'معلم لغة عربية', specialty: 'ماجستير في الادب والبلاغة' },
        { id: 13, name: 'حسام حسن محمد الزهراني', role: 'معلم لغة عربية', specialty: 'بكالوريوس في الادب' },
        { id: 14, name: 'مؤيد مرزوق نوار اللقماني', role: 'معلم لغة عربية', specialty: 'بكالوريوس في الادب' }
      ]
    },
    {
      id: 'mathematics',
      title: 'قسم الرياضيات',
      shortTitle: 'الرياضيات',
      icon: 'pi-percentage',
      members: [
        { id: 15, name: 'محمد إبراهيم شامي عسيري', role: 'معلم رياضيات', specialty: 'بكالوريوس في الرياضيات' },
        { id: 16, name: 'أكرم مسعود سعد المطرفي', role: 'معلم رياضيات', specialty: 'بكالوريوس في الرياضيات' },
        { id: 17, name: 'خيري مصطفى عدلي السيد غانم', role: 'معلم رياضيات', specialty: 'بكالوريوس في الرياضيات' },
        { id: 18, name: 'عبدالله سليم عبدالرحمن السلمي', role: 'معلم رياضيات', specialty: 'بكالوريوس في الرياضيات' }
      ]
    },
    {
      id: 'science',
      title: 'قسم العلوم',
      shortTitle: 'العلوم',
      icon: 'pi-sparkles',
      members: [
        { id: 19, name: 'يزن سعد مبارك الظاهري', role: 'معلم علوم', specialty: 'بكالوريوس في الكيمياء' },
        { id: 20, name: 'عبدالله عطيه خلف الله المالكي', role: 'معلم علوم', specialty: 'بكالوريوس في الاحياء' },
        { id: 21, name: 'ماجد احمد عمر باعارمه', role: 'معلم علوم', specialty: 'بكالوريوس في الاحياء' }
      ]
    },
    {
      id: 'social-studies',
      title: 'قسم الاجتماعيات',
      shortTitle: 'الاجتماعيات',
      icon: 'pi-globe',
      members: [
        { id: 22, name: 'أحمد محمد سالم اللقماني', role: 'معلم اجتماعيات', specialty: 'بكالوريوس في الجغرافيا' },
        { id: 23, name: 'منصور سعد عايد الكحيلي', role: 'معلم اجتماعيات', specialty: 'بكالوريوس في التاريخ' }
      ]
    },
    {
      id: 'computer',
      title: 'قسم الحاسب الآلي',
      shortTitle: 'الحاسب الآلي',
      icon: 'pi-desktop',
      members: [
        { id: 24, name: 'أحمد جبر عبدالرحمن الدسوقي', role: 'معلم حاسب الي', specialty: 'بكالوريوس في علوم الحاسب' },
        { id: 25, name: 'حسين عبد الرحمن سالم خرد', role: 'معلم حاسب الي', specialty: 'بكالوريوس في علوم الحاسب' }
      ]
    },
    {
      id: 'english',
      title: 'قسم اللغة الإنجليزية',
      shortTitle: 'اللغة الإنجليزية',
      icon: 'pi-language',
      members: [
        { id: 26, name: 'جمال فضل محمد الحوشبي', role: 'معلم لغة انجليزية', specialty: 'دكتوراة في الدراسات الإسلامية' },
        { id: 27, name: 'محمود أحمد السعيد بدوي', role: 'معلم لغة انجليزية', specialty: 'بكالوريوس في اللغة الإنجليزية' },
        { id: 28, name: 'عبدالله عبيد هاشم الحساني', role: 'معلم لغة انجليزية', specialty: 'بكالوريوس في اللغة الإنجليزية' }
      ]
    },
    {
      id: 'arts-sports',
      title: 'قسم الفنون والرياضة',
      shortTitle: 'الفنون والرياضة',
      icon: 'pi-palette',
      members: [
        { id: 29, name: 'مؤيد وديع غالب خياط', role: 'معلم تربية فنية', specialty: 'بكالوريوس في التربية الفنية' },
        { id: 30, name: 'فؤاد عبدالحميد عبدالغني فلمبان', role: 'معلم تربية بدنية', specialty: 'بكالوريوس في التربية الرياضية' },
        { id: 31, name: 'مشعل أحمد يوسف المالكي', role: 'معلم تربية بدنية', specialty: 'بكالوريوس في التربية الرياضية' }
      ]
    }
  ];

  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly destroyRef = inject(DestroyRef);

  ngAfterViewInit(): void {
    const element = this.host.nativeElement;
    if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) {
      element.classList.add('is-visible');
      return;
    }

    const observer = new IntersectionObserver(
      ([entry]) => {
        if (entry?.isIntersecting) {
          element.classList.add('is-visible');
          observer.disconnect();
        }
      },
      { threshold: 0.12, rootMargin: '0px 0px -8% 0px' }
    );
    observer.observe(element);
    this.destroyRef.onDestroy(() => observer.disconnect());
  }

  toggleChart(): void {
    this.chartOpen.update((open) => !open);
  }

  toggleDepartment(id: string): void {
    this.openDepartments.update((current) => {
      const next = new Set(current);
      next.has(id) ? next.delete(id) : next.add(id);
      return next;
    });
  }

  isDepartmentOpen(id: string): boolean {
    return this.openDepartments().has(id);
  }

  initials(name: string): string {
    return name
      .trim()
      .split(/\s+/)
      .slice(0, 2)
      .map((part) => part[0])
      .join('');
  }
}
