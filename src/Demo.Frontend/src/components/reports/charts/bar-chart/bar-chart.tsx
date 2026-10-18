import { useState } from "react";
import type { AggregateChartData, ChartData } from "../chart-types";
import { chartColors, formatChartValue } from "../chart-utils";

export default function BarChart({ data }: { data: ChartData }) {
  if (data.kind === "simple") {
    const max = Math.max(...data.points.map((point) => point.value), 1);
    return (
      <div className="report-bars">
        {data.points.map((point, index) => (
          <div className="report-bar-row" key={`${point.label}-${index}`}>
            <span className="report-bar-label" title={point.label}>{point.label}</span>
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
  const [hiddenMetrics, setHiddenMetrics] = useState<string[]>([]);
  const isMetricVisible = (metric: string) => !hiddenMetrics.includes(metric);
  const toggleMetric = (metric: string) => {
    setHiddenMetrics((hidden) => hidden.includes(metric) ? hidden.filter((name) => name !== metric) : [...hidden, metric]);
  };
  const visiblePoints = data.points.filter((point) => isMetricVisible(point.metric));
  if (!visiblePoints.length) {
    return (
      <>
        {data.metrics.length > 1 && <div className="report-series-legend" role="group" aria-label="Chart series">{data.metrics.map((metric, index) => (
          <label key={metric}>
            <input type="checkbox" checked={isMetricVisible(metric)} style={{ accentColor: chartColors[index % chartColors.length] }} onChange={() => toggleMetric(metric)} />
            {metric}
          </label>
        ))}</div>}
        <div className="report-chart-empty"><strong>No chart series selected</strong><span>Enable a legend item to show it in the chart.</span></div>
      </>
    );
  }

  const max = Math.max(...visiblePoints.map((point) => Math.abs(point.value)), 1);
  const visibleMetrics = data.metrics.filter(isMetricVisible);
  const groups = new Map<number, { label: string; points: typeof visiblePoints }>();
  visiblePoints.forEach((point) => {
    const group = groups.get(point.rowIndex);
    if (group) group.points.push(point);
    else groups.set(point.rowIndex, { label: point.label, points: [point] });
  });
  const barGroups = [...groups.values()];
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
      {data.metrics.length > 1 && <div className="report-series-legend" role="group" aria-label="Chart series">{data.metrics.map((metric, index) => (
        <label key={metric}>
          <input type="checkbox" checked={isMetricVisible(metric)} style={{ accentColor: chartColors[index % chartColors.length] }} onChange={() => toggleMetric(metric)} />
          {metric}
        </label>
      ))}</div>}
      <div className="report-svg-wrap">
        <svg viewBox={`0 0 ${width} ${height}`} role="img" aria-label={`Grouped bar chart of ${data.metrics.join(", ")}`}>
          {[0, 1, 2, 3, 4].map((tick) => {
            const x = left + (plotWidth * tick) / 4;
            const value = (max * tick) / 4;
            return <g key={tick}>
              <line x1={x} x2={x} y1={top - 6} y2={height - 20} className="chart-grid-line" />
              <text x={x} y={height - 4} textAnchor="middle" className="chart-label">{formatChartValue(value)}</text>
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
                  <rect x={left} y={y} width={barWidth} height={barHeight} rx="3" fill={chartColors[data.metrics.indexOf(point.metric) % chartColors.length]} />
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
