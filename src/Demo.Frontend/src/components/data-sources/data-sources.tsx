import { useEffect, useState, type CSSProperties } from "react";
import { useDispatch, useSelector } from "react-redux";
import { useSearchParams } from "react-router-dom";
import Icon from "../icon/icon";
import PanelSplitter from "../panel-splitter/panel-splitter";
import { useResizablePanel } from "../../utils/use-resizable-panel";
import DataGrid from "./data-grid/data-grid";
import DiagramSplitter from "./diagram-splitter/diagram-splitter";
import DiagramView from "./diagram-view/diagram-view";
import { fetchDbSchema, fetchDbTableData, fetchDbTables, setDatabaseViewMode } from "./database-slice";
import TableList from "./table-list/table-list";
import "./data-sources.css";

type DatabaseViewMode = "data" | "diagram";

export default function DataSources() {
  const dispatch = useDispatch();
  const [searchParams, setSearchParams] = useSearchParams();
  const { tables, selectedTable, selectedSchema, previewTable, viewMode, tableStatus, dataByTable } = useSelector((state) => state.database);
  const routeViewMode = searchParams.get("view") === "data" ? "data" : "diagram";
  const selectedKey = selectedSchema ? `${selectedSchema}.${selectedTable}` : selectedTable;
  const selectedStatus = dataByTable[selectedKey]?.status;
  const selectedData = dataByTable[selectedKey];
  const panel = useResizablePanel("data-sources");
  const [diagramHeight, setDiagramHeight] = useState(600);
  const minDiagramHeight = 360;

  function resizeDiagram(height: number) {
    setDiagramHeight(Math.max(minDiagramHeight, Math.round(height)));
  }

  useEffect(() => {
    dispatch(setDatabaseViewMode(routeViewMode));
  }, [dispatch, routeViewMode]);

  useEffect(() => {
    dispatch(fetchDbTables());
    dispatch(fetchDbSchema());
  }, [dispatch]);

  useEffect(() => {
    if (routeViewMode === "data" && viewMode === "data" && selectedTable && selectedStatus !== "succeeded" && selectedStatus !== "loading") {
      dispatch(fetchDbTableData({ tableName: selectedTable, schema: selectedSchema }));
    }
  }, [dispatch, routeViewMode, selectedTable, selectedSchema, selectedStatus, viewMode]);

  function changeViewMode(nextViewMode: DatabaseViewMode) {
    dispatch(setDatabaseViewMode(nextViewMode));
    setSearchParams((current) => {
      const next = new URLSearchParams(current);
      if (nextViewMode === "data") next.set("view", "data");
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
            <h2>Explore your database</h2>
            <p>Browse available tables, inspect their columns, and explore your records.</p>
            <div className="database-heading-summary" aria-label="Database summary">
              <span className="database-heading-current"><Icon name="table" size={13} />{selectedTable ? `${selectedSchema ? `${selectedSchema}.` : ""}${selectedTable}` : "No table selected"}</span>
              <span><strong>{tableStatus === "succeeded" ? tables.length : "—"}</strong> tables</span>
              <span><strong>{selectedStatus === "succeeded" && selectedData ? selectedData.columns.length : "—"}</strong> columns</span>
              <span><strong>{selectedStatus === "succeeded" && selectedData ? selectedData.rows.length : "—"}</strong> records</span>
            </div>
          </div>
        </div>
        <nav className="database-section-tabs" role="tablist" aria-label="Database views">
          <button type="button" role="tab" aria-selected={viewMode === "diagram"} className={viewMode === "diagram" ? "active" : ""} onClick={() => changeViewMode("diagram")}>
            <Icon name="schema" size={14} /> Diagram
          </button>
          <button type="button" role="tab" aria-selected={viewMode === "data"} className={viewMode === "data" ? "active" : ""} onClick={() => changeViewMode("data")}>
            <Icon name="table" size={14} /> Data View
          </button>
        </nav>
      </section>
      <section className={`database-browser${viewMode === "data" ? ` split-workspace${panel.collapsed ? " panel-collapsed" : ""}` : " diagram-browser"}`} style={viewMode === "data" ? panel.style : undefined}>
        {viewMode === "data" ? (
          <>
            <TableList />
            <PanelSplitter panel={panel} label="tables panel" />
            <section className="database-content" aria-live="polite">
              {selectedTable ? <DataGrid /> : (
                <div className="database-placeholder">
                  <Icon name="database" size={24} />
                  <p>{tableStatus === "succeeded" ? "Select a table to view its records." : "Loading database tables…"}</p>
                </div>
              )}
            </section>
          </>
        ) : (
          <div className="diagram-mode-content" style={{ "--diagram-panel-height": `${diagramHeight}px` } as CSSProperties}>
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
        )}
      </section>
    </>
  );
}
