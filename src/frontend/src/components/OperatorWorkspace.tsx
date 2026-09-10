import '@material/web/button/filled-button.js';
import '@material/web/button/outlined-button.js';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useEffect, useMemo, useState } from 'react';
import { flushSync } from 'react-dom';
import { useLocation, useNavigate, useParams, useSearchParams } from 'react-router-dom';
import {
  acquireNextOperatorWork,
  acquireOperatorWork,
  assignOperatorAppeal,
  downloadOperatorAttachment,
  getOperatorAppeal,
  getOperatorQueue,
  getOperatorWorkLeaseId,
  heartbeatOperatorWork,
  isOperatorLeaseConflict,
  isOperatorStaleConflict,
  operatorError,
  rejectOperatorAppeal,
  releaseOperatorWork,
  resolveOperatorAppeal,
  triageOperatorAppeal,
  type AppealPriority,
  type OperatorQueueItem,
} from '../api/operatorQueue';
import { OperatorCollaborationWorkspace } from './OperatorCollaborationWorkspace';
import { OperatorCrisisWorkspace } from './OperatorCrisisWorkspace';
import { OperatorLifecycleWorkspace } from './OperatorLifecycleWorkspace';
import { ActionReceipt, ResponsiveMasterDetail, UnsavedChangesGuard } from './ux/WorkspacePrimitives';

type ActionMode = 'respond' | 'reject' | null;
type TriageStep = 'route' | 'assign';
const QUEUE_PAGE_SIZE = 30;

export function OperatorWorkspace({ view = 'queue' }: { view?: 'queue' | 'line' | 'crisis' | 'requests' | 'returns' | 'complaints' }) {
  if (view === 'line') return <OperatorLineWorkspace />;
  if (view === 'crisis') return <OperatorCrisisWorkspace />;
  if (view === 'requests') return <OperatorCollaborationWorkspace />;
  if (view === 'returns') return <OperatorLifecycleWorkspace view="returns" />;
  if (view === 'complaints') return <OperatorLifecycleWorkspace view="complaints" />;
  return <OperatorQueueWorkspace />;
}

type OperatorLineMode = {
  slug: 'urgent' | 'standard' | 'low';
  priority: AppealPriority;
  title: string;
  description: string;
};

const OPERATOR_LINE_MODES: OperatorLineMode[] = [
  {
    slug: 'urgent',
    priority: 'Urgent',
    title: 'Срочные',
    description: 'Обращения с уже подтверждённым срочным приоритетом.',
  },
  {
    slug: 'standard',
    priority: 'Standard',
    title: 'Обычные',
    description: 'Основной поток обращений в порядке ожидания.',
  },
  {
    slug: 'low',
    priority: 'Low',
    title: 'Низкие',
    description: 'Обращения с низким приоритетом в порядке ожидания.',
  },
];

