import { useState } from "react";
import type { AggregateChartData, ChartData } from "../chart-types";
import { chartColors, formatChartValue, niceChartMax, niceChartStep } from "../chart-utils";

export default function BarChart({ data }: { data: ChartData }) {
  if (data.kind === "simple") {
    const max = Math.max(...data.points.map((point) => point.value), 1);
    return (
      <div className="report-bars">
        {data.points.map((point, index) => (
          <div className="report-bar-row" key={`${point.label}-${index}`} title={`${point.label}: ${formatChartValue(point.value)}`}>
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
  const [hiddenSeries, setHiddenSeries] = useState<string[]>([]);
  const isSeriesVisible = (series: string) => !hiddenSeries.includes(series);
  const toggleSeries = (series: string) => {
    setHiddenSeries((hidden) => hidden.includes(series) ? hidden.filter((name) => name !== series) : [...hidden, series]);
  };
  const visiblePoints = data.points.filter((point) => isSeriesVisible(point.series));
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
  const visibleSeries = data.seriesNames.filter(isSeriesVisible);
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
      {data.seriesNames.length > 1 && <div className="report-series-legend" role="group" aria-label="Chart series">{data.seriesNames.map((series, index) => (
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
            return <g key={`${group.label}-${groupIndex}`}>
              <text x="8" y={groupTop + contentHeight / 2 + 4} textAnchor="start" className="chart-label">{group.label.length > 18 ? `${group.label.slice(0, 17)}…` : group.label}</text>
              <title>{group.label}</title>
              {group.points.map((point, pointIndex) => {
                const barWidth = Math.max(3, (Math.abs(point.value) / max) * plotWidth);
                const y = groupTop + pointIndex * barStep;
                const seriesIndex = data.seriesNames.indexOf(point.series);
                const detail = `${group.label}\n${point.series}: ${formatChartValue(point.value)}`;
                const tooltipX = Math.max(left, Math.min(left + barWidth - 190, width - 190));
                const tooltipY = Math.max(2, y - 46);
                return <g key={point.series} className="chart-data-point" tabIndex={0} aria-label={detail}>
                  <title>{detail}</title>
                  <rect x={left} y={y} width={barWidth} height={barHeight} rx="3" fill={chartColors[seriesIndex % chartColors.length]} />
                  <text x={left + barWidth + 5} y={y + barHeight - 1} className="chart-label">{formatChartValue(point.value)}</text>
                  <g className="chart-hover-tooltip" transform={`translate(${tooltipX},${tooltipY})`}>
                    <rect width="184" height="40" rx="5" />
                    <text x="8" y="15" className="chart-tooltip-label">{group.label.slice(0, 28)}</text>
                    <text x="8" y="31" className="chart-tooltip-value">{`${point.series}: ${formatChartValue(point.value)}`.slice(0, 30)}</text>
                  </g>
                </g>;
              })}
            </g>;
          })}
        </svg>
      </div>
    </>
  );
}
