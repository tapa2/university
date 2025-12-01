using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Npgsql;

namespace UniversityApp
{
    // ===========================
    // DTO-моделі
    // ===========================

    public class StudentDto
    {
        public long Id { get; set; }
        public string StudentCode { get; set; } = "";
        public string FullName { get; set; } = "";
        public string GroupCode { get; set; } = "";
    }

    public class StudentEnrollmentDto
    {
        public long EnrollmentId { get; set; }
        public string StudentCode { get; set; } = "";
        public string StudentName { get; set; } = "";
        public string AcademicYear { get; set; } = "";
        public int Semester { get; set; }
        public string CourseName { get; set; } = "";
        public long TeacherId { get; set; }
    }

    // ===========================
    // Інтерфейси
    // ===========================

    public interface IStudentRepository
    {
        Task<List<StudentDto>> GetActiveAsync();
        Task<List<StudentEnrollmentDto>> GetEnrollmentsAsync(long studentId);
        Task EnrollToCourseAsync(long studentId, long courseInstanceId);
        Task DeleteStudentAsync(long studentId);
        Task<decimal?> GetGpaAsync(long studentId);
    }

    public interface IGradeRepository
    {
        Task AddGradeAsync(long enrollmentId, decimal gradeValue, string gradeType);
    }

    public interface IUnitOfWork : IAsyncDisposable
    {
        IStudentRepository Students { get; }
        IGradeRepository Grades { get; }

        Task BeginAsync(long currentUserId);
        Task CommitAsync();
        Task RollbackAsync();
    }

    // ===========================
    // Unit of Work
    // ===========================

    public class UnitOfWork : IUnitOfWork
    {
        private readonly NpgsqlConnection _connection;
        private NpgsqlTransaction? _transaction;

        public IStudentRepository Students { get; }
        public IGradeRepository Grades { get; }


        private const string ConnectionString =
            "Host=localhost;Port=5432;Database=uni;Username=postgres;Password=password";

        public UnitOfWork()
        {
            _connection = new NpgsqlConnection(ConnectionString);
            _connection.Open();

            Students = new StudentRepository(_connection, () => _transaction);
            Grades = new GradeRepository(_connection, () => _transaction);
        }

        public async Task BeginAsync(long currentUserId)
        {
            _transaction = await _connection.BeginTransactionAsync();

            // встановлюємо поточного користувача для тригерів / процедур
            using var cmd = new NpgsqlCommand("CALL set_current_user(@uid);", _connection, _transaction);
            cmd.Parameters.AddWithValue("uid", currentUserId);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task CommitAsync()
        {
            if (_transaction != null)
            {
                await _transaction.CommitAsync();
                await _transaction.DisposeAsync();
                _transaction = null;
            }
        }

        public async Task RollbackAsync()
        {
            if (_transaction != null)
            {
                await _transaction.RollbackAsync();
                await _transaction.DisposeAsync();
                _transaction = null;
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_transaction != null)
                await _transaction.DisposeAsync();

            await _connection.DisposeAsync();
        }

        internal NpgsqlTransaction? GetTransaction() => _transaction;
    }

    // ===========================
    // Репозиторії (без Dapper)
    // ===========================

    public class StudentRepository : IStudentRepository
    {
        private readonly NpgsqlConnection _connection;
        private readonly Func<NpgsqlTransaction?> _tx;

        public StudentRepository(NpgsqlConnection connection, Func<NpgsqlTransaction?> tx)
        {
            _connection = connection;
            _tx = tx;
        }

        // Читання через VIEW v_students_active
        public async Task<List<StudentDto>> GetActiveAsync()
        {
            var result = new List<StudentDto>();

            const string sql = @"
                SELECT id, student_code, full_name, group_code
                FROM v_students_active
                ORDER BY full_name;";

            using var cmd = new NpgsqlCommand(sql, _connection, _tx());
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(new StudentDto
                {
                    Id = reader.GetInt64(reader.GetOrdinal("id")),
                    StudentCode = reader.GetString(reader.GetOrdinal("student_code")),
                    FullName = reader.GetString(reader.GetOrdinal("full_name")),
                    GroupCode = reader.GetString(reader.GetOrdinal("group_code"))
                });
            }

            return result;
        }

        // Читання через VIEW v_student_enrollments
        public async Task<List<StudentEnrollmentDto>> GetEnrollmentsAsync(long studentId)
        {
            var result = new List<StudentEnrollmentDto>();

            const string sql = @"
                SELECT 
                    id              AS enrollment_id,
                    student_code,
                    student_name,
                    academic_year,
                    semester,
                    course_name,
                    teacher_id
                FROM v_student_enrollments
                WHERE student_code = (
                    SELECT student_code FROM students WHERE id = @studentId
                );";

            using var cmd = new NpgsqlCommand(sql, _connection, _tx());
            cmd.Parameters.AddWithValue("studentId", studentId);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(new StudentEnrollmentDto
                {
                    EnrollmentId = reader.GetInt64(reader.GetOrdinal("enrollment_id")),
                    StudentCode = reader.GetString(reader.GetOrdinal("student_code")),
                    StudentName = reader.GetString(reader.GetOrdinal("student_name")),
                    AcademicYear = reader.GetString(reader.GetOrdinal("academic_year")),
                    Semester = reader.GetInt32(reader.GetOrdinal("semester")),
                    CourseName = reader.GetString(reader.GetOrdinal("course_name")),
                    TeacherId = reader.GetInt64(reader.GetOrdinal("teacher_id"))
                });
            }

