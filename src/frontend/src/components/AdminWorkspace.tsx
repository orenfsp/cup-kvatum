import '@material/web/button/filled-button.js';
import '@material/web/button/outlined-button.js';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useEffect, useState } from 'react';
import {
  adminError,
  changeAdminUserState,
  createAdminCategory,
  createAdminGroup,
  createAdminUser,
  deactivateAdminCategory,
  deactivateAdminGroup,
  deactivateAdminRule,
  getAdminAudit,
  getAdminAuditDetail,
  getAdminConfiguration,
  getAdminUsers,
  getStuckAppeals,
  interveneInStuckAppeal,
  saveAdminRule,
  updateAdminCategory,
  updateAdminGroup,
  updateAdminUser,
  type AdminCategory,
  type AdminGroup,
  type AdminUser,
  type AuditListItem,
  type StuckAppeal,
} from '../api/admin';
import type { StaffRole } from '../api/staffAuth';

export type AdminView = 'configuration' | 'users' | 'stuck' | 'audit';

export function AdminWorkspace({ view }: { view: AdminView }) {
  if (view === 'users') return <UsersView />;
  if (view === 'stuck') return <StuckView />;
  if (view === 'audit') return <AuditView />;
  return <ConfigurationView />;
}

function ConfigurationView() {
  const queryClient = useQueryClient();
  const configuration = useQuery({
    queryKey: ['admin-configuration'],
    queryFn: getAdminConfiguration,
  });
  const [notice, setNotice] = useState('');
  const invalidate = async (message: string) => {
    setNotice(message);
    await queryClient.invalidateQueries({ queryKey: ['admin-configuration'] });
  };

  return (
    <div className="admin-workspace">
      <WorkspaceHeading
        eyebrow="Кабинет администратора"
        title="Конфигурация маршрутизации"
        copy="Категории, группы и правила работают как единая цепочка. Изменения применяются к новым подсказкам, а использованная версия остается у обращения."
      />
      <PrivacyBoundary />
      {notice ? <p className="operator-notice" role="status">{notice}</p> : null}
      {configuration.isPending ? <p className="staff-copy">Загружаем конфигурацию…</p> : null}
      {configuration.isError ? <p className="form-error" role="alert">Не удалось загрузить настройки.</p> : null}
      {configuration.data ? (
        <div className="admin-config-grid">
          <CategoryEditor
            items={configuration.data.categories}
            onChanged={invalidate}
          />
          <GroupEditor
            items={configuration.data.groups}
            experts={configuration.data.experts}
            onChanged={invalidate}
          />
          <RuleEditor
            categories={configuration.data.categories}
            groups={configuration.data.groups}
            rules={configuration.data.rules}
            onChanged={invalidate}
          />
        </div>
      ) : null}
    </div>
  );
}