function OperatorLineWorkspace() {
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const { linePriority, appealId } = useParams();
  const mode = OPERATOR_LINE_MODES.find((candidate) => candidate.slug === linePriority);
  const [leaseId] = useState(getOperatorWorkLeaseId);
  const [notice, setNotice] = useState('');
  const [detailDirty, setDetailDirty] = useState(false);
  const takeNextMutation = useMutation({
    mutationFn: (selectedMode: OperatorLineMode) => acquireNextOperatorWork(leaseId, {
      scope: 'queue',
      priority: selectedMode.priority,
    }),
    onSuccess: (lease, selectedMode) => {
      navigate(`/staff/operator/line/${selectedMode.slug}/${lease.appealId}`, { replace: true });
    },
  });
  const detailQuery = useQuery({
    queryKey: ['operator-appeal', appealId],
    queryFn: async () => {
      await acquireOperatorWork(appealId!, leaseId);
      return getOperatorAppeal(appealId!);
    },
    enabled: Boolean(mode && appealId),
    retry: false,
  });

  useEffect(() => {
    if (!mode || appealId || !takeNextMutation.isIdle) return;
    takeNextMutation.mutate(mode);
  }, [appealId, mode, takeNextMutation]);

  useEffect(() => {
    if (!appealId) return undefined;
    const heartbeat = window.setInterval(() => {
      void heartbeatOperatorWork(appealId, leaseId).catch(() => undefined);
    }, 30_000);
    return () => window.clearInterval(heartbeat);
  }, [appealId, leaseId]);

  useEffect(() => {
    if (!appealId) setDetailDirty(false);
  }, [appealId]);

  function chooseMode(selectedMode: OperatorLineMode) {
    setNotice('');
    takeNextMutation.reset();
    navigate(`/staff/operator/line/${selectedMode.slug}`);
  }

  function retryLine() {
    if (!mode) return;
    setNotice('');
    takeNextMutation.mutate(mode);
  }

  function finish(message: string) {
    if (!mode) return;
    flushSync(() => setDetailDirty(false));
    setNotice(message);
    navigate(`/staff/operator/line/${mode.slug}`, { replace: true });
    takeNextMutation.reset();
  }

  async function refresh() {
    await queryClient.invalidateQueries({ queryKey: ['operator-appeal', appealId] });
  }

  async function changeLine() {
    if (detailDirty && !window.confirm('Введённые данные ещё не сохранены. Выйти без сохранения?')) return;
    flushSync(() => setDetailDirty(false));
    if (appealId) await releaseOperatorWork(appealId, leaseId).catch(() => undefined);
    setNotice('');
    takeNextMutation.reset();
    navigate('/staff/operator/line');
  }

  function continueLine() {
    if (!mode) return;
    flushSync(() => setDetailDirty(false));
    navigate(`/staff/operator/line/${mode.slug}`, { replace: true });
    takeNextMutation.reset();
  }

  if (linePriority && !mode) {
    return (
      <section className="operator-line" aria-labelledby="operator-line-heading">
        <div className="empty-state">
          <h1 id="operator-line-heading" className="staff-title">Такой линии нет</h1>
          <p>Выберите один из доступных режимов работы.</p>
          <md-filled-button onClick={() => navigate('/staff/operator/line', { replace: true })}>Выбрать линию</md-filled-button>
        </div>
      </section>
    );
  }

  if (!mode) {
    return (
      <section className="operator-line" aria-labelledby="operator-line-heading">
        <header className="operator-line__heading">
          <p className="eyebrow">Кабинет оператора</p>
          <h1 id="operator-line-heading" className="staff-title">Линия</h1>
          <p className="staff-copy">Выберите приоритет. Система закрепит за вами одно свободное обращение и после завершения откроет следующее.</p>
        </header>
        <div className="operator-line__modes" aria-label="Режим линии">
          {OPERATOR_LINE_MODES.map((candidate) => (
            <section className="operator-line-mode" key={candidate.slug}>
              <div>
                <h2>{candidate.title}</h2>
                <p>{candidate.description}</p>
              </div>
              <md-outlined-button onClick={() => chooseMode(candidate)}>Выйти на линию</md-outlined-button>
            </section>
          ))}
        </div>
        <p className="operator-line__note">Неподтверждённые сигналы непосредственной опасности сначала проверяются в разделе «Срочная помощь».</p>
      </section>
    );
  }

  return (
    <section className="operator-line operator-line--active" aria-labelledby="operator-line-heading">
      <UnsavedChangesGuard when={detailDirty} />
      <header className="operator-line__active-heading">
        <div>
          <p className="eyebrow">Линия оператора</p>
          <h1 id="operator-line-heading" className="staff-title">{mode.title}</h1>
          <p className="staff-copy">Одно обращение за раз. После завершения система автоматически закрепит следующее свободное.</p>
        </div>
        <md-outlined-button disabled={takeNextMutation.isPending} onClick={() => void changeLine()}>Сменить линию</md-outlined-button>
      </header>

      <ActionReceipt message={notice} />
      {!appealId && takeNextMutation.isPending ? (
        <div className="operator-line__state" role="status">
          <h2>Ищем свободное обращение…</h2>
          <p>Система проверяет выбранную линию и закрепляет только одну карточку.</p>
        </div>
      ) : null}
      {!appealId && takeNextMutation.isError ? (
        <div className="operator-line__state">
          <h2>В этой линии сейчас нет свободных обращений</h2>
          <p>{operatorError(takeNextMutation.error)}</p>
          <div className="operator-line__actions">
            <md-filled-button onClick={retryLine}>Проверить снова</md-filled-button>
            <md-outlined-button onClick={() => void changeLine()}>Сменить линию</md-outlined-button>
          </div>
        </div>
      ) : null}
      {appealId && detailQuery.isPending ? <p className="staff-copy" role="status">Открываем обращение…</p> : null}
      {appealId && detailQuery.isError ? (
        <div className="operator-line__state">
          <h2>{isOperatorLeaseConflict(detailQuery.error) ? 'Обращение уже взял другой оператор' : 'Не удалось открыть обращение'}</h2>
          <p>{operatorError(detailQuery.error)}</p>
          <md-filled-button onClick={continueLine}>Взять следующее в этой линии</md-filled-button>
        </div>
      ) : null}
      {detailQuery.data ? (
        <AppealDetail
          key={detailQuery.data.id}
          appeal={detailQuery.data}
          onRefresh={refresh}
          onFinish={finish}
          onDirtyChange={setDetailDirty}
          onLeave={continueLine}
          leaseId={leaseId}
        />
      ) : null}
    </section>
  );
}

