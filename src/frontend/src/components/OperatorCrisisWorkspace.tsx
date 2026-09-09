import '@material/web/button/filled-button.js';
import '@material/web/button/outlined-button.js';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import {
  confirmCrisisUrgent,
  getCrisisQueue,
  readCrisisContact,
  type CrisisQueueItem,
} from '../api/operatorCrisis';
import { operatorError } from '../api/operatorQueue';
import { ResponsiveMasterDetail } from './ux/WorkspacePrimitives';

export function OperatorCrisisWorkspace() {
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const { appealId: selectedId } = useParams();
  const [visibleContacts, setVisibleContacts] = useState<Record<string, string>>({});
  const queueQuery = useQuery({
    queryKey: ['operator-crisis'],
    queryFn: getCrisisQueue,
    refetchInterval: 15_000,
  });
  const selected = queueQuery.data?.items.find((item) => item.id === selectedId);
  const urgentMutation = useMutation({
    mutationFn: ({ appealId, version }: { appealId: string; version: number }) =>
      confirmCrisisUrgent(appealId, version),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['operator-crisis'] }),
        queryClient.invalidateQueries({ queryKey: ['operator-queue'] }),
      ]);
    },
  });
  const contactMutation = useMutation({
    mutationFn: readCrisisContact,
    onSuccess: (result, appealId) => {
      setVisibleContacts((current) => ({ ...current, [appealId]: result.contact }));
    },
  });

  return (
    <ResponsiveMasterDetail className="operator-workspace crisis-workspace" hasDetail={Boolean(selectedId)}>
      <section className="operator-queue" aria-labelledby="crisis-queue-heading">
        <header className="operator-heading">
          <div><p className="eyebrow">Кабинет оператора</p><h1 id="crisis-queue-heading" className="staff-title">Срочная помощь</h1><p className="staff-copy">Сначала подтвердите срочность, затем направьте обращение в назначение.</p></div>
          {queueQuery.data ? <div className="queue-counts" aria-label="Сводка кризисной очереди"><span><strong>{queueQuery.data.total}</strong> требуют внимания</span><span><strong>{queueQuery.data.unconfirmedCount}</strong> без подтверждения</span></div> : null}
        </header>
        {queueQuery.isPending ? <p className="staff-copy">Загружаем кризисную очередь…</p> : null}
        {queueQuery.isError ? <p className="form-error" role="alert">Не удалось загрузить очередь.</p> : null}
        <div className="queue-list" aria-label="Кризисные обращения">
          {queueQuery.data?.items.map((appeal) => (
            <button className={`queue-row${selectedId === appeal.id ? ' queue-row--selected' : ''}`} key={appeal.id} type="button" aria-pressed={selectedId === appeal.id} onClick={() => navigate(`/staff/operator/urgent/${appeal.id}`)}>
              <span className="queue-row__topline"><strong>{appeal.priority === 'Urgent' ? 'Назначить срочную помощь' : 'Подтвердить срочность'}</strong><span>{formatWaiting(appeal.waitingMinutes)}</span></span>
              <span className="queue-row__category">{appeal.applicantTypeText} · {appeal.category}</span>
              <span className="queue-row__meta">{appeal.hasContact ? 'Есть добровольный контакт' : 'Контакт не оставлен'}</span>
            </button>
          ))}
        </div>
        {queueQuery.data?.items.length === 0 ? <div className="empty-state"><h2>Срочных задач сейчас нет</h2><p>Очередь обновляется автоматически.</p></div> : null}
      </section>

      <section className="operator-detail" aria-label="Проверка срочности">
        {selectedId ? <button className="operator-back" type="button" onClick={() => navigate('/staff/operator/urgent')}>Вернуться к срочным задачам</button> : null}
        {!selectedId ? <div className="operator-detail__empty"><p className="eyebrow">Проверка риска</p><h2>Выберите обращение</h2><p>Вы увидите только безопасные признаки, контакт и следующий допустимый шаг.</p></div> : null}
        {selectedId && queueQuery.isPending ? <p>Открываем задачу…</p> : null}
        {selectedId && !queueQuery.isPending && !selected ? <div className="operator-detail__empty"><h2>Обращение уже обновилось</h2><p>Откройте актуальную кризисную очередь.</p><md-filled-button onClick={() => navigate('/staff/operator/urgent')}>К актуальной очереди</md-filled-button></div> : null}
        {selected ? (
          <CrisisDetail
            appeal={selected}
            contact={visibleContacts[selected.id]}
            contactPending={contactMutation.isPending}
            urgentPending={urgentMutation.isPending}
            onConfirmUrgent={() => urgentMutation.mutate({ appealId: selected.id, version: selected.version })}
            onReadContact={() => contactMutation.mutate(selected.id)}
            onOpenInQueue={() => navigate(`/staff/operator/queue/${selected.id}`)}
            error={urgentMutation.error ?? contactMutation.error}
          />
        ) : null}
      </section>
    </ResponsiveMasterDetail>
  );
}

function CrisisDetail({ appeal, contact, contactPending, urgentPending, onConfirmUrgent, onReadContact, onOpenInQueue, error }: {
  appeal: CrisisQueueItem;
  contact?: string;
  contactPending: boolean;
  urgentPending: boolean;
  onConfirmUrgent: () => void;
  onReadContact: () => void;
  onOpenInQueue: () => void;
  error: unknown;
}) {
  return (
    <div className="operator-detail__content crisis-detail">
      <header className="appeal-detail__heading"><div><p className="eyebrow">Маркер риска · ожидание {formatWaiting(appeal.waitingMinutes)}</p><h2>{appeal.priority === 'Urgent' ? 'Срочность подтверждена' : 'Срочность ещё не подтверждена'}</h2></div><span>{appeal.statusText}</span></header>
      <section className="detail-section"><h3>Граница помощи</h3><p>Если человек не оставил контакт и местонахождение, сервис не может направить физическую помощь или установить личность. Продолжайте анонимную помощь и покажите экстренные контакты в ответе.</p></section>
      <section className="detail-section crisis-support-reference"><h3>Контакты немедленной помощи</h3><p><strong>112</strong> — единый номер экстренных служб</p><p><strong>8 800 2000 122</strong> — детский телефон доверия</p></section>
      <section className="detail-section"><h3>Добровольный контакт</h3>{contact ? <p className="crisis-contact-value">{contact}</p> : appeal.hasContact ? <><p>Контакт скрыт. Перед открытием будет создана запись аудита.</p><md-outlined-button disabled={contactPending} onClick={onReadContact}>{contactPending ? 'Открываем…' : 'Открыть контакт и записать доступ'}</md-outlined-button></> : <p>Контакт не оставлен. Не обещайте физическую помощь; используйте анонимный канал и экстренные номера.</p>}{contact ? <small>Открытие контакта записано в журнал доступа.</small> : null}</section>
      <section className="operator-actions"><h3>Следующий шаг</h3>{appeal.priority !== 'Urgent' ? <md-filled-button disabled={urgentPending} onClick={onConfirmUrgent}>{urgentPending ? 'Подтверждаем…' : 'Подтвердить срочность'}</md-filled-button> : appeal.canOpenInPrimaryQueue ? <md-filled-button onClick={onOpenInQueue}>Перейти к назначению</md-filled-button> : <p>Обращение уже находится у специалиста.</p>}</section>
      {error ? <p className="form-error" role="alert">{operatorError(error)}</p> : null}
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
