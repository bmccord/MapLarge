export function el<K extends keyof HTMLElementTagNameMap>(
  tag: K,
  className?: string,
  text?: string
): HTMLElementTagNameMap[K] {
  const node = document.createElement(tag);
  if (className) node.className = className;
  if (text !== undefined) node.textContent = text;
  return node;
}

export function btn(
  className: string,
  text: string,
  onClick?: () => void
): HTMLButtonElement {
  const button = el('button', className, text);
  button.type = 'button';
  if (onClick) button.addEventListener('click', onClick);
  return button;
}

export function iconBtn(
  className: string,
  iconName: string,
  label: string,
  onClick?: () => void
): HTMLButtonElement {
  const button = el('button', className);
  button.type = 'button';
  button.title = label;
  button.setAttribute('aria-label', label);
  const icon = el('span', `icon icon-${iconName}`);
  icon.setAttribute('aria-hidden', 'true');
  button.append(icon);
  if (onClick) button.addEventListener('click', onClick);
  return button;
}

export function show(host: HTMLElement, ...content: (Node | string)[]): void {
  host.replaceChildren(...content);
  host.hidden = false;
}

export function hide(host: HTMLElement): void {
  host.replaceChildren();
  host.hidden = true;
}
