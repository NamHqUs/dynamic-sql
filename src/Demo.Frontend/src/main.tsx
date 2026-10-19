import { useCallback, useEffect, useRef, useState } from "react";
import { createRoot } from "react-dom/client";
import { Provider } from "react-redux";
import { createBrowserRouter, Navigate, Route, RouterProvider, Routes, useBlocker, useLocation, useNavigate } from "react-router-dom";
import Icon from "./components/icon/icon";
import DataSources from "./components/data-sources/data-sources";
import QueryBuilder, { setQueryBuilderDefinition } from "./components/query-builder/query-builder";
import MetadataBuilder from "./components/metadata-builder/metadata-builder";
import SqlFunctions from "./components/sql-functions/sql-functions";
import Reports from "./components/reports/reports";
import { MessageBoxProvider } from "./components/message-box/message-box";
import type { QueryDefinition } from "./components/query-builder/query-design/query-builder-slice";
import { store } from "./store/store";
import "./styles.css";

const PAGE_PATHS = {
  reports: "/reports",
  builder: "/query-builder",
  metadata: "/metadata",
  functions: "/functions",
  sources: "/data-sources",
} as const;

type Page = keyof typeof PAGE_PATHS;
type NavigationGuard = (continueNavigation: () => void, cancelNavigation?: () => void) => void;

function pageForPath(path: string): Page | null {
  return (Object.keys(PAGE_PATHS) as Page[]).find((page) => PAGE_PATHS[page] === path) ?? null;
}

function App() {
  const location = useLocation();
  const routerNavigate = useNavigate();
  const page = pageForPath(location.pathname);
  const [metadataVisited, setMetadataVisited] = useState(() => location.pathname === PAGE_PATHS.metadata);
  const [queryBuilderVisited, setQueryBuilderVisited] = useState(() => location.pathname === PAGE_PATHS.builder);
  const [queryBuilderInstance, setQueryBuilderInstance] = useState(0);
  const metadataNavigationGuard = useRef<NavigationGuard | null>(null);
  const functionsNavigationGuard = useRef<NavigationGuard | null>(null);
  const approvedNavigation = useRef<string | null>(null);
  const handledBlockedLocation = useRef<string | null>(null);
  const updateMetadataNavigationGuard = useCallback((guard: NavigationGuard | null) => {
    metadataNavigationGuard.current = guard;
  }, []);
  const updateFunctionsNavigationGuard = useCallback((guard: NavigationGuard | null) => {
    functionsNavigationGuard.current = guard;
  }, []);

  const guardForPage = (targetPage: Page | null) => {
    if (targetPage === "metadata") return metadataNavigationGuard.current;
    if (targetPage === "functions") return functionsNavigationGuard.current;
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
    if (location.pathname === PAGE_PATHS.builder) setQueryBuilderVisited(true);
  }, [location.pathname]);

  const navigateToPage = (nextPage: Page, fromReport = false) => {
    if (nextPage === page && !(nextPage === "builder" && fromReport)) return;
    const destination = nextPage === "builder" && fromReport
      ? `${PAGE_PATHS.builder}?from=reports`
      : PAGE_PATHS[nextPage];
    const continueNavigation = () => {
      if (nextPage === "metadata") setMetadataVisited(true);
      if (nextPage === "builder") setQueryBuilderVisited(true);
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
        <nav className="topnav"><button className={page === "builder" ? "nav-item active" : "nav-item"} onClick={() => navigateToPage("builder")}><Icon name="grid" /> Query builder</button><button className={page === "metadata" ? "nav-item active" : "nav-item"} onClick={() => navigateToPage("metadata")}><Icon name="sliders" /> Metadata</button><button className={page === "functions" ? "nav-item active" : "nav-item"} onClick={() => navigateToPage("functions")}><Icon name="code" /> Function definitions</button><button className={page === "sources" ? "nav-item active" : "nav-item"} onClick={() => navigateToPage("sources")}><Icon name="database" /> Data sources</button></nav>
        <div className="top-actions"><span className="live-dot" /> Local demo <button className="avatar">N</button></div>
      </header>

      <Routes>
        <Route path={PAGE_PATHS.reports} element={<div className="page-transition"><Reports onNavigateToPage={navigateToPage} onEditQuery={(definition: QueryDefinition) => {
          setQueryBuilderDefinition(definition);
          setQueryBuilderInstance((current) => current + 1);
          navigateToPage("builder", true);
        }} /></div>} />
        <Route path={PAGE_PATHS.builder} element={null} />
        <Route path={PAGE_PATHS.metadata} element={null} />
        <Route path={PAGE_PATHS.functions} element={<div className="page-transition page-transition-no-slide"><SqlFunctions onNavigationGuardChange={updateFunctionsNavigationGuard} /></div>} />
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
