using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LumenusErp.Data.Migrations.App
{
    /// <inheritdoc />
    public partial class EnglishStudio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EnglishActivities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<string>(type: "text", nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Minutes = table.Column<int>(type: "integer", nullable: false),
                    Xp = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnglishActivities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EnglishActivities_AspNetUsers_StudentId",
                        column: x => x.StudentId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EnglishBookings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Contact = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Handled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnglishBookings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EnglishGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TeacherId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Level = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Schedule = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnglishGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EnglishGroups_AspNetUsers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EnglishMaterials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TeacherId = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Level = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Content = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                    Url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnglishMaterials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EnglishMaterials_AspNetUsers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EnglishMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FromId = table.Column<string>(type: "text", nullable: false),
                    ToId = table.Column<string>(type: "text", nullable: false),
                    Text = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnglishMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EnglishMessages_AspNetUsers_FromId",
                        column: x => x.FromId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EnglishMessages_AspNetUsers_ToId",
                        column: x => x.ToId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EnglishProfiles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "text", nullable: false),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TimeZone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Goal = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Interests = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Bio = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Level = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    WeeklyGoalMinutes = table.Column<int>(type: "integer", nullable: false),
                    Reminders = table.Column<bool>(type: "boolean", nullable: false),
                    TeacherId = table.Column<string>(type: "text", nullable: true),
                    TeacherNotes = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    TeacherComment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    SkillSpeaking = table.Column<int>(type: "integer", nullable: false),
                    SkillListening = table.Column<int>(type: "integer", nullable: false),
                    SkillReading = table.Column<int>(type: "integer", nullable: false),
                    SkillWriting = table.Column<int>(type: "integer", nullable: false),
                    SkillGrammar = table.Column<int>(type: "integer", nullable: false),
                    SkillVocabulary = table.Column<int>(type: "integer", nullable: false),
                    AllowRecordings = table.Column<bool>(type: "boolean", nullable: false),
                    AllowDownloads = table.Column<bool>(type: "boolean", nullable: false),
                    AllowRetakes = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    LastActivityAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnglishProfiles", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_EnglishProfiles_AspNetUsers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_EnglishProfiles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EnglishPrograms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TeacherId = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Level = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnglishPrograms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EnglishPrograms_AspNetUsers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EnglishWords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<string>(type: "text", nullable: false),
                    Word = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Translation = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Example = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Learned = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnglishWords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EnglishWords_AspNetUsers_StudentId",
                        column: x => x.StudentId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EnglishEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<string>(type: "text", nullable: false),
                    StudentId = table.Column<string>(type: "text", nullable: true),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Kind = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    StartsAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    Link = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnglishEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EnglishEvents_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EnglishEvents_AspNetUsers_StudentId",
                        column: x => x.StudentId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EnglishEvents_EnglishGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "EnglishGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EnglishGroupMembers",
                columns: table => new
                {
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnglishGroupMembers", x => new { x.GroupId, x.StudentId });
                    table.ForeignKey(
                        name: "FK_EnglishGroupMembers_AspNetUsers_StudentId",
                        column: x => x.StudentId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EnglishGroupMembers_EnglishGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "EnglishGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EnglishFavorites",
                columns: table => new
                {
                    StudentId = table.Column<string>(type: "text", nullable: false),
                    MaterialId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnglishFavorites", x => new { x.StudentId, x.MaterialId });
                    table.ForeignKey(
                        name: "FK_EnglishFavorites_AspNetUsers_StudentId",
                        column: x => x.StudentId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EnglishFavorites_EnglishMaterials_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "EnglishMaterials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EnglishLessons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TeacherId = table.Column<string>(type: "text", nullable: false),
                    ProgramId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Summary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Topic = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Level = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    Color = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    VideoUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    PhotoMediaId = table.Column<Guid>(type: "uuid", nullable: true),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    Published = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnglishLessons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EnglishLessons_AspNetUsers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EnglishLessons_EnglishPrograms_ProgramId",
                        column: x => x.ProgramId,
                        principalTable: "EnglishPrograms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_EnglishLessons_MediaFiles_PhotoMediaId",
                        column: x => x.PhotoMediaId,
                        principalTable: "MediaFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "EnglishHomework",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TeacherId = table.Column<string>(type: "text", nullable: false),
                    LessonId = table.Column<Guid>(type: "uuid", nullable: true),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Instruction = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    DueAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnglishHomework", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EnglishHomework_AspNetUsers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EnglishHomework_EnglishGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "EnglishGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_EnglishHomework_EnglishLessons_LessonId",
                        column: x => x.LessonId,
                        principalTable: "EnglishLessons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "EnglishLessonBlocks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LessonId = table.Column<Guid>(type: "uuid", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Text = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnglishLessonBlocks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EnglishLessonBlocks_EnglishLessons_LessonId",
                        column: x => x.LessonId,
                        principalTable: "EnglishLessons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EnglishLessonProgress",
                columns: table => new
                {
                    StudentId = table.Column<string>(type: "text", nullable: false),
                    LessonId = table.Column<Guid>(type: "uuid", nullable: false),
                    Percent = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    Reflection = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnglishLessonProgress", x => new { x.StudentId, x.LessonId });
                    table.ForeignKey(
                        name: "FK_EnglishLessonProgress_AspNetUsers_StudentId",
                        column: x => x.StudentId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EnglishLessonProgress_EnglishLessons_LessonId",
                        column: x => x.LessonId,
                        principalTable: "EnglishLessons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EnglishTests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TeacherId = table.Column<string>(type: "text", nullable: false),
                    LessonId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Required = table.Column<bool>(type: "boolean", nullable: false),
                    Published = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnglishTests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EnglishTests_AspNetUsers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EnglishTests_EnglishLessons_LessonId",
                        column: x => x.LessonId,
                        principalTable: "EnglishLessons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "EnglishSubmissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HomeworkId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<string>(type: "text", nullable: false),
                    Text = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Score = table.Column<int>(type: "integer", nullable: true),
                    Comment = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnglishSubmissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EnglishSubmissions_AspNetUsers_StudentId",
                        column: x => x.StudentId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EnglishSubmissions_EnglishHomework_HomeworkId",
                        column: x => x.HomeworkId,
                        principalTable: "EnglishHomework",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EnglishQuestions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TestId = table.Column<Guid>(type: "uuid", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Options = table.Column<List<string>>(type: "text[]", nullable: false),
                    Correct = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Explanation = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnglishQuestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EnglishQuestions_EnglishTests_TestId",
                        column: x => x.TestId,
                        principalTable: "EnglishTests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EnglishTestResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TestId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<string>(type: "text", nullable: false),
                    Score = table.Column<int>(type: "integer", nullable: false),
                    CorrectCount = table.Column<int>(type: "integer", nullable: false),
                    Total = table.Column<int>(type: "integer", nullable: false),
                    Answers = table.Column<List<string>>(type: "text[]", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnglishTestResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EnglishTestResults_AspNetUsers_StudentId",
                        column: x => x.StudentId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EnglishTestResults_EnglishTests_TestId",
                        column: x => x.TestId,
                        principalTable: "EnglishTests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EnglishActivities_StudentId_CreatedAt",
                table: "EnglishActivities",
                columns: new[] { "StudentId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EnglishBookings_CreatedAt",
                table: "EnglishBookings",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_EnglishEvents_GroupId",
                table: "EnglishEvents",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_EnglishEvents_OwnerId_StartsAt",
                table: "EnglishEvents",
                columns: new[] { "OwnerId", "StartsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EnglishEvents_StudentId",
                table: "EnglishEvents",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_EnglishFavorites_MaterialId",
                table: "EnglishFavorites",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_EnglishGroupMembers_StudentId",
                table: "EnglishGroupMembers",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_EnglishGroups_TeacherId",
                table: "EnglishGroups",
                column: "TeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_EnglishHomework_GroupId",
                table: "EnglishHomework",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_EnglishHomework_LessonId",
                table: "EnglishHomework",
                column: "LessonId");

            migrationBuilder.CreateIndex(
                name: "IX_EnglishHomework_TeacherId_CreatedAt",
                table: "EnglishHomework",
                columns: new[] { "TeacherId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EnglishLessonBlocks_LessonId_Order",
                table: "EnglishLessonBlocks",
                columns: new[] { "LessonId", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_EnglishLessonProgress_LessonId",
                table: "EnglishLessonProgress",
                column: "LessonId");

            migrationBuilder.CreateIndex(
                name: "IX_EnglishLessons_PhotoMediaId",
                table: "EnglishLessons",
                column: "PhotoMediaId");

            migrationBuilder.CreateIndex(
                name: "IX_EnglishLessons_ProgramId",
                table: "EnglishLessons",
                column: "ProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_EnglishLessons_TeacherId_Order",
                table: "EnglishLessons",
                columns: new[] { "TeacherId", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_EnglishMaterials_TeacherId",
                table: "EnglishMaterials",
                column: "TeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_EnglishMessages_FromId_ToId_CreatedAt",
                table: "EnglishMessages",
                columns: new[] { "FromId", "ToId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EnglishMessages_ToId_ReadAt",
                table: "EnglishMessages",
                columns: new[] { "ToId", "ReadAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EnglishProfiles_TeacherId",
                table: "EnglishProfiles",
                column: "TeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_EnglishPrograms_TeacherId",
                table: "EnglishPrograms",
                column: "TeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_EnglishQuestions_TestId_Order",
                table: "EnglishQuestions",
                columns: new[] { "TestId", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_EnglishSubmissions_HomeworkId_Status",
                table: "EnglishSubmissions",
                columns: new[] { "HomeworkId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_EnglishSubmissions_StudentId_SubmittedAt",
                table: "EnglishSubmissions",
                columns: new[] { "StudentId", "SubmittedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EnglishTestResults_StudentId_CreatedAt",
                table: "EnglishTestResults",
                columns: new[] { "StudentId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EnglishTestResults_TestId",
                table: "EnglishTestResults",
                column: "TestId");

            migrationBuilder.CreateIndex(
                name: "IX_EnglishTests_LessonId",
                table: "EnglishTests",
                column: "LessonId");

            migrationBuilder.CreateIndex(
                name: "IX_EnglishTests_TeacherId",
                table: "EnglishTests",
                column: "TeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_EnglishWords_StudentId_CreatedAt",
                table: "EnglishWords",
                columns: new[] { "StudentId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EnglishActivities");

            migrationBuilder.DropTable(
                name: "EnglishBookings");

            migrationBuilder.DropTable(
                name: "EnglishEvents");

            migrationBuilder.DropTable(
                name: "EnglishFavorites");

            migrationBuilder.DropTable(
                name: "EnglishGroupMembers");

            migrationBuilder.DropTable(
                name: "EnglishLessonBlocks");

            migrationBuilder.DropTable(
                name: "EnglishLessonProgress");

            migrationBuilder.DropTable(
                name: "EnglishMessages");

            migrationBuilder.DropTable(
                name: "EnglishProfiles");

            migrationBuilder.DropTable(
                name: "EnglishQuestions");

            migrationBuilder.DropTable(
                name: "EnglishSubmissions");

            migrationBuilder.DropTable(
                name: "EnglishTestResults");

            migrationBuilder.DropTable(
                name: "EnglishWords");

            migrationBuilder.DropTable(
                name: "EnglishMaterials");

            migrationBuilder.DropTable(
                name: "EnglishHomework");

            migrationBuilder.DropTable(
                name: "EnglishTests");

            migrationBuilder.DropTable(
                name: "EnglishGroups");

            migrationBuilder.DropTable(
                name: "EnglishLessons");

            migrationBuilder.DropTable(
                name: "EnglishPrograms");
        }
    }
}
