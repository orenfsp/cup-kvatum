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
      { label: 'Очередь', to: '/staff/operator/queue' },
      { label: 'Срочная помощь', to: '/staff/operator/urgent' },
      { label: 'Запросы экспертов', to: '/staff/operator/requests' },
      { label: 'Возвраты', to: '/staff/operator/returns' },
      { label: 'Аналитика', to: '/staff/analytics', analytics: true },
    ],
  },
  Expert: {
    label: 'Кабинет эксперта',
    className: 'expert',
    defaultPath: '/staff/expert/inbox',
    navigation: [
      { label: 'Новые назначения', to: '/staff/expert/inbox', activePrefixes: ['/staff/expert/cases'] },
      { label: 'В работе', to: '/staff/expert/active' },
      { label: 'Ждут заявителя', to: '/staff/expert/waiting' },
      { label: 'Завершённые', to: '/staff/expert/completed' },
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
  const logoutMutation = useMutation({
    mutationFn: logoutStaff,
    onSuccess: () => {
      queryClient.clear();
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

  return (
    <StaffAppShell
      displayName={session.displayName}
      roleLabel={presentation.label}
      roleClassName={presentation.className}
      navigation={presentation.navigation}
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
            <Route path="operator/queue" element={<OperatorWorkspace view="queue" />} />
            <Route path="operator/queue/:appealId" element={<OperatorWorkspace view="queue" />} />
            <Route path="operator/urgent" element={<OperatorWorkspace view="crisis" />} />
            <Route path="operator/urgent/:appealId" element={<OperatorWorkspace view="crisis" />} />
            <Route path="operator/requests" element={<OperatorWorkspace view="requests" />} />
            <Route path="operator/requests/:appealId" element={<OperatorWorkspace view="requests" />} />
            <Route path="operator/returns" element={<OperatorWorkspace view="lifecycle" />} />
            <Route path="operator/returns/:appealId" element={<OperatorWorkspace view="lifecycle" />} />
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
            <Route path="admin" element={<AdminWorkspace view="configuration" />} />
            <Route path="admin/overview" element={<AdminWorkspace view="configuration" />} />
            <Route path="admin/categories" element={<AdminWorkspace view="configuration" />} />
            <Route path="admin/expert-groups" element={<AdminWorkspace view="configuration" />} />
            <Route path="admin/routing-rules" element={<AdminWorkspace view="configuration" />} />
            <Route path="admin/users" element={<AdminWorkspace view="users" />} />
            <Route path="admin/stuck" element={<AdminWorkspace view="stuck" />} />
            <Route path="admin/audit" element={<AdminWorkspace view="audit" />} />
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
