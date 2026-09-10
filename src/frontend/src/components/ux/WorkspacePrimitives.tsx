import { useEffect, useRef, type ReactNode } from 'react';
import { useBlocker, useLocation } from 'react-router-dom';

const scrollPositions = new Map<string, number>();
const discardMessage = 'Введённые данные ещё не сохранены. Выйти без сохранения?';

export function ActionReceipt({ message, title = 'Действие выполнено' }: { message: string; title?: string }) {
  const receiptRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (message) requestAnimationFrame(() => receiptRef.current?.focus({ preventScroll: true }));
  }, [message]);

  if (!message) return null;
  return (
    <div className="action-receipt" role="status" aria-live="polite" ref={receiptRef} tabIndex={-1}>
      <strong>{title}</strong>
      <span>{message}</span>
    </div>
  );
}

export function PageHeader({
  eyebrow,
  title,
  description,
  action,
  id,
}: {
  eyebrow?: string;
  title: string;
  description?: string;
  action?: ReactNode;
  id?: string;
}) {
  const headingRef = useRef<HTMLHeadingElement>(null);
  const location = useLocation();

  useEffect(() => {
    requestAnimationFrame(() => headingRef.current?.focus({ preventScroll: true }));
  }, [location.pathname]);

  return (
    <header className="page-header">
      <div>
        {eyebrow ? <p className="eyebrow">{eyebrow}</p> : null}
        <h1 id={id} className="staff-title" ref={headingRef} tabIndex={-1}>{title}</h1>
        {description ? <p className="staff-copy">{description}</p> : null}
      </div>
      {action ? <div className="page-header__action">{action}</div> : null}
    </header>
  );
}

export function ResponsiveMasterDetail({
  className,
  hasDetail,
  focusKey,
  scrollKey,
  children,
  list,
  detail,
}: {
  className: string;
  hasDetail: boolean;
  focusKey?: string;
  scrollKey?: string;
  children?: ReactNode;
  list?: ReactNode;
  detail?: ReactNode;
}) {
  const rootRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!hasDetail || !focusKey) return;
    const root = rootRef.current;
    if (!root) return;

    const focusHeading = () => {
      const heading = root.querySelector<HTMLElement>('[data-detail-heading]');
      if (!heading) return false;
      requestAnimationFrame(() => {
        window.scrollTo({ top: 0, left: 0, behavior: 'auto' });
        heading.focus({ preventScroll: true });
      });
      return true;
    };

    if (focusHeading()) return;
    const observer = new MutationObserver(() => {
      if (focusHeading()) observer.disconnect();
    });
    observer.observe(root, { childList: true, subtree: true });
    return () => observer.disconnect();
  }, [focusKey, hasDetail]);

  useEffect(() => {
    if (!scrollKey) return;
    const root = rootRef.current;
    if (!root) return;

    const restore = () => {
      const region = root.querySelector<HTMLElement>('[data-scroll-region]');
      const saved = scrollPositions.get(scrollKey);
      if (region && saved !== undefined && region.scrollTop !== saved) region.scrollTop = saved;
    };
    requestAnimationFrame(restore);
    const observer = new MutationObserver(restore);
    observer.observe(root, { childList: true, subtree: true });
    return () => observer.disconnect();
  }, [scrollKey]);

  return (
    <div
      className={`${className}${hasDetail ? ` ${className}--detail` : ''} responsive-master-detail`}
      ref={rootRef}
      onScrollCapture={(event) => {
        if (!scrollKey) return;
        const target = event.target as HTMLElement;
        if (target.matches('[data-scroll-region]')) scrollPositions.set(scrollKey, target.scrollTop);
      }}
    >
      {children ?? <>{list}{detail}</>}
    </div>
  );
}

export function UnsavedChangesGuard({ when }: { when: boolean }) {
  const blocker = useBlocker(when);

  useEffect(() => {
    if (blocker.state !== 'blocked') return;
    if (window.confirm(discardMessage)) blocker.proceed();
    else blocker.reset();
  }, [blocker]);

  useEffect(() => {
    if (!when) return;
    const beforeUnload = (event: BeforeUnloadEvent) => {
      event.preventDefault();
      event.returnValue = '';
    };
    window.addEventListener('beforeunload', beforeUnload);
    return () => {
      window.removeEventListener('beforeunload', beforeUnload);
    };
  }, [when]);
  return null;
}
