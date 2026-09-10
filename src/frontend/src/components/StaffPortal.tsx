import '@material/web/button/filled-button.js';
import '@material/web/button/outlined-button.js';
import axios from 'axios';
import { useMutation, useQuery, useQueryClient, type UseMutationResult } from '@tanstack/react-query';
import { FormEvent, lazy, Suspense, useState } from 'react';
import { Navigate, Route, Routes, useLocation, useNavigate } from 'react-router-dom';
import {
  currentStaff,
  loginStaff,
  logoutStaff,
  type StaffRole,
  type StaffSession,
} from '../api/staffAuth';
import { AppHeader } from './PublicFrame';
import { ExpertWorkspace } from './ExpertWorkspace';
import { OperatorWorkspace } from './OperatorWorkspace';
import { StaffAppShell, type StaffNavigationItem } from './ux/AppShells';
import { useExpertWorkSummary } from './ux/useExpertWorkSummary';

const AdminWorkspace = lazy(() => import('./AdminWorkspace').then((module) => ({
  default: module.AdminWorkspace,
})));
const AnalyticsWorkspace = lazy(() => import('./AnalyticsWorkspace').then((module) => ({
  default: module.AnalyticsWorkspace,
})));

type RolePresentation = {
  label: string;
  className: string;
  defaultPath: string;
  navigation: StaffNavigationItem[];
};

export const rolePresentation: Record<StaffRole, RolePresentation> = {
  Operator: {
    label: 'Кабинет оператора',
    className: 'operator',
    defaultPath: '/staff/operator/queue',
    navigation: [
      { label: 'Линия', to: '/staff/operator/line', activePrefixes: ['/staff/operator/line'] },
      { label: 'Разбор обращений', to: '/staff/operator/queue' },
      { label: 'Срочная помощь', to: '/staff/operator/urgent' },
      { label: 'Запросы специалистов', to: '/staff/operator/requests' },
      { label: 'Возвраты', to: '/staff/operator/returns' },
      { label: 'Жалобы', to: '/staff/operator/complaints' },
      { label: 'Аналитика', to: '/staff/analytics', analytics: true },
    ],
  },
  Expert: {
    label: 'Кабинет эксперта',
    className: 'expert',
    defaultPath: '/staff/expert/inbox',
    navigation: [
      { label: 'Новые назначения', to: '/staff/expert/inbox', activeMatch: { pathPrefix: '/staff/expert/cases', searchParam: 'from', value: 'inbox' } },
      { label: 'В работе', to: '/staff/expert/active', activeMatch: { pathPrefix: '/staff/expert/cases', searchParam: 'from', value: 'active' } },
      { label: 'Ждут заявителя', to: '/staff/expert/waiting', activeMatch: { pathPrefix: '/staff/expert/cases', searchParam: 'from', value: 'waiting' } },
      { label: 'Завершённые', to: '/staff/expert/completed', activeMatch: { pathPrefix: '/staff/expert/cases', searchParam: 'from', value: 'completed' } },
      { label: 'Аналитика', to: '/staff/analytics', analytics: true },
    ],
  },
  Administrator: {
    label: 'Кабинет администратора',
    className: 'administrator',
    defaultPath: '/staff/admin',
    navigation: [
      { label: 'Обзор', to: '/staff/admin', activePrefixes: ['/staff/admin/overview'], end: true },
      { label: 'Категории', to: '/staff/admin/categories' },
      { label: 'Группы экспертов', to: '/staff/admin/expert-groups' },
      { label: 'Правила', to: '/staff/admin/routing-rules' },
      { label: 'Сотрудники', to: '/staff/admin/users' },
      { label: 'Зависшие', to: '/staff/admin/stuck' },
      { label: 'Журнал', to: '/staff/admin/audit' },
      { label: 'Аналитика', to: '/staff/analytics', analytics: true },
    ],
  },
};

