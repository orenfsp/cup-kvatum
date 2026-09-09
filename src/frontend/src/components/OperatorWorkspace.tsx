import '@material/web/button/filled-button.js';
import '@material/web/button/outlined-button.js';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useEffect, useMemo, useState } from 'react';
import { useLocation, useNavigate, useParams, useSearchParams } from 'react-router-dom';
import {
  assignOperatorAppeal,
  downloadOperatorAttachment,
  getOperatorAppeal,
  getOperatorQueue,
  operatorError,
  rejectOperatorAppeal,
  resolveOperatorAppeal,
  triageOperatorAppeal,
  type AppealPriority,
  type OperatorQueueItem,
} from '../api/operatorQueue';
import { OperatorCollaborationWorkspace } from './OperatorCollaborationWorkspace';
import { OperatorCrisisWorkspace } from './OperatorCrisisWorkspace';
import { OperatorLifecycleWorkspace } from './OperatorLifecycleWorkspace';
import { ResponsiveMasterDetail } from './ux/WorkspacePrimitives';

type ActionMode = 'respond' | 'reject' | null;

export function OperatorWorkspace({ view = 'queue' }: { view?: 'queue' | 'crisis' | 'requests' | 'lifecycle' }) {
  if (view === 'crisis') return <OperatorCrisisWorkspace />;
  if (view === 'requests') return <OperatorCollaborationWorkspace />;
  if (view === 'lifecycle') return <OperatorLifecycleWorkspace />;
  return <OperatorQueueWorkspace />;
}

