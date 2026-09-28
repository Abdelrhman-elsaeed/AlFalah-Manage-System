import { TestBed } from '@angular/core/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { MessageService } from 'primeng/api';
import { of, throwError } from 'rxjs';
import { ClassroomDto } from '../../../core/models/daily-operations.models';
import { DailyOperationsService } from '../../../core/services/daily-operations.service';
import { ClassroomsManagementComponent } from './classrooms-management.component';

describe('ClassroomsManagementComponent', () => {
  let api: jasmine.SpyObj<DailyOperationsService>;

  beforeEach(async () => {
    api = jasmine.createSpyObj<DailyOperationsService>('DailyOperationsService', [
      'getAcademicYears', 'getClassrooms', 'createClassroom', 'updateClassroom', 'deleteClassroom'
    ]);
    api.getAcademicYears.and.returnValue(of({
      isSuccess: true, message: '',
      data: [{ id: 3, code: '2026-2027', nameAr: '2026-2027', isActive: true }], errors: []
    }));
    api.getClassrooms.and.returnValue(of(page([])));

    await TestBed.configureTestingModule({
      imports: [ClassroomsManagementComponent, NoopAnimationsModule],
      providers: [MessageService, { provide: DailyOperationsService, useValue: api }]
    }).compileComponents();
  });

  it('finishes loading with an explicit empty collection', () => {
    const component = TestBed.createComponent(ClassroomsManagementComponent).componentInstance;

    expect(component.loading()).toBeFalse();
    expect(component.classrooms()).toEqual([]);
    expect(component.errorMessage()).toBe('');
  });

  it('exposes a list load failure for the retry state', () => {
    api.getClassrooms.and.returnValue(throwError(() => ({ status: 500 })));
    const component = TestBed.createComponent(ClassroomsManagementComponent).componentInstance;

    expect(component.loading()).toBeFalse();
    expect(component.errorMessage()).not.toBe('');
  });

  it('validates required fields and sends only school-safe classroom fields', () => {
    const component = TestBed.createComponent(ClassroomsManagementComponent).componentInstance;
    component.openCreate();
    component.save();
    expect(api.createClassroom).not.toHaveBeenCalled();

    component.form.setValue({
      academicYearId: 3,
      stage: 'Primary',
      gradeLevel: 1,
      section: ' A ',
      classLabel: ' 1/A ',
      physicalLocation: ' Floor 1 ',
      isActive: true
    });
    api.createClassroom.and.returnValue(of({ isSuccess: true, message: '', data: classroom(), errors: [] }));
    component.save();

    const request = api.createClassroom.calls.mostRecent().args[0];
    expect(request).toEqual({
      academicYearId: 3,
      stage: 'Primary',
      gradeLevel: 1,
      section: 'A',
      classLabel: '1/A',
      physicalLocation: 'Floor 1'
    });
    expect('schoolId' in request).toBeFalse();
  });
});

function classroom(): ClassroomDto {
  return {
    id: 7, label: '1/A', stage: 'Primary', gradeLevel: 1, section: 'A',
    physicalLocation: 'Floor 1', academicYearId: 3, academicYearLabel: '2026-2027',
    isActive: true, activeEnrollmentCount: 0, rowVersion: ''
  };
}

function page(items: readonly ClassroomDto[]) {
  return {
    isSuccess: true, message: '',
    data: { items, totalCount: items.length, page: 1, pageSize: 100, totalPages: 1, hasNext: false, hasPrevious: false },
    errors: []
  };
}
