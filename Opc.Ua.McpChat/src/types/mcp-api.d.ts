export interface McpApi {
  query(question: string): Promise<string>;
  getStatus(): Promise<{ connected: boolean; error?: string }>;
}

declare global {
  interface Window {
    mcpApi: McpApi;
  }
}
