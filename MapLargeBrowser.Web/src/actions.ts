import * as api from './api';
import { el } from './dom';
import { errorMessage } from './format';
import { openPickerAsync } from './picker';
import type { RouteState } from './router';
import type { FileEntry } from './types';

export type ShowError = (message: string) => void;

export async function tryDelete(entry: FileEntry, showError: ShowError): Promise<boolean> {
  if (!window.confirm(`Delete ${entry.relativePath}?`)) return false;
  try {
    await api.deleteEntry(entry.relativePath, false);
    return true;
  } catch (error) {
    if (api.isApiError(error) && error.status === 409 && entry.type === 'Directory') {
      if (!window.confirm(`${entry.name} is not empty. Delete it and everything inside?`)) {
        return false;
      }
      try {
        await api.deleteEntry(entry.relativePath, true);
        return true;
      } catch (err2) {
        showError(errorMessage(err2));
        return false;
      }
    }
    showError(errorMessage(error));
    return false;
  }
}

export function tryMoveOrCopy(
  state: RouteState,
  entry: FileEntry,
  op: 'move' | 'copy'
): Promise<boolean> {
  const lastSlash = entry.relativePath.lastIndexOf('/');
  const parentFolder = lastSlash >= 0 ? entry.relativePath.slice(0, lastSlash) : '';

  return openPickerAsync({
    title: `${op === 'move' ? 'Move' : 'Copy'} ${entry.name}`,
    initialFolder: parentFolder,
    initialName: entry.name,
    showTree: true,
    showHidden: state.showHidden,
    submitLabel: op === 'move' ? 'Move' : 'Copy',
    forbiddenPath: entry.type === 'Directory' ? entry.relativePath : undefined
  }, async (destination, overwrite) => {
    if (op === 'move') {
      await api.move(entry.relativePath, destination, overwrite);
    } else {
      await api.copy(entry.relativePath, destination, overwrite);
    }
  });
}

export function tryUpload(state: RouteState, showError: ShowError): Promise<boolean> {
  return new Promise(resolve => {
    const input = el('input');
    input.type = 'file';
    input.addEventListener('cancel', () => resolve(false));
    input.addEventListener('change', async () => {
      const file = input.files?.[0];
      if (!file) {
        resolve(false);
        return;
      }
      resolve(await uploadWithConflictHandling(state, file, showError));
    });
    input.click();
  });
}

async function uploadWithConflictHandling(
  state: RouteState,
  file: File,
  showError: ShowError
): Promise<boolean> {
  try {
    await api.upload(state.path, file, false);
    return true;
  } catch (error) {
    if (api.isApiError(error) && error.status === 409) {
      return openConflictPicker(state, file, error.detail);
    }
    showError(errorMessage(error));
    return false;
  }
}

function openConflictPicker(
  state: RouteState,
  file: File,
  conflictDetail: string
): Promise<boolean> {
  return openPickerAsync({
    title: `Upload ${file.name}`,
    initialFolder: state.path,
    initialName: file.name,
    showTree: false,
    showHidden: state.showHidden,
    submitLabel: 'Upload',
    initialConflict: conflictDetail
  }, async (destination, overwrite) => {
    const newName = destination.split('/').pop() ?? file.name;
    const candidate = newName === file.name ? file : new File([file], newName, { type: file.type });
    await api.upload(state.path, candidate, overwrite);
  });
}

export function downloadEntry(entry: FileEntry): void {
  const a = el('a');
  a.href = api.downloadUrl(entry.relativePath);
  a.download = entry.name;
  document.body.append(a);
  a.click();
  a.remove();
}
