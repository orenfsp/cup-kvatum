import '@material/web/button/filled-button.js';
import '@material/web/button/outlined-button.js';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useEffect, useState } from 'react';
import { flushSync } from 'react-dom';
import { useNavigate, useParams } from 'react-router-dom';
import {
  closeReturnedAppeal,
  getLifecycleWork,
  getReturnDetail,
  reassignReturnedAppeal,
  resolveComplaint,
} from '../api/operatorLifecycle';
import { isOperatorStaleConflict, operatorError } from '../api/operatorQueue';
import { ActionReceipt, ResponsiveMasterDetail, UnsavedChangesGuard } from './ux/WorkspacePrimitives';

export function OperatorLifecycleWorkspace({ view }: { view: 'returns' | 'complaints' }) {
  return view === 'complaints' ? <ComplaintWorkspace /> : <ReturnsWorkspace />;
}

function ReturnsWorkspace() {
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const { appealId: selectedId } = useParams();
  const [notice, setNotice] = useState('');
  const [detailDirty, setDetailDirty] = useState(false);
  const workQuery = useQuery({ queryKey: ['operator-lifecycle'], queryFn: getLifecycleWork, refetchInterval: 30_000 });
  const detailQuery = useQuery({
    queryKey: ['operator-return', selectedId],
    queryFn: () => getReturnDetail(selectedId!),
    enabled: Boolean(selectedId),
    retry: false,
  });
  useEffect(() => {
    if (!selectedId) setDetailDirty(false);
  }, [selectedId]);

  async function refresh() {
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: ['operator-lifecycle'] }),
      queryClient.invalidateQueries({ queryKey: ['operator-return', selectedId] }),
    ]);
  }

  function done(message: string) {
    const returns = workQuery.data?.returns ?? [];
    const currentIndex = returns.findIndex((item) => item.id === selectedId);
    const next = returns[currentIndex + 1] ?? returns[currentIndex - 1];
    flushSync(() => setDetailDirty(false));
    setNotice(message);
    navigate(next ? `/staff/operator/returns/${next.id}` : '/staff/operator/returns');
    void queryClient.invalidateQueries({ queryKey: ['operator-lifecycle'] });
  }

  return (
    <ResponsiveMasterDetail className="operator-workspace" hasDetail={Boolean(selectedId)} focusKey={selectedId} scrollKey="operator-returns">
      <UnsavedChangesGuard when={detailDirty} />
      <section className="operator-queue" aria-labelledby="returns-heading">
        <div className="operator-heading">
          <div><p className="eyebrow">Повторная помощь</p><h1 id="returns-heading" className="staff-title">Возвраты</h1><p className="staff-copy">Заявитель сообщил, что рекомендация не помогла. Подберите другого специалиста или завершите цикл с объяснением.</p></div>
          <p className="queue-counts"><span><strong>{workQuery.data?.returns.length ?? 0}</strong> ждут решения</span></p>
        </div>
        {!selectedId ? <ActionReceipt message={notice} /> : null}
        {workQuery.isPending ? <p className="operator-message">Загружаем задачи…</p> : null}
        {workQuery.isError ? <p className="form-error" role="alert">Не удалось загрузить возвраты. Проверьте соединение и попробуйте ещё раз.</p> : null}
        <div className="queue-list" aria-label="Возвращенные обращения" data-scroll-region>
          {workQuery.data?.returns.map((item) => (
            <button className={`queue-row${selectedId === item.id ? ' queue-row--selected' : ''}`} key={item.id} type="button" aria-current={selectedId === item.id ? 'true' : undefined} data-appeal-id={item.id} onClick={() => { setNotice(''); navigate(`/staff/operator/returns/${item.id}`); }}>
              <span className="queue-row__topline"><strong>{item.requiresFinalDecision ? 'Принять итоговое решение' : 'Подобрать помощь повторно'}</strong><span>{formatWaiting(item.returnedAt)}</span></span>
              <span className="queue-row__category">{item.category}</span>
              <span className="queue-row__meta">{item.applicantTypeText} · возврат {item.returnCount} из 2</span>
            </button>
          ))}
        </div>
        {workQuery.data?.crisisReviews.length ? (
          <section className="lifecycle-list" aria-labelledby="crisis-reviews-heading"><h2 id="crisis-reviews-heading">Ручная проверка кризисных обращений</h2>{workQuery.data.crisisReviews.map((item) => <p key={item.id}>Не закрывать автоматически · поступило {formatDate(item.createdAt)}</p>)}</section>
        ) : null}
      </section>
      <section className="operator-detail" aria-label="Карточка возврата">
        {selectedId ? <button className="operator-back" type="button" onClick={() => navigate('/staff/operator/returns')}>Вернуться к возвратам</button> : null}
        {selectedId ? <ActionReceipt message={notice} /> : null}
        {!selectedId ? <div className="operator-detail__empty"><p className="eyebrow">Повторное решение</p><h2>Выберите возврат</h2><p>Назначьте специалиста повторно или завершите обращение с бережным объяснением.</p></div> : null}
        {selectedId && detailQuery.isPending ? <p className="operator-message">Открываем возврат…</p> : null}
        {selectedId && detailQuery.isError ? <div className="operator-detail__empty"><h2>Возврат уже обновился</h2><p>Откройте актуальный список и выберите задачу, которая ещё требует решения.</p><md-filled-button onClick={() => navigate('/staff/operator/returns')}>К актуальным возвратам</md-filled-button></div> : null}
        {detailQuery.data ? <ReturnDecision key={detailQuery.data.id} detail={detailQuery.data} onDone={done} onRefresh={refresh} onDirtyChange={setDetailDirty} /> : null}
      </section>
    </ResponsiveMasterDetail>
  );
}

