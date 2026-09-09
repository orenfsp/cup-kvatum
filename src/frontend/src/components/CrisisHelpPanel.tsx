import type { CrisisSupportContact } from '../api/publicAppeals';

export function CrisisHelpPanel({
  contacts,
  contactValue,
  onContactChange,
  compact = false,
}: {
  contacts: CrisisSupportContact[];
  contactValue?: string;
  onContactChange?: (value: string) => void;
  compact?: boolean;
}) {
  return (
    <aside className={compact ? 'crisis-help crisis-help--compact' : 'crisis-help'} aria-labelledby="crisis-help-heading">
      <p className="eyebrow">Помощь прямо сейчас</p>
      <h2 id="crisis-help-heading">Если опасность рядом</h2>
      <p>
        Перейдите туда, где есть безопасный взрослый или другие люди. Если есть непосредственная угроза жизни,
        позвоните 112 сами или попросите человека рядом сделать это.
      </p>
      <a className="crisis-emergency-link" href="tel:112">Позвонить 112</a>
      <div className="crisis-contact-list" aria-label="Телефоны доверия">
        {contacts.map((contact) => (
          <a href={`tel:${contact.dialNumber}`} key={contact.dialNumber}>
            <strong>{contact.displayNumber}</strong>
            <span>{contact.displayName}. {contact.description}</span>
          </a>
        ))}
      </div>
      <p>
        Отклик не знает, кто вы и где вы, и без контакта не может направить физическую помощь.
        Обращение все равно можно отправить и продолжить анонимный диалог.
      </p>
      {onContactChange ? (
        <label className="text-field crisis-contact-field">
          <span>Телефон или другой способ связи — необязательно</span>
          <input
            autoComplete="off"
            maxLength={200}
            value={contactValue ?? ''}
            onChange={(event) => onContactChange(event.target.value)}
            placeholder="Можно оставить пустым"
          />
          <small>Контакт будет зашифрован. Его сможет открыть только оператор; каждое открытие записывается.</small>
        </label>
      ) : null}
    </aside>
  );
}
