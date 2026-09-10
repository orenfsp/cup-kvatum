import { apiClient } from './client';
import { staffCsrfToken } from './staffAuth';

export type CrisisQueueItem = {
  id: string;
  caseNumber: string;
  version: number;
  status: string;
  statusText: string;
  priority: 'Low' | 'Standard' | 'Urgent';
  priorityText: string;
  detectedAt: string;
  waitingMinutes: number;
  hasContact: boolean;
  applicantTypeText: string;
  category: string;
  workState: 'Available' | 'Mine' | 'Busy';
  sourceText: string;
  riskTypeText: string;
  narrative: string | null;
  answers: Array<{ questionCode: string; question: string; value: string }>;
  canOpenInPrimaryQueue: boolean;
};

export type CrisisQueueResponse = {
  total: number;
  unconfirmedCount: number;
  items: CrisisQueueItem[];
};

export async function getCrisisQueue(leaseId: string) {
  const response = await apiClient.get<CrisisQueueResponse>('/staff/operator/crisis', {
    headers: { 'X-Operator-Work-Lease': leaseId },
  });
  return response.data;
}

export async function confirmCrisisUrgent(appealId: string, expectedVersion: number, leaseId: string) {
  const token = await staffCsrfToken();
  const response = await apiClient.post(
    `/staff/operator/crisis/${appealId}/urgent`,
    { expectedVersion, leaseId },
    { headers: { 'X-CSRF-TOKEN': token } },
  );
  return response.data;
}

export async function dismissCrisisSignal(appealId: string, expectedVersion: number, reason: string, leaseId: string) {
  const token = await staffCsrfToken();
  const response = await apiClient.post(
    `/staff/operator/crisis/${appealId}/dismiss`,
    { expectedVersion, reason, leaseId },
    { headers: { 'X-CSRF-TOKEN': token } },
  );
  return response.data;
}

export async function readCrisisContact(appealId: string, leaseId: string) {
  const token = await staffCsrfToken();
  const response = await apiClient.post<{ contact: string; accessedAt: string }>(
    `/staff/operator/crisis/${appealId}/contact/read`,
    { leaseId },
    { headers: { 'X-CSRF-TOKEN': token } },
  );
  return response.data;
}
