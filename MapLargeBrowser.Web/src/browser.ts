import { downloadEntry, tryDelete, tryMoveOrCopy, tryUpload } from './actions';
import * as api from './api';
import { btn, el, iconBtn } from './dom';
import { errorMessage } from './format';
import { renderListing, renderSearchResults, type ListingHandlers } from './listing';
import { linkTo, navigateTo, parseLocation, replaceState, type RouteState } from './router';
import type { FileEntry } from './types';

const SEARCH_DEBOUNCE_MS = 250;
const BROWSE_URL_PREFIX = '/browse';

interface ToolbarHandlers {
  onSearchType: (value: string) => void;
  onUpload: (state: RouteState) => void;
  onNavigate: (to: Partial<RouteState>) => Promise<void>;
}

function renderToolbar(state: RouteState, handlers: ToolbarHandlers): HTMLElement {
  const toolbar = el('div', 'toolbar');

  const search = el('input', 'search');
  search.type = 'search';
  search.placeholder = 'Search names and paths…';
  search.value = state.query;
  search.addEventListener('input', () => {
    handlers.onSearchType(search.value);
  });

  const hiddenLabel = el('label');
  const hiddenInput = el('input');
  hiddenInput.type = 'checkbox';
  hiddenInput.checked = state.showHidden;
  hiddenInput.addEventListener('change', () => handlers.onNavigate({ showHidden: hiddenInput.checked }));
  hiddenLabel.append(hiddenInput, ' Show hidden');

  const uploadBtn = btn('btn-secondary upload-btn', 'Upload here…', () => handlers.onUpload(state));

  toolbar.append(search, hiddenLabel, uploadBtn);
  return toolbar;
}

function renderBreadcrumbs(
  state: RouteState,
  navigate: (to: Partial<RouteState>) => Promise<void>
): HTMLElement {
  const nav = el('nav', 'breadcrumbs');

  nav.append(linkTo({ path: '', query: '' }, 'Root', navigate));

  if (state.path) {
    const segments = state.path.split('/').filter(Boolean);
    let accumulated = '';
    for (const segment of segments) {
      accumulated = accumulated ? `${accumulated}/${segment}` : segment;
      nav.append(document.createTextNode(' / '), linkTo({ path: accumulated, query: '' }, segment, navigate));
    }
  }

  return nav;
}