function OperatorQueueWorkspace() {
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const location = useLocation();
  const { appealId: selectedId } = useParams();
  const [searchParams, setSearchParams] = useSearchParams();
  const sort = searchParams.get('sort') ?? 'default';
  const stage = searchParams.get('stage') ?? 'all';
  const priorityFilter = searchParams.get('priority') ?? 'all';
  const waiting = searchParams.get('waiting') ?? 'all';
  const categoryFilter = searchParams.get('category') ?? 'all';
  const [notice, setNotice] = useState('');
  const queueQuery = useQuery({
    queryKey: ['operator-queue', sort],
    queryFn: () => getOperatorQueue(sort),
    refetchInterval: 30_000,
  });
  const detailQuery = useQuery({
    queryKey: ['operator-appeal', selectedId],
    queryFn: () => getOperatorAppeal(selectedId!),
    enabled: Boolean(selectedId),
    retry: false,
  });
  const categories = useMemo(() => [...new Set(queueQuery.data?.items.map((item) => item.category) ?? [])], [queueQuery.data]);
  const filteredItems = useMemo(() => (queueQuery.data?.items ?? []).filter((item) =>
    (stage === 'all' || item.status.toLowerCase() === stage)
      && (priorityFilter === 'all' || item.priority.toLowerCase() === priorityFilter)
      && (waiting === 'all' || (waiting === 'overdue' && item.isOverdue))
      && (categoryFilter === 'all' || item.category === categoryFilter)),
  [categoryFilter, priorityFilter, queueQuery.data, stage, waiting]);

  function setFilter(name: string, value: string) {
    const next = new URLSearchParams(searchParams);
    if (value === 'all' || (name === 'sort' && value === 'default')) next.delete(name);
    else next.set(name, value);
    setSearchParams(next, { replace: true });
  }

  function selectAppeal(appealId: string | null) {
    setNotice('');
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
    const currentIndex = filteredItems.findIndex((item) => item.id === selectedId);
    const nextItem = filteredItems[currentIndex + 1] ?? filteredItems[currentIndex - 1];
    selectAppeal(nextItem?.id ?? null);
    setNotice(message);
    void queryClient.invalidateQueries({ queryKey: ['operator-queue'] });
  }

  return (
    <ResponsiveMasterDetail className="operator-workspace" hasDetail={Boolean(selectedId)}>
      <section className="operator-queue" aria-labelledby="operator-queue-heading">
        <div className="operator-heading">
          <div>
            <p className="eyebrow">Кабинет оператора</p>
            <h1 id="operator-queue-heading" className="staff-title">Очередь обращений</h1>
          </div>
          {queueQuery.data ? (
            <div className="queue-counts" aria-label="Сводка очереди">
              <span><strong>{queueQuery.data.total}</strong> в очереди</span>
              <span><strong>{queueQuery.data.overdueCount}</strong> ждут больше {queueQuery.data.overdueHours} ч</span>
            </div>
          ) : null}
        </div>

        <div className="queue-filters" aria-label="Фильтры очереди">
          <label className="select-field"><span>Задача</span><select value={stage} onChange={(event) => setFilter('stage', event.target.value)}><option value="all">Все задачи</option><option value="new">Новые — провести разбор</option><option value="triaged">Проверенные — назначить</option></select></label>
          <label className="select-field"><span>Категория</span><select value={categoryFilter} onChange={(event) => setFilter('category', event.target.value)}><option value="all">Все категории</option>{categories.map((category) => <option key={category} value={category}>{category}</option>)}</select></label>
          <label className="select-field"><span>Приоритет</span><select value={priorityFilter} onChange={(event) => setFilter('priority', event.target.value)}><option value="all">Любой</option><option value="urgent">Срочный</option><option value="standard">Обычный</option><option value="low">Низкий</option></select></label>
          <label className="select-field"><span>Ожидание</span><select value={waiting} onChange={(event) => setFilter('waiting', event.target.value)}><option value="all">Любое</option><option value="overdue">Дольше срока</option></select></label>
          <label className="select-field"><span>Порядок</span><select value={sort} onChange={(event) => setFilter('sort', event.target.value)}><option value="default">Сначала важные</option><option value="oldest">Сначала старые</option><option value="priority">По приоритету</option></select></label>
        </div>

        {notice ? <p className="operator-notice" role="status">{notice}</p> : null}
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
          <div className="queue-list" aria-label="Новые обращения">
            {filteredItems.map((appeal) => (
              <QueueRow
                appeal={appeal}
                key={appeal.id}
                selected={selectedId === appeal.id}
                onSelect={() => selectAppeal(appeal.id)}
              />
            ))}
          </div>
        ) : null}
      </section>

      <section className="operator-detail" aria-label="Карточка обращения">
        {selectedId ? (
          <button className="operator-back" type="button" onClick={() => selectAppeal(null)}>
            Вернуться к очереди
          </button>
        ) : null}
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
            <h2>Обращение обновил другой сотрудник</h2>
            <p>Откройте актуальную версию. Ваш безопасный выбор в фильтрах очереди сохранён.</p>
            <md-filled-button onClick={() => void detailQuery.refetch()}>Открыть актуальную версию</md-filled-button>
          </div>
        ) : null}
        {detailQuery.data ? (
          <AppealDetail
            appeal={detailQuery.data}
            onRefresh={refresh}
            onFinish={finish}
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
      className={selected ? 'queue-row queue-row--selected' : 'queue-row'}
      type="button"
      aria-pressed={selected}
      onClick={onSelect}
    >
      <span className="queue-row__topline">
        <strong>{appeal.nextAction}</strong>
        <span>{formatWaiting(appeal.waitingMinutes)}</span>
      </span>
      <span className="queue-row__category">{appeal.applicantTypeText} · {appeal.category}</span>
      <span className="queue-row__meta">
        {appeal.priorityText}{appeal.isOverdue ? ' · ждёт дольше срока' : ''}{appeal.hasAttachments ? ' · есть файл' : ''}
      </span>
      {appeal.blockingReason ? <span className="queue-row__blocker">Препятствие: {appeal.blockingReason}</span> : null}
    </button>
  );
}

