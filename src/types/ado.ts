export interface AdoTestPlan {
    id: number;
    name: string;
}

export interface AdoTestSuite {
    id: number;
    name: string;
    parentSuite?: { id: number; name?: string };
    children?: AdoTestSuite[];
}

export interface AdoTestPoint {
    id: number;
    results?: {
        outcome?: string;
        state?: string;
        lastResultId?: number;
        lastTestRunId?: number;
    };
}

export interface AdoTestRun {
    id: number;
    plan?: { id: string | number };
}

export interface AdoTestResult {
    id: number;
    state?: string;
    outcome?: string;
    testPoint?: { id: string | number };
}
