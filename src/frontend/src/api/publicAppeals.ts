import axios from 'axios';
import { apiClient } from './client';

export type ApplicantType = 'Student' | 'Parent' | 'Teacher';
export type SubmissionPath = 'FreeText' | 'Category';

export type IntakeCategory = {
  id: string;
  code: string;
  displayName: string;
};

export type IntakeQuestion = {
  code: string;
  studentText: string;
  adultText: string;
};

export type CrisisSupportContact = {
  displayName: string;
  displayNumber: string;
  dialNumber: string;
  description: string;
};

export type IntakeOptions = {
  applicantTypes: Array<{ value: ApplicantType; label: string }>;
  submissionPaths: Array<{ value: SubmissionPath; label: string }>;
  categories: IntakeCategory[];
  questions: IntakeQuestion[];
  crisisMarkers: string[];
  crisisSupport: CrisisSupportContact[];
};

export type CreateAppealRequest = {
  clientRequestId: string;
  applicantType: ApplicantType;
  submissionPath: SubmissionPath;
  categoryId: string | null;
  narrative: string | null;
  answers: Record<string, string>;
  crisisContact: string | null;
};

export type CreateAppealResponse = {
  appealId: string;
  trackNumber: string;
  status: string;
  statusText: string;
  createdAt: string;
};

export type AppealStatusResponse = {
  threadVersion: number;
  cycleCount: number;
  summary: {
    createdAt: string;
    lastActivityAt: string;
    currentCycle: number;
    status: string;
    statusText: string;
  };
  currentCycle: AppealCycle;
  cycles: AppealCycle[];
  permissions: {
    canContinue: boolean;
    canReply: boolean;
    canMarkOutcome: boolean;
    requiresFinalOperatorDecision: boolean;
    canLeaveFeedback: boolean;
    feedbackSubmitted: boolean;
    complaintSubmitted: boolean;
  };
  status: string;
  statusText: string;
  version: number;
  returnCount: number;
  createdAt: string;
  applicantType: ApplicantType;
  category: string | null;
  resolution: string | null;
  canReply: boolean;
  canMarkOutcome: boolean;
  requiresFinalOperatorDecision: boolean;
  canLeaveFeedback: boolean;
  feedbackSubmitted: boolean;
  complaintSubmitted: boolean;
  needsImmediateHelp: boolean;
  crisisSupport: CrisisSupportContact[];
  messages: AppealPublicMessage[];
  recommendations: AppealRecommendation[];
  attachments: AppealAttachment[];
  timeline: Array<{ status: string; text: string; at: string }>;
};

export type AppealCycle = {
  id: string;
  number: number;
  status: string;
  statusText: string;
  version: number;
  returnCount: number;
  createdAt: string;
  completedAt: string | null;
  applicantType: ApplicantType;
  category: string | null;
  narrative: string | null;
  resolution: string | null;
  messages: AppealPublicMessage[];
  recommendations: AppealRecommendation[];
  attachments: AppealAttachment[];
  timeline: Array<{ status: string; text: string; at: string }>;
};

export type AppealPublicMessage = {
  id: string;
  author: 'Applicant' | 'Expert';
  authorLabel: string;
  body: string;
  createdAt: string;
};

export type AppealRecommendation = {
  id: string;
  version: number;
  body: string;
  createdAt: string;
};

export type AppealAttachment = {
  id: string;
  displayName: string;
  contentType: string;
  size: number;
  createdAt: string;
};

export async function getIntakeOptions() {
  const response = await apiClient.get<IntakeOptions>('/public/intake/options');
  return response.data;
}

export async function createAppeal(request: CreateAppealRequest) {
  const response = await apiClient.post<CreateAppealResponse>('/public/appeals', request);
  return response.data;
}

export async function getAppealStatus(trackNumber: string) {
  const response = await apiClient.post<AppealStatusResponse>('/public/appeals/open', { trackNumber });
  return response.data;
}

