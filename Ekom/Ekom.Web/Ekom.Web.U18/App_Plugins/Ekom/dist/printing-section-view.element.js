var b = Object.defineProperty;
var m = (a, o, e) => o in a ? b(a, o, { enumerable: !0, configurable: !0, writable: !0, value: e }) : a[o] = e;
var c = (a, o, e) => m(a, typeof o != "symbol" ? o + "" : o, e);
const f = "/ekom/printer/manager", i = (a) => String(a ?? "").replace(/[&<>"']/g, (o) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" })[o]), g = (a) => a ? new Date(/[zZ]$|[+-]\d\d:\d\d$/.test(a) ? a : a + "Z").toLocaleString() : "Never", y = (a) => ({
  PendingRender: "Generating slip",
  Ready: "Queued",
  Claimed: "With agent",
  Submitted: "Submitted to Windows",
  NeedsReview: "Needs review",
  Cancelled: "Cancelled",
  RenderFailed: "Generation failed"
})[a] ?? a;
function v(a, o) {
  const e = a.hasHtml ? ["preview", "history"] : ["history"];
  return o && (a.state === "RenderFailed" && e.push("retry"), ["PendingRender", "Ready", "RenderFailed"].includes(a.state) && e.push("cancel"), a.state === "Submitted" && a.hasHtml && e.push("reprint")), e;
}
function $(a) {
  const o = document.createElement("template");
  o.innerHTML = a;
  const e = document.implementation.createHTMLDocument("Picking slip"), s = new Set("div span p h1 h2 h3 h4 h5 h6 header footer main section article aside table thead tbody tfoot tr th td caption colgroup col ul ol li dl dt dd strong em b i u small br hr pre code blockquote img style".split(" ")), r = new Set("script iframe frame object embed svg math template meta link base form input button textarea select".split(" ")), h = new Set("class id style colspan rowspan scope width height alt title".split(" ")), p = (t, l) => {
    if (t.nodeType === Node.TEXT_NODE) {
      l.appendChild(e.createTextNode(t.textContent ?? ""));
      return;
    }
    if (!(t instanceof Element) || t.namespaceURI !== "http://www.w3.org/1999/xhtml") return;
    const n = t.localName;
    if (r.has(n)) return;
    const d = s.has(n) ? e.createElement(n) : e.createElement("span");
    for (const u of Array.from(t.attributes))
      h.has(u.name) && d.setAttribute(u.name, u.value), n === "img" && u.name === "src" && /^data:image\/(png|jpeg|gif|webp);base64,/i.test(u.value) && d.setAttribute("src", u.value);
    for (const u of Array.from(t.childNodes)) p(u, d);
    l.appendChild(d);
  };
  for (const t of Array.from(o.content.childNodes)) p(t, e.body);
  return '<!doctype html><html><head><meta charset="utf-8"><meta http-equiv="Content-Security-Policy" content="default-src &#39;none&#39;; script-src &#39;none&#39;; style-src &#39;unsafe-inline&#39;; img-src data:; font-src data:; base-uri &#39;none&#39;; form-action &#39;none&#39;; connect-src &#39;none&#39;">' + e.head.innerHTML + "</head><body>" + e.body.innerHTML + "</body></html>";
}
class w extends HTMLElement {
  constructor() {
    super(...arguments);
    c(this, "panel", this.attachShadow({ mode: "open" }));
    c(this, "capabilities");
    c(this, "jobs", { jobs: [], total: 0, page: 1, pageSize: 25 });
    c(this, "stations", []);
    c(this, "state", "");
    c(this, "station", "");
    c(this, "search", "");
    c(this, "page", 1);
    c(this, "loading", !0);
    c(this, "error", "");
    c(this, "busy", !1);
    c(this, "generation", 0);
    c(this, "requests", /* @__PURE__ */ new Map());
  }
  connectedCallback() {
    this.render(), this.initialize();
  }
  disconnectedCallback() {
    this.generation++;
  }
  async json(e, s) {
    const r = await fetch(f + e, {
      credentials: "same-origin",
      ...s,
      headers: { Accept: "application/json", ...s == null ? void 0 : s.headers }
    });
    if (!r.ok) throw new Error(r.status === 404 ? "Printing is unavailable or the stored slip has expired." : r.status === 403 ? "You do not have permission to perform this action." : r.status === 409 ? "The job changed or cannot be updated. Refresh the list." : "Could not contact printing. Try refreshing.");
    return r.json();
  }
  async initialize() {
    try {
      this.capabilities = await this.json("/capabilities"), await this.load();
    } catch (e) {
      this.error = String(e.message), this.loading = !1, this.render();
    }
  }
  async load() {
    const e = ++this.generation;
    this.loading = !0, this.error = "", this.render();
    try {
      const s = new URLSearchParams({ page: String(this.page), state: this.state, station: this.station, search: this.search }), [r, h] = await Promise.all([this.json("/jobs?" + s), this.json("/stations")]);
      if (e !== this.generation) return;
      this.jobs = r, this.stations = h;
    } catch (s) {
      e === this.generation && (this.error = s.message);
    } finally {
      e === this.generation && (this.loading = !1, this.render());
    }
  }
  render() {
    var s, r, h, p;
    const e = this.loading || this.busy ? "disabled" : "";
    this.panel.innerHTML = `<style>
      :host{display:block;font:inherit;color:var(--uui-color-text,#303033)} *{box-sizing:border-box}
      main{padding:24px;max-width:1500px;margin:auto} h2{font-size:20px;margin:0 0 16px} h3{font-size:16px}
      section{background:var(--uui-color-surface,#fff);padding:20px;margin-bottom:20px;border:1px solid var(--uui-color-border,#ddd);border-radius:4px}
      .filters,.actions,.pager{display:flex;gap:12px;align-items:end;flex-wrap:wrap;margin-bottom:16px} label{display:grid;gap:5px}
      input,select,button{font:inherit;padding:8px;border:1px solid var(--uui-color-border,#bbb);border-radius:3px;background:var(--uui-color-surface,#fff);color:inherit}
      button:not(:disabled){cursor:pointer} button:disabled{opacity:.5} button:focus-visible,input:focus-visible,select:focus-visible{outline:2px solid var(--uui-color-selected,#3544b1)}
      table{border-collapse:collapse;width:100%;text-align:left} th,td{padding:12px 8px;border-bottom:1px solid var(--uui-color-border,#ddd);vertical-align:top}
      td small{display:block;margin-top:6px} .table{overflow:auto} .actions{gap:6px;margin:0} .actions button{padding:5px 8px}
      .error{color:var(--uui-color-danger,#a22);padding:12px 0}.muted{color:var(--uui-color-text-alt,#666)} .attention{font-weight:600;color:var(--uui-color-warning-emphasis,#794600)}
      .pager{justify-content:space-between;margin:16px 0 0} dialog{width:min(1000px,95vw);height:90vh;background:var(--uui-color-surface,#fff);color:inherit;border:1px solid #aaa;padding:20px} dialog iframe{width:100%;height:calc(100% - 50px);border:1px solid #ddd;background:white} dialog::backdrop{background:#0006}
      @media(max-width:650px){main{padding:12px}section{padding:12px}}
    </style><main aria-busy="${this.loading}"><section><h2>Print jobs</h2>
      <div class="filters"><label>State<select data-filter="state">${[["", "All jobs"], ["attention", "Needs attention"], ["PendingRender", "Generating slip"], ["Ready", "Queued"], ["Claimed", "With agent"], ["Submitted", "Submitted to Windows"], ["Cancelled", "Cancelled"]].map(([t, l]) => `<option value="${t}" ${t === this.state ? "selected" : ""}>${l}</option>`).join("")}</select></label>
      <label>Station<select data-filter="station"><option value="">All stations</option>${this.stations.map((t) => `<option value="${i(t.alias)}" ${t.alias === this.station ? "selected" : ""}>${i(t.alias)}</option>`).join("")}</select></label>
      <label>Order number<input data-filter="search" value="${i(this.search)}" maxlength="100" type="search"></label><button data-refresh ${e}>Refresh</button></div>
      ${this.error ? `<p class="error" role="alert">${i(this.error)}</p>` : ""}
      <p class="muted" aria-live="polite">${this.loading ? "Loading…" : `${this.jobs.total} jobs`}</p>
      <div class="table"><table><thead><tr><th>Order</th><th>Station</th><th>State</th><th>Created</th><th>Actions</th></tr></thead><tbody>
      ${this.jobs.jobs.map((t) => {
      var l;
      return `<tr><td>${i(t.orderNumber)}<small>${i(t.storeAlias)}</small></td><td>${i(t.stationAlias)}<small>${t.copies} ${t.copies === 1 ? "copy" : "copies"}</small></td>
      <td class="${["NeedsReview", "RenderFailed"].includes(t.state) ? "attention" : ""}">${i(y(t.state))}${t.state === "NeedsReview" ? "<small>Check the printer before arranging another copy.</small>" : ""}</td><td>${i(g(t.createdUtc))}</td>
      <td><div class="actions">${v(t, ((l = this.capabilities) == null ? void 0 : l.canManage) === !0).map((n) => `<button data-job="${i(t.id)}" data-action="${n}" ${e}>${{ preview: "Preview", history: "History", retry: "Retry generation", cancel: "Cancel", reprint: "Reprint" }[n]}</button>`).join("")}</div></td></tr>`;
    }).join("")}
      ${!this.loading && this.jobs.jobs.length === 0 ? '<tr><td colspan="5">No print jobs match these filters.</td></tr>' : ""}</tbody></table></div>
      <div class="pager"><button data-page="-1" ${this.page <= 1 || this.loading ? "disabled" : ""}>Previous</button><span>Page ${this.page} of ${Math.max(1, Math.ceil(this.jobs.total / 25))}</span><button data-page="1" ${this.page * 25 >= this.jobs.total || this.loading ? "disabled" : ""}>Next</button></div>
      <p class="muted">Submitted to Windows means accepted by the print queue, not confirmed physical printing.</p></section>
      <section><h2>Stations</h2><p class="muted">Settings are read-only here. Configure printing in appsettings.json and select the printer in the Windows agent.</p>
      <div class="table"><table><thead><tr><th>Station / printer</th><th>Connection</th><th>Last seen</th><th>Trigger settings</th></tr></thead><tbody>${this.stations.map((t) => `<tr><td>${i(t.alias)}<small>${i(t.health.printerName || "Printer not reported")}</small><small>${i(t.stores.join(", "))}</small></td><td>${t.health.online ? "Online" : "Offline"}<small>${t.health.agentVersion ? `Agent ${i(t.health.agentVersion)}${t.health.compatible ? "" : " — incompatible"}` : "No agent connected"}</small></td><td>${i(g(t.health.lastSeenUtc))}</td><td>${t.enabled ? i(t.statuses.join(", ")) : "Disabled"}<small>${i(t.policy)} · ${t.copies} ${t.copies === 1 ? "copy" : "copies"}</small></td></tr>`).join("")}
      ${!this.loading && !this.stations.length ? '<tr><td colspan="4">No stations are available for your stores.</td></tr>' : ""}</tbody></table></div></section><dialog><button data-close>Close</button><div data-detail></div></dialog></main>`, (s = this.panel.querySelector("[data-refresh]")) == null || s.addEventListener("click", () => {
      this.search = this.panel.querySelector('[data-filter="search"]').value, this.page = 1, this.load();
    });
    for (const t of ["state", "station"]) (r = this.panel.querySelector(`[data-filter="${t}"]`)) == null || r.addEventListener("change", (l) => {
      this[t] = l.target.value, this.page = 1, this.load();
    });
    (h = this.panel.querySelector('[data-filter="search"]')) == null || h.addEventListener("keydown", (t) => {
      t.key === "Enter" && this.panel.querySelector("[data-refresh]").click();
    }), this.panel.querySelectorAll("[data-page]").forEach((t) => t.addEventListener("click", () => {
      this.page += Number(t.dataset.page), this.load();
    })), this.panel.querySelectorAll("[data-action]").forEach((t) => t.addEventListener("click", () => {
      this.action(t.dataset.job, t.dataset.action);
    })), (p = this.panel.querySelector("[data-close]")) == null || p.addEventListener("click", () => this.panel.querySelector("dialog").close());
  }
  async action(e, s) {
    var r;
    if (!this.busy)
      try {
        if (s === "preview" || s === "history") {
          const t = this.panel.querySelector("dialog"), l = this.panel.querySelector("[data-detail]");
          if (s === "preview") {
            const n = await this.json(`/jobs/${e}/preview`), d = document.createElement("iframe");
            d.title = "Picking slip preview", d.setAttribute("sandbox", ""), d.referrerPolicy = "no-referrer", d.srcdoc = $(n.html), l.replaceChildren(d), l.style.height = "90%";
          } else {
            const n = await this.json(`/jobs/${e}/history`);
            l.style.height = "auto", l.innerHTML = `<h3>Job history</h3>${n.delivery ? `<p>Claimed: ${i(g(n.delivery.claimedUtc))}</p><p>Reported: ${i(g(n.delivery.reportedUtc))}</p><p>Windows queue reference: ${i(n.delivery.spoolJobId || "Not reported")}</p>` : "<p>Not yet claimed by an agent.</p>"}<ul>${n.actions.map((d) => `<li>${i(g(d.createdUtc))} — ${i(d.action)} by ${i(d.actor)}</li>`).join("")}</ul>`;
          }
          t.showModal();
          return;
        }
        if (!((r = this.capabilities) != null && r.canManage) || !window.confirm(s === "reprint" ? "Create another copy of this stored picking slip?" : s === "cancel" ? "Cancel this queued picking slip?" : "Retry generating this picking slip?")) return;
        const h = e + ":" + s, p = this.requests.get(h) ?? crypto.randomUUID();
        this.requests.set(h, p), this.busy = !0, this.render(), await this.json(`/jobs/${e}/actions`, { method: "POST", headers: {
          "Content-Type": "application/json",
          [this.capabilities.csrfHeader]: this.capabilities.csrfToken
        }, body: JSON.stringify({ action: s, requestId: p }) }), this.requests.delete(h), this.busy = !1, await this.load();
      } catch (h) {
        this.busy = !1, this.error = h.message, this.render();
      }
  }
}
customElements.get("ekom-printing-section-view") || customElements.define("ekom-printing-section-view", w);
export {
  v as availableActions,
  $ as createPreviewDocument,
  w as default,
  y as stateLabel
};
