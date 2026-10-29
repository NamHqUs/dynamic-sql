import { useState, type ReactNode, type RefObject } from "react";
import Icon from "../../icon/icon";
import "./metadata-sidebar.css";

export type MetadataSidebarGroup = {
  id: string;
  title: string;
  count: number;
  content: ReactNode;
  emptyMessage?: string;
};

type MetadataSidebarProps = {
  className: string;
  title: string;
  totalCount: number;
  filter: string;
  searchPlaceholder: string;
  loadingMessage?: string;
  groups: MetadataSidebarGroup[];
  onFilterChange: (filter: string) => void;
  scrollable?: boolean;
  listRef?: RefObject<HTMLDivElement | null>;
};

export default function MetadataSidebar({
  className,
  title,
  totalCount,
  filter,
  searchPlaceholder,
  loadingMessage,
  groups,
  onFilterChange,
  scrollable = false,
  listRef,
}: MetadataSidebarProps) {
  const [expandedGroups, setExpandedGroups] = useState<Record<string, boolean>>({});
  const listContent = (
    <>
      {loadingMessage && <p className="metadata-message">{loadingMessage}</p>}
      {groups.map((group) => {
        const expanded = expandedGroups[group.id] ?? true;
        return (
          <section className="metadata-list-group" key={group.id}>
            <button
              className="metadata-list-group-head"
              type="button"
              aria-expanded={expanded}
              aria-controls={group.id}
              onClick={() => setExpandedGroups((current) => ({
                ...current,
                [group.id]: !expanded,
              }))}
            >
              <span className="metadata-list-group-title">{group.title}</span>
              <span className="tab-number">{group.count}</span>
              <span className="metadata-list-group-chevron"><Icon name="chevron-down" size={14} /></span>
            </button>
            <div id={group.id} hidden={!expanded}>
              {group.content}
              {group.emptyMessage && <p className="metadata-list-group-empty">{group.emptyMessage}</p>}
            </div>
          </section>
        );
      })}
    </>
  );

  return (
    <aside className={className}>
      <div className="metadata-list-head">
        <span className="section-kicker">{title}</span>
        <span className="tab-number">{totalCount}</span>
      </div>
      <label className="metadata-search">
        <Icon name="search" size={14} />
        <input
          value={filter}
          onChange={(event) => onFilterChange(event.target.value)}
          placeholder={searchPlaceholder}
        />
      </label>
      {scrollable
        ? <div className="metadata-sidebar-scroll" ref={listRef}>{listContent}</div>
        : listContent}
    </aside>
  );
}
