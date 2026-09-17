import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { TimetableService } from './timetable.service';

describe('TimetableService API enum normalization', () => {
  let service: TimetableService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [TimetableService, provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(TimetableService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('maps string enum values from the API to the numeric values used by the timetable grid', () => {
    let result: any;
    service.getCurrent(1, 1).subscribe(response => result = response.data);

    const request = http.expectOne(req => req.url.endsWith('/api/v1/timetables/current'));
    request.flush({
      isSuccess: true,
      message: null,
      errors: [],
      data: {
        id: 10,
        schoolId: 1,
        academicYearId: 1,
        academicYearName: '2026-2027',
        semester: 'First',
        semesterLabelAr: 'الفصل الأول',
        title: 'الجدول المولد',
        isPublished: true,
        publishedAt: null,
        revision: 1,
        updatedAt: '2026-09-16T00:00:00Z',
        entries: [{
          instructorProfileId: 4,
          day: 'Sunday',
          period: 1,
          entryType: 'Lesson',
          classLabel: 'الأول - أ',
          subject: 'الرياضيات'
        }],
        teacherSummaries: [],
        capabilities: { canManage: true, canDelegate: false, canViewVersions: true }
      }
    });

    expect(result.semester).toBe(1);
    expect(result.entries[0].day).toBe(2);
    expect(result.entries[0].entryType).toBe(1);
  });
});
