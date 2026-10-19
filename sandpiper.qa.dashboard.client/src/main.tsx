import { createRoot } from 'react-dom/client';
import App from './App';
import './App.css';
import './index.css';
import { initializeAdoExtension, notifyWidgetReady } from './adoExtension';
import { createBackendClient, resolveApiBase } from './services/adoApi';

const root = createRoot(document.getElementById('root')!);

async function start() {
    const embedded = window.self !== window.top;
    let initialized = false;
    try {
        if (embedded) {
            await initializeAdoExtension();
            initialized = true;
        }
        const base = resolveApiBase(import.meta.env.DEV, embedded, import.meta.env.VITE_API_BASE_URL);
        root.render(<App api={createBackendClient(base)} />);
    } catch (error) {
        root.render(<main className="main"><h1>Sandpiper QA Command Center</h1>
            <p role="alert">{error instanceof Error ? error.message : 'Initialization failed.'}</p>
            <p>No live analytics are shown until the backend connection is configured.</p></main>);
    } finally {
        if (initialized) notifyWidgetReady();
    }
}

void start();
