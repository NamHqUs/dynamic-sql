import { useEffect, useState } from "react";
import { apiUrls } from "../../const/api-urls";
import Icon from "../icon/icon";
import { useMessageBox } from "../message-box/message-box";
import {
  EditableFunctionArgument,
  EditableFunctionDefinition,
  FunctionArgumentDefaultKind,
  FunctionArgumentKind,
  FunctionDefinition,
  emptyFunction,
  emptyFunctionArgument,
  toEditableFunction,
} from "./types";
import "./sql-functions.css";

async function request(url: string, options?: RequestInit) {
  const response = await fetch(url, options);
  if (!response.ok) {
    const detail = await response.text();
    throw new Error(detail || `Function definition request failed (${response.status}).`);
  }
  return response.status === 204 ? null : response.json();
}

type SqlFunctionsProps = {
  onNavigationGuardChange?: (guard: ((continueNavigation: () => void) => void) | null) => void;
};

function parseJson(text: string, field: string): unknown {
  try {
    return JSON.parse(text);
  } catch {
    throw new Error(`${field} must contain valid JSON.`);
  }
}

function splitValues(text: string): string[] {
  return text.split(",").map((value) => value.trim()).filter(Boolean);
}

function toPayload(draft: EditableFunctionDefinition): FunctionDefinition {
  const sqlTemplates = parseJson(draft.sqlTemplatesText, "Dialect SQL templates");
  if (!sqlTemplates || Array.isArray(sqlTemplates) || typeof sqlTemplates !== "object") {
    throw new Error("Dialect SQL templates must be a JSON object.");
  }
  const args = draft.arguments.map((argument) => {
    const allowedConstants = argument.kind === "Constant"
      ? parseJson(argument.allowedConstantsText, `Allowed constants for ${argument.name || "argument"}`)
      : [];
    if (argument.kind === "Constant" && !Array.isArray(allowedConstants)) {
      throw new Error(`Allowed constants for ${argument.name || "argument"} must be a JSON array.`);
    }
    const defaultValue = argument.defaultKind === null
      ? null
      : parseJson(argument.defaultValueText, `Default value for ${argument.name || "argument"}`);
    return {
      name: argument.name.trim(),
      kind: argument.kind,
      allowedTokens: argument.kind === "QueryExpressionOrSqlToken" ? splitValues(argument.allowedTokensText) : [],
      allowedTypes: argument.kind === "SqlType" ? splitValues(argument.allowedTypesText) : [],
      allowedConstants: allowedConstants as unknown[],
      optional: argument.optional,
      defaultKind: argument.defaultKind,
      defaultValue,
    };
  });
  return {
    name: draft.name.trim(),
    arguments: args,
    resultType: draft.resultType,
    isAggregate: draft.isAggregate,
    description: draft.description.trim(),
    defaultSqlTemplate: draft.defaultSqlTemplate?.trim() || null,
    sqlTemplates: sqlTemplates as Record<string, string>,
  };
}

function defaultKindsFor(kind: FunctionArgumentKind): FunctionArgumentDefaultKind[] {
  switch (kind) {
    case "Constant":
    case "QueryExpression":
      return ["Constant"];
    case "QueryExpressionOrSqlToken":
      return ["Constant", "SqlToken"];
    case "SqlType":
      return ["LogicalType"];
  }
}

