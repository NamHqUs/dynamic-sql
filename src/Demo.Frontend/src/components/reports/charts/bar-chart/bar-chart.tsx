import { useState } from "react";
import type { AggregateChartData, ChartData } from "../chart-types";
import { chartColors, formatChartValue, niceChartMax, niceChartStep } from "../chart-utils";

export default function BarChart({ data }: { data: ChartData }) {
  if (data.kind === "simple") {
    const max = Math.max(...data.points.map((point) => point.value), 1);
    return (
      <div className="report-bars">
        {data.points.map((point, index) => (
          <div className="report-bar-row" key={`${point.label}-${index}`}>
            <span className="report-bar-label">{point.label}</span>
            <div className="report-bar-track"><i style={{ width: `${Math.max(3, (point.value / max) * 100)}%`, opacity: 1 - index * 0.035 }} /></div>
            <b>{formatChartValue(point.value)}</b>
          </div>
        ))}
      </div>
    );
  }

  return <AggregateBarChart data={data} />;
}

function AggregateBarChart({ data }: { data: AggregateChartData }) {
  const [hiddenSeries, setHiddenSeries] = useState<string[]>([]);
  const [selectedCategory, setSelectedCategory] = useState<string | null>(null);
  const [selectedMetric, setSelectedMetric] = useState<string | null>(null);
  const breakdown = selectedCategory ? data.breakdowns?.[selectedCategory] : undefined;
  const chartData = breakdown && selectedMetric
    ? {
      ...breakdown,
      metrics: breakdown.metrics.filter((metric) => metric === selectedMetric),
      points: breakdown.points.filter((point) => point.metric === selectedMetric),
      seriesNames: breakdown.seriesNames.filter((series) => series === selectedMetric),
    }
    : breakdown ?? data;
  const isSeriesVisible = (series: string) => !hiddenSeries.includes(series);
  const toggleSeries = (series: string) => {
    setHiddenSeries((hidden) => hidden.includes(series) ? hidden.filter((name) => name !== series) : [...hidden, series]);
  };
  const visiblePoints = chartData.points.filter((point) => isSeriesVisible(point.series));
  if (!visiblePoints.length) {
    return (
      <>
        {data.seriesNames.length > 1 && <div className="report-series-legend" role="group" aria-label="Chart series">{data.seriesNames.map((series, index) => (
          <button type="button" aria-pressed={isSeriesVisible(series)} style={{ color: chartColors[index % chartColors.length] }} key={series} onClick={() => toggleSeries(series)}>
            {series}
          </button>
        ))}</div>}
        <div className="report-chart-empty"><strong>No chart series selected</strong><span>Enable a legend item to show it in the chart.</span></div>
      </>
    );
  }

  const rawMax = Math.max(...visiblePoints.map((point) => Math.abs(point.value)), 1);
  const step = niceChartStep(rawMax);
  const max = niceChartMax(rawMax);
  const visibleSeries = chartData.seriesNames.filter(isSeriesVisible);
  const groups = new Map<string, { label: string; points: typeof visiblePoints }>();
  visiblePoints.forEach((point) => {
    const group = groups.get(point.label);
    if (group) group.points.push(point);
    else groups.set(point.label, { label: point.label, points: [point] });
  });
  const barGroups = [...groups.values()];
  const width = 680;
  const left = 120;
  const plotWidth = 420;
  const barHeight = 8;
  const barStep = 12;
  const groupStep = visibleSeries.length * barStep + 8;
  const top = 24;
  const height = top + barGroups.length * groupStep + 24;

  return (
    <>
      {selectedCategory && breakdown && <div className="report-chart-drilldown-heading">
      <button type="button" onClick={() => {
        setSelectedCategory(null);
        setSelectedMetric(null);
      }}>Back to chart</button>
      <span>Breakdown for <strong>{selectedCategory}</strong>{selectedMetric && <> · <strong>{selectedMetric}</strong></>}</span>
      </div>}
      {chartData.seriesNames.length > 1 && <div className="report-series-legend" role="group" aria-label="Chart series">{chartData.seriesNames.map((series, index) => (
        <button type="button" aria-pressed={isSeriesVisible(series)} style={{ color: chartColors[index % chartColors.length] }} key={series} onClick={() => toggleSeries(series)}>
          {series}
        </button>
      ))}</div>}
      <div className="report-svg-wrap">
        <svg viewBox={`0 0 ${width} ${height}`} role="img" aria-label={`Grouped bar chart of ${data.seriesNames.join(", ")}`}>
          {[0, 1, 2, 3, 4, 5].map((tick) => {
            const x = left + (plotWidth * tick) / 5;
            const value = step * tick;
            return <g key={tick}>
              <line x1={x} x2={x} y1={top - 6} y2={height - 20} className="chart-grid-line" />
              <text x={x} y={height - 4} textAnchor="middle" className="chart-label">{formatChartValue(value)}</text>
            </g>;
          })}
          {barGroups.map((group, groupIndex) => {
            const groupTop = top + groupIndex * groupStep;
            const contentHeight = group.points.length * barStep - (barStep - barHeight);
            const childChart = !breakdown && data.breakdowns?.[group.label];
            const openBreakdown = (metric: string) => {
              if (childChart) {
                setSelectedCategory(group.label);
                setSelectedMetric(metric);
              }
            };
            return <g
              key={`${group.label}-${groupIndex}`}
              className={childChart ? "chart-drillable" : undefined}
            >
              <text x={left - 8} y={groupTop + contentHeight / 2 + 4} textAnchor="end" className="chart-label">{group.label.length > 18 ? `${group.label.slice(0, 17)}…` : group.label}</text>
              {group.points.map((point, pointIndex) => {
                const barWidth = Math.max(3, (Math.abs(point.value) / max) * plotWidth);
                const y = groupTop + pointIndex * barStep;
                const seriesIndex = data.seriesNames.indexOf(point.series);
                const detail = `${group.label}\n${point.series}: ${formatChartValue(point.value)}`;
                return <g
                  key={point.series}
                  className="chart-data-point"
                  role={childChart ? "button" : undefined}
                  tabIndex={0}
                  aria-label={childChart ? `Show ${point.metric} breakdown for ${group.label}` : detail}
                  onClick={childChart ? () => openBreakdown(point.metric) : undefined}
                  onKeyDown={childChart ? (event) => {
                    if (event.key === "Enter" || event.key === " ") {
                      event.preventDefault();
                      openBreakdown(point.metric);
                    }
                  } : undefined}
                >
                  <rect x={left} y={y} width={barWidth} height={barHeight} rx="3" fill={chartColors[seriesIndex % chartColors.length]} />
                  <text x={left + barWidth + 5} y={y + barHeight - 1} className="chart-label">{formatChartValue(point.value)}</text>
                </g>;
              })}
            </g>;
          })}
        </svg>
      </div>
    </>
  );
}
