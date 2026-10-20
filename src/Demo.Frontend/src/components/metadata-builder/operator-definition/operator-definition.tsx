import { useState } from "react";
import Icon from "../../icon/icon";
import { useMessageBox } from "../../message-box/message-box";
import DefinitionActions, {
  resolveDefinitionChanges,
  useDefinitionUnsavedWarning,
} from "../definition-actions/definition-actions";
import type { DefinitionActionAdapter } from "../definition-actions/definition-actions";
import "./operator-definition.css";

type OperatorDefinitionData = {
  id: string;
  name: string;
  symbol: string;
  description: string;
  leftOperandType: string;
  rightOperandType: string;
};

type OperatorDraft = Omit<OperatorDefinitionData, "id"> & { id: string | null };

const operandTypes = ["String", "Number", "Datetime", "Boolean"];
let nextOperatorId = 1;

function emptyOperator(): OperatorDraft {
  return {
    id: null,
    name: "",
    symbol: "",
    description: "",
    leftOperandType: "String",
    rightOperandType: "String",
  };
}

export default function OperatorDefinition() {
  const showMessageBox = useMessageBox();
  const [definitions, setDefinitions] = useState<OperatorDefinitionData[]>([]);
  const [selectedId, setSelectedId] = useState("");
  const [draft, setDraft] = useState<OperatorDraft | null>(null);
  const [dirty, setDirty] = useState(false);
  const [saving, setSaving] = useState(false);
  const [message, setMessage] = useState("");
  const [filter, setFilter] = useState("");
  const selectedDefinition = definitions.find((definition) => definition.id === selectedId);
  const selectedName = selectedDefinition?.name || "";

  useDefinitionUnsavedWarning(dirty);

  const discardChanges = () => {
    setDraft(selectedDefinition ? { ...selectedDefinition } : null);
    setDirty(false);
    setMessage("");
  };

  const save = async (): Promise<boolean> => {
    if (!draft?.name.trim() || !draft.symbol.trim()) {
      setMessage("Operator name and symbol are required.");
      return false;
    }
    const name = draft.name.trim();
    if (definitions.some((definition) =>
      definition.id !== draft.id && definition.name.toLowerCase() === name.toLowerCase(),
    )) {
      setMessage("An operator with this name already exists.");
      return false;
    }

    setSaving(true);
    try {
      const saved: OperatorDefinitionData = {
        ...draft,
        id: draft.id || `operator-${nextOperatorId++}`,
        name,
        symbol: draft.symbol.trim(),
        description: draft.description.trim(),
      };
      setDefinitions((current) => {
        const existingIndex = current.findIndex((definition) => definition.id === saved.id);
        if (existingIndex === -1) return [...current, saved];
        return current.map((definition) => definition.id === saved.id ? saved : definition);
      });
      setSelectedId(saved.id);
      setDraft(saved);
      setDirty(false);
      setMessage("");
      return true;
    } finally {
      setSaving(false);
    }
  };

  const create = () => {
    setSelectedId("");
    setDraft(emptyOperator());
    setDirty(true);
    setMessage("");
  };

  const remove = async () => {
    if (!selectedId) return;
    setDefinitions((current) => current.filter((definition) => definition.id !== selectedId));
    setSelectedId("");
    setDraft(null);
    setDirty(false);
    setMessage("");
  };

  const actionAdapter: DefinitionActionAdapter = {
    resourceName: "operator",
    selectedName,
    hasSelection: Boolean(selectedId),
    isBuiltIn: false,
    hasDraft: Boolean(draft),
    dirty,
    saving,
    onSave: save,
    onDiscard: discardChanges,
    onDelete: remove,
    onNew: create,
  };

  const selectOperator = async (definition: OperatorDefinitionData) => {
    if (!(await resolveDefinitionChanges(actionAdapter, showMessageBox))) return;
    setSelectedId(definition.id);
    setDraft({ ...definition });
    setDirty(false);
    setMessage("");
  };

  const updateDraft = (update: (current: OperatorDraft) => OperatorDraft) => {
    if (!draft) return;
    setDraft(update(draft));
    setDirty(true);
  };

  const visibleDefinitions = definitions.filter((definition) =>
    `${definition.name} ${definition.symbol}`.toLowerCase().includes(filter.toLowerCase()),
  );

  return (
    <section className="operator-workspace">
      <aside className="operator-list">
        <div className="metadata-list-head">
          <span className="section-kicker">OPERATORS</span>
          <span className="tab-number">{definitions.length}</span>
        </div>
        <label className="metadata-search">
          <Icon name="search" size={14} />
          <input
            value={filter}
            onChange={(event) => setFilter(event.target.value)}
            placeholder="Find an operator"
          />
        </label>
        {visibleDefinitions.map((definition) => (
          <button
            className={`metadata-entity${definition.id === selectedId ? " active" : ""}`}
            key={definition.id}
            type="button"
            onClick={() => void selectOperator(definition)}
          >
            <span className="entity-icon"><Icon name="code" size={14} /></span>
            <span>
              <b>{definition.name}</b>
              <small>{definition.symbol} · {definition.leftOperandType} → {definition.rightOperandType}</small>
            </span>
            <Icon name="chevron" size={14} />
          </button>
        ))}
        {!visibleDefinitions.length && (
          <p className="operator-empty">
            <Icon name="code" size={18} />
            {definitions.length ? "No matching operators." : "No operator definitions yet."}
          </p>
        )}
      </aside>
      <main className="operator-editor">
        <div className="editor-head">
          <div>
            <span className="section-kicker">OPERATOR DEFINITION</span>
            <h2>{selectedName || (draft ? "New Operator…" : "Select an operator")}</h2>
          </div>
          <DefinitionActions adapter={actionAdapter} />
        </div>
        <p className="operator-notice">Preview only. Operator definitions are held in memory and are not saved to an API.</p>
        {!draft
          ? <div className="metadata-empty">
              <Icon name="code" size={32} />
              <p>{message || "Select an operator or create a new custom definition."}</p>
            </div>
          : <>
              {message && <p className="metadata-alert" role="alert">{message}</p>}
              <fieldset className="operator-form" disabled={saving}>
                <div className="metadata-form-grid">
                  <label>
                    Name
                    <input
                      readOnly={Boolean(selectedId)}
                      value={draft.name}
                      onChange={(event) => updateDraft((current) => ({ ...current, name: event.target.value }))}
                      placeholder="e.g. Contains"
                    />
                  </label>
                  <label>
                    Operator symbol
                    <input
                      value={draft.symbol}
                      onChange={(event) => updateDraft((current) => ({ ...current, symbol: event.target.value }))}
                      placeholder="e.g. CONTAINS"
                    />
                  </label>
                  <label className="operator-description">
                    Description
                    <input
                      value={draft.description}
                      onChange={(event) => updateDraft((current) => ({ ...current, description: event.target.value }))}
                      placeholder="Describe what this operator compares"
                    />
                  </label>
                  <label>
                    Left operand type
                    <select
                      value={draft.leftOperandType}
                      onChange={(event) => updateDraft((current) => ({ ...current, leftOperandType: event.target.value }))}
                    >
                      {operandTypes.map((type) => <option key={type} value={type}>{type}</option>)}
                    </select>
                  </label>
                  <label>
                    Right operand type
                    <select
                      value={draft.rightOperandType}
                      onChange={(event) => updateDraft((current) => ({ ...current, rightOperandType: event.target.value }))}
                    >
                      {operandTypes.map((type) => <option key={type} value={type}>{type}</option>)}
                    </select>
                  </label>
                </div>
                <section className="editor-section">
                  <div className="editor-section-head">
                    <div>
                      <h3>Provider templates</h3>
                      <p>Map the operator to SQL for each supported database provider.</p>
                    </div>
                  </div>
                  <div className="operator-template-preview" aria-hidden="true">
                    <span>PROVIDER</span>
                    <span>SQL TEMPLATE</span>
                    <span>ADD PROVIDER TEMPLATE</span>
                  </div>
                </section>
              </fieldset>
            </>}
      </main>
    </section>
  );
}