            return result;
        }

        // Запис на курс – тільки через збережену процедуру enroll_student
        public async Task EnrollToCourseAsync(long studentId, long courseInstanceId)
        {
            using var cmd = new NpgsqlCommand("CALL enroll_student(@studentId, @courseInstanceId);",
                                              _connection, _tx());
            cmd.Parameters.AddWithValue("studentId", studentId);
            cmd.Parameters.AddWithValue("courseInstanceId", courseInstanceId);

            await cmd.ExecuteNonQueryAsync();
        }

        // Логічне видалення – тільки через процедуру delete_student
        public async Task DeleteStudentAsync(long studentId)
        {
            using var cmd = new NpgsqlCommand("CALL delete_student(@studentId);",
                                              _connection, _tx());
            cmd.Parameters.AddWithValue("studentId", studentId);

            await cmd.ExecuteNonQueryAsync();
        }

        // Обчислення середнього балу – через функцію calculate_gpa
        public async Task<decimal?> GetGpaAsync(long studentId)
        {
            using var cmd = new NpgsqlCommand("SELECT calculate_gpa(@studentId);", _connection, _tx());
            cmd.Parameters.AddWithValue("studentId", studentId);

            var result = await cmd.ExecuteScalarAsync();
            if (result == null || result is DBNull) return null;
            return (decimal)result;
        }
    }

    public class GradeRepository : IGradeRepository
    {
        private readonly NpgsqlConnection _connection;
        private readonly Func<NpgsqlTransaction?> _tx;

        public GradeRepository(NpgsqlConnection connection, Func<NpgsqlTransaction?> tx)
        {
            _connection = connection;
            _tx = tx;
        }

        // Додавання оцінки – тільки через процедуру add_grade
        public async Task AddGradeAsync(long enrollmentId, decimal gradeValue, string gradeType)
        {
            using var cmd = new NpgsqlCommand("CALL add_grade(@enrollmentId, @gradeValue, @gradeType);",
                                              _connection, _tx());
            cmd.Parameters.AddWithValue("enrollmentId", enrollmentId);
            cmd.Parameters.AddWithValue("gradeValue", gradeValue);
            cmd.Parameters.AddWithValue("gradeType", gradeType);

            await cmd.ExecuteNonQueryAsync();
        }
    }

    // ===========================
    // Точка входу (демо роботи UoW + Repository)
    // ===========================

    public class Program
    {
        public static async Task Main()
        {
            // ці id повинні існувати в БД
            const long currentUserId = 1; // user.id, від імені якого виконуються дії
            const long studentId = 1; // students.id
            const long courseInstanceId = 1; // course_instances.id

            await using var uow = new UnitOfWork();

            try
            {
                await uow.BeginAsync(currentUserId);

                // 1) Показати активних студентів (читання через VIEW)
                Console.WriteLine("=== Active students ===");
                var students = await uow.Students.GetActiveAsync();
                foreach (var s in students)
                {
                    Console.WriteLine($"{s.Id}: {s.FullName} ({s.StudentCode}), group {s.GroupCode}");
                }

                // 2) Записати студента на курс (процедура enroll_student,
                //    всередині якої ми вже обробляємо дубль без помилки)
                await uow.Students.EnrollToCourseAsync(studentId, courseInstanceId);
                Console.WriteLine("Enroll checked (insert or skip).");

                // 3) Показати його записи на курси (читання через VIEW v_student_enrollments)
                Console.WriteLine("=== Enrollments ===");
                var enrollments = await uow.Students.GetEnrollmentsAsync(studentId);
                foreach (var e in enrollments)
                {
                    Console.WriteLine($"Enrollment {e.EnrollmentId}: {e.CourseName} {e.AcademicYear} sem {e.Semester}");
                }

                if (enrollments.Count == 0)
                {
                    Console.WriteLine("No enrollments for this student, GPA cannot be calculated.");
                }
                else
                {
                    // 4) Беремо перший enrollment і додаємо оцінку саме до нього
                    var firstEnrollmentId = enrollments[0].EnrollmentId;
                    await uow.Grades.AddGradeAsync(firstEnrollmentId, 95m, "exam");
                    Console.WriteLine("Grade added.");

                    // 5) Рахуємо середній бал (функція calculate_gpa)
                    var gpa = await uow.Students.GetGpaAsync(studentId);
                    Console.WriteLine($"GPA: {gpa}");
                }

                // 6) (опціонально) логічно видалити студента
                // await uow.Students.DeleteStudentAsync(studentId);
                // Console.WriteLine("Student soft-deleted.");

                await uow.CommitAsync();
            }
            catch (PostgresException ex)
            {
                Console.WriteLine($"Postgres error: {ex.SqlState} - {ex.MessageText}");
                await uow.RollbackAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
                await uow.RollbackAsync();
            }

            Console.WriteLine("Done. Press any key...");
            Console.ReadKey();
        }
    }
}
