import axios from 'axios';
import { apiClient } from './client';
import { staffCsrfToken } from './staffAuth';

export type AppealPriority = 'Low' | 'Standard' | 'Urgent';

export type OperatorQueueItem = {
  id: string;
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

export async function getOperatorQueue(sort: string) {
  const response = await apiClient.get<OperatorQueueResponse>('/staff/operator/queue', {
    params: { sort },
  });
  return response.data;
}

export async function getOperatorAppeal(appealId: string) {
  const response = await apiClient.get<OperatorAppealDetails>(`/staff/operator/queue/${appealId}`);
  return response.data;
}

export async function triageOperatorAppeal(
  appealId: string,
  request: { categoryId: string; priority: AppealPriority; expectedVersion: number; reason?: string },
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
  },
) {
  return postOperatorAction(appealId, 'assign', request);
}

export async function rejectOperatorAppeal(
  appealId: string,
  request: { reasonCode: 'Spam' | 'OutOfScope'; internalReason: string; expectedVersion: number },
) {
  return postOperatorAction(appealId, 'reject', request);
}

export async function resolveOperatorAppeal(
  appealId: string,
  request: { message: string; expectedVersion: number },
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
