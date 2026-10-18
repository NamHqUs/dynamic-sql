import { useEffect, useMemo, useRef, useState } from "react";
import type { DragEvent } from "react";
import Icon from "../../icon/icon";
import type { QueryResult, ReportDto } from "../../query-builder/query-design/query-builder-slice";
import { categoryTone, chartOptions, presentationFor, reportTitle, type ChartType } from "../report-model";

function numberValue(value: string | undefined): number | null {
  if (value === undefined || value.trim() === "") return null;
  const parsed = Number(value.replace(/[$,%]/g, ""));
  return Number.isFinite(parsed) ? parsed : null;
}

function formatValue(value: number): string {
  return new Intl.NumberFormat(undefined, {
    notation: Math.abs(value) >= 100000 ? "compact" : "standard",
    maximumFractionDigits: 1,
  }).format(value);
}

export function ReportChart({ result, type, metric, groupBy, xMetric }: {
  result: QueryResult;
  type: ChartType;
  metric?: string;
  groupBy?: string;
  xMetric?: string;
}) {
  const chartData = useMemo(() => {
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
      const label = groupIndex >= 0 ? row[groupIndex]
        : row.find((cell, cellIndex) => cellIndex !== numericIndex && numberValue(cell) === null)
        || result.columns.find((_, columnIndex) => columnIndex === numericIndex)
        || `Item ${index + 1}`;
      return { label, value: Math.max(0, numeric), xValue: xIndex >= 0 ? values[xIndex] ?? index + 1 : index + 1 };
    });
  }, [groupBy, metric, result, xMetric]);

  if (!chartData.length) return <div className="report-chart-empty"><strong>No data to visualize</strong><span>The query returned no rows.</span></div>;

  const max = Math.max(...chartData.map((item) => item.value), 1);
  const width = 680;
  const height = 235;
  const xMin = Math.min(...chartData.map((item) => item.xValue));
  const xMax = Math.max(...chartData.map((item) => item.xValue), xMin + 1);
  const points = chartData.map((item, index) => ({
    x: type === "scatter"
      ? 22 + ((item.xValue - xMin) / (xMax - xMin)) * (width - 44)
      : chartData.length === 1 ? width / 2 : 22 + (index * (width - 44)) / (chartData.length - 1),
    y: height - 25 - (item.value / max) * (height - 55),
  }));
  const total = chartData.reduce((sum, item) => sum + item.value, 0) || 1;

  if (type === "donut") {
    let start = 0;
    const colors = ["#a78bfa", "#58d5b1", "#63a7f8", "#f5b85c", "#ee7e9d", "#6fd0dc"];
    const slices = chartData.map((item, index) => {
      const angle = (item.value / total) * Math.PI * 2;
      const slice = { ...item, start, end: start + angle, color: colors[index % colors.length] };
      start += angle;
      return slice;
    });
    const arcPath = (from: number, to: number) => {
      const radius = 82;
      const x1 = 105 + radius * Math.cos(from - Math.PI / 2);
      const y1 = 105 + radius * Math.sin(from - Math.PI / 2);
      const x2 = 105 + radius * Math.cos(to - Math.PI / 2);
      const y2 = 105 + radius * Math.sin(to - Math.PI / 2);
      return `M 105 105 L ${x1} ${y1} A ${radius} ${radius} 0 ${to - from > Math.PI ? 1 : 0} 1 ${x2} ${y2} Z`;
    };
    return (
      <div className="report-donut-layout">
        <svg viewBox="0 0 210 210" role="img" aria-label="Report distribution donut chart">
          {slices.map((slice) => <path key={slice.label} d={arcPath(slice.start, slice.end)} fill={slice.color} stroke="#10131a" strokeWidth="2" />)}
          <circle cx="105" cy="105" r="52" fill="#11151e" />
          <text x="105" y="101" textAnchor="middle" className="donut-total">{formatValue(total)}</text>
          <text x="105" y="119" textAnchor="middle" className="donut-caption">TOTAL</text>
        </svg>
        <div className="report-legend">{slices.map((slice) => <div className="report-legend-item" key={slice.label}><i style={{ background: slice.color }} /><span>{slice.label}</span><b>{formatValue(slice.value)}</b></div>)}</div>
      </div>
    );
  }

  if (type === "line" || type === "scatter") {
    const line = points.map((point, index) => `${index ? "L" : "M"} ${point.x} ${point.y}`).join(" ");
    return (
      <div className="report-svg-wrap">
        <svg viewBox={`0 0 ${width} ${height}`} role="img" aria-label={type === "line" ? "Report trend chart" : "Report scatter chart"}>
          {[0, 1, 2, 3].map((row) => <line key={row} x1="20" x2={width - 16} y1={35 + row * 52} y2={35 + row * 52} className="chart-grid-line" />)}
          {type === "line" && <path d={line} className="report-line" />}
          {points.map((point, index) => (
            <g key={`${chartData[index].label}-${index}`}>
              <circle cx={point.x} cy={point.y} r={type === "scatter" ? 5.5 : 4} className="report-point" />
              {chartData.length < 8 && <text x={point.x} y={height - 5} textAnchor="middle" className="chart-label">{chartData[index].label.slice(0, 12)}</text>}
            </g>
          ))}
        </svg>
      </div>
    );
  }

  return (
    <div className={`report-bars${type === "funnel" ? " report-funnel" : ""}`}>
      {chartData.map((item, index) => (
        <div className="report-bar-row" key={`${item.label}-${index}`}>
          <span className="report-bar-label" title={item.label}>{item.label}</span>
          <div className="report-bar-track"><i style={{ width: `${Math.max(3, (item.value / max) * 100)}%`, opacity: 1 - index * 0.035 }} /></div>
          <b>{formatValue(item.value)}</b>
        </div>
      ))}
    </div>
  );
}

