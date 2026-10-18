import type { QueryResult, ReportDto } from "../../query-builder/query-design/query-builder-slice";
import { reportChartFields, type ReportChartSettings } from "../report-model";

export default function ReportChartSettingsPanel({ report, result, settings, onChange }: {
  report: ReportDto;
  result: QueryResult;
  settings: ReportChartSettings;
  onChange: (settings: ReportChartSettings) => void;
}) {
  const fields = reportChartFields(report, result);
  if (!fields.metrics.length) return null;

  const setMetric = (metric: string, enabled: boolean) => {
    const metrics = enabled
      ? [...settings.metrics, metric]
      : settings.metrics.filter((selected) => selected !== metric);
    onChange({ ...settings, metrics });
  };

  return (
    <section
      className="report-chart-settings"
      aria-label="Chart channels"
    >
      {fields.metrics.length > 0 && <fieldset>
        <legend>Aggregate channels</legend>
        {fields.metrics.map((metric) => <button className="report-channel-option" type="button" aria-pressed={settings.metrics.includes(metric)} key={metric} onClick={() => setMetric(metric, !settings.metrics.includes(metric))}>
          {metric}
        </button>)}
      </fieldset>}
    </section>
  );
}
