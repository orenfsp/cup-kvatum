import axios from 'axios';
import { apiClient } from './client';
import { staffCsrfToken } from './staffAuth';

export type AppealPriority = 'Low' | 'Standard' | 'Urgent';

export type OperatorQueueItem = {
  id: string;
  caseNumber: string;
  version: number;
  status: string;
  statusText: string;
  priority: AppealPriority;
  priorityText: string;
  crisisFlag: boolean;
  receivedAt: string;
  waitingMinutes: number;
  isOverdue: boolean;
  applicantType: string;
  applicantTypeText: string;
  category: string;
  workState: 'Available' | 'Mine' | 'Busy';
  nextAction: string;
  blockingReason: string | null;
  hasAttachments: boolean;
  lastActivityAt: string;
};

export type OperatorQueueResponse = {
  total: number;
  overdueCount: number;
  overdueHours: number;
  sort: string;
  items: OperatorQueueItem[];
};

export type OperatorAppealDetails = {
  id: string;
  caseNumber: string;
  version: number;
  status: string;
  statusText: string;
  priority: AppealPriority;
  priorityText: string;
  crisisFlag: boolean;
  receivedAt: string;
  applicantType: string;
  applicantTypeText: string;
  submissionPath: string;
  categoryId: string | null;
  category: string | null;
  narrative: string | null;
  answers: Array<{ questionCode: string; question: string; value: string }>;
  attachments: Array<{
    id: string;
    displayName: string;
    contentType: string;
    size: number;
    createdAt: string;
  }>;
  categories: Array<{ id: string; displayName: string }>;
  suggestion: { categoryId: string; category: string; reason: string } | null;
  routing: {
    groupId: string | null;
    groupName: string | null;
    warning: string | null;
    experts: Array<{
      id: string;
      displayName: string;
      activeCount: number;
      limit: number;
      atCapacity: boolean;
    }>;
  };
};

export async function getOperatorQueue(sort: string, leaseId: string) {
  const response = await apiClient.get<OperatorQueueResponse>('/staff/operator/queue', {
    params: { sort },
    headers: { 'X-Operator-Work-Lease': leaseId },
  });
  return response.data;
}

export async function getOperatorAppeal(appealId: string) {
  const response = await apiClient.get<OperatorAppealDetails>(`/staff/operator/queue/${appealId}`);
  return response.data;
}

export async function triageOperatorAppeal(
  appealId: string,
  request: { categoryId: string; priority: AppealPriority; expectedVersion: number; reason?: string; leaseId: string },
) {
  return postOperatorAction(appealId, 'triage', request);
}

export async function assignOperatorAppeal(
  appealId: string,
  request: {
    expertId: string;
    expectedVersion: number;
    allowOverCapacity: boolean;
    overrideReason?: string;
    leaseId: string;
  },
) {
  return postOperatorAction(appealId, 'assign', request);
}

export async function rejectOperatorAppeal(
  appealId: string,
  request: { reasonCode: 'Spam' | 'OutOfScope'; internalReason: string; expectedVersion: number; leaseId: string },
) {
  return postOperatorAction(appealId, 'reject', request);
}

export async function resolveOperatorAppeal(
  appealId: string,
  request: { message: string; expectedVersion: number; leaseId: string },
) {
  return postOperatorAction(appealId, 'resolve', request);
}

async function postOperatorAction(appealId: string, action: string, request: object) {
  const token = await staffCsrfToken();
  const response = await apiClient.post(`/staff/operator/queue/${appealId}/${action}`, request, {
    headers: { 'X-CSRF-TOKEN': token },
  });
  return response.data;
}

export async function downloadOperatorAttachment(
  appealId: string,
  attachment: OperatorAppealDetails['attachments'][number],
) {
  const response = await apiClient.get<Blob>(
    `/staff/operator/queue/${appealId}/attachments/${attachment.id}`,
    { responseType: 'blob' },
  );
  const url = URL.createObjectURL(response.data);
  const link = document.createElement('a');
  link.href = url;
  link.download = attachment.displayName;
  link.click();
  URL.revokeObjectURL(url);
}

export function operatorError(error: unknown) {
  if (!axios.isAxiosError(error)) return 'Не удалось выполнить действие. Повторите попытку.';
  const data = error.response?.data as {
    detail?: string;
    errors?: Record<string, string[]>;
  } | undefined;
  const validation = data?.errors ? Object.values(data.errors).flat()[0] : undefined;
  return validation ?? data?.detail ?? 'Не удалось выполнить действие. Повторите попытку.';
}

export function isOperatorStaleConflict(error: unknown) {
  if (!axios.isAxiosError(error) || error.response?.status !== 409) return false;
  const data = error.response.data as {
    capacityOverrideRequired?: boolean;
    operatorLeaseConflict?: boolean;
    operatorQueueExhausted?: boolean;
  } | undefined;
  return data?.capacityOverrideRequired !== true
    && data?.operatorLeaseConflict !== true
    && data?.operatorQueueExhausted !== true;
}

export function isOperatorLeaseConflict(error: unknown) {
  if (!axios.isAxiosError(error) || error.response?.status !== 409) return false;
  return (error.response.data as { operatorLeaseConflict?: boolean } | undefined)?.operatorLeaseConflict === true;
}

export type OperatorWorkLease = {
  appealId: string;
  state: 'Mine';
  leaseSeconds: number;
};

export type OperatorWorkSelection =
  | { scope: 'queue'; priority?: AppealPriority }
  | { scope: 'crisis'; priority?: never };

const OPERATOR_WORK_LEASE_KEY = 'otklik:operator-work-lease';
let memoryLeaseId: string | null = null;

export function getOperatorWorkLeaseId() {
  if (memoryLeaseId) return memoryLeaseId;
  const stored = window.sessionStorage.getItem(OPERATOR_WORK_LEASE_KEY);
  memoryLeaseId = stored || crypto.randomUUID();
  if (!stored) window.sessionStorage.setItem(OPERATOR_WORK_LEASE_KEY, memoryLeaseId);
  return memoryLeaseId;
}

export async function acquireOperatorWork(appealId: string, leaseId: string) {
  return postOperatorWork<OperatorWorkLease>(`${appealId}/acquire`, { leaseId });
}

export async function heartbeatOperatorWork(appealId: string, leaseId: string) {
  return postOperatorWork<OperatorWorkLease>(`${appealId}/heartbeat`, { leaseId });
}

export async function releaseOperatorWork(appealId: string, leaseId: string) {
  return postOperatorWork<void>(`${appealId}/release`, { leaseId });
}

export async function acquireNextOperatorWork(leaseId: string, selection: OperatorWorkSelection) {
  return postOperatorWork<OperatorWorkLease>('acquire-next', { leaseId, ...selection });
}

async function postOperatorWork<T>(path: string, request: object) {
  const token = await staffCsrfToken();
  const response = await apiClient.post<T>(`/staff/operator/work/${path}`, request, {
    headers: { 'X-CSRF-TOKEN': token },
  });
  return response.data;
}
