var w = Object.defineProperty;
var $ = (i, r, e) => r in i ? w(i, r, { enumerable: !0, configurable: !0, writable: !0, value: e }) : i[r] = e;
var p = (i, r, e) => $(i, typeof r != "symbol" ? r + "" : r, e);
const k = (i) => class extends i {
  connectedCallback() {
  }
  disconnectedCallback() {
  }
}, j = "/ekom/printer/manager", a = (i) => String(i ?? "").replace(/[&<>"']/g, (r) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" })[r]), m = (i) => i ? new Date(/[zZ]$|[+-]\d\d:\d\d$/.test(i) ? i : i + "Z").toLocaleString(void 0, { year: "numeric", month: "short", day: "numeric", hour: "2-digit", minute: "2-digit" }) : "Never", S = (i) => ({
  PendingRender: "Generating slip",
  Ready: "Queued",
  Claimed: "With agent",
  Submitted: "Submitted to printer",
  NeedsReview: "Needs review",
  Cancelled: "Cancelled",
  RenderFailed: "Generation failed"
})[i] ?? i;
function z(i, r) {
  const e = i.hasHtml ? ["preview", "history"] : ["history"];
  return r && (i.state === "RenderFailed" && e.push("retry"), ["PendingRender", "Ready", "RenderFailed"].includes(i.state) && e.push("cancel"), i.state === "Submitted" && i.hasHtml && e.push("reprint")), e;
}
function C(i) {
  const r = document.createElement("template");
  r.innerHTML = i;
  const e = document.implementation.createHTMLDocument("Picking slip"), n = new Set("div span p h1 h2 h3 h4 h5 h6 header footer main section article aside table thead tbody tfoot tr th td caption colgroup col ul ol li dl dt dd strong em b i u small br hr pre code blockquote img style".split(" ")), o = new Set("script iframe frame object embed svg math template meta link base form input button textarea select".split(" ")), c = new Set("class id style colspan rowspan scope width height alt title".split(" ")), b = (d, g) => {
    if (d.nodeType === Node.TEXT_NODE) {
      g.appendChild(e.createTextNode(d.textContent ?? ""));
      return;
    }
    if (!(d instanceof Element) || d.namespaceURI !== "http://www.w3.org/1999/xhtml") return;
    const l = d.localName;
    if (o.has(l)) return;
    const s = n.has(l) ? e.createElement(l) : e.createElement("span");
    for (const h of Array.from(d.attributes))
      c.has(h.name) && s.setAttribute(h.name, h.value), l === "img" && h.name === "src" && /^data:image\/(png|jpeg|gif|webp);base64,/i.test(h.value) && s.setAttribute("src", h.value);
    for (const h of Array.from(d.childNodes)) b(h, s);
    g.appendChild(s);
  };
  for (const d of Array.from(r.content.childNodes)) b(d, e.body);
  return '<!doctype html><html><head><meta charset="utf-8"><meta http-equiv="Content-Security-Policy" content="default-src &#39;none&#39;; script-src &#39;none&#39;; style-src &#39;unsafe-inline&#39;; img-src data:; font-src data:; base-uri &#39;none&#39;; form-action &#39;none&#39;; connect-src &#39;none&#39;">' + e.head.innerHTML + "</head><body>" + e.body.innerHTML + "</body></html>";
}
class R extends k(HTMLElement) {
  constructor() {
    super(...arguments);
    p(this, "panel", this.attachShadow({ mode: "open" }));
    p(this, "capabilities");
    p(this, "jobs", { jobs: [], total: 0, page: 1, pageSize: 25 });
    p(this, "stations", []);
    p(this, "state", "");
    p(this, "station", "");
    p(this, "search", "");
    p(this, "page", 1);
    p(this, "loading", !0);
    p(this, "error", "");
    p(this, "busy", !1);
    p(this, "generation", 0);
    p(this, "requests", /* @__PURE__ */ new Map());
  }
  connectedCallback() {
    super.connectedCallback(), this.render(), this.initialize();
  }
  disconnectedCallback() {
    this.generation++, super.disconnectedCallback();
  }
  async json(e, n) {
    const o = await fetch(j + e, {
      credentials: "same-origin",
      ...n,
      headers: { Accept: "application/json", ...n == null ? void 0 : n.headers }
    });
    if (!o.ok) throw new Error(o.status === 404 ? "Printing is unavailable or the stored slip has expired." : o.status === 403 ? "You do not have permission to perform this action." : o.status === 409 ? "The job changed or cannot be updated. Refresh the list." : "Could not contact printing. Try refreshing.");
    return o.json();
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
      const n = new URLSearchParams({ page: String(this.page), state: this.state, station: this.station, search: this.search }), [o, c] = await Promise.all([this.json("/jobs?" + n), this.json("/stations")]);
      if (e !== this.generation) return;
      this.jobs = o, this.stations = c;
    } catch (n) {
      e === this.generation && (this.error = n.message);
    } finally {
      e === this.generation && (this.loading = !1, this.render());
    }
  }
  render() {
    var g, l, s, h, x, v, y;
    const e = this.loading || this.busy ? "disabled" : "", n = this.stations.filter((t) => t.health.online && t.health.compatible).length, o = this.jobs.jobs.filter((t) => ["NeedsReview", "RenderFailed"].includes(t.state)).length, c = Math.max(1, Math.ceil(this.jobs.total / this.jobs.pageSize)), b = (t) => ({ NeedsReview: "warning", RenderFailed: "danger", Submitted: "success", Ready: "info", Claimed: "info" })[t] ?? "neutral", d = (t) => t.replace(/([a-z])([A-Z])/g, "$1 $2");
    this.panel.innerHTML = `<style>
      :host{display:block;font:inherit;color:var(--uui-color-text,#20232b);--line:var(--uui-color-border,#e2e4e9);--surface:var(--uui-color-surface,#fff);--subtle:var(--uui-color-surface-alt,#f6f7f9);--muted:var(--uui-color-text-alt,#626773);--accent:var(--uui-color-interactive,#3544b1)}
      *{box-sizing:border-box}main{max-width:1440px;margin:auto;padding:32px}h1,h2,h3,p{margin:0}h1{font-size:28px;letter-spacing:-.6px;line-height:1.2}h2{font-size:18px;letter-spacing:-.2px}h3{font-size:15px}button,input,select{font:inherit;color:inherit}button{cursor:pointer;display:inline-flex;align-items:center;justify-content:center;gap:7px;border:1px solid var(--line);border-radius:6px;background:var(--surface);padding:9px 14px;min-height:38px;font-weight:600;font-size:13px;white-space:nowrap}button:hover:not(:disabled){background:var(--subtle);border-color:var(--muted)}button:disabled{opacity:.45;cursor:default}button:focus-visible,input:focus-visible,select:focus-visible,summary:focus-visible{outline:3px solid var(--accent);outline-offset:3px}.primary{background:var(--accent);color:#fff;border-color:var(--accent)}.primary:hover:not(:disabled){background:var(--accent);filter:brightness(.9)}.page-heading{display:flex;align-items:center;justify-content:space-between;gap:20px;margin-bottom:24px}.subtitle{color:var(--muted);font-size:14px;line-height:1.6;margin-top:7px}.heading-actions{display:flex;align-items:center;gap:16px}.connection{font-size:13px;color:var(--muted);display:flex;align-items:center;gap:8px}.dot{width:7px;height:7px;border-radius:50%;background:currentColor;display:inline-block;flex:none}.connection .dot{color:${n === this.stations.length && n > 0 ? "#18734b" : "#916019"}}
      .card{background:var(--surface);border:1px solid var(--line);border-radius:10px;margin-bottom:24px;overflow:hidden}.section-heading{padding:22px 24px;display:flex;align-items:center;justify-content:space-between;gap:12px}.title-group{display:flex;align-items:center;gap:10px}.count{font-size:12px;padding:3px 8px;border-radius:5px;background:var(--subtle);color:var(--muted);font-weight:600}.filters{display:flex;gap:14px;align-items:end;flex-wrap:wrap;padding:0 24px 22px}.filters label{display:grid;gap:7px;font-size:12px;font-weight:600;color:var(--muted);flex:1;min-width:150px;max-width:240px}.filters label.search{max-width:340px;flex:2}input,select{width:100%;height:40px;padding:0 12px;border:1px solid var(--line);border-radius:6px;background:var(--surface);font-size:14px}input::placeholder{color:var(--muted);opacity:.8}.filters button{height:40px}.notice{display:flex;align-items:center;justify-content:space-between;gap:16px;padding:14px 24px;background:#fff7e8;color:#704800;border-top:1px solid #f1dfba;font-size:13px;line-height:1.5}.notice strong{display:block}.notice button{background:transparent;border-color:#c9ad78;color:#704800}.error{padding:16px 24px;color:#9e2924;background:#fff1f0;border-top:1px solid #efc4c1;line-height:1.5}.table{overflow-x:auto}table{width:100%;border-collapse:collapse;text-align:left;font-size:14px}th{padding:12px 20px;background:var(--subtle);color:var(--muted);font-size:11px;text-transform:uppercase;letter-spacing:.65px;font-weight:600;border-top:1px solid var(--line);border-bottom:1px solid var(--line)}td{padding:18px 20px;border-bottom:1px solid var(--line);vertical-align:middle}th:first-child,td:first-child{padding-left:24px}th:last-child,td:last-child{padding-right:24px}tbody tr:last-child td{border-bottom:0}tbody tr:hover{background:var(--subtle)}td strong{font-size:14px;font-weight:650}td small{display:block;color:var(--muted);font-size:12px;margin-top:5px;line-height:1.5}td:first-child strong{white-space:nowrap}.status-cell{min-width:210px;max-width:290px}.badge{display:inline-flex;align-items:center;gap:6px;border-radius:5px;padding:5px 8px;font-size:12px;font-weight:650;line-height:1.2;white-space:nowrap}.warning{background:#fff0d3;color:#805000}.danger{background:#fde8e6;color:#a22c25}.success{background:#e6f4ec;color:#176340}.info{background:#edf0ff;color:#394d98}.neutral{background:#eef0f3;color:#535b67}.actions{display:flex;gap:6px;flex-wrap:wrap}.actions button{font-size:12px;min-height:32px;padding:6px 10px;font-weight:500}.actions button[data-action="preview"]{color:var(--accent);border-color:transparent;background:var(--subtle)}.created{white-space:nowrap;color:var(--muted);font-size:12px;font-variant-numeric:tabular-nums}.pager{display:flex;justify-content:space-between;align-items:center;gap:16px;padding:15px 24px;border-top:1px solid var(--line);font-size:12px;color:var(--muted)}.pager-controls{display:flex;align-items:center;gap:12px}.pager button{font-size:12px;min-height:32px;padding:6px 10px}.footnote{padding:12px 24px;background:var(--subtle);border-top:1px solid var(--line);color:var(--muted);font-size:12px;line-height:1.6}.empty{text-align:center;padding:44px 20px;color:var(--muted)}.empty strong{display:block;font-size:16px;color:var(--uui-color-text,#20232b);margin-bottom:6px}.empty p{font-size:13px}.empty button{margin-top:16px}
      .stations-heading{display:flex;align-items:center;justify-content:space-between;gap:16px;margin:30px 0 14px}.stations-heading p{font-size:13px;color:var(--muted);margin-top:5px}.station-grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(300px,1fr));gap:16px}.station-card{background:var(--surface);border:1px solid var(--line);border-radius:10px;padding:22px;min-width:0}.station-top{display:flex;justify-content:space-between;align-items:start;gap:12px;margin-bottom:20px}.station-top h3{overflow-wrap:anywhere}.printer-name{font-size:13px;color:var(--muted);margin-top:5px}.station-card dl{display:grid;grid-template-columns:110px minmax(0,1fr);gap:12px 16px;font-size:13px;margin:0}.station-card dt{color:var(--muted)}.station-card dd{margin:0;overflow-wrap:anywhere;line-height:1.5}.chips{display:flex;gap:6px;flex-wrap:wrap}.chip{padding:3px 8px;border:1px solid var(--line);border-radius:4px;font-size:12px;background:var(--subtle)}.station-bottom{margin-top:20px;padding-top:14px;border-top:1px solid var(--line);display:flex;justify-content:space-between;gap:12px;color:var(--muted);font-size:12px}.setup-help{color:var(--muted);font-size:12px;margin:18px 0;line-height:1.7}.setup-help summary{cursor:pointer;width:fit-content;font-weight:600}.setup-help p{margin-top:8px;max-width:700px}dialog{width:min(1000px,95vw);height:90vh;background:var(--surface);color:inherit;border:1px solid var(--line);border-radius:10px;padding:24px}dialog iframe{width:100%;height:100%;border:1px solid var(--line);background:#fff}dialog::backdrop{background:#18203280}.dialog-heading{display:flex;align-items:center;justify-content:space-between;margin-bottom:18px}.muted{color:var(--muted)}
      @media(min-width:901px){.station-card:only-child dl{grid-template-columns:110px minmax(0,1fr) 110px minmax(0,1fr)}}@media(max-width:900px){main{padding:20px}.heading-actions .connection{display:none}.created{white-space:normal}.filters label{max-width:none}.filters label.search{max-width:none}.station-grid{grid-template-columns:1fr}th,td{padding:14px 12px}}@media(max-width:600px){main{padding:16px 10px}.page-heading{align-items:start}h1{font-size:24px}.subtitle{max-width:230px}.section-heading,.filters{padding-left:16px;padding-right:16px}.filters label{min-width:calc(50% - 14px)}.filters label.search{min-width:100%}.filters button{width:100%}.table table{min-width:800px}.notice{align-items:start;flex-direction:column;padding:14px 16px}.pager{padding:14px 16px;align-items:start;flex-direction:column}.pager-controls{width:100%;justify-content:space-between}.station-card{padding:18px}.station-card dl{grid-template-columns:88px 1fr}.footnote{padding:12px 16px}}
    </style><main aria-busy="${this.loading}">
      <header class="page-heading"><div><h1>Printing</h1><p class="subtitle">Track picking slips and warehouse printer connections.</p></div><div class="heading-actions"><span class="connection"><span class="dot"></span>${this.loading ? "Checking stations…" : `${n} of ${this.stations.length} stations online`}</span><button data-reload ${e}>${this.loading ? "Refreshing…" : "Refresh"}</button></div></header>
      <section class="card" aria-labelledby="jobs-heading"><div class="section-heading"><div class="title-group"><h2 id="jobs-heading">Print jobs</h2><span class="count" aria-live="polite">${this.loading ? "Loading…" : `${this.jobs.total} ${this.jobs.total === 1 ? "job" : "jobs"}`}</span></div>${this.state || this.station || this.search ? "<button data-clear>Clear filters</button>" : ""}</div>
      <div class="filters"><label>Status<select data-filter="state">${[["", "All statuses"], ["attention", "Needs attention"], ["PendingRender", "Generating slip"], ["Ready", "Queued"], ["Claimed", "With agent"], ["Submitted", "Submitted to printer"], ["Cancelled", "Cancelled"]].map(([t, u]) => `<option value="${t}" ${t === this.state ? "selected" : ""}>${u}</option>`).join("")}</select></label>
      <label>Station<select data-filter="station"><option value="">All stations</option>${this.stations.map((t) => `<option value="${a(t.alias)}" ${t.alias === this.station ? "selected" : ""}>${a(t.alias)}</option>`).join("")}</select></label>
      <label class="search">Order number<input data-filter="search" value="${a(this.search)}" placeholder="Search by order number" maxlength="100" type="search"></label><button class="primary" data-refresh ${e}>Search</button></div>
      ${this.error ? `<p class="error" role="alert">${a(this.error)}</p>` : ""}
      ${!this.loading && o ? `<div class="notice"><div><strong>${o} ${o === 1 ? "job on this page needs" : "jobs on this page need"} attention</strong>${this.jobs.jobs.some((t) => t.state === "NeedsReview") ? "Check the physical printer before arranging another copy of an uncertain print." : "Review the affected jobs and retry generating their picking slips."}</div>${this.state !== "attention" ? "<button data-attention>View all needing attention</button>" : ""}</div>` : ""}
      <div class="table" tabindex="0" role="region" aria-label="Print jobs; scroll horizontally on small screens"><table><thead><tr><th scope="col">Order</th><th scope="col">Station</th><th scope="col">Status</th><th scope="col">Created</th><th scope="col">Actions</th></tr></thead><tbody>
      ${this.jobs.jobs.map((t) => {
      var u;
      return `<tr><td><strong>${a(t.orderNumber || "Order unavailable")}</strong><small>${a(t.storeAlias)}</small></td><td>${a(t.stationAlias)}<small>${t.copies} ${t.copies === 1 ? "copy" : "copies"}</small></td>
      <td class="status-cell"><span class="badge ${b(t.state)}"><span class="dot"></span>${a(S(t.state))}</span>${t.state === "NeedsReview" ? "<small>Printing could not be confirmed.</small>" : t.state === "RenderFailed" ? "<small>The picking slip could not be generated.</small>" : ""}</td><td class="created">${a(m(t.createdUtc))}</td>
      <td><div class="actions">${z(t, ((u = this.capabilities) == null ? void 0 : u.canManage) === !0).map((f) => `<button aria-label="${a({ preview: "Preview", history: "History for", retry: "Retry generation for", cancel: "Cancel", reprint: "Reprint" }[f] + " " + t.orderNumber)}" data-job="${a(t.id)}" data-action="${f}" ${e}>${{ preview: "Preview", history: "History", retry: "Retry generation", cancel: "Cancel", reprint: "Reprint" }[f]}</button>`).join("")}</div></td></tr>`;
    }).join("")}
      ${this.jobs.jobs.length === 0 ? `<tr><td colspan="5"><div class="empty"><strong>${this.loading ? "Loading print jobs…" : "No print jobs to show"}</strong><p>${this.loading ? "Fetching the latest queue and station information." : this.state || this.station || this.search ? "Try a different order number or clear your filters." : "Picking slips appear here when orders reach a configured status."}</p></div></td></tr>` : ""}</tbody></table></div>
      <div class="pager"><span>${this.jobs.total ? `${(this.page - 1) * this.jobs.pageSize + 1}–${Math.min(this.page * this.jobs.pageSize, this.jobs.total)} of ${this.jobs.total} jobs` : "0 jobs"}</span><div class="pager-controls"><button data-page="-1" ${this.page <= 1 || this.loading ? "disabled" : ""}>Previous</button><span>Page ${this.page} of ${c}</span><button data-page="1" ${this.page >= c || this.loading ? "disabled" : ""}>Next</button></div></div>
      <p class="footnote"><strong>Submitted to printer</strong> confirms delivery to the print queue. It does not confirm that paper was printed.</p></section>
      <div class="stations-heading"><div><h2 id="stations-heading">Printer stations</h2><p>Connection health and automatic printing rules.</p></div><span class="count">${this.stations.length} ${this.stations.length === 1 ? "station" : "stations"}</span></div>
      <section class="station-grid" aria-labelledby="stations-heading">${this.stations.map((t) => `<article class="station-card"><div class="station-top"><div><h3>${a(t.alias)}</h3><p class="printer-name">${a(t.health.printerName || "No printer reported")}</p></div><span class="badge ${t.health.online ? t.health.compatible ? "success" : "warning" : "neutral"}"><span class="dot"></span>${t.health.online ? t.health.compatible ? "Online" : "Update required" : "Offline"}</span></div><dl><dt>Store</dt><dd>${a(t.stores.join(", "))}</dd><dt>Print when</dt><dd><div class="chips">${t.statuses.map((u) => `<span class="chip">${a(d(u))}</span>`).join("") || "No statuses configured"}</div></dd><dt>Copies</dt><dd>${t.copies} ${t.copies === 1 ? "copy" : "copies"} · ${a(t.policy === "OncePerOrder" ? "Once per order" : t.policy === "OncePerStatus" ? "Once per status" : d(t.policy))}</dd><dt>Automation</dt><dd>${t.enabled ? "Enabled" : "Disabled"}</dd><dt>Last connected</dt><dd>${a(m(t.health.lastSeenUtc))}</dd></dl><div class="station-bottom"><span>${t.health.agentVersion ? `Agent ${a(t.health.agentVersion)}` : "Agent not connected"}</span><span>Settings managed externally</span></div></article>`).join("")}
      ${!this.loading && !this.stations.length ? '<div class="station-card empty"><strong>No printer stations</strong><p>No stations are available for your stores.</p></div>' : ""}</section>
      <details class="setup-help"><summary>How do I change printer settings?</summary><p>Change status rules and copies in the website’s Ekom printing configuration. Select the physical printer in the Windows agent. Station settings are read-only here.</p></details>
      <dialog aria-labelledby="detail-heading"><div class="dialog-heading"><h2 id="detail-heading">Print job details</h2><button data-close>Close</button></div><div data-detail></div></dialog></main>`, (g = this.panel.querySelector("[data-refresh]")) == null || g.addEventListener("click", () => {
      this.search = this.panel.querySelector('[data-filter="search"]').value, this.page = 1, this.load();
    }), (l = this.panel.querySelector("[data-reload]")) == null || l.addEventListener("click", () => {
      this.load();
    }), (s = this.panel.querySelector("[data-clear]")) == null || s.addEventListener("click", () => {
      this.state = "", this.station = "", this.search = "", this.page = 1, this.load();
    }), (h = this.panel.querySelector("[data-attention]")) == null || h.addEventListener("click", () => {
      this.state = "attention", this.station = "", this.search = "", this.page = 1, this.load();
    });
    for (const t of ["state", "station"]) (x = this.panel.querySelector(`[data-filter="${t}"]`)) == null || x.addEventListener("change", (u) => {
      this[t] = u.target.value, this.page = 1, this.load();
    });
    (v = this.panel.querySelector('[data-filter="search"]')) == null || v.addEventListener("keydown", (t) => {
      t.key === "Enter" && this.panel.querySelector("[data-refresh]").click();
    }), this.panel.querySelectorAll("[data-page]").forEach((t) => t.addEventListener("click", () => {
      this.page += Number(t.dataset.page), this.load();
    })), this.panel.querySelectorAll("[data-action]").forEach((t) => t.addEventListener("click", () => {
      this.action(t.dataset.job, t.dataset.action);
    })), (y = this.panel.querySelector("[data-close]")) == null || y.addEventListener("click", () => this.panel.querySelector("dialog").close());
  }
  async action(e, n) {
    var o;
    if (!this.busy)
      try {
        if (n === "preview" || n === "history") {
          const d = this.panel.querySelector("dialog"), g = this.panel.querySelector("[data-detail]");
          if (n === "preview") {
            const l = await this.json(`/jobs/${e}/preview`), s = document.createElement("iframe");
            s.title = "Picking slip preview", s.setAttribute("sandbox", ""), s.referrerPolicy = "no-referrer", s.srcdoc = C(l.html), g.replaceChildren(s), g.style.height = "90%";
          } else {
            const l = await this.json(`/jobs/${e}/history`);
            g.style.height = "auto", g.innerHTML = `<h3>Job history</h3>${l.delivery ? `<p>Claimed: ${a(m(l.delivery.claimedUtc))}</p><p>Reported: ${a(m(l.delivery.reportedUtc))}</p><p>Windows queue reference: ${a(l.delivery.spoolJobId || "Not reported")}</p>` : "<p>Not yet claimed by an agent.</p>"}<ul>${l.actions.map((s) => `<li>${a(m(s.createdUtc))} — ${a(s.action)} by ${a(s.actor)}</li>`).join("")}</ul>`;
          }
          d.showModal();
          return;
        }
        if (!((o = this.capabilities) != null && o.canManage) || !window.confirm(n === "reprint" ? "Create another copy of this stored picking slip?" : n === "cancel" ? "Cancel this queued picking slip?" : "Retry generating this picking slip?")) return;
        const c = e + ":" + n, b = this.requests.get(c) ?? crypto.randomUUID();
        this.requests.set(c, b), this.busy = !0, this.render(), await this.json(`/jobs/${e}/actions`, { method: "POST", headers: {
          "Content-Type": "application/json",
          [this.capabilities.csrfHeader]: this.capabilities.csrfToken
        }, body: JSON.stringify({ action: n, requestId: b }) }), this.requests.delete(c), this.busy = !1, await this.load();
      } catch (c) {
        this.busy = !1, this.error = c.message, this.render();
      }
  }
}
customElements.get("ekom-printing-section-view") || customElements.define("ekom-printing-section-view", R);
export {
  z as availableActions,
  C as createPreviewDocument,
  R as default,
  S as stateLabel
};
