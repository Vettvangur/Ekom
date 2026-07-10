import { UmbElementMixin } from '@umbraco-cms/backoffice/element-api';
import { UMB_DOCUMENT_WORKSPACE_CONTEXT } from '@umbraco-cms/backoffice/document';
import { UMB_WORKSPACE_EDITOR_CONTEXT } from '@umbraco-cms/backoffice/workspace';

const VARIANT_COUNT_HINT = 'ekom-variant-count';
const VARIANTS_WORKSPACE_VIEW_ALIAS = 'Ekom.WorkspaceView.Product.Variants';

type VariantCountResponse = {
  count: number;
};

type DocumentWorkspaceContext = typeof UMB_DOCUMENT_WORKSPACE_CONTEXT.TYPE;
type WorkspaceEditorContext = typeof UMB_WORKSPACE_EDITOR_CONTEXT.TYPE;

class EkomVariantCountWorkspaceFooterAppElement extends UmbElementMixin(HTMLElement) {
  private documentWorkspaceContext?: DocumentWorkspaceContext;
  private workspaceEditorContext?: WorkspaceEditorContext;
  private productId = '';
  private requestId = 0;

  override connectedCallback(): void {
    super.connectedCallback();
    this.style.display = 'none';

    this.consumeContext(UMB_WORKSPACE_EDITOR_CONTEXT, context => {
      this.workspaceEditorContext = context;
      void this.updateBadge();
    });

    this.consumeContext(UMB_DOCUMENT_WORKSPACE_CONTEXT, context => {
      if (context == null) {
        return;
      }

      this.documentWorkspaceContext = context;

      const unique = context.getUnique();
      if (unique != null) {
        this.productId = unique;
      }

      this.observe(context.unique, value => {
        if (value == null || value === this.productId) {
          return;
        }

        this.productId = value;
        void this.updateBadge();
      }, 'ekomVariantCountProductId');

      void this.updateBadge();
    });
  }

  override disconnectedCallback(): void {
    this.requestId++;
    void this.setBadge(0);
    super.disconnectedCallback();
  }

  private async updateBadge(): Promise<void> {
    const productId = this.productId || this.documentWorkspaceContext?.getUnique() || '';
    const requestId = ++this.requestId;

    if (!productId) {
      await this.setBadge(0).catch(() => undefined);
      return;
    }

    try {
      const result = await this.fetchJson<VariantCountResponse>(`/ekom/backoffice/Variants/${encodeURIComponent(productId)}/Count`);

      if (requestId !== this.requestId) {
        return;
      }

      await this.setBadge(result.count);
      this.setTabBadge(result.count);
    } catch {
      if (requestId === this.requestId) {
        await this.setBadge(0).catch(() => undefined);
        this.setTabBadge(0);
      }
    }
  }

  private async setBadge(count: number): Promise<void> {
    const viewContext = await this.workspaceEditorContext?.getViewContext(VARIANTS_WORKSPACE_VIEW_ALIAS);

    if (viewContext == null) {
      return;
    }

    if (viewContext.hints.has(VARIANT_COUNT_HINT)) {
      viewContext.hints.removeOne(VARIANT_COUNT_HINT);
    }

    if (count > 0) {
      viewContext.hints.addOne({
        unique: VARIANT_COUNT_HINT,
        text: String(count),
        color: 'default',
      });
    }
  }

  private setTabBadge(count: number): void {
    const apply = (): boolean => {
      const tab = querySelectorDeep(`[data-mark="workspace:view-link:${VARIANTS_WORKSPACE_VIEW_ALIAS}"]`);

      if (tab == null) {
        return false;
      }

      tab.querySelector('[data-ekom-variant-count-badge]')?.remove();

      if (count <= 0) {
        return true;
      }

      const iconSlot = tab.querySelector('[slot="icon"]');
      if (iconSlot == null) {
        return false;
      }

      const badge = document.createElement('umb-badge');
      badge.setAttribute('data-ekom-variant-count-badge', '');
      badge.setAttribute('color', 'default');
      badge.textContent = String(count);
      iconSlot.append(badge);

      return true;
    };

    if (apply()) {
      return;
    }

    window.setTimeout(apply, 100);
    window.setTimeout(apply, 500);
  }

  private async fetchJson<T>(url: string): Promise<T> {
    const response = await fetch(url, {
      credentials: 'same-origin',
      headers: {
        Accept: 'application/json',
      },
    });

    if (!response.ok) {
      throw new Error(`Request failed with status ${response.status}`);
    }

    return await response.json() as T;
  }
}

function querySelectorDeep(selector: string, root: ParentNode = document): Element | null {
  const match = root.querySelector(selector);

  if (match != null) {
    return match;
  }

  const elements = root.querySelectorAll('*');

  for (const element of elements) {
    if (element.shadowRoot == null) {
      continue;
    }

    const shadowMatch = querySelectorDeep(selector, element.shadowRoot);
    if (shadowMatch != null) {
      return shadowMatch;
    }
  }

  return null;
}

customElements.define('ekom-variant-count-workspace-footer-app', EkomVariantCountWorkspaceFooterAppElement);

export default EkomVariantCountWorkspaceFooterAppElement;

declare global {
  interface HTMLElementTagNameMap {
    'ekom-variant-count-workspace-footer-app': EkomVariantCountWorkspaceFooterAppElement;
  }
}
