import React, { useEffect, useState } from 'react';
import { createRoot } from 'react-dom/client';
import './styles.css';

type TopRow = { name: string; amount: number };
type Summary = { totalSaving: number; topBuyers: TopRow[]; topSuppliers: TopRow[] };
type ImportStatus = {
    jobId: string | null;
    status: 'Idle' | 'Queued' | 'Running' | 'Completed' | 'Failed' | 'Cancelled';
    result: { scanned: number; matched: number; saved: number } | null;
    error: string | null;
};

const formatMoney = (value: number) => new Intl.NumberFormat('uk-UA', {
    style: 'currency',
    currency: 'UAH',
    maximumFractionDigits: 0
}).format(value);

function TopTable({ title, rows }: { title: string; rows: TopRow[] }) {
    return <section className="card">
        <h2>{title}</h2>
        <table>
            <thead><tr><th>Назва</th><th>Сума контрактів</th></tr></thead>
            <tbody>{rows.map(r => <tr key={r.name}><td>{r.name}</td><td>{formatMoney(r.amount)}</td></tr>)}</tbody>
        </table>
    </section>;
}

function App() {
    const [summary, setSummary] = useState<Summary | null>(null);
    const [status, setStatus] = useState<ImportStatus | null>(null);
    const [loading, setLoading] = useState(false);
    const [importing, setImporting] = useState(false);
    const isImportRunning =
        status?.status === 'Queued' || status?.status === 'Running';

    const load = async () => {
        setLoading(true);
        try {
            const response = await fetch('/api/analytics/summary');
            setSummary(await response.json());
        } finally {
            setLoading(false);
        }
    };

    const loadStatus = async () => {
        const response = await fetch('/api/import/status');
        const data = await response.json();
        setStatus(data);
        setImporting(data.status === 'Queued' || data.status === 'Running');
    };

    const runImport = async () => {
        const response = await fetch('/api/import', { method: 'POST' });
        if (!response.ok && response.status !== 202) {
            alert('Import is already running or failed to start.');
            return;
        }

        setImporting(true);
        await loadStatus();
    };

    useEffect(() => { load(); loadStatus(); }, []);

    useEffect(() => {
        if (!importing) return;

        const timer = window.setInterval(async () => {
            await loadStatus();
            await load();
        }, 3000);

        return () => window.clearInterval(timer);
    }, [importing]);

    return <main>
        <header>
            <div>
                <p className="eyebrow">Prozorro Data Analyzing</p>
                <h1>Аналітика закупівель електричної енергії</h1>
            </div>
            <button
                onClick={runImport}
                disabled={isImportRunning}
                title={isImportRunning ? 'Зараз аналітика виконується у фоновому режимі' : 'Оновити дані'}
            >
                {isImportRunning ? 'Імпорт виконується...' : 'Оновити дані'}
            </button>
        </header>

        <section className="hero card">
            <span>Загальна економія бюджету</span>
            <strong>{loading || !summary ? '—' : formatMoney(summary.totalSaving)}</strong>
        </section>

        {summary && <div className="grid">
            <TopTable title="Top-5 закупівельників" rows={summary.topBuyers} />
            <TopTable title="Top-5 постачальників" rows={summary.topSuppliers} />
        </div>}
    </main>;
}

createRoot(document.getElementById('root')!).render(<App />);
