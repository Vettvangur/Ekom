var p = Object.defineProperty;
var m = (d, i, e) => i in d ? p(d, i, { enumerable: !0, configurable: !0, writable: !0, value: e }) : d[i] = e;
var r = (d, i, e) => m(d, typeof i != "symbol" ? i + "" : i, e);
class b extends HTMLElement {
  constructor() {
    super(...arguments);
    r(this, "manifest");
    r(this, "name");
    r(this, "dataSourceAlias");
    r(this, "config");
    r(this, "mandatory");
    r(this, "mandatoryMessage");
    r(this, "pageSize", 10);
    r(this, "coupons", []);
    r(this, "page", 1);
    r(this, "totalPages", 0);
    r(this, "query", "");
    r(this, "status");
  }
  get value() {
  }
  set value(e) {
  }
  get readonly() {
    return this.hasAttribute("readonly");
  }
  set readonly(e) {
    this.toggleAttribute("readonly", e), this.syncDisabledState();
  }
  connectedCallback() {
    if (this.renderShell(), this.isCreateMode() || this.getContentKey() == null) {
      this.renderCreateMessage();
      return;
    }
    this.loadCoupons();
  }
  renderShell() {
    this.innerHTML = `
      <style>
        :host {
          display: block;
        }

        .ekom-coupon-editor {
          display: grid;
          gap: var(--uui-size-space-4, 16px);
        }

        .actions,
        .search,
        .pager,
        form {
          display: flex;
          flex-wrap: wrap;
          align-items: end;
          gap: var(--uui-size-space-3, 12px);
        }

        form {
          border: 1px solid var(--uui-color-border, #d8d7d9);
          border-radius: var(--uui-border-radius, 3px);
          padding: var(--uui-size-space-4, 16px);
        }

        form[hidden] {
          display: none;
        }

        label {
          display: grid;
          gap: var(--uui-size-space-1, 4px);
          font-weight: 600;
        }

        input,
        select {
          box-sizing: border-box;
          min-height: 32px;
          border: 1px solid var(--uui-color-border, #d8d7d9);
          border-radius: var(--uui-border-radius, 3px);
          padding: var(--uui-size-space-2, 8px);
          background: var(--uui-color-surface, #fff);
          color: var(--uui-color-text, #1b264f);
          font: inherit;
        }

        table {
          width: 100%;
          border-collapse: collapse;
          background: var(--uui-color-surface, #fff);
        }

        th,
        td {
          border-bottom: 1px solid var(--uui-color-border, #d8d7d9);
          padding: var(--uui-size-space-3, 12px);
          text-align: left;
        }

        button {
          border: 0;
          border-radius: var(--uui-border-radius, 3px);
          padding: var(--uui-size-space-2, 8px) var(--uui-size-space-4, 16px);
          background: var(--uui-color-interactive, #3544b1);
          color: var(--uui-color-interactive-contrast, #fff);
          cursor: pointer;
          font: inherit;
          font-weight: 600;
        }

        button[data-kind='secondary'] {
          background: var(--uui-color-surface-alt, #f3f3f5);
          color: var(--uui-color-text, #1b264f);
        }

        button[data-kind='danger'] {
          background: var(--uui-color-danger, #d42054);
          color: var(--uui-color-danger-contrast, #fff);
        }

        button:disabled,
        input:disabled,
        select:disabled {
          cursor: not-allowed;
          opacity: 0.55;
        }

        p {
          margin: 0;
          color: var(--uui-color-text-alt, #515054);
          line-height: 1.4;
        }

        p[data-error='true'] {
          color: var(--uui-color-danger, #d42054);
        }
      </style>
      <div class="ekom-coupon-editor"></div>
      <p aria-live="polite"></p>
    `, this.status = this.querySelector("p") ?? void 0;
  }
  renderCreateMessage() {
    const e = this.getEditor();
    e.replaceChildren(), e.textContent = "You need to save the order discount before you can add coupon codes. You will need to refresh the page after saving the order discount.";
  }
  renderEditor() {
    var t, o, a, s, n, c, l, h;
    const e = this.getEditor();
    e.innerHTML = `
      <div class="actions">
        <button type="button" data-action="show-add">Add Code</button>
        <button type="button" data-action="show-generate" data-kind="secondary">Generate Coupons</button>
        <button type="button" data-action="export" data-kind="secondary">Export</button>
      </div>
      <form data-form="add" hidden>
        <label>
          Coupon Code
          <input type="text" name="couponCode" autocomplete="off" required />
        </label>
        <label>
          Usage limits
          <input type="number" name="numberAvailable" min="0" value="1" required />
        </label>
        <button type="submit">Add</button>
        <button type="button" data-action="cancel-forms" data-kind="secondary">Cancel</button>
      </form>
      <form data-form="generate" hidden>
        <label>
          Amount
          <input type="number" name="count" min="1" value="10" required />
        </label>
        <label>
          Usage limits
          <input type="number" name="numberAvailable" min="0" value="1" required />
        </label>
        <label>
          Prefix
          <input type="text" name="prefix" autocomplete="off" />
        </label>
        <label>
          Random length
          <input type="number" name="randomLength" min="1" value="8" required />
        </label>
        <label>
          Character set
          <select name="characterSet">
            <option value="UppercaseAlphanumeric">Uppercase letters and numbers</option>
            <option value="Numbers">Numbers only</option>
            <option value="Letters">Letters only</option>
          </select>
        </label>
        <button type="submit">Generate</button>
        <button type="button" data-action="cancel-forms" data-kind="secondary">Cancel</button>
      </form>
      <div class="search">
        <label>
          Search
          <input type="search" name="query" placeholder="Type to search..." value="${this.escapeAttribute(this.query)}" />
        </label>
      </div>
      <table>
        <thead>
          <tr>
            <th>Coupon Code</th>
            <th>Usage limits</th>
            <th>Created</th>
            <th></th>
          </tr>
        </thead>
        <tbody></tbody>
      </table>
      <div class="pager">
        <button type="button" data-action="previous" data-kind="secondary">Previous</button>
        <span>Page ${this.page} of ${Math.max(this.totalPages, 1)}</span>
        <button type="button" data-action="next" data-kind="secondary">Next</button>
      </div>
    `, (t = e.querySelector('[data-action="show-add"]')) == null || t.addEventListener("click", () => this.showForm("add")), (o = e.querySelector('[data-action="show-generate"]')) == null || o.addEventListener("click", () => this.showForm("generate")), (a = e.querySelector('[data-action="export"]')) == null || a.addEventListener("click", () => this.exportCoupons()), e.querySelectorAll('[data-action="cancel-forms"]').forEach((u) => {
      u.addEventListener("click", () => this.hideForms());
    }), (s = e.querySelector('[data-action="previous"]')) == null || s.addEventListener("click", () => this.changePage(this.page - 1)), (n = e.querySelector('[data-action="next"]')) == null || n.addEventListener("click", () => this.changePage(this.page + 1)), (c = e.querySelector('input[name="query"]')) == null || c.addEventListener("input", (u) => this.search(u.target.value)), (l = e.querySelector('form[data-form="add"]')) == null || l.addEventListener("submit", (u) => void this.addCoupon(u)), (h = e.querySelector('form[data-form="generate"]')) == null || h.addEventListener("submit", (u) => void this.generateCoupons(u)), this.renderRows(), this.syncDisabledState();
  }
  renderRows() {
    var o;
    const e = this.querySelector("tbody");
    if (e == null)
      return;
    if (this.coupons.length === 0) {
      e.innerHTML = '<tr><td colspan="4">No coupon codes found</td></tr>';
      return;
    }
    const t = document.createDocumentFragment();
    for (const a of this.coupons) {
      const s = document.createElement("tr");
      s.innerHTML = `
        <td>${this.escapeHtml(a.couponCode ?? "")}</td>
        <td>${a.numberAvailable ?? ""}</td>
        <td>${this.formatDate(a.date)}</td>
        <td></td>
      `;
      const n = document.createElement("button");
      n.type = "button", n.dataset.kind = "danger", n.textContent = "Delete", n.disabled = this.readonly, n.addEventListener("click", () => void this.deleteCoupon(a.couponCode ?? "")), (o = s.lastElementChild) == null || o.append(n), t.append(s);
    }
    e.replaceChildren(t);
  }
  async loadCoupons() {
    const e = this.getContentKey();
    if (e == null) {
      this.renderCreateMessage();
      return;
    }
    this.setStatus("Loading coupons...");
    try {
      const t = new URLSearchParams({
        query: this.query,
        page: String(this.page),
        pageSize: String(this.pageSize)
      }), o = await this.fetchJson(`/ekom/backoffice/coupon/discountId/${e}?${t}`);
      this.coupons = o.item1 ?? o.data ?? [], this.totalPages = o.item2 ?? o.totalPages ?? 0, this.renderEditor(), this.setStatus("");
    } catch (t) {
      this.setStatus(this.getErrorMessage(t, "Could not load coupons."), !0);
    }
  }
  async addCoupon(e) {
    if (e.preventDefault(), this.readonly)
      return;
    const t = e.currentTarget, o = this.getFormValue(t, "couponCode"), a = this.getFormNumber(t, "numberAvailable");
    if (o.length === 0 || a == null) {
      this.setStatus("Coupon Code and Usage Limit are required fields.", !0);
      return;
    }
    try {
      await this.fetchText(`/ekom/backoffice/coupon/${encodeURIComponent(o)}/NumberAvailable/${a}/discountId/${this.getContentKey()}`, { method: "POST" }), this.hideForms(), t.reset(), await this.loadCoupons(), this.setStatus("Coupon Code added successfully.");
    } catch (s) {
      const n = s;
      this.setStatus(n.status === 409 ? "Coupon Code already exists on this or another discount." : this.getErrorMessage(s, "Error on creating coupon."), !0);
    }
  }
  async generateCoupons(e) {
    if (e.preventDefault(), this.readonly)
      return;
    const t = e.currentTarget, o = {
      count: this.getFormNumber(t, "count") ?? 0,
      numberAvailable: this.getFormNumber(t, "numberAvailable") ?? -1,
      prefix: this.getFormValue(t, "prefix"),
      randomLength: this.getFormNumber(t, "randomLength") ?? 0,
      characterSet: this.getFormValue(t, "characterSet")
    };
    if (o.count <= 0) {
      this.setStatus("Amount must be greater than zero.", !0);
      return;
    }
    if (o.numberAvailable < 0) {
      this.setStatus("Usage Limit is required and can not be negative.", !0);
      return;
    }
    if (o.randomLength <= 0) {
      this.setStatus("Random length must be greater than zero.", !0);
      return;
    }
    try {
      const a = await this.fetchJson(`/ekom/backoffice/coupon/generate/discountId/${this.getContentKey()}`, {
        method: "POST",
        body: JSON.stringify(o),
        headers: {
          "Content-Type": "application/json"
        }
      });
      this.hideForms(), await this.loadCoupons(), this.setStatus(`Generated ${a.created ?? 0} coupon codes.`);
    } catch (a) {
      this.setStatus(this.getErrorMessage(a, "Error on generating coupons."), !0);
    }
  }
  async deleteCoupon(e) {
    if (!(this.readonly || e.length === 0))
      try {
        await this.fetchText(`/ekom/backoffice/coupon/${encodeURIComponent(e)}/discountId/${this.getContentKey()}`, { method: "DELETE" }), await this.loadCoupons(), this.setStatus("Coupon Code removed.");
      } catch (t) {
        this.setStatus(this.getErrorMessage(t, "Error on removing coupon."), !0);
      }
  }
  search(e) {
    this.query = e, this.page = 1, this.loadCoupons();
  }
  changePage(e) {
    e < 1 || this.totalPages > 0 && e > this.totalPages || (this.page = e, this.loadCoupons());
  }
  exportCoupons() {
    const e = this.getContentKey();
    e != null && (window.location.href = `/ekom/backoffice/coupon/export/discountId/${e}`);
  }
  showForm(e) {
    var t;
    this.hideForms(), (t = this.querySelector(`form[data-form="${e}"]`)) == null || t.removeAttribute("hidden");
  }
  hideForms() {
    this.querySelectorAll("form").forEach((e) => e.hidden = !0);
  }
  syncDisabledState() {
    this.querySelectorAll("input, select, button").forEach((e) => {
      e.matches('[data-action="export"], input[name="query"], [data-action="previous"], [data-action="next"]') || (e.disabled = this.readonly);
    });
  }
  getEditor() {
    return this.querySelector(".ekom-coupon-editor");
  }
  setStatus(e, t = !1) {
    this.status != null && (this.status.textContent = e, this.status.dataset.error = String(t));
  }
  getContentKey() {
    return window.location.pathname.split("/").find((e) => /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(e));
  }
  isCreateMode() {
    return window.location.pathname.includes("/workspace/document/create/");
  }
  getFormValue(e, t) {
    var o;
    return (((o = new FormData(e).get(t)) == null ? void 0 : o.toString()) ?? "").trim();
  }
  getFormNumber(e, t) {
    const o = this.getFormValue(e, t);
    if (o.length === 0)
      return;
    const a = Number.parseInt(o, 10);
    return Number.isNaN(a) ? void 0 : a;
  }
  formatDate(e) {
    if (e == null || e.length === 0)
      return "";
    const t = new Date(e);
    return Number.isNaN(t.getTime()) ? e : t.toLocaleString();
  }
  escapeHtml(e) {
    return e.replaceAll("&", "&amp;").replaceAll("<", "&lt;").replaceAll(">", "&gt;").replaceAll('"', "&quot;").replaceAll("'", "&#039;");
  }
  escapeAttribute(e) {
    return this.escapeHtml(e);
  }
  getErrorMessage(e, t) {
    return e instanceof Error ? e.message : t;
  }
  async fetchJson(e, t) {
    const o = await fetch(e, {
      credentials: "same-origin",
      headers: {
        Accept: "application/json",
        ...t == null ? void 0 : t.headers
      },
      ...t
    });
    if (!o.ok)
      throw Object.assign(new Error(`Request to ${e} failed with status ${o.status}.`), { status: o.status });
    return await o.json();
  }
  async fetchText(e, t) {
    const o = await fetch(e, {
      credentials: "same-origin",
      ...t
    });
    if (!o.ok)
      throw Object.assign(new Error(`Request to ${e} failed with status ${o.status}.`), { status: o.status });
    return await o.text();
  }
}
customElements.define("ekom-coupon-editor", b);
export {
  b as EkomCouponEditorElement,
  b as default
};
