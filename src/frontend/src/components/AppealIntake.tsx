import '@material/web/button/filled-button.js';
import '@material/web/button/outlined-button.js';
import { useMutation, useQuery } from '@tanstack/react-query';
import { FormEvent, useEffect, useMemo, useRef, useState } from 'react';
import {
  createAppeal,
  getIntakeOptions,
  publicAppealError,
  uploadAppealAttachment,
  type ApplicantType,
  type CreateAppealResponse,
  type SubmissionPath,
} from '../api/publicAppeals';
import { crisisTextMatches } from '../crisis';
import { CrisisHelpPanel } from './CrisisHelpPanel';
import { PublicFrame } from './PublicFrame';

const maxAttachmentCount = 5;
const maxAttachmentSize = 10 * 1024 * 1024;
const allowedAttachmentTypes: Record<string, string[]> = {
  'image/png': ['.png'],
  'image/jpeg': ['.jpg', '.jpeg'],
  'application/pdf': ['.pdf'],
};

type PendingAttachment = {
  clientUploadId: string;
  file: File;
  previewUrl: string | null;
  progress: number;
  status: 'ready' | 'uploading' | 'uploaded' | 'error';
};

export function validateAttachmentSelection(existingCount: number, incoming: File[]) {
  if (existingCount + incoming.length > maxAttachmentCount) {
    return 'К одному обращению можно прикрепить не больше пяти файлов.';
  }

  for (const file of incoming) {
    if (file.size > maxAttachmentSize) {
      return `Файл «${file.name}» больше 10 МБ.`;
    }

    const extension = file.name.slice(file.name.lastIndexOf('.')).toLowerCase();
    if (!allowedAttachmentTypes[file.type]?.includes(extension)) {
      return `Файл «${file.name}» не подходит. Выберите PNG, JPEG или PDF.`;
    }
  }

  return null;
}

type ToneCopy = {
  pathHeading: string;
  narrativeHeading: string;
  narrativeHint: string;
  questionsHeading: string;
  optionalHint: string;
  submit: string;
};

export function copyForApplicant(type: ApplicantType): ToneCopy {
  if (type === 'Student') {
    return {
      pathHeading: 'Как тебе удобнее рассказать?',
      narrativeHeading: 'Расскажи, что происходит',
      narrativeHint: 'Пиши так, как получается. Здесь не нужно подбирать правильные слова.',
      questionsHeading: 'Если хочешь, уточни детали',
      optionalHint: 'Все вопросы ниже можно пропустить.',
      submit: 'Отправить обращение',
    };
  }

  return {
    pathHeading: 'Как вам удобнее рассказать?',
    narrativeHeading: 'Расскажите, что происходит',
    narrativeHint: 'Опишите ситуацию в удобной форме. Подбирать официальные формулировки не нужно.',
    questionsHeading: 'Если хотите, уточните детали',
    optionalHint: 'Все вопросы ниже можно пропустить.',
    submit: 'Отправить обращение',
  };
}

