export interface LoginRequest {
  phone: string;
  password: string;
}

export interface RegisterRequest {
  fullName: string;
  phone: string;
  password: string;
  householdName: string;
  productionAddress: string;
  contactInfo: string | null;
}
