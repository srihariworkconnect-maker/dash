import * as SDK from 'azure-devops-extension-sdk';
import type {
    AdoTestPlan, AdoTestSuite, AdoTestPoint, AdoTestRun, AdoTestResult,
} from '../types/ado';

type Parameters = Record<string, string>;

function apiUrl(path: string, parameters: Parameters): URL {
    const context = SDK.getWebContext();
    const host = SDK.getHost();
    if (!context.project?.id || !host.name || !host.isHosted) {
        throw new Error('Open the widget on an Azure DevOps project dashboard.');
    }
    const url = new URL(
        `https://dev.azure.com/${encodeURIComponent(host.name)}/` +
        `${encodeURIComponent(context.project.id)}/_apis/${path}`
    );
    if (url.protocol !== 'https:') {
        throw new Error('An HTTPS Azure DevOps connection is required.');
    }
    url.searchParams.set('api-version', '7.1');
    for (const [name, value] of Object.entries(parameters)) {
        url.searchParams.set(name, value);
    }
    return url;
}

async function delay(ms: number, signal: AbortSignal): Promise<void> {
    signal.throwIfAborted();
    await new Promise<void>((resolve, reject) => {
        const abort = () => {
            clearTimeout(timer);
            reject(new DOMException('Request cancelled.', 'AbortError'));
        };
        const timer = setTimeout(() => {
            signal.removeEventListener('abort', abort);
            resolve();
        }, ms);
        signal.addEventListener('abort', abort, { once: true });
    });
}

async function requestPage<T>(
    path: string, parameters: Parameters, signal: AbortSignal
): Promise<{ items: T[]; token: string | null }> {
    const url = apiUrl(path, parameters);
    for (let attempt = 0; attempt < 4; attempt++) {
        signal.throwIfAborted();
        const token = await SDK.getAccessToken();
        const response = await fetch(url, {
            signal: AbortSignal.any([signal, AbortSignal.timeout(60000)]),
            credentials: 'omit',
            headers: { Authorization: `Bearer ${token}`, Accept: 'application/json' },
        });
        if ((response.status === 429 || response.status === 503) && attempt < 3) {
            const retry = response.headers.get('Retry-After');
            const seconds = retry == null ? NaN : Number(retry);
            const retryTime = retry == null ? NaN : Date.parse(retry);
            const wait = Number.isFinite(seconds)
                ? seconds * 1000
                : Number.isFinite(retryTime) ? retryTime - Date.now() : 1000 * 2 ** attempt;
            if (wait > 60000) {
                throw new Error('Azure DevOps is throttling requests. Try refreshing later.');
            }
            await response.body?.cancel();
            await delay(Math.max(0, wait), signal);
            continue;
        }
        if (!response.ok) {
            throw new Error(
                `Azure DevOps request failed (${response.status}). ` +
                'Check your sign-in and Test Plans permissions, then refresh.'
            );
        }
        const body: unknown = await response.json();
        if (typeof body !== 'object' || body === null ||
            !('value' in body) || !Array.isArray(body.value)) {
            throw new Error('Azure DevOps returned an unexpected response.');
        }
        return {
            items: body.value as T[],
            token: response.headers.get('x-ms-continuationtoken'),
        };
    }
    throw new Error('Azure DevOps is temporarily unavailable.');
}

async function tokenPages<T>(
    path: string, signal: AbortSignal
): Promise<T[]> {
    const all: T[] = [];
    const seen = new Set<string>();
    let token: string | null = null;
    for (let page = 0; page < 10000; page++) {
        const result: { items: T[]; token: string | null } = await requestPage<T>(
            path, token ? { continuationToken: token } : {}, signal
        );
        all.push(...result.items);
        token = result.token;
        if (!token) return all;
        if (seen.has(token)) throw new Error('Repeated Azure DevOps pagination token.');
        seen.add(token);
    }
    throw new Error('Pagination safety limit reached; no partial totals are displayed.');
}

async function offsetPages<T extends { id: number }>(
    path: string, parameters: Parameters, signal: AbortSignal
): Promise<T[]> {
    const items = new Map<number, T>();
    let skip = 0;
    for (let page = 0; page < 10000; page++) {
        const result = await requestPage<T>(path, {
            ...parameters, $skip: String(skip), $top: '200',
        }, signal);
        if (result.items.length === 0) return [...items.values()];
        const previousSize = items.size;
        for (const item of result.items) items.set(item.id, item);
        if (items.size === previousSize) {
            throw new Error('Azure DevOps history pagination did not advance. Refresh and retry.');
        }
        skip += result.items.length;
    }
    throw new Error('History safety limit reached; no partial count is displayed.');
}

export async function getTestPlans(signal: AbortSignal): Promise<AdoTestPlan[]> {
    const plans = await tokenPages<AdoTestPlan>('testplan/plans', signal);
    return [...new Map(plans.map(plan => [plan.id, plan])).values()];
}

export async function getTestSuites(
    planId: number, signal: AbortSignal
): Promise<AdoTestSuite[]> {
    const suites = await tokenPages<AdoTestSuite>(
        `testplan/Plans/${planId}/suites`, signal
    );
    const nodes = new Map<number, AdoTestSuite>();
    for (const suite of suites) nodes.set(suite.id, { ...suite, children: [] });
    const roots: AdoTestSuite[] = [];
    for (const node of nodes.values()) {
        const ancestors = new Set([node.id]);
        let ancestor = node.parentSuite?.id;
        while (ancestor != null && nodes.has(ancestor)) {
            if (ancestors.has(ancestor)) throw new Error('Invalid cyclic suite hierarchy.');
            ancestors.add(ancestor);
            ancestor = nodes.get(ancestor)?.parentSuite?.id;
        }
        const parent = node.parentSuite ? nodes.get(node.parentSuite.id) : undefined;
        if (parent) parent.children!.push(node);
        else roots.push(node);
    }
    return roots;
}

export async function getTestPoints(
    planId: number, suiteId: number, signal: AbortSignal
): Promise<AdoTestPoint[]> {
    return tokenPages<AdoTestPoint>(
        `testplan/Plans/${planId}/Suites/${suiteId}/TestPoint`, signal
    );
}

export function getTestRuns(planId: number, signal: AbortSignal): Promise<AdoTestRun[]> {
    return offsetPages<AdoTestRun>('test/runs', { planId: String(planId) }, signal);
}

export function getTestResults(runId: number, signal: AbortSignal): Promise<AdoTestResult[]> {
    return offsetPages<AdoTestResult>(`test/Runs/${runId}/results`, {}, signal);
}
