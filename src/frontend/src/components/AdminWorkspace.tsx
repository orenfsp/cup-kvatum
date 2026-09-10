import '@material/web/button/filled-button.js';
import '@material/web/button/outlined-button.js';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useEffect, useMemo, useRef, useState } from 'react';
import { Link, useLocation, useNavigate, useParams, useSearchParams } from 'react-router-dom';
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
  type AdminConfiguration,
  type AdminUser,
  type StuckAppeal,
} from '../api/admin';
import type { StaffRole } from '../api/staffAuth';
import { PageHeader, ResponsiveMasterDetail, UnsavedChangesGuard } from './ux/WorkspacePrimitives';

export type AdminView = 'overview' | 'categories' | 'groups' | 'rules' | 'users' | 'stuck' | 'audit';

const configurationKey = ['admin-configuration'] as const;

export function AdminWorkspace({ view }: { view: AdminView }) {
  if (view === 'overview') return <OverviewView />;
  if (view === 'categories') return <CategoriesView />;
  if (view === 'groups') return <GroupsView />;
  if (view === 'rules') return <RulesView />;
  if (view === 'users') return <UsersView />;
  if (view === 'stuck') return <StuckView />;
  return <AuditView />;
}

function OverviewView() {
  const configuration = useConfiguration();
  const stuck = useQuery({ queryKey: ['admin-stuck', '', ''], queryFn: () => getStuckAppeals({}) });
  const audit = useQuery({ queryKey: ['admin-audit', '', ''], queryFn: () => getAdminAudit({}) });
  const data = configuration.data;
  const activeRules = data?.rules.filter((item) => item.isActive) ?? [];
  const categoriesWithoutRule = data?.categories.filter(
    (category) => category.isActive && !activeRules.some((rule) => rule.categoryId === category.id),
  ) ?? [];
  const groupsWithoutExpert = data?.groups.filter((group) => group.isActive && !group.memberIds.some((id) => {
    const expert = data.experts.find((item) => item.id === id);
    return expert?.isActive && expert.isAvailable;
  })) ?? [];

  return (
    <div className="admin-workspace">
      <PageHeader
        eyebrow="Кабинет администратора"
        title="Задачи настройки и контроля"
        description="Обзор показывает только то, что требует действия. Статистика и обезличенные выгрузки находятся в отдельном разделе."
        action={<Link className="workspace-action-link" to="/staff/analytics">Открыть аналитику</Link>}
      />
      <PrivacyBoundary />
      {configuration.isPending || stuck.isPending || audit.isPending ? <LoadingText /> : null}
      {configuration.isError || stuck.isError || audit.isError ? <ErrorText text="Не удалось собрать административные задачи." /> : null}
      <section className="admin-task-list" aria-label="Задачи администратора">
        <AdminTask count={categoriesWithoutRule.length} title="Категории без активного правила" copy="Для них оператор не получит настроенную подсказку маршрута." to="/staff/admin/categories?state=unlinked" />
        <AdminTask count={groupsWithoutExpert.length} title="Группы без доступного эксперта" copy="Такие группы нельзя использовать как рабочий маршрут." to="/staff/admin/expert-groups?state=unavailable" />
        <AdminTask count={stuck.data?.total ?? 0} title="Зависшие обращения" copy="Можно проверить только статус, приоритет, назначение и сроки." to="/staff/admin/stuck" />
      </section>
      <section className="admin-overview-section" aria-labelledby="recent-admin-events">
        <SectionHeading id="recent-admin-events" title="Последние безопасные изменения" copy="Содержание обращений и коммуникаций сюда не попадает." />
        <div className="admin-list">
          {audit.data?.items.slice(0, 5).map((item) => (
            <Link className="admin-row admin-row--link" to={`/staff/admin/audit/${item.id}`} key={item.id}>
              <span><strong>{actionText(item.action)}</strong><span>{item.actorDisplayName} · {targetText(item.targetType)}</span></span>
              <span>{formatDateTime(item.occurredAt)}</span>
            </Link>
          ))}
          {audit.data && audit.data.items.length === 0 ? <EmptyText text="Изменений пока нет." /> : null}
        </div>
      </section>
    </div>
  );
}

function AdminTask({ count, title, copy, to }: { count: number; title: string; copy: string; to: string }) {
  return <Link className="admin-task" to={to}><strong>{count}</strong><span><b>{title}</b><small>{copy}</small></span><span aria-hidden="true">Открыть</span></Link>;
}

function CategoriesView() {
  const { categoryId } = useParams();
  const configuration = useConfiguration();
  const [searchParams, setSearchParams] = useSearchParams();
  const state = searchParams.get('state') ?? 'all';
  const categories = useMemo(() => {
    const items = configuration.data?.categories ?? [];
    if (state !== 'unlinked') return items;
    return items.filter((item) => item.isActive && !configuration.data?.rules.some((rule) => rule.isActive && rule.categoryId === item.id));
  }, [configuration.data, state]);

  return (
    <EntityLayout
      title="Категории обращений"
      description="Категория помогает заявителю назвать ситуацию и связывает обращение с правилом маршрутизации."
      action={<Link className="workspace-action-link" to="/staff/admin/categories/new">Создать категорию</Link>}
      hasDetail={Boolean(categoryId)} focusKey={categoryId} scrollKey={`admin-categories:${state}`}
      filters={<label className="select-field admin-compact-filter"><span>Показать</span><select value={state} onChange={(event) => setSearchParams(event.target.value === 'all' ? {} : { state: event.target.value })}><option value="all">Все категории</option><option value="unlinked">Без активного правила</option></select></label>}
      list={<EntityList pending={configuration.isPending} error={configuration.isError} empty={!categories.length}>{categories.map((item) => <EntityLink key={item.id} to={`/staff/admin/categories/${item.id}${searchParams.size ? `?${searchParams}` : ''}`} selected={categoryId === item.id} title={item.displayName} lines={[item.isActive ? 'Используется в форме' : 'Не используется в новых обращениях', `Порядок ${item.sortOrder} · обращений ${item.usageCount}`]} />)}</EntityList>}
      detail={categoryId ? <CategoryDetail key={categoryId} id={categoryId} configuration={configuration.data} pending={configuration.isPending} /> : <SelectPrompt title="Выберите категорию" copy="Откроются параметры категории и связанное правило." />}
    />
  );
}

