namespace LumenusErp.Data;

// ── English Studio: платформа преподавателя английского и его учеников ────────────────────────
// Роли Identity: "Teacher" и "Student". Ученик привязан к одному преподавателю (EnglishProfile.TeacherId)
// и видит только опубликованные уроки, тесты, задания и материалы этого преподавателя.

/// <summary>Профиль пользователя English Studio (ученика или преподавателя), 1:1 с AspNetUsers.</summary>
public class EnglishProfile
{
    public const int DefaultWeeklyGoal = 120;

    /// <summary>AspNetUsers.Id, он же первичный ключ.</summary>
    public string UserId { get; set; } = "";
    public ApplicationUser? User { get; set; }

    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string City { get; set; } = "";

    /// <summary>IANA-идентификатор часового пояса (Europe/Moscow): в нём показываются и вводятся даты расписания.</summary>
    public string TimeZone { get; set; } = "Europe/Moscow";
    public string Goal { get; set; } = "";
    public string Interests { get; set; } = "";
    public string Bio { get; set; } = "";

    /// <summary>Уровень ученика (A2, B1…) или специализация преподавателя.</summary>
    public string Level { get; set; } = "";

    /// <summary>Недельная цель ученика, минут.</summary>
    public int WeeklyGoalMinutes { get; set; } = DefaultWeeklyGoal;
    public bool Reminders { get; set; } = true;

    /// <summary>Преподаватель ученика; null — ученик ещё не привязан. У преподавателя всегда null.</summary>
    public string? TeacherId { get; set; }
    public ApplicationUser? Teacher { get; set; }

    // Поля ниже заполняет преподаватель ученика
    /// <summary>Личные заметки преподавателя об ученике (ученик их не видит).</summary>
    public string TeacherNotes { get; set; } = "";

    /// <summary>Комментарий преподавателя на странице прогресса ученика.</summary>
    public string TeacherComment { get; set; } = "";

    /// <summary>Оценка навыков преподавателем, 0–100.</summary>
    public int SkillSpeaking { get; set; }
    public int SkillListening { get; set; }
    public int SkillReading { get; set; }
    public int SkillWriting { get; set; }
    public int SkillGrammar { get; set; }
    public int SkillVocabulary { get; set; }

    // Права доступа ученика
    public bool AllowRecordings { get; set; } = true;
    public bool AllowDownloads { get; set; } = true;
    public bool AllowRetakes { get; set; } = true;

    public DateTime CreatedAt { get; set; }
    public DateTime? LastActivityAt { get; set; }

    public string DisplayName => string.IsNullOrWhiteSpace(FirstName + LastName) ? User?.Email ?? "" : $"{FirstName} {LastName}".Trim();
}

/// <summary>Учебная группа преподавателя.</summary>
public class EnglishGroup
{
    public Guid Id { get; set; }
    public string TeacherId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Level { get; set; } = "";

