var E = Object.defineProperty;
var P = (s, o, t) => o in s ? E(s, o, { enumerable: !0, configurable: !0, writable: !0, value: t }) : s[o] = t;
var h = (s, o, t) => P(s, typeof o != "symbol" ? o + "" : o, t);
import { m as f, E as A, e as j, r as q, b as R, q as L, k as N, A as M } from "./manager-shared.js";
import { UmbElementMixin as T } from "@umbraco-cms/backoffice/element-api";
const H = "/ekom/printer/manager", r = (s) => String(s ?? "").replace(/[&<>"']/g, (o) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" })[o]), b = (s) => {
  if (!s) return "Never";
  const o = new Date(/[zZ]$|[+-]\d\d:\d\d$/.test(s) ? s : s + "Z");
  return N() ? M(o) : o.toLocaleString(void 0, { year: "numeric", month: "short", day: "numeric", hour: "2-digit", minute: "2-digit" });
}, x = (s) => ({
  PendingRender: "Generating slip",
  Ready: "Queued",
  Claimed: "With agent",
  ConfirmedPrinted: "Confirmed printed",
  Submitted: "Submitted to printer",
  NeedsReview: "Needs review",
  Cancelled: "Cancelled",
  RenderFailed: "Generation failed"
})[s] ?? s;
function O(s, o) {
  const t = s.hasHtml ? ["preview", "history"] : ["history"];
  return o && (s.state === "RenderFailed" && t.push("retry"), ["PendingRender", "Ready", "RenderFailed"].includes(s.state) && t.push("cancel"), ["Submitted", "ConfirmedPrinted"].includes(s.state) && s.hasHtml && t.push("reprint"), s.state === "NeedsReview" && (t.push("resolve-printed"), s.hasHtml && t.push("resolve-reprint"))), t;
}
function D(s) {
  const o = document.createElement("template");
  o.innerHTML = s;
  const t = document.implementation.createHTMLDocument("Picking slip"), i = new Set("div span p h1 h2 h3 h4 h5 h6 header footer main section article aside table thead tbody tfoot tr th td caption colgroup col ul ol li dl dt dd strong em b i u small br hr pre code blockquote img style".split(" ")), a = new Set("script iframe frame object embed svg math template meta link base form input button textarea select".split(" ")), d = new Set("class id style colspan rowspan scope width height alt title".split(" ")), g = (p, l) => {
    if (p.nodeType === Node.TEXT_NODE) {
      l.appendChild(t.createTextNode(p.textContent ?? ""));
      return;
    }
    if (!(p instanceof Element) || p.namespaceURI !== "http://www.w3.org/1999/xhtml") return;
    const c = p.localName;
    if (a.has(c)) return;
    const n = i.has(c) ? t.createElement(c) : t.createElement("span");
    for (const m of Array.from(p.attributes))
      d.has(m.name) && n.setAttribute(m.name, m.value), c === "img" && m.name === "src" && /^data:image\/(png|jpeg|gif|webp);base64,/i.test(m.value) && n.setAttribute("src", m.value);
    for (const m of Array.from(p.childNodes)) g(m, n);
    l.appendChild(n);
  };
  for (const p of Array.from(o.content.childNodes)) g(p, t.body);
  return '<!doctype html><html><head><meta charset="utf-8"><meta http-equiv="Content-Security-Policy" content="default-src &#39;none&#39;; script-src &#39;none&#39;; style-src &#39;unsafe-inline&#39;; img-src data:; font-src data:; base-uri &#39;none&#39;; form-action &#39;none&#39;; connect-src &#39;none&#39;">' + t.head.innerHTML + "</head><body>" + t.body.innerHTML + "</body></html>";
}
class U extends T(HTMLElement) {
  constructor() {
    super(...arguments);
    h(this, "panel", this.attachShadow({ mode: "open" }));
    h(this, "capabilities");
    h(this, "jobs", { jobs: [], total: 0, page: 1, pageSize: 25 });
    h(this, "stations", []);
    h(this, "state", "");
    h(this, "station", "");
    h(this, "search", "");
    h(this, "page", 1);
    h(this, "loading", !0);
    h(this, "error", "");
    h(this, "busy", !1);
    h(this, "generation", 0);
    h(this, "cancelConfirmation");
    h(this, "requests", /* @__PURE__ */ new Map());
  }
  connectedCallback() {
    var t;
    super.connectedCallback(), this.hasAttribute("classic") || (t = this.consumeContext) == null || t.call(this, "umbThemeContext", (i) => {
      i && this.observe(i.theme, (a) => {
        this.toggleAttribute("data-ekom-dark", a === "umb-dark-theme");
      }, "ekom-manager-theme");
    }), this.render(), this.initialize();
  }
  disconnectedCallback() {
    var t;
    this.generation++, (t = this.cancelConfirmation) == null || t.call(this), super.disconnectedCallback();
  }
  async json(t, i) {
    const a = await fetch(H + t, {
      credentials: "same-origin",
      ...i,
      headers: { Accept: "application/json", ...i == null ? void 0 : i.headers }
    });
    if (!a.ok) throw new Error(a.status === 404 ? "Printing is unavailable or the stored slip has expired." : a.status === 403 ? "You do not have permission to perform this action." : a.status === 409 ? "The job changed or cannot be updated. Refresh the list." : "Could not contact printing. Try refreshing.");
    return a.json();
  }
  async initialize() {
    var t;
    try {
      this.hasAttribute("classic") || (f.stores = await new A().stores(), f.stores.some((i) => i.alias === f.filters.store) || (f.filters.store = ((t = f.stores[0]) == null ? void 0 : t.alias) || ""), j()), this.capabilities = await this.json("/capabilities"), await this.load();
    } catch (i) {
      this.error = String(i.message), this.loading = !1, this.render();
    }
  }
  async load() {
    const t = ++this.generation;
    this.loading = !0, this.error = "", this.render();
    try {
      const i = new URLSearchParams({ page: String(this.page), state: this.state, station: this.station, search: this.search });
      this.hasAttribute("classic") || (i.set("store", f.filters.store), i.set("currency", f.filters.currency || ""));
      const [a, d] = await Promise.all([this.json("/jobs?" + i), this.json("/stations")]);
      if (t !== this.generation) return;
      this.jobs = a, this.stations = this.hasAttribute("classic") ? d : d.filter((g) => g.stores.includes(f.filters.store));
    } catch (i) {
      t === this.generation && (this.error = i.message);
    } finally {
      t === this.generation && (this.loading = !1, this.render());
    }
  }
  render() {
    var m, v, $, w, k, C, S;
    const t = this.loading || this.busy ? "disabled" : "", i = this.stations.filter((e) => e.health.online && e.health.compatible).length, a = this.jobs.jobs.filter((e) => ["NeedsReview", "RenderFailed"].includes(e.state)).length, d = Math.max(1, Math.ceil(this.jobs.total / this.jobs.pageSize)), g = (e) => ({ NeedsReview: "warning", RenderFailed: "danger", Submitted: "success", ConfirmedPrinted: "success", Ready: "info", Claimed: "info" })[e] ?? "neutral", p = (e) => e.replace(/([a-z])([A-Z])/g, "$1 $2");
    q(this.panel, `<link rel="stylesheet" href="/App_Plugins/Ekom/dist/manager-layout.css"><link rel="stylesheet" href="/App_Plugins/Ekom/dist/printing-layout.css"><main aria-busy="${this.loading}">${R("printing", "data-field", !this.hasAttribute("classic"))}
      <header class="page-heading"><div><p class="subtitle">Track picking slips and warehouse printer connections.</p></div><div class="heading-actions"><span class="connection"><span class="dot"></span>${this.loading ? "Checking stations…" : `${i} of ${this.stations.length} stations online`}</span><button data-reload ${t}>${this.loading ? "Refreshing…" : "Refresh"}</button></div></header>
      <section class="card" aria-labelledby="jobs-heading"><div class="ekom-print-toolbar"><div class="section-heading"><div class="title-group"><h2 id="jobs-heading">Print jobs</h2><span class="count" aria-live="polite">${this.loading ? "Loading…" : `${this.jobs.total} ${this.jobs.total === 1 ? "job" : "jobs"}`}</span></div>${this.state || this.station || this.search ? "<button data-clear>Clear filters</button>" : ""}</div>
      <div class="filters"><label>Status<select data-filter="state">${[["", "All statuses"], ["attention", "Needs attention"], ["PendingRender", "Generating slip"], ["Ready", "Queued"], ["Claimed", "With agent"], ["Submitted", "Submitted to printer"], ["ConfirmedPrinted", "Confirmed printed"], ["Cancelled", "Cancelled"]].map(([e, u]) => `<option value="${e}" ${e === this.state ? "selected" : ""}>${u}</option>`).join("")}</select></label>
      <label>Station<select data-filter="station"><option value="">All stations</option>${this.stations.map((e) => `<option value="${r(e.alias)}" ${e.alias === this.station ? "selected" : ""}>${r(e.alias)}</option>`).join("")}</select></label>
      <label class="search">Order number<input data-filter="search" value="${r(this.search)}" placeholder="Search by order number" maxlength="100" type="search"></label></div></div>
      ${this.error ? `<p class="error" role="alert">${r(this.error)}</p>` : ""}
      ${!this.loading && a ? `<div class="notice"><div><strong>${a} ${a === 1 ? "job on this page needs" : "jobs on this page need"} attention</strong>${this.jobs.jobs.some((e) => e.state === "NeedsReview") ? "Check the physical printer before arranging another copy of an uncertain print." : "Review the affected jobs and retry generating their picking slips."}</div>${this.state !== "attention" ? "<button data-attention>View all needing attention</button>" : ""}</div>` : ""}
      <div class="table" tabindex="0" role="region" aria-label="Print jobs; scroll horizontally on small screens"><table><thead><tr><th scope="col">Order</th><th scope="col">Station</th><th scope="col">Status</th><th scope="col">Created</th><th scope="col">Actions</th></tr></thead><tbody>
      ${this.jobs.jobs.map((e) => {
      var u;
      return `<tr><td><strong>${r(e.orderNumber || "Order unavailable")}</strong><small>${r(e.storeAlias)}</small></td><td>${r(e.stationAlias)}<small>${e.copies} ${e.copies === 1 ? "copy" : "copies"}</small></td>
      <td class="status-cell"><span class="badge ${g(e.state)}"><span class="dot"></span>${r(x(e.state))}</span>${e.state === "NeedsReview" ? "<small>Printing could not be confirmed.</small>" : e.state === "RenderFailed" ? "<small>The picking slip could not be generated.</small>" : ""}</td><td class="created">${r(b(e.createdUtc))}</td>
      <td><div class="actions">${O(e, ((u = this.capabilities) == null ? void 0 : u.canManage) === !0).map((y) => `<button aria-label="${r({ preview: "Preview", history: "History for", retry: "Retry generation for", cancel: "Cancel", reprint: "Reprint", "resolve-printed": "Mark as printed", "resolve-reprint": "Print again after checking" }[y] + " " + e.orderNumber)}" data-job="${r(e.id)}" data-action="${y}" ${t}>${{ preview: "Preview", history: "History", retry: "Retry generation", cancel: "Cancel", reprint: "Reprint", "resolve-printed": "Mark as printed", "resolve-reprint": "Print again after checking" }[y]}</button>`).join("")}</div></td></tr>`;
    }).join("")}
      ${this.jobs.jobs.length === 0 ? `<tr><td colspan="5"><div class="empty"><strong>${this.loading ? "Loading print jobs…" : "No print jobs to show"}</strong><p>${this.loading ? "Fetching the latest queue and station information." : this.state || this.station || this.search ? "Try a different order number or clear your filters." : "Picking slips appear here when orders reach a configured status."}</p></div></td></tr>` : ""}</tbody></table></div>
      <div class="pager"><span>${this.jobs.total ? `${(this.page - 1) * this.jobs.pageSize + 1}–${Math.min(this.page * this.jobs.pageSize, this.jobs.total)} of ${this.jobs.total} jobs` : "0 jobs"}</span><div class="pager-controls"><button data-page="-1" ${this.page <= 1 || this.loading ? "disabled" : ""}>Previous</button><span>Page ${this.page} of ${d}</span><button data-page="1" ${this.page >= d || this.loading ? "disabled" : ""}>Next</button></div></div>
      <p class="footnote"><strong>Submitted to printer</strong> confirms delivery to the print queue. It does not confirm that paper was printed.</p></section>
      <div class="stations-heading"><div><h2 id="stations-heading">Printer stations</h2><p>Connection health and automatic printing rules.</p></div><span class="count">${this.stations.length} ${this.stations.length === 1 ? "station" : "stations"}</span></div>
      <section class="station-grid" aria-labelledby="stations-heading">${this.stations.map((e) => `<article class="station-card"><div class="station-top"><div><h3>${r(e.alias)}</h3><p class="printer-name">${r(e.health.printerName || "No printer reported")}</p></div><span class="badge ${e.health.online ? e.health.compatible ? "success" : "warning" : "neutral"}"><span class="dot"></span>${e.health.online ? e.health.compatible ? "Online" : "Update required" : "Offline"}</span></div><dl><dt>Store</dt><dd>${r(e.stores.join(", "))}</dd><dt>Print when</dt><dd><div class="chips">${e.triggerMode === "ExplicitCapture" ? '<span class="chip">Explicit capture API</span>' : e.statuses.map((u) => `<span class="chip">${r(p(u))}</span>`).join("") || "No statuses configured"}</div></dd><dt>Copies</dt><dd>${e.copies} ${e.copies === 1 ? "copy" : "copies"} · ${r(e.policy === "OncePerOrder" ? "Once per order" : e.policy === "OncePerStatus" ? "Once per status" : p(e.policy))}</dd><dt>Automation</dt><dd>${e.enabled ? "Enabled" : "Disabled"}</dd><dt>Last connected</dt><dd>${r(b(e.health.lastSeenUtc))}</dd></dl><div class="station-bottom"><span>${e.health.agentVersion ? `Agent ${r(e.health.agentVersion)}` : "Agent not connected"}</span><span>Settings managed externally</span></div></article>`).join("")}
      ${!this.loading && !this.stations.length ? '<div class="station-card empty"><strong>No printer stations</strong><p>No stations are available for your stores.</p></div>' : ""}</section>
      <details class="setup-help"><summary>How do I change printer settings?</summary><p>Change status rules and copies in the website’s Ekom printing configuration. Select the physical printer in the Windows agent. Station settings are read-only here.</p></details>
      <dialog aria-labelledby="detail-heading"><div class="dialog-heading"><h2 id="detail-heading">Print job details</h2><button data-close>Close</button></div><div data-detail></div></dialog></main>`), L(this.panel, () => {
      this.load();
    }), (m = this.panel.querySelector('[data-field="store"]')) == null || m.addEventListener("change", (e) => {
      f.filters.store = e.target.value, j(), this.station = "", this.page = 1, this.load();
    });
    const l = this.panel.querySelector('[data-filter="search"]'), c = () => {
      this.search !== (l == null ? void 0 : l.value) && (this.search = (l == null ? void 0 : l.value) || "", this.page = 1, this.load());
    };
    l == null || l.addEventListener("change", c), (v = this.panel.querySelector("[data-reload]")) == null || v.addEventListener("click", () => {
      this.load();
    }), ($ = this.panel.querySelector("[data-clear]")) == null || $.addEventListener("click", () => {
      this.state = "", this.station = "", this.search = "", this.page = 1, this.load();
    }), (w = this.panel.querySelector("[data-attention]")) == null || w.addEventListener("click", () => {
      this.state = "attention", this.station = "", this.search = "", this.page = 1, this.load();
    });
    for (const e of ["state", "station"]) (k = this.panel.querySelector(`[data-filter="${e}"]`)) == null || k.addEventListener("change", (u) => {
      this[e] = u.target.value, this.page = 1, this.load();
    });
    (C = this.panel.querySelector('[data-filter="search"]')) == null || C.addEventListener("keydown", (e) => {
      e.key === "Enter" && c();
    }), this.panel.querySelectorAll("[data-page]").forEach((e) => e.addEventListener("click", () => {
      this.page += Number(e.dataset.page), this.load();
    })), this.panel.querySelectorAll("[data-action]").forEach((e) => e.addEventListener("click", () => {
      this.action(e.dataset.job, e.dataset.action);
    }));
    const n = this.panel.querySelector("dialog");
    (S = this.panel.querySelector("[data-close]")) == null || S.addEventListener("click", () => n.close()), n.addEventListener("cancel", (e) => {
      e.preventDefault(), n.close();
    }), n.addEventListener("keydown", (e) => {
      e.key === "Escape" && (e.preventDefault(), e.stopPropagation(), n.close());
    }), n.addEventListener("click", (e) => {
      if (e.target !== n) return;
      const u = n.getBoundingClientRect();
      (e.clientX < u.left || e.clientX > u.right || e.clientY < u.top || e.clientY > u.bottom) && n.close();
    });
  }
  confirmAction(t) {
    return this.cancelConfirmation ? Promise.resolve(!1) : new Promise((i) => {
      const a = document.createElement("dialog");
      a.className = "action-confirmation", a.setAttribute("aria-labelledby", "print-action-title"), a.setAttribute("aria-describedby", "print-action-message"), a.innerHTML = `<h2 id="print-action-title">Confirm printing action</h2><p id="print-action-message">${r(t)}</p><div class="confirmation-actions"><button data-dismiss autofocus>Cancel</button><button class="primary" data-confirm>Confirm</button></div>`;
      const d = (g) => {
        this.cancelConfirmation = void 0, a.remove(), i(g);
      };
      this.cancelConfirmation = () => d(!1), a.addEventListener("close", () => d(a.returnValue === "confirm"), { once: !0 }), a.querySelector("[data-dismiss]").addEventListener("click", () => a.close()), a.querySelector("[data-confirm]").addEventListener("click", () => a.close("confirm")), this.panel.append(a), a.showModal();
    });
  }
  async action(t, i) {
    var a;
    if (!this.busy)
      try {
        if (i === "preview" || i === "history") {
          const p = this.panel.querySelector("dialog"), l = this.panel.querySelector("[data-detail]");
          if (i === "preview") {
            const c = await this.json(`/jobs/${t}/preview`), n = document.createElement("iframe");
            n.title = "Picking slip preview", n.setAttribute("sandbox", ""), n.referrerPolicy = "no-referrer", n.srcdoc = D(c.html), l.replaceChildren(n), l.style.height = "90%";
          } else {
            const c = await this.json(`/jobs/${t}/history`);
            l.style.height = "auto", l.innerHTML = `<h3>Job history</h3>${c.delivery ? `<p>Claimed: ${r(b(c.delivery.claimedUtc))}</p><p>Reported: ${r(b(c.delivery.reportedUtc))}</p><p>Windows queue reference: ${r(c.delivery.spoolJobId || "Not reported")}</p>` : "<p>Not yet claimed by an agent.</p>"}<ul>${c.actions.map((n) => `<li>${r(b(n.createdUtc))} — ${r({ "resolve-printed": "Confirmed physical printing", "resolve-reprint": "Closed uncertain job and queued replacement" }[n.action] ?? n.action)} by ${r(n.actor)}</li>`).join("")}</ul>`;
          }
          p.showModal();
          return;
        }
        if (!((a = this.capabilities) != null && a.canManage) || !await this.confirmAction(i === "resolve-printed" ? "Have you checked the physical output and confirmed this picking slip was printed? This records your confirmation without printing again." : i === "resolve-reprint" ? "Check the physical printer and clear any pending copy from the Windows print queue first. A delayed original may still print. Continue to close this uncertain job and queue one replacement job with the original copy count?" : i === "reprint" ? "Create another copy of this stored picking slip?" : i === "cancel" ? "Cancel this queued picking slip?" : "Retry generating this picking slip?")) return;
        const d = t + ":" + i, g = this.requests.get(d) ?? crypto.randomUUID();
        this.requests.set(d, g), this.busy = !0, this.render(), await this.json(`/jobs/${t}/actions`, { method: "POST", headers: {
          "Content-Type": "application/json",
          [this.capabilities.csrfHeader]: this.capabilities.csrfToken
        }, body: JSON.stringify({ action: i, requestId: g }) }), this.requests.delete(d), this.busy = !1, await this.load();
      } catch (d) {
        this.busy = !1, this.error = d.message, this.render();
      }
  }
}
customElements.get("ekom-printing-section-view") || customElements.define("ekom-printing-section-view", U);
export {
  O as availableActions,
  D as createPreviewDocument,
  U as default,
  x as stateLabel
};
