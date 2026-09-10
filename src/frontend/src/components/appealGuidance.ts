import type { ApplicantType } from '../api/publicAppeals';

export type AppealJourneyStep = {
  title: string;
  description: string;
  state: 'done' | 'current' | 'next';
};

export type ApplicantAppealGuidance = {
  title: string;
  description: string;
  next: string;
  actionLabel?: string;
  actionRoute?: '/appeal/dialog' | '/appeal/answer' | '/appeal/history' | '/appeal/new';
  actionRequired: boolean;
  journey: AppealJourneyStep[];
};

type GuidanceCopy = Omit<ApplicantAppealGuidance, 'journey'> & { stage: number };

const studentCopy: Record<string, GuidanceCopy> = {
  New: {
    title: 'Мы получили твоё обращение',
    description: 'Твой рассказ и файлы сохранены. Оператор прочитает их и поймёт, кто сможет помочь.',
    next: 'Сначала оператор проверит обращение. Потом его передадут подходящему специалисту.',
    actionRequired: false,
    stage: 0,
  },
  Triaged: {
    title: 'Оператор прочитал твоё обращение',
    description: 'Сейчас команда выбирает специалиста, который лучше всего сможет помочь в этой ситуации.',
    next: 'Специалист получит обращение и внимательно прочитает всё, что ты уже написал.',
    actionRequired: false,
    stage: 1,
  },
  Assigned: {
    title: 'К обращению подключился специалист',
    description: 'Он уже получил твой рассказ и начинает разбираться в ситуации.',
    next: 'Если специалисту понадобится уточнение, его вопрос появится в переписке. Мы отдельно покажем, что нужен твой ответ.',
    actionRequired: false,
    stage: 2,
  },
  InProgress: {
    title: 'Специалист разбирается в ситуации',
    description: 'Сейчас от тебя ничего не нужно. Всё, что ты уже отправил, остаётся у специалиста.',
    next: 'Специалист либо задаст вопрос в переписке, либо подготовит ответ с дальнейшими шагами.',
    actionRequired: false,
    stage: 2,
  },
  NeedsClarification: {
    title: 'Специалист ждёт твоего ответа',
    description: 'В переписке появился вопрос. Ответь, когда будешь готов, и только тем, чем хочешь поделиться.',
    next: 'После твоего ответа специалист продолжит работу. Обращение и вся переписка сохранятся.',
    actionLabel: 'Прочитать вопрос и ответить',
    actionRoute: '/appeal/dialog',
    actionRequired: true,
    stage: 2,
  },
  RecommendationReady: {
    title: 'Ответ специалиста готов',
    description: 'Прочитай ответ и выбери, достаточно ли помощи. Если нет, обращение можно вернуть на пересмотр.',
    next: 'После твоего выбора обращение либо завершится, либо команда продолжит искать решение.',
    actionLabel: 'Прочитать ответ специалиста',
    actionRoute: '/appeal/answer',
    actionRequired: true,
    stage: 3,
  },
  Returned: {
    title: 'Команда снова ищет, как помочь',
    description: 'Ты сообщил, что прошлый ответ не помог. Обращение не потерялось: оператор снова рассматривает его вместе со всей историей.',
    next: 'Оператор решит, как продолжить работу, и при необходимости подключит специалиста снова.',
    actionRequired: false,
    stage: 2,
  },
  Closed: {
    title: 'Работа по обращению завершена',
    description: 'Ответ, переписка и файлы сохранены. Их можно открыть в любой момент с этим же доступом.',
    next: 'Если эта же ситуация повторится, продолжи обращение в истории — новый трек-номер не понадобится.',
    actionLabel: 'Посмотреть историю и продолжения',
    actionRoute: '/appeal/history',
    actionRequired: false,
    stage: 4,
  },
  Rejected: {
    title: 'Сервис завершил работу с этим обращением',
    description: 'По этому обращению помощь через сервис продолжаться не будет. Если это ошибка или речь о другой ситуации, можно обратиться снова.',
    next: 'Новое обращение получит новый трек-номер и будет рассмотрено отдельно.',
    actionLabel: 'Создать новое обращение',
    actionRoute: '/appeal/new',
    actionRequired: false,
    stage: 0,
  },
};

