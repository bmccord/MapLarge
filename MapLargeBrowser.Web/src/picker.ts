import * as api from './api';
import { btn, el, hide, show } from './dom';
import { errorMessage } from './format';

export interface PickerConfig {
  title: string;
  initialFolder: string;
  initialName: string;
  showTree: boolean;
  showHidden: boolean;
  submitLabel: string;
  initialConflict?: string;
  forbiddenPath?: string;
  onSubmit: (destination: string, overwrite: boolean) => Promise<void>;
  onCancel?: () => void;
}

function isForbidden(path: string, forbiddenPath: string | undefined): boolean {
  if (!forbiddenPath) return false;
  return path === forbiddenPath || path.startsWith(`${forbiddenPath}/`);
}

export function openPickerAsync(
  config: Omit<PickerConfig, 'onSubmit' | 'onCancel'>,
  perform: (destination: string, overwrite: boolean) => Promise<void>
): Promise<boolean> {
  return new Promise(resolve => {
    openPicker({
      ...config,
      onSubmit: async (destination, overwrite) => {
        await perform(destination, overwrite);
        resolve(true);
      },
      onCancel: () => resolve(false)
    });
  });
}

function openPicker(config: PickerConfig): void {
  let currentFolder = config.initialFolder;
  let submitted = false;

  const overlay = el('div', 'picker-overlay');
  const picker = el('div', 'picker');

  const header = el('div', 'picker-header');
  header.append(el('h2', undefined, config.title));

  const body = el('div', 'picker-body');

  const banner = el('div', 'picker-banner');
  banner.hidden = true;

  const treeWrap = el('div', 'picker-tree-wrap');
  if (!config.showTree) treeWrap.hidden = true;

  if (config.showTree) {
    const tree = createTreePane(config.showHidden, currentFolder, config.forbiddenPath, path => {
      currentFolder = path;
      updateFullPath();
    });
    treeWrap.append(tree);
  }

  const inputs = el('div', 'picker-inputs');

  const folderField = el('div', 'picker-field');
  const folderText = el('div', 'picker-folder');
  folderField.append(labelText('Destination folder:'), folderText);

  const nameField = el('label', 'picker-field');
  nameField.append(labelText('Name:'));
  const nameInput = el('input', 'picker-name');
  nameInput.type = 'text';
  nameInput.value = config.initialName;
  nameField.append(nameInput);

  const fullPath = el('div', 'picker-fullpath');

  inputs.append(folderField, nameField, fullPath);

  body.append(banner, treeWrap, inputs);

  const footer = el('div', 'picker-footer');

  const errorBox = el('div', 'picker-error');
  errorBox.hidden = true;

  const cancelBtn = btn('btn-secondary', 'Cancel', close);
  const submitBtn = btn('btn-primary', config.submitLabel, () => submit(false));

  footer.append(errorBox, cancelBtn, submitBtn);

  picker.append(header, body, footer);
  overlay.append(picker);
  document.body.append(overlay);

  function close(): void {
    document.removeEventListener('keydown', onKey);
    overlay.remove();
    if (!submitted) config.onCancel?.();
  }

  function buildDestination(): string {
    const trimmed = nameInput.value.trim();
    if (!trimmed) return '';
    if (!currentFolder) return trimmed;
    return `${currentFolder}/${trimmed}`;
  }

  function updateFullPath(): void {
    folderText.textContent = currentFolder ? `/${currentFolder}` : '/';
    const dest = buildDestination();
    fullPath.textContent = dest ? `Full path: ${dest}` : '';
    submitBtn.disabled = !dest || isForbidden(currentFolder, config.forbiddenPath);
  }

  function showError(message: string): void {
    show(errorBox, message);
  }

  function clearError(): void {
    hide(errorBox);
  }

  function showConflict(detail: string): void {
    const text = el('span', undefined, detail);
    const overwriteBtn = btn('btn-secondary', 'Overwrite', () => submit(true));
    const renameBtn = btn('btn-secondary', 'Change name', () => {
      clearConflict();
      nameInput.focus();
      nameInput.select();
    });
    show(banner, text, overwriteBtn, renameBtn);
  }

  function clearConflict(): void {
    hide(banner);
  }

  async function submit(overwrite: boolean): Promise<void> {
    const destination = buildDestination();
    if (!destination) return;

    submitBtn.disabled = true;
    cancelBtn.disabled = true;
    clearError();
    clearConflict();

    try {
      await config.onSubmit(destination, overwrite);
      submitted = true;
      close();
    } catch (error) {
      submitBtn.disabled = false;
      cancelBtn.disabled = false;
      if (api.isAbortError(error)) return;
      if (api.isApiError(error) && error.status === 409) {
        showConflict(error.detail);
      } else {
        showError(errorMessage(error));
      }
    }
  }

  function onKey(event: KeyboardEvent): void {
    if (event.key === 'Escape') {
      event.preventDefault();
      close();
    } else if (event.key === 'Enter' && document.activeElement === nameInput) {
      event.preventDefault();
      submit(false);
    }
  }

  nameInput.addEventListener('input', () => {
    updateFullPath();
    clearConflict();
  });
  document.addEventListener('keydown', onKey);

  updateFullPath();
  if (config.initialConflict) {
    showConflict(config.initialConflict);
  }
  nameInput.focus();
  nameInput.select();
}

function labelText(text: string): HTMLElement {
  return el('span', 'picker-label', text);
}

function createTreePane(
  showHidden: boolean,
  initialSelection: string,
  forbiddenPath: string | undefined,
  onSelect: (path: string) => void
): HTMLElement {
  const pane = el('div', 'tree');

  function setSelected(label: HTMLElement, relativePath: string): void {
    pane.querySelectorAll('.tree-label.selected').forEach(node => node.classList.remove('selected'));
    label.classList.add('selected');
    onSelect(relativePath);
  }

  function buildNode(relativePath: string, displayName: string): HTMLElement {
    const wrapper = el('div', 'tree-node');
    const row = el('div', 'tree-row');

    const expander = btn('tree-expander', '▸', () => toggle());
    const label = btn('tree-label', displayName, () => setSelected(label, relativePath));
    if (relativePath === initialSelection) {
      label.classList.add('selected');
    }
    if (isForbidden(relativePath, forbiddenPath)) {
      label.disabled = true;
    }

    const childrenContainer = el('div', 'tree-children');
    childrenContainer.hidden = true;

    let loaded = false;
    let expanded = false;

    async function toggle(): Promise<void> {
      if (expanded) {
        childrenContainer.hidden = true;
        expander.textContent = '▸';
        expanded = false;
        return;
      }

      if (!loaded) {
        expander.textContent = '…';
        try {
          const response = await api.browse(relativePath, showHidden);
          const dirs = response.entries.filter(e => e.type === 'Directory');
          childrenContainer.replaceChildren();
          if (dirs.length === 0) {
            childrenContainer.append(el('div', 'tree-empty', '(no subfolders)'));
          } else {
            for (const dir of dirs) {
              childrenContainer.append(buildNode(dir.relativePath, dir.name));
            }
          }
          loaded = true;
        } catch (error) {
          childrenContainer.replaceChildren();
          childrenContainer.append(el('div', 'tree-error', errorMessage(error)));
        }
      }

      childrenContainer.hidden = false;
      expander.textContent = '▾';
      expanded = true;
    }

    row.append(expander, label);
    wrapper.append(row, childrenContainer);

    if (relativePath === '') {
      toggle();
    }

    return wrapper;
  }

  pane.append(buildNode('', 'Root'));
  return pane;
}
