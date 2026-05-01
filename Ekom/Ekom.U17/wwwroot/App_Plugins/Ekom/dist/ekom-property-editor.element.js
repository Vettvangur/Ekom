//#region src/property-editors/ekom-property-editor.element.ts
var e = class extends HTMLElement {
	input;
	internalValue = "";
	get value() {
		return this.internalValue;
	}
	set value(e) {
		this.internalValue = e ?? "", this.input != null && this.input.value !== this.internalValue && (this.input.value = this.internalValue);
	}
	get readonly() {
		return this.hasAttribute("readonly");
	}
	set readonly(e) {
		this.toggleAttribute("readonly", e), this.input != null && (this.input.readOnly = e);
	}
	connectedCallback() {
		this.render();
	}
	attributeChangedCallback(e) {
		e === "readonly" && this.input != null && (this.input.readOnly = this.readonly);
	}
	render() {
		this.innerHTML = "\n      <style>\n        :host {\n          display: block;\n        }\n\n        label {\n          display: grid;\n          gap: var(--uui-size-space-2, 8px);\n          color: var(--uui-color-text, #1b264f);\n        }\n\n        span {\n          font-weight: 600;\n        }\n\n        textarea {\n          box-sizing: border-box;\n          min-height: 120px;\n          width: 100%;\n          padding: var(--uui-size-space-3, 12px);\n          border: 1px solid var(--uui-color-border, #d8d7d9);\n          border-radius: var(--uui-border-radius, 3px);\n          color: var(--uui-color-text, #1b264f);\n          font: inherit;\n          resize: vertical;\n        }\n\n        small {\n          color: var(--uui-color-text-alt, #515054);\n        }\n      </style>\n      <label>\n        <span>Ekom value</span>\n        <textarea spellcheck=\"false\"></textarea>\n        <small>This placeholder editor preserves the JSON value until the specialized Ekom UI is ported.</small>\n      </label>\n    ", this.input = this.querySelector("textarea") ?? void 0, this.input != null && (this.input.value = this.internalValue, this.input.readOnly = this.readonly, this.input.addEventListener("input", () => {
			this.internalValue = this.input?.value ?? "", this.dispatchEvent(new CustomEvent("change", {
				bubbles: !0,
				composed: !0
			}));
		}));
	}
	static get observedAttributes() {
		return ["readonly"];
	}
};
customElements.define("ekom-property-editor", e);
//#endregion
export { e as EkomPropertyEditorElement, e as default };
