// ─── Phase 2: School & User Management DTOs (mirror backend contracts) ───────

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
  hasPrevious: boolean;
  hasNext: boolean;
}

export interface PagedQuery {
  page?: number;
  pageSize?: number;
  search?: string;
  sortBy?: string;
  sortDesc?: boolean;
}

// ─── Schools ─────────────────────────────────────────────────────────────────

export type SchoolStage = 'Primary' | 'Intermediate' | 'Secondary';

export interface SchoolListItem {
  id: number;
  name: string;
  stage: string;
  city: string;
  schoolLocationId?: number;
  schoolLocationName?: string;
  regionName?: string;
  latitude?: number;
  longitude?: number;
  locationDetails?: string;
  logoUrl?: string;
  isActive: boolean;
  managerUserId?: string;
  managerFullName?: string;
  activeUserCount: number;
  createdAt: string;
}

export interface SchoolDetail {
  id: number;
  name: string;
  stage: string;
  city: string;
  schoolLocationId?: number;
  schoolLocationName?: string;
  regionName?: string;
  latitude?: number;
  longitude?: number;
  locationDetails?: string;
  logoUrl?: string;
  isActive: boolean;
  managerUserId?: string;
  managerFullName?: string;
  managerUsername?: string;
  createdAt: string;
  updatedAt: string;
  activeUserCount: number;
}

export interface SchoolCreateRequest {
  name: string;
  stage: SchoolStage;
  schoolLocationId: number;
  locationDetails?: string;
  logoUrl?: string;
  managerUserId?: string;
  isActive?: boolean;
}

export interface SchoolUpdateRequest {
  name: string;
  stage: SchoolStage;
  schoolLocationId: number;
  locationDetails?: string;
  logoUrl?: string;
  managerUserId?: string;
}

export interface AssignSchoolManagerRequest {
  userId: string;
}

export interface SchoolListQuery extends PagedQuery {
  city?: string;
  stage?: SchoolStage;
  isActive?: boolean;
}

// ─── Users ──────────────────────────────────────────────────────────────────

export type PhaseTwoRole = 'SchoolManager' | 'Secretary' | 'Moderator' | 'Instructor';

export interface UserSchoolBrief {
  schoolId: number;
  schoolName: string;
  role: string;
}

export interface UserListItem {
  userId: string;
  username: string;
  fullName: string;
  email?: string;
  isActive: boolean;
  roles: string[];
  schools: UserSchoolBrief[];
  createdAt: string;
  lastLoginAt?: string;
}

export interface UserDetail {
  userId: string;
  username: string;
  firstName: string;
  lastName: string;
  fullName: string;
  email?: string;
  phoneNumber?: string;
  preferredLanguage: string;
  isActive: boolean;
  roles: string[];
  schools: UserSchoolBrief[];
  createdAt: string;
  lastLoginAt?: string;
  employeeNumber?: string | null;
  subject?: string | null;
  stage?: SchoolStage | number | null;
  classes: string[];
}

export interface SchoolLocation {
  id: number;
  nameAr: string;
  nameEn?: string;
  regionNameAr: string;
  regionNameEn?: string;
  latitude: number;
  longitude: number;
}

export interface SchoolLocationCreateRequest {
  nameAr: string;
  nameEn?: string;
  regionNameAr: string;
  regionNameEn?: string;
  latitude: number;
  longitude: number;
}

export interface UserCreateRequest {
  username: string;
  password: string;
  firstName: string;
  lastName: string;
  email?: string;
  phoneNumber?: string;
  preferredLanguage?: 'ar' | 'en';
  role: PhaseTwoRole;
  schoolId?: number;
  fullName?: string;
  employeeNumber?: string;
  subject?: string;
  stage?: SchoolStage | number;
  classes?: string[];
}

export interface UserUpdateRequest {
  firstName: string;
  lastName: string;
  email?: string;
  phoneNumber?: string;
  preferredLanguage?: 'ar' | 'en';
  role?: PhaseTwoRole;
  fullName?: string;
  schoolId?: number;
  employeeNumber?: string;
  subject?: string;
  stage?: SchoolStage | number;
  classes?: string[];
}

export interface UserListQuery extends PagedQuery {
  role?: string;
  schoolId?: number;
  isActive?: boolean;
}

// ─── UserSchoolRole ─────────────────────────────────────────────────────────

export interface UserSchoolRoleCreateRequest {
  userId: string;
  schoolId: number;
  role: PhaseTwoRole;
}

export interface UserSchoolRoleDetail {
  id: number;
  userId: string;
  username: string;
  fullName: string;
  schoolId: number;
  schoolName: string;
  roleId: string;
  role: string;
  isActive: boolean;
  createdAt: string;
}
