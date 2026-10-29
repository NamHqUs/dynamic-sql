import { useCallback, useEffect, useRef, useState } from "react";
import { createRoot } from "react-dom/client";
import { Provider, useSelector } from "react-redux";
import { createBrowserRouter, Navigate, Route, RouterProvider, Routes, useBlocker, useLocation, useNavigate } from "react-router-dom";
import Icon, { getDatabaseIconName } from "./components/icon/icon";
import DataSources from "./components/data-sources/data-sources";
import QueryBuilder, { setQueryBuilderDefinition } from "./components/query-builder/query-builder";
import MetadataBuilder from "./components/metadata-builder/metadata-builder";
import Reports from "./components/reports/reports";
import { MessageBoxProvider } from "./components/message-box/message-box";
import type { QueryDefinition } from "./components/query-builder/query-design/query-builder-slice";
import { store, type RootState } from "./store/store";
import "./styles.css";

const PAGE_PATHS = {
  reports: "/reports",
  builder: "/query-builder",
  metadata: "/metadata",
  sources: "/data-sources",
} as const;

const LEGACY_FUNCTIONS_PATH = "/functions";

type Page = keyof typeof PAGE_PATHS;
type NavigationGuard = (continueNavigation: () => void, cancelNavigation?: () => void) => void;

function pageForPath(path: string): Page | null {
  return (Object.keys(PAGE_PATHS) as Page[]).find((page) => PAGE_PATHS[page] === path) ?? null;
}