export async function continueAppeal(request: {
  trackNumber: string;
  clientContinuationId: string;
  expectedThreadVersion: number;
  body: string;
  crisisContact?: string;
}) {
  const response = await apiClient.post<{
    threadVersion: number;
    cycleNumber: number;
    status: string;
    statusText: string;
    createdAt: string;
  }>('/public/appeals/continue', {
    ...request,
    crisisContact: request.crisisContact || null,
  });
  return response.data;
}

export async function addApplicantMessage(
  trackNumber: string,
  clientMessageId: string,
  body: string,
  crisisContact?: string,
) {
  const response = await apiClient.post('/public/appeals/messages', {
    trackNumber,
    clientMessageId,
    body,
    crisisContact: crisisContact || null,
  });
  return response.data;
}

export async function markAppealHelped(trackNumber: string, clientActionId: string, expectedVersion: number) {
  const response = await apiClient.post('/public/appeals/outcomes/helped', {
    trackNumber, clientActionId, expectedVersion,
  });
  return response.data;
}

export async function returnAppeal(trackNumber: string, request: {
  clientActionId: string;
  expectedVersion: number;
  reason: string;
  details?: string;
}) {
  const response = await apiClient.post('/public/appeals/outcomes/returned', { trackNumber, ...request });
  return response.data;
}

export async function leaveAppealFeedback(trackNumber: string, request: {
  clientFeedbackId: string;
  score: number;
  comment?: string;
}) {
  const response = await apiClient.post('/public/appeals/feedback', { trackNumber, ...request });
  return response.data;
}

export async function submitAppealComplaint(trackNumber: string, clientComplaintId: string, body: string) {
  const response = await apiClient.post('/public/appeals/complaints', { trackNumber, clientComplaintId, body });
  return response.data;
}

export async function uploadAppealAttachment(
  trackNumber: string,
  clientUploadId: string,
  file: File,
  onProgress: (percent: number) => void,
) {
  const form = new FormData();
  form.append('trackNumber', trackNumber);
  form.append('clientUploadId', clientUploadId);
  form.append('file', file, file.name);
  const response = await apiClient.post<AppealAttachment>('/public/appeals/attachments', form, {
    timeout: 30_000,
    onUploadProgress: (event) => {
      if (event.total) onProgress(Math.round((event.loaded / event.total) * 100));
    },
  });
  return response.data;
}

export async function downloadAppealAttachment(trackNumber: string, attachment: AppealAttachment) {
  const response = await apiClient.post<Blob>(
    `/public/appeals/attachments/${attachment.id}/download`,
    { trackNumber },
    { responseType: 'blob', timeout: 30_000 },
  );
  const url = URL.createObjectURL(response.data);
  const link = document.createElement('a');
  link.href = url;
  link.download = attachment.displayName;
  link.click();
  URL.revokeObjectURL(url);
}

export function publicAppealError(error: unknown) {
  if (axios.isAxiosError(error) && error.response?.status === 404) {
    return 'Не удалось найти обращение. Проверьте номер или оставьте новое обращение.';
  }

  if (axios.isAxiosError(error) && error.response?.status === 400) {
    const attachmentMessage = error.response.data?.errors?.attachment?.[0];
    if (typeof attachmentMessage === 'string') return attachmentMessage;
    return 'Проверьте заполненные поля. Можно пропустить все уточняющие вопросы.';
  }

  if (axios.isAxiosError(error) && (error.response?.status === 409 || error.response?.status === 423)) {
    const detail = error.response.data?.detail;
    return typeof detail === 'string' ? detail : 'Статус уже изменился. Обновите обращение.';
  }

  if (axios.isAxiosError(error) && error.response?.status === 413) {
    return 'Файл больше 10 МБ. Выберите файл меньшего размера.';
  }

  if (axios.isAxiosError(error) && error.response?.status === 429) {
    return 'Слишком много попыток. Подождите немного — это защищает обращения от подбора номера.';
  }

  return 'Сейчас не получается связаться с сервисом. Ваш текст останется на этой странице — попробуйте еще раз.';
}
