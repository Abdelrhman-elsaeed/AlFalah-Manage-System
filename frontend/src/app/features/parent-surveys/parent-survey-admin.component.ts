import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormArray, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import {
  ParentSurvey,
  ParentSurveyRating,
  ParentSurveyStatus,
  ParentSurveySubmission,
  ParentSurveySubmissionListItem,
  SaveParentSurveyRequest
} from '../../core/models/parent-survey.models';
import { ParentSurveysService } from '../../core/services/parent-surveys.service';
import { ToastService } from '../../core/services/toast.service';

interface SurveyEditorForm {
  title: FormControl<string>;
  description: FormControl<string>;
  items: FormArray<FormControl<string>>;
}

@Component({
  selector: 'app-parent-survey-admin',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './parent-survey-admin.component.html',
  styleUrls: ['./parent-survey-admin.component.css']
})
export class ParentSurveyAdminComponent implements OnInit {
  private readonly service = inject(ParentSurveysService);
  private readonly toast = inject(ToastService);

  readonly ParentSurveyStatus = ParentSurveyStatus;
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly activeTab = signal<'forms' | 'templates'>('forms');
  readonly surveys = signal<ParentSurvey[]>([]);
  readonly templates = signal<ParentSurvey[]>([]);
  readonly editorOpen = signal(false);
  readonly editingId = signal<number | null>(null);
  readonly editingTemplate = signal(false);
  readonly selectedTemplateId = signal<number | null>(null);
  readonly submissionsOpen = signal(false);
  readonly selectedSurvey = signal<ParentSurvey | null>(null);
  readonly submissions = signal<ParentSurveySubmissionListItem[]>([]);
  readonly selectedSubmission = signal<ParentSurveySubmission | null>(null);

