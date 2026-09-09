import '@material/web/button/filled-button.js';
import '@material/web/button/outlined-button.js';
import { useMutation, useQuery } from '@tanstack/react-query';
import { FormEvent, useEffect, useMemo, useState } from 'react';
import { Link, NavLink, useLocation, useNavigate } from 'react-router-dom';
import { createAppealUpdatesConnection } from '../api/appealUpdates';
import {
  getDeviceAppealStatus,
  getDevicePushConfig,
  pushCapabilityError,
  queueTestNotification,
  removeDeviceSession,
  revokeDevicePushSubscription,
  saveDevicePushSubscription,
  urlBase64ToUint8Array,
} from '../api/deviceSession';
import {
  addApplicantMessage,
  continueAppeal,
  downloadAppealAttachment,
  getAppealStatus,
  getIntakeOptions,
  leaveAppealFeedback,
  markAppealHelped,
  publicAppealError,
  returnAppeal,
  submitAppealComplaint,
  type AppealAttachment,
  type AppealCycle,
} from '../api/publicAppeals';
import { crisisTextMatches } from '../crisis';
import { CrisisHelpPanel } from './CrisisHelpPanel';
import { PublicFrame } from './PublicFrame';

type PublicWorkspace = 'dialog' | 'answer' | 'history' | 'access';

const workspaceLabels: Array<{ key: PublicWorkspace; label: string }> = [
  { key: 'dialog', label: 'Диалог' },
  { key: 'answer', label: 'Ответ' },
  { key: 'history', label: 'История' },
  { key: 'access', label: 'Доступ' },
];

