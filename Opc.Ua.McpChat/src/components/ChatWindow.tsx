import { useEffect, useRef } from 'react';
import { ChatMessage as ChatMessageComponent } from './ChatMessage';
import type { ChatMessage } from './App';
import '../styles/ChatWindow.css';

interface ChatWindowProps {
  messages: ChatMessage[];
  loading: boolean;
}

export function ChatWindow({ messages, loading }: ChatWindowProps) {
  const bottomRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [messages, loading]);

  return (
    <div className="chat-window">
      {messages.length === 0 && !loading && (
        <div className="chat-welcome">
          <h2>Welcome to the OPC UA Specification ChatBot</h2>
          <p>Ask any question about the OPC UA specification. For example:</p>
          <ul>
            <li>What is an AddressSpace?</li>
            <li>What is the difference between ClientServer and PubSub?</li>
            <li>What are modelling rules?</li>
            <li>How to represent a finite state machine?</li>
            <li>What is a global discovery server and why is it used?</li>
          </ul>
          <p>This prototype does <b>NOT</b> remember context from previous questions.</p>
        </div>
      )}
      {messages.map((msg, idx) => (
        <ChatMessageComponent key={idx} message={msg} />
      ))}
      {loading && (
        <div className="chat-loading">
          <div className="chat-loading-dots">
            <span></span>
            <span></span>
            <span></span>
          </div>
          <span className="chat-loading-text">Thinking...</span>
        </div>
      )}
      <div ref={bottomRef} />
    </div>
  );
}
