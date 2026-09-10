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
import { getApplicantAppealGuidance } from './appealGuidance';
import { CrisisHelpPanel } from './CrisisHelpPanel';
import { PublicFrame } from './PublicFrame';

type PublicWorkspace = 'overview' | 'dialog' | 'answer' | 'history' | 'access';

const workspaceLabels: Array<{ key: PublicWorkspace; label: string }> = [
  { key: 'overview', label: 'Главное' },
  { key: 'dialog', label: 'Переписка' },
  { key: 'answer', label: 'Ответ специалиста' },
  { key: 'history', label: 'Вся история' },
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
    onSuccess: () => navigate('/appeal/overview'),
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
    : status ? 'overview' : 'access';
  const current = status?.currentCycle;
  const permissions = status?.permissions;
  const guidance = current ? getApplicantAppealGuidance(current.status, current.applicantType) : undefined;
  const refreshStatus = () => usesDeviceAccess
    ? deviceStatusQuery.refetch()
    : Promise.resolve(statusMutation.mutate(trackNumber));

  useEffect(() => {
    if (location.pathname !== '/appeal' || deviceStatusQuery.isPending) return;
    navigate(status ? '/appeal/overview' : '/appeal/access', { replace: true });
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
      navigate('/appeal/overview');
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
        ) : status && current && guidance ? (
          <section className="status-result">
            <header className="appeal-workspace__summary">
              <div>
                <p className="eyebrow">{status.cycleCount > 1 ? `Продолжение ${current.number} · один трек-номер` : 'Анонимная помощь'}</p>
                <h1>{current.applicantType === 'Student' ? 'Твоё обращение' : 'Ваше обращение'}</h1>
                <p>{current.applicantType === 'Student' ? 'Здесь видно, что происходит и нужно ли что-то сделать.' : 'Здесь видно, что происходит и требуется ли действие.'}<br />Обновлено {formatDate(status.summary.lastActivityAt)}</p>
              </div>
              <div className="appeal-workspace__actions">
                <Link className="appeal-access-link" to="/appeal/access">Доступ и уведомления</Link>
                <md-outlined-button onClick={() => void refreshStatus()}>Обновить</md-outlined-button>
              </div>
            </header>

            <nav className="appeal-section-nav" aria-label="Разделы обращения">
              {workspaceLabels.map((item) => (
                <NavLink key={item.key} to={`/appeal/${item.key}`}>{item.label}</NavLink>
              ))}
            </nav>

            {showCrisisHelp ? (
              <CrisisHelpPanel
                applicantType={current.applicantType}
                contacts={status.crisisSupport.length > 0 ? status.crisisSupport : optionsQuery.data?.crisisSupport ?? []}
                contactValue={crisisContact}
                onContactChange={permissions?.canReply || permissions?.canContinue ? setCrisisContact : undefined}
                compact
              />
            ) : null}

            {workspace === 'overview' ? renderOverview() : null}
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

  function renderOverview() {
    const student = current!.applicantType === 'Student';
    const latestExpertMessage = [...current!.messages].reverse().find((message) => message.author === 'Expert');
    const messageCount = current!.messages.length;
    const recommendationCount = current!.recommendations.length;
    const attachmentCount = current!.attachments.length;

    return (
      <section className="appeal-section appeal-overview" aria-labelledby="overview-heading">
        <section className={`appeal-guidance${guidance!.actionRequired ? ' appeal-guidance--action' : ''}`} aria-live="polite">
          <p className="appeal-guidance__label">
            {guidance!.actionRequired
              ? student ? 'Тебе нужно сделать один шаг' : 'От вас требуется действие'
              : student ? 'Что происходит сейчас' : 'Что происходит сейчас'}
          </p>
          <h2 id="overview-heading">{guidance!.title}</h2>
          <p className="appeal-guidance__description">{guidance!.description}</p>
          {permissions?.canReply && latestExpertMessage ? (
            <section className="appeal-current-question" aria-labelledby="current-question-heading">
              <h3 id="current-question-heading">Последний вопрос специалиста</h3>
              <p>{latestExpertMessage.body}</p>
            </section>
          ) : null}
          {guidance!.actionRoute && guidance!.actionLabel ? (
            <md-filled-button onClick={() => navigate(guidance!.actionRoute!)}>{guidance!.actionLabel}</md-filled-button>
          ) : null}
        </section>

        <section className="appeal-next-step" aria-labelledby="next-step-heading">
          <h3 id="next-step-heading">Что будет дальше</h3>
          <p>{guidance!.next}</p>
        </section>

        {guidance!.journey.length > 0 ? (
          <section className="appeal-journey" aria-labelledby="journey-heading">
            <header>
              <h3 id="journey-heading">Как обращение проходит путь</h3>
              <p>{student
                ? 'Тебе не нужно запоминать статусы. Здесь всегда отмечен текущий этап.'
                : 'Не нужно запоминать названия статусов. Здесь всегда отмечен текущий этап.'}</p>
            </header>
            <ol>
              {guidance!.journey.map((step) => (
                <li className={`appeal-journey__step appeal-journey__step--${step.state}`} key={step.title}>
                  <div>
                    <strong>{step.title}</strong>
                    <span>{step.description}</span>
                  </div>
                  <span className="appeal-journey__state">
                    {step.state === 'done' ? 'Готово' : step.state === 'current' ? 'Сейчас' : 'Дальше'}
                  </span>
                </li>
              ))}
            </ol>
          </section>
        ) : null}

        <section className="appeal-contents" aria-labelledby="contents-heading">
          <header>
            <h3 id="contents-heading">{student ? 'Что уже есть в твоём обращении' : 'Что уже есть в обращении'}</h3>
            <p>{student
              ? 'Ничего не исчезает: твой рассказ, переписка, ответ и файлы хранятся в своих разделах.'
              : 'Ничего не исчезает: исходный текст, переписка, ответ и файлы хранятся в своих разделах.'}</p>
          </header>
          <article className="appeal-story">
            <h4>{student ? 'Что ты написал сначала' : 'Исходный текст обращения'}</h4>
            <p>{current!.narrative || (student ? 'Твой рассказ сохранён в ответах формы.' : 'Исходные сведения сохранены в ответах формы.')}</p>
            {current!.category ? <span>Тема: {current!.category}</span> : null}
          </article>
          <div className="appeal-content-links">
            <Link className="appeal-content-link" to="/appeal/dialog">
              <span><strong>Переписка</strong><small>{messageCount > 0 ? countLabel(messageCount, ['сообщение', 'сообщения', 'сообщений']) : 'Специалист пока ничего не написал'}{permissions?.canReply ? ' · нужен ответ' : ''}</small></span>
              <span>Открыть</span>
            </Link>
            <Link className="appeal-content-link" to="/appeal/answer">
              <span><strong>Ответ специалиста</strong><small>{recommendationCount > 0 ? `${countLabel(recommendationCount, ['ответ', 'ответа', 'ответов'])} готово` : 'Ответ ещё готовится'}</small></span>
              <span>Открыть</span>
            </Link>
            <Link className="appeal-content-link" to="/appeal/history">
              <span><strong>Вся история</strong><small>{status!.cycleCount > 1 ? countLabel(status!.cycleCount, ['часть обращения', 'части обращения', 'частей обращения']) : 'Все изменения по порядку'}</small></span>
              <span>Открыть</span>
            </Link>
            {attachmentCount > 0 ? (
              <Link className="appeal-content-link" to="/appeal/dialog">
                <span><strong>Файлы</strong><small>{countLabel(attachmentCount, ['файл', 'файла', 'файлов'])}</small></span>
                <span>Открыть</span>
              </Link>
            ) : (
              <div className="appeal-content-link appeal-content-link--static">
                <span><strong>Файлы</strong><small>К этой части обращения файлы не прикреплены</small></span>
                <span>Нет файлов</span>
              </div>
            )}
          </div>
        </section>
      </section>
    );
  }

  function renderDialog() {
    const student = current!.applicantType === 'Student';
    return (
      <section className="appeal-section public-chat" aria-labelledby="dialog-heading">
        <header>
          <p className="eyebrow">Вопросы и ответы</p>
          <h2 id="dialog-heading">Переписка со специалистом</h2>
          <p>{permissions?.canReply
            ? student
              ? 'Ниже есть вопрос специалиста. Напиши только то, чем готов поделиться.'
              : 'Ниже есть вопрос специалиста. Напишите только то, чем готовы поделиться.'
            : student
              ? 'Здесь хранятся вопросы специалиста и твои ответы. Когда понадобится ответ, это будет заметно на главном экране.'
              : 'Здесь хранятся вопросы специалиста и ваши ответы. Когда понадобится ответ, это будет заметно на главном экране.'}</p>
        </header>
        {current!.narrative ? (
          <article className="appeal-opening-message">
            <strong>{student ? 'Что ты написал сначала' : 'Исходный текст обращения'}</strong>
            <p>{current!.narrative}</p>
          </article>
        ) : null}
        <div className="public-messages" aria-live="polite">
          {current!.messages.length === 0 ? <p className="empty-state">{student
            ? 'Специалист пока ничего не написал. Твой рассказ уже у команды — сейчас отвечать не нужно.'
            : 'Специалист пока ничего не написал. Обращение уже у команды — сейчас отвечать не нужно.'}</p> : null}
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
              <span>{student ? 'Твой ответ' : 'Ваш ответ'}</span>
              <textarea maxLength={4000} minLength={2} required value={replyBody} onChange={(event) => setReplyBody(event.target.value)} />
            </label>
            <md-filled-button type="submit" disabled={replyMutation.isPending || replyBody.trim().length < 2 || !crisisContactValid}>
              {replyMutation.isPending ? 'Отправляем…' : 'Отправить ответ'}
            </md-filled-button>
            {replyMutation.isError ? <p className="gentle-error" role="alert">{publicAppealError(replyMutation.error)}</p> : null}
          </form>
        ) : (
          <div className="appeal-next-action">
            <strong>{student ? 'Сейчас от тебя ничего не нужно' : 'Сейчас от вас ничего не требуется'}</strong>
            <p>{student ? 'Когда специалист задаст вопрос, мы покажем это на главном экране.' : 'Когда специалист задаст вопрос, это появится на главном экране.'}</p>
            {current!.recommendations.length > 0 ? <Link to="/appeal/answer">Перейти к ответу специалиста</Link> : null}
          </div>
        )}
        {current!.attachments.length > 0 ? renderAttachments(current!) : null}
      </section>
    );
  }

  function renderAnswer() {
    const student = current!.applicantType === 'Student';
    return (
      <section className="appeal-section" aria-labelledby="answer-heading">
        <header>
          <p className="eyebrow">Помощь специалиста</p>
          <h2 id="answer-heading">Ответ специалиста</h2>
          <p>{current!.recommendations.length > 0
            ? student
              ? 'Ответ сохранён здесь. Прочитай его спокойно — после этого можно решить, достаточно ли помощи.'
              : 'Ответ сохранён здесь. После ознакомления можно указать, достаточно ли помощи.'
            : student
              ? 'Ответа пока нет. Специалист ещё разбирается в ситуации, а на главном экране видно текущий этап.'
              : 'Ответа пока нет. Специалист ещё работает с обращением; текущий этап виден на главном экране.'}</p>
        </header>
        <div className="public-recommendations">
          {current!.recommendations.length === 0 ? (
            <div className="appeal-answer-empty">
              <p>{student ? 'Тебе не нужно проверять эту страницу: когда ответ появится, главное действие в обращении изменится.' : 'Не нужно постоянно проверять эту страницу: когда ответ появится, главное действие в обращении изменится.'}</p>
              <md-outlined-button onClick={() => navigate('/appeal/overview')}>Вернуться к главному</md-outlined-button>
            </div>
          ) : null}
          {current!.recommendations.map((recommendation, index) => (
            <article className="public-recommendation" key={recommendation.id}>
              <strong>{index === 0 ? 'Ответ специалиста' : `Обновлённый ответ ${index + 1}`}</strong>
              <p>{recommendation.body}</p>
              <time dateTime={recommendation.createdAt}>{formatDate(recommendation.createdAt)}</time>
            </article>
          ))}
        </div>
        {current!.resolution ? <section className="public-resolution"><h3>Итог сервиса</h3><p>{current!.resolution}</p></section> : null}
        {permissions?.canMarkOutcome ? (
          <section className="public-outcome" aria-labelledby="outcome-heading">
            <h3 id="outcome-heading">{student ? 'Помог ли тебе этот ответ?' : 'Помог ли вам этот ответ?'}</h3>
            <p>{student ? 'Если помощи недостаточно, обращение не исчезнет — команда продолжит работу с той же историей.' : 'Если помощи недостаточно, обращение не исчезнет — команда продолжит работу с той же историей.'}</p>
            <div className="action-row">
              <md-outlined-button disabled={helpedMutation.isPending || returnMutation.isPending} onClick={() => helpedMutation.mutate()}>{student ? 'Да, этого достаточно' : 'Да, помощи достаточно'}</md-outlined-button>
              <md-outlined-button disabled={helpedMutation.isPending || returnMutation.isPending} onClick={() => setReturnMode(true)}>Нет, нужна ещё помощь</md-outlined-button>
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
        {permissions?.requiresFinalOperatorDecision ? <p className="public-lifecycle-note">{student ? 'Оператор снова проверяет ситуацию. Обращение и вся история у него — от тебя пока ничего не нужно.' : 'Оператор снова проверяет ситуацию. Обращение и вся история доступны ему — от вас пока ничего не требуется.'}</p> : null}
        {permissions?.canLeaveFeedback ? (
          <form className="outcome-form" onSubmit={(event) => { event.preventDefault(); feedbackMutation.mutate(); }}>
            <h3>{student ? 'Оцени полученную помощь' : 'Оцените полученную помощь'}</h3>
            <label className="select-field"><span>Оценка</span><select value={score} onChange={(event) => setScore(Number(event.target.value))}><option value={5}>5 — очень помогло</option><option value={4}>4</option><option value={3}>3</option><option value={2}>2</option><option value={1}>1 — не помогло</option></select></label>
            <label className="text-field"><span>Комментарий (необязательно)</span><textarea maxLength={2000} value={feedbackComment} onChange={(event) => setFeedbackComment(event.target.value)} /></label>
            <md-filled-button type="submit" disabled={feedbackMutation.isPending}>Отправить оценку</md-filled-button>
          </form>
        ) : null}
        {permissions?.feedbackSubmitted ? <p className="public-lifecycle-note">Спасибо, оценка сохранена.</p> : null}
        {current!.recommendations.length > 0 && !permissions?.complaintSubmitted ? (
          <section className="public-complaint">
            <h3>Если специалист поступил неправильно</h3>
            <p>{student ? 'Можно отдельно сообщить об этом оператору. Специалист не увидит текст сообщения.' : 'Об этом можно отдельно сообщить оператору. Специалист не увидит текст сообщения.'}</p>
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
    const student = current!.applicantType === 'Student';
    return (
      <section className="appeal-section" aria-labelledby="history-heading">
        <header>
          <p className="eyebrow">Всё сохранено под одним трек-номером</p>
          <h2 id="history-heading">История обращения</h2>
          <p>{student
            ? 'Здесь по порядку видно, что происходило. Если ты просил помочь снова, прошлые сообщения и ответы всё равно остаются.'
            : 'Здесь по порядку видно, что происходило. Каждое продолжение сохраняется, а прошлые сообщения и ответы не исчезают.'}</p>
        </header>
        <div className="appeal-cycle-list">
          {[...status!.cycles].reverse().map((cycle) => (
            <details className="appeal-cycle" key={cycle.id} open={cycle.id === current!.id}>
              <summary>
                <span><strong>{status!.cycleCount === 1 ? 'Первое обращение' : `Часть обращения ${cycle.number}`}</strong><small>{formatDate(cycle.createdAt)}</small></span>
                <span>{cycle.statusText}</span>
              </summary>
              <div className="appeal-cycle__content">
                {cycle.narrative ? <section><h3>{student ? 'Что ты рассказал' : 'С чего началась эта часть'}</h3><p>{cycle.narrative}</p></section> : null}
                {cycle.messages.length > 0 ? <section><h3>Переписка</h3>{cycle.messages.map((message) => <article className="appeal-cycle__message" key={message.id}><strong>{message.authorLabel}</strong><p>{message.body}</p><time dateTime={message.createdAt}>{formatDate(message.createdAt)}</time></article>)}</section> : null}
                {cycle.recommendations.length > 0 ? <section><h3>Ответы специалиста</h3>{cycle.recommendations.map((recommendation) => <article className="appeal-cycle__message" key={recommendation.id}><p>{recommendation.body}</p><time dateTime={recommendation.createdAt}>{formatDate(recommendation.createdAt)}</time></article>)}</section> : null}
                <section><h3>Что происходило</h3><div className="status-timeline">{cycle.timeline.map((item) => <div className="status-timeline__item" key={`${cycle.id}-${item.status}-${item.at}`}><strong>{item.text}</strong><span>{formatDate(item.at)}</span></div>)}</div></section>
                {cycle.attachments.length > 0 ? renderAttachments(cycle) : null}
              </div>
            </details>
          ))}
        </div>
        <section className="continuation-choice" aria-labelledby="continuation-heading">
          <h3 id="continuation-heading">{student ? 'Что ты хочешь сделать?' : 'Что вы хотите сделать?'}</h3>
          {permissions?.canContinue ? (
            <>
              <p><strong>Та же ситуация продолжается?</strong> {student ? 'Расскажи, что изменилось. Трек-номер и вся история сохранятся.' : 'Расскажите, что изменилось. Трек-номер и вся история сохранятся.'}</p>
              {!continuationMode ? <md-filled-button onClick={() => setContinuationMode(true)}>Продолжить это обращение</md-filled-button> : (
                <form className="outcome-form" onSubmit={(event) => { event.preventDefault(); continuationMutation.mutate(); }}>
                  <label className="text-field"><span>Что изменилось или повторилось</span><textarea minLength={10} maxLength={10000} required value={continuationBody} onChange={(event) => setContinuationBody(event.target.value)} /></label>
                  <div className="action-row"><md-filled-button type="submit" disabled={continuationMutation.isPending || continuationBody.trim().length < 10 || !crisisContactValid}>{continuationMutation.isPending ? 'Сохраняем…' : 'Продолжить обращение'}</md-filled-button><md-outlined-button type="button" onClick={() => setContinuationMode(false)}>Отмена</md-outlined-button></div>
                  {continuationMutation.isError ? <p className="gentle-error" role="alert">{publicAppealError(continuationMutation.error)}</p> : null}
                </form>
              )}
            </>
          ) : <p>{student ? 'Сейчас обращение ещё открыто. Ответь в переписке, только когда специалист попросит уточнение.' : 'Сейчас обращение ещё открыто. Ответьте в переписке, когда специалист попросит уточнение.'}</p>}
          <p><strong>Это другая ситуация?</strong> {student ? 'Создай отдельное обращение с новым трек-номером.' : 'Создайте отдельное обращение с новым трек-номером.'}</p>
          <Link className="secondary-link" to="/appeal/new">Создать другое обращение</Link>
        </section>
      </section>
    );
  }

  function renderAccess() {
    const student = current?.applicantType === 'Student';
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
            <p>{student ? 'Удали локальный доступ.' : 'Удалите локальный доступ.'} Вернуться после этого можно только по сохранённому трек-номеру.</p>
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
  return value === 'access' || workspaceLabels.some((workspace) => workspace.key === value);
}

function countLabel(value: number, forms: [string, string, string]) {
  const lastTwo = value % 100;
  const last = value % 10;
  const form = lastTwo >= 11 && lastTwo <= 14
    ? forms[2]
    : last === 1 ? forms[0] : last >= 2 && last <= 4 ? forms[1] : forms[2];
  return `${value} ${form}`;
}

function formatFileSize(size: number) {
  if (size < 1024 * 1024) return `${Math.max(1, Math.round(size / 1024))} КБ`;
  return `${(size / 1024 / 1024).toFixed(1)} МБ`;
}

function formatDate(value: string) {
  return new Intl.DateTimeFormat('ru-RU', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value));
}