export function startBrowser(): void {
  const sizeCache = new Map<string, number>();
  let dialogOverlay: HTMLElement | null = null;
  let dialogContent: HTMLElement | null = null;
  let bodyContainer: HTMLElement | null = null;
  let dialogBanner: HTMLElement | null = null;
  let resetButton: HTMLButtonElement | null = null;
  let searchAbort: AbortController | null = null;
  let searchTimer: number | null = null;

  function showError(message: string): void {
    if (!dialogBanner) return;
    dialogBanner.textContent = message;
    dialogBanner.hidden = false;
  }

  function clearError(): void {
    if (!dialogBanner) return;
    dialogBanner.hidden = true;
    dialogBanner.textContent = '';
  }

  function clearPendingSearch(): void {
    if (searchTimer !== null) {
      clearTimeout(searchTimer);
      searchTimer = null;
    }
    if (searchAbort) {
      searchAbort.abort();
      searchAbort = null;
    }
  }

  async function openBrowserDialog(): Promise<void> {
    if (dialogOverlay) return;

    const overlay = el('div', 'browser-dialog-overlay');
    const dialog = el('div', 'browser-dialog');
    const header = el('div', 'browser-dialog-header');
    const title = el('h2', undefined, 'MapLarge Browser');

    const reset = btn('browser-dialog-reset', 'Reset sample files', onReset);
    reset.hidden = true;
    const closeBtn = iconBtn('browser-dialog-close', 'x', 'Close', () => closeBrowserDialog());

    const headerActions = el('div', 'browser-dialog-actions');
    headerActions.append(reset, closeBtn);
    header.append(title, headerActions);

    const banner = el('div', 'browser-dialog-banner');
    banner.hidden = true;
    banner.addEventListener('click', clearError);

    const content = el('div', 'browser-dialog-content');

    dialog.append(header, banner, content);
    overlay.append(dialog);
    document.body.append(overlay);
    document.body.style.overflow = 'hidden';

    dialogOverlay = overlay;
    dialogContent = content;
    dialogBanner = banner;
    resetButton = reset;

    document.addEventListener('keydown', onDialogKey);

    if (!window.location.pathname.startsWith(BROWSE_URL_PREFIX)) {
      history.pushState(null, '', `${BROWSE_URL_PREFIX}/`);
    }

    await fullRender();
  }

  function closeBrowserDialog(updateUrl = true): void {
    if (!dialogOverlay) return;

    clearPendingSearch();
    document.removeEventListener('keydown', onDialogKey);
    dialogOverlay.remove();
    document.body.style.overflow = '';
    dialogOverlay = null;
    dialogContent = null;
    bodyContainer = null;
    dialogBanner = null;
    resetButton = null;

    if (updateUrl) {
      history.pushState(null, '', '/');
    }
  }

  function onDialogKey(event: KeyboardEvent): void {
    if (event.key !== 'Escape' || event.defaultPrevented) return;
    // If the picker (move/copy/upload-conflict) is open, let it handle Esc.
    if (document.querySelector('.picker-overlay')) return;
    event.preventDefault();
    closeBrowserDialog();
  }

  async function fullRender(): Promise<void> {
    if (!dialogContent) return;

    clearError();
    clearPendingSearch();
    const state = parseLocation();

    const toolbar = renderToolbar(state, toolbarHandlers);
    const breadcrumbs = renderBreadcrumbs(state, navigate);
    bodyContainer = el('section');

    dialogContent.replaceChildren(toolbar, breadcrumbs, bodyContainer);

    await refreshBody(state);
  }

  async function refreshBody(state: RouteState): Promise<void> {
    if (!bodyContainer) return;

    const container = bodyContainer;
    container.replaceChildren(el('p', 'loading', 'Loading…'));

    if (searchAbort) {
      searchAbort.abort();
    }
    searchAbort = new AbortController();
    const signal = searchAbort.signal;

    try {
      let child: HTMLElement;
      if (state.query) {
        child = renderSearchResults(
          state,
          await api.search(state.path, state.query, state.showHidden, signal),
          handlers
        );
      } else {
        const response = await api.browse(state.path, state.showHidden, signal);
        if (resetButton) resetButton.hidden = !response.rootIsResettable;
        child = renderListing(state, response, handlers);
      }
      if (signal.aborted) return;
      container.replaceChildren(child);
    } catch (error) {
      if (api.isAbortError(error)) return;
      container.replaceChildren(el('p', 'error', `Failed to load: ${errorMessage(error)}`));
    }
  }

  async function onReset(): Promise<void> {
    if (!window.confirm('Delete all files in the sample root and restore defaults?')) return;
    try {
      await api.resetSampleRoot();
      await afterMutation();
    } catch (error) {
      showError(`Reset failed: ${errorMessage(error)}`);
    }
  }

  async function afterMutation(): Promise<void> {
    sizeCache.clear();
    await fullRender();
  }

  function onSearchType(value: string): void {
    if (searchTimer !== null) {
      clearTimeout(searchTimer);
    }
    searchTimer = window.setTimeout(async () => {
      searchTimer = null;
      await refreshBody(replaceState({ query: value }));
    }, SEARCH_DEBOUNCE_MS);
  }

  async function navigate(to: Partial<RouteState>): Promise<void> {
    const next: RouteState = { ...parseLocation(), ...to };
    navigateTo(next);
    await fullRender();
  }

  async function fetchRecursiveSize(path: string): Promise<number> {
    const cached = sizeCache.get(path);
    if (cached !== undefined) return cached;
    const total = await api.size(path);
    sizeCache.set(path, total);
    return total;
  }

  async function onDelete(entry: FileEntry): Promise<void> {
    if (await tryDelete(entry, showError)) await afterMutation();
  }

  async function onMove(state: RouteState, entry: FileEntry): Promise<void> {
    if (await tryMoveOrCopy(state, entry, 'move')) await afterMutation();
  }

  async function onCopy(state: RouteState, entry: FileEntry): Promise<void> {
    if (await tryMoveOrCopy(state, entry, 'copy')) await afterMutation();
  }

  async function onUpload(state: RouteState): Promise<void> {
    if (await tryUpload(state, showError)) await afterMutation();
  }

  const handlers: ListingHandlers = {
    onDownload: downloadEntry,
    onMove,
    onCopy,
    onDelete,
    onFetchRecursiveSize: fetchRecursiveSize,
    onNavigate: navigate
  };

  const toolbarHandlers: ToolbarHandlers = {
    onSearchType,
    onUpload,
    onNavigate: navigate
  };

  const trigger = document.getElementById('open-browser');
  if (!trigger) {
    throw new Error('Missing #open-browser trigger button');
  }
  trigger.addEventListener('click', openBrowserDialog);

  window.addEventListener('popstate', async () => {
    if (window.location.pathname.startsWith(BROWSE_URL_PREFIX)) {
      if (!dialogOverlay) {
        await openBrowserDialog();
      } else {
        await fullRender();
      }
    } else if (dialogOverlay) {
      closeBrowserDialog(false);
    }
  });

  if (window.location.pathname.startsWith(BROWSE_URL_PREFIX)) {
    openBrowserDialog();
  }
}
