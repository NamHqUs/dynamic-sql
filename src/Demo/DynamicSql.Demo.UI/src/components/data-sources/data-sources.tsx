import { useEffect, useState } from "react";
import Icon from "../icon/icon";
import { parseCsv, toCsv } from "../../utils/csv";
import DatabaseConnection from "./database-connection/database-connection";
import RawData from "./raw-data/raw-data";
import UploadCsv from "./upload-csv/upload-csv";
import "./data-sources.css";

export default function DataSources({ rows, sourceName, apiError, onApply, onOpenBuilder }) {
  const [mode, setMode] = useState("raw");
  const [text, setText] = useState(() => toCsv(rows));
  const [error, setError] = useState("");
  const [showPreview, setShowPreview] = useState(false);
  useEffect(() => {
    setText(toCsv(rows));
  }, [rows, sourceName]);

  const selectMode = (nextMode) => {
    setMode(nextMode);
    setShowPreview(false);
  };

  const apply = (csv, name) => {
    const parsed = parseCsv(csv);
    if (!parsed.length) {
      setError("Enter a header row and at least one data row (comma-separated).");
      return;
    }
    setError("");
    onApply(parsed, name);
    setShowPreview(true);
  };

  const handleUpload = (content, name) => {
    setText(content);
    setMode("upload");
    apply(content, name);
  };

  const editorProps = {
    text,
    error,
    rows,
    showPreview,
    onChange: setText,
    onEdit: () => setShowPreview(false),
  };

  return (
    <>
      <section className="page-heading">
        <div><div className="eyebrow">WORKSPACE / DATA SOURCES</div><h1>Data sources</h1><p>Define raw data, upload a CSV, or connect to a database.</p>{apiError && <p role="alert">{apiError}</p>}</div>
        <div className="heading-actions"><button className="button primary" onClick={onOpenBuilder}><Icon name="grid" /> Open query builder</button></div>
      </section>
      <section className="workspace-card data-sources-card">
        <div className="tabs">
          <button className={mode === "raw" ? "tab active" : "tab"} onClick={() => selectMode("raw")}><Icon name="plus" size={14} /> Define raw data</button>
          <button className={mode === "upload" ? "tab active" : "tab"} onClick={() => selectMode("upload")}><Icon name="upload" size={14} /> Upload CSV</button>
          <button className={mode === "db" ? "tab active" : "tab"} onClick={() => selectMode("db")}><Icon name="database" size={14} /> Connect to database</button>
          <span className="tab-line" /><span className="query-status">{rows.length} rows loaded</span>
        </div>
        {mode === "raw" && <RawData
          rows={rows}
          sourceName={sourceName}
          onApply={onApply}
        />}
        {mode === "upload" && <UploadCsv {...editorProps} sourceName={sourceName} onApply={(name) => apply(text, name)} onUpload={handleUpload} />}
        {mode === "db" && <DatabaseConnection />}
      </section>
    </>
  );
}
