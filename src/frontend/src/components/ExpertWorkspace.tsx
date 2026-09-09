import '@material/web/button/filled-button.js';
import '@material/web/button/outlined-button.js';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { FormEvent, useEffect, useState } from 'react';
import { useNavigate, useParams, useSearchParams } from 'react-router-dom';
import { createAppealUpdatesConnection } from '../api/appealUpdates';
import {
  acceptExpertAppeal,
  acquireExpertComposer,
  addExpertNote,
  askExpertQuestion,
  createExpertWorkflowRequest,
  downloadExpertAttachment,
  getExpertAppeal,
  getExpertAppeals,
  getExpertPresence,
  heartbeatExpertPresence,
  publishExpertRecommendation,
  releaseExpertComposer,
  type ExpertAppealFilters,
  type ExpertAppealItem,
} from '../api/expertAppeals';
import { ResponsiveMasterDetail } from './ux/WorkspacePrimitives';

const initialFilters: ExpertAppealFilters = { status: '', priority: '', categoryId: '' };
type ExpertSection = 'inbox' | 'active' | 'waiting' | 'completed' | 'case';

const sectionPresentation: Record<Exclude<ExpertSection, 'case'>, { title: string; status: string }> = {
  inbox: { title: 'Новые назначения', status: 'Assigned' },
  active: { title: 'В работе', status: 'InProgress' },
  waiting: { title: 'Ждут заявителя', status: 'NeedsClarification' },
  completed: { title: 'Завершённые обращения', status: '' },
};

