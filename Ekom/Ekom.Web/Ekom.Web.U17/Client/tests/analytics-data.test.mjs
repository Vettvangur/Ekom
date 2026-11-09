import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import test from 'node:test';
import {runInNewContext} from 'node:vm';
import ts from 'typescript';
function module(name, globals = {}) {
 const exports = {};
 runInNewContext(ts.transpileModule(readFileSync(new URL(`../src/manager/${name}.ts`, import.meta.url),'utf8'), {compilerOptions:{module:ts.ModuleKind.CommonJS,target:ts.ScriptTarget.ES2022}}).outputText,{exports,...globals});
 return exports;
}
const data = module('analytics-data');
function chart(start, amounts, orders) {
 const points = values => values.map((y,i)=>({x:new Date(Date.parse(start)+i*86400000).toISOString().slice(0,10),y}));
 return {revenueChart:{points:points(amounts)},ordersChart:{points:points(orders)},avarageChart:{points:[]}};
}
test('comparison and presets cross leap day and year boundaries',()=>{
 assert.equal(data.previousPeriod('2024-03-01','2024-03-02').dateFrom,'2024-02-28');
 assert.equal(data.previousPeriod('2024-01-01','2024-01-07').dateTo,'2023-12-31');
 assert.equal(data.presetRange('7',new Date(2026,0,3)).dateFrom,'2025-12-28');
 assert.equal(data.presetRange('year',new Date(2026,10,8)).dateFrom,'2026-01-01');
});
test('calendar groups are clipped and averages are weighted by orders',()=>{
 const current=chart('2026-01-31',[100,900,0],[1,3,0]);
 const previous=chart('2026-01-28',[10,20,0],[1,1,0]);
 const months=data.groupAnalytics(current,previous,'month');
 assert.equal(months.length,2);
 assert.equal(months[1].end,'2026-02-02');
 assert.equal(months[1].average,300);
 assert.equal(months[1].previousStart,'2026-01-29');
 assert.equal(data.analyticsSummary(current).average,250);
 const weeks=data.groupAnalytics(current,previous,'week');
 assert.equal(weeks[0].end,'2026-02-01');
 assert.equal(weeks[1].average,null);
});
function view() {
 const shared=module('manager-shared');
 const exports=module('analytics-section-view.element',{
 require:name=>name==='./manager-shared'?shared:name==='./analytics-data'?data:{UmbElementMixin:Base=>Base},
 HTMLElement:class {hasAttribute(){return true;} dispatchEvent(event){this.event=event;}},
 customElements:{define(){}},AbortController,CustomEvent:class {constructor(type,options){this.type=type;this.detail=options.detail;}}
 });
 const instance=new exports.EkomAnalyticsSectionViewElement();
 instance.render=()=>{};instance.compare=false;
 shared.managerState.filters.store='main';
 return {instance,shared};
}
test('late chart response cannot overwrite the latest filters or loading state',async()=>{
 const {instance,shared}=view();const pending=[];
 instance.api.charts=(filters,signal)=>new Promise(resolve=>pending.push({resolve,signal}));
 const old=instance.loadCharts();shared.managerState.filters.store='other';const latest=instance.loadCharts();
 assert.equal(pending[0].signal.aborted,true);
 const latestData=chart('2026-01-01',[22],[2]);pending[1].resolve(latestData);await latest;
 pending[0].resolve(chart('2026-01-01',[99],[9]));await old;
 assert.equal(instance.chartData,latestData);assert.equal(instance.chartFilters.store,'other');assert.equal(instance.loading,false);
});
test('product queries are on demand and late failures do not replace current results',async()=>{
 const {instance}=view();let calls=0;instance.loadCharts=async()=>{};
 instance.api.mostSoldProducts=()=>{calls++;};await instance.loadAnalytics();assert.equal(calls,0);
 const pending=[];instance.api.mostSoldProducts=()=>new Promise((resolve,reject)=>pending.push({resolve,reject}));
 const old=instance.loadMostSoldProducts(),latest=instance.loadMostSoldProducts();
 pending[1].resolve({products:[{title:'latest'}],page:1});await latest;
 pending[0].reject(new Error('old error'));await old;
 assert.equal(instance.mostSoldProducts.products[0].title,'latest');assert.equal(instance.productError,'');
});
test('drill through uses the selected bucket and clears hidden order filters',()=>{
 const {instance,shared}=view();instance.chartFilters={...shared.managerState.filters,store:'main',query:'hidden',productSku:'old'};
 instance.chartData=chart('2026-01-31',[100,900,0],[1,3,0]);instance.grouping='month';instance.openOrders(1);
 assert.equal(instance.event.type,'analytics-open-orders');assert.equal(instance.event.detail.dateFrom,'2026-02-01');
 assert.equal(instance.event.detail.dateTo,'2026-02-02');assert.equal(instance.event.detail.store,'main');
 assert.equal(instance.event.detail.query,'');assert.equal(instance.event.detail.productSku,'');
});

