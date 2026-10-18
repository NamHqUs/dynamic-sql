import { useEffect, useMemo, useState } from "react";
import Icon from "../../icon/icon";
import type { QueryResult, ReportDto } from "../../query-builder/query-design/query-builder-slice";
import { categoryTone, presentationFor, reportTitle, type ChartType } from "../report-model";
import { ReportChart } from "../report-item/report-item";
import "./report-detail.css";

type ReportDetailProps = {
  report: ReportDto;
  result?: QueryResult;
  chartType: ChartType;
  loading: boolean;
  error?: string;
  onClose: () => void;
  onRun: () => void;
};

type TableSort = { column: string; direction: "asc" | "desc" } | null;

function ReportDataTable({ result }: { result: QueryResult }) {
  const [filters, setFilters] = useState<Record<string, string>>({});
  const [sort, setSort] = useState<TableSort>(null);

  const visibleRows = useMemo(() => {
    const filtered = result.rows.filter((row) => result.columns.every((column, index) => {
      const filter = (filters[column] || "").trim().toLocaleLowerCase();
      return !filter || String(row[index] ?? "").toLocaleLowerCase().includes(filter);
    }));
    if (!sort) return filtered;

    const sortIndex = result.columns.indexOf(sort.column);
    if (sortIndex < 0) return filtered;
    const direction = sort.direction === "asc" ? 1 : -1;
    return [...filtered].sort((left, right) => {
      const leftValue = left[sortIndex] ?? "";
      const rightValue = right[sortIndex] ?? "";
      const leftNumber = Number(leftValue);
      const rightNumber = Number(rightValue);
      const comparison = leftValue !== "" && rightValue !== "" && Number.isFinite(leftNumber) && Number.isFinite(rightNumber)
        ? leftNumber - rightNumber
        : leftValue.localeCompare(rightValue, undefined, { numeric: true, sensitivity: "base" });
      return comparison * direction;
    });
  }, [filters, result.columns, result.rows, sort]);

  const toggleSort = (column: string) => {
    setSort((current) => ({
      column,
      direction: current?.column === column && current.direction === "asc" ? "desc" : "asc",
    }));
  };

  return (
    <>
      <div className="report-detail-table-wrap">
        {result.columns.length > 0 ? (
          <table className="report-detail-table">
            <thead>
              <tr>{result.columns.map((column, index) => (
                <th
                  key={`${column}-${index}`}
                  aria-sort={sort?.column === column ? (sort.direction === "asc" ? "ascending" : "descending") : "none"}
                >
                  <button className="report-detail-sort" type="button" onClick={() => toggleSort(column)}>
                    {column}
                    <span>{sort?.column === column ? (sort.direction === "asc" ? "▲" : "▼") : "↕"}</span>
                  </button>
                  <input
                    aria-label={`Filter ${column}`}
                    value={filters[column] || ""}
                    onChange={(event) => setFilters((current) => ({ ...current, [column]: event.target.value }))}
                    placeholder={`Filter ${column}`}
                  />
                </th>
              ))}</tr>
            </thead>
            <tbody>
              {visibleRows.map((row, rowIndex) => (
                <tr key={rowIndex}>{result.columns.map((_, columnIndex) => (
                  <td key={columnIndex}>{row[columnIndex] ?? ""}</td>
                ))}</tr>
              ))}
            </tbody>
          </table>
        ) : <p className="report-detail-empty">This report returned no columns.</p>}
        {result.columns.length > 0 && result.rows.length === 0 && <p className="report-detail-empty">This report returned no rows.</p>}
        {result.rows.length > 0 && visibleRows.length === 0 && <p className="report-detail-empty">No rows match these filters.</p>}
      </div>
    </>
  );
}

export default function ReportDetail({ report, result, chartType, loading, error, onClose, onRun }: ReportDetailProps) {
  const presentation = presentationFor(report);
  const title = reportTitle(report);

  useEffect(() => {
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") onClose();
    };
    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, [onClose]);

  return (
    <div className="report-detail-page">
      <section className="report-detail-panel" aria-labelledby="report-detail-title">
        <header className="report-detail-heading">
          <div>
            <span className="eyebrow">REPORT DETAIL</span>
            <h2 id="report-detail-title">{title}</h2>
            <p>{report.description || presentation.description}</p>
          </div>
          <div className="report-detail-actions">
            <button className="button secondary" type="button" onClick={onRun} disabled={loading}>
              <Icon name="reset" size={13} /> {loading ? "Running…" : "Refresh"}
            </button>
            <button className="report-icon-button" type="button" aria-label="Close report details" onClick={onClose} autoFocus>
              <Icon name="close" />
            </button>
          </div>
        </header>

        <section className="report-detail-chart-section" aria-labelledby="report-detail-chart-title">
          <div className="report-detail-section-heading">
            <div><span className="eyebrow">VISUALIZATION</span><h3 id="report-detail-chart-title">Report chart</h3></div>
            {result && <span>{result.rows.length} rows</span>}
          </div>
          <div className={`report-detail-chart tone-${categoryTone(presentation.category)}`}>
            {result
              ? <ReportChart result={result} type={chartType} metric={presentation.metric} groupBy={presentation.groupBy} xMetric={presentation.xMetric} />
              : loading
                ? <div className="report-detail-status"><span className="report-spinner" /> Running this report…</div>
                : <div className="report-detail-status">{error ? <span role="alert">{error}</span> : <><span>Run this report to load its chart and data.</span><button className="button primary" type="button" onClick={onRun}><Icon name="play" size={13} /> Run report</button></>}</div>}
          </div>
        </section>

        <section className="report-detail-data-section" aria-labelledby="report-detail-data-title">
          <div className="report-detail-section-heading">
            <div><span className="eyebrow">QUERY RESULTS</span><h3 id="report-detail-data-title">Report data</h3></div>
            {result && <span>{result.rows.length} rows</span>}
          </div>
          {result && <ReportDataTable key={report.fileName} result={result} />}
        </section>
      </section>
    </div>
  );
}
