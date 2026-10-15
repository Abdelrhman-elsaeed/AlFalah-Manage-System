import { InjectionToken, TemplateRef } from '@angular/core';

export type DutySection = 'schedule' | 'settings' | 'execution' | 'documents' | 'fairness' | 'history' | 'permissions';
export interface DutyWorkspaceNavigation {
  activateSection(section: DutySection): void;
  pageTemplate(section: DutySection): TemplateRef<unknown> | null;
  canLeaveSection(): boolean | Promise<boolean>;
}
export const DUTY_WORKSPACE_NAVIGATION = new InjectionToken<DutyWorkspaceNavigation>('Duty workspace navigation');

export const DUTY_SECTIONS: { id: DutySection; key: string; icon: string; managerOnly?: boolean; ownerOnly?:boolean }[] = [
  { id: 'settings', key: 'DUTY_PAGES.SETTINGS', icon: 'pi pi-sliders-h', managerOnly: true },
  { id: 'schedule', key: 'DUTY_PAGES.SCHEDULE', icon: 'pi pi-calendar' },
  { id: 'execution', key: 'DUTY_PAGES.EXECUTION', icon: 'pi pi-pen-to-square' },
  { id: 'documents', key: 'DUTY_PAGES.DOCUMENTS', icon: 'pi pi-file-pdf' },
  { id: 'fairness', key: 'DUTY_PAGES.FAIRNESS', icon: 'pi pi-chart-bar', managerOnly: true },
  { id: 'history', key: 'DUTY_PAGES.HISTORY', icon: 'pi pi-history', managerOnly: true },
  {id:'permissions',key:'DUTY_ACCESS.TITLE',icon:'pi pi-shield',managerOnly:true,ownerOnly:true}
];
export function dutySectionRoute(section: DutySection) { return section === 'schedule' ? '/school-duty' : `/school-duty/${section}`; }
