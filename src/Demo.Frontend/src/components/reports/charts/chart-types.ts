export type ChartPoint = { label: string; value: number; xValue: number };

export type AggregateChartPoint = ChartPoint & {
  series: string;
  metric: string;
  rowIndex: number;
};

export type SimpleChartData = {
  kind: "simple";
  points: ChartPoint[];
};

export type AggregateChartData = {
  kind: "aggregate";
  metrics: string[];
  points: AggregateChartPoint[];
  categories: string[];
  seriesNames: string[];
  breakdowns?: Record<string, AggregateChartData>;
};

export type ChartData = SimpleChartData | AggregateChartData;
