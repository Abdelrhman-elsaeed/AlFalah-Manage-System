import { TestBed } from '@angular/core/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { MessageService } from 'primeng/api';
import { of } from 'rxjs';
import { DailyOperationsService } from '../../../core/services/daily-operations.service';
import { AttendanceSheetComponent } from './attendance-sheet.component';

describe('AttendanceSheetComponent RTL filter layout', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AttendanceSheetComponent, NoopAnimationsModule],
      providers: [
        MessageService,
        {
          provide: DailyOperationsService,
          useValue: {
            getClassrooms: () => of({
              isSuccess: true,
              message: '',
              data: { items: [], pageNumber: 1, pageSize: 100, totalCount: 0, totalPages: 0 },
              errors: []
            })
          }
        }
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
});
