import axios from 'axios';
import { apiClient } from './client';

export type StaffRole = 'Operator' | 'Expert' | 'Administrator';

export type StaffSession = {
  id: string;
  userName: string;
  displayName: string;
  roles: StaffRole[];
};

export type RoleOverview = {
  role: StaffRole;
  title: string;
};

export async function staffCsrfToken() {
  const response = await apiClient.get<{ token: string }>('/staff/auth/csrf');
  return response.data.token;
}

export async function currentStaff(): Promise<StaffSession | null> {
  try {
    const response = await apiClient.get<StaffSession>('/staff/auth/me');
    return response.data;
  } catch (error) {
    if (axios.isAxiosError(error) && error.response?.status === 401) {
      return null;
    }

    throw error;
  }
}

export async function loginStaff(userName: string, password: string) {
  const token = await staffCsrfToken();
  const response = await apiClient.post<StaffSession>(
    '/staff/auth/login',
    { userName, password },
    { headers: { 'X-CSRF-TOKEN': token } },
  );
  return response.data;
}

export async function logoutStaff() {
  const token = await staffCsrfToken();
  await apiClient.post('/staff/auth/logout', undefined, {
    headers: { 'X-CSRF-TOKEN': token },
  });
}

export async function getRoleOverview(role: StaffRole) {
  const routeByRole: Record<StaffRole, string> = {
    Operator: 'operator',
    Expert: 'expert',
    Administrator: 'administrator',
  };
  const response = await apiClient.get<RoleOverview>(`/staff/${routeByRole[role]}/overview`);
  return response.data;
}