export function AppealIntake() {
  const optionsQuery = useQuery({ queryKey: ['intake-options'], queryFn: getIntakeOptions, retry: 2 });
  const [step, setStep] = useState(1);
  const [applicantType, setApplicantType] = useState<ApplicantType | null>(null);
  const [submissionPath, setSubmissionPath] = useState<SubmissionPath | null>(null);
  const [categoryId, setCategoryId] = useState<string | null>(null);
  const [narrative, setNarrative] = useState('');
  const [answers, setAnswers] = useState<Record<string, string>>({});
  const [crisisContact, setCrisisContact] = useState('');
  const [attachments, setAttachments] = useState<PendingAttachment[]>([]);
  const [attachmentMessage, setAttachmentMessage] = useState('');
  const [savedAppeal, setSavedAppeal] = useState<CreateAppealResponse | null>(null);
  const [created, setCreated] = useState<CreateAppealResponse | null>(null);
  const clientRequestId = useRef(crypto.randomUUID());
  const attachmentSnapshot = useRef<PendingAttachment[]>([]);
  attachmentSnapshot.current = attachments;
  const submitMutation = useMutation({
    mutationFn: async (request: Parameters<typeof createAppeal>[0]) => {
      const result = savedAppeal ?? await createAppeal(request);
      setSavedAppeal(result);
      for (const attachment of attachments) {
        if (attachment.status === 'uploaded') continue;
        updateAttachment(attachment.clientUploadId, { status: 'uploading', progress: 0 });
        try {
          await uploadAppealAttachment(
            result.trackNumber,
            attachment.clientUploadId,
            attachment.file,
            (progress) => updateAttachment(attachment.clientUploadId, { progress }),
          );
          updateAttachment(attachment.clientUploadId, { status: 'uploaded', progress: 100 });
        } catch (error) {
          updateAttachment(attachment.clientUploadId, { status: 'error' });
          throw error;
        }
      }
      return result;
    },
    onSuccess: setCreated,
  });

  useEffect(() => () => {
    attachmentSnapshot.current.forEach((attachment) => {
      if (attachment.previewUrl) URL.revokeObjectURL(attachment.previewUrl);
    });
  }, []);

  const tone = applicantType ? copyForApplicant(applicantType) : null;
  const selectedCategory = useMemo(
    () => optionsQuery.data?.categories.find((category) => category.id === categoryId),
    [categoryId, optionsQuery.data?.categories],
  );
  const crisisDetected = useMemo(
    () => crisisTextMatches(
      [narrative, ...Object.values(answers)],
      optionsQuery.data?.crisisMarkers ?? [],
    ),
    [answers, narrative, optionsQuery.data?.crisisMarkers],
  );

  if (created) {
    return (
      <TrackNumberSuccess
        result={created}
        needsImmediateHelp={crisisDetected}
        crisisSupport={optionsQuery.data?.crisisSupport ?? []}
      />
    );
  }

  if (optionsQuery.isPending) {
    return <PublicMessage message="Готовим безопасную форму…" />;
  }

  if (optionsQuery.isError || !optionsQuery.data) {
    return <PublicMessage message="Форма временно недоступна. Обновите страницу через несколько минут." />;
  }

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!applicantType || !submissionPath) {
      return;
    }

    submitMutation.mutate({
      clientRequestId: clientRequestId.current,
      applicantType,
      submissionPath,
      categoryId: submissionPath === 'Category' ? categoryId : null,
      narrative: narrative.trim() || null,
      answers,
      crisisContact: crisisDetected ? crisisContact.trim() || null : null,
    });
  }

  function updateAttachment(clientUploadId: string, patch: Partial<PendingAttachment>) {
    setAttachments((current) => current.map((attachment) => (
      attachment.clientUploadId === clientUploadId ? { ...attachment, ...patch } : attachment
    )));
  }

  function selectAttachments(event: React.ChangeEvent<HTMLInputElement>) {
    const incoming = Array.from(event.target.files ?? []);
    event.target.value = '';
    if (incoming.length === 0) return;
    const validationMessage = validateAttachmentSelection(attachments.length, incoming);
    if (validationMessage) {
      setAttachmentMessage(validationMessage);
      return;
    }

    setAttachmentMessage('');
    setAttachments((current) => [
      ...current,
      ...incoming.map((file) => ({
        clientUploadId: crypto.randomUUID(),
        file,
        previewUrl: file.type.startsWith('image/') ? URL.createObjectURL(file) : null,
        progress: 0,
        status: 'ready' as const,
      })),
    ]);
  }

  function removeAttachment(clientUploadId: string) {
    setAttachments((current) => {
      const removed = current.find((attachment) => attachment.clientUploadId === clientUploadId);
      if (removed?.previewUrl) URL.revokeObjectURL(removed.previewUrl);
      return current.filter((attachment) => attachment.clientUploadId !== clientUploadId);
    });
    setAttachmentMessage('');
  }

  const narrativeRequired = submissionPath === 'FreeText' || selectedCategory?.code === 'unsure';
  const canSubmit = Boolean(
    applicantType
      && submissionPath
      && (submissionPath === 'FreeText' || categoryId)
      && (!narrativeRequired || narrative.trim().length >= 10),
  );

  return (
    <PublicFrame current="new">
      <main className="intake-main">
        <section className="intake-card" aria-labelledby="intake-heading">
          <div className="intake-progress" aria-label={`Шаг ${step} из 3`}>
            <span>Шаг {step} из 3</span>
            <progress max="3" value={step} />
          </div>

          {step === 1 ? (
            <>
              <p className="eyebrow">Начнем без имени и регистрации</p>
              <h1 id="intake-heading" className="intake-title">Кто оставляет обращение?</h1>
              <p className="intake-copy">Это нужно только для подходящего тона и вопросов.</p>
              <div className="choice-grid" role="group" aria-label="Тип заявителя">
                {optionsQuery.data.applicantTypes.map((type) => (
                  <button
                    className={applicantType === type.value ? 'choice-card choice-card--selected' : 'choice-card'}
                    key={type.value}
                    type="button"
                    aria-pressed={applicantType === type.value}
                    onClick={() => setApplicantType(type.value)}
                  >
                    <strong>{type.label}</strong>
                    <span>{type.value === 'Student' ? 'Общаемся на «ты»' : 'Общаемся на «вы»'}</span>
                  </button>
                ))}
              </div>
              <div className="intake-actions intake-actions--end">
                <md-filled-button disabled={!applicantType} onClick={() => setStep(2)}>Продолжить</md-filled-button>
              </div>
            </>
          ) : null}

          {step === 2 && tone ? (
            <>
              <p className="eyebrow">Можно выбрать любой путь</p>
              <h1 id="intake-heading" className="intake-title">{tone.pathHeading}</h1>
              <p className="intake-copy">Оба варианта попадут к оператору и получат трек-номер.</p>
              <div className="choice-grid">
                <button
                  className={submissionPath === 'FreeText' ? 'choice-card choice-card--selected' : 'choice-card'}
                  type="button"
                  aria-pressed={submissionPath === 'FreeText'}
                  onClick={() => setSubmissionPath('FreeText')}
                >
                  <strong>Рассказать своими словами</strong>
                  <span>Без категорий и официальных формулировок</span>
                </button>
                <button
                  className={submissionPath === 'Category' ? 'choice-card choice-card--selected' : 'choice-card'}
                  type="button"
                  aria-pressed={submissionPath === 'Category'}
                  onClick={() => setSubmissionPath('Category')}
                >
                  <strong>Выбрать категорию</strong>
                  <span>Если уже понятно, к чему относится ситуация</span>
                </button>
              </div>
              <div className="intake-actions">
                <md-outlined-button onClick={() => setStep(1)}>Назад</md-outlined-button>
                <md-filled-button disabled={!submissionPath} onClick={() => setStep(3)}>Продолжить</md-filled-button>
              </div>
            </>
          ) : null}

          {step === 3 && tone && submissionPath ? (
            <form onSubmit={submit}>
              <p className="eyebrow">Анонимное обращение</p>
              <h1 id="intake-heading" className="intake-title">{tone.narrativeHeading}</h1>
              <p className="intake-copy">{tone.narrativeHint}</p>

              {submissionPath === 'Category' ? (
                <fieldset className="intake-fieldset">
                  <legend>Категория</legend>
                  <div className="category-grid">
                    {optionsQuery.data.categories.map((category) => (
                      <button
                        className={categoryId === category.id ? 'category-choice category-choice--selected' : 'category-choice'}
                        key={category.id}
                        type="button"
                        disabled={Boolean(savedAppeal)}
                        aria-pressed={categoryId === category.id}
                        onClick={() => setCategoryId(category.id)}
                      >
                        {category.displayName}
                      </button>
                    ))}
                  </div>
                </fieldset>
              ) : null}

              <label className="textarea-field">
                <span>{narrativeRequired ? 'Что происходит' : 'Дополнительное описание — необязательно'}</span>
                <textarea
                  maxLength={10_000}
                  required={narrativeRequired}
                  disabled={Boolean(savedAppeal)}
                  rows={7}
                  value={narrative}
                  onChange={(event) => setNarrative(event.target.value)}
                  placeholder={applicantType === 'Student' ? 'Напиши так, как можешь…' : 'Опишите ситуацию своими словами…'}
                />
              </label>

              <section className="optional-section" aria-labelledby="optional-heading">
                <h2 id="optional-heading">{tone.questionsHeading}</h2>
                <p>{tone.optionalHint}</p>
                <div className="optional-fields">
                  {optionsQuery.data.questions.map((question) => (
                    <label key={question.code}>
                      <span>{applicantType === 'Student' ? question.studentText : question.adultText}</span>
                      <input
                        value={answers[question.code] ?? ''}
                        maxLength={1_000}
                        disabled={Boolean(savedAppeal)}
                        onChange={(event) => setAnswers((current) => ({
                          ...current,
                          [question.code]: event.target.value,
                        }))}
                        placeholder="Можно пропустить"
                      />
                    </label>
                  ))}
                </div>
              </section>

              {crisisDetected ? (
                <CrisisHelpPanel
                  contacts={optionsQuery.data.crisisSupport}
                  contactValue={crisisContact}
                  onContactChange={setCrisisContact}
                />
              ) : null}

              <section className="attachment-section" aria-labelledby="attachment-heading">
                <h2 id="attachment-heading">Прикрепить файлы — необязательно</h2>
                <p>До пяти PNG, JPEG или PDF, каждый до 10 МБ. У изображений удалятся EXIF и геолокация.</p>
                <label className="file-picker">
                  <span>Выбрать файлы</span>
                  <input
                    accept=".png,.jpg,.jpeg,.pdf,image/png,image/jpeg,application/pdf"
                    disabled={submitMutation.isPending || attachments.length >= maxAttachmentCount}
                    multiple
                    type="file"
                    onChange={selectAttachments}
                  />
                </label>
                {attachmentMessage ? <p className="attachment-message" role="alert">{attachmentMessage}</p> : null}
                {!attachmentMessage && attachments.length === maxAttachmentCount ? (
                  <p className="attachment-limit" role="status">Выбрано пять файлов — это максимум для одного обращения.</p>
                ) : null}
                {attachments.length > 0 ? (
                  <div className="attachment-list" aria-label="Выбранные файлы">
                    {attachments.map((attachment) => (
                      <div className="attachment-item" key={attachment.clientUploadId}>
                        {attachment.previewUrl ? (
                          <img alt="" src={attachment.previewUrl} />
                        ) : (
                          <span className="attachment-item__document" aria-hidden="true">PDF</span>
                        )}
                        <div className="attachment-item__body">
                          <strong>{attachment.file.name}</strong>
                          <span>{formatFileSize(attachment.file.size)} · {attachmentStatusText(attachment)}</span>
                          {attachment.status === 'uploading' ? (
                            <progress max="100" value={attachment.progress} aria-label={`Загрузка ${attachment.file.name}`} />
                          ) : null}
                        </div>
                        {attachment.status === 'ready' || attachment.status === 'error' ? (
                          <button type="button" onClick={() => removeAttachment(attachment.clientUploadId)}>Удалить</button>
                        ) : null}
                      </div>
                    ))}
                  </div>
                ) : null}
              </section>

              {submitMutation.isError ? (
                <div className="gentle-error" role="alert">
                  <p>{publicAppealError(submitMutation.error)}</p>
                  {savedAppeal ? <p>Текст обращения уже сохранен. Исправьте или удалите файл и повторите загрузку.</p> : null}
                </div>
              ) : null}

              <div className="intake-actions">
                <md-outlined-button type="button" disabled={Boolean(savedAppeal)} onClick={() => setStep(2)}>Назад</md-outlined-button>
                <md-filled-button type="submit" disabled={!canSubmit || submitMutation.isPending}>
                  {submitMutation.isPending ? 'Отправляем…' : tone.submit}
                </md-filled-button>
              </div>
            </form>
          ) : null}
        </section>
      </main>
    </PublicFrame>
  );
}

