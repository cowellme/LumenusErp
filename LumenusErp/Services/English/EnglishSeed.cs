using LumenusErp.Data;
using Microsoft.EntityFrameworkCore;

namespace LumenusErp.Services.English;

/// <summary>
/// Стартовое наполнение кабинета нового преподавателя: программа A2 → B1 из 6 уроков, тест по Past Simple,
/// домашнее задание и материалы библиотеки (тексты из прототипа English Studio). Всё можно править и удалять.
/// Выполняется один раз — только если у преподавателя ещё нет ни одного урока.
/// </summary>
public static class EnglishSeed
{
    private record LessonSeed(string Title, string Summary, string Topic, string Color, string Rule, string Theory, string Formula, string[] Examples, string Words);

    private static readonly LessonSeed[] Lessons =
    [
        new("Nice to meet you", "Знакомства и small talk", "Speaking", "peach",
            "Знакомимся", "Начни с приветствия: Hello! Nice to meet you. What do you do?", "Read → Listen → Practise",
            ["It’s a pleasure to meet you.", "Could you tell me more?", "I would love to try something new."],
            "pleasure — удовольствие\nintroduce — представлять\nsmall talk — светская беседа"),
        new("My everyday life", "Привычки и Present Simple", "Grammar", "blue",
            "Present Simple", "Используем для привычек: I work every day. She works every day.", "Subject + V / V-s",
            ["I usually get up at seven.", "She works from home.", "Do you like your routine?"],
            "routine — распорядок\nusually — обычно\ncommute — дорога на работу"),
        new("Stories worth telling", "Истории и Past Simple", "Grammar", "mint",
            "Past Simple", "Используем для действий, завершённых в прошлом. Слова-подсказки: yesterday, last week, two days ago.", "Subject + verb-ed / V₂",
            ["I visited London last summer.", "I didn’t visit London last summer.", "Did you visit London last summer?"],
            "journey — путешествие\nexperience — опыт\ndiscover — открывать\nmemorable — запоминающийся\nget away — уехать отдохнуть"),
        new("A world of possibilities", "Планы и путешествия", "Speaking", "purple",
            "Future plans", "Говорим о планах: I am going to travel next month.", "Subject + am/is/are going to + V",
            ["I’m going to visit my friends.", "We are going to book a hotel.", "Are you going to travel this summer?"],
            "book — бронировать\nsightseeing — осмотр достопримечательностей\nabroad — за границей"),
        new("Have you ever…?", "Опыт и Present Perfect", "Grammar", "yellow",
            "Present Perfect", "Говорим об опыте без указания завершённого времени: I have visited London.", "Subject + have / has + V₃",
            ["I have been to Italy.", "Have you ever tried surfing?", "She has never seen snow."],
            "ever — когда-либо\nnever — никогда\nalready — уже"),
        new("Let’s talk about work", "Работа и деловое общение", "Vocabulary", "blue",
            "At work", "Вежливые просьбы помогают в работе: Could you send me the report?", "Could you + V…?",
            ["Could you send me the report?", "I’d like to schedule a meeting.", "Let me check and get back to you."],
            "deadline — срок\nschedule — назначать, расписание\ncolleague — коллега"),
    ];

    private static readonly (string Text, string[] Options, string Correct, string Why)[] PastSimpleQuestions =
    [
        ("Yesterday I ___ a fascinating place.", ["visit", "visited", "have visit", "visiting"], "visited",
            "Yesterday указывает на завершённое время. В Past Simple: visit → visited."),
        ("Choose the correct sentence.", ["Did you went to London?", "Did you go to London?", "Do you went to London?", "Did you goes to London?"], "Did you go to London?",
            "После did используем начальную форму глагола: go."),
        ("She ___ at home last Sunday.", ["were", "is", "was", "be"], "was",
            "В прошедшем времени с she используется was."),
        ("We didn’t ___ the train.", ["missed", "miss", "missing", "misses"], "miss",
            "После didn’t нужна начальная форма глагола."),
        ("Как перевести «запоминающееся путешествие»?", ["a memorable journey", "an ordinary day", "a short answer", "a difficult question"], "a memorable journey",
            "Memorable — запоминающийся; journey — путешествие."),
    ];

