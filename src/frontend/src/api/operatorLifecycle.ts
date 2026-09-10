import { apiClient } from './client';
import { staffCsrfToken } from './staffAuth';

export type LifecycleWork = {
  returns: Array<{
    id: string;
    version: number;
    returnCount: number;
    returnedAt: string;
    applicantTypeText: string;
    category: string;
    priority: string;
    priorityText: string;
    requiresFinalDecision: boolean;
  }>;
  complaints: Array<{
    id: string;
    appealId: string;
    body: string;
    createdAt: string;
    applicantTypeText: string;
    category: string;
  }>;
  crisisReviews: Array<{ id: string; appealId: string; createdAt: string }>;
};

export type ReturnDetail = LifecycleWork['returns'][number] & {
  returns: Array<{
    id: string;
    returnSequence: number;
    reason: string;
    reasonText: string;
    details: string | null;
    createdAt: string;
  }>;
  complaints: Array<{ id: string; body: string; createdAt: string }>;
  operatorDecisions: Array<{ id: string; decisionText: string; occurredAt: string }>;
  candidates: Array<{ id: string; displayName: string }>;
};

export async function getLifecycleWork() {
  const response = await apiClient.get<LifecycleWork>('/staff/operator/lifecycle');
  return response.data;
}

export async function getReturnDetail(appealId: string) {
  const response = await apiClient.get<ReturnDetail>(`/staff/operator/lifecycle/returns/${appealId}`);
  return response.data;
}

export async function reassignReturnedAppeal(appealId: string, expertId: string, expectedVersion: number) {
  return post(`/staff/operator/lifecycle/returns/${appealId}/reassign`, { expertId, expectedVersion });
}

export async function closeReturnedAppeal(appealId: string, explanation: string, expectedVersion: number) {
  return post(`/staff/operator/lifecycle/returns/${appealId}/close`, { explanation, expectedVersion });
}

export async function resolveComplaint(complaintId: string) {
  return post(`/staff/operator/lifecycle/complaints/${complaintId}/resolve`, { expectedVersion: 1 });
}

async function post(route: string, body: object) {
  const token = await staffCsrfToken();
  const response = await apiClient.post(route, body, { headers: { 'X-CSRF-TOKEN': token } });
  return response.data;
}
