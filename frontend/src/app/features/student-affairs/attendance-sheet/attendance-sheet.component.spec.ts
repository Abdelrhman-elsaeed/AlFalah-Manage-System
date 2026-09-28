import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { MessageService } from 'primeng/api';
import { Subject, of, throwError } from 'rxjs';
import { ApiResponse } from '../../../core/models/api-response.model';
import {
  StudentAttendanceSheetDto,
  StudentAttendanceSheetRowDto
} from '../../../core/models/daily-operations.models';
import { DailyOperationsService } from '../../../core/services/daily-operations.service';
import { AttendanceSheetComponent } from './attendance-sheet.component';

describe('AttendanceSheetComponent', () => {
  let api: jasmine.SpyObj<DailyOperationsService>;

  beforeEach(async () => {
    api = jasmine.createSpyObj<DailyOperationsService>('DailyOperationsService', [
      'getClassrooms',
      'getAttendanceSheet',
      'saveAttendanceSheet',
      'createIdempotencyKey'
    ]);
    api.getClassrooms.and.returnValue(of({
      isSuccess: true,
      message: '',
      data: {
        items: [], totalCount: 0, page: 1, pageSize: 100,
        totalPages: 0, hasNext: false, hasPrevious: false
      },
      errors: []
    }));
    api.createIdempotencyKey.and.returnValue('attendance-key-1');

    await TestBed.configureTestingModule({
      imports: [AttendanceSheetComponent, NoopAnimationsModule],
      providers: [
        MessageService,
        { provide: DailyOperationsService, useValue: api }
      ]
    }).compileComponents();
  });

  it('keeps the classroom and date controls padded and aligned in RTL', () => {
    const fixture = TestBed.createComponent(AttendanceSheetComponent);
    fixture.detectChanges();

    const selectors = fixture.nativeElement.querySelector('.selectors') as HTMLElement;
    const dropdown = fixture.nativeElement.querySelector('.selectors .p-dropdown') as HTMLElement;
    const calendar = fixture.nativeElement.querySelector('.selectors .p-calendar') as HTMLElement;

    const selectorsStyle = getComputedStyle(selectors);
    expect(Number.parseFloat(selectorsStyle.paddingInlineStart)).toBeGreaterThanOrEqual(14);
    expect(Number.parseFloat(selectorsStyle.paddingInlineEnd)).toBeGreaterThanOrEqual(14);
    expect(getComputedStyle(dropdown).width).toBe(getComputedStyle(calendar).width);
    expect(getComputedStyle(dropdown).minHeight).toBe(getComputedStyle(calendar).minHeight);
  });

  it('sends checked students as absent and unchecked students as present', () => {
    const component = createComponent();
    component.sheet.set(sheet([row(1, 'Present'), row(2, 'Absent')]));
    component.selectedAbsentIds.set(new Set([2]));
    component.setAbsent(row(1, 'Present'), true);
    component.setAbsent(row(2, 'Absent'), false);
    api.saveAttendanceSheet.and.returnValue(of(success(sheet([row(1, 'Absent'), row(2, 'Present')], 'r2'))));

    invokeSave(component);

    expect(api.saveAttendanceSheet).toHaveBeenCalledOnceWith({
      date: '2026-09-28',
      classroomId: 12,
      absentStudentIds: [1],
      rosterRevision: 'r1'
    }, 'attendance-key-1');
    expect(component.dirty()).toBeFalse();
  });

  it('submits an empty absent list to mark the whole active roster present', () => {
    const component = createComponent();
    component.sheet.set(sheet([row(1, 'Absent'), row(2, 'Present')]));
    component.selectedAbsentIds.set(new Set());
    api.saveAttendanceSheet.and.returnValue(of(success(sheet([row(1, 'Present'), row(2, 'Present')], 'r2'))));

    invokeSave(component);

    expect(api.saveAttendanceSheet.calls.mostRecent().args[0].absentStudentIds).toEqual([]);
  });

  it('blocks a second save while the first request is in flight', () => {
    const component = createComponent();
    component.sheet.set(sheet([row(1, 'Present')]));
    const pending = new Subject<ApiResponse<StudentAttendanceSheetDto>>();
    api.saveAttendanceSheet.and.returnValue(pending);

    invokeSave(component);
    invokeSave(component);

    expect(api.saveAttendanceSheet).toHaveBeenCalledTimes(1);
    expect(component.saving()).toBeTrue();
  });

  it('reloads on 409 and preserves only selections still in the active roster', () => {
    const component = createComponent();
    component.sheet.set(sheet([row(1, 'Absent', 'Ali'), row(2, 'Absent', 'Omar')]));
    component.selectedAbsentIds.set(new Set([1, 2]));
    api.saveAttendanceSheet.and.returnValue(throwError(() => new HttpErrorResponse({ status: 409 })));
    api.getAttendanceSheet.and.returnValue(of(success(
      sheet([row(1, 'Present', 'Ali'), row(3, 'Present', 'Mona')], 'r2')
    )));

    invokeSave(component);

    expect(api.getAttendanceSheet).toHaveBeenCalledOnceWith('2026-09-28', 12);
    expect([...component.selectedAbsentIds()]).toEqual([1]);
    expect(component.removedDuringConflict()).toEqual(['Omar']);
    expect(component.dirty()).toBeTrue();
  });

  it('never selects or overwrites an accepted AbsentExcused row', () => {
    const component = createComponent();
    const excused = row(1, 'AbsentExcused');
    component.sheet.set(sheet([excused, row(2, 'Present')]));
    component.selectedAbsentIds.set(new Set());
    component.setAbsent(excused, true);
    api.saveAttendanceSheet.and.returnValue(of(success(sheet([excused, row(2, 'Present')], 'r2'))));

    invokeSave(component);

    expect(component.selectedAbsentIds().has(1)).toBeFalse();
    expect(api.saveAttendanceSheet.calls.mostRecent().args[0].absentStudentIds).toEqual([]);
    expect(component.excusedCount()).toBe(1);
    expect(component.presentCount()).toBe(1);
  });

  it('renders scoped 403 and 404 sheet failures instead of clearing a saved selection', () => {
    for (const status of [403, 404]) {
      api.getAttendanceSheet.and.returnValue(throwError(() => new HttpErrorResponse({
        status,
        error: { message: status === 403 ? 'Permission denied.' : 'Classroom was not found.' }
      })));
      const fixture = TestBed.createComponent(AttendanceSheetComponent);
      const component = fixture.componentInstance;
      component.selectedAbsentIds.set(new Set([7]));

      component.classroomControl.setValue(12);
      fixture.detectChanges();

      expect(component.errorMessage()).toContain(status === 403 ? 'Permission' : 'not found');
      expect(component.selectedAbsentIds().has(7)).toBeTrue();
      expect(fixture.nativeElement.querySelector('[role="alert"]')).not.toBeNull();
    }
  });

  it('uses a horizontally scrollable roster wrapper for narrow viewports', () => {
    const fixture = TestBed.createComponent(AttendanceSheetComponent);
    const component = fixture.componentInstance;
    component.sheet.set(sheet([row(1, 'Present')]));
    fixture.detectChanges();

    const wrapper = fixture.nativeElement.querySelector('.p-datatable-wrapper') as HTMLElement;
    expect(wrapper).not.toBeNull();
    expect(['auto', 'scroll']).toContain(getComputedStyle(wrapper).overflowX);
  });

  function createComponent(): AttendanceSheetComponent {
    return TestBed.createComponent(AttendanceSheetComponent).componentInstance;
  }

  function invokeSave(component: AttendanceSheetComponent): void {
    (component as unknown as { save(): void }).save();
  }
});

