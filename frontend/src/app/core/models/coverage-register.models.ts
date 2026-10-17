import { PagedResult } from './api-response.model';
export type CoverageRegisterScope = 'Day' | 'Week';
export interface CoverageRegisterInfo {
  id:number;date:string;revision:number;sourceRevision:string;isComplete:boolean;fileName:string;sha256:string;issuedAt:string;scope?:CoverageRegisterScope;endDate?:string|null;
}
export interface CoverageRegisterContext {
  date:string;sourceRevision:string|null;issue:string|null;teacherCount:number;periodCount:number;executedPeriods:number;isComplete:boolean;
  history:PagedResult<CoverageRegisterInfo>;scope?:CoverageRegisterScope;endDate?:string|null;
}
