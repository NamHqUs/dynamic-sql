import { useEffect, useMemo, useState } from "react";
import type { FormEvent } from "react";
import { useDispatch, useSelector } from "react-redux";
import Icon from "../icon/icon";
import { useMessageBox } from "../message-box/message-box";
import {
  deleteReport,
  executeQuery,
  clearQueryResult,
  fetchQueryMetadata,
  fetchReports,
  fromApiQuery,
  MetadataEntity,
  QueryDefinition,
  QueryResult,
  saveReport,
  ReportDto,
} from "../query-builder/query-design/query-builder-slice";
import "./reports.css";

type ChartType = "line" | "bar" | "donut" | "funnel" | "scatter";
type ReportPresentation = {
  description: string;
  chartType: ChartType;
  category: string;
  metric?: string;
  groupBy?: string;
  xMetric?: string;
};

const builtinPresentations: Record<number, ReportPresentation> = {
  1: { description: "Follow monthly revenue alongside invoice volume to spot growth and seasonal shifts.", chartType: "line", category: "Revenue", metric: "Revenue" },
  2: { description: "Compare revenue across customer segments and regions to reveal where sales concentrate.", chartType: "bar", category: "Revenue", metric: "Revenue" },
  3: { description: "Rank your most valuable customers by cumulative paid lifetime spend.", chartType: "bar", category: "Customers", metric: "LifetimeValue" },
  4: { description: "Compare product sales value, with units sold available in the underlying query.", chartType: "bar", category: "Products", metric: "Revenue" },
  5: { description: "Rank products by gross profit; the query also includes each product’s margin percentage.", chartType: "bar", category: "Products", metric: "GrossProfit" },
  6: { description: "Review open invoice balances alongside days overdue to prioritize collection activity.", chartType: "bar", category: "Finance", metric: "TotalAmount" },
  7: { description: "Compare average days to payment by payment method and see amounts collected.", chartType: "bar", category: "Finance", metric: "AverageDaysToPay" },
  8: { description: "See how revenue and average order value are divided across your sales channels.", chartType: "donut", category: "Revenue", metric: "Revenue" },
  9: { description: "Rank product pairs by how often they appear together on paid invoices.", chartType: "bar", category: "Products", metric: "OrdersTogether" },
  10: { description: "Find customers with multiple paid purchases and compare order counts and last-order dates.", chartType: "bar", category: "Customers", metric: "OrderCount" },
  11: { description: "Explore customers with paid orders by segment; the result is a customer list, not an aggregate.", chartType: "donut", category: "Customers", groupBy: "Segment" },
  12: { description: "Surface products at or below reorder levels and compare their estimated inventory cost.", chartType: "bar", category: "Inventory", metric: "InventoryCost" },
  13: { description: "Track the monthly discount rate against net revenue to understand discount impact.", chartType: "line", category: "Revenue", metric: "DiscountRate" },
  14: { description: "Compare paid order revenue by weekday to reveal the strongest sales days.", chartType: "bar", category: "Revenue", metric: "Revenue" },
  15: { description: "Compare invoice counts at each status, with pipeline value available in the result.", chartType: "funnel", category: "Finance", metric: "InvoiceCount" },
  16: { description: "Compare each invoice’s units with its total value to see how basket size relates to spend.", chartType: "scatter", category: "Products", metric: "TotalAmount", xMetric: "Units" },
  17: { description: "Compare category revenue; gross profit is included for a profitability cross-check.", chartType: "bar", category: "Products", metric: "Revenue" },
  18: { description: "Follow paid monthly revenue over time; the current query provides the monthly series.", chartType: "line", category: "Revenue", metric: "Revenue" },
  19: { description: "Explore customer frequency against monetary value, with last purchase available for recency.", chartType: "scatter", category: "Customers", metric: "MonetaryValue", xMetric: "Frequency" },
  20: { description: "Rank customers by unpaid invoice exposure, alongside open counts and the oldest due date.", chartType: "bar", category: "Finance", metric: "AmountDue" },
};

const chartOptions: { value: ChartType; label: string }[] = [
  { value: "line", label: "Trend line" },
  { value: "bar", label: "Bar chart" },
  { value: "donut", label: "Donut chart" },
  { value: "funnel", label: "Funnel" },
  { value: "scatter", label: "Scatter plot" },
];

function builtinIndex(request: ReportDto): number | null {
  const match = request.fileName.match(/^(\d{2})-builtin-query-request\.yml$/i);
  return match ? Number(match[1]) : null;
}

