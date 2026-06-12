import { el, iconBtn } from './dom';
import { errorMessage, formatModified, formatSize } from './format';
import { linkTo, parseLocation, replaceState, type RouteState, type SortColumn, type SortDir } from './router';
import type { BrowseResponse, FileEntry, SearchResponse } from './types';

const HEADER_LABELS: Record<SortColumn, string> = {
  name: 'Name',
  type: 'Type',
  size: 'Size',
  modified: 'Modified'
};

function typeRank(type: FileEntry['type']): number {
  switch (type) {
    case 'Directory': return 0;
    case 'File': return 1;
    case 'Symlink': return 2;
  }
}

function sortEntries(
  entries: readonly FileEntry[],
  column: SortColumn,
  dir: SortDir
): FileEntry[] {
  const byName = (a: FileEntry, b: FileEntry) =>
    a.name.localeCompare(b.name, undefined, { sensitivity: 'base' });
  const compare = (a: FileEntry, b: FileEntry): number => {
    switch (column) {
      case 'name': return byName(a, b);
      case 'type': return (typeRank(a.type) - typeRank(b.type)) || byName(a, b);
      case 'size': return (a.size - b.size) || byName(a, b);
      case 'modified': return a.modifiedUtc.localeCompare(b.modifiedUtc) || byName(a, b);
    }
  };
  const cmp = dir === 'asc' ? compare : (a: FileEntry, b: FileEntry) => -compare(a, b);
  const sorted = [...entries];
  sorted.sort(cmp);
  return sorted;
}

function nextSortState(
  currentColumn: SortColumn | null,
  currentDir: SortDir,
  clickedColumn: SortColumn
): { sortColumn: SortColumn | null; sortDir: SortDir } {
  if (currentColumn !== clickedColumn) {
    return { sortColumn: clickedColumn, sortDir: 'asc' };
  }
  if (currentDir === 'asc') {
    return { sortColumn: clickedColumn, sortDir: 'desc' };
  }
  return { sortColumn: null, sortDir: 'asc' };
}

export interface ListingHandlers {
  onDownload: (entry: FileEntry) => void;
  onMove: (state: RouteState, entry: FileEntry) => void;
  onCopy: (state: RouteState, entry: FileEntry) => void;
  onDelete: (entry: FileEntry) => void;
  onFetchRecursiveSize: (path: string) => Promise<number>;
  onNavigate: (to: Partial<RouteState>) => Promise<void>;
}

function renderSizeSummary(
  initialState: RouteState,
  response: BrowseResponse,
  onFetchRecursiveSize: (path: string) => Promise<number>
): HTMLElement {
  const summary = el('p', 'summary');
  const counts = el(
    'span',
    undefined,
    `${response.directoryCount} folder(s), ${response.fileCount} file(s) — `
  );
  const sizeText = el('span', 'size-text');
  const recursiveLabel = el('label', 'recursive-toggle');
  const recursiveCheckbox = el('input');
  recursiveCheckbox.type = 'checkbox';
  recursiveCheckbox.checked = initialState.recursiveSize;
  recursiveLabel.append(recursiveCheckbox, ' Show recursive size');

  function showImmediate(): void {
    sizeText.textContent = `${formatSize(response.immediateSize)} immediate`;
  }

  async function showRecursive(): Promise<void> {
    sizeText.textContent = 'Computing…';
    recursiveCheckbox.disabled = true;
    try {
      const total = await onFetchRecursiveSize(initialState.path);
      sizeText.textContent = `${formatSize(total)} recursive`;
    } catch (error) {
      sizeText.textContent = `Error: ${errorMessage(error)}`;
      recursiveCheckbox.checked = false;
      replaceState({ recursiveSize: false });
    } finally {
      recursiveCheckbox.disabled = false;
    }
  }

  recursiveCheckbox.addEventListener('change', async () => {
    replaceState({ recursiveSize: recursiveCheckbox.checked });
    if (recursiveCheckbox.checked) {
      await showRecursive();
    } else {
      showImmediate();
    }
  });

  if (initialState.recursiveSize) {
    showRecursive();
  } else {
    showImmediate();
  }

  summary.append(counts, sizeText, ' ', recursiveLabel);
  return summary;
}

