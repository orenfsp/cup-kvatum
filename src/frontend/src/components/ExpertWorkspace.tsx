import '@material/web/button/filled-button.js';
import '@material/web/button/outlined-button.js';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { FormEvent, useEffect, useState } from 'react';
import { Link, useLocation, useNavigate, useParams, useSearchParams } from 'react-router-dom';
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
import { ActionReceipt, ResponsiveMasterDetail, UnsavedChangesGuard } from './ux/WorkspacePrimitives';

const initialFilters: ExpertAppealFilters = { status: '', priority: '', categoryId: '' };
type ExpertSection = 'inbox' | 'active' | 'waiting' | 'completed' | 'case';
type ExpertCaseWorkspace = 'overview' | 'dialog' | 'notes' | 'answer' | 'team';

const expertCaseWorkspaces: Array<{ id: ExpertCaseWorkspace; label: string }> = [
  { id: 'overview', label: 'Обзор' },
  { id: 'dialog', label: 'Диалог' },
  { id: 'notes', label: 'Заметки' },
  { id: 'answer', label: 'Ответ' },
  { id: 'team', label: 'Команда' },
];

const sectionPresentation: Record<Exclude<ExpertSection, 'case'>, { title: string; status: string }> = {
  inbox: { title: 'Новые назначения', status: 'Assigned' },
  active: { title: 'В работе', status: 'InProgress' },
  waiting: { title: 'Ждут заявителя', status: 'NeedsClarification' },
  completed: { title: 'Завершённые', status: 'Completed' },
};