function OperatorQueueWorkspace() {
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const location = useLocation();
  const { appealId: selectedId } = useParams();
  const [leaseId] = useState(getOperatorWorkLeaseId);
  const [searchParams, setSearchParams] = useSearchParams();
  const sort = searchParams.get('sort') ?? 'default';
  const stage = searchParams.get('stage') ?? 'all';
  const priorityFilter = searchParams.get('priority') ?? 'all';
  const waiting = searchParams.get('waiting') ?? 'all';
  const categoryFilter = searchParams.get('category') ?? 'all';
  const searchTerm = searchParams.get('q') ?? '';
  const hasAdvancedFilters = categoryFilter !== 'all' || waiting !== 'all' || sort !== 'default';
  const [filtersOpen, setFiltersOpen] = useState(hasAdvancedFilters);
  const [visibleCount, setVisibleCount] = useState(QUEUE_PAGE_SIZE);
  const [notice, setNotice] = useState('');
  const [detailDirty, setDetailDirty] = useState(false);
  const queueQuery = useQuery({
    queryKey: ['operator-queue', sort],
    queryFn: () => getOperatorQueue(sort, leaseId),
    refetchInterval: 10_000,
  });
  const detailQuery = useQuery({
    queryKey: ['operator-appeal', selectedId],
    queryFn: async () => {
      await acquireOperatorWork(selectedId!, leaseId);
      void queryClient.invalidateQueries({ queryKey: ['operator-queue'] });
      return getOperatorAppeal(selectedId!);
    },
    enabled: Boolean(selectedId),
    retry: false,
  });

  useEffect(() => {
    if (!selectedId) return undefined;
    const appealId = selectedId;
    const heartbeat = window.setInterval(() => {
      void heartbeatOperatorWork(appealId, leaseId).catch(() => {
        void queryClient.invalidateQueries({ queryKey: ['operator-queue'] });
      });
    }, 30_000);
    return () => {
      window.clearInterval(heartbeat);
    };
  }, [leaseId, queryClient, selectedId]);

  useEffect(() => {
    if (!selectedId) setDetailDirty(false);
  }, [selectedId]);
  useEffect(() => {
    if (hasAdvancedFilters) setFiltersOpen(true);
  }, [hasAdvancedFilters]);
  useEffect(() => {
    setVisibleCount(QUEUE_PAGE_SIZE);
  }, [categoryFilter, priorityFilter, searchTerm, sort, stage, waiting]);
  const categories = useMemo(() => [...new Set(queueQuery.data?.items.map((item) => item.category) ?? [])], [queueQuery.data]);
  const filteredItems = useMemo(() => (queueQuery.data?.items ?? []).filter((item) =>
    (stage === 'all'
      || (stage === 'unassigned' && ['new', 'triaged'].includes(item.status.toLowerCase()))
      || item.status.toLowerCase() === stage)
      && (priorityFilter === 'all' || item.priority.toLowerCase() === priorityFilter)
      && (waiting === 'all' || (waiting === 'overdue' && item.isOverdue))
      && (categoryFilter === 'all' || item.category === categoryFilter)
      && (!searchTerm.trim() || item.caseNumber.toLowerCase().includes(searchTerm.trim().toLowerCase()))),
  [categoryFilter, priorityFilter, queueQuery.data, searchTerm, stage, waiting]);
  const selectedListIndex = filteredItems.findIndex((item) => item.id === selectedId);
  const displayedItems = filteredItems.slice(
    0,
    Math.max(visibleCount, selectedListIndex >= 0 ? selectedListIndex + 1 : 0),
  );
  const takeNextMutation = useMutation({
    mutationFn: () => acquireNextOperatorWork(leaseId, { scope: 'queue' }),
    onSuccess: (lease) => {
      setNotice('');
      navigate({ pathname: `/staff/operator/queue/${lease.appealId}`, search: location.search });
      void queryClient.invalidateQueries({ queryKey: ['operator-queue'] });
    },
    onError: () => setNotice(''),
  });

  function setFilter(name: string, value: string) {
    const next = new URLSearchParams(searchParams);
    if (!value || value === 'all' || (name === 'sort' && value === 'default')) next.delete(name);
    else next.set(name, value);
    setSearchParams(next, { replace: true });
  }

  function selectAppeal(appealId: string | null) {
    setNotice('');
    if (selectedId && selectedId !== appealId) {
      void releaseOperatorWork(selectedId, leaseId).then(() => {
        void queryClient.invalidateQueries({ queryKey: ['operator-queue'] });
      }).catch(() => undefined);
    }
    navigate({
      pathname: appealId ? `/staff/operator/queue/${appealId}` : '/staff/operator/queue',
      search: location.search,
    });
  }

  async function refresh() {
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: ['operator-queue'] }),
      queryClient.invalidateQueries({ queryKey: ['operator-appeal', selectedId] }),
    ]);
  }

  function finish(message: string) {
    const completedItem = filteredItems.find((item) => item.id === selectedId);
    flushSync(() => setDetailDirty(false));
    void acquireNextOperatorWork(leaseId, { scope: 'queue' }).then((nextLease) => {
      navigate({ pathname: `/staff/operator/queue/${nextLease.appealId}`, search: location.search });
      setNotice(`${message}${completedItem ? ` ${completedItem.applicantTypeText}, категория «${completedItem.category}».` : ''} Открыто следующее свободное обращение.`);
    }).catch(() => {
      navigate({ pathname: '/staff/operator/queue', search: location.search });
      setNotice(`${message}${completedItem ? ` ${completedItem.applicantTypeText}, категория «${completedItem.category}».` : ''} Открыт актуальный список.`);
    }).finally(() => {
      void queryClient.invalidateQueries({ queryKey: ['operator-queue'] });
    });
  }

  return (
    <ResponsiveMasterDetail
      className="operator-workspace"
      hasDetail={Boolean(selectedId)}
      focusKey={selectedId}
      scrollKey={`operator-queue:${location.search}`}
    >
      <UnsavedChangesGuard when={detailDirty} />
      <section className="operator-queue" aria-labelledby="operator-queue-heading">
        <div className="operator-heading">
          <div>
            <p className="eyebrow">Кабинет оператора</p>
            <h1 id="operator-queue-heading" className="staff-title">Разбор обращений</h1>
            <p className="staff-copy">Откройте обращение, проверьте исходные сведения, определите маршрут и назначьте специалиста.</p>
          </div>
          {!selectedId ? (
            <md-filled-button disabled={takeNextMutation.isPending} onClick={() => takeNextMutation.mutate()}>
              {takeNextMutation.isPending ? 'Закрепляем…' : 'Взять следующее по приоритету'}
            </md-filled-button>
          ) : null}
          {queueQuery.data ? (
            <div className="queue-counts" aria-label="Сводка очереди">
              <span><strong>{queueQuery.data.total}</strong> в очереди</span>
              <span><strong>{queueQuery.data.overdueCount}</strong> ждут больше {queueQuery.data.overdueHours} ч</span>
            </div>
          ) : null}
        </div>

        <div className="queue-filters queue-filters--primary" aria-label="Основные фильтры очереди">
          <label className="text-field queue-search"><span>Найти по номеру обращения</span><input type="search" value={searchTerm} onChange={(event) => setFilter('q', event.target.value)} placeholder="ОБР-XXXXXXXX" /></label>
          <label className="select-field"><span>Задача</span><select aria-label="Задача" value={stage} onChange={(event) => setFilter('stage', event.target.value)}><option value="all">Все задачи</option><option value="new">Новые — провести разбор</option><option value="triaged">Проверенные — назначить</option><option value="unassigned">Без специалиста</option></select></label>
          <label className="select-field"><span>Приоритет</span><select aria-label="Приоритет" value={priorityFilter} onChange={(event) => setFilter('priority', event.target.value)}><option value="all">Любой</option><option value="urgent">Срочный</option><option value="standard">Обычный</option><option value="low">Низкий</option></select></label>
        </div>
        <details className="queue-more-filters" open={filtersOpen} onToggle={(event) => setFiltersOpen(event.currentTarget.open)}>
          <summary>Дополнительные фильтры{hasAdvancedFilters ? ' · применены' : ''}</summary>
          <div className="queue-filters queue-filters--advanced">
            <label className="select-field"><span>Категория</span><select aria-label="Категория" value={categoryFilter} onChange={(event) => setFilter('category', event.target.value)}><option value="all">Все категории</option>{categories.map((category) => <option key={category} value={category}>{category}</option>)}</select></label>
            <label className="select-field"><span>Ожидание</span><select aria-label="Ожидание" value={waiting} onChange={(event) => setFilter('waiting', event.target.value)}><option value="all">Любое</option><option value="overdue">Дольше срока</option></select></label>
            <label className="select-field"><span>Порядок</span><select aria-label="Порядок" value={sort} onChange={(event) => setFilter('sort', event.target.value)}><option value="default">Сначала важные</option><option value="oldest">Сначала старые</option><option value="priority">По приоритету</option></select></label>
          </div>
        </details>

        {!selectedId ? <ActionReceipt message={notice} /> : null}
        {!selectedId && takeNextMutation.isError ? <p className="form-error" role="alert">{operatorError(takeNextMutation.error)}</p> : null}
        {queueQuery.isPending ? <p className="staff-copy">Загружаем очередь…</p> : null}
        {queueQuery.isError ? (
          <p className="form-error" role="alert">Не удалось загрузить очередь. Повторите попытку.</p>
        ) : null}
        {filteredItems.length === 0 && !queueQuery.isPending ? (
          <div className="empty-state">
            <h2>По этим фильтрам задач нет</h2>
            <p>Измените фильтры или дождитесь обновления очереди.</p>
          </div>
        ) : null}
        {filteredItems.length ? (
          <div className="queue-list" aria-label="Новые обращения" data-scroll-region>
            {displayedItems.map((appeal) => (
              <QueueRow
                appeal={appeal}
                key={appeal.id}
                selected={selectedId === appeal.id}
                onSelect={() => selectAppeal(appeal.id)}
              />
            ))}
            <div className="queue-list__progress">
              <span>Показано {displayedItems.length} из {filteredItems.length}</span>
              {displayedItems.length < filteredItems.length ? (
                <md-outlined-button onClick={() => setVisibleCount((count) => count + QUEUE_PAGE_SIZE)}>
                  Показать ещё
                </md-outlined-button>
              ) : null}
            </div>
          </div>
        ) : null}
      </section>

      <section className="operator-detail" aria-label="Карточка обращения">
        {selectedId ? (
          <button className="operator-back" type="button" onClick={() => selectAppeal(null)}>
            Вернуться к очереди
          </button>
        ) : null}
        {selectedId ? <ActionReceipt message={notice} /> : null}
        {!selectedId ? (
          <div className="operator-detail__empty">
            <p className="eyebrow">Карточка обращения</p>
            <h2>Выберите обращение в очереди</h2>
            <p>Здесь появятся исходный рассказ, ответы, вложения и подсказка маршрута.</p>
          </div>
        ) : null}
        {selectedId && detailQuery.isPending ? <p className="staff-copy">Открываем обращение…</p> : null}
        {selectedId && detailQuery.isError ? (
          <div className="operator-detail__empty">
            <h2>{isOperatorLeaseConflict(detailQuery.error) ? 'Обращение уже разбирает другой оператор' : 'Обращение уже обновилось'}</h2>
            <p>{isOperatorLeaseConflict(detailQuery.error) ? 'Рабочая карточка закреплена за другим окном. Возьмите следующее свободное обращение.' : 'Откройте актуальную версию. Ваш выбор в фильтрах очереди сохранён.'}</p>
            {isOperatorLeaseConflict(detailQuery.error)
              ? <md-filled-button onClick={() => selectAppeal(null)}>Вернуться к свободным обращениям</md-filled-button>
              : <md-filled-button onClick={() => void detailQuery.refetch()}>Открыть актуальную версию</md-filled-button>}
          </div>
        ) : null}
        {detailQuery.data ? (
          <AppealDetail
            key={detailQuery.data.id}
            appeal={detailQuery.data}
            onRefresh={refresh}
            onFinish={finish}
            onDirtyChange={setDetailDirty}
            onLeave={() => selectAppeal(null)}
            leaseId={leaseId}
          />
        ) : null}
      </section>
    </ResponsiveMasterDetail>
  );
}