type ReportItemProps = {
  report: ReportDto;
  chartType: ChartType;
  result?: QueryResult;
  loading: boolean;
  error?: string;
  dropPosition?: "before" | "after";
  onChartTypeChange: (chartType: ChartType) => void;
  onOpenDetails: () => void;
  onDragStart: (event: DragEvent<HTMLDivElement>) => void;
  onDragEnd: () => void;
  onDragOver: (event: DragEvent<HTMLElement>) => void;
  onDragLeave: (event: DragEvent<HTMLElement>) => void;
  onDrop: (event: DragEvent<HTMLElement>) => void;
  onRun: () => void;
  onEdit: () => void;
  onDelete: () => void;
  onCustomize: () => void;
};

export default function ReportItem({
  report, chartType, result, loading, error,
  dropPosition, onChartTypeChange, onOpenDetails, onDragStart, onDragEnd, onDragOver, onDragLeave, onDrop,
  onRun, onEdit, onDelete, onCustomize,
}: ReportItemProps) {
  const presentation = presentationFor(report);
  const title = reportTitle(report);
  const [chartMenuOpen, setChartMenuOpen] = useState(false);
  const chartTypePickerRef = useRef<HTMLDivElement>(null);
  const chartTypeButtonRef = useRef<HTMLButtonElement>(null);
  const selectedChartOption = chartOptions.find((option) => option.value === chartType) || chartOptions[0];

  useEffect(() => {
    if (!chartMenuOpen) return;
    const closeOnOutsideClick = (event: PointerEvent) => {
      if (event.target instanceof Node && !chartTypePickerRef.current?.contains(event.target)) {
        setChartMenuOpen(false);
      }
    };
    document.addEventListener("pointerdown", closeOnOutsideClick);
    return () => document.removeEventListener("pointerdown", closeOnOutsideClick);
  }, [chartMenuOpen]);

  return (
    <article
      className={`report-dashboard-card tone-${categoryTone(presentation.category)}${dropPosition ? ` drop-${dropPosition}` : ""}`}
      onDragOver={onDragOver}
      onDragLeave={onDragLeave}
      onDrop={onDrop}
    >
      <div
        className="report-dashboard-card-top"
        draggable
        title="Drag this header to reorder the report"
        onDragStart={onDragStart}
        onDragEnd={onDragEnd}
      >
        <h4>{title}</h4>
        <div className="report-dashboard-card-tools">
          <div className="chart-type-picker" ref={chartTypePickerRef} onKeyDown={(event) => {
            if (event.key === "Escape" && chartMenuOpen) {
              setChartMenuOpen(false);
              chartTypeButtonRef.current?.focus();
            }
          }}>
            <button
              className="report-icon-button chart-type-picker-button"
              type="button"
              aria-label={`${title} chart type: ${selectedChartOption.label}`}
              title={`Chart type: ${selectedChartOption.label}`}
              aria-haspopup="true"
              aria-expanded={chartMenuOpen}
              onClick={() => setChartMenuOpen((open) => !open)}
              ref={chartTypeButtonRef}
            >
              <Icon name={selectedChartOption.icon} size={14} />
            </button>
            {chartMenuOpen && <div className="chart-type-picker-menu" role="group" aria-label={`${title} chart types`}>
              {chartOptions.map((option) => (
                <button
                  className={`chart-type-picker-option${option.value === chartType ? " selected" : ""}`}
                  type="button"
                  key={option.value}
                  aria-label={option.label}
                  aria-pressed={option.value === chartType}
                  title={option.label}
                  onClick={() => {
                    onChartTypeChange(option.value);
                    setChartMenuOpen(false);
                  }}
                >
                  <Icon name={option.icon} size={14} />
                </button>
              ))}
            </div>}
          </div>
          <button className="report-icon-button" type="button" aria-label={`Run ${title}`} title="Run report" onClick={onRun} disabled={loading}><Icon name="reset" size={13} /></button>
          {report.canDelete ? <>
            <button className="report-icon-button" type="button" aria-label={`Edit ${title}`} title="Edit report" onClick={onEdit}><Icon name="edit" size={13} /></button>
            <button className="report-icon-button report-delete-button" type="button" aria-label={`Delete ${title}`} title="Delete report" onClick={onDelete}><Icon name="trash" size={13} /></button>
          </> : <button className="report-icon-button" type="button" aria-label={`Customize ${title}`} title="Customize report" onClick={onCustomize}><Icon name="plus" size={13} /></button>}
        </div>
      </div>
      <section className="report-card-visual" aria-label={`${title} chart`}>
        <button
          className="report-card-visual-open"
          type="button"
          aria-label={`Open ${title} report details`}
          onClick={onOpenDetails}
        />
        <div className="report-card-chart">
          {result
            ? <ReportChart key={chartType} result={result} type={chartType} metric={presentation.metric} groupBy={presentation.groupBy} xMetric={presentation.xMetric} />
            : loading
              ? <div className="report-card-chart-status"><span className="report-spinner" /> Loading chart…</div>
              : <div className="report-card-chart-status">{error ? <span role="alert">{error}</span> : <><span>Run this report to load its chart.</span><button type="button" onClick={onRun}>Run report <Icon name="play" size={11} /></button></>}</div>}
        </div>
      </section>
    </article>
  );
}