function CategoryEditor({
  items,
  onChanged,
}: {
  items: AdminCategory[];
  onChanged: (message: string) => Promise<void>;
}) {
  const [selected, setSelected] = useState<AdminCategory | null>(null);
  const [code, setCode] = useState('');
  const [displayName, setDisplayName] = useState('');
  const [sortOrder, setSortOrder] = useState(50);
  const save = useMutation({
    mutationFn: () => selected
      ? updateAdminCategory(selected.id, { code, displayName, sortOrder, expectedVersion: selected.version })
      : createAdminCategory({ code, displayName, sortOrder }),
    onSuccess: async () => {
      reset();
      await onChanged(selected ? 'Категория обновлена.' : 'Категория добавлена в форму заявителя.');
    },
  });
  const deactivate = useMutation({
    mutationFn: (item: AdminCategory) => deactivateAdminCategory(item.id, item.version),
    onSuccess: () => onChanged('Категория деактивирована. В старых обращениях она сохранена.'),
  });

  function edit(item: AdminCategory) {
    setSelected(item);
    setCode(item.code);
    setDisplayName(item.displayName);
    setSortOrder(item.sortOrder);
  }

  function reset() {
    setSelected(null);
    setCode('');
    setDisplayName('');
    setSortOrder(50);
  }

  return (
    <section className="admin-section" aria-labelledby="admin-categories-heading">
      <SectionHeading
        id="admin-categories-heading"
        title="Категории"
        copy="Порядок определяет расположение в публичной форме. Использованные категории не удаляются."
      />
      <form className="admin-form" onSubmit={(event) => { event.preventDefault(); save.mutate(); }}>
        <div className="admin-form__row">
          <label className="text-field">
            <span>Код</span>
            <input required minLength={3} value={code} onChange={(event) => setCode(event.target.value)} placeholder="school-safety" />
          </label>
          <label className="text-field">
            <span>Название</span>
            <input required minLength={2} value={displayName} onChange={(event) => setDisplayName(event.target.value)} placeholder="Безопасность в школе" />
          </label>
          <label className="text-field admin-number-field">
            <span>Порядок</span>
            <input type="number" min={0} max={10000} value={sortOrder} onChange={(event) => setSortOrder(Number(event.target.value))} />
          </label>
        </div>
        {save.isError ? <p className="form-error" role="alert">{adminError(save.error)}</p> : null}
        <div className="admin-actions">
          <md-filled-button type="submit" disabled={save.isPending}>{selected ? 'Сохранить' : 'Добавить категорию'}</md-filled-button>
          {selected ? <md-outlined-button type="button" onClick={reset}>Отменить</md-outlined-button> : null}
        </div>
      </form>
      <div className="admin-list" aria-label="Категории">
        {items.map((item) => (
          <div className="admin-row" key={item.id}>
            <div>
              <strong>{item.displayName}</strong>
              <span>{item.code} · порядок {item.sortOrder} · обращений {item.usageCount}</span>
              {!item.isActive ? <span>Не показывается в новых обращениях</span> : null}
            </div>
            <div className="admin-row__actions">
              <button type="button" onClick={() => edit(item)}>Изменить</button>
              {item.isActive && item.code !== 'unsure' ? (
                <button type="button" onClick={() => deactivate.mutate(item)}>Деактивировать</button>
              ) : null}
            </div>
          </div>
        ))}
      </div>
    </section>
  );
}