function formatFileSize(size: number) {
  if (size < 1024 * 1024) return `${Math.max(1, Math.round(size / 1024))} КБ`;
  return `${(size / 1024 / 1024).toFixed(1)} МБ`;
}

function attachmentStatusText(attachment: PendingAttachment) {
  if (attachment.status === 'uploading') return `Загрузка ${attachment.progress}%`;
  if (attachment.status === 'uploaded') return 'Безопасная копия загружена';
  if (attachment.status === 'error') return 'Не загрузился — можно повторить';
  return 'Готов к загрузке';
}

function TrackNumberSuccess({
  result,
  needsImmediateHelp,
  crisisSupport,
}: {
  result: CreateAppealResponse;
  needsImmediateHelp: boolean;
  crisisSupport: Awaited<ReturnType<typeof getIntakeOptions>>['crisisSupport'];
}) {
  const [copyState, setCopyState] = useState('');

  async function copyTrackNumber() {
    await navigator.clipboard.writeText(result.trackNumber);
    setCopyState('Номер скопирован');
  }

  function saveTrackNumber() {
    const content = `Отклик\nТрек-номер: ${result.trackNumber}\nСохраните его: без номера обращение нельзя открыть на другом устройстве.\n`;
    const url = URL.createObjectURL(new Blob([content], { type: 'text/plain;charset=utf-8' }));
    const link = document.createElement('a');
    link.href = url;
    link.download = 'otklik-track.txt';
    link.click();
    URL.revokeObjectURL(url);
  }

  return (
    <PublicFrame current="new">
      <main className="intake-main">
        <section className="intake-card success-card" aria-labelledby="success-heading">
          <p className="eyebrow">Обращение принято</p>
          <h1 id="success-heading" className="intake-title">Сохраните трек-номер</h1>
          <p className="intake-copy">
            Мы не знаем вашего имени. Этот номер — единственный ключ к обращению на другом устройстве.
          </p>
          <p className="intake-copy">
            На этом устройстве статус откроется через защищенный доступ без номера в адресе. Для общего устройства удалите доступ на странице статуса.
          </p>
          <div className="track-number" aria-label="Трек-номер обращения">{result.trackNumber}</div>
          {copyState ? <p className="copy-confirmation" role="status">{copyState}</p> : null}
          <div className="success-actions">
            <md-filled-button onClick={copyTrackNumber}>Скопировать номер</md-filled-button>
            <md-outlined-button onClick={saveTrackNumber}>Сохранить в файл</md-outlined-button>
          </div>
          <div className="privacy-note privacy-note--plain">
            <strong>{result.statusText}</strong>
            <span>Текст уже сохранен. Повторная отправка не создаст дубликат.</span>
          </div>
          {needsImmediateHelp ? <CrisisHelpPanel contacts={crisisSupport} compact /> : null}
          <md-outlined-button onClick={() => window.location.assign('/appeal/status')}>
            Открыть статус
          </md-outlined-button>
        </section>
      </main>
    </PublicFrame>
  );
}

function PublicMessage({ message }: { message: string }) {
  return (
    <PublicFrame current="new">
      <main className="intake-main">
        <section className="intake-card" aria-live="polite">{message}</section>
      </main>
    </PublicFrame>
  );
}
