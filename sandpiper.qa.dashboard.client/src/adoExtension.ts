import * as SDK from 'azure-devops-extension-sdk';

let initialization: Promise<void> | undefined;

export function initializeAdoExtension(): Promise<void> {
    if (initialization) return initialization;

    initialization = (async () => {
        if (window.self === window.top) {
            throw new Error(
                'Open this widget inside Azure DevOps Overview > Dashboards. ' +
                'Standalone mode does not provide Azure DevOps authentication.'
            );
        }

        const handshake = SDK.init({ loaded: false, applyTheme: true });
        let timeout: ReturnType<typeof setTimeout> | undefined;
        try {
            await Promise.race([
                handshake.then(() => SDK.ready()),
                new Promise<never>((_, reject) => {
                    timeout = setTimeout(() => reject(new Error(
                        'Azure DevOps initialization timed out. Reload the widget.'
                    )), 20000);
                }),
            ]);
        } finally {
            clearTimeout(timeout);
        }

        SDK.register(SDK.getContributionId(), {
            load: async () => ({ state: 0 }),
            reload: async () => {
                window.dispatchEvent(new Event('ado-widget-refresh'));
                return { state: 0 };
            },
        });
    })();

    return initialization;
}

export function notifyWidgetReady(): void {
    void SDK.notifyLoadSucceeded();
}
