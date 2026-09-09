import { apiClient } from './client';
import { staffCsrfToken } from './staffAuth';

export type CollaborationRequestItem = {
  id: string;
  version: number;
  type: 'Transfer' | 'CoExecutor' | 'PriorityReview';
  typeText: string;
  status: 'Pending' | 'Approved' | 'Rejected';
  statusText: string;
  reason: string;
  requestedAt: string;
  requestedBy: string;
  appealId: string;
  applicantTypeText: string;
  category: string;
  priority: string;
  priorityText: string;
};

export type CollaborationRequestDetail = CollaborationRequestItem & {
  appeal: { id: string; version: number; applicantTypeText: string; category: string; priority: string; priorityText: string; status: string };
  candidates: Array<{ id: string; displayName: string }>;
  participants: Array<{ id: string; expertId: string; displayName: string; role: string; roleText: string }>;
};

export async function getCollaborationRequests() {
  const response = await apiClient.get<{ total: number; items: CollaborationRequestItem[] }>('/staff/operator/collaboration/requests');
  return response.data;
}

export async function getCollaborationRequest(requestId: string) {
  const response = await apiClient.get<CollaborationRequestDetail>(`/staff/operator/collaboration/requests/${requestId}`);
  return response.data;
}

export async function approveCollaborationRequest(requestId: string, request: {
  expertId?: string;
  priority?: string;
  keepPreviousAsCoExecutor: boolean;
  decisionReason?: string;
  expectedVersion: number;
}) {
  const token = await staffCsrfToken();
  const response = await apiClient.post(`/staff/operator/collaboration/requests/${requestId}/approve`, request,
    { headers: { 'X-CSRF-TOKEN': token } });
  return response.data;
}

export async function rejectCollaborationRequest(requestId: string, decisionReason: string, expectedVersion: number) {
  const token = await staffCsrfToken();
  const response = await apiClient.post(`/staff/operator/collaboration/requests/${requestId}/reject`,
    { decisionReason, expectedVersion }, { headers: { 'X-CSRF-TOKEN': token } });
  return response.data;
}

export async function removeCoExecutor(appealId: string, participantId: string, reason: string, expectedAppealVersion: number) {
  const token = await staffCsrfToken();
  const response = await apiClient.post(
    `/staff/operator/collaboration/appeals/${appealId}/participants/${participantId}/remove`,
    { reason, expectedAppealVersion },
    { headers: { 'X-CSRF-TOKEN': token } },
  );
  return response.data;
}