function ComplaintWorkspace() {
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const { complaintId } = useParams();
  const [notice, setNotice] = useState('');
  const workQuery = useQuery({ queryKey: ['operator-lifecycle'], queryFn: getLifecycleWork, refetchInterval: 30_000 });
  const complaints = workQuery.data?.complaints ?? [];
  const selected = complaints.find((item) => item.id === complaintId);

  function finish() {
    const currentIndex = complaints.findIndex((item) => item.id === complaintId);
    const next = complaints[currentIndex + 1] ?? complaints[currentIndex - 1];
    setNotice('Жалоба зафиксирована как обработанная и убрана из входящих.');
    navigate(next ? `/staff/operator/complaints/${next.id}` : '/staff/operator/complaints');
    void queryClient.invalidateQueries({ queryKey: ['operator-lifecycle'] });
  }

  return (
    <ResponsiveMasterDetail className="operator-workspace" hasDetail={Boolean(complaintId)} focusKey={complaintId} scrollKey="operator-complaints">
      <section className="operator-queue" aria-labelledby="complaints-heading">
        <div className="operator-heading"><div><p className="eyebrow">Контроль качества помощи</p><h1 id="complaints-heading" className="staff-title">Жалобы</h1><p className="staff-copy">Здесь только сообщения заявителей о работе специалиста. Жалобы не видны специалисту и не попадают в общий диалог.</p></div><p className="queue-counts"><span><strong>{complaints.length}</strong> ждут обработки</span></p></div>
        {!complaintId ? <ActionReceipt message={notice} /> : null}
        {workQuery.isPending ? <p className="operator-message">Загружаем жалобы…</p> : null}
        {workQuery.isError ? <p className="form-error" role="alert">Не удалось загрузить жалобы. Повторите попытку.</p> : null}
        <div className="queue-list" aria-label="Жалобы на специалистов" data-scroll-region>{complaints.map((item) => <button className={`queue-row${complaintId === item.id ? ' queue-row--selected' : ''}`} key={item.id} type="button" aria-current={complaintId === item.id ? 'true' : undefined} data-complaint-id={item.id} onClick={() => { setNotice(''); navigate(`/staff/operator/complaints/${item.id}`); }}><span className="queue-row__topline"><strong>Проверить жалобу</strong><span>{formatWaiting(item.createdAt)}</span></span><span className="queue-row__category">{item.applicantTypeText} · {item.category}</span><span className="queue-row__meta">Отдельно от возврата обращения</span></button>)}</div>
        {!workQuery.isPending && complaints.length === 0 ? <div className="empty-state"><h2>Новых жалоб нет</h2><p>Если заявитель пожалуется на работу специалиста, сообщение появится здесь отдельной задачей.</p></div> : null}
      </section>
      <section className="operator-detail" aria-label="Карточка жалобы">
        {complaintId ? <button className="operator-back" type="button" onClick={() => navigate('/staff/operator/complaints')}>Вернуться к жалобам</button> : null}
        {complaintId ? <ActionReceipt message={notice} /> : null}
        {!complaintId ? <div className="operator-detail__empty"><p className="eyebrow">Отдельная рабочая очередь</p><h2>Выберите жалобу</h2><p>Прочитайте сообщение заявителя и зафиксируйте, что оно принято в работу.</p></div> : null}
        {complaintId && workQuery.isPending ? <p className="operator-message">Открываем жалобу…</p> : null}
        {complaintId && !workQuery.isPending && !selected ? <div className="operator-detail__empty"><h2>Жалоба уже обработана</h2><p>Вернитесь к актуальному списку.</p><md-filled-button onClick={() => navigate('/staff/operator/complaints')}>К актуальным жалобам</md-filled-button></div> : null}
        {selected ? <ComplaintDecision key={selected.id} complaint={selected} onDone={finish} /> : null}
      </section>
    </ResponsiveMasterDetail>
  );
}