    /// <summary>Свободный текст: «вторник и четверг, 18:00».</summary>
    public string Schedule { get; set; } = "";
    public string Description { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public List<EnglishGroupMember> Members { get; set; } = new();
}

public class EnglishGroupMember
{
    public Guid GroupId { get; set; }
    public EnglishGroup? Group { get; set; }
    public string StudentId { get; set; } = "";
    public ApplicationUser? Student { get; set; }
}

/// <summary>Учебная программа: набор уроков (EnglishLesson.ProgramId).</summary>
public class EnglishProgram
{
    public Guid Id { get; set; }
    public string TeacherId { get; set; } = "";
    public string Title { get; set; } = "";
    public string Level { get; set; } = "";
    public string Description { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

/// <summary>Урок: описание, блоки (теория, слова, примеры…), запись занятия и фото доски.</summary>
public class EnglishLesson
{
    public static readonly string[] Colors = ["mint", "blue", "peach", "purple", "yellow"];

    public Guid Id { get; set; }
    public string TeacherId { get; set; } = "";
    public Guid? ProgramId { get; set; }
    public EnglishProgram? Program { get; set; }
    public string Title { get; set; } = "";
    public string Summary { get; set; } = "";

    /// <summary>Speaking, Grammar, Vocabulary…</summary>
    public string Topic { get; set; } = "";
    public string Level { get; set; } = "";
    public int DurationMinutes { get; set; } = 45;

    /// <summary>Цвет обложки: одно из <see cref="Colors"/>.</summary>
    public string Color { get; set; } = "mint";

    /// <summary>Ссылка на запись занятия (прямая на видеофайл или YouTube/облако).</summary>
    public string VideoUrl { get; set; } = "";
    public Guid? PhotoMediaId { get; set; }
    public MediaFile? Photo { get; set; }

    /// <summary>Порядок в библиотеке: номер юнита.</summary>
    public int Order { get; set; }
    public bool Published { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<EnglishLessonBlock> Blocks { get; set; } = new();
}

/// <summary>Блок урока. Type — подпись блока (Теория, Новые слова, Примеры…); в «Примерах» каждая строка озвучивается.</summary>
public class EnglishLessonBlock
{
    public static readonly string[] Types = ["Теория", "Новые слова", "Примеры", "Вопросы", "Домашнее задание", "Рефлексия"];
    public const string Examples = "Примеры", Words = "Новые слова";

    public Guid Id { get; set; }
    public Guid LessonId { get; set; }
    public EnglishLesson? Lesson { get; set; }
    public int Order { get; set; }
    public string Type { get; set; } = "";
    public string Text { get; set; } = "";
}

/// <summary>Прохождение урока учеником: процент, заметки, рефлексия.</summary>
public class EnglishLessonProgress
{
    public string StudentId { get; set; } = "";
    public Guid LessonId { get; set; }
    public EnglishLesson? Lesson { get; set; }
    public int Percent { get; set; }
    public string Notes { get; set; } = "";

    /// <summary>Ответ после урока: «Всё понятно», «Хочу повторить», «Нужна помощь».</summary>
    public string Reflection { get; set; } = "";
    public DateTime? CompletedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>Тест с вариантами ответов; проверяется автоматически.</summary>
public class EnglishTest
{
    public Guid Id { get; set; }
    public string TeacherId { get; set; } = "";
    public Guid? LessonId { get; set; }
    public EnglishLesson? Lesson { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";

    /// <summary>Обязательный тест выделяется у ученика.</summary>
    public bool Required { get; set; }
    public bool Published { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<EnglishQuestion> Questions { get; set; } = new();
}

public class EnglishQuestion
{
    public Guid Id { get; set; }
    public Guid TestId { get; set; }
    public EnglishTest? Test { get; set; }
    public int Order { get; set; }
    public string Text { get; set; } = "";
    public List<string> Options { get; set; } = new();

    /// <summary>Правильный ответ, совпадает с одним из Options.</summary>
    public string Correct { get; set; } = "";
    public string Explanation { get; set; } = "";
}

/// <summary>Попытка теста учеником. Answers — ответы по порядку вопросов на момент прохождения.</summary>
public class EnglishTestResult
{
    public Guid Id { get; set; }
    public Guid TestId { get; set; }
    public EnglishTest? Test { get; set; }
    public string StudentId { get; set; } = "";
    public ApplicationUser? Student { get; set; }
    public int Score { get; set; }
    public int CorrectCount { get; set; }
    public int Total { get; set; }
    public List<string> Answers { get; set; } = new();
    public DateTime CreatedAt { get; set; }
}

/// <summary>Домашнее задание. GroupId = null — всем ученикам преподавателя.</summary>
public class EnglishHomework
{
    public Guid Id { get; set; }
    public string TeacherId { get; set; } = "";
    public Guid? LessonId { get; set; }
    public EnglishLesson? Lesson { get; set; }
    public Guid? GroupId { get; set; }
    public EnglishGroup? Group { get; set; }
    public string Title { get; set; } = "";
    public string Instruction { get; set; } = "";
    public DateTime? DueAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<EnglishSubmission> Submissions { get; set; } = new();
}

/// <summary>Работа ученика по домашнему заданию и отзыв преподавателя.</summary>
public class EnglishSubmission
{
    public const string StatusSubmitted = "submitted";
    public const string StatusReviewed = "reviewed";

    public Guid Id { get; set; }
    public Guid HomeworkId { get; set; }
    public EnglishHomework? Homework { get; set; }
    public string StudentId { get; set; } = "";
    public ApplicationUser? Student { get; set; }
    public string Text { get; set; } = "";

    /// <summary>"submitted" или "reviewed".</summary>
    public string Status { get; set; } = StatusSubmitted;

    /// <summary>Оценка 0–100, выставляется при проверке.</summary>
    public int? Score { get; set; }
    public string Comment { get; set; } = "";
    public DateTime SubmittedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
}

/// <summary>Слово в личном словаре ученика.</summary>
public class EnglishWord
{
    public Guid Id { get; set; }
    public string StudentId { get; set; } = "";
    public string Word { get; set; } = "";
    public string Translation { get; set; } = "";
    public string Example { get; set; } = "";
    public bool Learned { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Событие календаря. Создатель — OwnerId. Событие преподавателя видят: адресат (StudentId), участники группы (GroupId)
/// или все его ученики, если адресата нет. Личное событие ученика видит только он.
/// </summary>
public class EnglishEvent
{
    public static readonly string[] Kinds = ["Занятие", "Разговорная практика", "Дедлайн", "Другое"];

    public Guid Id { get; set; }
    public string OwnerId { get; set; } = "";
    public string? StudentId { get; set; }
    public Guid? GroupId { get; set; }
    public EnglishGroup? Group { get; set; }
    public string Title { get; set; } = "";
    public string Kind { get; set; } = "";

    /// <summary>Начало, UTC.</summary>
    public DateTime StartsAt { get; set; }
    public int DurationMinutes { get; set; } = 60;

    /// <summary>Ссылка на звонок.</summary>
    public string Link { get; set; } = "";
    public string Notes { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

/// <summary>Сообщение в учебном чате ученик ↔ преподаватель.</summary>
public class EnglishMessage
{
    public Guid Id { get; set; }
    public string FromId { get; set; } = "";
    public string ToId { get; set; } = "";
    public string Text { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }
}

/// <summary>Запись активности ученика: из неё строятся тепловая карта, время с английским, XP и серия дней.</summary>
public class EnglishActivity
{
    public const string KindLesson = "lesson", KindTest = "test", KindHomework = "homework", KindWords = "words";

    public Guid Id { get; set; }
    public string StudentId { get; set; } = "";
    public string Kind { get; set; } = "";
    public int Minutes { get; set; }
    public int Xp { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>Материал библиотеки преподавателя.</summary>
public class EnglishMaterial
{
    public static readonly string[] Categories = ["Грамматика", "Лексика", "Видео", "Подкасты", "Чтение"];

    public Guid Id { get; set; }
    public string TeacherId { get; set; } = "";
    public string Title { get; set; } = "";
    public string Category { get; set; } = "";
    public string Level { get; set; } = "";
    public string Description { get; set; } = "";

    /// <summary>Текст памятки (markdown), доступен для чтения и скачивания.</summary>
    public string Content { get; set; } = "";
    public string Url { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public class EnglishFavorite
{
    public string StudentId { get; set; } = "";
    public Guid MaterialId { get; set; }
    public EnglishMaterial? Material { get; set; }
}

/// <summary>Заявка на знакомство с лендинга /english (видна всем преподавателям).</summary>
public class EnglishBooking
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Contact { get; set; } = "";
    public string Message { get; set; } = "";
    public bool Handled { get; set; }
    public DateTime CreatedAt { get; set; }
}
