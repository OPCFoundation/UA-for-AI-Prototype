import './index.css';
import 'highlight.js/styles/atom-one-dark.css';
import { createRoot } from 'react-dom/client';
import { createElement } from 'react';
import { App } from './components/App';

const root = createRoot(document.getElementById('root')!);
root.render(createElement(App));
