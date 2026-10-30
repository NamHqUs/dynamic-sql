import { useEffect, useState, type CSSProperties } from "react";
import { useDispatch, useSelector } from "react-redux";
import { useSearchParams } from "react-router-dom";
import Icon from "../icon/icon";
import DataGrid from "./data-grid/data-grid";
import DiagramSplitter from "./diagram-splitter/diagram-splitter";
import DiagramView from "./diagram-view/diagram-view";
import { fetchDbSchema, fetchDbTables, setDatabaseViewMode } from "./database-slice";
import MetadataBuilder from "../metadata-builder/metadata-builder";
import "./data-sources.css";

type DatabaseViewMode = "diagram" | "entities";

type DataSourcesProps = {
  onRequestConnection: () => void;
};

export default function DataSources({ onRequestConnection }: DataSourcesProps) {
  const dispatch = useDispatch();
  const [searchParams, setSearchParams] = useSearchParams();
  const {
    tables,
    selectedTable,
    selectedSchema,
    previewTable,
    viewMode,
    tableStatus,
    dataByTable,
    connectionStatus,
    providerCheckStatus,
    connectedProvider,
    connectedProfileName,
    connectionError,
  } = useSelector((state) => state.database);
  const isConnected = connectionStatus === "succeeded";
  const routeViewMode: DatabaseViewMode = searchParams.get("view") === "entities" ? "entities" : "diagram";
  const selectedKey = selectedSchema ? `${selectedSchema}.${selectedTable}` : selectedTable;
  const selectedStatus = dataByTable[selectedKey]?.status;
  const selectedData = dataByTable[selectedKey];
  const [diagramHeight, setDiagramHeight] = useState(600);
  const minDiagramHeight = 360;

  function resizeDiagram(height: number) {
    setDiagramHeight(Math.max(minDiagramHeight, Math.round(height)));
  }

  useEffect(() => {
    dispatch(setDatabaseViewMode(routeViewMode));
  }, [dispatch, routeViewMode]);

  useEffect(() => {
    if (routeViewMode === "entities" || providerCheckStatus === "loading" || providerCheckStatus === "idle") return;
    if (!connectedProvider) {
      if (providerCheckStatus === "succeeded") onRequestConnection();
      return;
    }
    if (connectionStatus === "failed" || connectionStatus === "loading") return;
    dispatch(fetchDbTables());
    dispatch(fetchDbSchema());
  }, [dispatch, providerCheckStatus, connectionStatus, connectedProvider, onRequestConnection, routeViewMode]);

  function changeViewMode(nextViewMode: DatabaseViewMode) {
    dispatch(setDatabaseViewMode(nextViewMode));
    setSearchParams((current) => {
      const next = new URLSearchParams(current);
      if (nextViewMode === "entities") next.set("view", "entities");
      else next.delete("view");
      return next;
    });
  }

  return (
    <>
      <section className="page-heading database-page-heading">
        <div className="database-heading-main">
          <span className="database-heading-mark" aria-hidden="true"><Icon name="database" size={21} /></span>
          <div className="database-heading-copy">
            <span className="database-heading-eyebrow">DATA EXPLORER <i /> TABLES · COLUMNS · RECORDS</span>
            <h2>{viewMode === "entities" ? "Define your data model" : isConnected ? "Explore your database" : "Database connection"}</h2>
            <p>{viewMode === "entities"
              ? "Define schemas, raw SQL sources, fields, and relationships used by the query builder."
              : isConnected
              ? `Connected to ${connectedProfileName || connectedProvider}. Browse tables, inspect relationships, and preview records.`
              : "Connect to a database to browse its schema and records."}</p>
            <div className="database-heading-summary" aria-label="Database summary">
              {isConnected && <>
                <span className="database-heading-current"><Icon name="table" size={13} />{selectedTable ? `${selectedSchema ? `${selectedSchema}.` : ""}${selectedTable}` : "No table selected"}</span>
                <span><strong>{tableStatus === "succeeded" ? tables.length : "—"}</strong> tables</span>
                <span><strong>{selectedStatus === "succeeded" && selectedData ? selectedData.columns.length : "—"}</strong> columns</span>
                <span><strong>{selectedStatus === "succeeded" && selectedData ? selectedData.rows.length : "—"}</strong> records</span>
              </>}
            </div>
          </div>
        </div>
        <nav className="database-section-tabs" role="tablist" aria-label="Metadata views">
            <button type="button" role="tab" aria-selected={viewMode === "diagram"} className={viewMode === "diagram" ? "active" : ""} onClick={() => changeViewMode("diagram")}>
              <Icon name="schema" size={14} /> Diagram
            </button>
            <button type="button" role="tab" aria-selected={viewMode === "entities"} className={viewMode === "entities" ? "active" : ""} onClick={() => changeViewMode("entities")}>
              <Icon name="table" size={14} /> Entity definitions
            </button>
          </nav>
      </section>
      {isConnected && connectionError && <p className="database-message error" role="alert">{connectionError}</p>}
      {viewMode === "entities" ? (
        <MetadataBuilder entityOnly embedded />
      ) : isConnected ? (
        <section className="database-browser diagram-browser">
          <div className="diagram-mode-content" style={{ "--diagram-panel-height": `${diagramHeight}px` } as CSSProperties}>
            <div className="diagram-definition-toolbar">
              <div>
                <strong>Database schema diagram</strong>
                <span>Use the entity definitions view to add schemas, raw SQL sources, and relationships.</span>
              </div>
              <button className="button secondary compact" type="button" onClick={() => changeViewMode("entities")}>
                <Icon name="edit" size={13} /> Define entities
              </button>
            </div>
            <DiagramView />
            <DiagramSplitter
              height={diagramHeight}
              minHeight={minDiagramHeight}
              onResize={resizeDiagram}
            />
            <section className="diagram-preview" aria-label="Data grid view" aria-live="polite">
              {previewTable ? <DataGrid table={previewTable} /> : (
                <div className="diagram-preview-placeholder">
                  <Icon name="table" size={19} />
                  <span>Double-click a table header to preview its first 10 rows.</span>
                </div>
              )}
            </section>
          </div>
        </section>
      ) : (
        <section className="database-browser database-disconnected">
          <div className="database-placeholder">
            <Icon name="database" size={24} />
            <p>Connect to a database from the top bar to browse its tables and records.</p>
          </div>
        </section>
      )}
    </>
  );
}
