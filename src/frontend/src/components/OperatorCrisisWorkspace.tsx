import '@material/web/button/filled-button.js';
import '@material/web/button/outlined-button.js';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useEffect, useState } from 'react';
import { useNavigate, useParams, useSearchParams } from 'react-router-dom';
import {
  confirmCrisisUrgent,
  dismissCrisisSignal,
  getCrisisQueue,
  readCrisisContact,
  type CrisisQueueItem,
} from '../api/operatorCrisis';
import {
  acquireNextOperatorWork,
  acquireOperatorWork,
  getOperatorWorkLeaseId,
  heartbeatOperatorWork,
  isOperatorLeaseConflict,
  isOperatorStaleConflict,
  operatorError,
  releaseOperatorWork,
} from '../api/operatorQueue';
import { ActionReceipt, ResponsiveMasterDetail } from './ux/WorkspacePrimitives';

const CRISIS_PAGE_SIZE = 30;

export function OperatorCrisisWorkspace() {
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const { appealId: selectedId } = useParams();
  const [leaseId] = useState(getOperatorWorkLeaseId);
  const [searchParams, setSearchParams] = useSearchParams();
  const searchTerm = searchParams.get('q') ?? '';
  const crisisSearch = searchParams.toString();
  const [visibleContacts, setVisibleContacts] = useState<Record<string, string>>({});
  const [notice, setNotice] = useState('');
  const [visibleCount, setVisibleCount] = useState(CRISIS_PAGE_SIZE);
  const queueQuery = useQuery({
    queryKey: ['operator-crisis'],
    queryFn: () => getCrisisQueue(leaseId),
    refetchInterval: 10_000,
  });
  const selected = queueQuery.data?.items.find((item) => item.id === selectedId);
  const leaseQuery = useQuery({
    queryKey: ['operator-work-lease', selectedId],
    queryFn: async () => {
      const lease = await acquireOperatorWork(selectedId!, leaseId);
      void queryClient.invalidateQueries({ queryKey: ['operator-crisis'] });
      return lease;
    },
    enabled: Boolean(selectedId && selected),
    retry: false,
  });

  useEffect(() => {
    if (!selectedId) return undefined;
    const appealId = selectedId;
    const heartbeat = window.setInterval(() => {
      void heartbeatOperatorWork(appealId, leaseId).catch(() => {
        void queryClient.invalidateQueries({ queryKey: ['operator-crisis'] });
      });
    }, 30_000);
    return () => {
      window.clearInterval(heartbeat);
    };
  }, [leaseId, queryClient, selectedId]);
  const filteredItems = queueQuery.data?.items.filter((item) => {
    const query = searchTerm.trim().toLowerCase();
    return !query || [item.caseNumber, item.riskTypeText, item.category]
      .some((value) => value.toLowerCase().includes(query));
  }) ?? [];
  const selectedListIndex = filteredItems.findIndex((item) => item.id === selectedId);
  const displayedItems = filteredItems.slice(
    0,
    Math.max(visibleCount, selectedListIndex >= 0 ? selectedListIndex + 1 : 0),
  ) ?? [];
  const takeNextMutation = useMutation({
    mutationFn: () => acquireNextOperatorWork(leaseId, { scope: 'crisis' }),
    onSuccess: (lease) => {
      setNotice('');
      navigate({ pathname: `/staff/operator/urgent/${lease.appealId}`, search: crisisSearch });
      void queryClient.invalidateQueries({ queryKey: ['operator-crisis'] });
    },
    onError: () => setNotice(''),
  });
  const urgentMutation = useMutation({
    mutationFn: ({ appealId, version }: { appealId: string; version: number }) =>
      confirmCrisisUrgent(appealId, version, leaseId),
    onSuccess: async () => {
      setNotice('Срочность подтверждена. Теперь назначьте ответственного специалиста в основной очереди.');
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['operator-crisis'] }),
        queryClient.invalidateQueries({ queryKey: ['operator-queue'] }),
      ]);
    },
  });
  const dismissMutation = useMutation({
    mutationFn: ({ appealId, version, reason }: { appealId: string; version: number; reason: string }) =>
      dismissCrisisSignal(appealId, version, reason, leaseId),
    onSuccess: async () => {
      setNotice('Проверка завершена: срочность не подтверждена. Обращение осталось в обычной очереди.');
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['operator-crisis'] }),
        queryClient.invalidateQueries({ queryKey: ['operator-queue'] }),
      ]);
      try {
        const next = await acquireNextOperatorWork(leaseId, { scope: 'crisis' });
        navigate({ pathname: `/staff/operator/urgent/${next.appealId}`, search: crisisSearch });
      } catch {
        navigate({ pathname: '/staff/operator/urgent', search: crisisSearch });
      }
    },
  });
  const contactMutation = useMutation({
    mutationFn: (appealId: string) => readCrisisContact(appealId, leaseId),
    onSuccess: (result, appealId) => {
      setVisibleContacts((current) => ({ ...current, [appealId]: result.contact }));
    },
  });

  function selectCrisis(appealId: string | null) {
    if (selectedId && selectedId !== appealId) {
      void releaseOperatorWork(selectedId, leaseId).then(() => {
        void queryClient.invalidateQueries({ queryKey: ['operator-crisis'] });
      }).catch(() => undefined);
    }
    navigate({
      pathname: appealId ? `/staff/operator/urgent/${appealId}` : '/staff/operator/urgent',
      search: crisisSearch,
    });
  }

  return (
    <ResponsiveMasterDetail className="operator-workspace crisis-workspace" hasDetail={Boolean(selectedId)} focusKey={selectedId} scrollKey="operator-crisis">
      <section className="operator-queue" aria-labelledby="crisis-queue-heading">
        <header className="operator-heading">
          <div><p className="eyebrow">Кабинет оператора</p><h1 id="crisis-queue-heading" className="staff-title">Срочная помощь</h1><p className="staff-copy">Сначала подтвердите срочность, затем направьте обращение в назначение.</p></div>
          {!selectedId ? <md-filled-button disabled={takeNextMutation.isPending} onClick={() => takeNextMutation.mutate()}>{takeNextMutation.isPending ? 'Закрепляем…' : 'Взять следующий срочный сигнал'}</md-filled-button> : null}
          {queueQuery.data ? <div className="queue-counts" aria-label="Сводка кризисной очереди"><span><strong>{queueQuery.data.total}</strong> требуют внимания</span><span><strong>{queueQuery.data.unconfirmedCount}</strong> без подтверждения</span></div> : null}
        </header>
        {!selectedId ? <ActionReceipt message={notice} title="Проверка сохранена" /> : null}
        {!selectedId && takeNextMutation.isError ? <p className="form-error" role="alert">{operatorError(takeNextMutation.error)}</p> : null}
        {queueQuery.isPending ? <p className="staff-copy">Загружаем кризисную очередь…</p> : null}
        {queueQuery.isError ? <p className="form-error" role="alert">Не удалось загрузить очередь.</p> : null}
        <label className="text-field queue-search"><span>Найти срочный сигнал</span><input type="search" value={searchTerm} onChange={(event) => { const next = new URLSearchParams(searchParams); if (event.target.value) next.set('q', event.target.value); else next.delete('q'); setSearchParams(next, { replace: true }); setVisibleCount(CRISIS_PAGE_SIZE); }} placeholder="Номер обращения, риск или категория" /></label>
        <div className="queue-list" aria-label="Кризисные обращения" data-scroll-region>
          {displayedItems.map((appeal) => (
            <button className={`queue-row${selectedId === appeal.id ? ' queue-row--selected' : ''}${appeal.workState === 'Busy' ? ' queue-row--busy' : ''}`} key={appeal.id} type="button" disabled={appeal.workState === 'Busy'} aria-current={selectedId === appeal.id ? 'true' : undefined} data-appeal-id={appeal.id} onClick={() => { setNotice(''); selectCrisis(appeal.id); }}>
              <span className="queue-row__topline"><strong>{appeal.priority === 'Urgent' ? 'Назначить срочную помощь' : 'Подтвердить срочность'}</strong><span>{formatWaiting(appeal.waitingMinutes)}</span></span>
              <span className="queue-row__category">{appeal.applicantTypeText} · {appeal.category}</span>
              <span className="queue-row__meta">{appeal.caseNumber}</span>
              <span className="queue-row__meta">{appeal.riskTypeText}</span>
              <span className="queue-row__meta">{appeal.hasContact ? 'Есть добровольный контакт' : 'Контакт не оставлен'}</span>
              {appeal.workState === 'Mine' ? <span className="queue-row__work-state">Закреплено за вами в этом окне</span> : null}
              {appeal.workState === 'Busy' ? <span className="queue-row__work-state">Сейчас проверяет другой оператор</span> : null}
            </button>
          ))}
          {filteredItems.length ? (
            <div className="queue-list__progress">
              <span>Показано {displayedItems.length} из {filteredItems.length}</span>
              {displayedItems.length < filteredItems.length ? (
                <md-outlined-button onClick={() => setVisibleCount((count) => count + CRISIS_PAGE_SIZE)}>
                  Показать ещё
                </md-outlined-button>
              ) : null}
            </div>
          ) : null}
        </div>
        {queueQuery.data?.items.length === 0 ? <div className="empty-state"><h2>Срочных задач сейчас нет</h2><p>Очередь обновляется автоматически.</p></div> : null}
        {Boolean(queueQuery.data?.items.length) && filteredItems.length === 0 ? <div className="empty-state"><h2>Ничего не найдено</h2><p>Проверьте трек-номер или измените запрос.</p></div> : null}
      </section>

      <section className="operator-detail" aria-label="Проверка срочности">
        {selectedId ? <button className="operator-back" type="button" onClick={() => selectCrisis(null)}>Вернуться к срочным задачам</button> : null}
        {selectedId ? <ActionReceipt message={notice} title="Проверка сохранена" /> : null}
        {!selectedId ? <div className="operator-detail__empty"><p className="eyebrow">Проверка риска</p><h2>Выберите обращение</h2><p>Вы увидите только безопасные признаки, контакт и следующий допустимый шаг.</p></div> : null}
        {selectedId && queueQuery.isPending ? <p>Открываем задачу…</p> : null}
        {selectedId && !queueQuery.isPending && !selected ? <div className="operator-detail__empty"><h2>Обращение уже обновилось</h2><p>Откройте актуальную кризисную очередь.</p><md-filled-button onClick={() => navigate('/staff/operator/urgent')}>К актуальной очереди</md-filled-button></div> : null}
        {selected && leaseQuery.isPending ? <p>Закрепляем задачу за вами…</p> : null}
        {selected && leaseQuery.isError ? <div className="operator-detail__empty"><h2>{isOperatorLeaseConflict(leaseQuery.error) ? 'Сигнал уже проверяет другой оператор' : 'Не удалось открыть задачу'}</h2><p>Вернитесь к очереди и возьмите следующий свободный сигнал.</p><md-filled-button onClick={() => selectCrisis(null)}>Вернуться к срочным задачам</md-filled-button></div> : null}
        {selected && leaseQuery.isSuccess ? (
          <CrisisDetail
            appeal={selected}
            contact={visibleContacts[selected.id]}
            contactPending={contactMutation.isPending}
            urgentPending={urgentMutation.isPending}
            dismissPending={dismissMutation.isPending}
            onConfirmUrgent={() => urgentMutation.mutate({ appealId: selected.id, version: selected.version })}
            onDismiss={(reason) => dismissMutation.mutate({ appealId: selected.id, version: selected.version, reason })}
            onReadContact={() => contactMutation.mutate(selected.id)}
            onOpenInQueue={() => navigate(`/staff/operator/queue/${selected.id}`)}
            onLeave={() => selectCrisis(null)}
            onRefresh={() => {
              urgentMutation.reset();
              contactMutation.reset();
              void queryClient.invalidateQueries({ queryKey: ['operator-crisis'] });
            }}
            error={urgentMutation.error ?? dismissMutation.error ?? contactMutation.error}
          />
        ) : null}
      </section>
    </ResponsiveMasterDetail>
  );
}

