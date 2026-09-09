import { useEffect, useRef, type ReactNode } from 'react';
import { useLocation } from 'react-router-dom';

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
    requestAnimationFrame(() => headingRef.current?.focus());
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
  children,
  list,
  detail,
}: {
  className: string;
  hasDetail: boolean;
  children?: ReactNode;
  list?: ReactNode;
  detail?: ReactNode;
}) {
  return (
    <div className={`${className}${hasDetail ? ` ${className}--detail` : ''} responsive-master-detail`}>
      {children ?? <>{list}{detail}</>}
    </div>
  );
}
