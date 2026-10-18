import { useState } from "react";
import type { AggregateChartData, ChartData } from "../chart-types";
import { chartColors } from "../chart-utils";

export default function LineChart({ data }: { data: ChartData }) {
  const [hiddenSeries, setHiddenSeries] = useState<string[]>([]);
  const width = 680;
  const height = 235;

  if (data.kind === "simple") {
    const max = Math.max(...data.points.map((point) => point.value), 1);
    const points = data.points.map((point, index) => ({
      x: data.points.length === 1 ? width / 2 : 22 + (index * (width - 44)) / (data.points.length - 1),
      y: height - 25 - (point.value / max) * (height - 55),
    }));
    const line = points.map((point, index) => `${index ? "L" : "M"} ${point.x} ${point.y}`).join(" ");

    return (
      <div className="report-svg-wrap">
        <svg viewBox={`0 0 ${width} ${height}`} role="img" aria-label="Report trend chart">
          {[0, 1, 2, 3].map((row) => <line key={row} x1="20" x2={width - 16} y1={35 + row * 52} y2={35 + row * 52} className="chart-grid-line" />)}
          <path d={line} className="report-line" />
          {points.map((point, index) => (
            <g key={`${data.points[index].label}-${index}`}>
              <circle cx={point.x} cy={point.y} r="4" className="report-point" />
              {data.points.length < 8 && <text x={point.x} y={height - 5} textAnchor="middle" className="chart-label">{data.points[index].label.slice(0, 12)}</text>}
            </g>
          ))}
        </svg>
      </div>
    );
  }

  return <AggregateLineChart data={data} hiddenSeries={hiddenSeries} onToggleSeries={(series) => {
    setHiddenSeries((hidden) => hidden.includes(series) ? hidden.filter((name) => name !== series) : [...hidden, series]);
  }} />;
}

function AggregateLineChart({ data, hiddenSeries, onToggleSeries }: {
  data: AggregateChartData;
  hiddenSeries: string[];
  onToggleSeries: (series: string) => void;
}) {
  const width = 680;
  const height = 235;
  const isSeriesVisible = (series: string) => !hiddenSeries.includes(series);
  const visiblePoints = data.points.filter((point) => isSeriesVisible(point.series));
  if (!visiblePoints.length) {
    return (
      <>
        <SeriesLegend data={data} isVisible={isSeriesVisible} onToggle={onToggleSeries} />
        <div className="report-chart-empty"><strong>No chart series selected</strong><span>Enable a legend item to show it in the chart.</span></div>
      </>
    );
  }

  const categories = data.categories.length ? data.categories : [...new Set(data.points.map((point) => point.label))];
  const minValue = Math.min(0, ...data.points.map((point) => point.value));
  const maxValue = Math.max(1, ...data.points.map((point) => point.value));
  const linePoints = (seriesPoints: typeof visiblePoints) => seriesPoints.map((point) => ({
    x: categories.length === 1 ? width / 2 : 22 + (categories.indexOf(point.label) * (width - 44)) / (categories.length - 1),
    y: height - 25 - ((point.value - minValue) / (maxValue - minValue || 1)) * (height - 55),
  }));

  return (
    <>
      <SeriesLegend data={data} isVisible={isSeriesVisible} onToggle={onToggleSeries} />
      <div className="report-svg-wrap">
        <svg viewBox={`0 0 ${width} ${height}`} role="img" aria-label="Report trend chart">
          {[0, 1, 2, 3].map((row) => <line key={row} x1="20" x2={width - 16} y1={35 + row * 52} y2={35 + row * 52} className="chart-grid-line" />)}
          {data.seriesNames.filter(isSeriesVisible).map((series) => {
            const seriesPoints = visiblePoints.filter((point) => point.series === series);
            const plotted = linePoints(seriesPoints);
            const line = plotted.map((point, index) => `${index ? "L" : "M"} ${point.x} ${point.y}`).join(" ");
            return <g key={series}>
              <path d={line} className="report-line" style={{ stroke: chartColors[data.seriesNames.indexOf(series) % chartColors.length] }} />
              {plotted.map((point, index) => <g key={`${series}-${index}`}>
                <circle cx={point.x} cy={point.y} r="4" className="report-point" style={{ fill: chartColors[data.seriesNames.indexOf(series) % chartColors.length] }} />
                {data.seriesNames.length === 1 && categories.length < 8 && <text x={point.x} y={height - 5} textAnchor="middle" className="chart-label">{seriesPoints[index].label.slice(0, 12)}</text>}
              </g>)}
            </g>;
          })}
        </svg>
      </div>
    </>
  );
}

function SeriesLegend({ data, isVisible, onToggle }: {
  data: AggregateChartData;
  isVisible: (series: string) => boolean;
  onToggle: (series: string) => void;
}) {
  return <div className="report-series-legend" role="group" aria-label="Chart series">{data.seriesNames.map((series, index) => (
    <label key={series}>
      <input type="checkbox" checked={isVisible(series)} style={{ accentColor: chartColors[index % chartColors.length] }} onChange={() => onToggle(series)} />
      {series}
    </label>
  ))}</div>;
}
