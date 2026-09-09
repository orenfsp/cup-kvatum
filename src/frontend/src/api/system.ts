import { apiClient } from './client';

export type ComponentState = {
  status: 'healthy' | 'degraded' | 'unhealthy';
  description: string | null;
  durationMs: number;
};

export type SystemStatus = {
  status: 'healthy' | 'degraded' | 'unhealthy';
  checkedAt: string;
  durationMs: number;
  components: Record<string, ComponentState>;
};

export async function getSystemStatus(): Promise<SystemStatus> {
  const response = await apiClient.get<SystemStatus>('/system/status');
  return response.data;
}