function GroupEditor({
  items,
  experts,
  onChanged,
}: {
  items: AdminGroup[];
  experts: Awaited<ReturnType<typeof getAdminConfiguration>>['experts'];
  onChanged: (message: string) => Promise<void>;
}) {
  const [selected, setSelected] = useState<AdminGroup | null>(null);
  const [code, setCode] = useState('');
  const [displayName, setDisplayName] = useState('');
  const [limit, setLimit] = useState(5);
  const [expertIds, setExpertIds] = useState<string[]>([]);
  const save = useMutation({
    mutationFn: () => selected
      ? updateAdminGroup(selected.id, {
          code,
          displayName,
          activeAppealLimit: limit,
          expertIds,
          expectedVersion: selected.version,
        })
      : createAdminGroup({ code, displayName, activeAppealLimit: limit, expertIds }),
    onSuccess: async () => {
      const editing = Boolean(selected);
      reset();
      await onChanged(editing ? 'Группа и ее состав обновлены.' : 'Группа экспертов создана.');
    },
  });
  const deactivate = useMutation({
    mutationFn: (item: AdminGroup) => deactivateAdminGroup(item.id, item.version),
    onSuccess: () => onChanged('Группа деактивирована для новых маршрутов.'),
  });

  function edit(item: AdminGroup) {
    setSelected(item);
    setCode(item.code);
    setDisplayName(item.displayName);
    setLimit(item.activeAppealLimit);
    setExpertIds(item.memberIds);
  }

  function reset() {
    setSelected(null);
    setCode('');
    setDisplayName('');
    setLimit(5);
    setExpertIds([]);
  }

  function toggleExpert(id: string) {
    setExpertIds((current) => current.includes(id) ? current.filter((item) => item !== id) : [...current, id]);
  }

  return (
    <section className="admin-section" aria-labelledby="admin-groups-heading">
      <SectionHeading
        id="admin-groups-heading"
        title="Группы экспертов"
        copy="Лимит применяется к каждому эксперту группы. Недоступные и заблокированные сотрудники не предлагаются оператору."
      />
      <form className="admin-form" onSubmit={(event) => { event.preventDefault(); save.mutate(); }}>
        <div className="admin-form__row">
          <label className="text-field">
            <span>Код</span>
            <input required minLength={3} value={code} onChange={(event) => setCode(event.target.value)} placeholder="school-support" />
          </label>
          <label className="text-field">
            <span>Название</span>
            <input required minLength={2} value={displayName} onChange={(event) => setDisplayName(event.target.value)} placeholder="Школьная поддержка" />
          </label>
          <label className="text-field admin-number-field">
            <span>Лимит</span>
            <input type="number" min={1} max={100} value={limit} onChange={(event) => setLimit(Number(event.target.value))} />
          </label>
        </div>
        <fieldset className="admin-members">
          <legend>Состав группы</legend>
          {experts.map((expert) => (
            <label className="check-field" key={expert.id}>
              <input type="checkbox" checked={expertIds.includes(expert.id)} onChange={() => toggleExpert(expert.id)} />
              <span>{expert.displayName}{!expert.isActive ? ' — заблокирован' : !expert.isAvailable ? ' — недоступен' : ''}</span>
            </label>
          ))}
        </fieldset>
        {save.isError ? <p className="form-error" role="alert">{adminError(save.error)}</p> : null}
        <div className="admin-actions">
          <md-filled-button type="submit" disabled={save.isPending}>{selected ? 'Сохранить' : 'Создать группу'}</md-filled-button>
          {selected ? <md-outlined-button type="button" onClick={reset}>Отменить</md-outlined-button> : null}
        </div>
      </form>
      <div className="admin-list" aria-label="Группы экспертов">
        {items.map((item) => (
          <div className="admin-row" key={item.id}>
            <div>
              <strong>{item.displayName}</strong>
              <span>{item.code} · лимит {item.activeAppealLimit} · участников {item.memberIds.length}</span>
              {!item.isActive ? <span>Не используется для новых маршрутов</span> : null}
            </div>
            <div className="admin-row__actions">
              <button type="button" onClick={() => edit(item)}>Изменить</button>
              {item.isActive ? <button type="button" onClick={() => deactivate.mutate(item)}>Деактивировать</button> : null}
            </div>
          </div>
        ))}
      </div>
    </section>
  );
}

