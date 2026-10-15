const backendApiUrl = import.meta.env.VITE_BACKEND_API_URL || "";

export const apiUrls = {
  backend: backendApiUrl,
  dataSource: import.meta.env.VITE_DATA_SOURCE_URL || "",
  database: {
    tables: `${backendApiUrl}/db/tables`,
    tableData: (tableName: string) => `${backendApiUrl}/db/data/${encodeURIComponent(tableName)}`,
  },
  metadata: {
    list: `${backendApiUrl}/metadata`,
    byName: (entityName: string) => `${backendApiUrl}/metadata/${encodeURIComponent(entityName)}`,
    reset: `${backendApiUrl}/metadata/reset`,
    functions: `${backendApiUrl}/metadata/functions`,
    functionByName: (functionName: string) => `${backendApiUrl}/metadata/functions/${encodeURIComponent(functionName)}`,
  },
  dynamicQuery: {
    execute: `${backendApiUrl}/dynamic-query/query`,
    executeText: `${backendApiUrl}/dynamic-query/query/from-text`,
    toString: `${backendApiUrl}/dynamic-query/query/to-string`,
    toStringText: `${backendApiUrl}/dynamic-query/query/to-string-from-text`,
  },
  queryRequests: {
    list: `${backendApiUrl}/query-request`,
    toString: `${backendApiUrl}/query-request/to-string`,
    parse: `${backendApiUrl}/query-request/parse`,
    byFileName: (fileName: string) => `${backendApiUrl}/query-request/${encodeURIComponent(fileName)}`,
  },
} as const;
