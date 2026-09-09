import { apiClient } from './client';
import { staffCsrfToken } from './staffAuth';

export type CrisisQueueItem = {
  id: string;
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
  canOpenInPrimaryQueue: boolean;
};

export type CrisisQueueResponse = {
  total: number;
  unconfirmedCount: number;
  items: CrisisQueueItem[];
};

export async function getCrisisQueue() {
  const response = await apiClient.get<CrisisQueueResponse>('/staff/operator/crisis');
  return response.data;
}

export async function confirmCrisisUrgent(appealId: string, expectedVersion: number) {
  const token = await staffCsrfToken();
  const response = await apiClient.post(
    `/staff/operator/crisis/${appealId}/urgent`,
    { expectedVersion },
    { headers: { 'X-CSRF-TOKEN': token } },
  );
  return response.data;
}

export async function readCrisisContact(appealId: string) {
  const token = await staffCsrfToken();
  const response = await apiClient.post<{ contact: string; accessedAt: string }>(
    `/staff/operator/crisis/${appealId}/contact/read`,
    {},
    { headers: { 'X-CSRF-TOKEN': token } },
  );
  return response.data;
}
