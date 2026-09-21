import { inject } from '@angular/core';
import { CanDeactivateFn } from '@angular/router';
import { TranslateService } from '@ngx-translate/core';

export interface HasUnsavedVisitV2Changes { hasUnsavedChanges(): boolean; }

export const visitV2UnsavedGuard: CanDeactivateFn<HasUnsavedVisitV2Changes> = component =>
  !component.hasUnsavedChanges() || window.confirm(inject(TranslateService).instant('VISITS_V2.UNSAVED_CONFIRM'));