function row(
  id: number,
  status: StudentAttendanceSheetRowDto['status'],
  displayName = `Student ${id}`
): StudentAttendanceSheetRowDto {
  return {
    attendanceId: status === 'Present' ? null : id + 100,
    student: {
      id,
      studentNumber: `ST-${id}`,
      displayName,
      classroomId: 12,
      classLabel: '1/A',
      isActive: true,
      photoUrl: null
    },
    status,
    excuseStatus: status === 'AbsentExcused' ? 'Accepted' : null,
    recordedBy: null,
    recordedAt: null,
    penaltyEligibleAbsenceBadge: {
      metricCode: 'PenaltyAbsenceDay',
      eligibleTermCount: 0,
      effectiveSettingsVersion: 1,
      nextThreshold: null,
      severity: 'None',
      lastOccurrenceAt: null,
      recalculatedAt: '2026-09-28T08:00:00Z'
    },
    rowVersion: null
  };
}

function sheet(
  rows: readonly StudentAttendanceSheetRowDto[],
  rosterRevision = 'r1'
): StudentAttendanceSheetDto {
  return {
    date: '2026-09-28',
    classroom: { id: 12, label: '1/A', stage: 'Primary', gradeLevel: 1, section: 'A' },
    rosterRevision,
    isSaved: rows.some(item => item.attendanceId !== null),
    rows
  };
}

function success(data: StudentAttendanceSheetDto): ApiResponse<StudentAttendanceSheetDto> {
  return { isSuccess: true, message: '', data, errors: [] };
}