  readonly editorForm = new FormGroup<SurveyEditorForm>({
    title: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(200)] }),
    description: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(2000)] }),
    items: new FormArray<FormControl<string>>([])
  });

  ngOnInit(): void {
    this.reload();
  }

  get items(): FormArray<FormControl<string>> {
    return this.editorForm.controls.items;
  }

  setTab(tab: 'forms' | 'templates'): void {
    this.activeTab.set(tab);
  }

  startCreate(isTemplate: boolean): void {
    this.editingId.set(null);
    this.editingTemplate.set(isTemplate);
    this.selectedTemplateId.set(null);
    this.resetEditorDefinition();
    this.editorOpen.set(true);
  }

  createFromTemplate(template: ParentSurvey): void {
    this.editingId.set(null);
    this.editingTemplate.set(false);
    this.selectedTemplateId.set(template.id);
    this.applyTemplate(template);
    this.editorOpen.set(true);
  }

  edit(survey: ParentSurvey): void {
    this.editingId.set(survey.id);
    this.editingTemplate.set(survey.isTemplate);
    this.selectedTemplateId.set(null);
    this.editorForm.controls.title.setValue(survey.title);
    this.editorForm.controls.description.setValue(survey.description ?? '');
    this.items.clear();
    survey.items.forEach(item => this.addItem(item.text));
    this.editorOpen.set(true);
  }

  onTemplateSelected(event: Event): void {
    const value = (event.target as HTMLSelectElement).value;
    const templateId = value ? Number(value) : null;
    this.selectedTemplateId.set(templateId);

    if (templateId === null) {
      this.resetEditorDefinition();
      return;
    }

    const template = this.templates().find(item => item.id === templateId);
    if (template) this.applyTemplate(template);
  }

  addItem(value = ''): void {
    this.items.push(new FormControl(value, {
      nonNullable: true,
      validators: [Validators.required, Validators.minLength(2), Validators.maxLength(500)]
    }));
  }

  removeItem(index: number): void {
    if (this.items.length === 1) {
      this.toast.warn('تنبيه', 'يجب أن يحتوي النموذج على بند واحد على الأقل.');
      return;
    }
    this.items.removeAt(index);
  }

  moveItem(index: number, direction: -1 | 1): void {
    const target = index + direction;
    if (target < 0 || target >= this.items.length) return;
    const control = this.items.at(index);
    this.items.removeAt(index);
    this.items.insert(target, control);
  }

  closeEditor(): void {
    if (!this.saving()) this.editorOpen.set(false);
  }

  save(): void {
    if (this.editorForm.invalid || this.items.length === 0) {
      this.editorForm.markAllAsTouched();
      this.toast.error('بيانات غير مكتملة', 'راجع العنوان وبنود التقييم.');
      return;
    }

    const raw = this.editorForm.getRawValue();
    const request: SaveParentSurveyRequest = {
      title: raw.title.trim(),
      description: raw.description.trim() || undefined,
      isTemplate: this.editingTemplate(),
      items: raw.items.map(text => ({ text: text.trim() }))
    };

    this.saving.set(true);
    const call = this.editingId()
      ? this.service.update(this.editingId()!, request)
      : this.service.create(request);
    call.subscribe({
      next: response => {
        this.saving.set(false);
        if (!response.isSuccess) {
          this.toast.error('تعذر الحفظ', response.errors?.join('، ') || response.message);
          return;
        }
        this.toast.success('تم الحفظ', this.editingTemplate() ? 'القالب جاهز لإعادة الاستخدام.' : 'النموذج جاهز للنشر.');
        this.editorOpen.set(false);
        this.reload();
      },
      error: () => this.saving.set(false)
    });
  }

  publish(survey: ParentSurvey): void {
    this.service.publish(survey.id).subscribe(response => {
      if (!response.isSuccess || !response.data) return;
      this.copyPublicLink(response.data.publicToken);
      this.toast.success('تم إنشاء الرابط', 'تم نسخ رابط النموذج ويمكنك إرساله لأولياء الأمور.');
      this.reload();
    });
  }

  copyExistingLink(survey: ParentSurvey): void {
    if (survey.publicToken) this.copyPublicLink(survey.publicToken);
  }

  closeSurvey(survey: ParentSurvey): void {
    if (!confirm(`إغلاق النموذج «${survey.title}» ومنع استقبال ردود جديدة؟`)) return;
    this.service.close(survey.id).subscribe(() => {
      this.toast.success('تم الإغلاق');
      this.reload();
    });
  }

  delete(survey: ParentSurvey): void {
    if (!confirm(`حذف «${survey.title}»؟`)) return;
    this.service.delete(survey.id).subscribe(() => {
      this.toast.success('تم الحذف');
      this.reload();
    });
  }

  openSubmissions(survey: ParentSurvey): void {
    this.selectedSurvey.set(survey);
    this.selectedSubmission.set(null);
    this.submissions.set([]);
    this.submissionsOpen.set(true);
    this.service.listSubmissions(survey.id).subscribe(response => {
      if (response.isSuccess && response.data) this.submissions.set(response.data);
    });
  }

  viewSubmission(row: ParentSurveySubmissionListItem): void {
    const survey = this.selectedSurvey();
    if (!survey) return;
    this.service.getSubmission(survey.id, row.id).subscribe(response => {
      if (response.isSuccess && response.data) this.selectedSubmission.set(response.data);
    });
  }

  closeSubmissions(): void {
    this.submissionsOpen.set(false);
    this.selectedSubmission.set(null);
  }

  statusLabel(status: ParentSurveyStatus): string {
    return status === ParentSurveyStatus.Published ? 'منشور'
      : status === ParentSurveyStatus.Closed ? 'مغلق' : 'مسودة';
  }

  ratingLabel(rating: ParentSurveyRating): string {
    return rating === ParentSurveyRating.Weak ? 'ضعيف'
      : rating === ParentSurveyRating.Acceptable ? 'مقبول'
      : rating === ParentSurveyRating.Good ? 'جيد' : 'جيد جدًا';
  }

  private reload(): void {
    this.loading.set(true);
    let pending = 2;
    const done = () => {
      pending--;
      if (pending === 0) this.loading.set(false);
    };
    this.service.list(false).subscribe({
      next: response => {
        if (response.isSuccess && response.data) this.surveys.set(response.data);
        done();
      },
      error: done
    });
    this.service.list(true).subscribe({
      next: response => {
        if (response.isSuccess && response.data) this.templates.set(response.data);
        done();
      },
      error: done
    });
  }

  private copyPublicLink(token: string): void {
    const link = `${window.location.origin}/parent-survey/${token}`;
    if (!navigator.clipboard) {
      window.prompt('انسخ رابط النموذج:', link);
      return;
    }
    navigator.clipboard.writeText(link).catch(() => window.prompt('انسخ رابط النموذج:', link));
  }

  private applyTemplate(template: ParentSurvey): void {
    this.editorForm.controls.title.setValue(template.title);
    this.editorForm.controls.description.setValue(template.description ?? '');
    this.items.clear();
    template.items.forEach(item => this.addItem(item.text));
    this.editorForm.markAsDirty();
  }

  private resetEditorDefinition(): void {
    this.editorForm.controls.title.setValue('');
    this.editorForm.controls.description.setValue('');
    this.items.clear();
    this.addItem();
    this.editorForm.markAsPristine();
    this.editorForm.markAsUntouched();
  }
}
