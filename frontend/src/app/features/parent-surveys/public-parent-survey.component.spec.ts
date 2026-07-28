import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { of } from 'rxjs';
import { ParentSurveyRating } from '../../core/models/parent-survey.models';
import { ParentSurveysService } from '../../core/services/parent-surveys.service';
import { PublicParentSurveyComponent } from './public-parent-survey.component';

describe('PublicParentSurveyComponent', () => {
  let fixture: ComponentFixture<PublicParentSurveyComponent>;
  let component: PublicParentSurveyComponent;
  let service: jasmine.SpyObj<ParentSurveysService>;

  beforeEach(async () => {
    service = jasmine.createSpyObj<ParentSurveysService>(
      'ParentSurveysService',
      ['getPublic', 'submitPublic']
    );
    service.getPublic.and.returnValue(of({
      isSuccess: true,
      message: '',
      errors: [],
      data: {
        title: 'تقييم المدرسة',
        schoolName: 'مدارس الفلاح',
        isAcceptingResponses: true,
        items: [{ id: 11, text: 'التواصل', sortOrder: 1 }]
      }
    }));
    service.submitPublic.and.returnValue(of({
      isSuccess: true,
      message: 'تم استلام تقييمك بنجاح.',
      errors: []
    }));

    await TestBed.configureTestingModule({
      imports: [PublicParentSurveyComponent],
      providers: [
        { provide: ParentSurveysService, useValue: service },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: { get: () => 'public-token' } } }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(PublicParentSurveyComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('intercepts the native form submit, sends the reply, and shows the success message', () => {
    component.parentName.setValue('ولي أمر الطالب');
    component.mobileNumber.setValue('+201001234567');
    component.selectRating(11, ParentSurveyRating.Good);
    fixture.detectChanges();

    const form = fixture.nativeElement.querySelector('form') as HTMLFormElement;
    const submitEvent = new Event('submit', { bubbles: true, cancelable: true });
    form.dispatchEvent(submitEvent);
    fixture.detectChanges();

    expect(submitEvent.defaultPrevented).toBeTrue();
    expect(service.submitPublic).toHaveBeenCalledTimes(1);
    expect(fixture.nativeElement.textContent).toContain('شكرًا لمشاركتك');
    expect(fixture.nativeElement.textContent).toContain('تم استلام تقييمك بنجاح');
  });
});