export function StaffPortal() {
  const queryClient = useQueryClient();
  const sessionQuery = useQuery({
    queryKey: ['staff-session'],
    queryFn: currentStaff,
    retry: false,
    staleTime: 30_000,
    refetchInterval: 60_000,
  });

  const loginMutation = useMutation({
    mutationFn: ({ userName, password }: { userName: string; password: string }) =>
      loginStaff(userName, password),
    onSuccess: (session) => queryClient.setQueryData(['staff-session'], session),
  });

  if (sessionQuery.isPending) {
    return <StaffLoading />;
  }

  if (sessionQuery.isError) {
    return (
      <StaffFrame>
        <section className="staff-card notice" role="alert">
          Не удалось проверить сессию. Проверьте соединение и обновите страницу.
        </section>
      </StaffFrame>
    );
  }

  if (!sessionQuery.data) {
    return <LoginPanel mutation={loginMutation} />;
  }

  return <RoleDashboard session={sessionQuery.data} />;
}

function LoginPanel({
  mutation,
}: {
  mutation: UseMutationResult<StaffSession, Error, { userName: string; password: string }>;
}) {
  const [userName, setUserName] = useState('');
  const [password, setPassword] = useState('');

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    mutation.mutate({ userName, password });
  }

  const invalidCredentials =
    mutation.isError && axios.isAxiosError(mutation.error) && mutation.error.response?.status === 401;

  return (
    <StaffFrame>
      <section className="login-card" aria-labelledby="login-heading">
        <p className="eyebrow">Служебный контур</p>
        <h1 id="login-heading" className="staff-title">Вход для сотрудников</h1>
        <p className="staff-copy">
          Доступ разрешен только оператору, эксперту и администратору. Заявителю учетная запись не нужна.
        </p>
        <form className="login-form" onSubmit={submit}>
          <label>
            <span>Логин</span>
            <input
              autoComplete="username"
              name="username"
              required
              value={userName}
              onChange={(event) => setUserName(event.target.value)}
            />
          </label>
          <label>
            <span>Пароль</span>
            <input
              autoComplete="current-password"
              name="password"
              required
              type="password"
              value={password}
              onChange={(event) => setPassword(event.target.value)}
            />
          </label>
          {mutation.isError ? (
            <p className="form-error" role="alert">
              {invalidCredentials
                ? 'Не удалось войти. Проверьте логин и пароль.'
                : 'Сервис входа временно недоступен. Повторите попытку.'}
            </p>
          ) : null}
          <md-filled-button type="submit" disabled={mutation.isPending}>
            {mutation.isPending ? 'Входим…' : 'Войти'}
          </md-filled-button>
        </form>
        <div className="privacy-note">
          <strong>Безопасность сессии</strong>
          <span>HttpOnly cookie, CSRF-защита и серверная проверка роли.</span>
        </div>
      </section>
    </StaffFrame>
  );
}

function RoleDashboard({ session }: { session: StaffSession }) {
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const location = useLocation();
  const role = session.roles[0];
  const presentation = role ? rolePresentation[role] : undefined;
  const expertWorkSummary = useExpertWorkSummary(role === 'Expert');
  const logoutMutation = useMutation({
    mutationFn: logoutStaff,
    onSuccess: () => {
      queryClient.removeQueries({ predicate: (query) => query.queryKey[0] !== 'staff-session' });
      queryClient.setQueryData(['staff-session'], null);
      navigate('/staff', { replace: true });
    },
  });

  if (!role || !presentation) {
    return (
      <StaffFrame>
        <section className="staff-card notice" role="alert">У учетной записи нет доступной роли.</section>
      </StaffFrame>
    );
  }

  const routeNotice = (location.state as { routeNotice?: string } | null)?.routeNotice;
  const navigation = role === 'Expert' && expertWorkSummary.data
    ? presentation.navigation.map((item) => {
      const section = item.to.split('/').at(-1) as 'inbox' | 'active' | 'waiting' | 'completed';
      const count = section in expertWorkSummary.data ? expertWorkSummary.data[section] : undefined;
      return {
        ...item,
        count,
        attention: section === 'active' && count !== undefined && count > 0,
        meta: section === 'active' && count !== undefined && count > 0
          ? `${count} ${count === 1 ? 'требует' : 'требуют'} действия`
          : undefined,
      };
    })
    : presentation.navigation;

  return (
    <StaffAppShell
      displayName={session.displayName}
      roleLabel={presentation.label}
      roleClassName={presentation.className}
      navigation={navigation}
      logoutPending={logoutMutation.isPending}
      onLogout={() => logoutMutation.mutate()}
    >
      {routeNotice ? <p className="operator-notice" role="status">{routeNotice}</p> : null}
      <RoleRoutes role={role} defaultPath={presentation.defaultPath} />
    </StaffAppShell>
  );
}

