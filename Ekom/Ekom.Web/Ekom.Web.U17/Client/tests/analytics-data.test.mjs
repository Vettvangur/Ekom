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
