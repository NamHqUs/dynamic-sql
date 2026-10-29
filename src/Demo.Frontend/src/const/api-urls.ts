const backendApiUrl = import.meta.env.VITE_BACKEND_API_URL || "";

export const apiUrls = {
  backend: backendApiUrl,
  dataSource: import.meta.env.VITE_DATA_SOURCE_URL || "",
  database: {
    connections: `${backendApiUrl}/db/connections`,
    connect: `${backendApiUrl}/db/connect`,
    disconnect: `${backendApiUrl}/db/disconnect`,
    tables: `${backendApiUrl}/db/tables`,
    schema: `${backendApiUrl}/db/schema`,
    tableData: (tableName: string, schema?: string, limit?: number) => {
      const query = new URLSearchParams();
      if (schema) query.set("schema", schema);
      if (limit) query.set("limit", String(limit));
      const queryString = query.toString();
      return `${backendApiUrl}/db/data/${encodeURIComponent(tableName)}${queryString ? `?${queryString}` : ""}`;
    },
  },
  metadata: {
    list: `${backendApiUrl}/metadata`,
    byName: (entityName: string) => `${backendApiUrl}/metadata/${encodeURIComponent(entityName)}`,
    reset: `${backendApiUrl}/metadata/reset`,
    functions: `${backendApiUrl}/metadata/functions`,
    functionByName: (functionName: string) => `${backendApiUrl}/metadata/functions/${encodeURIComponent(functionName)}`,
    operators: `${backendApiUrl}/metadata/operators`,
    operatorById: (operatorId: string) => `${backendApiUrl}/metadata/operators/${encodeURIComponent(operatorId)}`,
  },
  dynamicQuery: {
    execute: `${backendApiUrl}/dynamic-query/query`,
    executeText: `${backendApiUrl}/dynamic-query/query/from-text`,
    toString: `${backendApiUrl}/dynamic-query/query/to-string`,
    toStringText: `${backendApiUrl}/dynamic-query/query/to-string-from-text`,
  },
  reports: {
    list: `${backendApiUrl}/report`,
    byFileName: (fileName: string) => `${backendApiUrl}/report/${encodeURIComponent(fileName)}`,
  },
  queryRequests: {
    list: `${backendApiUrl}/query-request`,
    byName: (name: string) => `${backendApiUrl}/query-request/${encodeURIComponent(name)}`,
  },
} as const;