function App() {
  const location = useLocation();
  const routerNavigate = useNavigate();
  const database = useSelector((state: RootState) => state.database);
  const page = pageForPath(location.pathname);
  const [metadataVisited, setMetadataVisited] = useState(() => location.pathname === PAGE_PATHS.metadata);
  const [queryBuilderVisited, setQueryBuilderVisited] = useState(() => location.pathname === PAGE_PATHS.builder);
  const [queryBuilderInstance, setQueryBuilderInstance] = useState(0);
  const metadataNavigationGuard = useRef<NavigationGuard | null>(null);
  const approvedNavigation = useRef<string | null>(null);
  const handledBlockedLocation = useRef<string | null>(null);
  const updateMetadataNavigationGuard = useCallback((guard: NavigationGuard | null) => {
    metadataNavigationGuard.current = guard;
  }, []);
  const guardForPage = (targetPage: Page | null) => {
    if (targetPage === "metadata") return metadataNavigationGuard.current;
    return null;
  };

  const blocker = useBlocker(({ currentLocation, nextLocation }) => {
    const currentPage = pageForPath(currentLocation.pathname);
    const nextPage = pageForPath(nextLocation.pathname);
    const target = `${nextLocation.pathname}${nextLocation.search}${nextLocation.hash}`;
    if (approvedNavigation.current === target) {
      approvedNavigation.current = null;
      return false;
    }
    return currentPage !== null
      && nextPage !== null
      && currentPage !== nextPage
      && guardForPage(currentPage) !== null;
  });

  useEffect(() => {
    if (location.pathname === PAGE_PATHS.metadata) setMetadataVisited(true);
    if (location.pathname === PAGE_PATHS.builder) setQueryBuilderVisited(true);
  }, [location.pathname]);

  useEffect(() => {
    if ((database.connectionRequired || (page === "builder" && database.connectionStatus !== "succeeded"))
      && location.pathname !== PAGE_PATHS.sources)
      routerNavigate(PAGE_PATHS.sources);
  }, [database.connectionRequired, database.connectionStatus, location.pathname, page, routerNavigate]);

  const navigateToPage = (nextPage: Page, fromReport = false) => {
    const targetPage = nextPage === "builder" && database.connectionStatus !== "succeeded" ? "sources" : nextPage;
    if (targetPage === page && !(targetPage === "builder" && fromReport)) return;
    const destination = targetPage === "builder" && fromReport
      ? `${PAGE_PATHS.builder}?from=reports`
      : PAGE_PATHS[targetPage];
    const continueNavigation = () => {
      if (targetPage === "metadata") setMetadataVisited(true);
      if (targetPage === "builder") setQueryBuilderVisited(true);
      approvedNavigation.current = destination;
      routerNavigate(destination);
    };
    const guard = guardForPage(page);
    if (guard) {
      guard(continueNavigation);
      return;
    }
    continueNavigation();
  };

  useEffect(() => {
    if (blocker.state !== "blocked") return;
    const blockedLocation = `${blocker.location.key}:${blocker.location.pathname}${blocker.location.search}${blocker.location.hash}`;
    if (handledBlockedLocation.current === blockedLocation) return;
    handledBlockedLocation.current = blockedLocation;

    const guard = guardForPage(page);
    const continueNavigation = () => {
      handledBlockedLocation.current = null;
      blocker.proceed();
    };
    const cancelNavigation = () => {
      handledBlockedLocation.current = null;
      blocker.reset();
    };
    if (guard) guard(continueNavigation, cancelNavigation);
    else continueNavigation();
  }, [blocker, page]);

  return (
    <main className="app-shell">
      <header className="topbar">
        <button className="brand" type="button" aria-label="DynamicSQL Studio home" onClick={() => navigateToPage("reports")}><img className="brand-logo" src="/brand-concepts/dynamicsql-studio.svg" alt="" /></button>
        <nav className="topnav"><button className={page === "builder" ? "nav-item active" : "nav-item"} onClick={() => navigateToPage("builder")}><Icon name="query" /> Query builder</button><button className={page === "sources" ? "nav-item active" : "nav-item"} onClick={() => navigateToPage("sources")}><Icon name="database" /> Data sources</button><button className={page === "metadata" ? "nav-item active" : "nav-item"} onClick={() => navigateToPage("metadata")}><Icon name="schema" /> Metadata</button></nav>
        <div className="top-actions">
          {database.connectionStatus === "loading" ? (
            <button className="database-status connecting" type="button" onClick={() => navigateToPage("sources")} aria-live="polite">
              <span className="database-status-dot" />
              <Icon name={getDatabaseIconName(database.connectingProvider)} size={15} />
              <span>Connecting{database.connectingProvider ? ` · ${database.connectingProvider}` : ""}{database.connectingProfileName ? ` · ${database.connectingProfileName}` : ""}</span>
            </button>
          ) : database.connectionStatus === "succeeded" ? (
            <button className="database-status connected" type="button" onClick={() => navigateToPage("sources")} title="Open connected database">
              <span className="database-status-dot" />
              <Icon name={getDatabaseIconName(database.connectedProvider)} size={15} />
              <span>{database.connectedProvider}{database.connectedProfileName ? ` · ${database.connectedProfileName}` : " · Direct connection"}</span>
            </button>
          ) : (
            <button className={`database-status${database.connectionStatus === "failed" ? " failed" : ""}`} type="button" onClick={() => navigateToPage("sources")}>
              <span className="database-status-dot" />
              <Icon name="database" size={15} />
              {database.connectionStatus === "failed" ? "Connection failed · Retry" : "Connect database"}
            </button>
          )}
          <button className="avatar" type="button" aria-label="User profile">N</button>
        </div>
      </header>

      <Routes>
        <Route path={PAGE_PATHS.reports} element={<div className="page-transition page-transition-no-slide"><Reports onNavigateToPage={navigateToPage} onEditQuery={(definition: QueryDefinition) => {
          setQueryBuilderDefinition(definition);
          setQueryBuilderInstance((current) => current + 1);
          navigateToPage("builder", true);
        }} /></div>} />
        <Route path={PAGE_PATHS.builder} element={null} />
        <Route path={PAGE_PATHS.metadata} element={null} />
        <Route path={LEGACY_FUNCTIONS_PATH} element={<Navigate to={`${PAGE_PATHS.metadata}?section=functions`} replace />} />
        <Route path={PAGE_PATHS.sources} element={<div className="page-transition page-transition-no-slide"><DataSources /></div>} />
        <Route path="*" element={<Navigate to={PAGE_PATHS.reports} replace />} />
      </Routes>
      {queryBuilderVisited && <div hidden={page !== "builder"} className={page === "builder" ? "page-transition page-transition-no-slide" : undefined}><QueryBuilder
        key={queryBuilderInstance}
        onBackToReports={new URLSearchParams(location.search).get("from") === "reports"
          ? () => navigateToPage("reports")
          : undefined}
      /></div>}
      {metadataVisited && <div hidden={page !== "metadata"} className={page === "metadata" ? "page-transition page-transition-no-slide" : undefined}><MetadataBuilder onNavigationGuardChange={updateMetadataNavigationGuard} /></div>}
      <footer><span><span className="footer-mark">✦</span> DynamicSQL engine</span><span>Metadata-driven queries · Client preview</span></footer>
    </main>
  );
}

const router = createBrowserRouter([{ path: "*", element: <App /> }]);

createRoot(document.getElementById("root")!).render(
  <Provider store={store}>
    <MessageBoxProvider><RouterProvider router={router} /></MessageBoxProvider>
  </Provider>,
);
