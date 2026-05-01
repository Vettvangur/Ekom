export class EkomSectionViewElement extends HTMLElement {
  override connectedCallback(): void {
    this.render();
  }

  private render(): void {
    this.innerHTML = `
      <style>
        :host {
          display: block;
          padding: var(--uui-size-space-5, 20px);
        }

        .ekom-card {
          display: grid;
          gap: var(--uui-size-space-4, 16px);
          max-width: 960px;
          padding: var(--uui-size-space-5, 20px);
          border: 1px solid var(--uui-color-border, #d8d7d9);
          border-radius: var(--uui-border-radius, 3px);
          background: var(--uui-color-surface, #fff);
          color: var(--uui-color-text, #1b264f);
        }

        h1 {
          margin: 0;
          font-size: 1.4rem;
        }

        p {
          margin: 0;
          line-height: 1.5;
        }
      </style>
      <section class="ekom-card">
        <h1>Ekom</h1>
        <p>
          The Umbraco 17 backoffice shell is registered. The order manager UI will be ported from the
          legacy Angular implementation into Web Components in the next migration slice.
        </p>
      </section>
    `;
  }
}

customElements.define('ekom-section-view', EkomSectionViewElement);

export default EkomSectionViewElement;

declare global {
  interface HTMLElementTagNameMap {
    'ekom-section-view': EkomSectionViewElement;
  }
}
