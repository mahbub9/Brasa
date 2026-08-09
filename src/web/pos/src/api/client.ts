import type {
  AddLineRequest,
  CloseOrderResponse,
  MenuCategoryDto,
  OpenOrderRequest,
  OrderDto,
  ProblemDetails,
  RoomDto,
  TableDto,
} from './types';

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

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${API_BASE_URL}${path}`, {
    ...init,
    headers: {
      Accept: 'application/json',
      ...(init?.body ? { 'Content-Type': 'application/json' } : {}),
      ...init?.headers,
    },
  });

  if (!response.ok) {
    const problem: ProblemDetails = await response.json().catch(() => ({}));
    throw new ApiError(response.status, problem.code, problem.title ?? response.statusText);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}

/** Every mutating call gets its own key — see docs/architecture/api-contract.md (API-05). */
function newIdempotencyKey(): string {
  return crypto.randomUUID();
}

function post<T>(path: string, body?: unknown): Promise<T> {
  return request<T>(path, {
    method: 'POST',
    headers: { 'Idempotency-Key': newIdempotencyKey() },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
}

export const api = {
  getMenu: () => request<MenuCategoryDto[]>('/menu'),

  getFloor: () => request<RoomDto[]>('/floor'),

  clearTable: (tableId: string) => post<TableDto>(`/tables/${tableId}/clear`),

  openOrder: (body: OpenOrderRequest) => post<OrderDto>('/orders', body),

  getOrder: (orderId: string) => request<OrderDto>(`/orders/${orderId}`),

  addLine: (orderId: string, body: AddLineRequest) =>
    post<OrderDto>(`/orders/${orderId}/lines`, body),

  previewSplit: (orderId: string, parts: number) =>
    request<{ amount: number; currency: string }[]>(`/orders/${orderId}/split?parts=${parts}`),

  closeOrder: (orderId: string) => post<CloseOrderResponse>(`/orders/${orderId}/close`),
};