const adultCopy: Record<string, GuidanceCopy> = {
  New: {
    title: 'Мы получили ваше обращение',
    description: 'Ваш текст и файлы сохранены. Оператор изучит их и определит подходящий маршрут помощи.',
    next: 'Сначала оператор проверит обращение, затем передаст его подходящему специалисту.',
    actionRequired: false,
    stage: 0,
  },
  Triaged: {
    title: 'Оператор изучил обращение',
    description: 'Сейчас команда выбирает специалиста, который лучше всего подходит для этой ситуации.',
    next: 'Специалист получит обращение и ознакомится со всей уже переданной информацией.',
    actionRequired: false,
    stage: 1,
  },
  Assigned: {
    title: 'К обращению подключён специалист',
    description: 'Специалист уже получил обращение и начинает работу с ним.',
    next: 'Если потребуется уточнение, вопрос появится в переписке. Сервис отдельно покажет, что нужен ваш ответ.',
    actionRequired: false,
    stage: 2,
  },
  InProgress: {
    title: 'Специалист работает с обращением',
    description: 'Сейчас от вас ничего не требуется. Вся переданная информация доступна специалисту.',
    next: 'Специалист либо задаст уточняющий вопрос, либо подготовит ответ с дальнейшими шагами.',
    actionRequired: false,
    stage: 2,
  },
  NeedsClarification: {
    title: 'Специалист ждёт вашего ответа',
    description: 'В переписке появился вопрос. Ответьте, когда будете готовы, и только той информацией, которой хотите поделиться.',
    next: 'После ответа специалист продолжит работу. Обращение и вся переписка сохранятся.',
    actionLabel: 'Прочитать вопрос и ответить',
    actionRoute: '/appeal/dialog',
    actionRequired: true,
    stage: 2,
  },
  RecommendationReady: {
    title: 'Ответ специалиста готов',
    description: 'Прочитайте ответ и укажите, достаточно ли помощи. Если нет, обращение можно вернуть на пересмотр.',
    next: 'После вашего выбора обращение либо завершится, либо команда продолжит искать решение.',
    actionLabel: 'Прочитать ответ специалиста',
    actionRoute: '/appeal/answer',
    actionRequired: true,
    stage: 3,
  },
  Returned: {
    title: 'Команда снова ищет решение',
    description: 'Вы сообщили, что прошлый ответ не помог. Обращение не потерялось: оператор снова рассматривает его вместе со всей историей.',
    next: 'Оператор определит следующий шаг и при необходимости снова подключит специалиста.',
    actionRequired: false,
    stage: 2,
  },
  Closed: {
    title: 'Работа по обращению завершена',
    description: 'Ответ, переписка и файлы сохранены. Их можно открыть в любой момент с этим же доступом.',
    next: 'Если эта же ситуация повторится, продолжите обращение в истории — новый трек-номер не понадобится.',
    actionLabel: 'Посмотреть историю и продолжения',
    actionRoute: '/appeal/history',
    actionRequired: false,
    stage: 4,
  },
  Rejected: {
    title: 'Сервис завершил работу с этим обращением',
    description: 'По этому обращению помощь через сервис продолжаться не будет. Если это ошибка или речь о другой ситуации, можно обратиться снова.',
    next: 'Новое обращение получит новый трек-номер и будет рассмотрено отдельно.',
    actionLabel: 'Создать новое обращение',
    actionRoute: '/appeal/new',
    actionRequired: false,
    stage: 0,
  },
};

export function getApplicantAppealGuidance(
  status: string,
  applicantType: ApplicantType,
): ApplicantAppealGuidance {
  const student = applicantType === 'Student';
  const selected = (student ? studentCopy : adultCopy)[status] ?? {
    title: student ? 'Обращение обновилось' : 'Обращение обновлено',
    description: student
      ? 'Вся информация сохранена. Открой обращение позже, чтобы увидеть следующий шаг.'
      : 'Вся информация сохранена. Откройте обращение позже, чтобы увидеть следующий шаг.',
    next: student
      ? 'Когда понадобится твоё действие, сервис покажет его здесь.'
      : 'Когда понадобится ваше действие, сервис покажет его здесь.',
    actionRequired: false,
    stage: 0,
  };

  return {
    title: selected.title,
    description: selected.description,
    next: selected.next,
    actionLabel: selected.actionLabel,
    actionRoute: selected.actionRoute,
    actionRequired: selected.actionRequired,
    journey: status === 'Rejected' ? [] : createJourney(selected.stage, student),
  };
}

function createJourney(currentStage: number, student: boolean): AppealJourneyStep[] {
  const steps: Array<[string, string]> = student
    ? [
        ['Обращение получено', 'Твой рассказ и файлы сохранены.'],
        ['Оператор выбирает маршрут', 'Оператор понимает, кто сможет помочь.'],
        ['Команда разбирается в ситуации', 'Специалист читает обращение и при необходимости задаёт вопросы.'],
        ['Ответ и следующий шаг', 'Ты читаешь ответ и говоришь, достаточно ли помощи.'],
      ]
    : [
        ['Обращение получено', 'Ваш текст и файлы сохранены.'],
        ['Оператор выбирает маршрут', 'Оператор определяет, кто сможет помочь.'],
        ['Команда разбирается в ситуации', 'Специалист изучает обращение и при необходимости задаёт вопросы.'],
        ['Ответ и следующий шаг', 'Вы читаете ответ и указываете, достаточно ли помощи.'],
      ];

  return steps.map(([title, description], index) => ({
    title,
    description,
    state: currentStage >= steps.length || index < currentStage
      ? 'done'
      : index === currentStage ? 'current' : 'next',
  }));
}
