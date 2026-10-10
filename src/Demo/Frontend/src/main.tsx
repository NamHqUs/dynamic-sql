import { useState } from "react";
import { createRoot } from "react-dom/client";
import { Provider } from "react-redux";
import Icon from "./components/icon/icon";
import DataSources from "./components/data-sources/data-sources";
import QueryBuilder from "./components/query-builder/query-builder";
import { store } from "./store/store";
import "./styles.css";

function App() {
  const [page, setPage] = useState("sources");

  return (
    <main className="app-shell">
      <header className="topbar">
        <div className="brand"><span className="brand-mark"><Icon name="database" size={18} /></span><span>Dynamic<span className="brand-accent">SQL</span></span><span className="brand-divider" /><span className="brand-product">Studio</span></div>
        <nav className="topnav"><button className={page === "sources" ? "nav-item active" : "nav-item"} onClick={() => setPage("sources")}><Icon name="database" /> Data sources</button><button className={page === "builder" ? "nav-item active" : "nav-item"} onClick={() => setPage("builder")}><Icon name="grid" /> Query builder</button></nav>
        <div className="top-actions"><span className="live-dot" /> Local demo <button className="avatar">N</button></div>
      </header>

      {page === "sources" ? (
        <DataSources />
      ) : (
        <QueryBuilder />
      )}
      <footer><span><span className="footer-mark">✦</span> DynamicSQL engine</span><span>Metadata-driven queries · Client preview</span></footer>
    </main>
  );
}

createRoot(document.getElementById("root")).render(<Provider store={store}><App /></Provider>);