export function ExpertWorkspace({ section }: { section: ExpertSection }) {
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const { appealId: selectedId } = useParams();
  const [searchParams, setSearchParams] = useSearchParams();
  const sourceSection = searchParams.get('from') ?? 'inbox';
  const listSection = section === 'case' ? sourceSection : section;
  const knownSection = listSection in sectionPresentation
    ? listSection as Exclude<ExpertSection, 'case'>
    : 'inbox';
  const filters: ExpertAppealFilters = {
    ...initialFilters,
    status: searchParams.get('status') ?? sectionPresentation[knownSection].status,
    priority: searchParams.get('priority') ?? '',
    categoryId: searchParams.get('categoryId') ?? '',
  };
  const appealsQuery = useQuery({
    queryKey: ['expert-appeals', filters],
    queryFn: () => getExpertAppeals(filters),
  });
  const detailQuery = useQuery({
    queryKey: ['expert-appeal', selectedId],
    queryFn: () => getExpertAppeal(selectedId!),
    enabled: Boolean(selectedId),
  });
  const acceptMutation = useMutation({
    mutationFn: () => acceptExpertAppeal(selectedId!, detailQuery.data!.version),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['expert-appeal', selectedId] });
      await queryClient.invalidateQueries({ queryKey: ['expert-appeals'] });
    },
  });

  useEffect(() => {
    if (!selectedId) return undefined;
    const connection = createAppealUpdatesConnection(() => {
      void queryClient.invalidateQueries({ queryKey: ['expert-appeal', selectedId] });
      void queryClient.invalidateQueries({ queryKey: ['expert-appeals'] });
    });
    connection.onreconnected(() => connection.invoke('JoinStaffAppeal', selectedId).catch(() => undefined));
    void connection.start()
      .then(() => connection.invoke('JoinStaffAppeal', selectedId))
      .catch(() => undefined);
    return () => { void connection.stop(); };
  }, [queryClient, selectedId]);

  function updateFilter(name: keyof ExpertAppealFilters, value: string) {
    const next = new URLSearchParams(searchParams);
    if (value) next.set(name, value);
    else next.delete(name);
    setSearchParams(next, { replace: true });
  }

  function selectAppeal(appeal: ExpertAppealItem | null) {
    if (!appeal) {
      navigate(`/staff/expert/${knownSection}`);
      return;
    }
    const next = new URLSearchParams();
    next.set('from', knownSection);
    navigate(`/staff/expert/cases/${appeal.id}/overview?${next.toString()}`);
  }

  return (
    <ResponsiveMasterDetail className="expert-workspace" hasDetail={Boolean(selectedId)}>
      <section className="expert-cases" aria-labelledby="expert-cases-heading">
        <div className="operator-heading">
          <div>
            <p className="eyebrow">Кабинет эксперта</p>
            <h1 id="expert-cases-heading" className="staff-title">{sectionPresentation[knownSection].title}</h1>
          </div>
          <p className="queue-counts" aria-label="Сводка назначенных обращений">
            <span><strong>{appealsQuery.data?.total ?? 0}</strong> назначено вам</span>
          </p>
        </div>

        <div className="expert-filters" aria-label="Фильтры обращений">
          <label className="select-field">
            <span>Статус</span>
            <select value={filters.status} onChange={(event) => updateFilter('status', event.target.value)}>
              <option value="">Все</option>
              {appealsQuery.data?.filters.statuses.map((filter) => (
                <option key={filter.value} value={filter.value}>{filter.label}</option>
              ))}
            </select>
          </label>
          <label className="select-field">
            <span>Приоритет</span>
            <select value={filters.priority} onChange={(event) => updateFilter('priority', event.target.value)}>
              <option value="">Все</option>
              {appealsQuery.data?.filters.priorities.map((filter) => (
                <option key={filter.value} value={filter.value}>{filter.label}</option>
              ))}
            </select>
          </label>
          <label className="select-field">
            <span>Категория</span>
            <select value={filters.categoryId} onChange={(event) => updateFilter('categoryId', event.target.value)}>
              <option value="">Все</option>
              {appealsQuery.data?.filters.categories.map((category) => (
                <option key={category.id} value={category.id}>{category.displayName}</option>
              ))}
            </select>
          </label>
        </div>

        {appealsQuery.isError ? (
          <p className="operator-notice" role="alert">Не удалось загрузить назначения. Обновите страницу.</p>
        ) : null}
        <div className="case-list" aria-label="Назначенные обращения">
          {appealsQuery.isPending ? <p className="operator-message">Загружаем обращения…</p> : null}
          {appealsQuery.data?.items.map((appeal) => (
            <button
              className={`case-row${selectedId === appeal.id ? ' case-row--selected' : ''}`}
              key={appeal.id}
              type="button"
              aria-pressed={selectedId === appeal.id}
              onClick={() => selectAppeal(appeal)}
            >
              <span className="case-row__top">
                <strong>{appeal.applicantTypeText}</strong>
                <time dateTime={appeal.assignedAt ?? appeal.receivedAt}>{formatAssignedAt(appeal.assignedAt)}</time>
              </span>
              <span>{appeal.category}</span>
              <span>{appeal.statusText} · {appeal.priorityText}</span>
            </button>
          ))}
          {appealsQuery.data?.items.length === 0 ? (
            <p className="operator-message">По выбранным фильтрам обращений нет.</p>
          ) : null}
        </div>
      </section>

      <section className="expert-case-detail" aria-label="Рабочая карточка обращения">
        {selectedId ? (
          <>
            <button className="operator-back" type="button" onClick={() => selectAppeal(null)}>
              Вернуться к обращениям
            </button>
            {detailQuery.isPending ? <p className="operator-message">Загружаем карточку…</p> : null}
            {detailQuery.isError ? (
              <p className="operator-notice" role="alert">Карточка недоступна или назначение изменилось.</p>
            ) : null}
            {detailQuery.data ? (
              <ExpertCaseDetail
                detail={detailQuery.data}
                accepting={acceptMutation.isPending}
                acceptError={acceptMutation.isError}
                onAccept={() => acceptMutation.mutate()}
              />
            ) : null}
          </>
        ) : (
          <div className="operator-empty">
            <p className="eyebrow">Рабочая карточка</p>
            <h2>Выберите назначенное обращение</h2>
            <p>Здесь будут исходная ситуация, диалог, внутренние заметки и рекомендации.</p>
          </div>
        )}
      </section>
    </ResponsiveMasterDetail>
  );
}

