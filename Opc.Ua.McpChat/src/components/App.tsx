import { useState, useCallback } from 'react';
import { Toolbar } from './Toolbar';
import { ChatWindow } from './ChatWindow';
import { ChatInput } from './ChatInput';
import { Footer } from './Footer';
import '../styles/App.css';

export interface ChatMessage {
  role: 'user' | 'assistant';
  content: string;
}

export function App() {
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [loading, setLoading] = useState(false);

  const handleSend = useCallback(async (question: string) => {
    const userMessage: ChatMessage = { role: 'user', content: question };
    setMessages(prev => [...prev, userMessage]);
    setLoading(true);

    try {
      const response = await window.mcpApi.query(question);
      const assistantMessage: ChatMessage = { role: 'assistant', content: response };
      setMessages(prev => [...prev, assistantMessage]);
    } catch (error) {
      const errorMessage: ChatMessage = {
        role: 'assistant',
        content: `Error: ${error instanceof Error ? error.message : String(error)}`,
      };
      setMessages(prev => [...prev, errorMessage]);
    } finally {
      setLoading(false);
    }
  }, []);

  return (
    <div className="app">
      <Toolbar />
      <ChatWindow messages={messages} loading={loading} />
      <ChatInput onSend={handleSend} disabled={loading} />
      <Footer />
    </div>
  );
}
