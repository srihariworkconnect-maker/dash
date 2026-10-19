import { createRoot } from 'react-dom/client';
import App from './App';
import './App.css';
import './index.css';
import { initializeAdoExtension, notifyWidgetReady } from './adoExtension';

const root = createRoot(document.getElementById('root')!);

async function start() {
    try {
        await initializeAdoExtension();
        root.render(<App />);
        notifyWidgetReady();
    } catch (error) {
        root.render(
            <main className="main">
                <h1>Sandpiper QA Command Center</h1>
                <p role="alert">
                    {error instanceof Error ? error.message : 'Initialization failed.'}
                </p>
                <p>This application uses live Azure DevOps data only.</p>
            </main>
        );
    }
}

void start();
