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
  const [expandedPoint, setExpandedPoint] = useState<{ category: string; metric: string } | null>(null);
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
  let nextGroupTop = top;
  const groupLayouts = barGroups.map((group) => {
    const groupTop = nextGroupTop;
    const expandedIndex = expandedPoint?.category === group.label
      ? group.points.findIndex((point) => point.metric === expandedPoint.metric)
      : -1;
    const childPoints = expandedIndex >= 0
      ? (data.breakdowns?.[group.label]?.points.filter((point) =>
      point.metric === expandedPoint.metric && isSeriesVisible(point.series),
      ) ?? [])
      : [];
    const lastPointShift = childPoints.length && group.points.length - 1 > expandedIndex
      ? childPoints.length * barStep + 4
      : 0;
    const categoryCenterY = childPoints.length > 0
      ? groupTop + barHeight / 2
      : groupTop + ((group.points.length - 1) * barStep + lastPointShift) / 2 + barHeight / 2;
    const childTop = groupTop + (expandedIndex + 1) * barStep + 4;
    const longestLabelLength = Math.max(0, ...childPoints.map((point) => Math.min(point.label.length, 16)));
    const spineX = left - 8 - longestLabelLength * 6 - 8 - 2;
    nextGroupTop = groupTop + groupStep + (childPoints.length ? childPoints.length * barStep + 4 : 0);
    return { group, groupTop, categoryCenterY, childPoints, childTop, expandedIndex, spineX };
  });
  const height = nextGroupTop + 24;

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
          {groupLayouts.map(({ group, childPoints, childTop, expandedIndex, spineX }, groupIndex) =>
            expandedIndex >= 0 && childPoints.length > 0
              ? <rect
                key={`${group.label}-${groupIndex}-spine`}
                x={spineX}
                y={childTop - 2}
                width="2"
                height={childPoints.length * barStep}
                className="chart-breakdown-spine"
              />
              : null,
          )}
          {groupLayouts.map(({ group, groupTop, categoryCenterY, childPoints, childTop, expandedIndex }, groupIndex) => {
            const canExpand = Boolean(data.breakdowns?.[group.label]);
            const label = group.label.length > 18 ? `${group.label.slice(0, 17)}…` : group.label;
            return <g
              key={`${group.label}-${groupIndex}`}
            >
              <text
                x={left - 8}
                y={categoryCenterY + 4}
                textAnchor="end"
                className="chart-label"
              >
                {label}
              </text>
              {group.points.map((point, pointIndex) => {
                const barWidth = Math.max(3, (Math.abs(point.value) / max) * plotWidth);
                const isExpanded = expandedIndex === pointIndex;
                const shift = childPoints.length > 0 && expandedIndex >= 0 && pointIndex > expandedIndex ? childPoints.length * barStep + 4 : 0;
                const y = groupTop + pointIndex * barStep + shift;
                const seriesIndex = data.seriesNames.indexOf(point.series);
                const detail = `${group.label}\n${point.series}: ${formatChartValue(point.value)}`;
                return <g
                  key={point.series}
                  className="chart-drillable"
                  onClick={(event) => {
                    const bar = event.currentTarget.querySelector("rect");
                    if (canExpand) {
                      setExpandedPoint(isExpanded ? null : { category: group.label, metric: point.metric });
                      if (isExpanded) bar?.blur();
                      else bar?.focus();
                    } else {
                      bar?.focus();
                    }
                  }}
                >
                  <rect
                    x={left}
                    y={y}
                    width={barWidth}
                    height={barHeight}
                    rx="3"
                    fill={chartColors[seriesIndex % chartColors.length]}
                    className={`chart-data-point chart-drillable${isExpanded ? " chart-expanded-parent" : ""}`}
                    role="button"
                    tabIndex={0}
                    aria-label={canExpand ? `${isExpanded ? "Collapse" : "Expand"} ${point.metric} breakdown for ${group.label}` : detail}
                    aria-expanded={canExpand ? isExpanded : undefined}
                    onKeyDown={(event) => {
                      if (event.key === "Enter" || event.key === " ") {
                        event.preventDefault();
                        if (canExpand) {
                          setExpandedPoint(isExpanded ? null : { category: group.label, metric: point.metric });
                          if (isExpanded) event.currentTarget.blur();
                        }
                      }
                    }}
                  />
                  <text x={left + barWidth + 5} y={y + barHeight - 1} className={isExpanded ? "chart-label chart-expanded-value" : "chart-label"}>{formatChartValue(point.value)}</text>
                </g>;
              })}
              {childPoints.map((point, pointIndex) => {
                const barWidth = Math.max(3, (Math.abs(point.value) / max) * plotWidth);
                const y = childTop + pointIndex * barStep;
                const seriesIndex = data.seriesNames.indexOf(point.series);
                const label = point.label.length > 16 ? `${point.label.slice(0, 15)}…` : point.label;
                return <g key={`${point.label}-${point.series}`} className="chart-breakdown-point">
                  <text x={left - 8} y={y + barHeight - 1} textAnchor="end" className="chart-label chart-breakdown-label">{label}</text>
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