function RuleEditor({
  categories,
  groups,
  rules,
  onChanged,
}: {
  categories: AdminCategory[];
  groups: AdminGroup[];
  rules: Awaited<ReturnType<typeof getAdminConfiguration>>['rules'];
  onChanged: (message: string) => Promise<void>;
}) {
  const activeCategories = categories.filter((item) => item.isActive);
  const activeGroups = groups.filter((item) => item.isActive);
  const [categoryId, setCategoryId] = useState(activeCategories[0]?.id ?? '');
  const [groupId, setGroupId] = useState(activeGroups[0]?.id ?? '');
  const currentRule = rules.find((item) => item.categoryId === categoryId);
  useEffect(() => {
    if (!categoryId && activeCategories[0]) setCategoryId(activeCategories[0].id);
    if (!groupId && activeGroups[0]) setGroupId(activeGroups[0].id);
  }, [activeCategories, activeGroups, categoryId, groupId]);
  const save = useMutation({
    mutationFn: () => saveAdminRule({
      categoryId,
      expertGroupId: groupId,
      expectedVersion: currentRule?.version ?? null,
    }),
    onSuccess: () => onChanged(currentRule ? 'Правило обновлено; новая версия применяется только к новым подсказкам.' : 'Правило маршрутизации создано.'),
  });
  const deactivate = useMutation({
    mutationFn: (rule: NonNullable<typeof currentRule>) => deactivateAdminRule(rule.id, rule.version),
    onSuccess: () => onChanged('Правило деактивировано.'),
  });

  return (
    <section className="admin-section" aria-labelledby="admin-rules-heading">
      <SectionHeading
        id="admin-rules-heading"
        title="Правила маршрутизации"
        copy="Для каждой категории выбирается одна профильная группа. Версия меняется при каждом сохранении."
      />
      <form className="admin-form" onSubmit={(event) => { event.preventDefault(); save.mutate(); }}>
        <div className="admin-form__row admin-form__row--two">
          <label className="select-field">
            <span>Категория</span>
            <select value={categoryId} onChange={(event) => {
              const nextCategory = event.target.value;
              setCategoryId(nextCategory);
              const rule = rules.find((item) => item.categoryId === nextCategory && item.isActive);
              if (rule) setGroupId(rule.expertGroupId);
            }}>
              {activeCategories.map((item) => <option key={item.id} value={item.id}>{item.displayName}</option>)}
            </select>
          </label>
          <label className="select-field">
            <span>Группа</span>
            <select value={groupId} onChange={(event) => setGroupId(event.target.value)}>
              {activeGroups.map((item) => <option key={item.id} value={item.id}>{item.displayName}</option>)}
            </select>
          </label>
        </div>
        {currentRule ? <p className="admin-form__hint">Текущая версия: {currentRule.version}{!currentRule.isActive ? ' · правило неактивно' : ''}</p> : null}
        {save.isError ? <p className="form-error" role="alert">{adminError(save.error)}</p> : null}
        <div className="admin-actions">
          <md-filled-button type="submit" disabled={save.isPending || !categoryId || !groupId}>Сохранить правило</md-filled-button>
          {currentRule?.isActive ? <md-outlined-button type="button" onClick={() => deactivate.mutate(currentRule)}>Деактивировать</md-outlined-button> : null}
        </div>
      </form>
      <div className="admin-list" aria-label="Правила маршрутизации">
        {rules.map((item) => (
          <button className="admin-row admin-row--button" type="button" key={item.id} onClick={() => {
            setCategoryId(item.categoryId);
            setGroupId(item.expertGroupId);
          }}>
            <span><strong>{item.category}</strong><span>Группа: {item.expertGroup}</span></span>
            <span>Версия {item.version}{!item.isActive ? ' · неактивно' : ''}</span>
          </button>
        ))}
      </div>
    </section>
  );
}

