import assert from 'node:assert/strict';
import test from 'node:test';
import { readFileSync } from 'node:fs';
import { runInNewContext } from 'node:vm';
import { JSDOM } from 'jsdom';
import ts from 'typescript';
function fixture() {
 const dom = new JSDOM('<main></main>'); const exports = {};
 runInNewContext(ts.transpileModule(readFileSync(new URL('../src/manager/manager-shared.ts',import.meta.url),'utf8'),{compilerOptions:{module:ts.ModuleKind.CommonJS,target:ts.ScriptTarget.ES2022}}).outputText,{exports,HTMLElement:dom.window.HTMLElement,customElements:dom.window.customElements,CustomEvent:dom.window.CustomEvent});
 exports.registerDateRange(); const element=dom.window.document.createElement('ekom-date-range');element.setAttribute('from','2026-01-15');element.setAttribute('to','2026-01-25');dom.window.document.querySelector('main').append(element);return {dom,element,root:element.shadowRoot,exports};
}
test('range calendar stages changes and only commits on Apply',()=>{
 const {dom,element,root}=fixture();const applied=[];element.addEventListener('date-range-apply',e=>applied.push(e.detail));
 root.querySelector('[data-toggle]').click();assert.equal(root.querySelectorAll('.month').length,2);assert.equal(root.querySelectorAll('[data-preset]').length,8);
 root.querySelector('[data-day="2026-01-20"]').click();assert.equal(root.querySelector('[data-apply]').disabled,true);
 root.querySelector('[data-day="2026-02-03"]').click();assert.equal(element.getAttribute('from'),'2026-01-15');assert.equal(applied.length,0);
 root.querySelector('[data-apply]').click();assert.equal(applied[0].from,'2026-01-20');assert.equal(applied[0].to,'2026-02-03');assert.equal(root.querySelector('[role=dialog]'),null);dom.window.close();
});
test('Cancel and Escape discard preset changes without fetching',()=>{
 const {dom,element,root}=fixture();let changes=0;element.addEventListener('date-range-apply',()=>changes++);
 for(const action of ['cancel','escape']){root.querySelector('[data-toggle]').click();root.querySelector('[data-preset="0"]').click();if(action==='cancel')root.querySelector('[data-cancel]').click();else element.dispatchEvent(new dom.window.KeyboardEvent('keydown',{key:'Escape',bubbles:true}));assert.equal(element.getAttribute('from'),'2026-01-15');assert.equal(root.querySelector('[role=dialog]'),null);}
 assert.equal(changes,0);dom.window.close();
});
test('presets handle leap years and January month boundaries',()=>{
 const {dom,exports}=fixture();const ranges=exports.dateRangePresets(new Date(2024,2,1));assert.equal(ranges[5].from,'2024-02-01');assert.equal(ranges[5].to,'2024-02-29');assert.equal(exports.dateRangePresets(new Date(2026,0,1))[5].from,'2025-12-01');dom.window.close();
});
test('calendar fits a right-side anchor and releases the offset on mobile resize',()=>{
 const {dom,root}=fixture();root.querySelector('[data-toggle]').click();
 const popup=root.querySelector('.popover');const anchor=root.querySelector('.anchor');
 anchor.getBoundingClientRect=()=>({left:535});popup.getBoundingClientRect=()=>({width:760});
 dom.window.innerWidth=920;dom.window.dispatchEvent(new dom.window.Event('resize'));
 assert.equal(popup.style.left,'-399px');
 dom.window.innerWidth=551;dom.window.dispatchEvent(new dom.window.Event('resize'));
 assert.equal(popup.style.left,'');dom.window.close();
});

test('rerender keeps styles connected while replacing results and event targets', () => {
 const {dom,exports}=fixture();const root=dom.window.document.createElement('section');dom.window.document.body.append(root);
 const styles='<style>.result{color:red}</style><link rel="stylesheet" href="/manager.css">';
 exports.renderManagerContent(root,styles+'<button>Old result</button>');
 const sheet=root.querySelector('link'),inline=root.querySelector('style'),old=root.querySelector('button');
 const observer=new dom.window.MutationObserver(()=>{});observer.observe(root,{childList:true});
 exports.renderManagerContent(root,styles+'<button>New result</button>');
 assert.equal(root.querySelector('link'),sheet);assert.equal(root.querySelector('style'),inline);
 assert.equal(root.querySelector('button').textContent,'New result');assert.equal(old.isConnected,false);
 assert.ok(observer.takeRecords().every(record=>![...record.removedNodes].includes(sheet)&&![...record.removedNodes].includes(inline)));
 observer.disconnect();dom.window.close();
});