test('customer insight cards and time charts include zero buckets and explain overlapping buyer counts', () => {
 const {instance}=view();
 instance.chartData={...chart('2026-01-05',[350],[3]),customerInsights:{uniqueCustomers:2,newCustomers:1,returningCustomers:2,returningCustomerRate:100,firstOrders:1,repeatOrders:2,firstOrderAmount:100,repeatOrderAmount:250,missingIdentifiers:1,identityField:'CustomerEmail',orderTimes:[{weekday:0,hour:10,orders:3}]}};
 const html=instance.renderCustomerInsights();
 assert.ok(html.includes('Unique purchasing customers'));
 assert.ok(html.includes('100%'));
 assert.ok(html.includes('buyer can appear in both rows'));
 assert.ok(html.includes('1 qualifying orders have no customer identifier'));
 assert.ok(html.includes('Monday'));assert.ok(html.includes('Sunday'));
 assert.ok(html.includes('00:00'));assert.ok(html.includes('23:00'));
 assert.equal((html.match(/class="analytics-time-row"/g)||[]).length,31);
 instance.chartData.customerInsights.returningCustomerRate=null;
 instance.chartData.customerInsights.uniqueCustomers=0;
 assert.ok(instance.renderCustomerInsights().includes('<strong>—</strong>'));
});

test('date presets recognize shared ranges and preserve explicit choices when ranges overlap', () => {
 const now=new Date(2026,0,1);
 assert.equal(data.matchingPreset('2026-01-01','2026-01-01',undefined,now),'today');
 assert.equal(data.matchingPreset('2026-01-01','2026-01-01','year',now),'year');
 assert.equal(data.matchingPreset('2026-01-01','2026-01-01','month',now),'month');
 assert.equal(data.matchingPreset('2026-01-01','2026-01-01','',now),'');
 assert.equal(data.matchingPreset('2025-12-02','2026-01-01','year',now),'');
 const later=new Date(2026,10,9);
 for (const preset of ['today','7','30','month','year']) {
  const range=data.presetRange(preset,later);
  assert.equal(data.matchingPreset(range.dateFrom,range.dateTo,undefined,later),preset);
 }
});

test('rendered preset selection survives rerenders and follows changed shared dates', () => {
 const {instance,shared}=view();
 instance.bindEvents=()=>{};instance.renderCharts=()=>{};
 const render=Object.getPrototypeOf(instance).render.bind(instance);
 for (const preset of ['year','today','7','30','month']) {
  Object.assign(shared.managerState.filters,data.presetRange(preset));
  instance.datePreset=preset;
  render();render();
  assert.ok(instance.innerHTML.includes(`<option value="${preset}" selected>`));
 }
 Object.assign(shared.managerState.filters,{dateFrom:'2020-02-03',dateTo:'2020-02-05'});
 render();
 assert.doesNotMatch(instance.innerHTML,/<option value="(?:today|7|30|month|year)" selected>/);
});
