import { useMemo } from "react";
import type { ReactNode } from "react";
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

function isDimensionSelected(settings: ReportChartSettings, field: string): boolean {
  return settings.axes[field] === "axis1" || settings.axes[field] === "axis2";
}

function DimensionControls({ fields, settings, onSettingsChange }: {
  fields: string[];
  settings: ReportChartSettings;
  onSettingsChange: (settings: ReportChartSettings) => void;
}) {
  const setEnabled = (field: string, enabled: boolean) => {
    onSettingsChange({
      ...settings,
      axes: {
        ...Object.fromEntries(fields.map((dimension) => [
          dimension,
          dimension === field ? enabled ? "axis1" : "none" : isDimensionSelected(settings, dimension) ? "axis1" : "none",
        ])),
      },
    });
  };

  return (
    <div className="report-dimension-controls" role="group" aria-label="Group by dimensions">
      <div className="report-dimension-heading">
        <span className="report-dimension-title">Dimensions</span>
      </div>
      <div className="report-dimension-options">
        {fields.map((field) => {
          const enabled = isDimensionSelected(settings, field);
          return (
            <button className={`report-dimension-option${enabled ? " selected" : ""}`} type="button" aria-pressed={enabled} key={field} onClick={() => setEnabled(field, !enabled)}>
              <span title={field}>{field}</span>
            </button>
          );
        })}
      </div>
    </div>
  );
}

