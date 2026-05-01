//#region src/section/ekom-section-view.element.ts
var e = class extends HTMLElement {
	connectedCallback() {
		this.render();
	}
	render() {
		this.innerHTML = "\n      <style>\n        :host {\n          display: block;\n          padding: var(--uui-size-space-5, 20px);\n        }\n\n        .ekom-card {\n          display: grid;\n          gap: var(--uui-size-space-4, 16px);\n          max-width: 960px;\n          padding: var(--uui-size-space-5, 20px);\n          border: 1px solid var(--uui-color-border, #d8d7d9);\n          border-radius: var(--uui-border-radius, 3px);\n          background: var(--uui-color-surface, #fff);\n          color: var(--uui-color-text, #1b264f);\n        }\n\n        h1 {\n          margin: 0;\n          font-size: 1.4rem;\n        }\n\n        p {\n          margin: 0;\n          line-height: 1.5;\n        }\n      </style>\n      <section class=\"ekom-card\">\n        <h1>Ekom</h1>\n        <p>\n          The Umbraco 17 backoffice shell is registered. The order manager UI will be ported from the\n          legacy Angular implementation into Web Components in the next migration slice.\n        </p>\n      </section>\n    ";
	}
};
customElements.define("ekom-section-view", e);
//#endregion
export { e as EkomSectionViewElement, e as default };
