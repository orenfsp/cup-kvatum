import '@material/web/button/filled-button.js';
import '@material/web/button/outlined-button.js';
import { useQuery } from '@tanstack/react-query';
import { createBrowserRouter, isRouteErrorResponse, Navigate, Outlet, RouterProvider, useNavigate, useRouteError } from 'react-router-dom';
import { getSystemStatus } from './api/system';
import { AppealIntake } from './components/AppealIntake';
import { AppealStatusPage } from './components/AppealStatusPage';
import { PublicFrame } from './components/PublicFrame';
import { StaffPortal } from './components/StaffPortal';
import { StatusCard } from './components/StatusCard';
import { useAppShellStore } from './store/appShell';

const componentLabels: Record<string, string> = {
  postgres: 'PostgreSQL',
  redis: 'Redis',
  kafka: 'Kafka',
};

export default function App() {
  return <RouterProvider router={appRouter} />;
}

const appRouter = createBrowserRouter([{
  path: '/',
  element: <Outlet />,
  errorElement: <RouteErrorPage />,
  children: [
    { index: true, element: <PublicHome /> },
    { path: 'appeal/new', element: <AppealIntake /> },
    { path: 'appeal/status', element: <Navigate to="/appeal" replace /> },
    { path: 'appeal/*', element: <AppealStatusPage /> },
    { path: 'staff/*', element: <StaffPortal /> },
    { path: 'system', element: <SystemStatusPage /> },
    { path: '*', element: <Navigate to="/" replace /> },
  ],
}]);

function RouteErrorPage() {
  const error = useRouteError();
  const notFound = isRouteErrorResponse(error) && error.status === 404;
  return (
    <main className="route-error-page">
      <section className="route-error-card" role="alert" aria-labelledby="route-error-heading">
        <p className="kicker">{notFound ? 'Страница не найдена' : 'Не удалось открыть раздел'}</p>
        <h1 id="route-error-heading">{notFound ? 'Проверьте адрес страницы' : 'Данные на экране не потеряны'}</h1>
        <p>{notFound ? 'Вернитесь в нужный раздел через главную страницу.' : 'Обновите раздел. Если ошибка повторится, вернитесь в рабочий кабинет и откройте задачу снова.'}</p>
        <div className="action-row">
          <md-filled-button onClick={() => window.location.reload()}>Обновить страницу</md-filled-button>
          <md-outlined-button onClick={() => window.location.assign('/staff')}>В рабочий кабинет</md-outlined-button>
        </div>
      </section>
    </main>
  );
}

function PublicHome() {
  const navigate = useNavigate();
  return (
    <PublicFrame current="home">
      <main className="home-main">
        <section className="home-hero" aria-labelledby="home-heading">
          <p className="kicker">Можно без имени и регистрации</p>
          <h1 id="home-heading">Когда трудно, не обязательно оставаться с этим одному</h1>
          <p className="home-hero__copy">
            Расскажите о травле, конфликте или давлении своими словами. Обращение увидит
            специалист, а вернуться к ответу можно по личному трек-номеру.
          </p>
          <div className="home-hero__actions" aria-label="Действия с обращением">
            <md-filled-button onClick={() => navigate('/appeal/new')}>
              Рассказать, что случилось
            </md-filled-button>
            <md-outlined-button onClick={() => navigate('/appeal')}>
              Вернуться к обращению
            </md-outlined-button>
          </div>
          <p className="home-hero__assurance">Не просим имя, телефон или электронную почту.</p>
        </section>

        <section className="home-process" aria-labelledby="process-heading">
          <div className="home-section-heading">
            <p className="kicker">Как это работает</p>
            <h2 id="process-heading">Три понятных шага</h2>
          </div>
          <div className="process-grid">
            <article className="process-step">
              <span className="process-step__number" aria-hidden="true">01</span>
              <h3>Расскажите</h3>
              <p>Свободным текстом или через короткую форму — как сейчас удобнее.</p>
            </article>
            <article className="process-step">
              <span className="process-step__number" aria-hidden="true">02</span>
              <h3>Сохраните номер</h3>
              <p>Он заменяет регистрацию и нужен, чтобы безопасно открыть обращение снова.</p>
            </article>
            <article className="process-step">
              <span className="process-step__number" aria-hidden="true">03</span>
              <h3>Получите ответ</h3>
              <p>Оператор направит обращение подходящему специалисту.</p>
            </article>
          </div>
        </section>

        <section className="home-trust" aria-labelledby="trust-heading">
          <div>
            <p className="kicker">Приватность по умолчанию</p>
            <h2 id="trust-heading">Только то, чем вы готовы поделиться</h2>
          </div>
          <p>
            Все уточняющие вопросы можно пропустить. Данные обращения не передаются во внешние
            сервисы, а в отчётах не используется его текст.
          </p>
        </section>
      </main>
    </PublicFrame>
  );
}

function SystemStatusPage() {
  const detailsVisible = useAppShellStore((state) => state.detailsVisible);
  const toggleDetails = useAppShellStore((state) => state.toggleDetails);
  const statusQuery = useQuery({
    queryKey: ['system-status'],
    queryFn: getSystemStatus,
    refetchInterval: 10_000,
    retry: 2,
  });

  const overallStatus = statusQuery.isPending
    ? 'checking'
    : statusQuery.data?.status ?? 'unhealthy';

  return (
    <PublicFrame current="system">
      <main className="utility-main">
        <section className="utility-panel" aria-labelledby="system-heading">
          <div className="utility-heading">
            <div>
              <p className="kicker">Служебная информация</p>
              <h1 id="system-heading">Состояние сервиса</h1>
            </div>
            <span className={`system-summary system-summary--${overallStatus}`}>
              {overallStatus === 'healthy' ? 'Все компоненты доступны' : 'Идет проверка компонентов'}
            </span>
          </div>

          {statusQuery.isError ? (
            <div className="notice" role="status">
              API пока не отвечает. Страница попробует подключиться снова автоматически.
            </div>
          ) : null}

          <div className="status-list">
            <StatusCard
              label="Отклик API"
              status={statusQuery.isPending ? 'checking' : statusQuery.isError ? 'unhealthy' : 'healthy'}
              description="Единая точка доступа приложения"
            />
            {detailsVisible
              ? Object.entries(statusQuery.data?.components ?? {}).map(([key, component]) => (
                  <StatusCard
                    key={key}
                    label={componentLabels[key] ?? key}
                    status={component.status}
                    description={component.description}
                  />
                ))
              : null}
          </div>
          <md-outlined-button onClick={toggleDetails}>
            {detailsVisible ? 'Скрыть компоненты' : 'Показать компоненты'}
          </md-outlined-button>
        </section>
      </main>
    </PublicFrame>
  );
}
