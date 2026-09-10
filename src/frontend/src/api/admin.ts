import axios from 'axios';
import { apiClient } from './client';
import { staffCsrfToken, type StaffRole } from './staffAuth';

export type AdminCategory = {
  id: string;
  code: string;
  displayName: string;
  sortOrder: number;
  isActive: boolean;
  version: number;
  updatedAt: string;
  usageCount: number;
};

export type AdminExpert = {
  id: string;
  userName: string;
  displayName: string;
  isActive: boolean;
  isAvailable: boolean;
};

export type AdminGroup = {
  id: string;
  code: string;
  displayName: string;
  activeAppealLimit: number;
  isActive: boolean;
  version: number;
  updatedAt: string;
  memberIds: string[];
};

export type AdminRule = {
  id: string;
  categoryId: string;
  category: string;
  expertGroupId: string;
  expertGroup: string;
  version: number;
  isActive: boolean;
  updatedAt: string;
};

export type AdminConfiguration = {
  categories: AdminCategory[];
  groups: AdminGroup[];
  rules: AdminRule[];
  experts: AdminExpert[];
};

export type AdminUser = {
  id: string;
  userName: string;
  displayName: string;
  role: StaffRole;
  isActive: boolean;
  isAvailable: boolean;
  createdAt: string;
  updatedAt: string;
};

export type StuckAppeal = {
  id: string;
  version: number;
  categoryId: string | null;
  category: string;
  status: string;
  priority: string;
  assignedExpertId: string | null;
  assignedExpert: string | null;
  createdAt: string;
  lastStatusChangedAt: string;
  waitingHours: number;
  alertTypes: string[];
};

export type AuditListItem = {
  id: string;
  actorUserId: string;
  actorDisplayName: string;
  action: string;
  targetType: string;
  targetId: string;
  reason: string | null;
  occurredAt: string;
};

export type AuditList = {
  total: number;
  items: AuditListItem[];
  filters: { actions: string[]; targetTypes: string[] };
};

export type AuditDetail = AuditListItem & {
  before: Record<string, unknown> | null;
  after: Record<string, unknown> | null;
};

export async function getAdminConfiguration() {
  return (await apiClient.get<AdminConfiguration>('/staff/administrator/configuration')).data;
}

export async function createAdminCategory(request: { code: string; displayName: string; sortOrder: number }) {
  return adminPost<AdminCategory>('/staff/administrator/categories', request);
}

export async function updateAdminCategory(categoryId: string, request: {
  code: string;
  displayName: string;
  sortOrder: number;
  expectedVersion: number;
}) {
  return adminPost<AdminCategory>(`/staff/administrator/categories/${categoryId}/update`, request);
}

export async function deactivateAdminCategory(categoryId: string, expectedVersion: number) {
  return adminPost(`/staff/administrator/categories/${categoryId}/deactivate`, { expectedVersion });
}

export async function createAdminGroup(request: {
  code: string;
  displayName: string;
  activeAppealLimit: number;
  expertIds: string[];
}) {
  return adminPost<AdminGroup>('/staff/administrator/groups', request);
}

export async function updateAdminGroup(groupId: string, request: {
  code: string;
  displayName: string;
  activeAppealLimit: number;
  expertIds: string[];
  expectedVersion: number;
}) {
  return adminPost<AdminGroup>(`/staff/administrator/groups/${groupId}/update`, request);
}

export async function deactivateAdminGroup(groupId: string, expectedVersion: number) {
  return adminPost(`/staff/administrator/groups/${groupId}/deactivate`, { expectedVersion });
}

export async function saveAdminRule(request: {
  categoryId: string;
  expertGroupId: string;
  expectedVersion: number | null;
}) {
  return adminPost<AdminRule>('/staff/administrator/rules', request);
}

export async function deactivateAdminRule(ruleId: string, expectedVersion: number) {
  return adminPost(`/staff/administrator/rules/${ruleId}/deactivate`, { expectedVersion });
}

export async function getAdminUsers() {
  return (await apiClient.get<{ total: number; items: AdminUser[] }>('/staff/administrator/users')).data;
}

export async function createAdminUser(request: {
  userName: string;
  displayName: string;
  password: string;
  role: StaffRole;
  isAvailable: boolean;
}) {
  return adminPost<AdminUser>('/staff/administrator/users', request);
}

export async function updateAdminUser(userId: string, request: {
  displayName: string;
  role: StaffRole;
  isAvailable: boolean;
}) {
  return adminPost<AdminUser>(`/staff/administrator/users/${userId}/update`, request);
}

export async function changeAdminUserState(userId: string, isActive: boolean, reason?: string) {
  const action = isActive ? 'restore' : 'block';
  return adminPost<AdminUser>(`/staff/administrator/users/${userId}/${action}`, { reason });
}

export async function getStuckAppeals(filters: { status?: string; priority?: string }) {
  return (await apiClient.get<{ total: number; stuckHours: number; items: StuckAppeal[] }>(
    '/staff/administrator/stuck',
    { params: filters },
  )).data;
}

export async function interveneInStuckAppeal(appealId: string, request: {
  status: string;
  priority: string;
  assignedExpertId: string | null;
  expectedVersion: number;
  reason: string;
}) {
  return adminPost<{
    id: string;
    version: number;
    status: string;
    priority: string;
    assignedExpertId: string | null;
    assignedExpert: string | null;
    changedAt: string;
    auditEventId: string;
  }>(`/staff/administrator/stuck/${appealId}/intervene`, request);
}

export async function getAdminAudit(filters: { action?: string; targetType?: string }) {
  return (await apiClient.get<AuditList>('/staff/administrator/audit', { params: filters })).data;
}

export async function getAdminAuditDetail(eventId: string) {
  return (await apiClient.get<AuditDetail>(`/staff/administrator/audit/${eventId}`)).data;
}

async function adminPost<T = unknown>(route: string, body: object) {
  const token = await staffCsrfToken();
  return (await apiClient.post<T>(route, body, { headers: { 'X-CSRF-TOKEN': token } })).data;
}

export function adminError(error: unknown) {
  if (!axios.isAxiosError(error)) return 'Не удалось выполнить действие. Повторите попытку.';
  const data = error.response?.data as { detail?: string; errors?: Record<string, string[]> } | undefined;
  return (data?.errors && Object.values(data.errors).flat()[0])
    ?? data?.detail
    ?? 'Не удалось выполнить действие. Повторите попытку.';
}
