type CacheEditorState = 'idle' | 'loading' | 'success' | 'error';

export class EkomCacheEditorElement extends HTMLElement {
  private button?: HTMLButtonElement;
  private status?: HTMLParagraphElement;
  private state: CacheEditorState = 'idle';
  private message = 'Populate all Ekom caches when content changes need to be reflected immediately.';

  get readonly(): boolean {
    return this.hasAttribute('readonly');
  }

  set readonly(value: boolean) {
    this.toggleAttribute('readonly', value);
    this.syncButtonState();
  }

  override connectedCallback(): void {
    this.render();
  }

  attributeChangedCallback(name: string): void {
    if (name === 'readonly') {
      this.syncButtonState();
    }
  }

  private async populateCache(): Promise<void> {
    if (this.readonly || this.state === 'loading') {
      return;
    }

    this.setState('loading', 'Populating all Ekom caches...');

    try {
      const response = await fetch('/ekom/backoffice/Cache', {
        method: 'POST',
        credentials: 'same-origin',
        headers: {
          Accept: 'application/json',
        },
      });

      if (!response.ok) {
        throw new Error(`Cache update failed with status ${response.status}.`);
      }

      const result = await response.json() as boolean;

      if (!result) {
        throw new Error('The cache endpoint returned false.');
      }

      this.setState('success', 'Cache has been populated again.');
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Unknown cache update error.';
      this.setState('error', message);
    }
  }

  private setState(state: CacheEditorState, message: string): void {
    this.state = state;
    this.message = message;
    this.syncButtonState();
    this.syncStatus();
  }

  private syncButtonState(): void {
    if (this.button == null) {
      return;
    }

    this.button.disabled = this.readonly || this.state === 'loading';
    this.button.textContent = this.state === 'loading'
      ? 'Populating caches...'
      : 'Populate all Ekom caches';
  }

  private syncStatus(): void {
    if (this.status == null) {
      return;
    }

    this.status.textContent = this.message;
    this.status.dataset.state = this.state;
  }

  private render(): void {
    this.innerHTML = `
      <style>
        :host {
          display: block;
        }

        .ekom-cache-editor {
          display: grid;
          gap: var(--uui-size-space-3, 12px);
          justify-items: start;
        }

        button {
          border: 0;
          border-radius: var(--uui-border-radius, 3px);
          padding: var(--uui-size-space-3, 12px) var(--uui-size-space-5, 20px);
          background: var(--uui-color-interactive, #3544b1);
          color: var(--uui-color-interactive-contrast, #fff);
          cursor: pointer;
          font: inherit;
          font-weight: 600;
        }

        button:disabled {
          cursor: not-allowed;
          opacity: 0.55;
        }

        p {
          margin: 0;
          color: var(--uui-color-text-alt, #515054);
          line-height: 1.4;
        }

        p[data-state='success'] {
          color: var(--uui-color-positive, #287d3c);
        }

        p[data-state='error'] {
          color: var(--uui-color-danger, #d42054);
        }
      </style>
      <div class="ekom-cache-editor">
        <button type="button"></button>
        <p aria-live="polite"></p>
      </div>
    `;

    this.button = this.querySelector('button') ?? undefined;
    this.status = this.querySelector('p') ?? undefined;
    this.button?.addEventListener('click', () => void this.populateCache());

    this.syncButtonState();
    this.syncStatus();
  }

  static get observedAttributes(): string[] {
    return ['readonly'];
  }
}

customElements.define('ekom-cache-editor', EkomCacheEditorElement);

export default EkomCacheEditorElement;

declare global {
  interface HTMLElementTagNameMap {
    'ekom-cache-editor': EkomCacheEditorElement;
  }
}
