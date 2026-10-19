import { useEffect, useRef, useState } from "react";
import Icon from "../../icon/icon";
import type { SavedQueryRequestDto } from "../query-design/query-builder-slice";
import "./saved-query-list.css";

type SavedQueryListProps = {
  requests: SavedQueryRequestDto[];
  selectedRequestName: string | null;
  status: string;
  error: string;
  onSelectRequest: (request: SavedQueryRequestDto) => void;
  onRetry: () => void;
};

export default function SavedQueryList({
  requests,
  selectedRequestName,
  status,
  error,
  onSelectRequest,
  onRetry,
}: SavedQueryListProps) {
  const [isOpen, setIsOpen] = useState(false);
  const [searchTerm, setSearchTerm] = useState("");
  const libraryRef = useRef<HTMLDivElement>(null);
  const triggerRef = useRef<HTMLButtonElement>(null);

  useEffect(() => {
    if (!isOpen) return;

    const handlePointerDown = (event: PointerEvent) => {
      if (event.target instanceof Node && !libraryRef.current?.contains(event.target)) {
        setIsOpen(false);
      }
    };
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key !== "Escape") return;
      setIsOpen(false);
      triggerRef.current?.focus();
    };

    document.addEventListener("pointerdown", handlePointerDown);
    document.addEventListener("keydown", handleKeyDown);
    return () => {
      document.removeEventListener("pointerdown", handlePointerDown);
      document.removeEventListener("keydown", handleKeyDown);
    };
  }, [isOpen]);

  const normalizedSearchTerm = searchTerm.trim().toLowerCase();
  const filteredRequests = normalizedSearchTerm
    ? requests.filter((request) => request.name.toLowerCase().includes(normalizedSearchTerm))
    : requests;

  return (
    <div className="saved-query-library" ref={libraryRef}>
      <button
        ref={triggerRef}
        type="button"
        className={`saved-query-trigger${isOpen ? " is-open" : ""}`}
        aria-expanded={isOpen}
        aria-controls="saved-query-flyout"
        onClick={() => setIsOpen((open) => !open)}
      >
        <Icon name="database" size={14} />
        <span>Saved queries</span>
        <span className="saved-query-count">{requests.length}</span>
        <Icon name="chevron-down" size={13} />
      </button>
      {isOpen && (
        <div className="saved-query-flyout" id="saved-query-flyout" role="region" aria-label="Saved queries">
          <div className="saved-query-flyout-heading">
            <div>
              <span className="section-kicker">QUERY LIBRARY</span>
              <h3>Saved queries</h3>
            </div>
            <button
              type="button"
              className="saved-query-flyout-close"
              aria-label="Close saved queries"
              onClick={() => {
                setIsOpen(false);
                triggerRef.current?.focus();
              }}
            >
              <Icon name="close" size={14} />
            </button>
          </div>
          <div className="saved-query-flyout-content">
            {status !== "loading" && status !== "failed" && requests.length > 0 && (
              <label className="saved-query-search">
                <Icon name="search" size={15} />
                <input
                  type="search"
                  value={searchTerm}
                  onChange={(event) => setSearchTerm(event.target.value)}
                  placeholder="Search saved queries"
                  aria-label="Search saved queries"
                />
              </label>
            )}
            {status === "loading" && <p className="saved-query-status">Loading saved queries…</p>}
            {status === "failed" && (
              <div className="saved-query-status error" role="alert">
                <span>{error}</span>
                <button type="button" className="saved-query-retry" onClick={onRetry}>Retry</button>
              </div>
            )}
            {status !== "failed" && status !== "loading" && requests.length === 0 && (
              <p className="saved-query-status">No saved queries yet.</p>
            )}
            {status !== "failed" && status !== "loading" && requests.length > 0 && filteredRequests.length === 0 && (
              <p className="saved-query-status">No saved queries match “{searchTerm.trim()}”.</p>
            )}
            {(["custom", "builtin"] as const).map((kind) => {
              const items = filteredRequests.filter((item) => item.isBuiltin === (kind === "builtin"));
              if (!items.length) return null;
              return (
                <section className="saved-query-group" key={kind}>
                  <h4>{kind === "builtin" ? "Built-in" : "Custom"} <span>{items.length}</span></h4>
                  <ul>
                    {items.map((item) => (
                      <li key={`${kind}-${item.name}`}>
                        <button
                          type="button"
                          className={`saved-query-item${selectedRequestName === item.name ? " active" : ""}`}
                          onClick={() => {
                            setIsOpen(false);
                            triggerRef.current?.focus();
                            onSelectRequest(item);
                          }}
                          aria-current={selectedRequestName === item.name ? "page" : undefined}
                        >
                          <span className="saved-query-item-name">{item.name}</span>
                          <span className={`saved-query-kind ${kind}`}>{kind}</span>
                        </button>
                      </li>
                    ))}
                  </ul>
                </section>
              );
            })}
          </div>
        </div>
      )}
    </div>
  );
}
