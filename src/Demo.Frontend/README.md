# DynamicSQL Studio demo

This is the React/Vite front end for the DynamicSQL demo. It demonstrates a metadata-driven query builder with:

- CSV upload and local sample data
- Selectable projection fields and aggregates, organized into drag-and-drop rows
- Dynamic filter controls and quick search
- A responsive dark data grid
- A Reports studio with 20 curated built-in analyses, query-backed visualizations, and CRUD for custom reports
- A Saved Queries library that distinguishes built-in and custom requests, loads a selected request into Query Builder, and saves/deletes custom requests per user section
- URL-backed pages with browser back/forward support; Query Builder inputs remain in place while visiting other pages and its design is restored after a refresh
- A Data Sources explorer with Data View and a draggable schema Diagram, including table columns, primary keys, foreign-key cardinalities, and a top-10 row preview

The Data Sources page reads table names from `GET /db/tables` and schema/relationship metadata from `GET /db/schema`. Full Data View requests use `GET /db/data/{tableName}`; the diagram preview uses the same endpoint with `schema` and `limit=10` query parameters.

Built-in reports choose a visualization based on the analysis (for example, monthly revenue trends, customer rankings, channel mix, invoice-status funnels, and customer-value scatter plots). Custom reports save their name, description, visualization, and query with the demo backend.

Run it with:

```powershell
npm install
npm run dev
```

## Expression builder debug visuals

The expression builder's debug outline, model tooltips, and model JSON panel are controlled by `debugUiEnabled` in the `queryBuilder` Redux state. To change what appears after a browser refresh, edit the `debugUiEnabled` value in the slice's `initialState` (`true` shows the debug UI; `false` hides it). This is a source-code flag and does not require a `.env` setting.

To change it at runtime, dispatch `setExpressionDebugUiEnabled(false)` to turn the debug UI off, or `setExpressionDebugUiEnabled(true)` to turn it back on. In Redux DevTools, dispatch the corresponding action as:

```json
{ "type": "queryBuilder/setExpressionDebugUiEnabled", "payload": false }
```

To change the default, edit `debugUiEnabled` in `src/components/query-builder/query-design/query-builder-slice.ts`. The persistent left borders on groups and field boxes are structural styling and remain visible.
