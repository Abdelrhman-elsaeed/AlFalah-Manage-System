export interface SchoolLookup {
  id: number;
  name: string;
  city: string;
  stage: string;
  logoUrl?: string;
}

export interface SchoolLoginRequestDto {
  schoolId: number;
  username: string;
  password: string;
}

export interface MainManagerLoginRequest {
  username: string;
  password: string;
}

export interface RefreshTokenRequestDto {
  refreshToken: string;
}

export interface UserTokenInfoDto {
  userId: string;
  username: string;
  fullName: string;
  activeSchoolId?: number;
  activeSchoolName?: string;
  preferredLanguage: string;
  roles: string[];
  permissions: string[];
}

export interface AuthResponseDto {
  accessToken: string;
  refreshToken: string;
  accessTokenExpiry: string;
  refreshTokenExpiry: string;
  user: UserTokenInfoDto;
}

export interface CurrentUserDto {
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

// Backwards-compatible aliases for the existing feature code.
export type SchoolLoginRequest = SchoolLoginRequestDto;
export type RefreshTokenRequest = RefreshTokenRequestDto;
export type UserTokenInfo = UserTokenInfoDto;
export type AuthResponse = AuthResponseDto;
export type CurrentUser = CurrentUserDto;
