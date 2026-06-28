export const CREDENTIAL_TYPES = [
  'SqlConnectionString',
  'ApiKey',
  'BearerToken',
  'BasicAuth',
  'UsernamePassword',
  'GenericSecret',
] as const;

export type CredentialType = typeof CREDENTIAL_TYPES[number];

export interface CredentialDto {
  id: string;
  name: string;
  type: CredentialType | string;
  description: string | null;
  createdBy: string | null;
  isActive: boolean;
  expiresAt: string | null;
  createdAt: string;
  updatedAt: string | null;
}

export interface CreateCredentialRequest {
  name: string;
  type: string;
  description?: string;
  secretValue: string;
  createdBy?: string;
}

export interface UpdateCredentialRequest {
  name: string;
  type: string;
  description?: string;
  secretValue?: string;
  isActive?: boolean;
  expiresAt?: string | null;
}

export interface CredentialTestResponse {
  success: boolean;
  message: string;
}
