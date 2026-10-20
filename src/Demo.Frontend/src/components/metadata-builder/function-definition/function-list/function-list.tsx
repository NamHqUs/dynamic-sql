import { useState } from "react";
import Icon from "../../../icon/icon";
import { FunctionDefinition } from "../types";
import "./function-list.css";

type FunctionListProps = {
  definitions: FunctionDefinition[];
  selectedName: string;
  status: string;
  onSelect: (definition: FunctionDefinition) => void;
};

export default function FunctionList({
  definitions,
  selectedName,
  status,
  onSelect,
}: FunctionListProps) {
  const [filter, setFilter] = useState("");
  const visibleDefinitions = definitions.filter((definition) =>
    definition.name.toLowerCase().includes(filter.toLowerCase()),
  );

  return (
    <aside className="function-list">
      <div className="metadata-list-head">
        <span className="section-kicker">FUNCTIONS</span>
        <span className="tab-number">{definitions.length}</span>
      </div>
      <label className="metadata-search">
        <Icon name="search" size={14} />
        <input
          value={filter}
          onChange={(event) => setFilter(event.target.value)}
          placeholder="Find a function"
        />
      </label>
      {status === "loading" && <p className="metadata-message">Loading function definitions…</p>}
      {status !== "loading" &&
        visibleDefinitions.map((definition) => (
          <button
            className={`metadata-entity${definition.name === selectedName ? " active" : ""}`}
            key={definition.name}
            type="button"
            onClick={() => onSelect(definition)}
          >
            <span className="entity-icon"><Icon name="code" size={14} /></span>
            <span>
              <b>{definition.name}</b>
              <small>
                {definition.arguments.length} arguments · {definition.resultType}
                {definition.isAggregate ? " · aggregate" : ""}
              </small>
            </span>
            <Icon name="chevron" size={14} />
          </button>
        ))}
      {status !== "loading" && !visibleDefinitions.length && (
        <p className="metadata-message">
          {definitions.length ? "No matching functions." : "No function definitions found."}
        </p>
      )}
    </aside>
  );
}
