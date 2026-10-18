export const chartColors = ["#a78bfa", "#58d5b1", "#63a7f8", "#f5b85c", "#ee7e9d", "#6fd0dc", "#c68bfa", "#8dd16f"];

export function formatChartValue(value: number): string {
  return new Intl.NumberFormat(undefined, {
    notation: Math.abs(value) >= 100000 ? "compact" : "standard",
    maximumFractionDigits: 1,
  }).format(value);
}
