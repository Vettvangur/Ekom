import type { ChartData } from './manager-shared';

export type AnalyticsMetric = 'amount' | 'orders' | 'average';
export type AnalyticsBucket = {
  start: string; end: string; amount: number; orders: number; average: number | null;
  previousStart?: string; previousEnd?: string; previousAmount?: number; previousOrders?: number; previousAverage?: number | null;
};
const day = 86400000;
export const dateString = (date: Date): string => date.toISOString().slice(0, 10);
export const parseDate = (value: string): Date => new Date(`${value}T00:00:00Z`);
export function previousPeriod(start: string, end: string): { dateFrom: string; dateTo: string } {
  const first = parseDate(start), last = parseDate(end);
  const days = Math.round((last.getTime() - first.getTime()) / day) + 1;
  return { dateFrom: dateString(new Date(first.getTime() - days * day)), dateTo: dateString(new Date(first.getTime() - day)) };
}
export function presetRange(preset: string, now = new Date()): { dateFrom: string; dateTo: string } {
  const end = new Date(Date.UTC(now.getFullYear(), now.getMonth(), now.getDate()));
  const start = new Date(end);
  if (preset === '7') start.setUTCDate(start.getUTCDate() - 6);
  if (preset === '30') start.setUTCDate(start.getUTCDate() - 29);
  if (preset === 'month') start.setUTCDate(1);
  if (preset === 'year') { start.setUTCMonth(0); start.setUTCDate(1); }
  return { dateFrom: dateString(start), dateTo: dateString(end) };
}
export function groupAnalytics(data: ChartData, previous: ChartData | undefined, grouping: string): AnalyticsBucket[] {
  const result: AnalyticsBucket[] = [];
  const points = data.ordersChart.points;
  let index = 0;
  while (index < points.length) {
    const startIndex = index;
    const start = points[index].x!;
    const boundary = parseDate(start);
    if (grouping === 'month') { boundary.setUTCDate(1); boundary.setUTCMonth(boundary.getUTCMonth() + 1); }
    else if (grouping === 'week') boundary.setUTCDate(boundary.getUTCDate() + (7 - ((boundary.getUTCDay() + 6) % 7)));
    else boundary.setUTCDate(boundary.getUTCDate() + 1);
    let amount = 0, orders = 0, previousAmount = 0, previousOrders = 0;
    do {
      amount += Number(data.revenueChart.points[index]?.y || 0);
      orders += Number(points[index].y || 0);
      previousAmount += Number(previous?.revenueChart.points[index]?.y || 0);
      previousOrders += Number(previous?.ordersChart.points[index]?.y || 0);
      index++;
    } while (index < points.length && parseDate(points[index].x!).getTime() < boundary.getTime());
    result.push({ start, end: points[index - 1].x!, amount, orders, average: orders ? amount / orders : null,
      ...(previous ? { previousStart: previous.ordersChart.points[startIndex]?.x, previousEnd: previous.ordersChart.points[index - 1]?.x,
        previousAmount, previousOrders, previousAverage: previousOrders ? previousAmount / previousOrders : null } : {}),
    });
  }
  return result;
}
export function analyticsSummary(data: ChartData): { amount: number; orders: number; average: number | null } {
  const amount = data.revenueChart.points.reduce((sum, point) => sum + Number(point.y || 0), 0);
  const orders = data.ordersChart.points.reduce((sum, point) => sum + Number(point.y || 0), 0);
  return { amount, orders, average: orders ? amount / orders : null };
}
export function percentageChange(current: number | null, previous: number | null): string {
  if (current == null || previous == null) return 'No comparison available';
  if (previous === 0) return current === 0 ? 'No change' : 'No prior value';
  const change = (current - previous) / Math.abs(previous) * 100;
  return `${change > 0 ? '+' : ''}${change.toLocaleString(undefined, { maximumFractionDigits: 1 })}%`;
}