export function AppealStatusPage() {
  const location = useLocation();
  const navigate = useNavigate();
  const routeWorkspace = location.pathname.split('/').filter(Boolean).at(-1);
  const [trackNumber, setTrackNumber] = useState('');
  const [showTrackForm, setShowTrackForm] = useState(false);
  const [replyBody, setReplyBody] = useState('');
  const [crisisContact, setCrisisContact] = useState('');
  const [clientMessageId, setClientMessageId] = useState(() => crypto.randomUUID());
  const [clientOutcomeId, setClientOutcomeId] = useState(() => crypto.randomUUID());
  const [returnMode, setReturnMode] = useState(false);
  const [returnReason, setReturnReason] = useState('NotClear');
  const [returnDetails, setReturnDetails] = useState('');
  const [score, setScore] = useState(5);
  const [feedbackComment, setFeedbackComment] = useState('');
  const [clientFeedbackId, setClientFeedbackId] = useState(() => crypto.randomUUID());
  const [complaintMode, setComplaintMode] = useState(false);
  const [complaintBody, setComplaintBody] = useState('');
  const [clientComplaintId, setClientComplaintId] = useState(() => crypto.randomUUID());
  const [continuationMode, setContinuationMode] = useState(false);
  const [continuationBody, setContinuationBody] = useState('');
  const [clientContinuationId, setClientContinuationId] = useState(() => crypto.randomUUID());
  const [confirmDeviceRemoval, setConfirmDeviceRemoval] = useState(false);

  const optionsQuery = useQuery({ queryKey: ['intake-options'], queryFn: getIntakeOptions, retry: 2 });
  const statusMutation = useMutation({
    mutationFn: getAppealStatus,
    onSuccess: () => navigate('/appeal/dialog'),
  });
  const deviceStatusQuery = useQuery({
    queryKey: ['device-appeal-status'],
    queryFn: getDeviceAppealStatus,
    retry: false,
    refetchInterval: 30_000,
  });
  const status = statusMutation.data ?? deviceStatusQuery.data ?? undefined;
  const usesDeviceAccess = Boolean(deviceStatusQuery.data) && !statusMutation.data;
  const activeTrackNumber = usesDeviceAccess ? '' : trackNumber;
  const workspace: PublicWorkspace = isWorkspace(routeWorkspace)
    ? routeWorkspace
    : status ? 'dialog' : 'access';
  const current = status?.currentCycle;
  const permissions = status?.permissions;
  const refreshStatus = () => usesDeviceAccess
    ? deviceStatusQuery.refetch()
    : Promise.resolve(statusMutation.mutate(trackNumber));

  useEffect(() => {
    if (location.pathname !== '/appeal' || deviceStatusQuery.isPending) return;
    navigate(status ? '/appeal/dialog' : '/appeal/access', { replace: true });
  }, [deviceStatusQuery.isPending, location.pathname, navigate, status]);

  useEffect(() => {
    if (status || deviceStatusQuery.isPending || isWorkspace(routeWorkspace)) return;
    navigate('/appeal/access', { replace: true });
  }, [deviceStatusQuery.isPending, navigate, routeWorkspace, status]);

  useEffect(() => {
    if (!status || usesDeviceAccess || !trackNumber) return undefined;
    const connection = createAppealUpdatesConnection(() => statusMutation.mutate(trackNumber));
    connection.onreconnected(() => connection.invoke('JoinApplicantAppeal', trackNumber).catch(() => undefined));
    void connection.start()
      .then(() => connection.invoke('JoinApplicantAppeal', trackNumber))
      .catch(() => undefined);
    return () => { void connection.stop(); };
  }, [status, trackNumber, usesDeviceAccess]);

  const replyCrisisDetected = useMemo(
    () => crisisTextMatches([replyBody], optionsQuery.data?.crisisMarkers ?? []),
    [optionsQuery.data?.crisisMarkers, replyBody],
  );
  const continuationCrisisDetected = useMemo(
    () => crisisTextMatches([continuationBody], optionsQuery.data?.crisisMarkers ?? []),
    [continuationBody, optionsQuery.data?.crisisMarkers],
  );
  const showCrisisHelp = Boolean(status?.needsImmediateHelp || replyCrisisDetected || continuationCrisisDetected);
  const crisisContactValid = crisisContact.length === 0 || crisisContact.trim().length >= 5;

  const replyMutation = useMutation({
    mutationFn: () => addApplicantMessage(
      activeTrackNumber,
      clientMessageId,
      replyBody,
      crisisContact.trim() || undefined,
    ),
    onSuccess: () => {
      setReplyBody('');
      setCrisisContact('');
      setClientMessageId(crypto.randomUUID());
      void refreshStatus();
    },
  });
  const continuationMutation = useMutation({
    mutationFn: () => continueAppeal({
      trackNumber: activeTrackNumber,
      clientContinuationId,
      expectedThreadVersion: status!.threadVersion,
      body: continuationBody,
      crisisContact: crisisContact.trim() || undefined,
    }),
    onSuccess: () => {
      setContinuationBody('');
      setCrisisContact('');
      setClientContinuationId(crypto.randomUUID());
      setContinuationMode(false);
      void refreshStatus();
      navigate('/appeal/dialog');
    },
  });
  const helpedMutation = useMutation({
    mutationFn: () => markAppealHelped(activeTrackNumber, clientOutcomeId, current!.version),
    onSuccess: () => { setClientOutcomeId(crypto.randomUUID()); void refreshStatus(); },
  });
  const returnMutation = useMutation({
    mutationFn: () => returnAppeal(activeTrackNumber, {
      clientActionId: clientOutcomeId,
      expectedVersion: current!.version,
      reason: returnReason,
      details: returnDetails || undefined,
    }),
    onSuccess: () => {
      setReturnMode(false);
      setReturnDetails('');
      setClientOutcomeId(crypto.randomUUID());
      void refreshStatus();
    },
  });
  const feedbackMutation = useMutation({
    mutationFn: () => leaveAppealFeedback(activeTrackNumber, {
      clientFeedbackId,
      score,
      comment: feedbackComment || undefined,
    }),
    onSuccess: () => { setClientFeedbackId(crypto.randomUUID()); setFeedbackComment(''); void refreshStatus(); },
  });
  const complaintMutation = useMutation({
    mutationFn: () => submitAppealComplaint(activeTrackNumber, clientComplaintId, complaintBody),
    onSuccess: () => {
      setComplaintMode(false);
      setComplaintBody('');
      setClientComplaintId(crypto.randomUUID());
      void refreshStatus();
    },
  });
  const downloadMutation = useMutation({
    mutationFn: ({ attachment }: { attachment: AppealAttachment }) =>
      downloadAppealAttachment(activeTrackNumber, attachment),
  });

  const devicePushQuery = useQuery({
    queryKey: ['device-push-config'],
    queryFn: getDevicePushConfig,
    retry: false,
    enabled: Boolean(deviceStatusQuery.data),
  });
  const pushMutation = useMutation({
    mutationFn: enablePushNotifications,
    onSuccess: () => void devicePushQuery.refetch(),
  });
  const revokePushMutation = useMutation({
    mutationFn: async () => {
      const registration = await navigator.serviceWorker?.ready;
      const browserSubscription = await registration?.pushManager.getSubscription();
      if (browserSubscription) await browserSubscription.unsubscribe();
      return revokeDevicePushSubscription();
    },
    onSuccess: () => void devicePushQuery.refetch(),
  });
  const testPushMutation = useMutation({ mutationFn: queueTestNotification });
  const removeDeviceMutation = useMutation({
    mutationFn: async () => {
      const registration = await navigator.serviceWorker?.ready;
      const browserSubscription = await registration?.pushManager.getSubscription();
      if (browserSubscription) await browserSubscription.unsubscribe();
      await removeDeviceSession();
    },
    onSuccess: () => {
      statusMutation.reset();
      void deviceStatusQuery.refetch();
      setShowTrackForm(true);
      navigate('/appeal/access');
    },
  });

  function submitTrack(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    statusMutation.mutate(trackNumber);
  }

  async function enablePushNotifications() {
    if (!('serviceWorker' in navigator) || !('PushManager' in window) || !('Notification' in window)) {
      throw new Error('Push is not supported.');
    }
    const config = await getDevicePushConfig();
    if (!config.publicKey) throw new Error('Push is not configured.');
    const permission = Notification.permission === 'default'
      ? await Notification.requestPermission()
      : Notification.permission;
    if (permission !== 'granted') throw new DOMException('Permission denied', 'NotAllowedError');
    const registration = await navigator.serviceWorker.ready;
    const existing = await registration.pushManager.getSubscription();
    const subscription = existing ?? await registration.pushManager.subscribe({
      userVisibleOnly: true,
      applicationServerKey: urlBase64ToUint8Array(config.publicKey),
    });
    return saveDevicePushSubscription(subscription);
  }

  return (
    <PublicFrame current="status">
      <main className="appeal-workspace">
        {deviceStatusQuery.isPending && !status ? (
          <section className="appeal-entry" aria-labelledby="appeal-loading-heading">
            <p className="eyebrow">Моё обращение</p>
            <h1 id="appeal-loading-heading">Открываем безопасный доступ</h1>
            <p role="status">Проверяем, сохранён ли доступ на этом устройстве…</p>
          </section>
        ) : status && current ? (
          <section className="status-result" aria-live="polite">
            <header className="appeal-workspace__summary">
              <div>
                <p className="eyebrow">Моё обращение · цикл {current.number} из {status.cycleCount}</p>
                <h1>{status.statusText}</h1>
                <p>Последнее обновление {formatDate(status.summary.lastActivityAt)}</p>
              </div>
              <md-outlined-button onClick={() => void refreshStatus()}>Обновить</md-outlined-button>
            </header>

            <nav className="appeal-section-nav" aria-label="Разделы обращения">
              {workspaceLabels.map((item) => (
                <NavLink key={item.key} to={`/appeal/${item.key}`}>{item.label}</NavLink>
              ))}
            </nav>

            {showCrisisHelp ? (
              <CrisisHelpPanel
                contacts={status.crisisSupport.length > 0 ? status.crisisSupport : optionsQuery.data?.crisisSupport ?? []}
                contactValue={crisisContact}
                onContactChange={permissions?.canReply || permissions?.canContinue ? setCrisisContact : undefined}
                compact
              />
            ) : null}

            {workspace === 'dialog' ? renderDialog() : null}
            {workspace === 'answer' ? renderAnswer() : null}
            {workspace === 'history' ? renderHistory() : null}
            {workspace === 'access' ? renderAccess() : null}
          </section>
        ) : (
          <section className="appeal-entry" aria-labelledby="status-heading">
            <p className="eyebrow">Возвращение без регистрации</p>
            <h1 id="status-heading">Открыть моё обращение</h1>
            <p>Введите сохранённый трек-номер. Он не попадёт в адрес страницы.</p>
            {renderTrackForm()}
          </section>
        )}
      </main>
    </PublicFrame>
  );

  function renderDialog() {
    return (
      <section className="appeal-section public-chat" aria-labelledby="dialog-heading">
        <header>
          <p className="eyebrow">Текущий цикл</p>
          <h2 id="dialog-heading">Диалог со специалистом</h2>
          <p>{permissions?.canReply
            ? 'Специалист ждёт ваш ответ. Напишите только то, чем готовы поделиться.'
            : 'Здесь появятся вопросы специалиста и ваши ответы.'}</p>
        </header>
        {current!.narrative ? (
          <article className="appeal-opening-message">
            <strong>С чего начался этот цикл</strong>
            <p>{current!.narrative}</p>
          </article>
        ) : null}
        <div className="public-messages" aria-live="polite">
          {current!.messages.length === 0 ? <p className="empty-state">Сообщений пока нет.</p> : null}
          {current!.messages.map((message) => (
            <article className={`public-message public-message--${message.author.toLowerCase()}`} key={message.id}>
              <strong>{message.authorLabel}</strong>
              <p>{message.body}</p>
              <time dateTime={message.createdAt}>{formatDate(message.createdAt)}</time>
            </article>
          ))}
        </div>
        {permissions?.canReply ? (
          <form className="public-reply" onSubmit={(event) => { event.preventDefault(); replyMutation.mutate(); }}>
            <label className="text-field">
              <span>Ваш ответ</span>
              <textarea maxLength={4000} minLength={2} required value={replyBody} onChange={(event) => setReplyBody(event.target.value)} />
            </label>
            <md-filled-button type="submit" disabled={replyMutation.isPending || replyBody.trim().length < 2 || !crisisContactValid}>
              {replyMutation.isPending ? 'Отправляем…' : 'Отправить ответ'}
            </md-filled-button>
            {replyMutation.isError ? <p className="gentle-error" role="alert">{publicAppealError(replyMutation.error)}</p> : null}
          </form>
        ) : (
          <div className="appeal-next-action">
            <p>Сейчас ответ от вас не требуется.</p>
            {current!.recommendations.length > 0 ? <Link to="/appeal/answer">Перейти к ответу специалиста</Link> : null}
          </div>
        )}
        {current!.attachments.length > 0 ? renderAttachments(current!) : null}
      </section>
    );
  }

  function renderAnswer() {
    return (
      <section className="appeal-section" aria-labelledby="answer-heading">
        <header>
          <p className="eyebrow">Результат текущего цикла</p>
          <h2 id="answer-heading">Ответ и следующий шаг</h2>
          <p>{current!.recommendations.length > 0
            ? 'Рекомендации сохранены здесь и останутся в истории обращения.'
            : 'Специалист ещё готовит ответ. Можно вернуться позже.'}</p>
        </header>
        <div className="public-recommendations">
          {current!.recommendations.map((recommendation) => (
            <article className="public-recommendation" key={recommendation.id}>
              <strong>Рекомендация {recommendation.version}</strong>
              <p>{recommendation.body}</p>
              <time dateTime={recommendation.createdAt}>{formatDate(recommendation.createdAt)}</time>
            </article>
          ))}
        </div>
        {current!.resolution ? <section className="public-resolution"><h3>Итог сервиса</h3><p>{current!.resolution}</p></section> : null}
        {permissions?.canMarkOutcome ? (
          <section className="public-outcome" aria-labelledby="outcome-heading">
            <h3 id="outcome-heading">Нужна ли ещё помощь?</h3>
            <p>Ваш выбор определит следующий шаг по этому же циклу.</p>
            <div className="action-row">
              <md-outlined-button disabled={helpedMutation.isPending || returnMutation.isPending} onClick={() => helpedMutation.mutate()}>Помощи достаточно</md-outlined-button>
              <md-outlined-button disabled={helpedMutation.isPending || returnMutation.isPending} onClick={() => setReturnMode(true)}>Нужна ещё помощь</md-outlined-button>
            </div>
            {returnMode ? (
              <form className="outcome-form" onSubmit={(event) => { event.preventDefault(); returnMutation.mutate(); }}>
                <label className="select-field"><span>Чего не хватило</span><select value={returnReason} onChange={(event) => setReturnReason(event.target.value)}><option value="NotClear">Ответ непонятен</option><option value="NotSuitable">Совет не подходит</option><option value="NeedMoreHelp">Нужна дополнительная помощь</option><option value="SituationChanged">Ситуация изменилась</option><option value="Other">Другая причина</option></select></label>
                <label className="text-field"><span>Комментарий {returnReason === 'Other' ? '' : '(необязательно)'}</span><textarea maxLength={2000} minLength={returnReason === 'Other' ? 5 : undefined} required={returnReason === 'Other'} value={returnDetails} onChange={(event) => setReturnDetails(event.target.value)} /></label>
                <div className="action-row"><md-filled-button type="submit" disabled={returnMutation.isPending || (returnReason === 'Other' && returnDetails.trim().length < 5)}>Попросить пересмотреть помощь</md-filled-button><md-outlined-button type="button" onClick={() => setReturnMode(false)}>Отмена</md-outlined-button></div>
              </form>
            ) : null}
            {helpedMutation.isError || returnMutation.isError ? <p className="gentle-error" role="alert">{publicAppealError(helpedMutation.error ?? returnMutation.error)}</p> : null}
          </section>
        ) : null}
        {permissions?.requiresFinalOperatorDecision ? <p className="public-lifecycle-note">Оператор проверяет ситуацию и подготовит окончательное решение.</p> : null}
        {permissions?.canLeaveFeedback ? (
          <form className="outcome-form" onSubmit={(event) => { event.preventDefault(); feedbackMutation.mutate(); }}>
            <h3>Оцените полученную помощь</h3>
            <label className="select-field"><span>Оценка</span><select value={score} onChange={(event) => setScore(Number(event.target.value))}><option value={5}>5 — очень помогло</option><option value={4}>4</option><option value={3}>3</option><option value={2}>2</option><option value={1}>1 — не помогло</option></select></label>
            <label className="text-field"><span>Комментарий (необязательно)</span><textarea maxLength={2000} value={feedbackComment} onChange={(event) => setFeedbackComment(event.target.value)} /></label>
            <md-filled-button type="submit" disabled={feedbackMutation.isPending}>Отправить оценку</md-filled-button>
          </form>
        ) : null}
        {permissions?.feedbackSubmitted ? <p className="public-lifecycle-note">Спасибо, оценка сохранена.</p> : null}
        {current!.recommendations.length > 0 && !permissions?.complaintSubmitted ? (
          <section className="public-complaint">
            <h3>Проблема в работе специалиста</h3>
            {!complaintMode ? <md-outlined-button onClick={() => setComplaintMode(true)}>Сообщить оператору</md-outlined-button> : (
              <form className="outcome-form" onSubmit={(event) => { event.preventDefault(); complaintMutation.mutate(); }}>
                <label className="text-field"><span>Что произошло</span><textarea minLength={10} maxLength={2000} required value={complaintBody} onChange={(event) => setComplaintBody(event.target.value)} /></label>
                <div className="action-row"><md-filled-button type="submit" disabled={complaintMutation.isPending || complaintBody.trim().length < 10}>Передать оператору</md-filled-button><md-outlined-button type="button" onClick={() => setComplaintMode(false)}>Отмена</md-outlined-button></div>
              </form>
            )}
          </section>
        ) : null}
        {permissions?.complaintSubmitted ? <p className="public-lifecycle-note">Сообщение передано оператору. Специалист не видит его текст.</p> : null}
      </section>
    );
  }

  function renderHistory() {
    return (
      <section className="appeal-section" aria-labelledby="history-heading">
        <header>
          <p className="eyebrow">Один трек-номер</p>
          <h2 id="history-heading">История обращения</h2>
          <p>Каждое продолжение сохраняется отдельным циклом. Прошлые сообщения и ответы не исчезают.</p>
        </header>
        <div className="appeal-cycle-list">
          {[...status!.cycles].reverse().map((cycle) => (
            <details className="appeal-cycle" key={cycle.id} open={cycle.id === current!.id}>
              <summary>
                <span><strong>Цикл {cycle.number}</strong><small>{formatDate(cycle.createdAt)}</small></span>
                <span>{cycle.statusText}</span>
              </summary>
              <div className="appeal-cycle__content">
                {cycle.narrative ? <section><h3>Начало цикла</h3><p>{cycle.narrative}</p></section> : null}
                {cycle.messages.length > 0 ? <section><h3>Диалог</h3>{cycle.messages.map((message) => <article className="appeal-cycle__message" key={message.id}><strong>{message.authorLabel}</strong><p>{message.body}</p><time dateTime={message.createdAt}>{formatDate(message.createdAt)}</time></article>)}</section> : null}
                {cycle.recommendations.length > 0 ? <section><h3>Ответы специалиста</h3>{cycle.recommendations.map((recommendation) => <article className="appeal-cycle__message" key={recommendation.id}><p>{recommendation.body}</p><time dateTime={recommendation.createdAt}>{formatDate(recommendation.createdAt)}</time></article>)}</section> : null}
                <section><h3>Изменения статуса</h3><div className="status-timeline">{cycle.timeline.map((item) => <div className="status-timeline__item" key={`${cycle.id}-${item.status}-${item.at}`}><strong>{item.text}</strong><span>{formatDate(item.at)}</span></div>)}</div></section>
                {cycle.attachments.length > 0 ? renderAttachments(cycle) : null}
              </div>
            </details>
          ))}
        </div>
        <section className="continuation-choice" aria-labelledby="continuation-heading">
          <h3 id="continuation-heading">Что вы хотите сделать?</h3>
          {permissions?.canContinue ? (
            <>
              <p><strong>Та же ситуация продолжается?</strong> Добавьте новый цикл — трек-номер и вся история сохранятся.</p>
              {!continuationMode ? <md-filled-button onClick={() => setContinuationMode(true)}>Продолжить это обращение</md-filled-button> : (
                <form className="outcome-form" onSubmit={(event) => { event.preventDefault(); continuationMutation.mutate(); }}>
                  <label className="text-field"><span>Что изменилось или повторилось</span><textarea minLength={10} maxLength={10000} required value={continuationBody} onChange={(event) => setContinuationBody(event.target.value)} /></label>
                  <div className="action-row"><md-filled-button type="submit" disabled={continuationMutation.isPending || continuationBody.trim().length < 10 || !crisisContactValid}>{continuationMutation.isPending ? 'Сохраняем…' : 'Начать новый цикл'}</md-filled-button><md-outlined-button type="button" onClick={() => setContinuationMode(false)}>Отмена</md-outlined-button></div>
                  {continuationMutation.isError ? <p className="gentle-error" role="alert">{publicAppealError(continuationMutation.error)}</p> : null}
                </form>
              )}
            </>
          ) : <p>Текущий цикл ещё открыт. Новую информацию добавляйте в диалоге, когда специалист попросит уточнение.</p>}
          <p><strong>Это другая ситуация?</strong> Создайте отдельное обращение с новым трек-номером.</p>
          <Link className="secondary-link" to="/appeal/new">Создать другое обращение</Link>
        </section>
      </section>
    );
  }

  function renderAccess() {
    return (
      <section className="appeal-section device-access" aria-labelledby="access-heading">
        <header><p className="eyebrow">Приватность</p><h2 id="access-heading">Доступ к обращению</h2><p>{usesDeviceAccess ? 'На этом устройстве сохранён защищённый ключ. Трек-номер не хранится в адресе страницы.' : 'Сейчас обращение открыто по введённому трек-номеру.'}</p></header>
        {usesDeviceAccess ? (
          <section className="device-access__permission">
            <h3>Нейтральные уведомления</h3>
            {devicePushQuery.data?.subscribed ? (
              <div className="action-row"><md-outlined-button disabled={testPushMutation.isPending} onClick={() => testPushMutation.mutate()}>Проверить уведомление</md-outlined-button><md-outlined-button disabled={revokePushMutation.isPending} onClick={() => revokePushMutation.mutate()}>Выключить уведомления</md-outlined-button></div>
            ) : 'serviceWorker' in navigator && 'PushManager' in window && 'Notification' in window ? (
              <><p>В уведомлении будет только фраза «В обращении есть обновление».</p><md-filled-button disabled={pushMutation.isPending} onClick={() => pushMutation.mutate()}>{pushMutation.isPending ? 'Включаем…' : 'Включить уведомления'}</md-filled-button></>
            ) : <p>Этот браузер не поддерживает push. Доступ по трек-номеру продолжит работать.</p>}
            {pushMutation.isError || revokePushMutation.isError || testPushMutation.isError ? <p className="gentle-error" role="alert">{pushCapabilityError(pushMutation.error ?? revokePushMutation.error ?? testPushMutation.error)}</p> : null}
            {testPushMutation.isSuccess ? <p role="status">Тестовое уведомление поставлено в очередь.</p> : null}
          </section>
        ) : null}
        <section className="device-access__shared">
            <h3>Открыть по другому номеру</h3>
          {!showTrackForm ? <md-outlined-button onClick={() => setShowTrackForm(true)}>Ввести другой трек-номер</md-outlined-button> : renderTrackForm()}
        </section>
        {usesDeviceAccess ? (
          <section className="device-access__shared">
            <h3>Это общее устройство?</h3>
            <p>Удалите локальный доступ. Вернуться после этого можно только по сохранённому трек-номеру.</p>
            {!confirmDeviceRemoval ? <md-outlined-button onClick={() => setConfirmDeviceRemoval(true)}>Удалить доступ с устройства</md-outlined-button> : <div className="action-row"><md-filled-button disabled={removeDeviceMutation.isPending} onClick={() => removeDeviceMutation.mutate()}>{removeDeviceMutation.isPending ? 'Удаляем…' : 'Да, удалить доступ'}</md-filled-button><md-outlined-button onClick={() => setConfirmDeviceRemoval(false)}>Отмена</md-outlined-button></div>}
            {removeDeviceMutation.isError ? <p className="gentle-error" role="alert">{pushCapabilityError(removeDeviceMutation.error)}</p> : null}
          </section>
        ) : null}
      </section>
    );
  }

  function renderTrackForm() {
    return (
      <form className="track-form" onSubmit={submitTrack}>
        <label><span>Трек-номер</span><input autoCapitalize="characters" autoComplete="off" maxLength={32} required value={trackNumber} onChange={(event) => { setTrackNumber(event.target.value); statusMutation.reset(); }} placeholder="ОТК-XXXX-XXXX" /></label>
        <md-filled-button type="submit" disabled={statusMutation.isPending}>{statusMutation.isPending ? 'Открываем…' : 'Открыть обращение'}</md-filled-button>
        {statusMutation.isError ? <p className="gentle-error" role="alert">{publicAppealError(statusMutation.error)}</p> : null}
      </form>
    );
  }

  function renderAttachments(cycle: AppealCycle) {
    return (
      <section className="status-attachments" aria-labelledby="attachments-heading">
        <h3 id="attachments-heading">Вложения этого цикла</h3>
        {cycle.attachments.map((attachment) => (
          <div className="status-attachment" key={attachment.id}><div><strong>{attachment.displayName}</strong><span>{formatFileSize(attachment.size)}</span></div><md-outlined-button disabled={downloadMutation.isPending} onClick={() => downloadMutation.mutate({ attachment })}>Скачать</md-outlined-button></div>
        ))}
      </section>
    );
  }
}

function isWorkspace(value: string | undefined): value is PublicWorkspace {
  return workspaceLabels.some((workspace) => workspace.key === value);
}

function formatFileSize(size: number) {
  if (size < 1024 * 1024) return `${Math.max(1, Math.round(size / 1024))} КБ`;
  return `${(size / 1024 / 1024).toFixed(1)} МБ`;
}

function formatDate(value: string) {
  return new Intl.DateTimeFormat('ru-RU', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value));
}
