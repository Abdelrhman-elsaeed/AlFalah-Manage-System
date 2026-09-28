import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { MessageService } from 'primeng/api';
import { of, throwError } from 'rxjs';
import { DailyOperationsService } from '../../../core/services/daily-operations.service';
import { BiometricImportComponent } from './biometric-import.component';

describe('BiometricImportComponent', () => {
  let api: jasmine.SpyObj<DailyOperationsService>;

  beforeEach(async () => {
    api = jasmine.createSpyObj<DailyOperationsService>('DailyOperationsService', ['importZajel']);
    await TestBed.configureTestingModule({
      imports: [BiometricImportComponent, NoopAnimationsModule],
      providers: [MessageService, { provide: DailyOperationsService, useValue: api }]
    }).compileComponents();
  });

  it('rejects a non-xlsx file before calling the API and renders the validation state', () => {
    const fixture = TestBed.createComponent(BiometricImportComponent);
    const component = fixture.componentInstance;

    acceptFile(component, new File(['data'], 'zajel.csv', { type: 'text/csv' }));
    fixture.detectChanges();

    expect(component.selectedFile()).toBeNull();
    expect(component.errorMessage()).toContain('.xlsx');
    expect(api.importZajel).not.toHaveBeenCalled();
    expect(fixture.nativeElement.querySelector('[role="alert"]')).not.toBeNull();
  });

  it('shows the HTTP failure while preserving the selected file for retry', () => {
    const fixture = TestBed.createComponent(BiometricImportComponent);
    const component = fixture.componentInstance;
    const file = new File(['workbook'], 'zajel.xlsx');
    acceptFile(component, file);
    api.importZajel.and.returnValue(throwError(() => new HttpErrorResponse({
      status: 403,
      error: { message: 'You do not have permission to import Zajel.' }
    })));

    component.upload();
    fixture.detectChanges();

    expect(component.uploading()).toBeFalse();
    expect(component.selectedFile()).toBe(file);
    expect(component.errorMessage()).toContain('permission');
    expect(fixture.nativeElement.querySelector('[role="alert"]').textContent).toContain('permission');
  });

  it('renders partial-success counters and row issues returned by the server', () => {
    const fixture = TestBed.createComponent(BiometricImportComponent);
    const component = fixture.componentInstance;
    acceptFile(component, new File(['workbook'], 'zajel.xlsx'));
    api.importZajel.and.returnValue(of({
      isSuccess: true,
      message: '',
      data: {
        totalRows: 5,
        importedDelays: 2,
        skippedOnTimeRows: 1,
        duplicateRows: 1,
        unmatchedRows: 1,
        issues: [{ rowNumber: 6, code: 'EnrollmentNotFound', message: 'No active enrollment.' }]
      },
      errors: []
    }));

    component.upload();
    fixture.detectChanges();

    expect(component.result()?.importedDelays).toBe(2);
    expect(component.result()?.issues).toHaveSize(1);
    expect(fixture.nativeElement.querySelector('.results')).not.toBeNull();
  });
});

function acceptFile(component: BiometricImportComponent, file: File): void {
  (component as unknown as { acceptFile(file: File): void }).acceptFile(file);
}