export function ExpertWorkspace({ section }: { section: ExpertSection }) {
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const location = useLocation();
  const { appealId: selectedId, workspace: workspaceParam } = useParams();
  const [searchParams, setSearchParams] = useSearchParams();
  const [movementNotice, setMovementNotice] = useState('');
  const sourceSection = searchParams.get('from') ?? 'inbox';
  const listSection = section === 'case' ? sourceSection : section;
  const knownSection = listSection in sectionPresentation
    ? listSection as Exclude<ExpertSection, 'case'>
    : 'inbox';
  const selectedWorkspace = expertCaseWorkspaces.some((item) => item.id === workspaceParam)
    ? workspaceParam as ExpertCaseWorkspace
    : 'overview';
  const filters: ExpertAppealFilters = {
    ...initialFilters,
    status: searchParams.get('status') ?? sectionPresentation[knownSection].status,
    priority: searchParams.get('priority') ?? '',
    categoryId: searchParams.get('categoryId') ?? '',
  };
  const appealsQuery = useQuery({
    queryKey: ['expert-appeals', filters],
    queryFn: () => getExpertAppeals(filters),
    refetchInterval: 30_000,
  });
  const detailQuery = useQuery({
    queryKey: ['expert-appeal', selectedId],
    queryFn: () => getExpertAppeal(selectedId!),
    enabled: Boolean(selectedId),
    staleTime: 0,
    refetchOnMount: 'always',
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

  useEffect(() => {
    if (!selectedId || workspaceParam === selectedWorkspace) return;
    navigate(`/staff/expert/cases/${selectedId}/${selectedWorkspace}${location.search}`, { replace: true });
  }, [location.search, navigate, selectedId, selectedWorkspace, workspaceParam]);

  useEffect(() => {
    if (!selectedId || !detailQuery.data) return;
    const targetSection = expertSectionForStatus(detailQuery.data.status);
    if (!targetSection || targetSection === knownSection) return;
    const next = new URLSearchParams(searchParams);
    next.set('from', targetSection);
    next.delete('status');
    setMovementNotice(sectionMovementText(knownSection, targetSection));
    navigate(`/staff/expert/cases/${selectedId}/${selectedWorkspace}?${next.toString()}`, { replace: true });
  }, [detailQuery.data, knownSection, navigate, searchParams, selectedId, selectedWorkspace]);

  function updateFilter(name: keyof ExpertAppealFilters, value: string) {
    const next = new URLSearchParams(searchParams);
    if (value) next.set(name, value);
    else next.delete(name);
    setSearchParams(next, { replace: true });
  }

  function selectAppeal(appeal: ExpertAppealItem | null) {
    setMovementNotice('');
    if (!appeal) {
      const listSearch = new URLSearchParams(searchParams);
      listSearch.delete('from');
      navigate({ pathname: `/staff/expert/${knownSection}`, search: listSearch.toString() });
      return;
    }
    const next = new URLSearchParams(searchParams);
    next.set('from', knownSection);
    navigate(`/staff/expert/cases/${appeal.id}/overview?${next.toString()}`);
  }

  return (
    <ResponsiveMasterDetail
      className="expert-workspace"
      hasDetail={Boolean(selectedId)}
      focusKey={selectedId ? `${selectedId}:${selectedWorkspace}` : undefined}
      scrollKey={`expert-${knownSection}`}
    >
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
            <select aria-label="Статус" value={filters.status} onChange={(event) => updateFilter('status', event.target.value)}>
              <option value="">Все</option>
              {appealsQuery.data?.filters.statuses.map((filter) => (
                <option key={filter.value} value={filter.value}>{filter.label}</option>
              ))}
            </select>
          </label>
          <label className="select-field">
            <span>Приоритет</span>
            <select aria-label="Приоритет" value={filters.priority} onChange={(event) => updateFilter('priority', event.target.value)}>
              <option value="">Все</option>
              {appealsQuery.data?.filters.priorities.map((filter) => (
                <option key={filter.value} value={filter.value}>{filter.label}</option>
              ))}
            </select>
          </label>
          <label className="select-field">
            <span>Категория</span>
            <select aria-label="Категория" value={filters.categoryId} onChange={(event) => updateFilter('categoryId', event.target.value)}>
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
        <div className="case-list" aria-label="Назначенные обращения" data-scroll-region>
          {appealsQuery.isPending ? <p className="operator-message">Загружаем обращения…</p> : null}
          {appealsQuery.data?.items.map((appeal) => (
            <button
              className={`case-row${selectedId === appeal.id ? ' case-row--selected' : ''}`}
              key={appeal.id}
              type="button"
              aria-current={selectedId === appeal.id ? 'true' : undefined}
              data-appeal-id={appeal.id}
              onClick={() => selectAppeal(appeal)}
            >
              <span className="case-row__top">
                <strong>{appeal.nextAction}</strong>
                <time dateTime={appeal.assignedAt ?? appeal.receivedAt}>{formatAssignedAt(appeal.assignedAt)}</time>
              </span>
              <span>{appeal.applicantTypeText} · {appeal.category}</span>
              <span>Статус: {appeal.statusText} · {appeal.roleText} · {appeal.priorityText}</span>
            </button>
          ))}
          {appealsQuery.data?.items.length === 0 ? (
            <p className="operator-message">{emptySectionText(knownSection)}</p>
          ) : null}
        </div>
      </section>

      <section className="expert-case-detail" aria-label="Рабочая карточка обращения">
        {selectedId ? (
          <>
            <button className="operator-back" type="button" onClick={() => selectAppeal(null)}>
              Вернуться в «{sectionPresentation[knownSection].title}»
            </button>
            <ActionReceipt message={movementNotice} title="Статус обновлён" />
            {detailQuery.isPending ? <p className="operator-message">Загружаем карточку…</p> : null}
            {detailQuery.isError ? (
              <p className="operator-notice" role="alert">Карточка недоступна или назначение изменилось.</p>
            ) : null}
            {detailQuery.data ? (
              <ExpertCaseDetail
                workspace={selectedWorkspace}
                workspaceSearch={location.search}
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
  workspace,
  workspaceSearch,
  detail,
  accepting,
  acceptError,
  onAccept,
}: {
  workspace: ExpertCaseWorkspace;
  workspaceSearch: string;
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
  const [answerMode, setAnswerMode] = useState<'edit' | 'preview'>('edit');
  const [clientRecommendationId, setClientRecommendationId] = useState(() => crypto.randomUUID());
  const [workflowType, setWorkflowType] = useState<'Transfer' | 'CoExecutor' | 'PriorityReview'>('CoExecutor');
  const [workflowReason, setWorkflowReason] = useState('');
  const [clientWorkflowId, setClientWorkflowId] = useState(() => crypto.randomUUID());
  const [leaseId] = useState(() => crypto.randomUUID());
  const dirty = noteBody.trim().length > 0
    || questionBody.trim().length > 0
    || recommendationBody.trim().length > 0
    || workflowReason.trim().length > 0;
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
    return () => {
      window.clearInterval(heartbeat);
      void releaseExpertComposer(detail.id, leaseId).catch(() => undefined);
    };
  }, [detail.id, leaseId]);

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
      setAnswerMode('edit');
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

  return (
    <div className="expert-detail-content">
      <UnsavedChangesGuard when={dirty} />
      <header className="expert-case-header">
        <div className="expert-case-header__title">
          <div>
            <p className="eyebrow">{detail.roleText} · {detail.specialization}</p>
            <h2 className="operator-detail__title">{detail.applicantTypeText}</h2>
            <p>{detail.category} · {detail.priorityText}</p>
          </div>
          {detail.status === 'Assigned' ? (
            <md-filled-button disabled={accepting} onClick={onAccept}>
              {accepting ? 'Принимаем…' : 'Взять в работу'}
            </md-filled-button>
          ) : null}
        </div>
        <dl className="expert-case-facts">
          <div><dt>Статус</dt><dd>{detail.statusText}</dd></div>
          <div><dt>Обращение</dt><dd>{detail.submissionPathText}, цикл {detail.sequence}</dd></div>
          <div><dt>Последняя активность</dt><dd>{formatDate(detail.lastActivityAt)}</dd></div>
        </dl>
        <div className="expert-current-action" aria-live="polite">
          <span>Требуется сейчас</span>
          <strong>{detail.nextAction}</strong>
        </div>
        {acceptError ? <p className="gentle-error" role="alert">Обращение уже обновилось. Откройте актуальную карточку.</p> : null}
      </header>

      <nav className="case-workspace-nav" aria-label="Разделы обращения">
        {expertCaseWorkspaces.map((item) => (
          <Link
            key={item.id}
            to={`/staff/expert/cases/${detail.id}/${item.id}${workspaceSearch}`}
            aria-current={workspace === item.id ? 'page' : undefined}
          >
            {item.label}
          </Link>
        ))}
      </nav>

      {workspace === 'overview' ? (
        <section className="expert-case-workspace" aria-labelledby="expert-overview-heading">
          <p className="eyebrow">Стабильный контекст</p>
          <h3 id="expert-overview-heading" data-detail-heading tabIndex={-1}>Обзор обращения</h3>
          <section className="detail-section" aria-labelledby="expert-initial-heading">
            <h4 id="expert-initial-heading">Первоначальный рассказ</h4>
            {detail.narrative ? <p className="appeal-narrative">{detail.narrative}</p> : <p>Рассказ не добавлен.</p>}
            {detail.answers.length > 0 ? (
              <dl className="appeal-answers">
                {detail.answers.map((answer) => (
                  <div key={answer.questionCode}><dt>{answer.question}</dt><dd>{answer.value}</dd></div>
                ))}
              </dl>
            ) : null}
            {detail.attachments.length > 0 ? (
              <div className="expert-attachments" aria-label="Вложения обращения">
                {detail.attachments.map((attachment) => (
                  <div className="expert-attachment" key={attachment.id}>
                    <span>{attachment.displayName}</span>
                    <md-outlined-button disabled={downloadMutation.isPending} onClick={() => downloadMutation.mutate(attachment)}>Скачать</md-outlined-button>
                  </div>
                ))}
              </div>
            ) : null}
          </section>
          <section className="detail-section" aria-labelledby="overview-team-heading">
            <h4 id="overview-team-heading">Участники</h4>
            <div className="participant-list">
              {detail.participants.map((participant) => (
                <p key={participant.id}><strong>{participant.displayName}</strong><span>{participant.roleText}</span></p>
              ))}
            </div>
          </section>
          {detail.previousCycles.length > 0 ? (
            <section className="previous-cycles" aria-labelledby="previous-cycles-heading">
              <h4 id="previous-cycles-heading">Предыдущие циклы помощи</h4>
              {detail.previousCycles.map((cycle) => (
                <article key={cycle.sequence}>
                  <header><strong>Цикл {cycle.sequence}</strong><span>{cycle.statusText} · {formatDate(cycle.startedAt)}</span></header>
                  {cycle.narrative ? <p>{cycle.narrative}</p> : null}
                  {cycle.messages.map((message) => <p key={message.id}><strong>{message.authorLabel}:</strong> {message.body}</p>)}
                  {cycle.recommendations.map((recommendation) => <p key={recommendation.id}><strong>Итог:</strong> {recommendation.body}</p>)}
                </article>
              ))}
            </section>
          ) : null}
        </section>
      ) : null}

      {workspace === 'dialog' ? (
        <section className="expert-case-workspace expert-chat" aria-labelledby="expert-chat-heading">
          <p className="eyebrow">Публичный канал</p>
          <h3 id="expert-chat-heading" data-detail-heading tabIndex={-1}>Диалог</h3>
          <p className="visibility-notice">Заявитель увидит это сообщение. Ваше имя не показывается: автор указан как «Специалист».</p>
          <div className="expert-messages" aria-live="polite">
            {detail.messages.length === 0 ? <p className="expert-zone-help">Сообщений пока нет.</p> : null}
            {detail.messages.map((message) => (
              <article className={`expert-message expert-message--${message.author.toLowerCase()}`} key={message.id}>
                <strong>{message.authorLabel}</strong><p>{message.body}</p><time dateTime={message.createdAt}>{formatDate(message.createdAt)}</time>
              </article>
            ))}
          </div>
          {detail.status === 'InProgress' ? (
            <form className="expert-compose" onSubmit={submitQuestion}>
              {collaborationActive && !ownsComposer ? (
                <div className="composer-access">
                  <p className="composer-lock" role="status">{composerOwner ? `${composerOwner} сейчас редактирует запись.` : 'При совместной работе редактор закрепляется за одним специалистом.'}</p>
                  <md-outlined-button type="button" disabled={acquireMutation.isPending || Boolean(composerOwner)} onClick={openComposer}>{acquireMutation.isPending ? 'Открываем…' : 'Начать редактирование'}</md-outlined-button>
                </div>
              ) : null}
              <label className="text-field"><span>Сообщение заявителю</span><textarea maxLength={4000} minLength={2} required disabled={!canCompose} value={questionBody} onChange={(event) => setQuestionBody(event.target.value)} /></label>
              <md-filled-button type="submit" disabled={questionMutation.isPending || questionBody.trim().length < 2 || !canCompose}>{questionMutation.isPending ? 'Отправляем…' : 'Отправить сообщение'}</md-filled-button>
              {questionMutation.isError ? <p className="gentle-error" role="alert">Не удалось отправить сообщение. Откройте актуальную карточку.</p> : null}
            </form>
          ) : null}
          {detail.status === 'Assigned' ? <p className="operator-message">Сначала возьмите обращение в работу. После этого здесь откроется публичный диалог.</p> : null}
          {detail.status === 'NeedsClarification' ? <p className="operator-message">Ждём ответа заявителя. Отправленное сообщение уже видно в диалоге; после ответа карточка вернётся в «В работе».</p> : null}
          {detail.status === 'RecommendationReady' ? <p className="operator-message">Итог уже опубликован. Теперь заявитель решает, помог ли ответ.</p> : null}
          {detail.status === 'Closed' ? <p className="operator-message">Обращение завершено. Диалог доступен только для просмотра.</p> : null}
        </section>
      ) : null}

      {workspace === 'notes' ? (
        <section className="expert-case-workspace expert-notes" aria-labelledby="expert-notes-heading">
          <p className="eyebrow">Внутренняя работа</p>
          <h3 id="expert-notes-heading" data-detail-heading tabIndex={-1}>Внутренние заметки</h3>
          <p className="visibility-notice">Видят только разрешённые участники обращения. Не видят заявитель, оператор и администратор.</p>
          {detail.notes.length === 0 ? <p className="expert-zone-help">Заметок пока нет.</p> : null}
          {detail.notes.map((note) => <article className="expert-note" key={note.id}><p>{note.body}</p><time dateTime={note.createdAt}>{formatDate(note.createdAt)}</time></article>)}
          {detail.status !== 'Closed' ? <form className="expert-compose" onSubmit={submitNote}>
            {collaborationActive && !ownsComposer ? (
              <div className="composer-access">
                <p className="composer-lock" role="status">{composerOwner ? `${composerOwner} сейчас редактирует запись.` : 'При совместной работе редактор закрепляется за одним специалистом.'}</p>
                <md-outlined-button type="button" disabled={acquireMutation.isPending || Boolean(composerOwner)} onClick={openComposer}>{acquireMutation.isPending ? 'Открываем…' : 'Начать редактирование'}</md-outlined-button>
              </div>
            ) : null}
            <label className="text-field"><span>Новая внутренняя заметка</span><textarea maxLength={4000} minLength={2} required disabled={!canCompose} value={noteBody} onChange={(event) => setNoteBody(event.target.value)} /></label>
            <md-outlined-button type="submit" disabled={noteMutation.isPending || noteBody.trim().length < 2 || !canCompose}>{noteMutation.isPending ? 'Сохраняем…' : 'Сохранить заметку'}</md-outlined-button>
            {noteMutation.isError ? <p className="gentle-error" role="alert">Не удалось сохранить заметку.</p> : null}
          </form> : <p className="operator-message">Обращение завершено. Заметки доступны только для просмотра.</p>}
        </section>
      ) : null}

      {workspace === 'answer' ? (
        <section className="expert-case-workspace expert-recommendations" aria-labelledby="expert-recommendations-heading">
          <p className="eyebrow">Итог для заявителя</p>
          <h3 id="expert-recommendations-heading" data-detail-heading tabIndex={-1}>Ответ</h3>
          {detail.recommendations.length > 0 ? <h4>Опубликованные версии</h4> : null}
          {detail.recommendations.map((recommendation) => (
            <article className="expert-recommendation" key={recommendation.id}><strong>Версия {recommendation.version}</strong><p>{recommendation.body}</p><time dateTime={recommendation.createdAt}>{formatDate(recommendation.createdAt)}</time></article>
          ))}
          {detail.status === 'InProgress' && detail.role === 'Responsible' && answerMode === 'edit' ? (
            <div className="expert-compose">
              {collaborationActive && !ownsComposer ? (
                <div className="composer-access">
                  <p className="composer-lock" role="status">{composerOwner ? `${composerOwner} сейчас редактирует запись.` : 'При совместной работе редактор закрепляется за одним специалистом.'}</p>
                  <md-outlined-button disabled={acquireMutation.isPending || Boolean(composerOwner)} onClick={openComposer}>{acquireMutation.isPending ? 'Открываем…' : 'Начать редактирование'}</md-outlined-button>
                </div>
              ) : null}
              <p className="visibility-notice">Заявитель увидит ответ только после отдельного подтверждения на следующем шаге.</p>
              <label className="text-field"><span>Новый ответ</span><textarea maxLength={10000} minLength={10} required disabled={!canCompose} value={recommendationBody} onChange={(event) => setRecommendationBody(event.target.value)} /></label>
              <md-filled-button disabled={recommendationBody.trim().length < 10 || !canCompose} onClick={() => setAnswerMode('preview')}>Проверить перед публикацией</md-filled-button>
            </div>
          ) : null}
          {detail.status === 'InProgress' && detail.role === 'Responsible' && answerMode === 'preview' ? (
            <section className="answer-preview" aria-labelledby="answer-preview-heading">
              <p className="eyebrow">Так увидит заявитель</p>
              <h4 id="answer-preview-heading">Ответ специалиста</h4>
              <p>{recommendationBody}</p>
              <div className="action-row">
                <md-filled-button disabled={recommendationMutation.isPending} onClick={() => recommendationMutation.mutate()}>{recommendationMutation.isPending ? 'Публикуем…' : 'Опубликовать ответ'}</md-filled-button>
                <md-outlined-button disabled={recommendationMutation.isPending} onClick={() => setAnswerMode('edit')}>Вернуться к редактированию</md-outlined-button>
              </div>
            </section>
          ) : null}
          {recommendationMutation.isError ? <p className="gentle-error" role="alert">Не удалось опубликовать ответ. Откройте актуальную карточку и проверьте текст.</p> : null}
          {detail.role === 'CoExecutor' ? <p className="operator-message">Опубликовать итог может только ответственный специалист. Вы можете сохранить предложение во внутренних заметках.</p> : null}
          {detail.status === 'Assigned' ? <p className="operator-message">Сначала возьмите обращение в работу, затем подготовьте ответ.</p> : null}
          {detail.status === 'NeedsClarification' ? <p className="operator-message">Сейчас ожидается ответ заявителя. После него снова станет доступна подготовка итога.</p> : null}
          {detail.status === 'RecommendationReady' ? <p className="operator-message">Ответ опубликован и сохранён в истории. Теперь ожидается решение заявителя.</p> : null}
          {detail.status === 'Closed' ? <p className="operator-message">Заявитель завершил обращение. Опубликованный ответ сохранён в истории.</p> : null}
        </section>
      ) : null}

      {workspace === 'team' ? (
        <section className="expert-case-workspace expert-collaboration" aria-labelledby="expert-collaboration-heading">
          <p className="eyebrow">Совместная работа</p>
          <h3 id="expert-collaboration-heading" data-detail-heading tabIndex={-1}>Команда</h3>
          <div className="participant-list">
            {detail.participants.map((participant) => <p key={participant.id}><strong>{participant.displayName}</strong><span>{participant.roleText}</span></p>)}
          </div>
          <p className="expert-zone-help" aria-live="polite">{presenceQuery.data?.active.length ? `Сейчас в карточке: ${presenceQuery.data.active.map((person) => person.isCurrent ? 'вы' : person.displayName).join(', ')}.` : 'Других специалистов в карточке сейчас нет.'}</p>
          {detail.status !== 'Closed' ? <form className="workflow-request" onSubmit={(event) => { event.preventDefault(); workflowMutation.mutate(); }}>
            <label className="select-field"><span>Что запросить у оператора</span><select aria-label="Тип запроса оператору" value={workflowType} onChange={(event) => setWorkflowType(event.target.value as typeof workflowType)}><option value="CoExecutor">Добавить соисполнителя</option><option value="Transfer">Передать обращение</option><option value="PriorityReview">Пересмотреть приоритет</option></select></label>
            <label className="text-field"><span>Эту причину увидит оператор</span><textarea maxLength={1000} minLength={10} required value={workflowReason} onChange={(event) => setWorkflowReason(event.target.value)} /></label>
            <md-outlined-button type="submit" disabled={workflowMutation.isPending || workflowReason.trim().length < 10}>{workflowMutation.isPending ? 'Отправляем…' : 'Отправить запрос'}</md-outlined-button>
            {workflowMutation.isError ? <p className="gentle-error" role="alert">Не удалось отправить запрос. Проверьте, нет ли уже ожидающего запроса этого типа.</p> : null}
          </form> : <p className="operator-message">Состав команды и запросы сохранены как история завершённого обращения.</p>}
          {detail.status !== 'Closed' ? <p className="expert-zone-help">Запрос оператору не приостанавливает обращение и не меняет его раздел. Пока оператор принимает решение, продолжайте работу по текущему статусу.</p> : null}
          {detail.workflowRequests.length > 0 ? <div className="workflow-history" aria-label="История запросов">{detail.workflowRequests.map((request) => <article key={request.id}><div><strong>{request.typeText}</strong><p>{request.reason}</p></div><span>{request.statusText} · {formatDate(request.requestedAt)}</span></article>)}</div> : <p className="expert-zone-help">Запросов оператору пока нет.</p>}
          {detail.assignmentHistory.length > 0 ? <details className="assignment-history"><summary>История участников</summary><div>{detail.assignmentHistory.map((item) => <p key={item.id}>{item.eventType === 'Removed' ? 'Участие завершено' : 'Добавлен'}: {item.displayName}, {item.roleText.toLowerCase()} · {formatDate(item.occurredAt)}</p>)}</div></details> : null}
        </section>
      ) : null}
    </div>
  );
}

function expertSectionForStatus(status: string): Exclude<ExpertSection, 'case'> | null {
  if (status === 'Assigned') return 'inbox';
  if (status === 'InProgress') return 'active';
  if (status === 'NeedsClarification') return 'waiting';
  if (status === 'RecommendationReady' || status === 'Closed') return 'completed';
  return null;
}

function sectionMovementText(
  from: Exclude<ExpertSection, 'case'>,
  to: Exclude<ExpertSection, 'case'>,
) {
  if (from === 'inbox' && to === 'active') return 'Обращение принято и перенесено в «В работе».';
  if (from === 'active' && to === 'waiting') return 'Сообщение отправлено. Обращение перенесено в «Ждут заявителя».';
  if (from === 'waiting' && to === 'active') return 'Заявитель ответил. Обращение вернулось в «В работе» — можно продолжать.';
  if (to === 'completed') return 'Рабочий этап завершён. Обращение перенесено в «Завершённые».';
  return `Обращение перенесено в «${sectionPresentation[to].title}».`;
}

function emptySectionText(section: Exclude<ExpertSection, 'case'>) {
  if (section === 'inbox') return 'Новых назначений сейчас нет.';
  if (section === 'active') return 'Сейчас нет обращений, по которым требуется ваше действие.';
  if (section === 'waiting') return 'Сейчас ни одно обращение не ждёт ответа заявителя.';
  return 'Завершённых обращений по выбранным фильтрам нет.';
}

function formatAssignedAt(value: string | null) {
  if (!value) return 'только что';
  return new Intl.DateTimeFormat('ru-RU', { day: 'numeric', month: 'short' }).format(new Date(value));
}

function formatDate(value: string) {
  return new Intl.DateTimeFormat('ru-RU', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value));
}
