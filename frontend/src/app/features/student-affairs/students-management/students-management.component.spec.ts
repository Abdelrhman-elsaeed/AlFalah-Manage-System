import { TestBed } from '@angular/core/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { MessageService } from 'primeng/api';
import { of, throwError } from 'rxjs';
import { ClassroomDto, StudentDetailsDto } from '../../../core/models/daily-operations.models';
import { DailyOperationsService } from '../../../core/services/daily-operations.service';
import { StudentsManagementComponent } from './students-management.component';

describe('StudentsManagementComponent', () => {
  let api: jasmine.SpyObj<DailyOperationsService>;

  beforeEach(async () => {
    api = jasmine.createSpyObj<DailyOperationsService>('DailyOperationsService', [
      'getClassrooms', 'getStudents', 'getStudent', 'createStudent', 'updateStudent', 'deleteStudent'
    ]);
    api.getClassrooms.and.returnValue(of(classroomPage([])));
    api.getStudents.and.returnValue(of(studentPage([])));

    await TestBed.configureTestingModule({
      imports: [StudentsManagementComponent, NoopAnimationsModule],
      providers: [MessageService, { provide: DailyOperationsService, useValue: api }]
    }).compileComponents();
  });

  it('finishes loading with an empty student state', () => {
    const component = TestBed.createComponent(StudentsManagementComponent).componentInstance;

    expect(component.loading()).toBeFalse();
    expect(component.students()).toEqual([]);
    expect(component.errorMessage()).toBe('');
  });

  it('exposes list errors without inventing client-side data', () => {
    api.getStudents.and.returnValue(throwError(() => ({ status: 500 })));
    const component = TestBed.createComponent(StudentsManagementComponent).componentInstance;

    expect(component.loading()).toBeFalse();
    expect(component.students()).toEqual([]);
    expect(component.errorMessage()).not.toBe('');
  });

  it('validates identity fields and never submits a client supplied schoolId', () => {
    const component = TestBed.createComponent(StudentsManagementComponent).componentInstance;
    component.openCreate();
    component.save();
    expect(api.createStudent).not.toHaveBeenCalled();

    component.form.setValue({
      studentNumber: ' ST-1 ', identityNumber: ' 1234567890 ', firstName: ' Ali ',
      lastName: ' Hassan ', classroomId: null, isActive: true
    });
    api.createStudent.and.returnValue(of({ isSuccess: true, message: '', data: student(), errors: [] }));
    component.save();

    const request = api.createStudent.calls.mostRecent().args[0];
    expect(request.studentNumber).toBe('ST-1');
    expect(request.identityNumber).toBe('1234567890');
    expect(request.classroomId).toBeNull();
    expect('schoolId' in request).toBeFalse();
  });
});

function student(): StudentDetailsDto {
  return {
    student: {
      id: 1, studentNumber: 'ST-1', identityNumber: '1234567890', displayName: 'Ali Hassan',
      classroomId: null, classLabel: null, isActive: true, photoUrl: null
    },
    identityNumber: '1234567890', firstName: 'Ali', middleName: null, lastName: 'Hassan',
    nationalId: null, dateOfBirth: null, gender: null, currentEnrollment: null, rowVersion: ''
  };
}

function classroomPage(items: readonly ClassroomDto[]) {
  return {
    isSuccess: true, message: '',
    data: { items, totalCount: items.length, page: 1, pageSize: 100, totalPages: 1, hasNext: false, hasPrevious: false },
    errors: []
  };
}

function studentPage(items: readonly never[]) {
  return {
    isSuccess: true, message: '',
    data: { items, totalCount: items.length, page: 1, pageSize: 100, totalPages: 1, hasNext: false, hasPrevious: false },
    errors: []
  };
}