function AggregateBarChart({ result, type, report, settings, onSettingsChange, xMetric }: {
  result: QueryResult;
  type: ChartType;
  report: ReportDto;
  settings?: ReportChartSettings;
  onSettingsChange?: (settings: ReportChartSettings) => void;
  xMetric?: string;
}) {
  const fields = reportChartFields(report, result);
  const defaults = defaultReportChartSettings(report, result);
  const chartSettings = settings ?? defaults;
  const dimensionControls = onSettingsChange && fields.axes.length > 0
    ? <DimensionControls fields={fields.axes} settings={chartSettings} onSettingsChange={onSettingsChange} />
    : null;
  const wrapChart = (content: ReactNode) => (
    <div className="report-chart-composition">
      {dimensionControls}
      {content}
    </div>
  );
  const metrics = fields.metrics.filter((metric) => chartSettings.metrics.includes(metric));
  if (!metrics.length) {
    return wrapChart(<div className="report-chart-empty"><strong>No aggregate channels selected</strong><span>Enable at least one aggregate channel in the report details.</span></div>);
  }

  const groupingFields = fields.axes.filter((field) => isDimensionSelected(chartSettings, field));
  const labelFields = groupingFields;
  const xIndex = xMetric ? result.columns.findIndex((column) => column.toLowerCase() === xMetric.toLowerCase()) : -1;
  const groupingIndices = groupingFields.map((field) => result.columns.indexOf(field));
  const metricIndices = metrics.map((metric) => ({
    metric,
    index: result.columns.indexOf(metric),
    rollup: fields.metricRollups[metric] ?? "unsupported",
  }));
  const groupRows = (rows: string[][], indices: number[]) => {
    const groups = new Map<string, string[][]>();
    rows.forEach((row) => {
      const key = JSON.stringify(indices.map((index) => row[index] || "Unspecified"));
      const group = groups.get(key);
      if (group) group.push(row);
      else groups.set(key, [row]);
    });
    return groups;
  };
  const rollupGroup = (group: string[][]) => {
    const row = [...group[0]];
    metricIndices.forEach(({ index, rollup }) => {
      if (group.length === 1) return;
      const values = group.map((sourceRow) => numberValue(sourceRow[index])).filter((value): value is number => value !== null);
      if (!values.length) {
        row[index] = "";
      } else if (rollup === "sum") {
        row[index] = String(values.reduce((total, value) => total + value, 0));
      } else if (rollup === "min") {
        row[index] = String(Math.min(...values));
      } else if (rollup === "max") {
        row[index] = String(Math.max(...values));
      }
    });
    return row;
  };
  const rowGroups = groupRows(result.rows, groupingIndices);
  const requiresRollup = [...rowGroups.values()].some((group) => group.length > 1);
  const unsupportedMetric = requiresRollup && metricIndices.find(({ rollup }) => rollup === "unsupported");
  if (unsupportedMetric) {
    return wrapChart(
      <div className="report-chart-empty" role="alert">
        <strong>Cannot combine this aggregate</strong>
        <span>{unsupportedMetric.metric} cannot be accurately rolled up from the current results. Keep all dimensions selected or change the report query to aggregate it at the desired level.</span>
      </div>,
    );
  }

  const chartRows = [...rowGroups.values()].map(rollupGroup).slice(0, 12);
  const categoryForRow = (row: string[], index: number) => {
    const label = labelFields.map((field) => row[result.columns.indexOf(field)] || "Unspecified").join(" · ");
    return label || "All records";
  };
  const categories = [...new Set(chartRows.map((row, index) => categoryForRow(row, index)))];
  const points: AggregateChartData["points"] = chartRows.flatMap((row, rowIndex) => {
    const category = categoryForRow(row, rowIndex);
    return metrics.flatMap((metric) => {
      const metricIndex = result.columns.indexOf(metric);
      const value = numberValue(row[metricIndex]);
      return value === null ? [] : [{
        label: category,
        series: metric,
        metric,
        value,
        xValue: xIndex >= 0 ? numberValue(row[xIndex]) ?? rowIndex + 1 : rowIndex + 1,
        rowIndex,
      }];
    });
  });
  if (!points.length) return wrapChart(<div className="report-chart-empty"><strong>No data to visualize</strong><span>The selected aggregate fields contain no numeric values.</span></div>);

  const breakdowns: Record<string, AggregateChartData> = {};
  const breakdownFields = fields.axes.filter((field) => !groupingFields.includes(field));
  if (groupingFields.length > 0 && breakdownFields.length > 0) {
    const breakdownIndices = breakdownFields.map((field) => result.columns.indexOf(field));
    chartRows.forEach((parentRow, rowIndex) => {
      const parentValues = groupingIndices.map((index) => parentRow[index] || "Unspecified");
      const matchingRows = result.rows.filter((row) =>
        groupingIndices.every((index, valueIndex) => (row[index] || "Unspecified") === parentValues[valueIndex]),
      );
      const childRows = [...groupRows(matchingRows, breakdownIndices).values()].map(rollupGroup).slice(0, 12);
      const childLabel = (row: string[]) =>
        breakdownFields.map((field) => row[result.columns.indexOf(field)] || "Unspecified").join(" · ") || "All records";
      const childPoints: AggregateChartData["points"] = childRows.flatMap((row, childRowIndex) => {
        const category = childLabel(row);
        return metrics.flatMap((metric) => {
          const metricIndex = result.columns.indexOf(metric);
          const value = numberValue(row[metricIndex]);
          return value === null ? [] : [{
            label: category,
            series: metric,
            metric,
            value,
            xValue: xIndex >= 0 ? numberValue(row[xIndex]) ?? childRowIndex + 1 : childRowIndex + 1,
            rowIndex: childRowIndex,
          }];
        });
      });
      if (childPoints.length) {
        breakdowns[categoryForRow(parentRow, rowIndex)] = {
          kind: "aggregate",
          metrics,
          points: childPoints,
          categories: [...new Set(childPoints.map((point) => point.label))],
          seriesNames: [...new Set(childPoints.map((point) => point.series))],
        };
      }
    });
  }

  return wrapChart(renderChart(type, {
      kind: "aggregate",
      metrics,
      points,
      categories,
      seriesNames: [...new Set(points.map((point) => point.series))],
      breakdowns,
    }));
}

export default function ReportChart({ result, type, report, settings, onSettingsChange, metric, groupBy, xMetric }: {
  result: QueryResult;
  type: ChartType;
  report?: ReportDto;
  settings?: ReportChartSettings;
  onSettingsChange?: (settings: ReportChartSettings) => void;
  metric?: string;
  groupBy?: string;
  xMetric?: string;
}) {
  if (report && reportChartFields(report, result).metrics.length) {
    return <AggregateBarChart result={result} type={type} report={report} settings={settings} onSettingsChange={onSettingsChange} xMetric={xMetric} />;
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
