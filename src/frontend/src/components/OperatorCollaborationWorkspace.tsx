import '@material/web/button/filled-button.js';
import '@material/web/button/outlined-button.js';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import {
  approveCollaborationRequest,
  getCollaborationRequest,
  getCollaborationRequests,
  rejectCollaborationRequest,
} from '../api/operatorCollaboration';
import { operatorError } from '../api/operatorQueue';
import { ResponsiveMasterDetail } from './ux/WorkspacePrimitives';

export function OperatorCollaborationWorkspace() {
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const { appealId: selectedId } = useParams();
  const listQuery = useQuery({
    queryKey: ['operator-collaboration'],
    queryFn: getCollaborationRequests,
    refetchInterval: 30_000,
  });
  const detailQuery = useQuery({
    queryKey: ['operator-collaboration', selectedId],
    queryFn: () => getCollaborationRequest(selectedId!),
    enabled: Boolean(selectedId),
  });

  return (
    <ResponsiveMasterDetail className="operator-workspace" hasDetail={Boolean(selectedId)}>
      <section className="operator-queue" aria-labelledby="collaboration-heading">
        <div className="operator-heading">
          <div>
            <p className="eyebrow">Совместная работа</p>
            <h1 id="collaboration-heading" className="staff-title">Запросы экспертов</h1>
          </div>
          <p className="queue-counts"><span><strong>{listQuery.data?.total ?? 0}</strong> всего</span></p>
        </div>
        <p className="staff-copy">Здесь только тип запроса, отдельная причина и метаданные. Диалог и внутренние заметки оператору недоступны.</p>
        {listQuery.isPending ? <p className="operator-message">Загружаем запросы…</p> : null}
        {listQuery.isError ? <p className="form-error" role="alert">Не удалось загрузить запросы.</p> : null}
        <div className="queue-list" aria-label="Запросы экспертов">
          {listQuery.data?.items.map((item) => (
            <button
              className={`queue-row${selectedId === item.id ? ' queue-row--selected' : ''}`}
              key={item.id}
              type="button"
              onClick={() => navigate(`/staff/operator/requests/${item.id}`)}
            >
              <span className="queue-row__topline"><strong>{item.typeText}</strong><span>{item.statusText}</span></span>
              <span className="queue-row__category">{item.category}</span>
              <span className="queue-row__meta">{item.applicantTypeText} · {item.priorityText}</span>
            </button>
          ))}
          {listQuery.data?.items.length === 0 ? <p className="operator-message">Запросов пока нет.</p> : null}
        </div>
      </section>
      <section className="operator-detail" aria-label="Решение по запросу">
        {selectedId ? <button className="operator-back" type="button" onClick={() => navigate('/staff/operator/requests')}>Вернуться к запросам</button> : null}
        {!selectedId ? (
          <div className="operator-detail__empty">
            <p className="eyebrow">Решение оператора</p>
            <h2>Выберите запрос</h2>
            <p>Проверьте профиль специалиста и подтвердите или отклоните запрос.</p>
          </div>
        ) : null}
        {selectedId && detailQuery.isPending ? <p className="operator-message">Открываем запрос…</p> : null}
        {detailQuery.data ? (
          <CollaborationDecision
            detail={detailQuery.data}
            onDone={async () => {
              await queryClient.invalidateQueries({ queryKey: ['operator-collaboration'] });
              await queryClient.invalidateQueries({ queryKey: ['operator-collaboration', selectedId] });
            }}
          />
        ) : null}
      </section>
    </ResponsiveMasterDetail>
  );
}

function CollaborationDecision({
  detail,
  onDone,
}: {
  detail: Awaited<ReturnType<typeof getCollaborationRequest>>;
  onDone: () => Promise<void>;
}) {
  const [expertId, setExpertId] = useState('');
  const [priority, setPriority] = useState(detail.appeal.priority);
  const [keepPrevious, setKeepPrevious] = useState(false);
  const [decisionReason, setDecisionReason] = useState('');
  useEffect(() => {
    setExpertId('');
    setPriority(detail.appeal.priority);
    setKeepPrevious(false);
    setDecisionReason('');
  }, [detail.id, detail.appeal.priority]);
  const approveMutation = useMutation({
    mutationFn: () => approveCollaborationRequest(detail.id, {
      expertId: detail.type === 'PriorityReview' ? undefined : expertId,
      priority: detail.type === 'PriorityReview' ? priority : undefined,
      keepPreviousAsCoExecutor: detail.type === 'Transfer' && keepPrevious,
      decisionReason: decisionReason || undefined,
      expectedVersion: detail.version,
    }),
    onSuccess: onDone,
  });
  const rejectMutation = useMutation({
    mutationFn: () => rejectCollaborationRequest(detail.id, decisionReason, detail.version),
    onSuccess: onDone,
  });
  const pending = detail.status === 'Pending';
  const error = approveMutation.error ?? rejectMutation.error;

  return (
    <div className="operator-detail__content collaboration-decision">
      <div className="operator-detail__heading">
        <div><p className="eyebrow">{detail.statusText}</p><h2 className="operator-detail__title">{detail.typeText}</h2></div>
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
          {error ? <p className="form-error" role="alert">{operatorError(error)}</p> : null}
        </section>
      ) : <p className="operator-message">Решение уже сохранено.</p>}
    </div>
  );
}

function formatDate(value: string) {
  return new Intl.DateTimeFormat('ru-RU', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value));
}