export function renderListing(
  initialState: RouteState,
  response: BrowseResponse,
  handlers: ListingHandlers
): HTMLElement {
  const container = el('section');
  container.append(renderSizeSummary(initialState, response, handlers.onFetchRecursiveSize));

  if (response.entries.length === 0) {
    container.append(el('p', 'empty', 'Empty folder'));
    return container;
  }

  const headerCells = new Map<SortColumn, HTMLTableCellElement>();

  function makeSortableHeader(column: SortColumn): HTMLTableCellElement {
    const th = el('th', 'sortable');
    th.addEventListener('click', () => onHeaderClick(column));
    headerCells.set(column, th);
    return th;
  }

  const table = createTable([
    makeSortableHeader('name'),
    makeSortableHeader('type'),
    makeSortableHeader('size'),
    makeSortableHeader('modified'),
    el('th', undefined, 'Actions')
  ]);
  const tbody = table.tBodies[0];

  function updateHeaderLabels(sortColumn: SortColumn | null, sortDir: SortDir): void {
    for (const [column, th] of headerCells) {
      const base = HEADER_LABELS[column];
      th.textContent = sortColumn === column
        ? `${base} ${sortDir === 'asc' ? '▲' : '▼'}`
        : base;
    }
  }

  function rerenderRows(state: RouteState): void {
    const entries = state.sortColumn === null
      ? response.entries
      : sortEntries(response.entries, state.sortColumn, state.sortDir);

    tbody.replaceChildren();
    for (const entry of entries) {
      tbody.append(renderEntryRow(state, entry, entry.name, handlers));
    }
  }

  function onHeaderClick(column: SortColumn): void {
    const state = parseLocation();
    const next = replaceState(nextSortState(state.sortColumn, state.sortDir, column));
    updateHeaderLabels(next.sortColumn, next.sortDir);
    rerenderRows(next);
  }

  updateHeaderLabels(initialState.sortColumn, initialState.sortDir);
  rerenderRows(initialState);

  container.append(table);
  return container;
}

export function renderSearchResults(
  state: RouteState,
  response: SearchResponse,
  handlers: ListingHandlers
): HTMLElement {
  const container = el('section');

  const counts =
    `${response.directoryCount} folder(s), ` +
    `${response.fileCount} file(s), ` +
    `${formatSize(response.totalSize)} total`;
  const summaryText = response.truncated
    ? `${response.entries.length} match(es) — truncated · ${counts}`
    : `${response.entries.length} match(es) · ${counts}`;
  const summary = el('p', 'summary', summaryText);
  container.append(summary);

  if (response.entries.length === 0) {
    container.append(el('p', 'empty', 'No matches'));
    return container;
  }

  const table = createTable(['Path', 'Type', 'Size', 'Modified', 'Actions']);
  const tbody = table.tBodies[0];

  for (const entry of response.entries) {
    tbody.append(renderEntryRow(state, entry, entry.relativePath, handlers));
  }

  container.append(table);
  return container;
}

function createTable(headers: ReadonlyArray<string | HTMLTableCellElement>): HTMLTableElement {
  const table = el('table', 'listing');
  const thead = el('thead');
  const headRow = el('tr');
  for (const header of headers) {
    headRow.append(typeof header === 'string' ? el('th', undefined, header) : header);
  }
  thead.append(headRow);

  const tbody = el('tbody');
  table.append(thead, tbody);
  return table;
}

function renderEntryRow(
  state: RouteState,
  entry: FileEntry,
  primaryLabel: string,
  handlers: ListingHandlers
): HTMLTableRowElement {
  const row = baseRow(entry);

  const firstCell = el('td');
  if (entry.type === 'Directory') {
    firstCell.append(linkTo({ path: entry.relativePath, query: '' }, primaryLabel, handlers.onNavigate));
  } else {
    firstCell.textContent = primaryLabel;
  }
  appendSymlinkTarget(firstCell, entry);

  row.append(firstCell, ...metadataCells(entry), actionsCell(state, entry, handlers));
  return row;
}

function baseRow(entry: FileEntry): HTMLTableRowElement {
  return el('tr', entry.type.toLowerCase());
}

function appendSymlinkTarget(cell: HTMLTableCellElement, entry: FileEntry): void {
  if (entry.type === 'Symlink' && entry.symlinkTarget) {
    cell.append(el('span', 'symlink-target', ` → ${entry.symlinkTarget}`));
  }
}

function metadataCells(entry: FileEntry): HTMLTableCellElement[] {
  const typeCell = el('td', undefined, entry.type);
  const sizeCell = el('td', 'size', entry.type === 'File' ? formatSize(entry.size) : '');
  const modifiedCell = el('td', 'modified', formatModified(entry.modifiedUtc));
  return [typeCell, sizeCell, modifiedCell];
}

function actionsCell(
  state: RouteState,
  entry: FileEntry,
  handlers: ListingHandlers
): HTMLTableCellElement {
  const cell = el('td', 'actions');

  if (entry.type === 'Symlink') {
    cell.textContent = '—';
    return cell;
  }

  if (entry.type === 'File') {
    cell.append(rowAction('download', 'Download', () => handlers.onDownload(entry)));
  }
  cell.append(rowAction('move', 'Move', () => handlers.onMove(state, entry)));
  cell.append(rowAction('copy', 'Copy', () => handlers.onCopy(state, entry)));
  cell.append(rowAction('trash-2', 'Delete', () => handlers.onDelete(entry), true));
  return cell;
}

function rowAction(
  iconName: string,
  label: string,
  handler: () => void,
  danger = false
): HTMLButtonElement {
  return iconBtn(danger ? 'row-action danger' : 'row-action', iconName, label, handler);
}
