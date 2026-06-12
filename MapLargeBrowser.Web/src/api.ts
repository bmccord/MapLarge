import { API_BASE_URL } from './config';
import type { BrowseResponse, EntryReference, SearchResponse } from './types';

export async function browse(
  path: string,
  showHidden: boolean,
  signal?: AbortSignal
): Promise<BrowseResponse> {
  const url = new URL(`${API_BASE_URL}/browse`, location.origin);
  url.searchParams.set('path', path);
  url.searchParams.set('showHidden', String(showHidden));
  return fetchJson<BrowseResponse>(url, signal);
}

export async function search(
  path: string,
  q: string,
  showHidden: boolean,
  signal?: AbortSignal
): Promise<SearchResponse> {
  const url = new URL(`${API_BASE_URL}/search`, location.origin);
  url.searchParams.set('path', path);
  url.searchParams.set('q', q);
  url.searchParams.set('showHidden', String(showHidden));
  return fetchJson<SearchResponse>(url, signal);
}

export async function size(path: string, signal?: AbortSignal): Promise<number> {
  const url = new URL(`${API_BASE_URL}/size`, location.origin);
  url.searchParams.set('path', path);
  return fetchJson<number>(url, signal);
}

export function downloadUrl(path: string): string {
  const url = new URL(`${API_BASE_URL}/download`, location.origin);
  url.searchParams.set('path', path);
  return url.toString();
}

export async function upload(
  targetDir: string,
  file: File,
  overwrite: boolean,
  signal?: AbortSignal
): Promise<void> {
  const url = new URL(`${API_BASE_URL}/upload`, location.origin);
  url.searchParams.set('path', targetDir);
  if (overwrite) url.searchParams.set('overwrite', 'true');
  const form = new FormData();
  form.append('file', file);
  const response = await fetch(url, { method: 'POST', body: form, signal });
  await throwIfFailed(response);
}

export async function deleteEntry(
  path: string,
  recursive: boolean,
  signal?: AbortSignal
): Promise<void> {
  const url = new URL(`${API_BASE_URL}/entries`, location.origin);
  url.searchParams.set('path', path);
  if (recursive) url.searchParams.set('recursive', 'true');
  const response = await fetch(url, { method: 'DELETE', signal });
  await throwIfFailed(response);
}

export async function resetSampleRoot(signal?: AbortSignal): Promise<void> {
  const url = new URL(`${API_BASE_URL}/admin/reset-sample-root`, location.origin);
  const response = await fetch(url, { method: 'POST', signal });
  await throwIfFailed(response);
}

export async function move(
  from: string,
  to: string,
  overwrite: boolean,
  signal?: AbortSignal
): Promise<void> {
  return mutate('move', from, to, overwrite, signal);
}

export async function copy(
  from: string,
  to: string,
  overwrite: boolean,
  signal?: AbortSignal
): Promise<void> {
  return mutate('copy', from, to, overwrite, signal);
}

async function mutate(
  op: 'move' | 'copy',
  from: string,
  to: string,
  overwrite: boolean,
  signal?: AbortSignal
): Promise<void> {
  const url = new URL(`${API_BASE_URL}/entries/${op}`, location.origin);
  if (overwrite) url.searchParams.set('overwrite', 'true');
  const body: EntryReference = { from, to };
  const response = await fetch(url, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
    signal
  });
  await throwIfFailed(response);
}

export class ApiError extends Error {
  constructor(public readonly status: number, public readonly detail: string) {
    super(`${status} ${detail}`);
    this.name = 'ApiError';
  }
}

export function isApiError(error: unknown): error is ApiError {
  return error instanceof ApiError;
}

export function isAbortError(error: unknown): boolean {
  return error instanceof DOMException && error.name === 'AbortError';
}

interface ProblemDetails {
  title?: string;
  detail?: string;
  status?: number;
}

async function fetchJson<T>(url: URL, signal?: AbortSignal): Promise<T> {
  const response = await fetch(url, { signal });
  await throwIfFailed(response);
  return response.json() as Promise<T>;
}

async function throwIfFailed(response: Response): Promise<void> {
  if (response.ok) return;
  let detail = response.statusText;
  try {
    const body = (await response.json()) as ProblemDetails;
    detail = body.detail ?? body.title ?? detail;
  } catch {
    // body wasn't JSON; fall back
  }
  throw new ApiError(response.status, detail);
}