function CategoryDetail({ id, configuration, pending }: { id: string; configuration?: AdminConfiguration; pending: boolean }) {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const isNew = id === 'new';
  const item = configuration?.categories.find((candidate) => candidate.id === id);
  const rule = configuration?.rules.find((candidate) => candidate.categoryId === id);
  const [editing, setEditing] = useState(isNew);
  const [code, setCode] = useState(() => isNew ? createSystemCode('category') : '');
  const [displayName, setDisplayName] = useState('');
  const [sortOrder, setSortOrder] = useState(50);
  const [confirming, setConfirming] = useState(false);

  useEffect(() => {
    setEditing(isNew); setCode((current) => item?.code ?? (isNew ? current || createSystemCode('category') : '')); setDisplayName(item?.displayName ?? ''); setSortOrder(item?.sortOrder ?? 50); setConfirming(false);
  }, [id, isNew, item?.code, item?.displayName, item?.sortOrder]);

  const dirty = editing && (code !== (item?.code ?? '') || displayName !== (item?.displayName ?? '') || sortOrder !== (item?.sortOrder ?? 50));
  const save = useMutation({
    mutationFn: () => item ? updateAdminCategory(item.id, { code, displayName, sortOrder, expectedVersion: item.version }) : createAdminCategory({ code, displayName, sortOrder }),
    onSuccess: async (saved) => { await queryClient.invalidateQueries({ queryKey: configurationKey }); navigate(`/staff/admin/categories/${saved.id}`, { replace: isNew, state: { notice: item ? 'Категория обновлена.' : 'Категория создана.', created: !item } }); setEditing(false); },
  });
  const deactivate = useMutation({ mutationFn: () => deactivateAdminCategory(item!.id, item!.version), onSuccess: async () => { setConfirming(false); await queryClient.invalidateQueries({ queryKey: configurationKey }); } });

  if (pending) return <LoadingText />;
  if (!isNew && !item) return <NotFound entity="Категория" to="/staff/admin/categories" />;

  return (
    <article className="admin-entity-detail">
      <UnsavedChangesGuard when={dirty && !save.isPending} />
      <DetailBack to="/staff/admin/categories" label="К списку категорий" />
      <p className="eyebrow">{isNew ? 'Новая категория' : 'Категория'}</p><h2 data-detail-heading tabIndex={-1}>{isNew ? 'Создайте категорию' : item!.displayName}</h2><RouteNotice />
      {editing ? (
        <form className="admin-form" onSubmit={(event) => { event.preventDefault(); save.mutate(); }}>
          <label className="text-field"><span>Название для заявителя</span><input required minLength={2} value={displayName} onChange={(event) => setDisplayName(event.target.value)} placeholder="Безопасность в школе" /></label>
          <p className="admin-form__hint">Служебный код создаётся автоматически и не требует ручного ввода.</p>
          <label className="text-field admin-number-field"><span>Порядок в форме</span><input type="number" min={0} max={10000} value={sortOrder} onChange={(event) => setSortOrder(Number(event.target.value))} /></label>
          {save.isError ? <ErrorText text={adminError(save.error)} /> : null}
          <div className="admin-actions"><md-filled-button type="submit" disabled={save.isPending}>{item ? 'Сохранить изменения' : 'Создать категорию'}</md-filled-button>{!isNew ? <md-outlined-button type="button" onClick={() => setEditing(false)}>Отменить</md-outlined-button> : null}</div>
        </form>
      ) : (
        <>
          <SafeMetadata rows={[["Статус", item!.isActive ? 'Используется в новых обращениях' : 'Деактивирована'], ['Код', item!.code], ['Порядок в форме', String(item!.sortOrder)], ['Использовано', `${item!.usageCount} обращений`], ['Версия', String(item!.version)]]} />
          <section className="admin-relationship" aria-labelledby="category-rule-heading"><SectionHeading id="category-rule-heading" title="Правило маршрутизации" copy="Связь определяет, какую группу предложить оператору." />{rule ? <Link to={`/staff/admin/routing-rules/${rule.id}`}>{rule.expertGroup} · версия {rule.version}</Link> : <Link to={`/staff/admin/routing-rules/new?categoryId=${item!.id}`}>Создать правило для категории</Link>}</section>
          <div className="admin-actions"><md-filled-button onClick={() => setEditing(true)}>Изменить</md-filled-button>{item!.isActive && item!.code !== 'unsure' ? <md-outlined-button onClick={() => setConfirming(true)}>Деактивировать</md-outlined-button> : null}</div>
          {confirming ? <ImpactConfirmation title="Деактивировать категорию?" impact="Она исчезнет из формы новых обращений. В ранее созданных обращениях название и история сохранятся. Связанное правило не удаляется." pending={deactivate.isPending} error={deactivate.isError ? adminError(deactivate.error) : undefined} confirmLabel="Деактивировать категорию" onCancel={() => setConfirming(false)} onConfirm={() => deactivate.mutate()} /> : null}
          <CreatedNextStep when="category" to={`/staff/admin/expert-groups/new?categoryId=${item!.id}`} label="Создать группу экспертов" />
        </>
      )}
    </article>
  );
}

function GroupsView() {
  const { groupId } = useParams();
  const configuration = useConfiguration();
  const [searchParams, setSearchParams] = useSearchParams();
  const state = searchParams.get('state') ?? 'all';
  const groups = useMemo(() => {
    const data = configuration.data;
    if (!data || state !== 'unavailable') return data?.groups ?? [];
    return data.groups.filter((group) => group.isActive && !group.memberIds.some((id) => { const expert = data.experts.find((candidate) => candidate.id === id); return expert?.isActive && expert.isAvailable; }));
  }, [configuration.data, state]);

  return (
    <EntityLayout title="Группы экспертов" description="Группа объединяет специалистов одного профиля и задаёт рабочий лимит назначений." action={<Link className="workspace-action-link" to="/staff/admin/expert-groups/new">Создать группу</Link>} hasDetail={Boolean(groupId)} focusKey={groupId} scrollKey={`admin-groups:${state}`}
      filters={<label className="select-field admin-compact-filter"><span>Показать</span><select value={state} onChange={(event) => setSearchParams(event.target.value === 'all' ? {} : { state: event.target.value })}><option value="all">Все группы</option><option value="unavailable">Без доступных экспертов</option></select></label>}
      list={<EntityList pending={configuration.isPending} error={configuration.isError} empty={!groups.length}>{groups.map((item) => { const available = item.memberIds.filter((id) => configuration.data?.experts.some((expert) => expert.id === id && expert.isActive && expert.isAvailable)).length; return <EntityLink key={item.id} to={`/staff/admin/expert-groups/${item.id}${searchParams.size ? `?${searchParams}` : ''}`} selected={groupId === item.id} title={item.displayName} lines={[item.isActive ? 'Активная группа' : 'Не используется для новых маршрутов', `Участников ${item.memberIds.length} · доступны ${available} · лимит ${item.activeAppealLimit}`]} />; })}</EntityList>}
      detail={groupId ? <GroupDetail key={groupId} id={groupId} configuration={configuration.data} pending={configuration.isPending} /> : <SelectPrompt title="Выберите группу" copy="Откроются состав, лимит и использующие группу правила." />}
    />
  );
}

