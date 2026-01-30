import { app, BrowserWindow, ipcMain, shell } from 'electron';
import { Client } from '@modelcontextprotocol/sdk/client';
import { StdioClientTransport } from '@modelcontextprotocol/sdk/client/stdio.js';
import path from 'path';

declare const MAIN_WINDOW_WEBPACK_ENTRY: string;
declare const MAIN_WINDOW_PRELOAD_WEBPACK_ENTRY: string;

if (require('electron-squirrel-startup')) {
  app.quit();
}

let mcpClient: Client | null = null;
let mcpTransport: StdioClientTransport | null = null;
let mcpConnected = false;
let mcpError: string | undefined;

async function initMcpClient(): Promise<void> {
  try {
    const serverProjectPath = path.resolve(app.getAppPath(), '..', 'Opc.Ua.McpServer');

    mcpTransport = new StdioClientTransport({
      command: 'dotnet',
      args: ['run', '--project', serverProjectPath, '--configuration', 'Release'],
      env: {
        ...process.env,
        DOTNET_NOLOGO: '1',
      },
    });

    mcpClient = new Client(
      { name: 'Opc.Ua.McpChat', version: '1.0.0' },
      { capabilities: {} },
    );

    mcpClient.onerror = (error) => {
      console.error('MCP client error:', error);
      mcpError = String(error);
    };

    await mcpClient.connect(mcpTransport);
    mcpConnected = true;
    mcpError = undefined;
    console.log('MCP client connected successfully.');
  } catch (error) {
    mcpConnected = false;
    mcpError = error instanceof Error ? error.message : String(error);
    console.error('Failed to connect MCP client:', mcpError);
  }
}

ipcMain.handle('mcp:query', async (_event, question: string): Promise<string> => {
  if (!mcpClient || !mcpConnected) {
    return 'Error: MCP server is not connected. Please restart the application.';
  }

  try {
    const result = await mcpClient.callTool({
      name: 'specificationQuery',
      arguments: { question },
    });

    const content = result.content as Array<{ type: string; text?: string }>;

    if (result.isError) {
      const errorText = content
        .filter(c => c.type === 'text' && c.text)
        .map(c => c.text)
        .join('\n');
      return `Error: ${errorText || 'Unknown error from MCP server'}`;
    }

    const text = content
      .filter(c => c.type === 'text' && c.text)
      .map(c => c.text)
      .join('\n');

    return text || 'No response from MCP server.';
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error);
    console.error('MCP query error:', message);
    return `Error: ${message}`;
  }
});

ipcMain.handle('mcp:status', async (): Promise<{ connected: boolean; error?: string }> => {
  return { connected: mcpConnected, error: mcpError };
});

const createWindow = (): void => {
  const mainWindow = new BrowserWindow({
    height: 800,
    width: 1000,
    minHeight: 600,
    minWidth: 700,
    webPreferences: {
      preload: MAIN_WINDOW_PRELOAD_WEBPACK_ENTRY,
      contextIsolation: true,
      nodeIntegration: false,
    },
  });

  mainWindow.loadURL(MAIN_WINDOW_WEBPACK_ENTRY);

  mainWindow.webContents.setWindowOpenHandler(({ url }) => {
    shell.openExternal(url);
    return { action: 'deny' };
  });

  mainWindow.webContents.on('will-navigate', (event, url) => {
    if (url !== MAIN_WINDOW_WEBPACK_ENTRY) {
      event.preventDefault();
      shell.openExternal(url);
    }
  });
};

app.on('ready', async () => {
  await initMcpClient();
  createWindow();
});

app.on('window-all-closed', () => {
  if (process.platform !== 'darwin') {
    app.quit();
  }
});

app.on('activate', () => {
  if (BrowserWindow.getAllWindows().length === 0) {
    createWindow();
  }
});

app.on('before-quit', async () => {
  if (mcpTransport) {
    try {
      await mcpTransport.close();
    } catch {
      // Ignore errors during shutdown
    }
  }
});
