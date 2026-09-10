import { apiClient } from './client';
import { staffCsrfToken } from './staffAuth';

export type ExpertAppealItem = {
  id: string;
  version: number;
  status: string;
  statusText: string;
  priority: string;
  priorityText: string;
  applicantType: string;
  applicantTypeText: string;
  categoryId: string | null;
  category: string;
  receivedAt: string;
  assignedAt: string | null;
  role: 'Responsible' | 'CoExecutor';
  roleText: string;
  specialization: string;
  nextAction: string;
  lastActivityAt: string;
};

export type ExpertWorkSummary = {
  inbox: number;
  active: number;
  waiting: number;
  completed: number;
  actionRequired: number;
};

export type ExpertFilter = { value: string; label: string };

export type ExpertAppealList = {
  total: number;
  items: ExpertAppealItem[];
  filters: {
    statuses: ExpertFilter[];
    priorities: ExpertFilter[];
    categories: Array<{ id: string; displayName: string }>;
  };
};

export type ExpertAppealFilters = {
  status: string;
  priority: string;
  categoryId: string;
};

export type ExpertAttachment = {
  id: string;
  displayName: string;
  contentType: string;
  size: number;
  createdAt: string;
};

export type ExpertAppealDetail = ExpertAppealItem & {
  submissionPath: string;
  submissionPathText: string;
  sequence: number;
  nextAction: string;
  narrative: string | null;
  answers: Array<{ questionCode: string; question: string; value: string }>;
  attachments: ExpertAttachment[];
  timeline: Array<{ status: string; text: string; at: string }>;
  messages: Array<{ id: string; author: string; authorLabel: string; body: string; createdAt: string }>;
  notes: Array<{ id: string; body: string; createdAt: string }>;
  recommendations: Array<{ id: string; version: number; body: string; createdAt: string }>;
  participants: Array<{ id: string; expertId: string; displayName: string; role: string; roleText: string }>;
  assignmentHistory: Array<{
    id: string;
    eventType: string;
    displayName: string;
    role: string;
    roleText: string;
    occurredAt: string;
  }>;
  workflowRequests: ExpertWorkflowRequest[];
  previousCycles: Array<{
    sequence: number;
    statusText: string;
    startedAt: string;
    completedAt: string | null;
    narrative: string | null;
    messages: Array<{ id: string; author: string; authorLabel: string; body: string; createdAt: string }>;
    recommendations: Array<{ id: string; version: number; body: string; createdAt: string }>;
  }>;
};

export type ExpertWorkflowRequest = {
  id: string;
  version: number;
  type: 'Transfer' | 'CoExecutor' | 'PriorityReview';
  typeText: string;
  status: 'Pending' | 'Approved' | 'Rejected';
  statusText: string;
  reason: string;
  requestedAt: string;
  decisionReason: string | null;
  decidedAt: string | null;
};

export type ExpertPresence = {
  active: Array<{ id: string; displayName: string; isCurrent: boolean }>;
  composer: { expertId: string; expertName: string; isCurrent: boolean } | null;
  leaseSeconds: number;
};

export type ExpertVersionResult = {
  id: string;
  version: number;
  status: string;
  statusText: string;
};

export async function getExpertAppeals(filters: ExpertAppealFilters) {
  const response = await apiClient.get<ExpertAppealList>('/staff/expert/appeals', {
    params: {
      status: filters.status || undefined,
      priority: filters.priority || undefined,
      categoryId: filters.categoryId || undefined,
    },
  });
  return response.data;
}

export async function getExpertWorkSummary() {
  const response = await apiClient.get<ExpertWorkSummary>('/staff/expert/appeals/work-summary');
  return response.data;
}

export async function getExpertAppeal(appealId: string) {
  const response = await apiClient.get<ExpertAppealDetail>(`/staff/expert/appeals/${appealId}`);
  return response.data;
}

export async function acceptExpertAppeal(appealId: string, expectedVersion: number) {
  const token = await staffCsrfToken();
  const response = await apiClient.post<ExpertVersionResult>(
    `/staff/expert/appeals/${appealId}/accept`,
    { expectedVersion },
    { headers: { 'X-CSRF-TOKEN': token } },
  );
  return response.data;
}

export async function addExpertNote(appealId: string, clientNoteId: string, body: string, leaseId?: string) {
  const token = await staffCsrfToken();
  const response = await apiClient.post<{ id: string; body: string; createdAt: string }>(
    `/staff/expert/appeals/${appealId}/notes`,
    { clientNoteId, body, leaseId },
    { headers: { 'X-CSRF-TOKEN': token } },
  );
  return response.data;
}

export async function askExpertQuestion(
  appealId: string,
  clientMessageId: string,
  body: string,
  expectedVersion: number,
  leaseId?: string,
) {
  const token = await staffCsrfToken();
  const response = await apiClient.post(
    `/staff/expert/appeals/${appealId}/questions`,
    { clientMessageId, body, expectedVersion, leaseId },
    { headers: { 'X-CSRF-TOKEN': token } },
  );
  return response.data;
}

export async function publishExpertRecommendation(
  appealId: string,
  clientRecommendationId: string,
  body: string,
  expectedVersion: number,
  leaseId?: string,
) {
  const token = await staffCsrfToken();
  const response = await apiClient.post(
    `/staff/expert/appeals/${appealId}/recommendations`,
    { clientRecommendationId, body, expectedVersion, leaseId },
    { headers: { 'X-CSRF-TOKEN': token } },
  );
  return response.data;
}

export async function createExpertWorkflowRequest(
  appealId: string,
  request: { clientRequestId: string; type: ExpertWorkflowRequest['type']; reason: string },
) {
  const token = await staffCsrfToken();
  const response = await apiClient.post<ExpertWorkflowRequest>(
    `/staff/expert/appeals/${appealId}/workflow-requests`,
    request,
    { headers: { 'X-CSRF-TOKEN': token } },
  );
  return response.data;
}

export async function heartbeatExpertPresence(appealId: string) {
  const token = await staffCsrfToken();
  const response = await apiClient.post<ExpertPresence>(
    `/staff/expert/appeals/${appealId}/presence`,
    {},
    { headers: { 'X-CSRF-TOKEN': token } },
  );
  return response.data;
}

export async function getExpertPresence(appealId: string) {
  const response = await apiClient.get<ExpertPresence>(`/staff/expert/appeals/${appealId}/presence`);
  return response.data;
}

export async function acquireExpertComposer(appealId: string, leaseId: string) {
  const token = await staffCsrfToken();
  const response = await apiClient.post<ExpertPresence>(
    `/staff/expert/appeals/${appealId}/composer/acquire`,
    { leaseId },
    { headers: { 'X-CSRF-TOKEN': token } },
  );
  return response.data;
}

export async function releaseExpertComposer(appealId: string, leaseId: string) {
  const token = await staffCsrfToken();
  await apiClient.post(
    `/staff/expert/appeals/${appealId}/composer/release`,
    { leaseId },
    { headers: { 'X-CSRF-TOKEN': token } },
  );
}

export async function downloadExpertAttachment(appealId: string, attachment: ExpertAttachment) {
  const response = await apiClient.get<Blob>(
    `/staff/expert/appeals/${appealId}/attachments/${attachment.id}`,
    { responseType: 'blob', timeout: 30_000 },
  );
  const url = URL.createObjectURL(response.data);
  const link = document.createElement('a');
  link.href = url;
  link.download = attachment.displayName;
  link.click();
  URL.revokeObjectURL(url);
}
