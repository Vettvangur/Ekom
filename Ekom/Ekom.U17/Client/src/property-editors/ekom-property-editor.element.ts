export class EkomPropertyEditorElement extends HTMLElement {
  private input?: HTMLTextAreaElement;
  private internalValue = '';

  get value(): string {
    return this.internalValue;
  }

  set value(value: string | null | undefined) {
    this.internalValue = value ?? '';

    if (this.input != null && this.input.value !== this.internalValue) {
      this.input.value = this.internalValue;
    }
  }

  get readonly(): boolean {
    return this.hasAttribute('readonly');
  }

  set readonly(value: boolean) {
    this.toggleAttribute('readonly', value);

    if (this.input != null) {
      this.input.readOnly = value;
    }
  }

  override connectedCallback(): void {
    this.render();
  }

  attributeChangedCallback(name: string): void {
    if (name === 'readonly' && this.input != null) {
      this.input.readOnly = this.readonly;
    }
  }

  private render(): void {
    this.innerHTML = `
      <style>
        :host {
          display: block;
        }

        label {
          display: grid;
          gap: var(--uui-size-space-2, 8px);
          color: var(--uui-color-text, #1b264f);
        }

        span {
          font-weight: 600;
        }

        textarea {
          box-sizing: border-box;
          min-height: 120px;
          width: 100%;
          padding: var(--uui-size-space-3, 12px);
          border: 1px solid var(--uui-color-border, #d8d7d9);
          border-radius: var(--uui-border-radius, 3px);
          color: var(--uui-color-text, #1b264f);
          font: inherit;
          resize: vertical;
        }

        small {
          color: var(--uui-color-text-alt, #515054);
        }
      </style>
      <label>
        <span>Ekom value</span>
        <textarea spellcheck="false"></textarea>
        <small>This placeholder editor preserves the JSON value until the specialized Ekom UI is ported.</small>
      </label>
    `;

    this.input = this.querySelector('textarea') ?? undefined;

    if (this.input == null) {
      return;
    }

    this.input.value = this.internalValue;
    this.input.readOnly = this.readonly;
    this.input.addEventListener('input', () => {
      this.internalValue = this.input?.value ?? '';
      this.dispatchEvent(new CustomEvent('change', { bubbles: true, composed: true }));
    });
  }

  static get observedAttributes(): string[] {
    return ['readonly'];
  }
}

customElements.define('ekom-property-editor', EkomPropertyEditorElement);

export default EkomPropertyEditorElement;

declare global {
  interface HTMLElementTagNameMap {
    'ekom-property-editor': EkomPropertyEditorElement;
  }
}