function ComplaintDecision({ complaint, onDone }: { complaint: Awaited<ReturnType<typeof getLifecycleWork>>['complaints'][number]; onDone: () => void }) {
  const [confirming, setConfirming] = useState(false);
  const resolveMutation = useMutation({ mutationFn: () => resolveComplaint(complaint.id), onSuccess: onDone });
  return <div className="operator-detail__content complaint-detail"><header className="operator-detail__heading"><div><p className="eyebrow">Жалоба на работу специалиста</p><h2 data-detail-heading tabIndex={-1}>Проверьте сообщение заявителя</h2></div><time dateTime={complaint.createdAt}>{formatDate(complaint.createdAt)}</time></header><dl className="operator-case-summary"><div><dt>Заявитель</dt><dd>{complaint.applicantTypeText}</dd></div><div><dt>Категория</dt><dd>{complaint.category}</dd></div></dl><section className="detail-section"><h3>Текст жалобы</h3><p className="complaint-body">{complaint.body}</p></section><section className="detail-section"><h3>Что означает «обработать»</h3><p>Система запишет, что оператор прочитал жалобу, и уберёт её из входящих. Это не удаляет обращение, не показывает жалобу специалисту и не отправляет данные во внешние сервисы.</p></section>{confirming ? <section className="decision-panel" aria-labelledby="complaint-confirm-heading"><h3 id="complaint-confirm-heading">Завершить обработку?</h3><p>Убедитесь, что содержание прочитано и дальнейшая внутренняя реакция понятна.</p><div className="action-row"><md-filled-button disabled={resolveMutation.isPending} onClick={() => resolveMutation.mutate()}>{resolveMutation.isPending ? 'Сохраняем…' : 'Подтвердить обработку'}</md-filled-button><md-outlined-button disabled={resolveMutation.isPending} onClick={() => setConfirming(false)}>Отменить</md-outlined-button></div></section> : <md-filled-button onClick={() => setConfirming(true)}>Обработать жалобу</md-filled-button>}{resolveMutation.isError ? <p className="form-error" role="alert">Не удалось сохранить решение. Обновите список и попробуйте снова.</p> : null}</div>;
}