function QueueRow({
  appeal,
  selected,
  onSelect,
}: {
  appeal: OperatorQueueItem;
  selected: boolean;
  onSelect: () => void;
}) {
  return (
    <button
      className={`${selected ? 'queue-row queue-row--selected' : 'queue-row'}${appeal.workState === 'Busy' ? ' queue-row--busy' : ''}`}
      type="button"
      disabled={appeal.workState === 'Busy'}
      aria-current={selected ? 'true' : undefined}
      data-appeal-id={appeal.id}
      onClick={onSelect}
    >
      <span className="queue-row__topline">
        <strong>{appeal.nextAction}</strong>
        <span>{formatWaiting(appeal.waitingMinutes)}</span>
      </span>
      <span className="queue-row__category">{appeal.applicantTypeText} · {appeal.category}</span>
      <span className="queue-row__meta">{appeal.caseNumber}</span>
      <span className="queue-row__meta">
        {appeal.priorityText}{appeal.isOverdue ? ' · ждёт дольше срока' : ''}{appeal.hasAttachments ? ' · есть файл' : ''}
      </span>
      {appeal.workState === 'Mine' ? <span className="queue-row__work-state">Закреплено за вами в этом окне</span> : null}
      {appeal.workState === 'Busy' ? <span className="queue-row__work-state">Сейчас разбирает другой оператор</span> : null}
      {appeal.blockingReason ? <span className="queue-row__blocker">Препятствие: {appeal.blockingReason}</span> : null}
    </button>
  );
}