function CrisisDetail({ appeal, contact, contactPending, urgentPending, dismissPending, onConfirmUrgent, onDismiss, onReadContact, onOpenInQueue, onLeave, onRefresh, error }: {
  appeal: CrisisQueueItem;
  contact?: string;
  contactPending: boolean;
  urgentPending: boolean;
  dismissPending: boolean;
  onConfirmUrgent: () => void;
  onDismiss: (reason: string) => void;
  onReadContact: () => void;
  onOpenInQueue: () => void;
  onLeave: () => void;
  onRefresh: () => void;
  error: unknown;
}) {
  const [showDismiss, setShowDismiss] = useState(false);
  const [dismissReason, setDismissReason] = useState('');
  const staleConflict = isOperatorStaleConflict(error);
  const leaseConflict = isOperatorLeaseConflict(error);
  return (
    <div className="operator-detail__content crisis-detail">
      <header className="appeal-detail__heading"><div><p className="eyebrow">Проверка риска · ожидание {formatWaiting(appeal.waitingMinutes)}</p><h2 data-detail-heading tabIndex={-1}>{appeal.priority === 'Urgent' ? 'Срочность подтверждена' : appeal.riskTypeText}</h2></div><span>{appeal.statusText}</span></header>
      <dl className="operator-case-summary" aria-label="Сводка кризисного обращения"><div><dt>Номер обращения</dt><dd>{appeal.caseNumber}</dd></div><div><dt>Заявитель</dt><dd>{appeal.applicantTypeText}</dd></div><div><dt>Категория</dt><dd>{appeal.category}</dd></div><div><dt>Источник сигнала</dt><dd>{appeal.sourceText}</dd></div><div><dt>Контакт</dt><dd>{appeal.hasContact ? 'Оставлен добровольно' : 'Не оставлен'}</dd></div></dl>
      <section className="detail-section crisis-signal" aria-labelledby="crisis-signal-heading"><h3 id="crisis-signal-heading">Сведения для проверки</h3><p className="crisis-signal__text">{appeal.narrative ?? 'Свободный текст не добавлен — проверьте ответы формы и источник сигнала.'}</p>{appeal.answers.length ? <dl className="answer-list">{appeal.answers.map((answer) => <div key={answer.questionCode}><dt>{answer.question}</dt><dd>{answer.value}</dd></div>)}</dl> : null}{appeal.sourceText.includes('диалоге') ? <p className="operator-step__waiting">Сигнал появился в новом сообщении заявителя. По матрице доступа оператор не читает экспертный диалог; после подтверждения срочности ответственный специалист увидит сообщение в своём кабинете.</p> : null}</section>
      <section className="detail-section crisis-support-reference"><h3>Контакты немедленной помощи</h3><p><a href="tel:112"><strong>112</strong></a> — единый номер экстренных служб</p><p><a href="tel:88002000122"><strong>8 800 2000 122</strong></a> — детский телефон доверия</p><p><a href="tel:124"><strong>124</strong></a> — короткий номер детского телефона доверия</p></section>
      <section className="detail-section"><h3>Добровольный контакт</h3>{contact ? <p className="crisis-contact-value">{contact}</p> : appeal.hasContact ? <><p>Контакт скрыт. Перед открытием будет создана запись аудита.</p><md-outlined-button disabled={contactPending} onClick={onReadContact}>{contactPending ? 'Открываем…' : 'Открыть контакт и записать доступ'}</md-outlined-button></> : <p>Контакт не оставлен. Не обещайте физическую помощь; используйте анонимный канал и экстренные номера.</p>}{contact ? <small>Открытие контакта записано в журнал доступа.</small> : null}</section>
      <section className="detail-section"><h3>Граница помощи</h3><p>Без добровольного контакта и местонахождения сервис не может установить личность или направить физическую помощь. Можно продолжить анонимную поддержку и показать экстренные контакты.</p></section>
      <section className="operator-actions"><h3>Решение по сигналу</h3>{appeal.priority !== 'Urgent' ? <><p>Подтвердите срочность, если сведения указывают на непосредственную угрозу жизни, здоровью или риск самоповреждения.</p><div className="action-row"><md-filled-button disabled={urgentPending || dismissPending} onClick={onConfirmUrgent}>{urgentPending ? 'Подтверждаем…' : 'Подтвердить срочность'}</md-filled-button><md-outlined-button disabled={urgentPending || dismissPending} onClick={() => setShowDismiss(true)}>Сигнал не подтверждается</md-outlined-button></div>{showDismiss ? <div className="decision-panel"><label className="text-field"><span>Почему срочность не подтверждается</span><textarea required minLength={10} maxLength={1000} value={dismissReason} onChange={(event) => setDismissReason(event.target.value)} /><small>Минимум 10 символов · сейчас {dismissReason.trim().length}</small></label><div className="action-row"><md-filled-button disabled={dismissPending || dismissReason.trim().length < 10} onClick={() => onDismiss(dismissReason)}>{dismissPending ? 'Сохраняем…' : 'Подтвердить решение'}</md-filled-button><md-outlined-button disabled={dismissPending} onClick={() => { setShowDismiss(false); setDismissReason(''); }}>Отменить</md-outlined-button></div></div> : null}</> : appeal.canOpenInPrimaryQueue ? <><p>Срочность подтверждена. Следующий шаг — определить маршрут и назначить ответственного специалиста.</p><md-filled-button onClick={onOpenInQueue}>Перейти к разбору и назначению</md-filled-button></> : <p>Обращение уже находится у специалиста. Срочный статус сохранён в его рабочем контексте.</p>}</section>
      {leaseConflict ? <div className="operator-stale" role="alert"><h3>Сигнал перешёл другому оператору</h3><p>Срок закрепления закончился, и карточку уже взяло другое рабочее окно. Ваше действие не сохранено.</p><md-filled-button onClick={onLeave}>Вернуться к срочным задачам</md-filled-button></div> : staleConflict ? <div className="operator-stale" role="alert"><h3>Срочность уже проверил другой оператор</h3><p>Откройте актуальную версию перед следующим действием.</p><md-filled-button onClick={onRefresh}>Открыть актуальную версию</md-filled-button></div> : error ? <p className="form-error" role="alert">{operatorError(error)}</p> : null}
    </div>
  );
}

function formatWaiting(minutes: number) {
  if (minutes < 1) return 'меньше минуты';
  if (minutes < 60) return `${minutes} мин`;
  const hours = Math.floor(minutes / 60);
  const rest = minutes % 60;
  return rest ? `${hours} ч ${rest} мин` : `${hours} ч`;
}