function ReturnDecision({ detail, onDone, onRefresh, onDirtyChange }: { detail: Awaited<ReturnType<typeof getReturnDetail>>; onDone: (message: string) => void; onRefresh: () => Promise<void>; onDirtyChange: (dirty: boolean) => void }) {
  const [expertId, setExpertId] = useState('');
  const [explanation, setExplanation] = useState('');
  const dirty = expertId.length > 0 || explanation.trim().length > 0;
  useEffect(() => onDirtyChange(dirty), [dirty, onDirtyChange]);
  const reassignMutation = useMutation({
    mutationFn: () => reassignReturnedAppeal(detail.id, expertId, detail.version), onSuccess: () => onDone('Обращение повторно назначено. Открыта следующая задача.'),
  });
  const closeMutation = useMutation({
    mutationFn: () => closeReturnedAppeal(detail.id, explanation, detail.version), onSuccess: () => onDone('Итоговое объяснение отправлено. Открыта следующая задача.'),
  });
  const error = reassignMutation.error ?? closeMutation.error;
  const staleConflict = isOperatorStaleConflict(error);

  async function refreshAfterConflict() {
    await onRefresh();
    reassignMutation.reset();
    closeMutation.reset();
  }
  return (
    <div className="operator-detail__content collaboration-decision">
      <div className="operator-detail__heading"><div><p className="eyebrow">Возврат {detail.returnCount} из 2</p><h2 className="operator-detail__title" data-detail-heading tabIndex={-1}>{detail.requiresFinalDecision ? 'Нужно итоговое решение' : 'Подберите следующий шаг'}</h2></div><span>{detail.category} · {detail.applicantTypeText}</span></div>
      <section className="detail-section"><h3>Что не помогло</h3><div className="workflow-history">{detail.returns.map((item) => <p key={item.id}><strong>{item.reasonText}</strong><span>{item.details || formatDate(item.createdAt)}</span></p>)}</div></section>
      <section className="detail-section"><h3>Предыдущие назначения</h3>{detail.operatorDecisions.length ? <div className="workflow-history">{detail.operatorDecisions.map((item) => <p key={item.id}><strong>{item.decisionText}</strong><span>{formatDate(item.occurredAt)}</span></p>)}</div> : <p>Служебных решений о назначении пока нет.</p>}</section>
      {!detail.requiresFinalDecision ? (
        <section className="operator-actions"><label className="select-field"><span>Ответственный специалист</span><select value={expertId} onChange={(event) => setExpertId(event.target.value)}><option value="">Выберите специалиста</option>{detail.candidates.map((candidate) => <option key={candidate.id} value={candidate.id}>{candidate.displayName}</option>)}</select></label><md-filled-button disabled={!expertId || reassignMutation.isPending} onClick={() => reassignMutation.mutate()}>Назначить повторно</md-filled-button></section>
      ) : <p className="operator-notice">После второго возврата повторное назначение недоступно. Нужно окончательное решение.</p>}
      <section className="operator-actions"><p>{detail.requiresFinalDecision ? 'Повторное назначение больше недоступно. Напишите спокойное объяснение следующего безопасного шага.' : 'Если повторное назначение не подходит, обращение можно завершить понятным объяснением.'}</p><label className="text-field"><span>Бережное объяснение заявителю</span><textarea minLength={10} maxLength={4000} value={explanation} onChange={(event) => setExplanation(event.target.value)} /></label>{detail.requiresFinalDecision ? <md-filled-button disabled={explanation.trim().length < 10 || closeMutation.isPending} onClick={() => closeMutation.mutate()}>Отправить объяснение и завершить</md-filled-button> : <md-outlined-button disabled={explanation.trim().length < 10 || closeMutation.isPending} onClick={() => closeMutation.mutate()}>Завершить с объяснением</md-outlined-button>}</section>
      {staleConflict ? <div className="operator-stale" role="alert"><h3>Возврат уже обновил другой оператор</h3><p>Откройте актуальную версию. Выбранный специалист и объяснение останутся на экране для проверки.</p><md-filled-button onClick={() => void refreshAfterConflict()}>Открыть актуальную версию</md-filled-button></div> : error ? <p className="form-error" role="alert">{operatorError(error)}</p> : null}
    </div>
  );
}

function formatDate(value: string) {
  return new Intl.DateTimeFormat('ru-RU', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value));
}

function formatWaiting(value: string) {
  const minutes = Math.max(0, Math.floor((Date.now() - new Date(value).getTime()) / 60_000));
  if (minutes < 1) return 'меньше минуты';
  if (minutes < 60) return `${minutes} мин`;
  const hours = Math.floor(minutes / 60);
  return `${hours} ч`;
}