function AppealDetail({
  appeal,
  onRefresh,
  onFinish,
  onDirtyChange,
  onLeave,
  leaseId,
}: {
  appeal: Awaited<ReturnType<typeof getOperatorAppeal>>;
  onRefresh: () => Promise<void>;
  onFinish: (message: string) => void;
  onDirtyChange: (dirty: boolean) => void;
  onLeave: () => void;
  leaseId: string;
}) {
  const [categoryId, setCategoryId] = useState(appeal.categoryId ?? appeal.suggestion?.categoryId ?? '');
  const [priority, setPriority] = useState<AppealPriority>(appeal.priority);
  const [priorityReason, setPriorityReason] = useState('');
  const [expertId, setExpertId] = useState(appeal.routing.experts[0]?.id ?? '');
  const [actionMode, setActionMode] = useState<ActionMode>(null);
  const [activeStep, setActiveStep] = useState<TriageStep>(appeal.status === 'New' ? 'route' : 'assign');
  const [operatorResponse, setOperatorResponse] = useState('');
  const [rejectionReason, setRejectionReason] = useState<'Spam' | 'OutOfScope'>('Spam');
  const [internalReason, setInternalReason] = useState('');
  const [allowOverCapacity, setAllowOverCapacity] = useState(false);
  const [overrideReason, setOverrideReason] = useState('');
  const [message, setMessage] = useState('');

  const dirty = categoryId !== (appeal.categoryId ?? appeal.suggestion?.categoryId ?? '')
    || priority !== appeal.priority
    || priorityReason.trim().length > 0
    || expertId !== (appeal.routing.experts[0]?.id ?? '')
    || allowOverCapacity
    || overrideReason.trim().length > 0
    || operatorResponse.trim().length > 0
    || internalReason.trim().length > 0;

  useEffect(() => {
    onDirtyChange(dirty);
  }, [dirty, onDirtyChange]);

  const triageMutation = useMutation({
    mutationFn: () => triageOperatorAppeal(appeal.id, {
      categoryId,
      priority,
      expectedVersion: appeal.version,
      reason: priorityReason || undefined,
      leaseId,
    }),
    onSuccess: async () => {
      setMessage('Категория и приоритет сохранены. Теперь назначьте специалиста.');
      setPriorityReason('');
      setActiveStep('assign');
      await onRefresh();
    },
  });
  const assignMutation = useMutation({
    mutationFn: () => assignOperatorAppeal(appeal.id, {
      expertId,
      expectedVersion: appeal.version,
      allowOverCapacity,
      overrideReason: overrideReason || undefined,
      leaseId,
    }),
    onSuccess: () => onFinish('Обращение распределено специалисту.'),
  });
  const rejectMutation = useMutation({
    mutationFn: () => rejectOperatorAppeal(appeal.id, {
      reasonCode: rejectionReason,
      internalReason,
      expectedVersion: appeal.version,
      leaseId,
    }),
    onSuccess: () => onFinish('Обращение отклонено и убрано из очереди.'),
  });
  const resolveMutation = useMutation({
    mutationFn: () => resolveOperatorAppeal(appeal.id, {
      message: operatorResponse,
      expectedVersion: appeal.version,
      leaseId,
    }),
    onSuccess: () => onFinish('Ответ заявителю сохранен, обращение завершено.'),
  });
  const activeError = triageMutation.error
    ?? assignMutation.error
    ?? rejectMutation.error
    ?? resolveMutation.error;
  const selectedExpert = appeal.routing.experts.find((expert) => expert.id === expertId);
  const staleConflict = isOperatorStaleConflict(activeError);
  const leaseConflict = isOperatorLeaseConflict(activeError);

  async function refreshAfterConflict() {
    await onRefresh();
    triageMutation.reset();
    assignMutation.reset();
    rejectMutation.reset();
    resolveMutation.reset();
    setMessage('Открыта актуальная версия. Введённые значения оставлены на экране — проверьте их и отправьте снова.');
  }

  return (
    <div className="appeal-detail">
      <header className="appeal-detail__heading">
        <div>
          <p className="eyebrow">Требуется сейчас</p>
          <h2 data-detail-heading tabIndex={-1}>{appeal.status === 'New' ? 'Провести разбор обращения' : 'Назначить специалиста'}</h2>
        </div>
        <time dateTime={appeal.receivedAt}>{formatDateTime(appeal.receivedAt)}</time>
      </header>

      <dl className="operator-case-summary" aria-label="Сводка обращения">
        <div><dt>Номер обращения</dt><dd>{appeal.caseNumber}</dd></div>
        <div><dt>Заявитель</dt><dd>{appeal.applicantTypeText}</dd></div>
        <div><dt>Статус</dt><dd>{appeal.statusText}</dd></div>
        <div><dt>Категория</dt><dd>{appeal.category ?? 'Нужно определить'}</dd></div>
        <div><dt>Приоритет</dt><dd>{appeal.priorityText}</dd></div>
      </dl>

      <div className="operator-flow" aria-label="Рабочая область разбора">
        <section className="operator-step operator-step--section operator-step--context" aria-labelledby="understand-heading">
          <header className="operator-step__heading">
            <div><span>Исходные сведения</span><h3 id="understand-heading">Что произошло</h3></div>
          </header>
          <div className="operator-step__body">
            <div className="appeal-original">
              <p>{appeal.narrative ?? 'Свободный текст не добавлен — заявитель заполнил структурированную форму.'}</p>
              {appeal.answers.length > 0 ? <dl className="answer-list">{appeal.answers.map((answer) => <div key={answer.questionCode}><dt>{answer.question}</dt><dd>{answer.value}</dd></div>)}</dl> : null}
            </div>
            {appeal.attachments.length > 0 ? <section className="operator-attachments" aria-labelledby="operator-attachments-heading"><h4 id="operator-attachments-heading">Вложения</h4>{appeal.attachments.map((attachment) => <div className="operator-attachment" key={attachment.id}><div><strong>{attachment.displayName}</strong><span>{formatFileSize(attachment.size)}</span></div><md-outlined-button onClick={() => downloadOperatorAttachment(appeal.id, attachment)}>Скачать</md-outlined-button></div>)}</section> : <p className="operator-step__waiting">Вложений нет.</p>}
          </div>
        </section>

        <section className={`operator-step operator-step--section${activeStep === 'route' ? ' operator-step--active' : ''}`} aria-labelledby="triage-heading">
          <header className="operator-step__heading">
            <div><span>Решение 1</span><h3 id="triage-heading">Категория и приоритет</h3></div>
            {activeStep === 'assign' && appeal.status === 'Triaged' ? <md-outlined-button onClick={() => setActiveStep('route')}>Изменить</md-outlined-button> : null}
          </header>
          {activeStep === 'route' ? (
            <div className="operator-step__body">
              {appeal.suggestion ? <p className="suggestion-copy"><strong>Подсказка системы:</strong> {appeal.suggestion.category}. {appeal.suggestion.reason}.</p> : null}
              <div className="triage-fields">
                <label className="select-field"><span>Категория</span><select aria-label="Категория обращения" value={categoryId} onChange={(event) => setCategoryId(event.target.value)}><option value="">Выберите категорию</option>{appeal.categories.map((category) => <option value={category.id} key={category.id}>{category.displayName}</option>)}</select></label>
                <label className="select-field"><span>Приоритет</span><select aria-label="Приоритет обращения" value={priority} onChange={(event) => setPriority(event.target.value as AppealPriority)}><option value="Urgent">Срочный</option><option value="Standard">Обычный</option><option value="Low">Низкий</option></select></label>
              </div>
              {priority !== appeal.priority ? <label className="text-field"><span>Почему меняется приоритет (необязательно)</span><input value={priorityReason} maxLength={1_000} onChange={(event) => setPriorityReason(event.target.value)} /></label> : null}
              <md-filled-button disabled={!categoryId || triageMutation.isPending} onClick={() => triageMutation.mutate()}>{triageMutation.isPending ? 'Сохраняем…' : 'Сохранить разбор'}</md-filled-button>
            </div>
          ) : (
            <div className="operator-step__summary">
              <p><strong>{appeal.category ?? 'Категория не выбрана'}</strong> · {appeal.priorityText}</p>
              <p>{appeal.routing.groupName ? `Маршрут: ${appeal.routing.groupName}` : 'Группа помощи пока не определена'}</p>
            </div>
          )}
        </section>

        <section className={`operator-step operator-step--section${activeStep === 'assign' ? ' operator-step--active' : ''}`} aria-labelledby="assignment-heading">
          <header className="operator-step__heading"><div><span>Решение 2</span><h3 id="assignment-heading">Ответственный специалист</h3></div></header>
          {appeal.status === 'Triaged' && activeStep === 'assign' ? (
            <div className="operator-step__body">
              <p className="routing-group">{appeal.routing.groupName ? `Подходящая группа: ${appeal.routing.groupName}` : 'Подходящая группа не определена'}</p>
              {appeal.routing.warning ? <p className="routing-warning">{appeal.routing.warning}</p> : null}
              <div className="expert-list" aria-label="Доступные специалисты">
                {appeal.routing.experts.map((expert) => (
                  <label className={expertId === expert.id ? 'expert-choice expert-choice--selected' : 'expert-choice'} key={expert.id}>
                    <input type="radio" name="responsible-expert" value={expert.id} checked={expertId === expert.id} onChange={() => setExpertId(expert.id)} />
                    <span><strong>{expert.displayName}</strong><small>{expert.activeCount} из {expert.limit} активных{expert.atCapacity ? ' · лимит достигнут' : ''}</small></span>
                  </label>
                ))}
              </div>
              {selectedExpert?.atCapacity ? (
                <div className="capacity-override">
                  <label><input type="checkbox" checked={allowOverCapacity} onChange={(event) => setAllowOverCapacity(event.target.checked)} /><span>Назначить сверх лимита вручную</span></label>
                  {allowOverCapacity ? <label className="text-field"><span>Причина исключения</span><textarea required minLength={10} value={overrideReason} maxLength={1_000} onChange={(event) => setOverrideReason(event.target.value)} /><small>Минимум 10 символов · сейчас {overrideReason.trim().length}</small></label> : null}
                </div>
              ) : null}
              <md-filled-button disabled={!expertId || assignMutation.isPending || (Boolean(selectedExpert?.atCapacity) && (!allowOverCapacity || overrideReason.trim().length < 10))} onClick={() => assignMutation.mutate()}>
                {assignMutation.isPending ? 'Назначаем…' : 'Назначить специалиста'}
              </md-filled-button>
            </div>
          ) : <p className="operator-step__waiting">{appeal.status === 'New' ? 'Сначала завершите разбор и сохраните маршрут.' : 'Сохраните изменения маршрута, чтобы перейти к назначению.'}</p>}
        </section>
      </div>

      <details className="operator-secondary-decisions">
        <summary>Другие решения</summary>
        <p>Ответ без специалиста завершит обращение. Отклонение доступно только для спама или ситуации вне компетенции сервиса.</p>
        <div className="action-row">
          <md-outlined-button onClick={() => setActionMode('respond')}>Ответить без специалиста</md-outlined-button>
          <md-outlined-button onClick={() => setActionMode('reject')}>Отклонить обращение</md-outlined-button>
        </div>
        {actionMode === 'respond' ? (
          <div className="decision-panel">
            <label className="text-field"><span>Ответ заявителю</span><textarea value={operatorResponse} maxLength={4_000} onChange={(event) => setOperatorResponse(event.target.value)} /></label>
            <md-filled-button disabled={operatorResponse.trim().length < 10 || resolveMutation.isPending} onClick={() => resolveMutation.mutate()}>{resolveMutation.isPending ? 'Сохраняем…' : 'Отправить ответ и завершить'}</md-filled-button>
          </div>
        ) : null}
        {actionMode === 'reject' ? (
          <div className="decision-panel">
            <label className="select-field"><span>Причина</span><select value={rejectionReason} onChange={(event) => setRejectionReason(event.target.value as 'Spam' | 'OutOfScope')}><option value="Spam">Спам</option><option value="OutOfScope">Вне компетенции сервиса</option></select></label>
            <label className="text-field"><span>Внутренняя причина</span><textarea value={internalReason} maxLength={1_000} onChange={(event) => setInternalReason(event.target.value)} /></label>
            <md-filled-button disabled={internalReason.trim().length < 5 || rejectMutation.isPending} onClick={() => rejectMutation.mutate()}>{rejectMutation.isPending ? 'Завершаем…' : 'Подтвердить отклонение'}</md-filled-button>
          </div>
        ) : null}
      </details>

      {message ? <p className="operator-message" role="status">{message}</p> : null}
      {leaseConflict ? (
        <div className="operator-stale" role="alert">
          <h3>Обращение перешло другому оператору</h3>
          <p>Срок закрепления закончился, и карточку уже взяло другое рабочее окно. Ваше действие не сохранено.</p>
          <md-filled-button onClick={onLeave}>Вернуться к свободным обращениям</md-filled-button>
        </div>
      ) : staleConflict ? (
        <div className="operator-stale" role="alert">
          <h3>Обращение обновил другой сотрудник</h3>
          <p>Откройте актуальную версию. Введённые значения останутся на этом экране, чтобы их можно было проверить и применить повторно.</p>
          <md-filled-button onClick={() => void refreshAfterConflict()}>Открыть актуальную версию</md-filled-button>
        </div>
      ) : activeError ? <p className="form-error" role="alert">{operatorError(activeError)}</p> : null}
    </div>
  );
}

function formatWaiting(minutes: number) {
  if (minutes < 60) return `${Math.max(1, minutes)} мин`;
  const hours = Math.floor(minutes / 60);
  const remaining = minutes % 60;
  return remaining ? `${hours} ч ${remaining} мин` : `${hours} ч`;
}

function formatDateTime(value: string) {
  return new Intl.DateTimeFormat('ru-RU', { dateStyle: 'medium', timeStyle: 'short' })
    .format(new Date(value));
}

function formatFileSize(size: number) {
  if (size < 1024 * 1024) return `${Math.max(1, Math.round(size / 1024))} КБ`;
  return `${(size / 1024 / 1024).toFixed(1)} МБ`;
}
