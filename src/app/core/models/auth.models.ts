export interface SchoolLookup {
  id: number;
  name: string;
  city: string;
  stage: string;
  logoUrl?: string;
}

export interface SchoolLoginRequest {
  schoolId: number;
  username: string;
  password: string;
}

export interface MainManagerLoginRequest {
  username: string;
  password: string;
}

export interface RefreshTokenRequest {
  refreshToken: string;
}

export interface UserTokenInfo {
  userId: string;
  username: string;
  fullName: string;
  activeSchoolId?: number;
  activeSchoolName?: string;
  preferredLanguage: string;
  roles: string[];
  permissions: string[];
}

export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  accessTokenExpiry: string;
  refreshTokenExpiry: string;
  user: UserTokenInfo;
}

export interface CurrentUser {
  userId: string;
  username: string;
  fullName: string;
  email?: string;
  preferredLanguage: string;
  activeSchoolId?: number;
  activeSchoolName?: string;
  roles: string[];
  permissions: string[];
}
