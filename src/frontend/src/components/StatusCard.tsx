type StatusCardProps = {
  label: string;
  status: 'healthy' | 'degraded' | 'unhealthy' | 'checking';
  description?: string | null;
};

const statusLabels = {
  healthy: 'Работает',
  degraded: 'Нужна проверка',
  unhealthy: 'Недоступно',
  checking: 'Проверяем',
} as const;

export function StatusCard({ label, status, description }: StatusCardProps) {
  return (
    <article className={`status-card status-card--${status}`}>
      <div className="status-card__content">
        <p className="status-card__label">{label}</p>
        {description ? <p className="status-card__description">{description}</p> : null}
      </div>
      <p className="status-card__value">{statusLabels[status]}</p>
    </article>
  );
}