    private static readonly (string Title, string Category, string Description, string Content, string Url)[] Materials =
    [
        ("Past Simple — памятка", "Грамматика", "Схема с примерами и неправильными глаголами.",
            "Past Simple: I visited London. Did you visit London? I didn’t visit London.\n\nНеправильные глаголы: go → went, see → saw, have → had, make → made.", ""),
        ("A little English every day", "Подкасты", "Короткие аудиоуроки для ежедневной практики.", "", "https://learnenglish.britishcouncil.org/general-english/audio-zone"),
        ("Stories in English", "Видео", "Обучающие видео British Council.", "", "https://learnenglish.britishcouncil.org/general-english/video-zone"),
        ("Travel phrasebook", "Лексика", "Выражения для следующего путешествия.",
            "Could you help me? I’d like to book a room. How do I get to the station?", ""),
    ];

    public static async Task EnsureSeededForTeacherAsync(ApplicationDbContext db, string teacherId, CancellationToken ct = default)
    {
        if (await db.EnglishLessons.AnyAsync(l => l.TeacherId == teacherId, ct)) return;

        var now = DateTime.UtcNow;
        var program = new EnglishProgram
        {
            Id = Guid.NewGuid(),
            TeacherId = teacherId,
            Title = "General English · A2 → B1",
            Level = "A2 → B1",
            Description = "Последовательный маршрут от цели к уверенной практике: 6 уроков в индивидуальном темпе.",
            CreatedAt = now,
        };
        db.EnglishPrograms.Add(program);

        Guid? pastSimpleLesson = null;
        for (var i = 0; i < Lessons.Length; i++)
        {
            var s = Lessons[i];
            var lesson = new EnglishLesson
            {
                Id = Guid.NewGuid(),
                TeacherId = teacherId,
                ProgramId = program.Id,
                Title = s.Title,
                Summary = s.Summary,
                Topic = s.Topic,
                Level = "A2 → B1",
                DurationMinutes = 45,
                Color = s.Color,
                Order = i + 1,
                Published = true,
                CreatedAt = now.AddSeconds(i),
                UpdatedAt = now,
            };
            lesson.Blocks =
            [
                new() { Id = Guid.NewGuid(), Order = 0, Type = "Теория", Text = $"{s.Rule}\n\n{s.Theory}\n\nФормула: {s.Formula}" },
                new() { Id = Guid.NewGuid(), Order = 1, Type = EnglishLessonBlock.Words, Text = s.Words },
                new() { Id = Guid.NewGuid(), Order = 2, Type = EnglishLessonBlock.Examples, Text = string.Join("\n", s.Examples) },
                new() { Id = Guid.NewGuid(), Order = 3, Type = "Вопросы", Text = "Составь три предложения о себе. Сохрани их в заметках и обсуди с преподавателем." },
            ];
            db.EnglishLessons.Add(lesson);
            if (s.Rule == "Past Simple") pastSimpleLesson = lesson.Id;
        }

        var test = new EnglishTest
        {
            Id = Guid.NewGuid(),
            TeacherId = teacherId,
            LessonId = pastSimpleLesson,
            Title = "Past Simple: practice",
            Description = "Пять коротких заданий по Past Simple.",
            Required = true,
            Published = true,
            CreatedAt = now,
            UpdatedAt = now,
        };
        test.Questions = PastSimpleQuestions.Select((q, i) => new EnglishQuestion
        {
            Id = Guid.NewGuid(),
            Order = i,
            Text = q.Text,
            Options = q.Options.ToList(),
            Correct = q.Correct,
            Explanation = q.Why,
        }).ToList();
        db.EnglishTests.Add(test);

        db.EnglishHomework.Add(new EnglishHomework
        {
            Id = Guid.NewGuid(),
            TeacherId = teacherId,
            LessonId = pastSimpleLesson,
            Title = "A weekend to remember",
            Instruction = "Напиши 80–120 слов о запоминающихся выходных. Используй Past Simple и минимум 5 слов из урока.",
            DueAt = now.Date.AddDays(7).AddHours(18),
            CreatedAt = now,
        });

        foreach (var m in Materials)
        {
            db.EnglishMaterials.Add(new EnglishMaterial
            {
                Id = Guid.NewGuid(),
                TeacherId = teacherId,
                Title = m.Title,
                Category = m.Category,
                Level = "A2 — B1",
                Description = m.Description,
                Content = m.Content,
                Url = m.Url,
                CreatedAt = now,
            });
        }

        await db.SaveChangesAsync(ct);
    }
}