function reportTitle(request: ReportDto): string {
  return request.query.name || request.fileName.replace(/\.(json|yml)$/i, "");
}

function presentationFor(request: ReportDto): ReportPresentation {
  const index = builtinIndex(request);
  if (index && builtinPresentations[index]) return builtinPresentations[index];
  const chartType = chartOptions.some((option) => option.value === request.chartType)
    ? request.chartType as ChartType
    : "bar";
  return {
    description: request.description || "A saved analysis from your DynamicSQL workspace.",
    chartType,
    category: "Custom",
  };
}

function getNewDefinition(entity: MetadataEntity): QueryDefinition {
  return {
    entity: entity.name,
    entityAliases: [],
    columns: entity.fields.map((field) => ({ field: field.name, alias: null })),
    filters: { logicalOperation: 0, filters: [] },
    sorts: [],
    page: 1,
    pageSize: 50,
  };
}

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

function ReportChart({ result, type, metric, groupBy, xMetric }: {
  result: QueryResult | null;
  type: ChartType;
  metric?: string;
  groupBy?: string;
  xMetric?: string;
}) {
  const chartData = useMemo(() => {
    if (!result?.rows.length) return [];
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

  if (!result) {
    return <div className="report-chart-empty"><span className="report-empty-mark"><Icon name="grid" size={20} /></span><strong>Your report canvas is ready</strong><span>Run this report to reveal its visual story.</span></div>;
  }
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

type ReportFormProps = {
  report: ReportDto | null;
  entities: MetadataEntity[];
  onClose: () => void;
  onSave: (details: { name: string; description: string; chartType: ChartType; entity: string }) => void;
};

function ReportForm({ report, entities, onClose, onSave }: ReportFormProps) {
  const existingPresentation = report ? presentationFor(report) : null;
  const [name, setName] = useState(report ? reportTitle(report) : "");
  const [description, setDescription] = useState(report?.description || "");
  const [chartType, setChartType] = useState<ChartType>(existingPresentation?.chartType || "bar");
  const [entity, setEntity] = useState(report?.query.entity || entities[0]?.name || "");

  const submit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    onSave({ name: name.trim(), description: description.trim(), chartType, entity });
  };

  return (
    <div className="report-modal-backdrop" role="presentation" onMouseDown={(event) => {
      if (event.target === event.currentTarget) onClose();
    }}>
      <form className="report-modal" onSubmit={submit} aria-labelledby="report-form-title">
        <div className="report-modal-heading"><div><span className="eyebrow">{report ? "REPORT SETTINGS" : "NEW ANALYSIS"}</span><h2 id="report-form-title">{report ? "Shape this report" : "Create a report"}</h2></div><button type="button" className="report-icon-button" aria-label="Close" onClick={onClose}><Icon name="close" /></button></div>
        <label>Report name<input autoFocus required maxLength={120} value={name} onChange={(event) => setName(event.target.value)} placeholder="e.g. Quarterly product pulse" /></label>
        <label>Description<textarea required maxLength={500} rows={3} value={description} onChange={(event) => setDescription(event.target.value)} placeholder="What question does this report answer?" /></label>
        <div className="report-form-row">
          <label>Visualization<select value={chartType} onChange={(event) => setChartType(event.target.value as ChartType)}>{chartOptions.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}</select></label>
          {!report && <label>Starting dataset<select value={entity} onChange={(event) => setEntity(event.target.value)}>{entities.map((item) => <option key={item.name} value={item.name}>{item.name}</option>)}</select></label>}
        </div>
        <p className="report-form-note">{report ? "The saved query stays connected to this report. Built-in reports are protected; create a copy to customize one." : "We’ll start with the selected dataset’s fields. You can refine the query later in Query Builder."}</p>
        <div className="report-modal-actions"><button className="button secondary" type="button" onClick={onClose}>Cancel</button><button className="button primary" type="submit">{report ? "Save changes" : "Create report"}</button></div>
      </form>
    </div>
  );
}

export default function Reports() {
  const dispatch = useDispatch();
  const showMessageBox = useMessageBox();
  const { reports, reportsStatus, reportsError, metadata, metadataStatus, metadataError, result, queryStatus, queryError } = useSelector((state) => state.queryBuilder);
  const [selectedFileName, setSelectedFileName] = useState("");
  const [lastExecutedFileName, setLastExecutedFileName] = useState("");
  const [search, setSearch] = useState("");
  const [category, setCategory] = useState("All reports");
  const [editingReport, setEditingReport] = useState<ReportDto | null>(null);
  const [creatingReport, setCreatingReport] = useState(false);
  const [message, setMessage] = useState("");
  const [messageIsError, setMessageIsError] = useState(false);

  useEffect(() => {
    if (reportsStatus === "idle") dispatch(fetchReports());
    if (metadataStatus === "idle") dispatch(fetchQueryMetadata());
  }, [dispatch, metadataStatus, reportsStatus]);

  const sortedReports = useMemo(() => [...reports].sort((left, right) => (
    reportTitle(left).localeCompare(reportTitle(right), undefined, { numeric: true, sensitivity: "base" })
  )), [reports]);
  const categories = useMemo(() => ["All reports", ...new Set(sortedReports.map((request) => presentationFor(request).category))], [sortedReports]);
  const filteredReports = useMemo(() => sortedReports.filter((request) => {
    const matchesCategory = category === "All reports" || presentationFor(request).category === category;
    const matchesSearch = `${reportTitle(request)} ${presentationFor(request).description}`.toLowerCase().includes(search.toLowerCase());
    return matchesCategory && matchesSearch;
  }), [category, search, sortedReports]);
  const selectedReport = sortedReports.find((request) => request.fileName === selectedFileName) || filteredReports[0] || sortedReports[0];
  const selectedPresentation = selectedReport ? presentationFor(selectedReport) : null;
  const selectedResult = selectedReport && lastExecutedFileName === selectedReport.fileName ? result : null;
  const numberOfBuiltins = sortedReports.filter((request) => !request.canDelete).length;
  const numberOfCustom = sortedReports.length - numberOfBuiltins;

  useEffect(() => {
    if (!selectedReport || selectedReport.fileName === selectedFileName) return;
    setSelectedFileName(selectedReport.fileName);
    setLastExecutedFileName(selectedReport.fileName);
    dispatch(clearQueryResult());
    dispatch(executeQuery(fromApiQuery(selectedReport.query)));
  }, [dispatch, selectedFileName, selectedReport]);

  const runReport = (request: ReportDto) => {
    setSelectedFileName(request.fileName);
    setLastExecutedFileName(request.fileName);
    dispatch(clearQueryResult());
    dispatch(executeQuery(fromApiQuery(request.query)));
  };

  const saveReport = async ({ name, description, chartType, entity }: { name: string; description: string; chartType: ChartType; entity: string }) => {
    const target = editingReport;
    const entityMetadata = metadata.find((item) => item.name === entity);
    const queryDefinition = target
      ? fromApiQuery(target.query)
      : entityMetadata ? getNewDefinition(entityMetadata) : null;
    if (!queryDefinition) {
      setMessageIsError(true);
      setMessage("Dataset metadata is not available yet.");
      return;
    }
    const resultAction = await dispatch(saveReport({
      fileName: target?.fileName,
      name,
      definition: queryDefinition,
      requestText: target?.fileName ? target.requestText || undefined : undefined,
      description,
      chartType,
    }));
    if (saveReport.fulfilled.match(resultAction)) {
      setSelectedFileName(resultAction.payload.fileName);
      setMessageIsError(false);
      setMessage(target ? "Report updated." : "Report created.");
      setCreatingReport(false);
      setEditingReport(null);
    } else {
      setMessageIsError(true);
      setMessage(String(resultAction.payload || "Unable to save report."));
    }
  };

  const deleteSelectedReport = async (request: ReportDto) => {
    const action = await showMessageBox({
      eyebrow: "DELETE REPORT",
      title: `Delete "${reportTitle(request)}"?`,
      message: "This custom report and its saved query will be permanently removed.",
      actions: [
        { value: "cancel", label: "Keep report", variant: "secondary" },
        { value: "delete", label: "Delete report", variant: "danger" },
      ],
    });
    if (action !== "delete") return;
    const resultAction = await dispatch(deleteReport(request.fileName));
    if (deleteReport.fulfilled.match(resultAction)) {
      setSelectedFileName("");
      setMessageIsError(false);
      setMessage("Report deleted.");
    } else {
      setMessageIsError(true);
      setMessage(String(resultAction.payload || "Unable to delete report."));
    }
  };

  const createReport = () => {
    setEditingReport(null);
    setCreatingReport(true);
  };

  const cloneBuiltin = (request: ReportDto) => {
    const defaults = presentationFor(request);
    setEditingReport({
      ...request,
      fileName: "",
      query: { ...request.query, name: `${reportTitle(request)} (copy)` },
      canDelete: true,
      description: defaults.description,
      chartType: defaults.chartType,
    });
    setCreatingReport(true);
  };

  return (
    <div className="reports-page">
      <section className="reports-hero">
        <div className="reports-hero-copy">
          <span className="eyebrow">DYNAMICSQL STUDIO / INSIGHTS</span>
          <h1>Reports that <span>move the story forward.</span></h1>
          <p>Explore the signals behind your business. Start with a curated report or build a view of your own.</p>
          <div className="reports-hero-actions"><button className="button primary" type="button" onClick={createReport}><Icon name="plus" size={15} /> New report</button><span><i /> {numberOfBuiltins} curated reports <b>·</b> {numberOfCustom} custom</span></div>
        </div>
        <div className="hero-orbit" aria-hidden="true"><div className="orbit-ring orbit-ring-outer" /><div className="orbit-ring orbit-ring-inner" /><div className="orbit-core"><Icon name="grid" size={22} /></div><span className="orbit-point orbit-point-one" /><span className="orbit-point orbit-point-two" /><span className="orbit-point orbit-point-three" /></div>
        <div className="reports-hero-glow" />
      </section>

      <div className="reports-layout">
        <aside className="reports-sidebar">
          <div className="reports-sidebar-title"><div><span className="eyebrow">YOUR LIBRARY</span><h2>Reports</h2></div><span className="report-count">{sortedReports.length.toString().padStart(2, "0")}</span></div>
          <label className="report-search"><Icon name="search" size={15} /><input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Find a report…" /></label>
          <div className="report-category-list" aria-label="Filter report categories">
            {categories.map((item) => <button className={category === item ? "report-category active" : "report-category"} key={item} type="button" onClick={() => setCategory(item)}>{item}<span>{item === "All reports" ? sortedReports.length : sortedReports.filter((request) => presentationFor(request).category === item).length}</span></button>)}
          </div>
          <div className="report-list-heading"><span>ALL REPORTS</span><span>{filteredReports.length}</span></div>
          <div className="report-list">
            {reportsStatus === "loading" && <p className="report-list-status">Loading your reports…</p>}
            {reportsStatus === "failed" && <p className="report-list-status error" role="alert">{reportsError}</p>}
            {reportsStatus !== "loading" && filteredReports.map((request) => {
              const presentation = presentationFor(request);
              return <button className={`report-list-item${selectedReport?.fileName === request.fileName ? " active" : ""}`} key={request.fileName} type="button" onClick={() => {
                setSelectedFileName(request.fileName);
                runReport(request);
              }}>
                <span className={`report-type-icon ${presentation.chartType}`}><Icon name={presentation.chartType === "line" ? "chart" : presentation.chartType === "donut" ? "database" : "chart"} size={15} /></span>
                <span className="report-list-copy"><strong>{reportTitle(request)}</strong><small>{presentation.category} <i /> {request.canDelete ? "Custom" : "Curated"}</small></span>
                <Icon name="chevron" size={14} />
              </button>;
            })}
            {reportsStatus !== "loading" && !filteredReports.length && <p className="report-list-status">No reports match this view.</p>}
          </div>
          <div className="reports-sidebar-foot"><span className="report-sync-dot" /> Live from your data source</div>
        </aside>

        <main className="report-detail">
          {queryError && <div className="report-alert" role="alert">{queryError}</div>}
          {message && <div className={`report-toast${messageIsError ? " error" : ""}`} role={messageIsError ? "alert" : "status"}>{message}<button type="button" aria-label="Dismiss" onClick={() => setMessage("")}><Icon name="close" size={13} /></button></div>}
          {selectedReport && selectedPresentation ? (
            <>
              <div className="report-detail-heading">
                <div className="report-breadcrumb"><span>REPORTS</span><Icon name="chevron" size={12} /><span>{selectedPresentation.category.toUpperCase()}</span></div>
                <div className="report-title-row"><div><div className="report-tags"><span className="report-tag">{selectedReport.canDelete ? "YOUR REPORT" : "CURATED REPORT"}</span><span className="report-tag subtle">{selectedPresentation.category}</span></div><h2>{reportTitle(selectedReport)}</h2><p>{selectedReport.description || selectedPresentation.description}</p></div>
                  <div className="report-detail-actions"><button className="button secondary report-refresh" type="button" onClick={() => runReport(selectedReport)} disabled={queryStatus === "loading"}><Icon name="reset" size={14} /> {queryStatus === "loading" ? "Running…" : "Refresh"}</button>{selectedReport.canDelete ? <><button className="report-icon-button" type="button" aria-label="Edit report" title="Edit report" onClick={() => setEditingReport(selectedReport)}><Icon name="edit" size={15} /></button><button className="report-icon-button report-delete-button" type="button" aria-label="Delete report" title="Delete report" onClick={() => void deleteSelectedReport(selectedReport)}><Icon name="trash" size={15} /></button></> : <button className="button secondary" type="button" onClick={() => cloneBuiltin(selectedReport)}><Icon name="plus" size={14} /> Customize</button>}</div>
                </div>
              </div>

              <div className="report-kpi-row">
                <div className="report-kpi"><span>VISUALIZATION</span><strong>{chartOptions.find((option) => option.value === selectedPresentation.chartType)?.label}</strong><small>Curated for this analysis</small></div>
                <div className="report-kpi"><span>RESULT ROWS</span><strong>{selectedResult ? formatValue(selectedResult.totalRecords >= 0 ? selectedResult.totalRecords : selectedResult.rows.length) : "—"}</strong><small>{selectedResult ? "Rows returned by the query" : "Refresh to load the latest result"}</small></div>
                <div className="report-kpi report-kpi-accent"><span>DATASET</span><strong>{selectedReport.query.entity}</strong><small>{selectedReport.query.fields.length} selected fields</small></div>
              </div>

              <section className="report-visual-card">
                <div className="report-visual-heading"><div><span className="eyebrow">REPORT CANVAS</span><h3>{selectedReport.query.name || reportTitle(selectedReport)}</h3><p>{selectedPresentation.description}</p></div><span className="report-chart-chip"><span /> {chartOptions.find((option) => option.value === selectedPresentation.chartType)?.label}</span></div>
                <div className="report-chart-stage">
                  {selectedResult ? <ReportChart result={selectedResult} type={selectedPresentation.chartType} metric={selectedPresentation.metric} groupBy={selectedPresentation.groupBy} xMetric={selectedPresentation.xMetric} /> : queryStatus === "loading" ? null : <div className="report-chart-empty"><span className="report-empty-mark"><Icon name="grid" size={20} /></span><strong>Turn this report into a live picture</strong><span>Run the report to visualize the latest data from {selectedReport.query.entity}.</span><button className="button secondary" type="button" onClick={() => runReport(selectedReport)}><Icon name="play" size={13} /> Run report</button></div>}
                  {queryStatus === "loading" && <div className="report-loading-overlay"><span className="report-spinner" /> Drawing insight…</div>}
                </div>
                <div className="report-visual-foot"><span><i className="report-live-indicator" /> Query-backed visualization</span><span>{selectedResult ? `${selectedResult.rows.length} rows displayed` : "No result loaded"}</span><button type="button" onClick={() => runReport(selectedReport)}>Run query <Icon name="chevron" size={12} /></button></div>
              </section>

              {selectedResult && <section className="report-data-card"><div className="report-data-heading"><div><span className="eyebrow">UNDERLYING DATA</span><h3>Query preview <span>{selectedResult.rows.length}</span></h3></div><span>Showing first {Math.min(selectedResult.rows.length, 8)} rows</span></div><div className="report-table-wrap"><table><thead><tr>{selectedResult.columns.map((column) => <th key={column}>{column}</th>)}</tr></thead><tbody>{selectedResult.rows.slice(0, 8).map((row, rowIndex) => <tr key={rowIndex}>{selectedResult.columns.map((column, columnIndex) => <td key={`${column}-${columnIndex}`}>{row[columnIndex] ?? "—"}</td>)}</tr>)}</tbody></table></div></section>}
            </>
          ) : <div className="reports-empty"><span className="report-empty-mark"><Icon name="grid" size={22} /></span><h2>Your reporting studio is ready</h2><p>{reportsStatus === "failed" ? reportsError : "Create a custom report or try again when your saved reports are available."}</p><button className="button primary" type="button" onClick={createReport}><Icon name="plus" size={14} /> Create your first report</button></div>}
          {metadataStatus === "failed" && <p className="report-metadata-error" role="alert">Could not load report datasets: {metadataError}</p>}
        </main>
      </div>

      {(creatingReport || editingReport) && <ReportForm
        report={editingReport}
        entities={metadata}
        onClose={() => { setCreatingReport(false); setEditingReport(null); }}
        onSave={(details) => void saveReport(details)}
      />}
    </div>
  );
}