function RoleRoutes({ role, defaultPath }: { role: StaffRole; defaultPath: string }) {
  const denied = (
    <Navigate
      to={defaultPath}
      replace
      state={{ routeNotice: 'Этот раздел недоступен для вашей роли. Открыт ваш рабочий кабинет.' }}
    />
  );

  return (
    <Suspense fallback={<p className="staff-copy">Открываем раздел…</p>}>
      <Routes>
        <Route path="analytics" element={<AnalyticsWorkspace />} />
        {role === 'Operator' ? (
          <>
            <Route path="operator/line" element={<OperatorWorkspace view="line" />} />
            <Route path="operator/line/:linePriority" element={<OperatorWorkspace view="line" />} />
            <Route path="operator/line/:linePriority/:appealId" element={<OperatorWorkspace view="line" />} />
            <Route path="operator/queue" element={<OperatorWorkspace view="queue" />} />
            <Route path="operator/queue/:appealId" element={<OperatorWorkspace view="queue" />} />
            <Route path="operator/urgent" element={<OperatorWorkspace view="crisis" />} />
            <Route path="operator/urgent/:appealId" element={<OperatorWorkspace view="crisis" />} />
            <Route path="operator/requests" element={<OperatorWorkspace view="requests" />} />
            <Route path="operator/requests/:appealId" element={<OperatorWorkspace view="requests" />} />
            <Route path="operator/returns" element={<OperatorWorkspace view="returns" />} />
            <Route path="operator/returns/:appealId" element={<OperatorWorkspace view="returns" />} />
            <Route path="operator/complaints" element={<OperatorWorkspace view="complaints" />} />
            <Route path="operator/complaints/:complaintId" element={<OperatorWorkspace view="complaints" />} />
          </>
        ) : null}
        {role === 'Expert' ? (
          <>
            <Route path="expert/inbox" element={<ExpertWorkspace section="inbox" />} />
            <Route path="expert/active" element={<ExpertWorkspace section="active" />} />
            <Route path="expert/waiting" element={<ExpertWorkspace section="waiting" />} />
            <Route path="expert/completed" element={<ExpertWorkspace section="completed" />} />
            <Route path="expert/cases/:appealId/:workspace" element={<ExpertWorkspace section="case" />} />
          </>
        ) : null}
        {role === 'Administrator' ? (
          <>
            <Route path="admin" element={<AdminWorkspace view="overview" />} />
            <Route path="admin/overview" element={<Navigate to="/staff/admin" replace />} />
            <Route path="admin/categories" element={<AdminWorkspace view="categories" />} />
            <Route path="admin/categories/:categoryId" element={<AdminWorkspace view="categories" />} />
            <Route path="admin/expert-groups" element={<AdminWorkspace view="groups" />} />
            <Route path="admin/expert-groups/:groupId" element={<AdminWorkspace view="groups" />} />
            <Route path="admin/routing-rules" element={<AdminWorkspace view="rules" />} />
            <Route path="admin/routing-rules/:ruleId" element={<AdminWorkspace view="rules" />} />
            <Route path="admin/users" element={<AdminWorkspace view="users" />} />
            <Route path="admin/users/:userId" element={<AdminWorkspace view="users" />} />
            <Route path="admin/stuck" element={<AdminWorkspace view="stuck" />} />
            <Route path="admin/stuck/:appealId" element={<AdminWorkspace view="stuck" />} />
            <Route path="admin/audit" element={<AdminWorkspace view="audit" />} />
            <Route path="admin/audit/:eventId" element={<AdminWorkspace view="audit" />} />
          </>
        ) : null}
        <Route path="" element={<Navigate to={defaultPath} replace />} />
        <Route path="*" element={denied} />
      </Routes>
    </Suspense>
  );
}

function StaffFrame({ children }: { children: React.ReactNode }) {
  return (
    <div className="staff-entry-shell">
      <AppHeader />
      <main className="staff-entry-main">{children}</main>
    </div>
  );
}

function StaffLoading() {
  return (
    <StaffFrame>
      <section className="login-card" aria-live="polite">Проверяем защищенную сессию…</section>
    </StaffFrame>
  );
}
