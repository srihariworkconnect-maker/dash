import type { AdoTestSuite } from '../types/ado';

export default function TestSuiteTree({ suites }: { suites: AdoTestSuite[] }) {
    function renderSuite(suite: AdoTestSuite) {
        if (!suite.children?.length) {
            return <li key={suite.id}>{suite.name} <small>#{suite.id}</small></li>;
        }
        return (
            <li key={suite.id}>
                <details>
                    <summary>{suite.name} <small>#{suite.id}</small></summary>
                    <ul>{suite.children.map(renderSuite)}</ul>
                </details>
            </li>
        );
    }
    return <ul className="suite-tree">{suites.map(renderSuite)}</ul>;
}
