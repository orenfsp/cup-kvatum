import '@material/web/button/filled-button.js';
import '@material/web/button/outlined-button.js';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import {
  closeReturnedAppeal,
  getLifecycleWork,
  getReturnDetail,
  reassignReturnedAppeal,
  resolveComplaint,
} from '../api/operatorLifecycle';
import { operatorError } from '../api/operatorQueue';
import { ResponsiveMasterDetail } from './ux/WorkspacePrimitives';

export function OperatorLifecycleWorkspace() {
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const { appealId: selectedId } = useParams();
  const workQuery = useQuery({ queryKey: ['operator-lifecycle'], queryFn: getLifecycleWork, refetchInterval: 30_000 });
  const detailQuery = useQuery({
    queryKey: ['operator-return', selectedId],
    queryFn: () => getReturnDetail(selectedId!),
    enabled: Boolean(selectedId),
  });
  const resolveMutation = useMutation({
    mutationFn: resolveComplaint,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['operator-lifecycle'] }),
  });

  async function done() {
    navigate('/staff/operator/returns');
    await queryClient.invalidateQueries({ queryKey: ['operator-lifecycle'] });
  }

  return (
    <ResponsiveMasterDetail className="operator-workspace" hasDetail={Boolean(selectedId)}>
      <section className="operator-queue" aria-labelledby="returns-heading">
        <div className="operator-heading">
          <div><p className="eyebrow">Повторная помощь</p><h1 id="returns-heading" className="staff-title">Возвраты и жалобы</h1></div>
          <p className="queue-counts"><span><strong>{workQuery.data?.returns.length ?? 0}</strong> возвратов</span><span><strong>{workQuery.data?.complaints.length ?? 0}</strong> жалоб</span></p>
        </div>
        {workQuery.isPending ? <p className="operator-message">Загружаем задачи…</p> : null}
        <div className="queue-list" aria-label="Возвращенные обращения">
          {workQuery.data?.returns.map((item) => (
            <button className={`queue-row${selectedId === item.id ? ' queue-row--selected' : ''}`} key={item.id} type="button" onClick={() => navigate(`/staff/operator/returns/${item.id}`)}>
              <span className="queue-row__topline"><strong>{item.applicantTypeText}</strong><span>возврат {item.returnCount} из 2</span></span>
              <span className="queue-row__category">{item.category}</span>
              <span className="queue-row__meta">{item.requiresFinalDecision ? 'Нужно окончательное решение' : 'Можно назначить повторно'}</span>
            </button>
          ))}
        </div>
        {workQuery.data?.complaints.length ? (
          <section className="lifecycle-list" aria-labelledby="complaints-heading">
            <h2 id="complaints-heading">Жалобы на специалиста</h2>
            {workQuery.data.complaints.map((complaint) => (
              <article key={complaint.id}>
                <strong>{complaint.applicantTypeText}, {complaint.category}</strong>
                <p>{complaint.body}</p>
                <md-outlined-button disabled={resolveMutation.isPending} onClick={() => resolveMutation.mutate(complaint.id)}>Отметить рассмотренной</md-outlined-button>
              </article>
            ))}
          </section>
        ) : null}
        {workQuery.data?.crisisReviews.length ? (
          <section className="lifecycle-list" aria-labelledby="crisis-reviews-heading"><h2 id="crisis-reviews-heading">Ручная проверка кризисных обращений</h2>{workQuery.data.crisisReviews.map((item) => <p key={item.id}>Не закрывать автоматически · поступило {formatDate(item.createdAt)}</p>)}</section>
        ) : null}
      </section>
      <section className="operator-detail" aria-label="Карточка возврата">
        {selectedId ? <button className="operator-back" type="button" onClick={() => navigate('/staff/operator/returns')}>Вернуться к возвратам</button> : null}
        {!selectedId ? <div className="operator-detail__empty"><p className="eyebrow">Повторное решение</p><h2>Выберите возврат</h2><p>Назначьте специалиста повторно или завершите обращение с бережным объяснением.</p></div> : null}
        {selectedId && detailQuery.isPending ? <p className="operator-message">Открываем возврат…</p> : null}
        {detailQuery.data ? <ReturnDecision detail={detailQuery.data} onDone={done} /> : null}
      </section>
    </ResponsiveMasterDetail>
  );
}

function ReturnDecision({ detail, onDone }: { detail: Awaited<ReturnType<typeof getReturnDetail>>; onDone: () => Promise<void> }) {
  const [expertId, setExpertId] = useState('');
  const [explanation, setExplanation] = useState('');
  const reassignMutation = useMutation({
    mutationFn: () => reassignReturnedAppeal(detail.id, expertId, detail.version), onSuccess: onDone,
  });
  const closeMutation = useMutation({
    mutationFn: () => closeReturnedAppeal(detail.id, explanation, detail.version), onSuccess: onDone,
  });
  const error = reassignMutation.error ?? closeMutation.error;
  return (
    <div className="operator-detail__content collaboration-decision">
      <div className="operator-detail__heading"><div><p className="eyebrow">Возврат {detail.returnCount} из 2</p><h2 className="operator-detail__title">{detail.applicantTypeText}</h2></div><span>{detail.category}</span></div>
      <section className="detail-section"><h3>Что не помогло</h3><div className="workflow-history">{detail.returns.map((item) => <p key={item.id}><strong>{item.reasonText}</strong><span>{item.details || formatDate(item.createdAt)}</span></p>)}</div></section>
      {!detail.requiresFinalDecision ? (
        <section className="operator-actions"><label className="select-field"><span>Ответственный специалист</span><select value={expertId} onChange={(event) => setExpertId(event.target.value)}><option value="">Выберите специалиста</option>{detail.candidates.map((candidate) => <option key={candidate.id} value={candidate.id}>{candidate.displayName}</option>)}</select></label><md-filled-button disabled={!expertId || reassignMutation.isPending} onClick={() => reassignMutation.mutate()}>Назначить повторно</md-filled-button></section>
      ) : <p className="operator-notice">После второго возврата повторное назначение недоступно. Нужно окончательное решение.</p>}
      <section className="operator-actions"><label className="text-field"><span>Бережное объяснение заявителю</span><textarea minLength={10} maxLength={4000} value={explanation} onChange={(event) => setExplanation(event.target.value)} /></label><md-outlined-button disabled={explanation.trim().length < 10 || closeMutation.isPending} onClick={() => closeMutation.mutate()}>Завершить обращение</md-outlined-button></section>
      {error ? <p className="form-error" role="alert">{operatorError(error)}</p> : null}
    </div>
  );
}

function formatDate(value: string) {
  return new Intl.DateTimeFormat('ru-RU', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value));
}
