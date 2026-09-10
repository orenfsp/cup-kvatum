import type { ApplicantType, CrisisSupportContact } from '../api/publicAppeals';

export function CrisisHelpPanel({
  contacts,
  contactValue,
  onContactChange,
  applicantType,
  compact = false,
}: {
  contacts: CrisisSupportContact[];
  contactValue?: string;
  onContactChange?: (value: string) => void;
  applicantType?: ApplicantType;
  compact?: boolean;
}) {
  const isStudent = applicantType === 'Student';

  return (
    <aside className={compact ? 'crisis-help crisis-help--compact' : 'crisis-help'} aria-labelledby="crisis-help-heading">
      <p className="eyebrow">Помощь прямо сейчас</p>
      <h2 id="crisis-help-heading">Если опасность рядом</h2>
      <p>
        {isStudent
          ? 'Перейди туда, где есть взрослый, которому ты доверяешь, или другие люди. Если жизни угрожает опасность, позвони 112 сам или попроси человека рядом сделать это.'
          : 'Перейдите туда, где есть человек, которому вы доверяете, или другие люди. Если жизни угрожает опасность, позвоните 112 сами или попросите человека рядом сделать это.'}
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
        {isStudent
          ? 'Отклик не знает, кто ты и где находишься, и без контакта не может направить помощь на место. Обращение всё равно можно отправить и продолжить анонимный диалог.'
          : 'Отклик не знает, кто вы и где находитесь, и без контакта не может направить помощь на место. Обращение всё равно можно отправить и продолжить анонимный диалог.'}
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
