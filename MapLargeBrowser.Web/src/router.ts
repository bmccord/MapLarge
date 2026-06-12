import { el } from './dom';

export type SortColumn = 'name' | 'type' | 'size' | 'modified';
export type SortDir = 'asc' | 'desc';

export interface RouteState {
  path: string;
  query: string;
  showHidden: boolean;
  recursiveSize: boolean;
  sortColumn: SortColumn | null;
  sortDir: SortDir;
}

const BROWSE_PREFIX = '/browse/';
const SORT_COLUMNS: readonly SortColumn[] = ['name', 'type', 'size', 'modified'];

export function parseLocation(): RouteState {
  const { pathname, search } = window.location;
  const path = pathname.startsWith(BROWSE_PREFIX)
    ? decodeURIComponent(pathname.slice(BROWSE_PREFIX.length))
    : '';
  const params = new URLSearchParams(search);
  const sortParam = params.get('sort');
  const sortColumn = SORT_COLUMNS.find(c => c === sortParam) ?? null;
  return {
    path,
    query: params.get('q') ?? '',
    showHidden: params.get('hidden') === '1',
    recursiveSize: params.get('recursive') === '1',
    sortColumn,
    sortDir: params.get('dir') === 'desc' ? 'desc' : 'asc'
  };
}

export function buildUrl(state: RouteState): string {
  const params = new URLSearchParams();
  if (state.query) params.set('q', state.query);
  if (state.showHidden) params.set('hidden', '1');
  if (state.recursiveSize) params.set('recursive', '1');
  if (state.sortColumn !== null) {
    params.set('sort', state.sortColumn);
    if (state.sortDir === 'desc') params.set('dir', 'desc');
  }
  const search = params.toString();
  return `${BROWSE_PREFIX}${encodeURI(state.path)}${search ? `?${search}` : ''}`;
}

export function navigateTo(state: RouteState): void {
  window.history.pushState(state, '', buildUrl(state));
}

export function replaceState(changes: Partial<RouteState>): RouteState {
  const next: RouteState = { ...parseLocation(), ...changes };
  window.history.replaceState(next, '', buildUrl(next));
  return next;
}

export function linkTo(
  to: Partial<RouteState>,
  label: string,
  onNavigate: (to: Partial<RouteState>) => Promise<void>
): HTMLAnchorElement {
  const a = el('a', undefined, label);
  a.href = buildUrl({ ...parseLocation(), ...to });
  a.addEventListener('click', event => {
    if (event.ctrlKey || event.metaKey || event.shiftKey || event.button !== 0) {
      return;
    }
    event.preventDefault();
    onNavigate(to);
  });
  return a;
}
