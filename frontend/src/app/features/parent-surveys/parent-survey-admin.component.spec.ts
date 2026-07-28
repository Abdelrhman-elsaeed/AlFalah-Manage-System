import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import {
  ParentSurvey,
  ParentSurveyStatus,
  ParentSurveySubmissionListItem
} from '../../core/models/parent-survey.models';
import { ParentSurveysService } from '../../core/services/parent-surveys.service';
import { ToastService } from '../../core/services/toast.service';
import { ParentSurveyAdminComponent } from './parent-survey-admin.component';

describe('ParentSurveyAdminComponent', () => {
  let fixture: ComponentFixture<ParentSurveyAdminComponent>;
  let component: ParentSurveyAdminComponent;
  let service: jasmine.SpyObj<ParentSurveysService>;

  const template: ParentSurvey = {
    id: 7,
    schoolId: 1,
    schoolName: 'مدارس الفلاح',
    title: 'قالب الخدمات',
    description: 'وصف القالب',
    isTemplate: true,
    status: ParentSurveyStatus.Draft,
    submissionCount: 0,
    createdAt: '2026-07-27T00:00:00Z',
    updatedAt: '2026-07-27T00:00:00Z',
    items: [
      { id: 71, text: 'النظافة', sortOrder: 1 },
      { id: 72, text: 'التواصل', sortOrder: 2 }
    ]
  };

  const survey: ParentSurvey = {
    ...template,
    id: 9,
    title: 'تقييم الخدمات',
    description: undefined,
    isTemplate: false,
    status: ParentSurveyStatus.Published,
    publicToken: 'token',
    submissionCount: 1
  };

  beforeEach(async () => {
    service = jasmine.createSpyObj<ParentSurveysService>(
      'ParentSurveysService',
      ['list', 'listSubmissions', 'getSubmission', 'create', 'update', 'publish', 'close', 'delete']
    );
    service.list.and.callFake((templates: boolean) => of({
      isSuccess: true,
      message: '',
      errors: [],
      data: templates ? [template] : [survey]
    }));
    service.listSubmissions.and.returnValue(of({
      isSuccess: true,
      message: '',
      errors: [],
      data: []
    }));

    const toast = jasmine.createSpyObj<ToastService>('ToastService', ['success', 'error', 'warn']);

    await TestBed.configureTestingModule({
      imports: [ParentSurveyAdminComponent],
      providers: [
        { provide: ParentSurveysService, useValue: service },
        { provide: ToastService, useValue: toast }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(ParentSurveyAdminComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('offers saved templates in the new-form dialog and applies the selected template', () => {
    component.startCreate(false);
    fixture.detectChanges();

    const select = fixture.nativeElement.querySelector('[data-testid="template-select"]') as HTMLSelectElement | null;
    expect(select).not.toBeNull();
    if (!select) return;

    select.value = String(template.id);
    select.dispatchEvent(new Event('change', { bubbles: true }));
    fixture.detectChanges();

    expect(component.editorForm.controls.title.value).toBe(template.title);
    expect(component.editorForm.controls.description.value).toBe(template.description!);
    expect(component.items.getRawValue()).toEqual(['النظافة', 'التواصل']);
  });

  it('keeps the empty response arrow grouped with its instruction', () => {
    const row: ParentSurveySubmissionListItem = {
      id: 1,
      parentName: 'ولي أمر',
      mobileNumber: '+201000000000',
      submittedAt: '2026-07-27T00:00:00Z',
      autoAdjustedAnswerCount: 0
    };
    component.selectedSurvey.set(survey);
    component.submissions.set([row]);
    component.submissionsOpen.set(true);
    fixture.detectChanges();

    const emptyState = fixture.nativeElement.querySelector('.choose-response') as HTMLElement;
    const style = getComputedStyle(emptyState);
    expect(style.display).toBe('flex');
    expect(style.flexDirection).toBe('column');
    expect(style.gap).not.toBe('normal');
  });
});
