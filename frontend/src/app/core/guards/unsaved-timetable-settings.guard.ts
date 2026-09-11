import { CanDeactivateFn } from '@angular/router';

export interface HasUnsavedTimetableSettings {
  hasUnsavedChanges(): boolean;
}

export const unsavedTimetableSettingsGuard: CanDeactivateFn<HasUnsavedTimetableSettings> = component =>
  !component.hasUnsavedChanges()
  || window.confirm('لديك تغييرات غير محفوظة في إعدادات الجدول. هل تريد المغادرة دون حفظها؟');