function GroupDetail({ id, configuration, pending }: { id: string; configuration?: AdminConfiguration; pending: boolean }) {
  const navigate = useNavigate(); const queryClient = useQueryClient(); const [searchParams] = useSearchParams(); const isNew = id === 'new';
  const item = configuration?.groups.find((candidate) => candidate.id === id);
  const usedRules = configuration?.rules.filter((candidate) => candidate.expertGroupId === id) ?? [];
  const [editing, setEditing] = useState(isNew); const [code, setCode] = useState(() => isNew ? createSystemCode('group') : ''); const [displayName, setDisplayName] = useState(''); const [limit, setLimit] = useState(5); const [expertIds, setExpertIds] = useState<string[]>([]); const [confirming, setConfirming] = useState(false);
  useEffect(() => { setEditing(isNew); setCode((current) => item?.code ?? (isNew ? current || createSystemCode('group') : '')); setDisplayName(item?.displayName ?? ''); setLimit(item?.activeAppealLimit ?? 5); setExpertIds(item?.memberIds ?? []); setConfirming(false); }, [id, isNew, item?.code, item?.displayName, item?.activeAppealLimit, item?.memberIds]);
  const baseIds = item?.memberIds ?? [];
  const dirty = editing && (code !== (item?.code ?? '') || displayName !== (item?.displayName ?? '') || limit !== (item?.activeAppealLimit ?? 5) || [...expertIds].sort().join() !== [...baseIds].sort().join());
  const sourceCategoryId = searchParams.get('categoryId');
  const save = useMutation({ mutationFn: () => item ? updateAdminGroup(item.id, { code, displayName, activeAppealLimit: limit, expertIds, expectedVersion: item.version }) : createAdminGroup({ code, displayName, activeAppealLimit: limit, expertIds }), onSuccess: async (saved) => { await queryClient.invalidateQueries({ queryKey: configurationKey }); navigate(`/staff/admin/expert-groups/${saved.id}${sourceCategoryId ? `?categoryId=${sourceCategoryId}` : ''}`, { replace: isNew, state: { notice: item ? 'Группа обновлена.' : 'Группа создана.', created: !item } }); setEditing(false); } });
  const deactivate = useMutation({ mutationFn: () => deactivateAdminGroup(item!.id, item!.version), onSuccess: async () => { setConfirming(false); await queryClient.invalidateQueries({ queryKey: configurationKey }); } });
  if (pending) return <LoadingText />;
  if (!isNew && !item) return <NotFound entity="Группа" to="/staff/admin/expert-groups" />;
  const availableCount = item?.memberIds.filter((memberId) => configuration?.experts.some((expert) => expert.id === memberId && expert.isActive && expert.isAvailable)).length ?? 0;

  return (
    <article className="admin-entity-detail">
      <UnsavedChangesGuard when={dirty && !save.isPending} /><DetailBack to="/staff/admin/expert-groups" label="К списку групп" /><p className="eyebrow">{isNew ? 'Новая группа' : 'Группа экспертов'}</p><h2 data-detail-heading tabIndex={-1}>{isNew ? 'Создайте профильную группу' : item!.displayName}</h2><RouteNotice />
      {editing ? (
        <form className="admin-form" onSubmit={(event) => { event.preventDefault(); save.mutate(); }}>
          <label className="text-field"><span>Название группы</span><input required minLength={2} value={displayName} onChange={(event) => setDisplayName(event.target.value)} placeholder="Школьная поддержка" /></label><label className="text-field admin-number-field"><span>Активных обращений на эксперта</span><input type="number" min={1} max={100} value={limit} onChange={(event) => setLimit(Number(event.target.value))} /></label><p className="admin-form__hint">Служебный код создаётся автоматически и не требует ручного ввода.</p>
          <fieldset className="admin-members"><legend>Состав группы</legend>{configuration?.experts.map((expert) => <label className="check-field" key={expert.id}><input type="checkbox" checked={expertIds.includes(expert.id)} onChange={() => setExpertIds((current) => current.includes(expert.id) ? current.filter((candidate) => candidate !== expert.id) : [...current, expert.id])} /><span>{expert.displayName}{!expert.isActive ? ' — доступ заблокирован' : !expert.isAvailable ? ' — не принимает новые назначения' : ''}</span></label>)}</fieldset>
          {save.isError ? <ErrorText text={adminError(save.error)} /> : null}<div className="admin-actions"><md-filled-button type="submit" disabled={save.isPending}>{item ? 'Сохранить изменения' : 'Создать группу'}</md-filled-button>{!isNew ? <md-outlined-button type="button" onClick={() => setEditing(false)}>Отменить</md-outlined-button> : null}</div>
        </form>
      ) : (
        <>
          <SafeMetadata rows={[["Статус", item!.isActive ? 'Используется в маршрутах' : 'Деактивирована'], ['Код', item!.code], ['Лимит на эксперта', String(item!.activeAppealLimit)], ['Участников', String(item!.memberIds.length)], ['Доступны сейчас', String(availableCount)], ['Версия', String(item!.version)]]} />
          <section className="admin-relationship"><SectionHeading title="Состав" copy="Недоступный или заблокированный эксперт остаётся виден в составе, но не предлагается оператору." /><div className="admin-related-list">{item!.memberIds.map((memberId) => { const expert = configuration?.experts.find((candidate) => candidate.id === memberId); return expert ? <Link key={memberId} to={`/staff/admin/users/${memberId}`}>{expert.displayName} · {expert.isActive ? expert.isAvailable ? 'доступен' : 'не принимает назначения' : 'заблокирован'}</Link> : null; })}{item!.memberIds.length === 0 ? <EmptyText text="В группе пока нет экспертов." /> : null}</div></section>
          <section className="admin-relationship"><SectionHeading title="Использующие правила" copy="Переход откроет правило и связанные с ним сущности." /><div className="admin-related-list">{usedRules.map((rule) => <Link key={rule.id} to={`/staff/admin/routing-rules/${rule.id}`}>{rule.category} · версия {rule.version}{rule.isActive ? '' : ' · неактивно'}</Link>)}{usedRules.length === 0 ? <EmptyText text="Группа пока не связана с правилами." /> : null}</div></section>
          <div className="admin-actions"><md-filled-button onClick={() => setEditing(true)}>Изменить</md-filled-button>{item!.isActive ? <md-outlined-button onClick={() => setConfirming(true)}>Деактивировать</md-outlined-button> : null}</div>
          {confirming ? <ImpactConfirmation title="Деактивировать группу?" impact="Группа перестанет быть доступна для новых маршрутов. Участники и история правил сохранятся, текущие назначения не изменятся." pending={deactivate.isPending} error={deactivate.isError ? adminError(deactivate.error) : undefined} confirmLabel="Деактивировать группу" onCancel={() => setConfirming(false)} onConfirm={() => deactivate.mutate()} /> : null}<CreatedNextStep when="group" to={`/staff/admin/routing-rules/new?groupId=${item!.id}${sourceCategoryId ? `&categoryId=${sourceCategoryId}` : ''}`} label="Создать правило маршрутизации" />
        </>
      )}
    </article>
  );
}

function RulesView() {
  const { ruleId } = useParams(); const configuration = useConfiguration();
  return <EntityLayout title="Правила маршрутизации" description="Каждое правило связывает одну категорию с одной профильной группой. Версия сохраняется у обращения." action={<Link className="workspace-action-link" to="/staff/admin/routing-rules/new">Создать правило</Link>} hasDetail={Boolean(ruleId)} focusKey={ruleId} scrollKey="admin-rules" list={<EntityList pending={configuration.isPending} error={configuration.isError} empty={!configuration.data?.rules.length}>{configuration.data?.rules.map((item) => <EntityLink key={item.id} to={`/staff/admin/routing-rules/${item.id}`} selected={ruleId === item.id} title={item.category} lines={[`Группа: ${item.expertGroup}`, `Версия ${item.version} · ${item.isActive ? 'активно' : 'неактивно'}`]} />)}</EntityList>} detail={ruleId ? <RuleDetail key={ruleId} id={ruleId} configuration={configuration.data} pending={configuration.isPending} /> : <SelectPrompt title="Выберите правило" copy="Откроются категория, группа и версия правила." />} />;
}

