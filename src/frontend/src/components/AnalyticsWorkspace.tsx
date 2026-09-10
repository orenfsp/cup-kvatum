import '@material/web/button/filled-button.js';
import '@material/web/button/outlined-button.js';
import { useMutation, useQuery } from '@tanstack/react-query';
import { useState } from 'react';
import {
  analyticsError,
  createAnalyticsExport,
  downloadAnalyticsExport,
  getAnalyticsDashboard,
  type AnalyticsDistribution,
} from '../api/analytics';
import { PageHeader } from './ux/WorkspacePrimitives';

export function AnalyticsWorkspace() {
  const [days, setDays] = useState(30);
  const dashboard = useQuery({
    queryKey: ['analytics-dashboard', days],
    queryFn: () => getAnalyticsDashboard(days),
    refetchInterval: 30_000,
  });
  const exportMutation = useMutation({
    mutationFn: async (format: 'csv' | 'xlsx') => {
      const prepared = await createAnalyticsExport(format, days);
      await downloadAnalyticsExport(prepared);
      return prepared;
    },
  });

  return (
    <section className="analytics-workspace" aria-labelledby="analytics-heading">
      <PageHeader
        id="analytics-heading"
        eyebrow="Обезличенная статистика"
        title="Аналитика"
        description="Показатели рассчитаны только по служебным метаданным. Тексты, чат, заметки, файлы, контакты и трек-номера не попадают в этот раздел и выгрузки."
      />

      <div className="analytics-toolbar">
        <label>
          <span>Период</span>
          <select value={days} onChange={(event) => setDays(Number(event.target.value))}>
            <option value={7}>Последние 7 дней</option>
            <option value={30}>Последние 30 дней</option>
            <option value={90}>Последние 90 дней</option>
            <option value={365}>Последние 365 дней</option>
          </select>
        </label>
        <div className="analytics-export" aria-label="Обезличенные выгрузки">
          <md-outlined-button
            disabled={exportMutation.isPending}
            onClick={() => exportMutation.mutate('csv')}
          >
            Скачать CSV
          </md-outlined-button>
          <md-outlined-button
            disabled={exportMutation.isPending}
            onClick={() => exportMutation.mutate('xlsx')}
          >
            Скачать XLSX
          </md-outlined-button>
        </div>
      </div>

      {dashboard.isPending ? <p className="staff-copy" aria-live="polite">Собираем безопасные показатели…</p> : null}
      {dashboard.isError ? <p className="form-error" role="alert">{analyticsError(dashboard.error)}</p> : null}
      {exportMutation.isError ? <p className="form-error" role="alert">{analyticsError(exportMutation.error)}</p> : null}
      {exportMutation.isSuccess ? (
        <p className="form-success" role="status">
          Выгрузка подготовлена и удалена с сервера после скачивания. Строк: {exportMutation.data.rowCount}.
        </p>
      ) : null}

      {dashboard.data ? (
        <>
          <p className="analytics-scope">{dashboard.data.scope.label}. Данные обновляются автоматически.</p>
          <section className="analytics-summary" aria-label="Ключевые показатели">
            <Metric value={dashboard.data.total} label="Обращений за период" />
            <Metric value={dashboard.data.active} label="Сейчас в работе" />
            <Metric value={`${dashboard.data.urgentSharePercent}%`} label="Подтверждено срочными" />
            <Metric value={`${dashboard.data.returnedSharePercent}%`} label="Возвращались на доработку" />
          </section>

          <section className="analytics-section" aria-labelledby="analytics-time-heading">
            <div className="analytics-section__heading">
              <h2 id="analytics-time-heading">Время ответа</h2>
              <p>Календарное время включает ожидание заявителя — это правило MVP.</p>
            </div>
            <div className="analytics-time-grid">
              <Metric value={duration(dashboard.data.averageMinutes.operatorAccepted)} label="До принятия оператором" />
              <Metric value={duration(dashboard.data.averageMinutes.firstExpertResponse)} label="До первого ответа эксперта" />
              <Metric value={duration(dashboard.data.averageMinutes.closed)} label="До закрытия" />
            </div>
          </section>

          <div className="analytics-distributions">
            <Distribution title="По статусам" items={dashboard.data.distributions.status} />
            <Distribution title="По категориям" items={dashboard.data.distributions.category} />
            <Distribution title="По типу заявителя" items={dashboard.data.distributions.applicantType} />
            <Distribution title="По приоритету" items={dashboard.data.distributions.priority} />
          </div>

          <section className="analytics-section" aria-labelledby="analytics-workload-heading">
            <div className="analytics-section__heading">
              <h2 id="analytics-workload-heading">Текущая нагрузка</h2>
              <p>Учитываются активные обращения из выбранного периода.</p>
            </div>
            <div className="analytics-table" role="table" aria-label="Нагрузка сотрудников">
              {dashboard.data.workload.length ? dashboard.data.workload.map((item) => (
                <div className="analytics-table__row" role="row" key={item.staffUserId}>
                  <span role="cell">{item.displayName}</span>
                  <strong role="cell">{item.activeCount}</strong>
                </div>
              )) : <p className="staff-copy">Активных назначений за этот период нет.</p>}
            </div>
          </section>

          <details className="analytics-definitions">
            <summary>Как считаются показатели</summary>
            <div>
              {Object.entries(dashboard.data.definitions).map(([key, value]) => (
                <p key={key}>{value}</p>
              ))}
            </div>
          </details>
        </>
      ) : null}
    </section>
  );
}

function Metric({ value, label }: { value: string | number; label: string }) {
  return <div className="analytics-metric"><strong>{value}</strong><span>{label}</span></div>;
}

function Distribution({ title, items }: { title: string; items: AnalyticsDistribution[] }) {
  const total = items.reduce((sum, item) => sum + item.count, 0);
  return (
    <section className="analytics-section" aria-label={title}>
      <div className="analytics-section__heading"><h2>{title}</h2></div>
      <div className="analytics-table" role="table">
        {items.length ? items.map((item) => (
          <div className="analytics-table__row" role="row" key={item.key}>
            <span role="cell">{item.label}</span>
            <span role="cell">{item.count} · {total ? Math.round(item.count * 100 / total) : 0}%</span>
          </div>
        )) : <p className="staff-copy">Нет данных за выбранный период.</p>}
      </div>
    </section>
  );
}

function duration(minutes: number | null) {
  if (minutes === null) return 'Нет данных';
  if (minutes < 60) return `${Math.round(minutes)} мин`;
  if (minutes < 1440) return `${(minutes / 60).toFixed(1)} ч`;
  return `${(minutes / 1440).toFixed(1)} дн`;
}
