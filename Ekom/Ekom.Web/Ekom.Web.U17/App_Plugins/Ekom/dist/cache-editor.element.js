var i = Object.defineProperty;
var r = (s, e, t) => e in s ? i(s, e, { enumerable: !0, configurable: !0, writable: !0, value: t }) : s[e] = t;
var a = (s, e, t) => r(s, typeof e != "symbol" ? e + "" : e, t);
class n extends HTMLElement {
  constructor() {
    super(...arguments);
    a(this, "button");
    a(this, "status");
    a(this, "state", "idle");
    a(this, "message", "Populate all Ekom caches when content changes need to be reflected immediately.");
  }
  get readonly() {
    return this.hasAttribute("readonly");
  }
  set readonly(t) {
    this.toggleAttribute("readonly", t), this.syncButtonState();
  }
  connectedCallback() {
    this.render();
  }
  attributeChangedCallback(t) {
    t === "readonly" && this.syncButtonState();
  }
  async populateCache() {
    if (!(this.readonly || this.state === "loading")) {
      this.setState("loading", "Populating all Ekom caches...");
      try {
        const t = await fetch("/ekom/backoffice/Cache", {
          method: "POST",
          credentials: "same-origin",
          headers: {
            Accept: "application/json"
          }
        });
        if (!t.ok)
          throw new Error(`Cache update failed with status ${t.status}.`);
        if (!await t.json())
          throw new Error("The cache endpoint returned false.");
        this.setState("success", "Cache has been populated again.");
      } catch (t) {
        const o = t instanceof Error ? t.message : "Unknown cache update error.";
        this.setState("error", o);
      }
    }
  }
  setState(t, o) {
    this.state = t, this.message = o, this.syncButtonState(), this.syncStatus();
  }
  syncButtonState() {
    this.button != null && (this.button.disabled = this.readonly || this.state === "loading", this.button.textContent = this.state === "loading" ? "Populating caches..." : "Populate all Ekom caches");
  }
  syncStatus() {
    this.status != null && (this.status.textContent = this.message, this.status.dataset.state = this.state);
  }
  render() {
    var t;
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
    `, this.button = this.querySelector("button") ?? void 0, this.status = this.querySelector("p") ?? void 0, (t = this.button) == null || t.addEventListener("click", () => void this.populateCache()), this.syncButtonState(), this.syncStatus();
  }
  static get observedAttributes() {
    return ["readonly"];
  }
}
customElements.define("ekom-cache-editor", n);
export {
  n as EkomCacheEditorElement,
  n as default
};
