import { type CSSProperties, useState } from "react";

export type ResizablePanel = {
  width: number;
  collapsed: boolean;
  min: number;
  max: number;
  setWidth: (width: number) => void;
  toggle: () => void;
  style: CSSProperties;
};

const storagePrefix = "dynamic-sql.panel.";
export const DEFAULT_PANEL_WIDTH = 260;

function readStoredWidth(key: string, fallback: number, min: number, max: number) {
  const stored = Number(window.localStorage.getItem(`${storagePrefix}${key}.width`));
  return Number.isFinite(stored) && stored >= min && stored <= max ? stored : fallback;
}

export function useResizablePanel(key: string, defaultWidth = DEFAULT_PANEL_WIDTH, min = 180, max = 520): ResizablePanel {
  const [width, setWidthState] = useState(() => readStoredWidth(key, defaultWidth, min, max));
  const [collapsed, setCollapsed] = useState(() => window.localStorage.getItem(`${storagePrefix}${key}.collapsed`) === "1");

  const setWidth = (next: number) => {
    const clamped = Math.min(max, Math.max(min, Math.round(next)));
    setWidthState(clamped);
    window.localStorage.setItem(`${storagePrefix}${key}.width`, String(clamped));
  };

  const toggle = () => {
    const next = !collapsed;
    setCollapsed(next);
    window.localStorage.setItem(`${storagePrefix}${key}.collapsed`, next ? "1" : "0");
  };

  return {
    width,
    collapsed,
    min,
    max,
    setWidth,
    toggle,
    style: { "--panel-width": collapsed ? "0px" : `${width}px` } as CSSProperties,
  };
}
