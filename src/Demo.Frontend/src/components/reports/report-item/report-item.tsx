import { useEffect, useRef, useState } from "react";
import type { DragEvent } from "react";
import Icon from "../../icon/icon";
import type { QueryResult, ReportDto } from "../../query-builder/query-design/query-builder-slice";
import ReportChart from "../charts/report-chart";
import {
  categoryTone,
  chartOptions,
  presentationFor,
  reportTitle,
  type ChartType,
  type ReportChartSettings,
} from "../report-model";

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