function UsersView() {
  const queryClient = useQueryClient();
  const users = useQuery({ queryKey: ['admin-users'], queryFn: getAdminUsers });
  const [selected, setSelected] = useState<AdminUser | null>(null);
  const [userName, setUserName] = useState('');
  const [displayName, setDisplayName] = useState('');
  const [password, setPassword] = useState('');
  const [role, setRole] = useState<StaffRole>('Expert');
  const [isAvailable, setIsAvailable] = useState(true);
  const [notice, setNotice] = useState('');
  const save = useMutation({
    mutationFn: () => selected
      ? updateAdminUser(selected.id, { displayName, role, isAvailable })
      : createAdminUser({ userName, displayName, password, role, isAvailable }),
    onSuccess: async () => {
      const editing = Boolean(selected);
      reset();
      setNotice(editing ? 'Учетная запись обновлена.' : 'Учетная запись создана с одной рабочей ролью.');
      await queryClient.invalidateQueries({ queryKey: ['admin-users'] });
      await queryClient.invalidateQueries({ queryKey: ['admin-configuration'] });
    },
  });
  const stateMutation = useMutation({
    mutationFn: (item: AdminUser) => changeAdminUserState(item.id, !item.isActive, 'Административное управление доступом'),
    onSuccess: async (item) => {
      setNotice(item.isActive ? 'Учетная запись восстановлена.' : 'Учетная запись заблокирована; активные сессии отозваны.');
      await queryClient.invalidateQueries({ queryKey: ['admin-users'] });
      await queryClient.invalidateQueries({ queryKey: ['admin-configuration'] });
    },
  });

  function edit(item: AdminUser) {
    setSelected(item);
    setUserName(item.userName);
    setDisplayName(item.displayName);
    setRole(item.role);
    setIsAvailable(item.isAvailable);
    setPassword('');
  }

  function reset() {
    setSelected(null);
    setUserName('');
    setDisplayName('');
    setPassword('');
    setRole('Expert');
    setIsAvailable(true);
  }

  return (
    <div className="admin-workspace">
      <WorkspaceHeading
        eyebrow="Кабинет администратора"
        title="Сотрудники и доступ"
        copy="У каждого сотрудника ровно одна рабочая роль. Блокировка немедленно отзывает активную сессию."
      />
      {notice ? <p className="operator-notice" role="status">{notice}</p> : null}
      <section className="admin-section">
        <SectionHeading title={selected ? 'Изменить сотрудника' : 'Новый сотрудник'} copy="Пароль показывается только при создании и не попадает в журнал." />
        <form className="admin-form" onSubmit={(event) => { event.preventDefault(); save.mutate(); }}>
          <div className="admin-form__row">
            <label className="text-field">
              <span>Логин</span>
              <input required disabled={Boolean(selected)} minLength={3} value={userName} onChange={(event) => setUserName(event.target.value)} />
            </label>
            <label className="text-field">
              <span>Имя в кабинете</span>
              <input required minLength={2} value={displayName} onChange={(event) => setDisplayName(event.target.value)} />
            </label>
            <label className="select-field">
              <span>Роль</span>
              <select value={role} onChange={(event) => setRole(event.target.value as StaffRole)}>
                <option value="Operator">Оператор</option>
                <option value="Expert">Эксперт</option>
                <option value="Administrator">Администратор</option>
              </select>
            </label>
          </div>
          {!selected ? (
            <label className="text-field admin-password-field">
              <span>Временный пароль</span>
              <input required type="password" minLength={12} autoComplete="new-password" value={password} onChange={(event) => setPassword(event.target.value)} />
            </label>
          ) : null}
          {role === 'Expert' ? (
            <label className="check-field">
              <input type="checkbox" checked={isAvailable} onChange={(event) => setIsAvailable(event.target.checked)} />
              <span>Доступен для новых назначений</span>
            </label>
          ) : null}
          {save.isError ? <p className="form-error" role="alert">{adminError(save.error)}</p> : null}
          <div className="admin-actions">
            <md-filled-button type="submit" disabled={save.isPending}>{selected ? 'Сохранить' : 'Создать сотрудника'}</md-filled-button>
            {selected ? <md-outlined-button type="button" onClick={reset}>Отменить</md-outlined-button> : null}
          </div>
        </form>
      </section>
      <section className="admin-section">
        <SectionHeading title="Учетные записи" copy={`${users.data?.total ?? 0} сотрудников в служебном контуре`} />
        {users.isPending ? <p className="staff-copy">Загружаем сотрудников…</p> : null}
        {users.isError ? <p className="form-error" role="alert">Не удалось загрузить учетные записи.</p> : null}
        <div className="admin-list">
          {users.data?.items.map((item) => (
            <div className="admin-row" key={item.id}>
              <div>
                <strong>{item.displayName}</strong>
                <span>{item.userName} · {roleText(item.role)}</span>
                <span>{!item.isActive ? 'Доступ заблокирован' : item.role === 'Expert' && !item.isAvailable ? 'Не принимает новые назначения' : 'Рабочий доступ активен'}</span>
              </div>
              <div className="admin-row__actions">
                <button type="button" onClick={() => edit(item)}>Изменить</button>
                <button type="button" onClick={() => stateMutation.mutate(item)}>{item.isActive ? 'Заблокировать' : 'Восстановить'}</button>
              </div>
            </div>
          ))}
        </div>
      </section>
    </div>
  );
}

