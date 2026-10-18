import { useRef, useState } from "react";
import Icon from "../../icon/icon";
import type { QueryResult, ReportDto } from "../../query-builder/query-design/query-builder-slice";
import ReportDetail from "../report-detail/report-detail";
import ReportItem from "../report-item/report-item";
import {
  categoryTone,
  defaultReportChartSettings,
  presentationFor,
  reportTitle,
  type ChartType,
  type ReportChartSettings,
} from "../report-model";

type ReportGroup = { name: string; reports: ReportDto[] };
type DropPosition = "before" | "after";

type ReportDashboardProps = {
  groups: ReportGroup[];
  sortedReports: ReportDto[];
  filteredReports: ReportDto[];
  category: string;
  search: string;
  numberOfBuiltins: number;
  numberOfCustom: number;
  reportsStatus: string;
  reportsError: string | null;
  metadataStatus: string;
  metadataError: string | null;
  reportResults: Record<string, QueryResult>;
  reportLoading: Record<string, boolean>;
  reportErrors: Record<string, string>;
  chartTypeOverrides: Record<string, ChartType>;
  chartSettingsOverrides: Record<string, ReportChartSettings>;
  detailReport: ReportDto | null;
  detailResult?: QueryResult;
  detailChartType: ChartType;
  detailChartSettings: ReportChartSettings;
  detailLoading: boolean;
  detailError?: string;
  message: string;
  messageIsError: boolean;
  onCreateReport: () => void;
  onSelectCategory: (category: string) => void;
  onSearchChange: (search: string) => void;
  onDismissMessage: () => void;
  onRun: (report: ReportDto) => void;
  onOpenDetails: (report: ReportDto) => void;
  onReorderReports: (sourceFileName: string, targetFileName: string, position: DropPosition) => void;
  onCloseDetails: () => void;
  onRunDetails: () => void;
  onChartTypeChange: (report: ReportDto, chartType: ChartType) => void;
  onChartSettingsChange: (report: ReportDto, settings: ReportChartSettings) => void;
  onEdit: (report: ReportDto) => void;
  onDelete: (report: ReportDto) => void;
  onCustomize: (report: ReportDto) => void;
};

