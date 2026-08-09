import type { MenuCategoryDto, ProblemDetails, RoomDto } from './types';

// http://localhost:5216 is the "http" launch profile in
// src/backend/Brasa.Api/Properties/launchSettings.json.
const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5216/api/v1';

/** Thrown for any non-2xx response, carrying the server's stable error code. */
export class ApiError extends Error {
  readonly status: number;
  readonly code?: string;

  constructor(status: number, code: string | undefined, message: string) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.code = code;
  }
}

async function request<T>(path: string): Promise<T> {
  const response = await fetch(`${API_BASE_URL}${path}`, {
    headers: { Accept: 'application/json' },
  });

  if (!response.ok) {
    const problem: ProblemDetails = await response.json().catch(() => ({}));
    throw new ApiError(response.status, problem.code, problem.title ?? response.statusText);
  }

  return (await response.json()) as T;
}

// Read-only shell — no mutating calls yet, so no Idempotency-Key plumbing
// (see pos's src/api/client.ts) is needed until WEB-10 adds real editors.
export const api = {
  getMenu: () => request<MenuCategoryDto[]>('/menu'),

  getFloor: () => request<RoomDto[]>('/floor'),
};
