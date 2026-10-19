import { useCallback, useRef, useState } from "react";
import { createRoot } from "react-dom/client";
import { Provider } from "react-redux";
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

function App() {
  const [page, setPage] = useState("reports");
  const [metadataVisited, setMetadataVisited] = useState(false);
  const queryBuilderNavigationGuard = useRef<((continueNavigation: () => void) => void) | null>(null);
  const metadataNavigationGuard = useRef<((continueNavigation: () => void) => void) | null>(null);
  const functionsNavigationGuard = useRef<((continueNavigation: () => void) => void) | null>(null);
  const updateQueryBuilderNavigationGuard = useCallback((guard: ((continueNavigation: () => void) => void) | null) => {
    queryBuilderNavigationGuard.current = guard;
  }, []);
  const updateMetadataNavigationGuard = useCallback((guard: ((continueNavigation: () => void) => void) | null) => {
    metadataNavigationGuard.current = guard;
  }, []);
  const updateFunctionsNavigationGuard = useCallback((guard: ((continueNavigation: () => void) => void) | null) => {
    functionsNavigationGuard.current = guard;
  }, []);

  const navigate = (nextPage: string) => {
    if (nextPage === page) return;
    const continueNavigation = () => {
      if (nextPage === "metadata") setMetadataVisited(true);
      setPage(nextPage);
    };
    if (page === "builder" && queryBuilderNavigationGuard.current) {
      queryBuilderNavigationGuard.current(continueNavigation);
    } else if (page === "metadata" && metadataNavigationGuard.current) {
      metadataNavigationGuard.current(continueNavigation);
    } else if (page === "functions" && functionsNavigationGuard.current) {
      functionsNavigationGuard.current(continueNavigation);
    } else {
      continueNavigation();
    }
  };

  return (
    <main className="app-shell">
      <header className="topbar">
        <div className="brand"><span className="brand-mark"><Icon name="database" size={18} /></span><span>Dynamic<span className="brand-accent">SQL</span></span><span className="brand-divider" /><span className="brand-product">Studio</span></div>
        <nav className="topnav"><button className={page === "reports" ? "nav-item active" : "nav-item"} onClick={() => navigate("reports")}><Icon name="chart" /> Reports</button><button className={page === "builder" ? "nav-item active" : "nav-item"} onClick={() => navigate("builder")}><Icon name="grid" /> Query builder</button><button className={page === "metadata" ? "nav-item active" : "nav-item"} onClick={() => navigate("metadata")}><Icon name="sliders" /> Metadata</button><button className={page === "functions" ? "nav-item active" : "nav-item"} onClick={() => navigate("functions")}><Icon name="code" /> Function definitions</button><button className={page === "sources" ? "nav-item active" : "nav-item"} onClick={() => navigate("sources")}><Icon name="database" /> Data sources</button></nav>
        <div className="top-actions"><span className="live-dot" /> Local demo <button className="avatar">N</button></div>
      </header>

      {page === "metadata" ? null : <div key={page} className="page-transition">
        {page === "sources" ? <DataSources /> : null}
        {page === "reports" ? <Reports onEditQuery={(definition: QueryDefinition) => {
          setQueryBuilderDefinition(definition);
          navigate("builder");
        }} /> : null}
        {page === "builder" ? <QueryBuilder onNavigationGuardChange={updateQueryBuilderNavigationGuard} /> : null}
        {page === "functions" ? <SqlFunctions onNavigationGuardChange={updateFunctionsNavigationGuard} /> : null}
      </div>}
      {metadataVisited && <div hidden={page !== "metadata"} className={page === "metadata" ? "page-transition" : undefined}><MetadataBuilder onNavigationGuardChange={updateMetadataNavigationGuard} /></div>}
      <footer><span><span className="footer-mark">✦</span> DynamicSQL engine</span><span>Metadata-driven queries · Client preview</span></footer>
    </main>
  );
}

createRoot(document.getElementById("root")).render(
  <Provider store={store}>
    <MessageBoxProvider><App /></MessageBoxProvider>
  </Provider>,
);
