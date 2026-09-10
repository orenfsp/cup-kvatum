import '@material/web/button/filled-button.js';
import '@material/web/button/outlined-button.js';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useEffect, useMemo, useState } from 'react';
import { flushSync } from 'react-dom';
import { useNavigate, useParams } from 'react-router-dom';
import {
  approveCollaborationRequest,
  getCollaborationRequest,
  getCollaborationRequests,
  rejectCollaborationRequest,
} from '../api/operatorCollaboration';
import { isOperatorStaleConflict, operatorError } from '../api/operatorQueue';
import { ActionReceipt, ResponsiveMasterDetail, UnsavedChangesGuard } from './ux/WorkspacePrimitives';

export function OperatorCollaborationWorkspace() {
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const { appealId: selectedId } = useParams();
  const [notice, setNotice] = useState('');
  const [detailDirty, setDetailDirty] = useState(false);
  const listQuery = useQuery({
    queryKey: ['operator-collaboration'],
    queryFn: getCollaborationRequests,
    refetchInterval: 30_000,
  });
  const detailQuery = useQuery({
    queryKey: ['operator-collaboration', selectedId],
    queryFn: () => getCollaborationRequest(selectedId!),
    enabled: Boolean(selectedId),
    retry: false,
  });
  const pendingRequests = useMemo(
    () => listQuery.data?.items.filter((item) => item.status === 'Pending') ?? [],
    [listQuery.data],
  );

  useEffect(() => {
    if (!selectedId) setDetailDirty(false);
  }, [selectedId]);

  async function refresh() {
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: ['operator-collaboration'] }),
      queryClient.invalidateQueries({ queryKey: ['operator-collaboration', selectedId] }),
    ]);
  }

  function finish(message: string) {
    const items = pendingRequests;
    const currentIndex = items.findIndex((item) => item.id === selectedId);
    const next = items.slice(currentIndex + 1).find((item) => item.status === 'Pending' && item.id !== selectedId)
      ?? items.slice(0, Math.max(0, currentIndex)).find((item) => item.status === 'Pending' && item.id !== selectedId);
    flushSync(() => setDetailDirty(false));
    setNotice(next ? `${message} Открыта следующая задача.` : `${message} Открыт актуальный список.`);
    navigate(next ? `/staff/operator/requests/${next.id}` : '/staff/operator/requests');
    void queryClient.invalidateQueries({ queryKey: ['operator-collaboration'] });
  }

  return (
    <ResponsiveMasterDetail className="operator-workspace" hasDetail={Boolean(selectedId)} focusKey={selectedId} scrollKey="operator-requests">
      <UnsavedChangesGuard when={detailDirty} />
      <section className="operator-queue" aria-labelledby="collaboration-heading">
        <div className="operator-heading">
          <div>
            <p className="eyebrow">Совместная работа</p>
            <h1 id="collaboration-heading" className="staff-title">Запросы специалистов</h1>
            <p className="staff-copy">Специалист создаёт запрос, когда нужен соисполнитель, передача другому эксперту или пересмотр приоритета. Оператор проверяет причину и принимает решение.</p>
          </div>
          <p className="queue-counts"><span><strong>{pendingRequests.length}</strong> ждут решения</span></p>
        </div>
        {!selectedId ? <ActionReceipt message={notice} /> : null}
        {listQuery.isPending ? <p className="operator-message">Загружаем запросы…</p> : null}
        {listQuery.isError ? <p className="form-error" role="alert">Не удалось загрузить запросы.</p> : null}
        <div className="queue-list" aria-label="Запросы специалистов" data-scroll-region>
          {pendingRequests.map((item) => (
            <button
              className={`queue-row${selectedId === item.id ? ' queue-row--selected' : ''}`}
              key={item.id}
              type="button"
              aria-current={selectedId === item.id ? 'true' : undefined}
              data-request-id={item.id}
              onClick={() => { setNotice(''); navigate(`/staff/operator/requests/${item.id}`); }}
            >
              <span className="queue-row__topline"><strong>{item.typeText}</strong><span>{item.statusText}</span></span>
              <span className="queue-row__category">{item.category}</span>
              <span className="queue-row__meta">{item.applicantTypeText} · {item.priorityText}</span>
            </button>
          ))}
          {!listQuery.isPending && pendingRequests.length === 0 ? <div className="empty-state"><h2>Новых запросов нет</h2><p>Они появятся здесь после действия специалиста. Оператор увидит причину запроса, текущих участников и допустимые варианты решения — без экспертного диалога и внутренних заметок.</p></div> : null}
        </div>
      </section>
      <section className="operator-detail" aria-label="Решение по запросу">
        {selectedId ? <button className="operator-back" type="button" onClick={() => navigate('/staff/operator/requests')}>Вернуться к запросам</button> : null}
        {selectedId ? <ActionReceipt message={notice} /> : null}
        {!selectedId ? (
          <div className="operator-detail__empty">
            <p className="eyebrow">Решение оператора</p>
            <h2>Выберите запрос</h2>
            <p>Проверьте причину, текущих участников и последствия решения. Текст обращения, диалог и заметки здесь намеренно не показываются.</p>
          </div>
        ) : null}
        {selectedId && detailQuery.isPending ? <p className="operator-message">Открываем запрос…</p> : null}
        {selectedId && detailQuery.isError ? <div className="operator-detail__empty"><h2>Запрос уже обновился</h2><p>Вернитесь к актуальному списку запросов и выберите доступное решение.</p><md-filled-button onClick={() => navigate('/staff/operator/requests')}>К актуальным запросам</md-filled-button></div> : null}
        {detailQuery.data ? (
          <CollaborationDecision
            key={detailQuery.data.id}
            detail={detailQuery.data}
            onDone={finish}
            onRefresh={refresh}
            onDirtyChange={setDetailDirty}
          />
        ) : null}
      </section>
    </ResponsiveMasterDetail>
  );
}