function ExpertCaseDetail({
  detail,
  accepting,
  acceptError,
  onAccept,
}: {
  detail: Awaited<ReturnType<typeof getExpertAppeal>>;
  accepting: boolean;
  acceptError: boolean;
  onAccept: () => void;
}) {
  const queryClient = useQueryClient();
  const [noteBody, setNoteBody] = useState('');
  const [clientNoteId, setClientNoteId] = useState(() => crypto.randomUUID());
  const [questionBody, setQuestionBody] = useState('');
  const [clientMessageId, setClientMessageId] = useState(() => crypto.randomUUID());
  const [recommendationBody, setRecommendationBody] = useState('');
  const [clientRecommendationId, setClientRecommendationId] = useState(() => crypto.randomUUID());
  const [workflowType, setWorkflowType] = useState<'Transfer' | 'CoExecutor' | 'PriorityReview'>('CoExecutor');
  const [workflowReason, setWorkflowReason] = useState('');
  const [clientWorkflowId, setClientWorkflowId] = useState(() => crypto.randomUUID());
  const [leaseId] = useState(() => crypto.randomUUID());
  const presenceQuery = useQuery({
    queryKey: ['expert-presence', detail.id],
    queryFn: () => getExpertPresence(detail.id),
    refetchInterval: 5_000,
  });
  const heartbeatMutation = useMutation({
    mutationFn: () => heartbeatExpertPresence(detail.id),
    onSuccess: (presence) => queryClient.setQueryData(['expert-presence', detail.id], presence),
  });
  const acquireMutation = useMutation({
    mutationFn: () => acquireExpertComposer(detail.id, leaseId),
    onSuccess: (presence) => queryClient.setQueryData(['expert-presence', detail.id], presence),
    onError: () => void queryClient.invalidateQueries({ queryKey: ['expert-presence', detail.id] }),
  });
  const workflowMutation = useMutation({
    mutationFn: () => createExpertWorkflowRequest(detail.id, {
      clientRequestId: clientWorkflowId,
      type: workflowType,
      reason: workflowReason,
    }),
    onSuccess: async () => {
      setWorkflowReason('');
      setClientWorkflowId(crypto.randomUUID());
      await queryClient.invalidateQueries({ queryKey: ['expert-appeal', detail.id] });
    },
  });
  useEffect(() => {
    heartbeatMutation.mutate();
    const heartbeat = window.setInterval(() => heartbeatMutation.mutate(), 15_000);
    return () => window.clearInterval(heartbeat);
  }, [detail.id]);

  const collaborationActive = detail.participants.length > 1;
  const ownsComposer = presenceQuery.data?.composer?.isCurrent === true;
  const composerOwner = presenceQuery.data?.composer && !presenceQuery.data.composer.isCurrent
    ? presenceQuery.data.composer.expertName
    : null;
  const canCompose = !collaborationActive || ownsComposer;

  useEffect(() => {
    if (!collaborationActive || !ownsComposer) return undefined;
    const renewal = window.setInterval(() => {
      void acquireExpertComposer(detail.id, leaseId).then((presence) => {
        queryClient.setQueryData(['expert-presence', detail.id], presence);
      }).catch(() => {
        void queryClient.invalidateQueries({ queryKey: ['expert-presence', detail.id] });
      });
    }, 15_000);
    return () => window.clearInterval(renewal);
  }, [collaborationActive, detail.id, leaseId, ownsComposer, queryClient]);

  function openComposer() {
    if (collaborationActive && !ownsComposer && !acquireMutation.isPending) acquireMutation.mutate();
  }

  function closeComposer() {
    if (!collaborationActive || !ownsComposer) return;
    void releaseExpertComposer(detail.id, leaseId).finally(() => {
      void queryClient.invalidateQueries({ queryKey: ['expert-presence', detail.id] });
    });
  }
  const noteMutation = useMutation({
    mutationFn: () => addExpertNote(detail.id, clientNoteId, noteBody, collaborationActive ? leaseId : undefined),
    onSuccess: async () => {
      setNoteBody('');
      setClientNoteId(crypto.randomUUID());
      closeComposer();
      await queryClient.invalidateQueries({ queryKey: ['expert-appeal', detail.id] });
    },
  });
  const downloadMutation = useMutation({
    mutationFn: (attachment: (typeof detail.attachments)[number]) =>
      downloadExpertAttachment(detail.id, attachment),
  });
  const questionMutation = useMutation({
    mutationFn: () => askExpertQuestion(
      detail.id,
      clientMessageId,
      questionBody,
      detail.version,
      collaborationActive ? leaseId : undefined,
    ),
    onSuccess: async () => {
      setQuestionBody('');
      setClientMessageId(crypto.randomUUID());
      closeComposer();
      await queryClient.invalidateQueries({ queryKey: ['expert-appeal', detail.id] });
      await queryClient.invalidateQueries({ queryKey: ['expert-appeals'] });
    },
  });
  const recommendationMutation = useMutation({
    mutationFn: () => publishExpertRecommendation(
      detail.id,
      clientRecommendationId,
      recommendationBody,
      detail.version,
      collaborationActive ? leaseId : undefined,
    ),
    onSuccess: async () => {
      setRecommendationBody('');
      setClientRecommendationId(crypto.randomUUID());
      closeComposer();
      await queryClient.invalidateQueries({ queryKey: ['expert-appeal', detail.id] });
      await queryClient.invalidateQueries({ queryKey: ['expert-appeals'] });
    },
  });

  function submitNote(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    noteMutation.mutate();
  }

  function submitQuestion(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    questionMutation.mutate();
  }

  function submitRecommendation(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    recommendationMutation.mutate();
  }

  return (
    <div className="expert-detail-content">
      <div className="operator-detail__heading">
        <div>
          <p className="eyebrow">{detail.statusText}</p>
          <h2 className="operator-detail__title">{detail.applicantTypeText}</h2>
        </div>
        <span>{formatDate(detail.receivedAt)}</span>
      </div>
      <p className="operator-message">{detail.category} · {detail.priorityText}</p>

      <section className="expert-collaboration" aria-labelledby="expert-collaboration-heading">
        <p className="eyebrow">{detail.roleText}</p>
        <h3 id="expert-collaboration-heading">Участники и передача</h3>
        <div className="participant-list">
          {detail.participants.map((participant) => (
            <p key={participant.id}><strong>{participant.displayName}</strong><span>{participant.roleText}</span></p>
          ))}
        </div>
        <p className="expert-zone-help" aria-live="polite">
          {presenceQuery.data?.active.length
            ? `Сейчас в карточке: ${presenceQuery.data.active.map((person) => person.isCurrent ? 'вы' : person.displayName).join(', ')}.`
            : 'Других специалистов в карточке сейчас нет.'}
        </p>
        {composerOwner ? (
          <p className="composer-lock" role="status">{composerOwner} сейчас готовит запись. Редактор освободится автоматически.</p>
        ) : null}
        <form className="workflow-request" onSubmit={(event) => { event.preventDefault(); workflowMutation.mutate(); }}>
          <label className="select-field">
            <span>Запрос оператору</span>
            <select value={workflowType} onChange={(event) => setWorkflowType(event.target.value as typeof workflowType)}>
              <option value="CoExecutor">Добавить соисполнителя</option>
              <option value="Transfer">Передать обращение</option>
              <option value="PriorityReview">Пересмотреть приоритет</option>
            </select>
          </label>
          <label className="text-field">
            <span>Причина запроса</span>
            <textarea maxLength={1000} minLength={10} required value={workflowReason} onChange={(event) => setWorkflowReason(event.target.value)} />
          </label>
          <md-outlined-button type="submit" disabled={workflowMutation.isPending || workflowReason.trim().length < 10}>
            {workflowMutation.isPending ? 'Отправляем…' : 'Отправить оператору'}
          </md-outlined-button>
          {workflowMutation.isError ? <p className="gentle-error" role="alert">Не удалось отправить запрос. Проверьте, нет ли уже ожидающего запроса этого типа.</p> : null}
        </form>
        {detail.workflowRequests.length > 0 ? (
          <div className="workflow-history" aria-label="История запросов">
            {detail.workflowRequests.map((request) => (
              <p key={request.id}><strong>{request.typeText}</strong><span>{request.statusText} · {formatDate(request.requestedAt)}</span></p>
            ))}
          </div>
        ) : null}
        {detail.assignmentHistory.length > 0 ? (
          <details className="assignment-history">
            <summary>История участников</summary>
            <div>
              {detail.assignmentHistory.map((item) => (
                <p key={item.id}>{item.eventType === 'Removed' ? 'Участие завершено' : 'Добавлен'}: {item.displayName}, {item.roleText.toLowerCase()} · {formatDate(item.occurredAt)}</p>
              ))}
            </div>
          </details>
        ) : null}
      </section>

      {detail.status === 'Assigned' ? (
        <div className="expert-accept">
          <p>Подтвердите, что берете обращение в работу.</p>
          <md-filled-button disabled={accepting} onClick={onAccept}>
            {accepting ? 'Принимаем…' : 'Взять в работу'}
          </md-filled-button>
          {acceptError ? <p className="gentle-error" role="alert">Статус уже изменился. Обновите карточку.</p> : null}
        </div>
      ) : null}

      <section className="detail-section" aria-labelledby="expert-initial-heading">
        <h3 id="expert-initial-heading">Первоначальное обращение</h3>
        {detail.narrative ? <p className="appeal-narrative">{detail.narrative}</p> : <p>Рассказ не добавлен.</p>}
        {detail.answers.length > 0 ? (
          <dl className="appeal-answers">
            {detail.answers.map((answer) => (
              <div key={answer.questionCode}>
                <dt>{answer.question}</dt>
                <dd>{answer.value}</dd>
              </div>
            ))}
          </dl>
        ) : null}
        {detail.attachments.length > 0 ? (
          <div className="expert-attachments" aria-label="Вложения обращения">
            {detail.attachments.map((attachment) => (
              <div className="expert-attachment" key={attachment.id}>
                <span>{attachment.displayName}</span>
                <md-outlined-button
                  disabled={downloadMutation.isPending}
                  onClick={() => downloadMutation.mutate(attachment)}
                >
                  Скачать
                </md-outlined-button>
              </div>
            ))}
          </div>
        ) : null}
      </section>

      <section className="expert-chat" aria-labelledby="expert-chat-heading">
        <p className="eyebrow">Видно заявителю</p>
        <h3 id="expert-chat-heading">Диалог</h3>
        <p className="expert-zone-help">Ваше имя не показывается — сообщения подписаны как «Специалист».</p>
        <div className="expert-messages" aria-live="polite">
          {detail.messages.length === 0 ? <p className="expert-zone-help">Сообщений пока нет.</p> : null}
          {detail.messages.map((message) => (
            <article className={`expert-message expert-message--${message.author.toLowerCase()}`} key={message.id}>
              <strong>{message.authorLabel}</strong>
              <p>{message.body}</p>
              <time dateTime={message.createdAt}>{formatDate(message.createdAt)}</time>
            </article>
          ))}
        </div>
        {detail.status === 'InProgress' ? (
          <form className="expert-compose" onSubmit={submitQuestion}>
            <label className="text-field">
              <span>Вопрос заявителю</span>
              <textarea
                maxLength={4000}
                minLength={2}
                required
                value={questionBody}
                onFocus={openComposer}
                onChange={(event) => setQuestionBody(event.target.value)}
              />
            </label>
            <md-filled-button
              type="submit"
              disabled={questionMutation.isPending || questionBody.trim().length < 2 || !canCompose}
            >
              {questionMutation.isPending ? 'Отправляем…' : 'Задать вопрос'}
            </md-filled-button>
            {questionMutation.isError ? (
              <p className="gentle-error" role="alert">Не удалось отправить вопрос. Обновите карточку.</p>
            ) : null}
          </form>
        ) : null}
        {detail.status === 'NeedsClarification' ? (
          <p className="operator-message">Ждем ответа заявителя. После ответа обращение вернется в работу.</p>
        ) : null}
      </section>

      <section className="expert-notes" aria-labelledby="expert-notes-heading">
        <p className="eyebrow">Только для участников обращения</p>
        <h3 id="expert-notes-heading">Внутренние заметки</h3>
        <p className="expert-zone-help">Заявитель, оператор и администратор не видят этот раздел.</p>
        {detail.notes.map((note) => (
          <article className="expert-note" key={note.id}>
            <p>{note.body}</p>
            <time dateTime={note.createdAt}>{formatDate(note.createdAt)}</time>
          </article>
        ))}
        <form className="expert-compose" onSubmit={submitNote}>
          <label className="text-field">
            <span>Новая внутренняя заметка</span>
            <textarea
              maxLength={4000}
              minLength={2}
              required
              value={noteBody}
              onFocus={openComposer}
              onChange={(event) => setNoteBody(event.target.value)}
            />
          </label>
          <md-outlined-button type="submit" disabled={noteMutation.isPending || noteBody.trim().length < 2 || !canCompose}>
            {noteMutation.isPending ? 'Сохраняем…' : 'Сохранить заметку'}
          </md-outlined-button>
          {noteMutation.isError ? <p className="gentle-error" role="alert">Не удалось сохранить заметку.</p> : null}
        </form>
      </section>

      <section className="expert-recommendations" aria-labelledby="expert-recommendations-heading">
        <p className="eyebrow">Итог для заявителя</p>
        <h3 id="expert-recommendations-heading">Рекомендации</h3>
        {detail.recommendations.map((recommendation) => (
          <article className="expert-recommendation" key={recommendation.id}>
            <strong>Версия {recommendation.version}</strong>
            <p>{recommendation.body}</p>
            <time dateTime={recommendation.createdAt}>{formatDate(recommendation.createdAt)}</time>
          </article>
        ))}
        {detail.status === 'InProgress' && detail.role === 'Responsible' ? (
          <form className="expert-compose" onSubmit={submitRecommendation}>
            <label className="text-field">
              <span>Итоговая рекомендация</span>
              <textarea
                maxLength={10000}
                minLength={10}
                required
                value={recommendationBody}
                onFocus={openComposer}
                onChange={(event) => setRecommendationBody(event.target.value)}
              />
            </label>
            <md-filled-button
              type="submit"
              disabled={recommendationMutation.isPending || recommendationBody.trim().length < 10 || !canCompose}
            >
              {recommendationMutation.isPending ? 'Публикуем…' : 'Опубликовать рекомендацию'}
            </md-filled-button>
            {recommendationMutation.isError ? (
              <p className="gentle-error" role="alert">Не удалось опубликовать рекомендацию. Обновите карточку.</p>
            ) : null}
          </form>
        ) : null}
        {detail.role === 'CoExecutor' ? (
          <p className="expert-zone-help">Итоговую рекомендацию публикует ответственный специалист.</p>
        ) : null}
      </section>
    </div>
  );
}

function formatAssignedAt(value: string | null) {
  if (!value) return 'только что';
  return new Intl.DateTimeFormat('ru-RU', { day: 'numeric', month: 'short' }).format(new Date(value));
}

function formatDate(value: string) {
  return new Intl.DateTimeFormat('ru-RU', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value));
}
