import { useEffect, useRef, useState, type CSSProperties } from "react";
import { useDispatch, useSelector } from "react-redux";
import { useSearchParams } from "react-router-dom";
import Icon from "../icon/icon";
import PanelSplitter from "../panel-splitter/panel-splitter";
import { useResizablePanel } from "../../utils/use-resizable-panel";
import DataGrid from "./data-grid/data-grid";
import DiagramSplitter from "./diagram-splitter/diagram-splitter";
import DiagramView from "./diagram-view/diagram-view";
import { fetchDbSchema, fetchDbTables, setDatabaseViewMode } from "./database-slice";
import TableList from "./table-list/table-list";
import TableDetails from "./table-details/table-details";
import "./data-sources.css";

type DatabaseViewMode = "data" | "diagram";

export default function DataSources() {
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
    connectedProvider,
    connectedProfileName,
    connectionError,
  } = useSelector((state) => state.database);
  const isConnected = connectionStatus === "succeeded";
  const routeViewMode = searchParams.get("view") === "diagram" ? "diagram" : "data";
  const selectedKey = selectedSchema ? `${selectedSchema}.${selectedTable}` : selectedTable;
  const selectedStatus = dataByTable[selectedKey]?.status;
  const selectedData = dataByTable[selectedKey];
  const panel = useResizablePanel("data-sources");
  const workspaceRef = useRef<HTMLDivElement>(null);
  const [tableListMaxHeight, setTableListMaxHeight] = useState<number | null>(null);
  const [diagramHeight, setDiagramHeight] = useState(600);
  const minDiagramHeight = 360;

  function resizeDiagram(height: number) {
    setDiagramHeight(Math.max(minDiagramHeight, Math.round(height)));
  }

  useEffect(() => {
    dispatch(setDatabaseViewMode(routeViewMode));
  }, [dispatch, routeViewMode]);

  useEffect(() => {
    if (connectionStatus === "failed" || connectionStatus === "loading") return;
    dispatch(fetchDbTables());
    dispatch(fetchDbSchema());
  }, [dispatch, connectionStatus]);

  useEffect(() => {
    const workspace = workspaceRef.current;
    const sidebar = workspace?.querySelector(".database-sidebar");
    const tableList = workspace?.querySelector(".database-table-list");
    const content = workspace?.querySelector(".database-content");
    const details = content?.firstElementChild;
    const shell = workspace?.closest(".app-shell");
    if (!workspace || !sidebar || !tableList || !content || !details || !shell) return;

    const updateMaxHeight = () => {
      const footer = shell.querySelector("footer");
      const shellPaddingBottom = Number.parseFloat(getComputedStyle(shell).paddingBottom) || 0;
      const availableWorkspaceHeight = window.innerHeight
        - workspace.getBoundingClientRect().top
        - window.scrollY
        - (footer?.getBoundingClientRect().height ?? 0)
        - shellPaddingBottom;
      const sidebarStyle = getComputedStyle(sidebar);
      const sidebarChrome = tableList.getBoundingClientRect().top
        - sidebar.getBoundingClientRect().top
        + (Number.parseFloat(sidebarStyle.paddingBottom) || 0)
        + (Number.parseFloat(sidebarStyle.borderBottomWidth) || 0);
      const detailsStyle = getComputedStyle(details);
      const contentStyle = getComputedStyle(content);
      const detailsHeight = details.getBoundingClientRect().height
        + (Number.parseFloat(detailsStyle.marginTop) || 0)
        + (Number.parseFloat(detailsStyle.marginBottom) || 0)
        + (Number.parseFloat(contentStyle.paddingTop) || 0)
        + (Number.parseFloat(contentStyle.paddingBottom) || 0);
      const tableContentHeight = Array.from(tableList.children).reduce((height, child) => {
        const childStyle = getComputedStyle(child);
        return height
          + child.getBoundingClientRect().height
          + (Number.parseFloat(childStyle.marginTop) || 0)
          + (Number.parseFloat(childStyle.marginBottom) || 0);
      }, 0);
      const maxHeight = detailsHeight > availableWorkspaceHeight
        ? Math.max(tableContentHeight, detailsHeight - sidebarChrome)
        : availableWorkspaceHeight - sidebarChrome;

      setTableListMaxHeight(Math.max(0, maxHeight));
    };

    updateMaxHeight();
    const observer = new ResizeObserver(updateMaxHeight);
    observer.observe(details);
    window.addEventListener("resize", updateMaxHeight);
    return () => {
      observer.disconnect();
      window.removeEventListener("resize", updateMaxHeight);
    };
  }, [connectionError, isConnected, selectedSchema, selectedStatus, selectedTable, tableStatus, viewMode]);

  function changeViewMode(nextViewMode: DatabaseViewMode) {
    dispatch(setDatabaseViewMode(nextViewMode));
    setSearchParams((current) => {
      const next = new URLSearchParams(current);
      if (nextViewMode === "diagram") next.set("view", "diagram");
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
            <h2>{isConnected ? "Explore your database" : "Database connection"}</h2>
            <p>{isConnected
              ? `Connected to ${connectedProfileName || connectedProvider}. Browse tables, inspect relationships, and preview records.`
              : "Use the connection control in the top bar to connect to or change a database."}</p>
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
        {isConnected && (
          <nav className="database-section-tabs" role="tablist" aria-label="Database views">
            <button type="button" role="tab" aria-selected={viewMode === "data"} className={viewMode === "data" ? "active" : ""} onClick={() => changeViewMode("data")}>
              <Icon name="table" size={14} /> Schema 
            </button>
            <button type="button" role="tab" aria-selected={viewMode === "diagram"} className={viewMode === "diagram" ? "active" : ""} onClick={() => changeViewMode("diagram")}>
              <Icon name="schema" size={14} /> Diagram
            </button>

          </nav>
        )}
      </section>
      {isConnected && connectionError && <p className="database-message error" role="alert">{connectionError}</p>}
      {isConnected ? (
        <section className={`database-browser${viewMode === "data" ? ` split-workspace${panel.collapsed ? " panel-collapsed" : ""}` : " diagram-browser"}`} style={viewMode === "data" ? panel.style : undefined}>
          {viewMode === "data" ? (
            <div
              ref={workspaceRef}
              className={`database-workspace split-workspace${panel.collapsed ? " panel-collapsed" : ""}`}
              style={tableListMaxHeight === null ? undefined : { "--table-list-max-height": `${tableListMaxHeight}px` } as CSSProperties}
            >
              <TableList />
              <PanelSplitter panel={panel} label="tables panel" />
              <section className="database-content" aria-live="polite">
                {selectedTable ? (
                  <>
                    <TableDetails dataLoadingEnabled={isConnected && routeViewMode === "data" && viewMode === "data"} />
                  </>
                ) : (
                  <div className="database-placeholder">
                    <Icon name="database" size={24} />
                    <p>{tableStatus === "succeeded" ? "Select a table to view its details and records." : "Loading database tables…"}</p>
                  </div>
                )}
              </section>
            </div>
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
