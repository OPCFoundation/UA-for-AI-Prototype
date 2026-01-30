import { contextBridge, ipcRenderer } from 'electron';

contextBridge.exposeInMainWorld('mcpApi', {
  query: (question: string): Promise<string> => {
    return ipcRenderer.invoke('mcp:query', question);
  },
  getStatus: (): Promise<{ connected: boolean; error?: string }> => {
    return ipcRenderer.invoke('mcp:status');
  },
});
