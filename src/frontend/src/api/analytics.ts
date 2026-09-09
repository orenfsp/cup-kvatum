import axios from 'axios';
import { apiClient } from './client';
import { staffCsrfToken } from './staffAuth';

export type AnalyticsDistribution = {
  key: string;
  label: string;
  count: number;
};

export type AnalyticsDashboard = {
  scope: { role: string; id: string; label: string };
  period: { from: string; to: string };
  generatedAt: string;
  total: number;
  active: number;
  urgentSharePercent: number;
  returnedSharePercent: number;
  averageMinutes: {
    operatorAccepted: number | null;
    firstExpertResponse: number | null;
    closed: number | null;
  };
  distributions: {
    status: AnalyticsDistribution[];
    priority: AnalyticsDistribution[];
    applicantType: AnalyticsDistribution[];
    category: AnalyticsDistribution[];
  };
  workload: Array<{ staffUserId: string; displayName: string; activeCount: number }>;
  definitions: Record<string, string>;
};

export type AnalyticsExport = {
  id: string;
  format: 'csv' | 'xlsx';
  rowCount: number;
  createdAt: string;
  expiresAt: string;
  downloadUrl: string;
};

export async function getAnalyticsDashboard(days: number) {
  const to = new Date();
  const from = new Date(to.getTime() - days * 24 * 60 * 60 * 1000);
  return (await apiClient.get<AnalyticsDashboard>('/staff/analytics/dashboard', {
    params: { from: from.toISOString(), to: to.toISOString() },
  })).data;
}

export async function createAnalyticsExport(format: 'csv' | 'xlsx', days: number) {
  const token = await staffCsrfToken();
  const to = new Date();
  const from = new Date(to.getTime() - days * 24 * 60 * 60 * 1000);
  return (await apiClient.post<AnalyticsExport>('/staff/analytics/exports', {
    format,
    from: from.toISOString(),
    to: to.toISOString(),
  }, { headers: { 'X-CSRF-TOKEN': token } })).data;
}

export async function downloadAnalyticsExport(exportFile: AnalyticsExport) {
  const token = await staffCsrfToken();
  const downloadPath = exportFile.downloadUrl.replace(/^\/api(?=\/)/, '');
  const response = await apiClient.post<Blob>(downloadPath, undefined, {
    headers: { 'X-CSRF-TOKEN': token },
    responseType: 'blob',
  });
  const href = URL.createObjectURL(response.data);
  const anchor = document.createElement('a');
  anchor.href = href;
  anchor.download = filename(response.headers['content-disposition'], `otklik-analytics.${exportFile.format}`);
  document.body.append(anchor);
  anchor.click();
  anchor.remove();
  URL.revokeObjectURL(href);
}

function filename(header: unknown, fallback: string) {
  if (typeof header !== 'string') return fallback;
  const match = /filename\*?=(?:UTF-8''|")?([^";]+)/i.exec(header);
  return match?.[1] ? decodeURIComponent(match[1].replaceAll('"', '')) : fallback;
}

export function analyticsError(error: unknown) {
  if (!axios.isAxiosError(error)) return 'Не удалось получить аналитику. Повторите попытку.';
  const data = error.response?.data as { detail?: string; errors?: Record<string, string[]> } | undefined;
  return (data?.errors && Object.values(data.errors).flat()[0])
    ?? data?.detail
    ?? 'Не удалось получить аналитику. Повторите попытку.';
}
