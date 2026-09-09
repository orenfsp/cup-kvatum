import axios from 'axios';

export const apiClient = axios.create({
  baseURL: '/api',
  timeout: 8_000,
  withCredentials: true,
  headers: {
    Accept: 'application/json',
  },
});
