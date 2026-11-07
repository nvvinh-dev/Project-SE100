export interface LoginRequest {
  phone: string;
  password: string;
}

export interface UpdateProfileRequest {
  fullName: string;
  phone: string;
}

export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
}

export interface RegisterRequest {
  fullName: string;
  phone: string;
  password: string;
  householdName: string;
  productionAddress: string;
  contactInfo: string | null;
}