function RuleDetail({ id, configuration, pending }: { id: string; configuration?: AdminConfiguration; pending: boolean }) {
  const navigate = useNavigate(); const queryClient = useQueryClient(); const [searchParams] = useSearchParams(); const isNew = id === 'new';
  const item = configuration?.rules.find((candidate) => candidate.id === id); const activeCategories = configuration?.categories.filter((candidate) => candidate.isActive) ?? []; const activeGroups = configuration?.groups.filter((candidate) => candidate.isActive) ?? [];
  const [editing, setEditing] = useState(isNew); const [categoryId, setCategoryId] = useState(''); const [groupId, setGroupId] = useState(''); const [confirming, setConfirming] = useState(false);
  useEffect(() => { setEditing(isNew); setCategoryId(item?.categoryId ?? searchParams.get('categoryId') ?? activeCategories[0]?.id ?? ''); setGroupId(item?.expertGroupId ?? searchParams.get('groupId') ?? activeGroups[0]?.id ?? ''); setConfirming(false); }, [id, isNew, item?.categoryId, item?.expertGroupId, searchParams, activeCategories[0]?.id, activeGroups[0]?.id]);
  const currentCategoryRule = configuration?.rules.find((candidate) => candidate.categoryId === categoryId);
  const dirty = editing && (categoryId !== (item?.categoryId ?? '') || groupId !== (item?.expertGroupId ?? ''));
  const save = useMutation({ mutationFn: () => saveAdminRule({ categoryId, expertGroupId: groupId, expectedVersion: item?.version ?? currentCategoryRule?.version ?? null }), onSuccess: async (saved) => { await queryClient.invalidateQueries({ queryKey: configurationKey }); navigate(`/staff/admin/routing-rules/${saved.id}`, { replace: isNew, state: { notice: item || currentCategoryRule ? 'Правило обновлено; новая версия применяется к новым подсказкам.' : 'Правило создано.' } }); setEditing(false); } });
  const deactivate = useMutation({ mutationFn: () => deactivateAdminRule(item!.id, item!.version), onSuccess: async () => { setConfirming(false); await queryClient.invalidateQueries({ queryKey: configurationKey }); } });
  if (pending) return <LoadingText />; if (!isNew && !item) return <NotFound entity="Правило" to="/staff/admin/routing-rules" />;
  const category = configuration?.categories.find((candidate) => candidate.id === item?.categoryId); const group = configuration?.groups.find((candidate) => candidate.id === item?.expertGroupId);
  return <article className="admin-entity-detail"><UnsavedChangesGuard when={dirty && !save.isPending} /><DetailBack to="/staff/admin/routing-rules" label="К списку правил" /><p className="eyebrow">{isNew ? 'Новое правило' : 'Правило маршрутизации'}</p><h2 data-detail-heading tabIndex={-1}>{isNew ? 'Свяжите категорию и группу' : item!.category}</h2><RouteNotice />
    {editing ? <form className="admin-form" onSubmit={(event) => { event.preventDefault(); save.mutate(); }}><label className="select-field"><span>Категория</span><select value={categoryId} onChange={(event) => setCategoryId(event.target.value)}>{activeCategories.map((candidate) => <option value={candidate.id} key={candidate.id}>{candidate.displayName}</option>)}</select></label><label className="select-field"><span>Профильная группа</span><select value={groupId} onChange={(event) => setGroupId(event.target.value)}>{activeGroups.map((candidate) => <option value={candidate.id} key={candidate.id}>{candidate.displayName}</option>)}</select></label>{currentCategoryRule && currentCategoryRule.id !== item?.id ? <p className="admin-form__hint">Для этой категории уже есть правило версии {currentCategoryRule.version}; сохранение обновит его.</p> : null}{save.isError ? <ErrorText text={adminError(save.error)} /> : null}<div className="admin-actions"><md-filled-button type="submit" disabled={save.isPending || !categoryId || !groupId}>Сохранить правило</md-filled-button>{!isNew ? <md-outlined-button type="button" onClick={() => setEditing(false)}>Отменить</md-outlined-button> : null}</div></form> : <><SafeMetadata rows={[["Статус", item!.isActive ? 'Используется для новых подсказок' : 'Деактивировано'], ['Версия', String(item!.version)], ['Обновлено', formatDateTime(item!.updatedAt)]]} /><section className="admin-relationship"><SectionHeading title="Связи правила" copy="Откройте исходную категорию или группу, не теряя контекст конфигурации." /><div className="admin-related-list">{category ? <Link to={`/staff/admin/categories/${category.id}`}>Категория: {category.displayName}</Link> : null}{group ? <Link to={`/staff/admin/expert-groups/${group.id}`}>Группа: {group.displayName}</Link> : null}</div></section><div className="admin-actions"><md-filled-button onClick={() => setEditing(true)}>Изменить группу</md-filled-button>{item!.isActive ? <md-outlined-button onClick={() => setConfirming(true)}>Деактивировать</md-outlined-button> : null}</div>{confirming ? <ImpactConfirmation title="Деактивировать правило?" impact="Новые обращения этой категории останутся без настроенной подсказки маршрута. Уже применённые версии и история обращений не изменятся." pending={deactivate.isPending} error={deactivate.isError ? adminError(deactivate.error) : undefined} confirmLabel="Деактивировать правило" onCancel={() => setConfirming(false)} onConfirm={() => deactivate.mutate()} /> : null}</>}
  </article>;
}

