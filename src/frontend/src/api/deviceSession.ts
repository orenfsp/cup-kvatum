import axios from 'axios';
import { apiClient } from './client';
import type { AppealStatusResponse } from './publicAppeals';

export type DevicePushConfig = {
  publicKey: string;
  subscribed: boolean;
  notificationTitle: string;
  notificationBody: string;
};

async function deviceCsrfToken() {
  return (await apiClient.get<{ token: string }>('/public/device-session/csrf')).data.token;
}

export async function getDeviceAppealStatus(): Promise<AppealStatusResponse | null> {
  const response = await apiClient.get<AppealStatusResponse | ''>('/public/device-session');
  return response.status === 204 ? null : response.data as AppealStatusResponse;
}

export async function getDevicePushConfig() {
  return (await apiClient.get<DevicePushConfig>('/public/device-session/push-config')).data;
}

export async function saveDevicePushSubscription(subscription: PushSubscription) {
  const json = subscription.toJSON();
  if (!json.endpoint || !json.keys?.p256dh || !json.keys.auth) {
    throw new Error('Browser returned an incomplete push subscription.');
  }
  const token = await deviceCsrfToken();
  return (await apiClient.post<{ subscribed: boolean }>('/public/device-session/push-subscriptions', {
    endpoint: json.endpoint,
    keys: { p256dh: json.keys.p256dh, auth: json.keys.auth },
  }, { headers: { 'X-CSRF-TOKEN': token } })).data;
}

export async function revokeDevicePushSubscription() {
  const token = await deviceCsrfToken();
  return (await apiClient.post<{ subscribed: boolean }>(
    '/public/device-session/push-subscriptions/revoke',
    undefined,
    { headers: { 'X-CSRF-TOKEN': token } },
  )).data;
}

export async function queueTestNotification() {
  const token = await deviceCsrfToken();
  return (await apiClient.post<{ queued: boolean }>(
    '/public/device-session/notifications/test',
    undefined,
    { headers: { 'X-CSRF-TOKEN': token } },
  )).data;
}

export async function removeDeviceSession() {
  const token = await deviceCsrfToken();
  await apiClient.post('/public/device-session/remove', undefined, {
    headers: { 'X-CSRF-TOKEN': token },
  });
}

export function pushCapabilityError(error: unknown) {
  if (error instanceof DOMException && error.name === 'NotAllowedError') {
    return 'Браузер не разрешил уведомления. Проверять обращение по трек-номеру можно как обычно.';
  }
  if (axios.isAxiosError(error)) {
    const detail = error.response?.data?.detail;
    if (typeof detail === 'string') return detail;
  }
  return 'Уведомления сейчас не включились. Это не влияет на обращение.';
}

export function urlBase64ToUint8Array(value: string) {
  const padding = '='.repeat((4 - value.length % 4) % 4);
  const raw = atob((value + padding).replaceAll('-', '+').replaceAll('_', '/'));
  return Uint8Array.from([...raw].map((character) => character.charCodeAt(0)));
}
