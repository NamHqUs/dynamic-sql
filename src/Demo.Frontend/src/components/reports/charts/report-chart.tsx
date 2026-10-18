import { useMemo } from "react";
import type { QueryResult, ReportDto } from "../../query-builder/query-design/query-builder-slice";
import type { ChartType, ReportChartSettings } from "../report-model";
import { defaultReportChartSettings, reportChartFields } from "../report-model";
import BarChart from "./bar-chart/bar-chart";
import DonutChart from "./donut-chart/donut-chart";
import FunnelChart from "./funnel-chart/funnel-chart";
import LineChart from "./line-chart/line-chart";
import type { AggregateChartData, ChartData, ChartPoint } from "./chart-types";
import ScatterChart from "./scatter-chart/scatter-chart";

function numberValue(value: string | undefined): number | null {
  if (value === undefined || value.trim() === "") return null;
  const parsed = Number(value.replace(/[$,%]/g, ""));
  return Number.isFinite(parsed) ? parsed : null;
}

function renderChart(type: ChartType, data: ChartData) {
  switch (type) {
    case "bar":
      return <BarChart data={data} />;
    case "donut":
      return <DonutChart data={data} />;
    case "line":
      return <LineChart data={data} />;
    case "scatter":
      return <ScatterChart data={data} />;
    case "funnel":
      return <FunnelChart data={data} />;
  }
}

function AggregateReportChart({ result, type, report, settings, xMetric }: {
  result: QueryResult;
  type: ChartType;
  report: ReportDto;
  settings?: ReportChartSettings;
  xMetric?: string;
}) {
  const fields = reportChartFields(report, result);
  const defaults = defaultReportChartSettings(report, result);
  const chartSettings = settings ?? defaults;
  const metrics = fields.metrics.filter((metric) => chartSettings.metrics.includes(metric));
  if (!metrics.length) {
    return <div className="report-chart-empty"><strong>No aggregate channels selected</strong><span>Enable at least one aggregate channel in the report details.</span></div>;
  }

  const axis1Fields = fields.axes.filter((field) => chartSettings.axes[field] === "axis1");
  const axis2Fields = fields.axes.filter((field) => chartSettings.axes[field] === "axis2");
  const categoryFields = axis1Fields.length ? axis1Fields : fields.axes.slice(0, 1);
  const xIndex = xMetric ? result.columns.findIndex((column) => column.toLowerCase() === xMetric.toLowerCase()) : -1;
  const chartRows = result.rows.slice(0, 12);
  const categoryForRow = (row: string[], index: number) => {
    const label = categoryFields.map((field) => row[result.columns.indexOf(field)] || "Unspecified").join(" · ");
    return label || row.find((cell, cellIndex) => numberValue(cell) === null && !fields.metrics.includes(result.columns[cellIndex])) || `Item ${index + 1}`;
  };
  const categories = [...new Set(chartRows.map((row, index) => categoryForRow(row, index)))];
  const points: AggregateChartData["points"] = chartRows.flatMap((row, rowIndex) => {
    const category = categoryForRow(row, rowIndex);
    const axis2 = axis2Fields.map((field) => row[result.columns.indexOf(field)] || "Unspecified").join(" · ");
    return metrics.flatMap((metric) => {
      const metricIndex = result.columns.indexOf(metric);
      const value = numberValue(row[metricIndex]);
      return value === null ? [] : [{
        label: category,
        series: [metric, axis2].filter(Boolean).join(" · "),
        metric,
        value,
        xValue: xIndex >= 0 ? numberValue(row[xIndex]) ?? rowIndex + 1 : rowIndex + 1,
        rowIndex,
      }];
    });
  });
  if (!points.length) return <div className="report-chart-empty"><strong>No data to visualize</strong><span>The selected aggregate fields contain no numeric values.</span></div>;

  return renderChart(type, {
    kind: "aggregate",
    metrics,
    points,
    categories,
    seriesNames: [...new Set(points.map((point) => point.series))],
  });
}

export default function ReportChart({ result, type, report, settings, metric, groupBy, xMetric }: {
  result: QueryResult;
  type: ChartType;
  report?: ReportDto;
  settings?: ReportChartSettings;
  metric?: string;
  groupBy?: string;
  xMetric?: string;
}) {
  if (report && reportChartFields(report, result).metrics.length) {
    return <AggregateReportChart result={result} type={type} report={report} settings={settings} xMetric={xMetric} />;
  }

  const chartData = useMemo<ChartPoint[]>(() => {
    if (!result.rows.length) return [];
    const metricIndex = metric ? result.columns.findIndex((column) => column.toLowerCase() === metric.toLowerCase()) : -1;
    const groupIndex = groupBy ? result.columns.findIndex((column) => column.toLowerCase() === groupBy.toLowerCase()) : -1;
    const xIndex = xMetric ? result.columns.findIndex((column) => column.toLowerCase() === xMetric.toLowerCase()) : -1;
    if (groupIndex >= 0) {
      const groups = new Map<string, number>();
      result.rows.forEach((row) => {
        const label = row[groupIndex] || "Unspecified";
        groups.set(label, (groups.get(label) || 0) + 1);
      });
      return [...groups.entries()].map(([label, value]) => ({ label, value, xValue: value })).slice(0, 12);
    }
    return result.rows.slice(0, 12).map((row, index) => {
      const values = row.map(numberValue);
      const numericIndices = values.flatMap((value, columnIndex) => value === null ? [] : [columnIndex]);
      const preferredIndex = numericIndices.find((columnIndex) =>
        /revenue|amount|total|sum|count|units|quantity|profit|margin|average|age|value|frequency|days/i
          .test(result.columns[columnIndex] || ""));
      const numericIndex = metricIndex >= 0 && values[metricIndex] !== null
        ? metricIndex
        : preferredIndex
        ?? numericIndices.find((columnIndex) => !/^(id|.*id)$/i.test(result.columns[columnIndex] || ""))
        ?? numericIndices[0]
        ?? (row.length > 1 ? 1 : 0);
      const numeric = values[numericIndex] ?? (index + 1);
      const label = row.find((cell, cellIndex) => cellIndex !== numericIndex && numberValue(cell) === null)
        || result.columns.find((_, columnIndex) => columnIndex === numericIndex)
        || `Item ${index + 1}`;
      return { label, value: Math.max(0, numeric), xValue: xIndex >= 0 ? values[xIndex] ?? index + 1 : index + 1 };
    });
  }, [groupBy, metric, result, xMetric]);

  if (!chartData.length) return <div className="report-chart-empty"><strong>No data to visualize</strong><span>The query returned no rows.</span></div>;
  return renderChart(type, { kind: "simple", points: chartData });
}
