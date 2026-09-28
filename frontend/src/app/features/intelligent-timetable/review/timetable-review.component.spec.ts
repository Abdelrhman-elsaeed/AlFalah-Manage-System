import { TestBed } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { throwError } from 'rxjs';
import { TimetableReviewService } from '../../../core/services/timetable-review.service';
import { TimetableReviewComponent } from './timetable-review.component';

describe('TimetableReviewComponent', () => {
  it('keeps the review visible and exposes a publication conflict', async () => {
    const api = jasmine.createSpyObj<TimetableReviewService>('TimetableReviewService', ['publish']);
    api.publish.and.returnValue(throwError(() => ({
      status: 409,
      error: { errors: ['Timetable changed and requires revalidation'] }
    })));
    TestBed.configureTestingModule({ providers: [
      { provide: TimetableReviewService, useValue: api },
      { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: { get: () => null } } } }
    ] });
    const component = TestBed.runInInjectionContext(() => new TimetableReviewComponent());
    component.review = {
      timetableId: 7,
      timetableRevision: 4,
      canPublish: true,
      findings: [], periods: [], breaks: [], entries: [], classrooms: [], teachers: [], unavailable: []
    } as any;

    await component.publish();

    expect(api.publish).toHaveBeenCalledOnceWith(7, 4);
    expect(component.review?.timetableId).toBe(7);
    expect(component.error).toContain('revalidation');
    expect(component.busy).toBeFalse();
  });
});