function UsersView() {
  const { userId } = useParams(); const users = useQuery({ queryKey: ['admin-users'], queryFn: getAdminUsers }); const [searchParams, setSearchParams] = useSearchParams(); const role = searchParams.get('role') ?? 'all'; const state = searchParams.get('state') ?? 'all';
  const filtered = users.data?.items.filter((item) => (role === 'all' || item.role === role) && (state === 'all' || (state === 'active') === item.isActive)) ?? [];
  const updateFilter = (key: string, value: string) => { const next = new URLSearchParams(searchParams); if (value === 'all') next.delete(key); else next.set(key, value); setSearchParams(next); };
  return <EntityLayout title="Сотрудники и доступ" description="Список отделён от создания и редактирования. У сотрудника одна рабочая роль; блокировка отзывает активные сессии." action={<Link className="workspace-action-link" to="/staff/admin/users/new">Создать сотрудника</Link>} hasDetail={Boolean(userId)} focusKey={userId} scrollKey={`admin-users:${role}:${state}`}
    filters={<div className="admin-filter-row admin-filter-row--inline"><label className="select-field"><span>Роль</span><select value={role} onChange={(event) => updateFilter('role', event.target.value)}><option value="all">Все роли</option><option value="Operator">Операторы</option><option value="Expert">Эксперты</option><option value="Administrator">Администраторы</option></select></label><label className="select-field"><span>Доступ</span><select value={state} onChange={(event) => updateFilter('state', event.target.value)}><option value="all">Любой</option><option value="active">Активен</option><option value="blocked">Заблокирован</option></select></label></div>}
    list={<EntityList pending={users.isPending} error={users.isError} empty={!filtered.length}>{filtered.map((item) => <EntityLink key={item.id} to={`/staff/admin/users/${item.id}${searchParams.size ? `?${searchParams}` : ''}`} selected={userId === item.id} title={item.displayName} lines={[`${item.userName} · ${roleText(item.role)}`, !item.isActive ? 'Доступ заблокирован' : item.role === 'Expert' && !item.isAvailable ? 'Не принимает новые назначения' : 'Рабочий доступ активен']} />)}</EntityList>}
    detail={userId ? <UserDetail key={userId} id={userId} users={users.data?.items} pending={users.isPending} /> : <SelectPrompt title="Выберите сотрудника" copy="Откроются роль, состояние доступа и доступность эксперта." />} />;
}

function UserDetail({ id, users, pending }: { id: string; users?: AdminUser[]; pending: boolean }) {
  const queryClient = useQueryClient(); const isNew = id === 'new'; const item = users?.find((candidate) => candidate.id === id);
  const [editing, setEditing] = useState(isNew); const [userName, setUserName] = useState(''); const [displayName, setDisplayName] = useState(''); const [password, setPassword] = useState(''); const [role, setRole] = useState<StaffRole>('Expert'); const [isAvailable, setIsAvailable] = useState(true); const [credential, setCredential] = useState<{ userName: string; password: string } | null>(null); const [confirming, setConfirming] = useState(false); const [reason, setReason] = useState(''); const [notice, setNotice] = useState('');
  useEffect(() => { setEditing(isNew); setUserName(item?.userName ?? ''); setDisplayName(item?.displayName ?? ''); setPassword(''); setRole(item?.role ?? 'Expert'); setIsAvailable(item?.isAvailable ?? true); setConfirming(false); setReason(''); setCredential(null); }, [id, isNew, item?.userName, item?.displayName, item?.role, item?.isAvailable]);
  const dirty = editing && (userName !== (item?.userName ?? '') || displayName !== (item?.displayName ?? '') || password !== '' || role !== (item?.role ?? 'Expert') || isAvailable !== (item?.isAvailable ?? true));
  const save = useMutation({ mutationFn: () => item ? updateAdminUser(item.id, { displayName, role, isAvailable }) : createAdminUser({ userName, displayName, password, role, isAvailable }), onSuccess: async () => { await queryClient.invalidateQueries({ queryKey: ['admin-users'] }); await queryClient.invalidateQueries({ queryKey: configurationKey }); if (!item) setCredential({ userName, password }); else { setEditing(false); setNotice('Учётная запись обновлена.'); } } });
  const stateMutation = useMutation({ mutationFn: () => changeAdminUserState(item!.id, !item!.isActive, reason || undefined), onSuccess: async () => { setNotice(item!.isActive ? 'Доступ заблокирован, активные сессии отозваны.' : 'Доступ сотрудника восстановлен.'); setConfirming(false); setReason(''); await queryClient.invalidateQueries({ queryKey: ['admin-users'] }); await queryClient.invalidateQueries({ queryKey: configurationKey }); } });
  if (pending) return <LoadingText />; if (!isNew && !item) return <NotFound entity="Сотрудник" to="/staff/admin/users" />;
  if (credential) return <article className="admin-entity-detail"><DetailBack to="/staff/admin/users" label="К списку сотрудников" /><p className="eyebrow">Создание завершено</p><DetailHeading focusWhen={credential.password}>Передайте временный пароль сотруднику</DetailHeading><p className="staff-copy">Пароль показан один раз. После ухода с этой страницы восстановить его нельзя.</p><div className="temporary-credential"><span>Логин</span><strong>{credential.userName}</strong><span>Временный пароль</span><code>{credential.password}</code><button type="button" onClick={async () => { await navigator.clipboard.writeText(credential.password); setNotice('Пароль скопирован.'); }}>Скопировать пароль</button></div>{notice ? <p className="form-success" role="status">{notice}</p> : null}<Link className="workspace-action-link" to="/staff/admin/users">Готово, вернуться к списку</Link></article>;
  return <article className="admin-entity-detail"><UnsavedChangesGuard when={dirty && !save.isPending} /><DetailBack to="/staff/admin/users" label="К списку сотрудников" /><p className="eyebrow">{isNew ? 'Новая учетная запись' : roleText(item!.role)}</p><h2 data-detail-heading tabIndex={-1}>{isNew ? 'Создайте сотрудника' : item!.displayName}</h2>{notice ? <p className="form-success" role="status">{notice}</p> : null}
    {editing ? <form className="admin-form" onSubmit={(event) => { event.preventDefault(); save.mutate(); }}><label className="text-field"><span>Логин</span><input required disabled={Boolean(item)} minLength={3} value={userName} onChange={(event) => setUserName(event.target.value)} /></label><label className="text-field"><span>Имя в кабинете</span><input required minLength={2} value={displayName} onChange={(event) => setDisplayName(event.target.value)} /></label><label className="select-field"><span>Рабочая роль</span><select value={role} onChange={(event) => setRole(event.target.value as StaffRole)}><option value="Operator">Оператор</option><option value="Expert">Эксперт</option><option value="Administrator">Администратор</option></select></label>{!item ? <label className="text-field admin-password-field"><span>Временный пароль</span><input required type="password" minLength={12} autoComplete="new-password" value={password} onChange={(event) => setPassword(event.target.value)} /><small>После создания пароль появится один раз с кнопкой копирования.</small></label> : null}{role === 'Expert' ? <label className="check-field"><input type="checkbox" checked={isAvailable} onChange={(event) => setIsAvailable(event.target.checked)} /><span>Доступен для новых назначений</span></label> : null}{save.isError ? <ErrorText text={adminError(save.error)} /> : null}<div className="admin-actions"><md-filled-button type="submit" disabled={save.isPending}>{item ? 'Сохранить изменения' : 'Создать сотрудника'}</md-filled-button>{item ? <md-outlined-button type="button" onClick={() => setEditing(false)}>Отменить</md-outlined-button> : null}</div></form> : <><SafeMetadata rows={[["Логин", item!.userName], ['Роль', roleText(item!.role)], ['Состояние доступа', item!.isActive ? 'Активен' : 'Заблокирован'], ...(item!.role === 'Expert' ? [['Новые назначения', item!.isAvailable ? 'Принимает' : 'Не принимает'] as [string, string]] : []), ['Создан', formatDateTime(item!.createdAt)], ['Обновлён', formatDateTime(item!.updatedAt)]]} /><div className="admin-actions"><md-filled-button onClick={() => setEditing(true)}>Изменить</md-filled-button><md-outlined-button onClick={() => setConfirming(true)}>{item!.isActive ? 'Заблокировать доступ' : 'Восстановить доступ'}</md-outlined-button></div>{confirming ? <ImpactConfirmation title={item!.isActive ? 'Заблокировать доступ?' : 'Восстановить доступ?'} impact={item!.isActive ? 'Сотрудник сразу потеряет доступ: все активные сессии будут отозваны. Его прошлая работа и записи в журнале сохранятся.' : 'Сотрудник снова сможет войти с действующим паролем. Роль и доступность эксперта не изменятся.'} pending={stateMutation.isPending} error={stateMutation.isError ? adminError(stateMutation.error) : undefined} confirmLabel={item!.isActive ? 'Заблокировать и отозвать сессии' : 'Восстановить доступ'} onCancel={() => { setConfirming(false); setReason(''); }} onConfirm={() => stateMutation.mutate()}><label className="text-field"><span>Причина {item!.isActive ? 'блокировки' : 'восстановления'}</span><textarea required={item!.isActive} minLength={item!.isActive ? 10 : undefined} maxLength={1000} value={reason} onChange={(event) => setReason(event.target.value)} /></label></ImpactConfirmation> : null}</>}
  </article>;
}

