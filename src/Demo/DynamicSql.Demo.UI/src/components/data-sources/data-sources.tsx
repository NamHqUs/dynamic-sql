import { useState } from "react";
import Icon from "../icon/icon";
import DatabaseConnection from "./database-connection/database-connection";
import RawData from "./raw-data/raw-data";
import "./data-sources.css";

export default function DataSources({ rows, sourceName, apiError, onApply, onOpenBuilder }) {
  const [mode, setMode] = useState("raw");

  const selectMode = (nextMode) => {
    setMode(nextMode);
  };

  return (
    <>
      <section className="page-heading">
        <div><div className="eyebrow">WORKSPACE / DATA SOURCES</div><h1>Data sources</h1><p>Define raw data or connect to a database.</p>{apiError && <p role="alert">{apiError}</p>}</div>
        <div className="heading-actions"><button className="button primary" onClick={onOpenBuilder}><Icon name="grid" /> Open query builder</button></div>
      </section>
      <section className="workspace-card data-sources-card">
        <div className="tabs">
          <button className={mode === "raw" ? "tab active" : "tab"} onClick={() => selectMode("raw")}><Icon name="plus" size={14} /> Define raw data</button>
          <button className={mode === "db" ? "tab active" : "tab"} onClick={() => selectMode("db")}><Icon name="database" size={14} /> Connect to database</button>
          <span className="tab-line" /><span className="query-status">{rows.length} rows loaded</span>
        </div>
        {mode === "raw" && <RawData
          rows={rows}
          sourceName={sourceName}
          onApply={onApply}
        />}
        {mode === "db" && <DatabaseConnection />}
      </section>
    </>
  );
}