export default function SqlFunctions({ onNavigationGuardChange }: SqlFunctionsProps) {
  const showMessageBox = useMessageBox();
  const [definitions, setDefinitions] = useState<FunctionDefinition[]>([]);
  const [selectedName, setSelectedName] = useState("");
  const [draft, setDraft] = useState<EditableFunctionDefinition | null>(null);
  const [filter, setFilter] = useState("");
  const [status, setStatus] = useState("loading");
  const [message, setMessage] = useState("");
  const [dirty, setDirty] = useState(false);

  useEffect(() => {
    const handleBeforeUnload = (event: BeforeUnloadEvent) => {
      if (!dirty) return;
      event.preventDefault();
      event.returnValue = "";
    };
    window.addEventListener("beforeunload", handleBeforeUnload);
    return () => window.removeEventListener("beforeunload", handleBeforeUnload);
  }, [dirty]);

  const loadDefinitions = async (selectFirst = false, preferredName = selectedName) => {
    setStatus("loading");
    setMessage("");
    try {
      const payload = await request(apiUrls.metadata.functions);
      if (!Array.isArray(payload)) throw new Error("Function definition API must return an array.");
      setDefinitions(payload);
      const preferred = payload.find((definition) => definition.name === preferredName);
      const next = selectFirst ? payload[0] : preferred;
      setSelectedName(next?.name || "");
      setDraft(next ? toEditableFunction(next) : null);
      setDirty(false);
      setStatus("succeeded");
      return true;
    } catch (error) {
      setStatus("failed");
      setMessage(error instanceof Error ? error.message : "Unable to load function definitions.");
      return false;
    }
  };

  useEffect(() => { void loadDefinitions(true); }, []);

  const discardChanges = () => {
    const original = definitions.find((definition) => definition.name === selectedName);
    setDraft(original ? toEditableFunction(original) : null);
    setDirty(false);
    setMessage("");
  };

  const save = async (): Promise<boolean> => {
    if (!draft?.name.trim() || !draft.description.trim()) {
      setMessage("Function name and description are required.");
      return false;
    }
    let payload: FunctionDefinition;
    try {
      payload = toPayload(draft);
    } catch (error) {
      setMessage(error instanceof Error ? error.message : "Function definition contains invalid values.");
      return false;
    }
    setStatus("saving");
    setMessage("");
    try {
      await request(
        selectedName ? apiUrls.metadata.functionByName(selectedName) : apiUrls.metadata.functions,
        {
          method: selectedName ? "PUT" : "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify(payload),
        },
      );
      return await loadDefinitions(false, payload.name);
    } catch (error) {
      setStatus("failed");
      setMessage(error instanceof Error ? error.message : "Unable to save function definition.");
      return false;
    }
  };

  const resolveDirtyChanges = async () => {
    if (!dirty) return true;
    const action = await showMessageBox({
      eyebrow: "UNSAVED FUNCTION DEFINITION",
      title: "Apply your function changes?",
      message: "Save your changes before continuing, discard them, or stay here to keep editing.",
      actions: [
        { value: "stay", label: "Stay here", variant: "secondary" },
        { value: "discard", label: "Discard changes", variant: "secondary" },
        { value: "apply", label: "Apply changes", variant: "primary" },
      ],
    });
    if (action === "apply") return save();
    if (action === "discard") {
      discardChanges();
      return true;
    }
    return false;
  };

  const reload = async () => {
    if (!(await resolveDirtyChanges())) return;
    await loadDefinitions();
  };

  useEffect(() => {
    const guard = (continueNavigation: () => void) => {
      void (async () => {
        if (await resolveDirtyChanges()) continueNavigation();
      })();
    };
    onNavigationGuardChange?.(guard);
    return () => onNavigationGuardChange?.(null);
  }, [onNavigationGuardChange, dirty, draft, definitions, selectedName]);

  const selectDefinition = async (definition: FunctionDefinition) => {
    if (!(await resolveDirtyChanges())) return;
    setSelectedName(definition.name);
    setDraft(toEditableFunction(definition));
    setDirty(false);
    setMessage("");
  };

  const createDefinition = async () => {
    if (!(await resolveDirtyChanges())) return;
    setSelectedName("");
    setDraft(emptyFunction());
    setDirty(true);
    setMessage("");
  };

  const updateDraft = (update: (current: EditableFunctionDefinition) => EditableFunctionDefinition) => {
    if (!draft) return;
    setDraft(update(draft));
    setDirty(true);
  };

  const updateArgument = (
    index: number,
    update: (argument: EditableFunctionArgument) => EditableFunctionArgument,
  ) => {
    updateDraft((current) => ({
      ...current,
      arguments: current.arguments.map((argument, argumentIndex) =>
        argumentIndex === index ? update(argument) : argument,
      ),
    }));
  };

  const visibleDefinitions = definitions.filter((definition) =>
    definition.name.toLowerCase().includes(filter.toLowerCase()),
  );

  return (
    <>
      <section className="page-heading">
        <div>
          <h2>Manage query functions</h2>
        </div>
        <div className="heading-actions">
          <button className="button secondary" type="button" onClick={() => void reload()}>
            <Icon name="reset" size={14} /> Reload
          </button>
          <button className="button primary" type="button" onClick={() => void createDefinition()}>
            <Icon name="plus" size={14} /> New function
          </button>
        </div>
      </section>
      <section className="function-workspace">
        <aside className="function-list">
          <div className="metadata-list-head">
            <span className="section-kicker">FUNCTIONS</span>
            <span className="tab-number">{definitions.length}</span>
          </div>
          <label className="metadata-search">
            <Icon name="search" size={14} />
            <input value={filter} onChange={(event) => setFilter(event.target.value)} placeholder="Find a function" />
          </label>
          {status === "loading" && <p className="metadata-message">Loading function definitions…</p>}
          {status !== "loading" && visibleDefinitions.map((definition) => (
            <button
              className={`metadata-entity${definition.name === selectedName ? " active" : ""}`}
              key={definition.name}
              type="button"
              onClick={() => void selectDefinition(definition)}
            >
              <span className="entity-icon"><Icon name="code" size={14} /></span>
              <span>
                <b>{definition.name}</b>
                <small>{definition.arguments.length} arguments · {definition.resultType}{definition.isAggregate ? " · aggregate" : ""}</small>
              </span>
              <Icon name="chevron" size={14} />
            </button>
          ))}
          {status !== "loading" && !visibleDefinitions.length && (
            <p className="metadata-message">{definitions.length ? "No matching functions." : "No function definitions found."}</p>
          )}
        </aside>
        <main className="metadata-editor">
          {!draft ? (
            <div className="metadata-empty">
              <Icon name="code" size={32} />
              <p>{message || "Select a function to view its definition."}</p>
              {status === "failed" && <button className="button secondary" type="button" onClick={() => void loadDefinitions()}>Retry</button>}
            </div>
          ) : (
            <>
              <div className="editor-head">
                <div>
                  <span className="section-kicker">FUNCTION DEFINITION</span>
                  <h2>{selectedName ? "View or update function" : "Create function"}</h2>
                </div>
                <div className="editor-actions">
                  <button
                    className="button primary"
                    type="button"
                    onClick={() => void save()}
                    disabled={status === "saving" || !dirty}
                  >
                    <Icon name="check" size={14} /> {status === "saving" ? "Saving…" : selectedName ? "Save changes" : "Create function"}
                  </button>
                </div>
              </div>
              {message && <p className="metadata-alert" role="alert">{message}</p>}
              <div className="function-form">
                <div className="metadata-form-grid">
                  <label>
                    Function name
                    <input readOnly={Boolean(selectedName)} value={draft.name} onChange={(event) => updateDraft((current) => ({ ...current, name: event.target.value }))} placeholder="e.g. DATE_TRUNC" />
                    {selectedName && <small>Function names cannot be changed after creation.</small>}
                  </label>
                  <label>
                    Result type
                    <select value={draft.resultType} onChange={(event) => updateDraft((current) => ({ ...current, resultType: event.target.value }))}>
                      {["string", "int", "long", "decimal", "double", "bool", "DateTime"].map((type) => <option key={type} value={type}>{type}</option>)}
                    </select>
                  </label>
                  <label className="function-description">
                    Description
                    <input value={draft.description} onChange={(event) => updateDraft((current) => ({ ...current, description: event.target.value }))} placeholder="What this function returns" />
                  </label>
                  <label className="function-checkbox">
                    <input type="checkbox" checked={draft.isAggregate} onChange={(event) => updateDraft((current) => ({ ...current, isAggregate: event.target.checked }))} />
                    Aggregate function
                  </label>
                </div>

                <section className="editor-section">
                  <div className="editor-section-head">
                    <div><h3>Arguments</h3><p>Arguments are listed in SQL placeholder order. Required arguments must precede optional ones.</p></div>
                    <button className="button secondary compact" type="button" onClick={() => updateDraft((current) => ({ ...current, arguments: [...current.arguments, emptyFunctionArgument()] }))}>
                      <Icon name="plus" size={13} /> Add argument
                    </button>
                  </div>
                  {!draft.arguments.length && <div className="section-empty">This function takes no arguments.</div>}
                  {draft.arguments.map((argument, index) => {
                    const defaultKinds = defaultKindsFor(argument.kind);
                    return (
                      <article className="function-argument-card" key={argument.id}>
                        <div className="function-argument-heading">
                          <strong>Argument {index + 1}</strong>
                          <button className="icon-button subtle" type="button" title="Remove argument" onClick={() => updateDraft((current) => ({ ...current, arguments: current.arguments.filter((_, itemIndex) => itemIndex !== index) }))}>
                            <Icon name="trash" size={15} />
                          </button>
                        </div>
                        <div className="function-argument-grid">
                          <label>Argument name<input value={argument.name} onChange={(event) => updateArgument(index, (item) => ({ ...item, name: event.target.value }))} placeholder="value" /></label>
                          <label>Kind
                            <select value={argument.kind} onChange={(event) => {
                              const kind = event.target.value as FunctionArgumentKind;
                              updateArgument(index, (item) => ({ ...item, kind, defaultKind: null, defaultValue: null, defaultValueText: "" }));
                            }}>
                              {(["Constant", "QueryExpression", "QueryExpressionOrSqlToken", "SqlType"] as const).map((kind) => <option key={kind} value={kind}>{kind}</option>)}
                            </select>
                          </label>
                          <label className="function-optional"><input type="checkbox" checked={argument.optional} onChange={(event) => updateArgument(index, (item) => ({ ...item, optional: event.target.checked }))} /> Optional</label>
                          {argument.kind === "Constant" && (
                            <label className="function-argument-wide">Allowed constants (JSON array)
                              <textarea value={argument.allowedConstantsText} onChange={(event) => updateArgument(index, (item) => ({ ...item, allowedConstantsText: event.target.value }))} rows={2} placeholder={'["day", "month"]'} />
                            </label>
                          )}
                          {argument.kind === "QueryExpressionOrSqlToken" && (
                            <label className="function-argument-wide">Allowed SQL tokens (comma-separated)
                              <input value={argument.allowedTokensText} onChange={(event) => updateArgument(index, (item) => ({ ...item, allowedTokensText: event.target.value }))} placeholder="*" />
                            </label>
                          )}
                          {argument.kind === "SqlType" && (
                            <label className="function-argument-wide">Allowed logical types (comma-separated)
                              <input value={argument.allowedTypesText} onChange={(event) => updateArgument(index, (item) => ({ ...item, allowedTypesText: event.target.value }))} placeholder="Integer, Decimal, String" />
                            </label>
                          )}
                          <label>Default kind
                            <select
                              value={argument.defaultKind || ""}
                              onChange={(event) => updateArgument(index, (item) => ({
                                ...item,
                                defaultKind: event.target.value ? event.target.value as FunctionArgumentDefaultKind : null,
                                defaultValue: null,
                                defaultValueText: "",
                              }))}
                            >
                              <option value="">No default</option>
                              {defaultKinds.map((kind) => <option key={kind} value={kind}>{kind}</option>)}
                            </select>
                          </label>
                          {argument.defaultKind !== null && (
                            <label>Default value (JSON)
                              <input value={argument.defaultValueText} onChange={(event) => updateArgument(index, (item) => ({ ...item, defaultValueText: event.target.value }))} placeholder={'"example"'} />
                            </label>
                          )}
                        </div>
                      </article>
                    );
                  })}
                </section>

                <section className="editor-section">
                  <h3>SQL templates</h3>
                  <p>Use {"{1}"}, {"{2}"}, etc. for argument placeholders. Define a default template, dialect-specific templates, or both.</p>
                  <div className="function-template-grid">
                    <label>Default SQL template
                      <input value={draft.defaultSqlTemplate || ""} onChange={(event) => updateDraft((current) => ({ ...current, defaultSqlTemplate: event.target.value }))} placeholder="LOWER({1})" />
                    </label>
                    <label>Dialect SQL templates (JSON object)
                      <textarea value={draft.sqlTemplatesText} onChange={(event) => updateDraft((current) => ({ ...current, sqlTemplatesText: event.target.value }))} rows={5} placeholder={'{"Sqlite": "LOWER({1})"}'} />
                    </label>
                  </div>
                </section>
              </div>
            </>
          )}
        </main>
      </section>
    </>
  );
}
