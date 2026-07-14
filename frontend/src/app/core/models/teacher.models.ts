// D-71 — Teachers Management + Teacher Profile (frontend mirror of
// backend AlFalah.Application.DTOs.Teachers.*).
// D-74 — adds Stage + Classes[] + the lightweight Teaching payload used by
// the account-settings "مادتي وفصولي" section + the visit-form auto-fill.

import { PagedResult, SchoolStage } from './phase2.models';

export interface TeacherListItem {
  userId: string;
  fullName: string;
  employeeNumber?: string | null;
  schoolId: number;
  schoolName: string;
  schoolStage: string;
  schoolStageLabelAr: string;
  subject?: string | null;
  stage: SchoolStage;
  classes: string[];
  visitCount: number;
  isActive: boolean;
}

export interface TeacherListQuery {
  page?: number;
  pageSize?: number;
  search?: string;
}

export interface TeacherProfile {
  userId: string;
  fullName: string;
  employeeNumber?: string | null;
  schoolId: number;
  schoolName: string;
  subject?: string | null;
  stage: SchoolStage;
  schoolStageLabelAr: string;
  phoneNumber?: string | null;
  email?: string | null;
  isActive: boolean;
  classes: string[];
  visitCount: number;
}

export interface TeacherVisitSummary {
  id: number;
  visitDate: string;
  lesson: string;
  visitCategory: number;
  visitCategoryLabelAr: string;
  status: number;
  statusLabelAr: string;
  createdByFullName?: string | null;
}

export interface TeacherDomainAverage {
  domainCode: string;
  domainNameAr: string;
  averageScore: number;
}

export interface TeacherVisitProgress {
  visitId: number;
  visitDate: string;
  legendLabel: string;
  domainAverages: TeacherDomainAverage[];
}

export interface TeacherProgress {
  userId: string;
  axisLabels: TeacherDomainAverage[];
  visits: TeacherVisitProgress[];
}

/**
 * D-74 — Lightweight payload returned by GET /teaching endpoints. Carries
 * just the auto-fill-relevant fields so the visit form can hydrate without
 * the full profile DTO.
 */
export interface TeacherTeaching {
  userId: string;
  schoolId: number;
  subject?: string | null;
  stage: SchoolStage;
  classes: string[];
}

/**
 * D-74 — Body of PUT /teaching endpoints. Subject is free-text and capped
 * server-side; Classes is the new (full) set — entries missing from this
 * list are soft-deleted, new entries are inserted (server-side diff).
 */
export interface TeacherTeachingUpsertRequest {
  subject?: string | null;
  stage?: SchoolStage | null;
  classes?: string[] | null;
}

export type TeacherListPage = PagedResult<TeacherListItem>;