function StuckView() {
  const { appealId } = useParams(); const [searchParams, setSearchParams] = useSearchParams(); const status = searchParams.get('status') ?? ''; const priority = searchParams.get('priority') ?? '';
  const stuck = useQuery({ queryKey: ['admin-stuck', status, priority], queryFn: () => getStuckAppeals({ status: status || undefined, priority: priority || undefined }), refetchInterval: 30_000 }); const config = useConfiguration(); const selected = stuck.data?.items.find((item) => item.id === appealId);
  const updateFilter = (key: string, value: string) => { const next = new URLSearchParams(searchParams); if (value) next.set(key, value); else next.delete(key); setSearchParams(next); };
  return <EntityLayout title="Зависшие обращения" description="Администратор возвращает процесс в движение только по служебным метаданным и не читает содержание обращения." hasDetail={Boolean(appealId)} focusKey={appealId} scrollKey={`admin-stuck:${status}:${priority}`} privacy
    filters={<div className="admin-filter-row admin-filter-row--inline"><label className="select-field"><span>Статус</span><select value={status} onChange={(event) => updateFilter('status', event.target.value)}><option value="">Все рабочие</option>{administrativeStatuses.map((value) => <option value={value} key={value}>{statusText(value)}</option>)}</select></label><label className="select-field"><span>Приоритет</span><select value={priority} onChange={(event) => updateFilter('priority', event.target.value)}><option value="">Любой</option><option value="Urgent">Срочный</option><option value="Standard">Обычный</option><option value="Low">Низкий</option></select></label></div>}
    list={<EntityList pending={stuck.isPending} error={stuck.isError} empty={!stuck.data?.items.length}>{stuck.data?.items.map((item) => <EntityLink key={item.id} to={`/staff/admin/stuck/${item.id}${searchParams.size ? `?${searchParams}` : ''}`} selected={appealId === item.id} title={item.category} lines={[`${statusText(item.status)} · ${priorityText(item.priority)}`, `${item.assignedExpert ?? 'Без исполнителя'} · без движения ${item.waitingHours} ч`]} trailing={formatDateTime(item.lastStatusChangedAt)} />)}</EntityList>}
    detail={appealId ? selected ? <StuckInterventionForm appeal={selected} experts={config.data?.experts.filter((item) => item.isActive && item.isAvailable) ?? []} /> : stuck.isPending ? <LoadingText /> : <NotFound entity="Зависшее обращение" to="/staff/admin/stuck" /> : <SelectPrompt title="Выберите обращение" copy="Откроются только разрешённые метаданные и одно действие разблокировки." />} />;
}

function StuckInterventionForm({ appeal, experts }: { appeal: StuckAppeal; experts: AdminConfiguration['experts'] }) {
  const queryClient = useQueryClient(); const [status, setStatus] = useState(appeal.status); const [priority, setPriority] = useState(appeal.priority); const [expertId, setExpertId] = useState(appeal.assignedExpertId ?? ''); const [reason, setReason] = useState(''); const [previewing, setPreviewing] = useState(false);
  useEffect(() => { setStatus(appeal.status); setPriority(appeal.priority); setExpertId(appeal.assignedExpertId ?? ''); setReason(''); setPreviewing(false); }, [appeal]);
  const mutation = useMutation({ mutationFn: () => interveneInStuckAppeal(appeal.id, { status, priority, assignedExpertId: expertId || null, expectedVersion: appeal.version, reason }), onSuccess: async () => { await queryClient.invalidateQueries({ queryKey: ['admin-stuck'], refetchType: 'none' }); await queryClient.invalidateQueries({ queryKey: ['admin-audit'] }); } });
  const selectedExpert = experts.find((item) => item.id === expertId)?.displayName ?? 'Без исполнителя'; const changed = status !== appeal.status || priority !== appeal.priority || expertId !== (appeal.assignedExpertId ?? '');
  const beforeRows: Array<[string, string]> = [['Статус', statusText(appeal.status)], ['Приоритет', priorityText(appeal.priority)], ['Ответственный', appeal.assignedExpert ?? 'Без исполнителя']]; const afterRows: Array<[string, string]> = [['Статус', statusText(status)], ['Приоритет', priorityText(priority)], ['Ответственный', selectedExpert]];
  if (mutation.data) return <article className="admin-entity-detail"><DetailBack to="/staff/admin/stuck" label="К списку зависших" /><p className="eyebrow">Разблокирование завершено</p><DetailHeading focusWhen={mutation.data.auditEventId}>Обращение возвращено в рабочий процесс</DetailHeading><p className="form-success" role="status">Изменение сохранено. Открытые административные сигналы сняты.</p><Link className="workspace-action-link" to={`/staff/admin/audit/${mutation.data.auditEventId}`}>Открыть событие в журнале</Link></article>;
  return <article className="admin-entity-detail admin-intervention"><UnsavedChangesGuard when={(changed || reason.length > 0) && !mutation.isPending} /><DetailBack to="/staff/admin/stuck" label="К списку зависших" /><p className="eyebrow">Безопасное разблокирование</p><h2 data-detail-heading tabIndex={-1}>{appeal.category}</h2><PrivacyBoundary compact /><SafeMetadata rows={[["Создано", formatDateTime(appeal.createdAt)], ['Последнее движение', formatDateTime(appeal.lastStatusChangedAt)], ['Без движения', `${appeal.waitingHours} ч`], ['Сигналы контроля', appeal.alertTypes.length ? appeal.alertTypes.join(', ') : 'Просрочен контрольный срок']]} />
    <form className="admin-form" onSubmit={(event) => { event.preventDefault(); if (previewing) mutation.mutate(); else setPreviewing(true); }}><label className="select-field"><span>Рабочий статус</span><select value={status} onChange={(event) => { setStatus(event.target.value); setPreviewing(false); }}>{administrativeStatuses.map((value) => <option value={value} key={value}>{statusText(value)}</option>)}</select></label><label className="select-field"><span>Приоритет</span><select value={priority} onChange={(event) => { setPriority(event.target.value); setPreviewing(false); }}><option value="Urgent">Срочный</option><option value="Standard">Обычный</option><option value="Low">Низкий</option></select></label><label className="select-field"><span>Ответственный</span><select value={expertId} onChange={(event) => { setExpertId(event.target.value); setPreviewing(false); }}><option value="">Без исполнителя</option>{experts.map((item) => <option value={item.id} key={item.id}>{item.displayName}</option>)}</select></label><label className="text-field"><span>Причина изменения</span><textarea required minLength={10} maxLength={1000} value={reason} onChange={(event) => { setReason(event.target.value); setPreviewing(false); }} /><small>Причина и безопасные значения до и после попадут в журнал.</small></label>
      {previewing ? <section className="admin-diff" aria-labelledby="stuck-diff-heading"><h3 id="stuck-diff-heading">Проверьте изменение</h3><div><section><h4>Было</h4><SafeMetadata rows={beforeRows} /></section><span aria-hidden="true">→</span><section><h4>Станет</h4><SafeMetadata rows={afterRows} /></section></div></section> : null}{mutation.isError ? <ErrorText text={adminError(mutation.error)} /> : null}<div className="admin-actions"><md-filled-button type="submit" disabled={mutation.isPending || !changed || reason.trim().length < 10}>{previewing ? 'Подтвердить и сохранить' : 'Проверить изменение'}</md-filled-button>{previewing ? <md-outlined-button type="button" onClick={() => setPreviewing(false)}>Вернуться к форме</md-outlined-button> : null}</div>
    </form></article>;
}

