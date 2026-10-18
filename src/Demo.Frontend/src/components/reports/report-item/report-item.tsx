import { useEffect, useMemo, useRef, useState } from "react";
import type { DragEvent } from "react";
import Icon from "../../icon/icon";
import type { QueryResult, ReportDto } from "../../query-builder/query-design/query-builder-slice";
import {
  categoryTone,
  chartOptions,
  defaultReportChartSettings,
  presentationFor,
  reportChartFields,
  reportTitle,
  type ChartType,
  type ReportChartSettings,
} from "../report-model";

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

type AggregateChartPoint = { label: string; series: string; metric: string; value: number; xValue: number; rowIndex: number };

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
  const axis1Fields = fields.axes.filter((field) => chartSettings.axes[field] === "axis1");
  const axis2Fields = fields.axes.filter((field) => chartSettings.axes[field] === "axis2");
  const [hiddenLegendNames, setHiddenLegendNames] = useState<string[]>([]);
  const categoryFields = axis1Fields.length ? axis1Fields : fields.axes.slice(0, 1);
  const xIndex = xMetric ? result.columns.findIndex((column) => column.toLowerCase() === xMetric.toLowerCase()) : -1;
  const chartRows = result.rows.slice(0, 12);
  const categoryForRow = (row: string[], index: number) => {
    const label = categoryFields.map((field) => row[result.columns.indexOf(field)] || "Unspecified").join(" · ");
    return label || row.find((cell, cellIndex) => numberValue(cell) === null && !fields.metrics.includes(result.columns[cellIndex])) || `Item ${index + 1}`;
  };
  const categories = [...new Set(chartRows.map((row, index) => categoryForRow(row, index)))];
  const points: AggregateChartPoint[] = chartRows.flatMap((row, rowIndex) => {
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

  if (!metrics.length) {
    return <div className="report-chart-empty"><strong>No aggregate channels selected</strong><span>Enable at least one aggregate channel in the report details.</span></div>;
  }
  if (!points.length) return <div className="report-chart-empty"><strong>No data to visualize</strong><span>The selected aggregate fields contain no numeric values.</span></div>;

  const colors = ["#a78bfa", "#58d5b1", "#63a7f8", "#f5b85c", "#ee7e9d", "#6fd0dc", "#c68bfa", "#8dd16f"];
  const seriesNames = [...new Set(points.map((point) => point.series))];
  const colorsForSeries = new Map(seriesNames.map((series, index) => [series, colors[index % colors.length]]));
  const colorsForMetric = new Map(metrics.map((metric, index) => [metric, colors[index % colors.length]]));
  const legendNames = type === "bar" ? metrics : seriesNames;
  const isLegendVisible = (name: string) => !hiddenLegendNames.includes(name);
  const toggleLegendName = (name: string) => {
    setHiddenLegendNames((hiddenNames) =>
      hiddenNames.includes(name) ? hiddenNames.filter((hiddenName) => hiddenName !== name) : [...hiddenNames, name],
    );
  };
  const legendColor = (name: string) => type === "bar" ? colorsForMetric.get(name) : colorsForSeries.get(name);
  const visiblePoints = points.filter((point) =>
    isLegendVisible(type === "bar" ? point.metric : type === "donut" ? point.label : point.series),
  );
  const max = Math.max(...visiblePoints.map((point) => Math.abs(point.value)), 1);
  const visibleSeriesNames = seriesNames.filter(isLegendVisible);
  const visibleMetrics = metrics.filter(isLegendVisible);
  const barGroups = chartRows.flatMap((row, rowIndex) => {
    const rowPoints = visiblePoints.filter((point) => point.rowIndex === rowIndex);
    return rowPoints.length ? [{ label: categoryForRow(row, rowIndex), points: rowPoints }] : [];
  });
  const legend = <div className="report-series-legend" role="group" aria-label="Chart series">{legendNames.map((name) => (
    <label key={name}>
      <input type="checkbox" checked={isLegendVisible(name)} style={{ accentColor: legendColor(name) }} onChange={() => toggleLegendName(name)} />
      {name}
    </label>
  ))}</div>;

  if (type === "donut") {
    return (
      <div className="report-donut-multiples">
        {metrics.map((metric) => {
          const allSlices = points.filter((point) => point.metric === metric);
          const slices = allSlices.filter((slice) => isLegendVisible(slice.label));
          const total = slices.reduce((sum, slice) => sum + Math.max(0, slice.value), 0);
          const angleTotal = total || 1;
          let start = 0;
          const metricSlices = slices.map((slice) => {
            const angle = (Math.max(0, slice.value) / angleTotal) * Math.PI * 2;
            const color = colors[allSlices.indexOf(slice) % colors.length];
            const item = { ...slice, start, end: start + angle, color };
            start += angle;
            return item;
          });
          const arcPath = (from: number, to: number) => {
            const radius = 66;
            const x1 = 80 + radius * Math.cos(from - Math.PI / 2);
            const y1 = 80 + radius * Math.sin(from - Math.PI / 2);
            const x2 = 80 + radius * Math.cos(to - Math.PI / 2);
            const y2 = 80 + radius * Math.sin(to - Math.PI / 2);
            return `M 80 80 L ${x1} ${y1} A ${radius} ${radius} 0 ${to - from > Math.PI ? 1 : 0} 1 ${x2} ${y2} Z`;
          };
          return <section className="report-donut-metric" key={metric} aria-label={`${metric} distribution`}>
            <strong>{metric}</strong>
            <div className="report-donut-layout">
              <svg viewBox="0 0 160 160" role="img" aria-label={`${metric} distribution donut chart`}>
                {metricSlices.map((slice, index) => <path key={`${slice.label}-${index}`} d={arcPath(slice.start, slice.end)} fill={slice.color} stroke="#10131a" strokeWidth="2" />)}
                <circle cx="80" cy="80" r="42" fill="#11151e" />
                <text x="80" y="77" textAnchor="middle" className="donut-total">{formatValue(total)}</text>
                <text x="80" y="95" textAnchor="middle" className="donut-caption">TOTAL</text>
              </svg>
              <div className="report-legend">{allSlices.map((slice, index) => <label className="report-legend-item" key={`${slice.label}-${index}`}>
                <input type="checkbox" checked={isLegendVisible(slice.label)} style={{ accentColor: colors[index % colors.length] }} onChange={() => toggleLegendName(slice.label)} />
                <span>{slice.label}</span>
                <b>{formatValue(slice.value)}</b>
              </label>)}</div>
            </div>
          </section>;
        })}
      </div>
    );
  }

  if (!visiblePoints.length) {
    return (
      <>
        {type !== "bar" || legendNames.length > 1 ? legend : null}
        <div className="report-chart-empty"><strong>No chart series selected</strong><span>Enable a legend item to show it in the chart.</span></div>
      </>
    );
  }

  if (type === "bar") {
    const width = 680;
    const left = 120;
    const plotWidth = 420;
    const barHeight = 8;
    const barStep = 12;
    const groupStep = visibleMetrics.length * barStep + 8;
    const top = 24;
    const height = top + barGroups.length * groupStep + 24;

    return (
      <>
        {legendNames.length > 1 && legend}
        <div className="report-svg-wrap">
          <svg viewBox={`0 0 ${width} ${height}`} role="img" aria-label={`Grouped bar chart of ${metrics.join(", ")}`}>
            {[0, 1, 2, 3, 4].map((tick) => {
              const x = left + (plotWidth * tick) / 4;
              const value = (max * tick) / 4;
              return <g key={tick}>
                <line x1={x} x2={x} y1={top - 6} y2={height - 20} className="chart-grid-line" />
                <text x={x} y={height - 4} textAnchor="middle" className="chart-label">{formatValue(value)}</text>
              </g>;
            })}
            {barGroups.map((group, groupIndex) => {
              const groupTop = top + groupIndex * groupStep;
              const contentHeight = group.points.length * barStep - (barStep - barHeight);
              return <g key={`${group.label}-${groupIndex}`}>
                <text x={left - 10} y={groupTop + contentHeight / 2 + 4} textAnchor="end" className="chart-label">{group.label}</text>
                <title>{group.label}</title>
                {group.points.map((point, pointIndex) => {
                  const barWidth = Math.max(3, (Math.abs(point.value) / max) * plotWidth);
                  const y = groupTop + pointIndex * barStep;
                  return <g key={point.series}>
                    <rect x={left} y={y} width={barWidth} height={barHeight} rx="3" fill={colorsForMetric.get(point.metric)} />
                    <text x={left + barWidth + 5} y={y + barHeight - 1} className="chart-label">{formatValue(point.value)}</text>
                  </g>;
                })}
              </g>;
            })}
          </svg>
        </div>
      </>
    );
  }

  if (type === "line" || type === "scatter") {
    const width = 680;
    const height = 235;
    const categoriesForChart = categories.length ? categories : [...new Set(points.map((point) => point.label))];
    const xMin = Math.min(...points.map((point) => point.xValue));
    const xMax = Math.max(...points.map((point) => point.xValue), xMin + 1);
    const minValue = Math.min(0, ...points.map((point) => point.value));
    const maxValue = Math.max(1, ...points.map((point) => point.value));
    const pointFor = (point: AggregateChartPoint, index: number) => ({
      x: type === "scatter"
        ? 22 + ((point.xValue - xMin) / (xMax - xMin)) * (width - 44)
        : categoriesForChart.length === 1 ? width / 2 : 22 + (categoriesForChart.indexOf(point.label) * (width - 44)) / (categoriesForChart.length - 1),
      y: height - 25 - ((point.value - minValue) / (maxValue - minValue || 1)) * (height - 55),
      index,
    });
    return (
      <>
        {legend}
        <div className="report-svg-wrap">
          <svg viewBox={`0 0 ${width} ${height}`} role="img" aria-label={type === "line" ? "Report trend chart" : "Report scatter chart"}>
            {[0, 1, 2, 3].map((row) => <line key={row} x1="20" x2={width - 16} y1={35 + row * 52} y2={35 + row * 52} className="chart-grid-line" />)}
            {visibleSeriesNames.map((series) => {
              const seriesPoints = visiblePoints.filter((point) => point.series === series);
              const plotted = seriesPoints.map((point, index) => pointFor(point, index));
              const line = plotted.map((point, index) => `${index ? "L" : "M"} ${point.x} ${point.y}`).join(" ");
              return <g key={series}>
                {type === "line" && <path d={line} className="report-line" style={{ stroke: colorsForSeries.get(series) }} />}
                {plotted.map((point, index) => <g key={`${series}-${point.index}`}>
                  <circle cx={point.x} cy={point.y} r={type === "scatter" ? 5.5 : 4} className="report-point" style={{ fill: colorsForSeries.get(series) }} />
                  {seriesNames.length === 1 && categoriesForChart.length < 8 && <text x={point.x} y={height - 5} textAnchor="middle" className="chart-label">{seriesPoints[index].label.slice(0, 12)}</text>}
                </g>)}
              </g>;
            })}
          </svg>
        </div>
      </>
    );
  }

  return (
    <>
      {seriesNames.length > 1 && legend}
      <div className="report-bars report-funnel">
        {visiblePoints.map((point, index) => (
          <div className="report-bar-row" key={`${point.label}-${point.series}-${index}`}>
            <span className="report-bar-label" title={`${point.label} · ${point.series}`}>{seriesNames.length > 1 ? `${point.label} · ${point.series}` : point.label}</span>
            <div className="report-bar-track"><i style={{ width: `${Math.max(3, (Math.abs(point.value) / max) * 100)}%`, opacity: 1 - (index % 12) * 0.035, background: colorsForSeries.get(point.series) }} /></div>
            <b>{formatValue(point.value)}</b>
          </div>
        ))}
      </div>
    </>
  );
}

export function ReportChart({ result, type, report, settings, metric, groupBy, xMetric }: {
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
  chartSettings: ReportChartSettings;
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
  report, chartType, result, chartSettings, loading, error,
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
            ? <ReportChart key={chartType} result={result} type={chartType} report={report} settings={chartSettings} metric={presentation.metric} groupBy={presentation.groupBy} xMetric={presentation.xMetric} />
            : loading
              ? <div className="report-card-chart-status"><span className="report-spinner" /> Loading chart…</div>
              : <div className="report-card-chart-status">{error ? <span role="alert">{error}</span> : <><span>Run this report to load its chart.</span><button type="button" onClick={onRun}>Run report <Icon name="play" size={11} /></button></>}</div>}
        </div>
      </section>
    </article>
  );
}