function CollaborationDecision({
  detail,
  onDone,
  onRefresh,
  onDirtyChange,
}: {
  detail: Awaited<ReturnType<typeof getCollaborationRequest>>;
  onDone: (message: string) => void;
  onRefresh: () => Promise<void>;
  onDirtyChange: (dirty: boolean) => void;
}) {
  const [expertId, setExpertId] = useState('');
  const [priority, setPriority] = useState(detail.appeal.priority);
  const [keepPrevious, setKeepPrevious] = useState(false);
  const [decisionReason, setDecisionReason] = useState('');
  const dirty = expertId.length > 0 || priority !== detail.appeal.priority || keepPrevious || decisionReason.trim().length > 0;
  useEffect(() => onDirtyChange(dirty), [dirty, onDirtyChange]);
  const approveMutation = useMutation({
    mutationFn: () => approveCollaborationRequest(detail.id, {
      expertId: detail.type === 'PriorityReview' ? undefined : expertId,
      priority: detail.type === 'PriorityReview' ? priority : undefined,
      keepPreviousAsCoExecutor: detail.type === 'Transfer' && keepPrevious,
      decisionReason: decisionReason || undefined,
      expectedVersion: detail.version,
    }),
    onSuccess: () => onDone('Запрос подтверждён.'),
  });
  const rejectMutation = useMutation({
    mutationFn: () => rejectCollaborationRequest(detail.id, decisionReason, detail.version),
    onSuccess: () => onDone('Запрос отклонён.'),
  });
  const pending = detail.status === 'Pending';
  const error = approveMutation.error ?? rejectMutation.error;
  const staleConflict = isOperatorStaleConflict(error);

  async function refreshAfterConflict() {
    await onRefresh();
    approveMutation.reset();
    rejectMutation.reset();
  }

  return (
    <div className="operator-detail__content collaboration-decision">
      <div className="operator-detail__heading">
        <div><p className="eyebrow">{detail.statusText}</p><h2 className="operator-detail__title" data-detail-heading tabIndex={-1}>{detail.typeText}</h2></div>
        <time dateTime={detail.requestedAt}>{formatDate(detail.requestedAt)}</time>
      </div>
      <dl className="appeal-answers">
        <div><dt>Обращение</dt><dd>{detail.appeal.applicantTypeText}, {detail.appeal.category}</dd></div>
        <div><dt>Приоритет</dt><dd>{detail.appeal.priorityText}</dd></div>
        <div><dt>Автор запроса</dt><dd>{detail.requestedBy}</dd></div>
        <div><dt>Причина</dt><dd>{detail.reason}</dd></div>
      </dl>
      <section className="detail-section">
        <h3>Текущие участники</h3>
        <div className="participant-list">
          {detail.participants.map((participant) => (
            <p key={participant.id}><strong>{participant.displayName}</strong><span>{participant.roleText}</span></p>
          ))}
        </div>
      </section>
      {pending ? (
        <section className="operator-actions">
          <p className="request-impact">{requestImpact(detail.type)}</p>
          {detail.type === 'PriorityReview' ? (
            <label className="select-field"><span>Новый приоритет</span><select value={priority} onChange={(event) => setPriority(event.target.value)}><option value="Low">Низкий</option><option value="Standard">Обычный</option><option value="Urgent">Срочный</option></select></label>
          ) : (
            <label className="select-field"><span>{detail.type === 'Transfer' ? 'Новый ответственный' : 'Соисполнитель'}</span><select required value={expertId} onChange={(event) => setExpertId(event.target.value)}><option value="">Выберите специалиста</option>{detail.candidates.filter((candidate) => detail.type === 'CoExecutor'
              ? !detail.participants.some((participant) => participant.expertId === candidate.id)
              : !detail.participants.some((participant) => participant.expertId === candidate.id && participant.role === 'Responsible'))
              .map((candidate) => <option key={candidate.id} value={candidate.id}>{candidate.displayName}</option>)}</select></label>
          )}
          {detail.type === 'Transfer' ? (
            <label className="check-field"><input type="checkbox" checked={keepPrevious} onChange={(event) => setKeepPrevious(event.target.checked)} /><span>Оставить прежнего специалиста соисполнителем</span></label>
          ) : null}
          <label className="text-field"><span>Комментарий к решению</span><textarea maxLength={1000} value={decisionReason} onChange={(event) => setDecisionReason(event.target.value)} /></label>
          <div className="action-row">
            <md-filled-button disabled={(detail.type !== 'PriorityReview' && !expertId) || approveMutation.isPending} onClick={() => approveMutation.mutate()}>Подтвердить</md-filled-button>
            <md-outlined-button disabled={decisionReason.trim().length < 5 || rejectMutation.isPending} onClick={() => rejectMutation.mutate()}>Отклонить</md-outlined-button>
          </div>
          {staleConflict ? <div className="operator-stale" role="alert"><h3>Запрос уже обновил другой оператор</h3><p>Откройте актуальную версию. Выбранный специалист и комментарий останутся на экране для проверки.</p><md-filled-button onClick={() => void refreshAfterConflict()}>Открыть актуальную версию</md-filled-button></div> : error ? <p className="form-error" role="alert">{operatorError(error)}</p> : null}
        </section>
      ) : <p className="operator-message">Решение уже сохранено.</p>}
    </div>
  );
}

function requestImpact(type: 'Transfer' | 'CoExecutor' | 'PriorityReview') {
  if (type === 'Transfer') return 'Новый специалист станет ответственным. Прежний сохранит доступ только при выборе соисполнения.';
  if (type === 'CoExecutor') return 'Специалист получит доступ к рабочему контексту как соисполнитель. Ответственный не изменится.';
  return 'Изменится порядок обработки обращения. Состав участников останется прежним.';
}

function formatDate(value: string) {
  return new Intl.DateTimeFormat('ru-RU', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value));
}
