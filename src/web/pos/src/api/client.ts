import type {
  AddLineRequest,
  CloseOrderResponse,
  MenuCategoryDto,
  OpenOrderRequest,
  OpenTakeawayOrderRequest,
  OrderDto,
  OrganizationDto,
  PreBillDto,
  ProblemDetails,
  RoomDto,
  SetLineNotesRequest,
  SetLineQuantityRequest,
  SiteDto,
  StaffDto,
  StaffPinRequest,
  TableDto,
  TransferOrderRequest,
} from './types';

// http://localhost:5216 is the "http" launch profile in
// src/backend/Brasa.Api/Properties/launchSettings.json.
const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5216/api/v1';

/**
 * The API's own origin, without the `/api/v1` suffix — `MenuItemDto.imageUrl`
 * (CAT-02) is a path relative to the API host root (served by
 * `UseStaticFiles`), not under `/api/v1` like every JSON endpoint, so
 * `MenuGrid` needs this to build a fetchable `<img src>`.
 */
export const apiOrigin = new URL(API_BASE_URL).origin;

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

/** Every mutating call needs its own key too — PUT counts, same as POST (API-05). */
function put<T>(path: string, body: unknown): Promise<T> {
  return request<T>(path, {
    method: 'PUT',
    headers: { 'Idempotency-Key': newIdempotencyKey() },
    body: JSON.stringify(body),
  });
}

export const api = {
  getMenu: () => request<MenuCategoryDto[]>('/menu'),

  getFloor: () => request<RoomDto[]>('/floor'),

  clearTable: (tableId: string) => post<TableDto>(`/tables/${tableId}/clear`),

  requestBill: (tableId: string) => post<TableDto>(`/tables/${tableId}/request-bill`),

  openOrder: (body: OpenOrderRequest) => post<OrderDto>('/orders', body),

  openTakeawayOrder: (body: OpenTakeawayOrderRequest) => post<OrderDto>('/orders/takeaway', body),

  getOrder: (orderId: string) => request<OrderDto>(`/orders/${orderId}`),

  addLine: (orderId: string, body: AddLineRequest) =>
    post<OrderDto>(`/orders/${orderId}/lines`, body),

  setLineNotes: (orderId: string, lineId: string, body: SetLineNotesRequest) =>
    put<OrderDto>(`/orders/${orderId}/lines/${lineId}/notes`, body),

  setLineQuantity: (orderId: string, lineId: string, body: SetLineQuantityRequest) =>
    put<OrderDto>(`/orders/${orderId}/lines/${lineId}/quantity`, body),

  transferOrder: (orderId: string, body: TransferOrderRequest) =>
    post<OrderDto>(`/orders/${orderId}/transfer`, body),

  previewSplit: (orderId: string, parts: number) =>
    request<{ amount: number; currency: string }[]>(`/orders/${orderId}/split?parts=${parts}`),

  getPreBill: (orderId: string) => request<PreBillDto>(`/orders/${orderId}/pre-bill`),

  closeOrder: (orderId: string) => post<CloseOrderResponse>(`/orders/${orderId}/close`),

  // IDN-01/08/09 (WEB-07) -- staff sign-in. No site-selector exists in pos
  // any more than it does in admin, so this resolves the same "first
  // organization's first site" shortcut every other single-site screen in
  // this codebase already takes.
  getOrganizations: () => request<OrganizationDto[]>('/organizations'),

  getSites: (organizationId: string) => request<SiteDto[]>(`/organizations/${organizationId}/sites`),

  getStaff: (siteId: string) => request<StaffDto[]>(`/sites/${siteId}/staff`),

  verifyStaffPin: (staffId: string, body: StaffPinRequest) =>
    post<StaffDto>(`/staff/${staffId}/verify-pin`, body),
};