function StuckView() {
  const queryClient = useQueryClient();
  const [status, setStatus] = useState('');
  const [priority, setPriority] = useState('');
  const [selected, setSelected] = useState<StuckAppeal | null>(null);
  const stuck = useQuery({
    queryKey: ['admin-stuck', status, priority],
    queryFn: () => getStuckAppeals({ status: status || undefined, priority: priority || undefined }),
    refetchInterval: 30_000,
  });
  const config = useQuery({ queryKey: ['admin-configuration'], queryFn: getAdminConfiguration });

  return (
    <div className="admin-workspace">
      <WorkspaceHeading
        eyebrow="Кабинет администратора"
        title="Зависшие обращения"
        copy="Здесь только служебные метаданные. Администратор может вернуть движение обращению, но не читает его содержание и не закрывает его."
      />
      <PrivacyBoundary />
      <div className="admin-filter-row">
        <label className="select-field">
          <span>Статус</span>
          <select value={status} onChange={(event) => setStatus(event.target.value)}>
            <option value="">Все рабочие</option>
            <option value="New">Новое</option>
            <option value="Triaged">Проверено</option>
            <option value="Assigned">Распределено</option>
            <option value="InProgress">В работе</option>
            <option value="NeedsClarification">Нужно уточнение</option>
            <option value="Returned">Возвращено</option>
          </select>
        </label>
        <label className="select-field">
          <span>Приоритет</span>
          <select value={priority} onChange={(event) => setPriority(event.target.value)}>
            <option value="">Любой</option>
            <option value="Urgent">Срочный</option>
            <option value="Standard">Обычный</option>
            <option value="Low">Низкий</option>
          </select>
        </label>
      </div>
      {stuck.data ? <p className="admin-summary">{stuck.data.total} обращений требуют внимания · контрольный срок {stuck.data.stuckHours} ч</p> : null}
      {stuck.isPending ? <p className="staff-copy">Проверяем служебные сроки…</p> : null}
      {stuck.isError ? <p className="form-error" role="alert">Не удалось загрузить список.</p> : null}
      <div className={selected ? 'admin-split admin-split--detail' : 'admin-split'}>
        <div className="admin-list admin-stuck-list">
          {stuck.data?.items.map((item) => (
            <button className={selected?.id === item.id ? 'admin-row admin-row--button admin-row--selected' : 'admin-row admin-row--button'} type="button" key={item.id} onClick={() => setSelected(item)}>
              <span>
                <strong>{item.category}</strong>
                <span>{statusText(item.status)} · {priorityText(item.priority)}</span>
                <span>{item.assignedExpert ?? 'Без исполнителя'} · без движения {item.waitingHours} ч</span>
              </span>
              <span>{formatDateTime(item.lastStatusChangedAt)}</span>
            </button>
          ))}
        </div>
        <div className="admin-detail">
          {selected ? (
            <StuckInterventionForm
              appeal={selected}
              experts={config.data?.experts.filter((item) => item.isActive && item.isAvailable) ?? []}
              onClosed={async () => {
                setSelected(null);
                await queryClient.invalidateQueries({ queryKey: ['admin-stuck'] });
                await queryClient.invalidateQueries({ queryKey: ['admin-audit'] });
              }}
            />
          ) : (
            <div className="operator-detail__empty">
              <h2>Выберите обращение</h2>
              <p>Откроются только статус, приоритет, исполнитель и временные отметки.</p>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

function StuckInterventionForm({
  appeal,
  experts,
  onClosed,
}: {
  appeal: StuckAppeal;
  experts: Awaited<ReturnType<typeof getAdminConfiguration>>['experts'];
  onClosed: () => Promise<void>;
}) {
  const [status, setStatus] = useState(appeal.status);
  const [priority, setPriority] = useState(appeal.priority);
  const [expertId, setExpertId] = useState(appeal.assignedExpertId ?? '');
  const [reason, setReason] = useState('');
  useEffect(() => {
    setStatus(appeal.status);
    setPriority(appeal.priority);
    setExpertId(appeal.assignedExpertId ?? '');
    setReason('');
  }, [appeal]);
  const mutation = useMutation({
    mutationFn: () => interveneInStuckAppeal(appeal.id, {
      status,
      priority,
      assignedExpertId: expertId || null,
      expectedVersion: appeal.version,
      reason,
    }),
    onSuccess: onClosed,
  });

  return (
    <form className="admin-intervention" onSubmit={(event) => { event.preventDefault(); mutation.mutate(); }}>
      <p className="eyebrow">Безопасное разблокирование</p>
      <h2>{appeal.category}</h2>
      <dl className="safe-metadata">
        <div><dt>Создано</dt><dd>{formatDateTime(appeal.createdAt)}</dd></div>
        <div><dt>Последнее движение</dt><dd>{formatDateTime(appeal.lastStatusChangedAt)}</dd></div>
      </dl>
      <label className="select-field">
        <span>Рабочий статус</span>
        <select value={status} onChange={(event) => setStatus(event.target.value)}>
          <option value="New">Новое</option>
          <option value="Triaged">Проверено оператором</option>
          <option value="Assigned">Распределено</option>
          <option value="InProgress">В работе</option>
          <option value="NeedsClarification">Нужно уточнение</option>
          <option value="Returned">Возвращено оператору</option>
        </select>
      </label>
      <label className="select-field">
        <span>Приоритет</span>
        <select value={priority} onChange={(event) => setPriority(event.target.value)}>
          <option value="Urgent">Срочный</option>
          <option value="Standard">Обычный</option>
          <option value="Low">Низкий</option>
        </select>
      </label>
      <label className="select-field">
        <span>Ответственный</span>
        <select value={expertId} onChange={(event) => setExpertId(event.target.value)}>
          <option value="">Вернуть без исполнителя</option>
          {experts.map((item) => <option key={item.id} value={item.id}>{item.displayName}</option>)}
        </select>
      </label>
      <label className="text-field">
        <span>Причина изменения</span>
        <textarea required minLength={10} maxLength={1000} value={reason} onChange={(event) => setReason(event.target.value)} />
      </label>
      <p className="admin-form__hint">Причина и безопасные значения до/после сохранятся в журнале.</p>
      {mutation.isError ? <p className="form-error" role="alert">{adminError(mutation.error)}</p> : null}
      <md-filled-button type="submit" disabled={mutation.isPending}>Сохранить изменение</md-filled-button>
    </form>
  );
}

function AuditView() {
  const [action, setAction] = useState('');
  const [targetType, setTargetType] = useState('');
  const [selected, setSelected] = useState<AuditListItem | null>(null);
  const audit = useQuery({
    queryKey: ['admin-audit', action, targetType],
    queryFn: () => getAdminAudit({ action: action || undefined, targetType: targetType || undefined }),
  });
  const detail = useQuery({
    queryKey: ['admin-audit-detail', selected?.id],
    queryFn: () => getAdminAuditDetail(selected!.id),
    enabled: Boolean(selected),
  });

  return (
    <div className="admin-workspace">
      <WorkspaceHeading
        eyebrow="Кабинет администратора"
        title="Журнал действий"
        copy="Неизменяемая история административных изменений. В журнал не копируются обращения, сообщения, заметки, файлы и контакты."
      />
      <div className="admin-filter-row">
        <label className="select-field">
          <span>Действие</span>
          <select value={action} onChange={(event) => setAction(event.target.value)}>
            <option value="">Все действия</option>
            {audit.data?.filters.actions.map((item) => <option key={item} value={item}>{actionText(item)}</option>)}
          </select>
        </label>
        <label className="select-field">
          <span>Объект</span>
          <select value={targetType} onChange={(event) => setTargetType(event.target.value)}>
            <option value="">Все объекты</option>
            {audit.data?.filters.targetTypes.map((item) => <option key={item} value={item}>{targetText(item)}</option>)}
          </select>
        </label>
      </div>
      {audit.data ? <p className="admin-summary">{audit.data.total} событий</p> : null}
      {audit.isPending ? <p className="staff-copy">Загружаем журнал…</p> : null}
      {audit.isError ? <p className="form-error" role="alert">Не удалось загрузить журнал.</p> : null}
      <div className={selected ? 'admin-split admin-split--detail' : 'admin-split'}>
        <div className="admin-list">
          {audit.data?.items.map((item) => (
            <button className={selected?.id === item.id ? 'admin-row admin-row--button admin-row--selected' : 'admin-row admin-row--button'} type="button" key={item.id} onClick={() => setSelected(item)}>
              <span>
                <strong>{actionText(item.action)}</strong>
                <span>{item.actorDisplayName} · {targetText(item.targetType)}</span>
              </span>
              <span>{formatDateTime(item.occurredAt)}</span>
            </button>
          ))}
        </div>
        <div className="admin-detail">
          {!selected ? <div className="operator-detail__empty"><h2>Выберите событие</h2><p>Откроются безопасные значения до и после изменения.</p></div> : null}
          {selected && detail.isPending ? <p className="staff-copy">Открываем событие…</p> : null}
          {detail.data ? (
            <article className="audit-detail">
              <p className="eyebrow">Только для чтения</p>
              <h2>{actionText(detail.data.action)}</h2>
              <dl className="safe-metadata">
                <div><dt>Кто</dt><dd>{detail.data.actorDisplayName}</dd></div>
                <div><dt>Когда</dt><dd>{formatDateTime(detail.data.occurredAt)}</dd></div>
                <div><dt>Объект</dt><dd>{targetText(detail.data.targetType)}</dd></div>
                <div><dt>ID</dt><dd>{detail.data.targetId}</dd></div>
                {detail.data.reason ? <div><dt>Причина</dt><dd>{detail.data.reason}</dd></div> : null}
              </dl>
              <MetadataBlock title="До изменения" value={detail.data.before} />
              <MetadataBlock title="После изменения" value={detail.data.after} />
            </article>
          ) : null}
        </div>
      </div>
    </div>
  );
}

function MetadataBlock({ title, value }: { title: string; value: Record<string, unknown> | null }) {
  return (
    <section className="audit-metadata">
      <h3>{title}</h3>
      <pre>{value ? JSON.stringify(value, null, 2) : 'Нет данных'}</pre>
    </section>
  );
}

function WorkspaceHeading({ eyebrow, title, copy }: { eyebrow: string; title: string; copy: string }) {
  return (
    <header className="admin-heading">
      <p className="eyebrow">{eyebrow}</p>
      <h1 className="staff-title">{title}</h1>
      <p className="staff-copy">{copy}</p>
    </header>
  );
}

function SectionHeading({ id, title, copy }: { id?: string; title: string; copy: string }) {
  return (
    <header className="admin-section__heading">
      <h2 id={id}>{title}</h2>
      <p>{copy}</p>
    </header>
  );
}

function PrivacyBoundary() {
  return (
    <aside className="admin-privacy-boundary">
      <strong>Граница доступа</strong>
      <span>Тексты обращений, чат, заметки, вложения и кризисные контакты в административный API не входят.</span>
    </aside>
  );
}

function roleText(role: StaffRole) {
  return role === 'Operator' ? 'Оператор' : role === 'Expert' ? 'Эксперт' : 'Администратор';
}

function statusText(status: string) {
  const labels: Record<string, string> = {
    New: 'Новое',
    Triaged: 'Проверено оператором',
    Assigned: 'Распределено',
    InProgress: 'В работе',
    NeedsClarification: 'Нужно уточнение',
    Returned: 'Возвращено',
  };
  return labels[status] ?? status;
}

function priorityText(priority: string) {
  return priority === 'Urgent' ? 'Срочный' : priority === 'Low' ? 'Низкий' : 'Обычный';
}

function actionText(action: string) {
  const labels: Record<string, string> = {
    CategoryCreated: 'Категория создана',
    CategoryUpdated: 'Категория изменена',
    CategoryDeactivated: 'Категория деактивирована',
    ExpertGroupCreated: 'Группа создана',
    ExpertGroupUpdated: 'Группа изменена',
    ExpertGroupDeactivated: 'Группа деактивирована',
    RoutingRuleCreated: 'Правило создано',
    RoutingRuleUpdated: 'Правило изменено',
    RoutingRuleDeactivated: 'Правило деактивировано',
    StaffAccountCreated: 'Сотрудник создан',
    StaffAccountUpdated: 'Сотрудник изменен',
    StaffAccountBlocked: 'Доступ сотрудника заблокирован',
    StaffAccountRestored: 'Доступ сотрудника восстановлен',
    StuckAppealIntervened: 'Зависшее обращение разблокировано',
  };
  return labels[action] ?? action;
}

function targetText(target: string) {
  const labels: Record<string, string> = {
    Category: 'Категория',
    ExpertGroup: 'Группа экспертов',
    RoutingRule: 'Правило маршрутизации',
    StaffUser: 'Учетная запись',
    Appeal: 'Обращение',
  };
  return labels[target] ?? target;
}

function formatDateTime(value: string) {
  return new Intl.DateTimeFormat('ru-RU', { dateStyle: 'short', timeStyle: 'short' }).format(new Date(value));
}