function AppealDetail({
  appeal,
  onRefresh,
  onFinish,
}: {
  appeal: Awaited<ReturnType<typeof getOperatorAppeal>>;
  onRefresh: () => Promise<void>;
  onFinish: (message: string) => void;
}) {
  const [categoryId, setCategoryId] = useState(appeal.categoryId ?? appeal.suggestion?.categoryId ?? '');
  const [priority, setPriority] = useState<AppealPriority>(appeal.priority);
  const [priorityReason, setPriorityReason] = useState('');
  const [expertId, setExpertId] = useState(appeal.routing.experts[0]?.id ?? '');
  const [actionMode, setActionMode] = useState<ActionMode>(null);
  const [editingTriage, setEditingTriage] = useState(appeal.status === 'New');
  const [operatorResponse, setOperatorResponse] = useState('');
  const [rejectionReason, setRejectionReason] = useState<'Spam' | 'OutOfScope'>('Spam');
  const [internalReason, setInternalReason] = useState('');
  const [allowOverCapacity, setAllowOverCapacity] = useState(false);
  const [overrideReason, setOverrideReason] = useState('');
  const [message, setMessage] = useState('');

  useEffect(() => {
    setCategoryId(appeal.categoryId ?? appeal.suggestion?.categoryId ?? '');
    setPriority(appeal.priority);
    setExpertId(appeal.routing.experts[0]?.id ?? '');
    setEditingTriage(appeal.status === 'New');
    setActionMode(null);
    setMessage('');
  }, [appeal]);

  const triageMutation = useMutation({
    mutationFn: () => triageOperatorAppeal(appeal.id, {
      categoryId,
      priority,
      expectedVersion: appeal.version,
      reason: priorityReason || undefined,
    }),
    onSuccess: async () => {
      setMessage('Категория и приоритет сохранены. Теперь назначьте специалиста.');
      setEditingTriage(false);
      await onRefresh();
    },
  });
  const assignMutation = useMutation({
    mutationFn: () => assignOperatorAppeal(appeal.id, {
      expertId,
      expectedVersion: appeal.version,
      allowOverCapacity,
      overrideReason: overrideReason || undefined,
    }),
    onSuccess: () => onFinish('Обращение распределено специалисту.'),
  });
  const rejectMutation = useMutation({
    mutationFn: () => rejectOperatorAppeal(appeal.id, {
      reasonCode: rejectionReason,
      internalReason,
      expectedVersion: appeal.version,
    }),
    onSuccess: () => onFinish('Обращение отклонено и убрано из очереди.'),
  });
  const resolveMutation = useMutation({
    mutationFn: () => resolveOperatorAppeal(appeal.id, {
      message: operatorResponse,
      expectedVersion: appeal.version,
    }),
    onSuccess: () => onFinish('Ответ заявителю сохранен, обращение завершено.'),
  });
  const activeError = triageMutation.error
    ?? assignMutation.error
    ?? rejectMutation.error
    ?? resolveMutation.error;
  const selectedExpert = appeal.routing.experts.find((expert) => expert.id === expertId);

  return (
    <div className="appeal-detail">
      <header className="appeal-detail__heading">
        <div>
          <p className="eyebrow">{appeal.statusText}</p>
          <h2>{appeal.applicantTypeText}</h2>
        </div>
        <span>{formatDateTime(appeal.receivedAt)}</span>
      </header>

      <div className="operator-flow" aria-label="Последовательность разбора">
        <details className="operator-step" open={appeal.status === 'New'}>
          <summary>
            <span>Шаг 1</span>
            <strong>Понять ситуацию</strong>
            <small>{appeal.attachments.length > 0 ? `Есть файлов: ${appeal.attachments.length}` : 'Без вложений'}</small>
          </summary>
          <section className="appeal-original" aria-labelledby="original-heading">
            <h3 id="original-heading">Что рассказал заявитель</h3>
            <p>{appeal.narrative ?? 'Текст не добавлен — заявитель выбрал категорию.'}</p>
            {appeal.answers.length > 0 ? (
              <dl className="answer-list">
                {appeal.answers.map((answer) => (
                  <div key={answer.questionCode}><dt>{answer.question}</dt><dd>{answer.value}</dd></div>
                ))}
              </dl>
            ) : null}
          </section>
          {appeal.attachments.length > 0 ? (
            <section className="operator-attachments" aria-labelledby="operator-attachments-heading">
              <h3 id="operator-attachments-heading">Вложения заявителя</h3>
              {appeal.attachments.map((attachment) => (
                <div className="operator-attachment" key={attachment.id}>
                  <div><strong>{attachment.displayName}</strong><span>{formatFileSize(attachment.size)}</span></div>
                  <md-outlined-button onClick={() => downloadOperatorAttachment(appeal.id, attachment)}>Скачать</md-outlined-button>
                </div>
              ))}
            </section>
          ) : null}
        </details>

        <section className="operator-step operator-step--section" aria-labelledby="triage-heading">
          <header className="operator-step__heading">
            <div><span>Шаг 2</span><h3 id="triage-heading">Определить маршрут</h3></div>
            {!editingTriage && appeal.status === 'Triaged' ? <md-outlined-button onClick={() => setEditingTriage(true)}>Изменить</md-outlined-button> : null}
          </header>
          {editingTriage || appeal.status === 'New' ? (
            <div className="operator-step__body">
              {appeal.suggestion ? <p className="suggestion-copy"><strong>Подсказка системы:</strong> {appeal.suggestion.category}. {appeal.suggestion.reason}.</p> : null}
              <div className="triage-fields">
                <label className="select-field"><span>Категория</span><select value={categoryId} onChange={(event) => setCategoryId(event.target.value)}><option value="">Выберите категорию</option>{appeal.categories.map((category) => <option value={category.id} key={category.id}>{category.displayName}</option>)}</select></label>
                <label className="select-field"><span>Приоритет</span><select value={priority} onChange={(event) => setPriority(event.target.value as AppealPriority)}><option value="Urgent">Срочный</option><option value="Standard">Обычный</option><option value="Low">Низкий</option></select></label>
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

        <section className="operator-step operator-step--section" aria-labelledby="assignment-heading">
          <header className="operator-step__heading"><div><span>Шаг 3</span><h3 id="assignment-heading">Назначить помощь</h3></div></header>
          {appeal.status === 'Triaged' && !editingTriage ? (
            <div className="operator-step__body">
              <p className="routing-group">{appeal.routing.groupName ? `Подходящая группа: ${appeal.routing.groupName}` : 'Подходящая группа не определена'}</p>
              {appeal.routing.warning ? <p className="routing-warning">{appeal.routing.warning}</p> : null}
              <div className="expert-list" aria-label="Доступные специалисты">
                {appeal.routing.experts.map((expert) => (
                  <button className={expertId === expert.id ? 'expert-choice expert-choice--selected' : 'expert-choice'} key={expert.id} type="button" aria-pressed={expertId === expert.id} onClick={() => setExpertId(expert.id)}>
                    <strong>{expert.displayName}</strong>
                    <span>{expert.activeCount} из {expert.limit} активных{expert.atCapacity ? ' · лимит достигнут' : ''}</span>
                  </button>
                ))}
              </div>
              {selectedExpert?.atCapacity ? (
                <div className="capacity-override">
                  <label><input type="checkbox" checked={allowOverCapacity} onChange={(event) => setAllowOverCapacity(event.target.checked)} /><span>Назначить сверх лимита вручную</span></label>
                  {allowOverCapacity ? <label className="text-field"><span>Причина исключения</span><textarea value={overrideReason} maxLength={1_000} onChange={(event) => setOverrideReason(event.target.value)} /></label> : null}
                </div>
              ) : null}
              <md-filled-button disabled={!expertId || assignMutation.isPending || (Boolean(selectedExpert?.atCapacity) && (!allowOverCapacity || overrideReason.trim().length < 5))} onClick={() => assignMutation.mutate()}>
                {assignMutation.isPending ? 'Назначаем…' : 'Назначить специалиста'}
              </md-filled-button>
            </div>
          ) : <p className="operator-step__waiting">{appeal.status === 'New' ? 'Сначала сохраните категорию и приоритет.' : 'Сохраните изменения маршрута, чтобы перейти к назначению.'}</p>}
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
      {activeError ? <p className="form-error" role="alert">{operatorError(activeError)}</p> : null}
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