function AuditView() {
  const { eventId } = useParams(); const [searchParams, setSearchParams] = useSearchParams(); const action = searchParams.get('action') ?? ''; const targetType = searchParams.get('targetType') ?? '';
  const audit = useQuery({ queryKey: ['admin-audit', action, targetType], queryFn: () => getAdminAudit({ action: action || undefined, targetType: targetType || undefined }) }); const detail = useQuery({ queryKey: ['admin-audit-detail', eventId], queryFn: () => getAdminAuditDetail(eventId!), enabled: Boolean(eventId) });
  const updateFilter = (key: string, value: string) => { const next = new URLSearchParams(searchParams); if (value) next.set(key, value); else next.delete(key); setSearchParams(next); };
  return <EntityLayout title="Журнал действий" description="Неизменяемая хронология административных изменений без обращений, сообщений, заметок, файлов, контактов и трек-номеров." hasDetail={Boolean(eventId)} focusKey={eventId} scrollKey={`admin-audit:${action}:${targetType}`} privacy
    filters={<div className="admin-filter-row admin-filter-row--inline"><label className="select-field"><span>Действие</span><select value={action} onChange={(event) => updateFilter('action', event.target.value)}><option value="">Все действия</option>{audit.data?.filters.actions.map((item) => <option value={item} key={item}>{actionText(item)}</option>)}</select></label><label className="select-field"><span>Объект</span><select value={targetType} onChange={(event) => updateFilter('targetType', event.target.value)}><option value="">Все объекты</option>{audit.data?.filters.targetTypes.map((item) => <option value={item} key={item}>{targetText(item)}</option>)}</select></label></div>}
    list={<EntityList pending={audit.isPending} error={audit.isError} empty={!audit.data?.items.length}>{audit.data?.items.map((item) => <EntityLink key={item.id} to={`/staff/admin/audit/${item.id}${searchParams.size ? `?${searchParams}` : ''}`} selected={eventId === item.id} title={actionText(item.action)} lines={[`${item.actorDisplayName} · ${targetText(item.targetType)}`, item.reason ? 'Причина указана' : 'Без отдельной причины']} trailing={formatDateTime(item.occurredAt)} />)}</EntityList>}
    detail={!eventId ? <SelectPrompt title="Выберите событие" copy="Откроются автор, время, объект, причина и безопасные значения до и после." /> : detail.isPending ? <LoadingText /> : detail.isError || !detail.data ? <NotFound entity="Событие" to="/staff/admin/audit" /> : <article className="admin-entity-detail audit-detail"><DetailBack to="/staff/admin/audit" label="К журналу" /><p className="eyebrow">Только для чтения</p><h2 data-detail-heading tabIndex={-1}>{actionText(detail.data.action)}</h2><SafeMetadata rows={[["Кто", detail.data.actorDisplayName], ['Когда', formatDateTime(detail.data.occurredAt)], ['Объект', targetText(detail.data.targetType)], ['Идентификатор объекта', detail.data.targetId], ...(detail.data.reason ? [['Причина', detail.data.reason] as [string, string]] : [])]} /><MetadataBlock title="Было" value={detail.data.before} /><MetadataBlock title="Стало" value={detail.data.after} /></article>} />;
}

