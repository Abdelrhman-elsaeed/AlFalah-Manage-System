import { TestBed } from '@angular/core/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { provideRouter, Router } from '@angular/router';
import { MessageService } from 'primeng/api';
import { of, throwError } from 'rxjs';
import { ClassroomDto, StudentDetailsDto, StudentListItemDto } from '../../../core/models/daily-operations.models';
import { DailyOperationsService } from '../../../core/services/daily-operations.service';
import { StudentsManagementComponent } from './students-management.component';

describe('StudentsManagementComponent', () => {
  let api: jasmine.SpyObj<DailyOperationsService>;

  beforeEach(async () => {
    api = jasmine.createSpyObj<DailyOperationsService>('DailyOperationsService', [
      'getClassrooms', 'getStudents', 'getStudent', 'createStudent', 'updateStudent', 'deleteStudent',
      'getGuardianOptions', 'getStudentGuardians', 'linkStudentGuardian', 'revokeStudentGuardian', 'transferStudent'
    ]);
    api.getClassrooms.and.returnValue(of(classroomPage([])));
    api.getStudents.and.returnValue(of(studentPage([])));

    await TestBed.configureTestingModule({
      imports: [StudentsManagementComponent, NoopAnimationsModule],
      providers: [MessageService, provideRouter([]), { provide: DailyOperationsService, useValue: api }]
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

  it('links an available guardian to the selected student', () => {
    const component = TestBed.createComponent(StudentsManagementComponent).componentInstance;
    const details = student();
    const item = { student: details.student, riskBadges: [] };
    api.getGuardianOptions.and.returnValue(of({
      isSuccess: true, message: '', errors: [],
      data: [{ id: 7, displayName: 'ولي أمر الطالب', username: 'guardian.7', phoneNumber: null }]
    }));
    api.getStudentGuardians.and.returnValue(of({ isSuccess: true, message: '', errors: [], data: [] }));
    api.linkStudentGuardian.and.returnValue(of({
      isSuccess: true, message: '', errors: [],
      data: {
        id: 11,
        guardian: { id: 7, displayName: 'ولي أمر الطالب', relationship: 'Father', isPrimary: true, receivesNotifications: true },
        canSubmitExcuses: true, canRequestGatePass: true, validFrom: '2026-10-02', validTo: null,
        isActive: true, rowVersion: ''
      }
    }));

    component.openGuardians(item);
    component.guardianForm.controls.guardianProfileId.setValue(7);
    component.linkGuardian();

    expect(api.linkStudentGuardian).toHaveBeenCalled();
    expect(component.guardianLinks()[0].guardian.id).toBe(7);
  });

  it('opens one classroom on a separate route', () => {
    const classroom = classroomItem(12, 'الصف الأول / أ', 7);
    api.getClassrooms.and.returnValue(of(classroomPage([classroom])));
    const component = TestBed.createComponent(StudentsManagementComponent).componentInstance;
    const router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.resolveTo(true);

    component.openStudentList(classroom.id);

    expect(router.navigate).toHaveBeenCalledWith(['/student-affairs/students/classroom', classroom.id]);
  });

  it('prefills the classroom when adding a student from its card', () => {
    const component = TestBed.createComponent(StudentsManagementComponent).componentInstance;

    component.openCreate(12);

    expect(component.dialogVisible()).toBeTrue();
    expect(component.form.controls.classroomId.value).toBe(12);
  });
});

function student(): StudentDetailsDto {
  return {
    student: {
      id: 1, studentNumber: 'ST-1', identityNumber: '1234567890', displayName: 'Ali Hassan',
      classroomId: null, classLabel: null, isActive: true, photoUrl: null
    },
    identityNumber: '1234567890', firstName: 'Ali', middleName: null, lastName: 'Hassan',
    nationalId: null, dateOfBirth: null, gender: null, currentEnrollment: null, guardians: [], rowVersion: ''
  };
}

function classroomPage(items: readonly ClassroomDto[]) {
  return {
    isSuccess: true, message: '',
    data: { items, totalCount: items.length, page: 1, pageSize: 100, totalPages: 1, hasNext: false, hasPrevious: false },
    errors: []
  };
}

function classroomItem(id: number, label: string, activeEnrollmentCount: number): ClassroomDto {
  return {
    id, label, activeEnrollmentCount, stage: 'Primary', gradeLevel: 1, section: 'A',
    physicalLocation: '', academicYearId: 1, academicYearLabel: '2026/2027', isActive: true, rowVersion: ''
  };
}

function studentPage(items: readonly StudentListItemDto[]) {
  return {
    isSuccess: true, message: '',
    data: { items, totalCount: items.length, page: 1, pageSize: 100, totalPages: 1, hasNext: false, hasPrevious: false },
    errors: []
  };
}