export default function ReportDashboard({
  groups, sortedReports, filteredReports, category, search, numberOfBuiltins, numberOfCustom,
  reportsStatus, reportsError, metadataStatus, metadataError, reportResults, reportLoading,
  reportErrors, chartTypeOverrides, chartSettingsOverrides, detailReport, detailResult, detailChartType, detailChartSettings, detailLoading, detailError,
  message, messageIsError, onCreateReport, onSelectCategory, onSearchChange,
  onDismissMessage, onRun, onOpenDetails, onReorderReports, onCloseDetails, onRunDetails, onChartTypeChange, onChartSettingsChange, onEdit, onDelete, onCustomize,
}: ReportDashboardProps) {
  const draggedReport = useRef<string | null>(null);
  const dragPreview = useRef<HTMLElement | null>(null);
  const [dropIndicator, setDropIndicator] = useState<{ fileName: string; position: DropPosition } | null>(null);

  const clearDragPreview = () => {
    dragPreview.current?.remove();
    dragPreview.current = null;
    draggedReport.current = null;
    setDropIndicator(null);
  };

  const dropPositionFor = (targetFileName: string): DropPosition => {
    const sourceIndex = filteredReports.findIndex((report) => report.fileName === draggedReport.current);
    const targetIndex = filteredReports.findIndex((report) => report.fileName === targetFileName);
    return sourceIndex < targetIndex ? "after" : "before";
  };

  if (detailReport) {
    return <ReportDetail
      key={detailReport.fileName}
      report={detailReport}
      result={detailResult}
      chartType={detailChartType}
      chartSettings={detailChartSettings}
      loading={detailLoading}
      error={detailError}
      onClose={onCloseDetails}
      onRun={onRunDetails}
      onChartTypeChange={(chartType) => onChartTypeChange(detailReport, chartType)}
      onChartSettingsChange={(settings) => onChartSettingsChange(detailReport, settings)}
    />;
  }

  return (
    <div className="reports-workspace">
    <section className="reports-hero" aria-labelledby="reports-hero-title">
      <div className="reports-hero-copy">
        <span className="eyebrow">DYNAMICSQL STUDIO <i /> SALES INTELLIGENCE</span>
        <h1 id="reports-hero-title">Turn sales signals<br /><span>into your next move.</span></h1>
        <p>Start with a business question, then explore the live reports and data behind your next decision.</p>
        <div className="reports-hero-actions">
          <button className="button primary" type="button" onClick={onCreateReport}><Icon name="plus" size={15} /> Create a report</button>
          <a className="reports-browse-link" href="#report-group-browser">Browse sales questions <Icon name="chevron" size={13} /></a>
        </div>
      </div>
      <div className="reports-hero-summary">
        <div className="reports-summary-heading"><span className="reports-summary-icon"><Icon name="chart" size={16} /></span><span>SALES REPORT LIBRARY</span><span className="reports-summary-live"><i /> LIVE QUERIES</span></div>
        <div className="reports-summary-count"><strong>{sortedReports.length}</strong><span>reports ready to explore</span></div>
        <div className="reports-summary-rule" />
        <div className="reports-summary-areas"><strong>{groups.length}</strong><span>sales questions</span><small>{numberOfBuiltins} curated · {numberOfCustom} custom reports</small></div>
        <div className="reports-summary-orbit" aria-hidden="true"><span /><span /><span /></div>
      </div>
      <div className="reports-hero-glow" />
    </section>
    <section className="report-group-browser" id="report-group-browser" aria-labelledby="report-groups-title">
      <div className="report-groups-heading">
        <div><span className="eyebrow">START WITH A QUESTION</span><h2 id="report-groups-title">What do you want to understand?</h2><p>Pick a sales question to focus the report dashboard below.</p></div>
        <span className="report-group-total">{numberOfBuiltins} curated <i /> {numberOfCustom} custom</span>
      </div>
      <div className="report-group-grid" aria-label="Report groups">
        {groups.map((group) => <button className={`report-group-filter tone-${categoryTone(group.name)}${category === group.name ? " active" : ""}`} type="button" aria-pressed={category === group.name} key={group.name} onClick={() => onSelectCategory(group.name)}>
          <span className="report-group-filter-mark" aria-hidden="true" />
          <strong>{group.name}</strong>
          <span className="report-group-filter-count">{group.reports.length}</span>
        </button>)}
        <button className={`report-group-filter all${category === "All reports" ? " active" : ""}`} type="button" aria-pressed={category === "All reports"} onClick={() => onSelectCategory("All reports")}>
          <span className="report-group-filter-mark"><Icon name="grid" size={13} /></span><strong>All reports</strong><span className="report-group-filter-count">{sortedReports.length}</span>
        </button>
      </div>
      <div className="report-group-dashboard" id="report-group-dashboard">
          <div className="report-group-dashboard-heading">
            <div><span className="eyebrow">REPORT DASHBOARD</span><h3>{category}</h3><p>{filteredReports.length} {filteredReports.length === 1 ? "report" : "reports"}{search ? " matching your search" : " for this sales question"}</p></div>
            <label className="report-search group-report-search"><Icon name="search" size={14} /><input value={search} onChange={(event) => onSearchChange(event.target.value)} placeholder="Find a report in this group…" /></label>
          </div>
          {message && <div className={`report-toast${messageIsError ? " error" : ""}`} role={messageIsError ? "alert" : "status"}>{message}<button type="button" aria-label="Dismiss" onClick={onDismissMessage}><Icon name="close" size={13} /></button></div>}
          {reportsStatus === "loading" && <p className="report-list-status">Loading your reports…</p>}
          {reportsStatus === "failed" && <p className="report-list-status error" role="alert">{reportsError}</p>}
          {metadataStatus === "failed" && <p className="report-metadata-error" role="alert">Could not load report datasets: {metadataError}</p>}
          {reportsStatus !== "loading" && reportsStatus !== "failed" && filteredReports.length > 0 && <div className="report-dashboard-grid">
            {filteredReports.map((report) => {
              const presentation = presentationFor(report);
              return <ReportItem
                key={report.fileName}
                report={report}
                chartType={chartTypeOverrides[report.fileName] || presentation.chartType}
                chartSettings={chartSettingsOverrides[report.fileName]
                  || (reportResults[report.fileName]
                    ? defaultReportChartSettings(report, reportResults[report.fileName])
                    : { metrics: [], axes: {} })}
                result={reportResults[report.fileName]}
                loading={Boolean(reportLoading[report.fileName])}
                error={reportErrors[report.fileName]}
                onOpenDetails={() => onOpenDetails(report)}
                dropPosition={dropIndicator?.fileName === report.fileName ? dropIndicator.position : undefined}
                onDragStart={(event) => {
                  if (event.target instanceof Element && event.target.closest(".report-dashboard-card-tools")) {
                    event.preventDefault();
                    return;
                  }
                  draggedReport.current = report.fileName;
                  event.dataTransfer.effectAllowed = "move";
                  event.dataTransfer.setData("text/plain", report.fileName);
                  const card = event.currentTarget.closest(".report-dashboard-card");
                  if (card instanceof HTMLElement) {
                    const preview = card.cloneNode(true) as HTMLElement;
                    preview.classList.add("report-dashboard-drag-preview");
                    preview.setAttribute("aria-hidden", "true");
                    preview.style.position = "fixed";
                    preview.style.top = "-10000px";
                    preview.style.left = "-10000px";
                    preview.style.width = `${card.offsetWidth}px`;
                    preview.style.pointerEvents = "none";
                    document.body.append(preview);
                    dragPreview.current = preview;
                    const bounds = card.getBoundingClientRect();
                    event.dataTransfer.setDragImage(preview, event.clientX - bounds.left, event.clientY - bounds.top);
                  }
                }}
                onDragEnd={clearDragPreview}
                onDragOver={(event) => {
                  if (!draggedReport.current || draggedReport.current === report.fileName) return;
                  event.preventDefault();
                  event.dataTransfer.dropEffect = "move";
                  const position = dropPositionFor(report.fileName);
                  setDropIndicator((current) => (
                    current?.fileName === report.fileName && current.position === position
                      ? current
                      : { fileName: report.fileName, position }
                  ));
                }}
                onDragLeave={(event) => {
                  if (event.relatedTarget instanceof Node && event.currentTarget.contains(event.relatedTarget)) return;
                  setDropIndicator((current) => current?.fileName === report.fileName ? null : current);
                }}
                onDrop={(event) => {
                  event.preventDefault();
                  const sourceFileName = draggedReport.current;
                  const position = dropPositionFor(report.fileName);
                  draggedReport.current = null;
                  if (sourceFileName && sourceFileName !== report.fileName) {
                    onReorderReports(sourceFileName, report.fileName, position);
                  }
                  setDropIndicator(null);
                }}
                onChartTypeChange={(chartType) => onChartTypeChange(report, chartType)}
                onChartSettingsChange={(settings) => onChartSettingsChange(report, settings)}
                onRun={() => onRun(report)}
                onEdit={() => onEdit(report)}
                onDelete={() => onDelete(report)}
                onCustomize={() => onCustomize(report)}
              />;
            })}
          </div>}
          {reportsStatus !== "loading" && reportsStatus !== "failed" && !filteredReports.length && <p className="report-group-empty">No reports match this group and search.</p>}
      </div>
    </section>
    </div>
  );
}