function EntityLayout({ title, description, action, hasDetail, focusKey, scrollKey, filters, list, detail, privacy = false }: { title: string; description: string; action?: React.ReactNode; hasDetail: boolean; focusKey?: string; scrollKey: string; filters?: React.ReactNode; list: React.ReactNode; detail: React.ReactNode; privacy?: boolean }) {
  return <div className="admin-workspace"><PageHeader eyebrow="Кабинет администратора" title={title} description={description} action={action} />{privacy ? <PrivacyBoundary /> : null}{filters}<ResponsiveMasterDetail className="admin-master-detail" hasDetail={hasDetail} focusKey={focusKey} scrollKey={scrollKey}><section className="admin-master-detail__list" data-scroll-region aria-label={title}>{list}</section><section className="admin-master-detail__detail" aria-label="Карточка">{detail}</section></ResponsiveMasterDetail></div>;
}
function EntityList({ pending, error, empty, children }: { pending: boolean; error: boolean; empty: boolean; children: React.ReactNode }) { if (pending) return <LoadingText />; if (error) return <ErrorText text="Не удалось загрузить список." />; if (empty) return <EmptyText text="По выбранным условиям ничего нет." />; return <div className="admin-list">{children}</div>; }
function EntityLink({ to, selected, title, lines, trailing }: { to: string; selected: boolean; title: string; lines: string[]; trailing?: string }) { return <Link className={`admin-row admin-row--link${selected ? ' admin-row--selected' : ''}`} to={to}><span><strong>{title}</strong>{lines.map((line) => <span key={line}>{line}</span>)}</span>{trailing ? <span>{trailing}</span> : <span aria-hidden="true">Открыть</span>}</Link>; }
function ImpactConfirmation({ title, impact, pending, error, confirmLabel, onConfirm, onCancel, children }: { title: string; impact: string; pending: boolean; error?: string; confirmLabel: string; onConfirm: () => void; onCancel: () => void; children?: React.ReactNode }) { return <section className="admin-confirmation" role="dialog" aria-labelledby="admin-confirmation-heading"><h3 id="admin-confirmation-heading">{title}</h3><p>{impact}</p>{children}{error ? <ErrorText text={error} /> : null}<div className="admin-actions"><md-filled-button disabled={pending} onClick={onConfirm}>{confirmLabel}</md-filled-button><md-outlined-button disabled={pending} onClick={onCancel}>Отменить</md-outlined-button></div></section>; }
function DetailHeading({ children, focusWhen }: { children: React.ReactNode; focusWhen: string }) { const ref = useRef<HTMLHeadingElement>(null); useEffect(() => { requestAnimationFrame(() => ref.current?.focus({ preventScroll: true })); }, [focusWhen]); return <h2 data-detail-heading tabIndex={-1} ref={ref}>{children}</h2>; }
function SafeMetadata({ rows }: { rows: Array<[string, string]> }) { return <dl className="safe-metadata">{rows.map(([term, value]) => <div key={term}><dt>{term}</dt><dd>{value}</dd></div>)}</dl>; }
function MetadataBlock({ title, value }: { title: string; value: Record<string, unknown> | null }) { const entries = value ? Object.entries(value) : []; return <section className="audit-metadata"><h3>{title}</h3>{entries.length ? <SafeMetadata rows={entries.map(([key, field]) => [metadataLabel(key), metadataValue(key, field)])} /> : <p className="staff-copy">Нет данных — объект был создан на этом шаге.</p>}</section>; }
function DetailBack({ to, label }: { to: string; label: string }) {
  const location = useLocation();
  const current = new URLSearchParams(location.search);
  const preserved = new URLSearchParams();
  for (const key of ['role', 'state', 'status', 'priority', 'action', 'targetType']) {
    const value = current.get(key);
    if (value) preserved.set(key, value);
  }
  return <Link className="detail-back-link" to={`${to}${preserved.size ? `?${preserved}` : ''}`}>← {label}</Link>;
}
function SelectPrompt({ title, copy }: { title: string; copy: string }) { return <div className="operator-detail__empty"><h2>{title}</h2><p>{copy}</p></div>; }
function LoadingText() { return <p className="staff-copy" aria-live="polite">Загружаем данные…</p>; }
function ErrorText({ text }: { text: string }) { return <p className="form-error" role="alert">{text}</p>; }
function EmptyText({ text }: { text: string }) { return <p className="staff-copy">{text}</p>; }
function NotFound({ entity, to }: { entity: string; to: string }) { return <div className="operator-detail__empty"><h2 data-detail-heading tabIndex={-1}>{entity} не найдена</h2><p>Возможно, данные изменились после открытия ссылки.</p><DetailBack to={to} label="Вернуться к списку" /></div>; }
function RouteNotice() { const location = useLocation(); const notice = (location.state as { notice?: string } | null)?.notice; return notice ? <p className="form-success" role="status">{notice}</p> : null; }
function CreatedNextStep({ when, to, label }: { when: 'category' | 'group'; to: string; label: string }) { const location = useLocation(); const created = Boolean((location.state as { created?: boolean } | null)?.created); if (!created) return null; return <section className="admin-next-step"><strong>Следующий шаг</strong><p>{when === 'category' ? 'Теперь создайте профильную группу, которую затем можно связать с категорией.' : 'Теперь свяжите группу с категорией отдельным правилом.'}</p><Link to={to}>{label}</Link></section>; }
function SectionHeading({ id, title, copy }: { id?: string; title: string; copy: string }) { return <header className="admin-section__heading"><h2 id={id}>{title}</h2><p>{copy}</p></header>; }
function PrivacyBoundary({ compact = false }: { compact?: boolean }) { return <aside className={`admin-privacy-boundary${compact ? ' admin-privacy-boundary--compact' : ''}`}><strong>Граница доступа</strong><span>Тексты обращений, чат, заметки, вложения, контакты и трек-номера в административный API не входят.</span></aside>; }
function useConfiguration() { return useQuery({ queryKey: configurationKey, queryFn: getAdminConfiguration }); }

const administrativeStatuses = ['New', 'Triaged', 'Assigned', 'InProgress', 'NeedsClarification', 'Returned'];
function roleText(role: StaffRole) { return role === 'Operator' ? 'Оператор' : role === 'Expert' ? 'Эксперт' : 'Администратор'; }
function statusText(status: string) { const labels: Record<string, string> = { New: 'Новое', Triaged: 'Проверено оператором', Assigned: 'Распределено', InProgress: 'В работе', NeedsClarification: 'Нужно уточнение', Returned: 'Возвращено оператору' }; return labels[status] ?? status; }
function priorityText(priority: string) { return priority === 'Urgent' ? 'Срочный' : priority === 'Low' ? 'Низкий' : 'Обычный'; }
function actionText(action: string) { const labels: Record<string, string> = { CategoryCreated: 'Категория создана', CategoryUpdated: 'Категория изменена', CategoryDeactivated: 'Категория деактивирована', ExpertGroupCreated: 'Группа создана', ExpertGroupUpdated: 'Группа изменена', ExpertGroupDeactivated: 'Группа деактивирована', RoutingRuleCreated: 'Правило создано', RoutingRuleUpdated: 'Правило изменено', RoutingRuleDeactivated: 'Правило деактивировано', StaffAccountCreated: 'Сотрудник создан', StaffAccountUpdated: 'Сотрудник изменён', StaffAccountBlocked: 'Доступ сотрудника заблокирован', StaffAccountRestored: 'Доступ сотрудника восстановлен', StuckAppealIntervened: 'Зависшее обращение разблокировано', AnalyticsExportCreated: 'Выгрузка подготовлена', AnalyticsExportDownloaded: 'Выгрузка скачана' }; return labels[action] ?? action; }
function targetText(target: string) { const labels: Record<string, string> = { Category: 'Категория', ExpertGroup: 'Группа экспертов', RoutingRule: 'Правило маршрутизации', StaffUser: 'Учётная запись', Appeal: 'Обращение', AnalyticsExport: 'Обезличенная выгрузка' }; return labels[target] ?? target; }
function metadataLabel(key: string) { const labels: Record<string, string> = { code: 'Код', displayName: 'Название', sortOrder: 'Порядок', isActive: 'Активность', version: 'Версия', activeAppealLimit: 'Лимит на эксперта', memberIds: 'Состав группы', categoryId: 'Категория', expertGroupId: 'Группа экспертов', userName: 'Логин', role: 'Роль', isAvailable: 'Доступность эксперта', status: 'Статус', priority: 'Приоритет', assignedExpertId: 'Ответственный' }; return labels[key] ?? 'Служебное значение'; }
function metadataValue(key: string, value: unknown) { if (key === 'memberIds' && Array.isArray(value)) return `${value.length} участников`; if (typeof value === 'boolean') return value ? 'Да' : 'Нет'; if (value === null || value === undefined || value === '') return 'Не задано'; if (key === 'status') return statusText(String(value)); if (key === 'priority') return priorityText(String(value)); if (key === 'role') return roleText(String(value) as StaffRole); return String(value); }
function formatDateTime(value: string) { return new Intl.DateTimeFormat('ru-RU', { dateStyle: 'short', timeStyle: 'short' }).format(new Date(value)); }
function createSystemCode(prefix: string) { return `${prefix}-${crypto.randomUUID().slice(0, 8)}`; }
