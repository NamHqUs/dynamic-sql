import { useCallback, useRef, useState } from "react";
import { createRoot } from "react-dom/client";
import { Provider } from "react-redux";
import Icon from "./components/icon/icon";
import DataSources from "./components/data-sources/data-sources";
import QueryBuilder from "./components/query-builder/query-builder";
import MetadataBuilder from "./components/metadata-builder/metadata-builder";
import { MessageBoxProvider } from "./components/message-box/message-box";
import { store } from "./store/store";
import "./styles.css";

function App() {
  const [page, setPage] = useState("sources");
  const [metadataVisited, setMetadataVisited] = useState(false);
  const queryBuilderNavigationGuard = useRef<((continueNavigation: () => void) => void) | null>(null);
  const metadataNavigationGuard = useRef<((continueNavigation: () => void) => void) | null>(null);
  const updateQueryBuilderNavigationGuard = useCallback((guard: ((continueNavigation: () => void) => void) | null) => {
    queryBuilderNavigationGuard.current = guard;
  }, []);
  const updateMetadataNavigationGuard = useCallback((guard: ((continueNavigation: () => void) => void) | null) => {
    metadataNavigationGuard.current = guard;
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
    } else {
      continueNavigation();
    }
  };

  return (
    <main className="app-shell">
      <header className="topbar">
        <div className="brand"><span className="brand-mark"><Icon name="database" size={18} /></span><span>Dynamic<span className="brand-accent">SQL</span></span><span className="brand-divider" /><span className="brand-product">Studio</span></div>
        <nav className="topnav"><button className={page === "sources" ? "nav-item active" : "nav-item"} onClick={() => navigate("sources")}><Icon name="database" /> Data sources</button><button className={page === "metadata" ? "nav-item active" : "nav-item"} onClick={() => navigate("metadata")}><Icon name="sliders" /> Metadata</button><button className={page === "builder" ? "nav-item active" : "nav-item"} onClick={() => navigate("builder")}><Icon name="grid" /> Query builder</button></nav>
        <div className="top-actions"><span className="live-dot" /> Local demo <button className="avatar">N</button></div>
      </header>

      {page === "sources" ? <DataSources /> : null}
      {metadataVisited && <div hidden={page !== "metadata"}><MetadataBuilder onNavigationGuardChange={updateMetadataNavigationGuard} /></div>}
      {page === "builder" ? <QueryBuilder onNavigationGuardChange={updateQueryBuilderNavigationGuard} /> : null}
      <footer><span><span className="footer-mark">✦</span> DynamicSQL engine</span><span>Metadata-driven queries · Client preview</span></footer>
    </main>
  );
}

createRoot(document.getElementById("root")).render(
  <Provider store={store}>
    <MessageBoxProvider><App /></MessageBoxProvider>
  </Provider>,
);
