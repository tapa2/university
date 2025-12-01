# Система управління університетом

Навчальний проєкт з баз даних та роботи з PostgreSQL + C#, який реалізує систему управління університетом:

- структура університету (факультети, кафедри, програми, групи);
- студенти та викладачі;
- курси, потоки курсів, записи на курси та оцінки;
- розклад занять;
- бізнес-логіка в БД (процедури, функції, тригери, view);
- робота з БД з коду через патерни **Repository + Unit of Work**.

---

## 1. Умова завдання

> **Варіант:** Система управління університетом  
>
> 1. Розробити схему бази даних з вказанням зв’язків між об’єктами, полями, ключами тощо.  
> 2. Мінімальна кількість сутностей — 15.  
> 3. Сутності повинні спиратися на сформовані вимоги. Хоча б деякі сутності мають підтримувати “Soft delete”, деякі — зберігати дату останньої зміни даних та користувача, що їх змінив.  
> 4. Реалізувати модель у одній з реляційних СУБД PostgreSQL.  
> 5. Створити й використати збережені процедури, користувацькі функції, тригери, розрізи даних (VIEW), тощо. В першу чергу для роботи з сутностями з п.3 (мінімум 10 об’єктів).  
> 6. Створити індекси (хоча б 2 різні типи).  
> 7. Реалізувати роботу з декількома сутностями БД з коду. Зробити комбінацію патернів **Repository + Unit of Work** для декількох логічно зв’язаних сутностей, де усі запити будуть через збережені процедури та розрізи даних (VIEW).

---

## 2. Предметна область

Система моделює процес навчання в університеті:

- університет складається з **факультетів**, **кафедр** та **освітніх програм**;
- студенти навчаються в **академічних групах**;
- викладачі закріплені за кафедрами;
- кафедри ведуть **курси**, які читаються у вигляді **потоків курсів** в конкретному навчальному році та семестрі;
- студенти **записуються на потоки курсів** та отримують **оцінки**;
- є **розклад занять**, що прив’язаний до аудиторій.

Також реалізовано **рівні доступу** (ролі користувачів) та механізми **аудиту** і **логічного видалення** (soft delete).

---

## 3. Схема бази даних

### 3.1. Сутності (15+)

У базі даних використано такі таблиці (сутності):

1. `users` – користувачі системи  
2. `roles` – ролі (Admin, Teacher, Student тощо)  
3. `user_roles` – зв’язок N:M між користувачами та ролями  
4. `faculties` – факультети  
5. `departments` – кафедри  
6. `study_programs` – освітні програми  
7. `student_groups` – академічні групи  
8. `students` – студенти  
9. `teachers` – викладачі  
10. `courses` – навчальні дисципліни  
11. `course_instances` – потоки курсів (курс + рік + семестр + викладач)  
12. `enrollments` – записи студентів на потоки курсів  
13. `grades` – оцінки  
14. `classrooms` – аудиторії  
15. `schedule_entries` – записи розкладу  

### 3.2. DBML-схема (для dbdiagram.io)

```dbml
// Use DBML to define your database structure
// Paste this into https://dbdiagram.io

/////////////////////////////////////////////////////////////
// USERS, ROLES, ACCESS CONTROL
/////////////////////////////////////////////////////////////

Table users {
  id bigserial [pk]
  username varchar [unique, not null]
  full_name varchar [not null]
  email varchar [unique, not null]

  created_at timestamp
  created_by bigint
  updated_at timestamp
  updated_by bigint
  deleted_at timestamp
  deleted_by bigint
  is_deleted boolean
}

Table roles {
  id bigserial [pk]
  name varchar [unique, not null]

  created_at timestamp
  created_by bigint
  updated_at timestamp
  updated_by bigint
  deleted_at timestamp
  deleted_by bigint
  is_deleted boolean
}

Table user_roles {
  user_id bigint [not null]
  role_id bigint [not null]

  primary key (user_id, role_id)
}

Ref: user_roles.user_id > users.id
Ref: user_roles.role_id > roles.id


/////////////////////////////////////////////////////////////
// UNIVERSITY STRUCTURE
/////////////////////////////////////////////////////////////

Table faculties {
  id bigserial [pk]
  name varchar [unique, not null]

  created_at timestamp
  created_by bigint
  updated_at timestamp
  updated_by bigint
  deleted_at timestamp
  deleted_by bigint
  is_deleted boolean
}

Table departments {
  id bigserial [pk]
  faculty_id bigint [not null]
  name varchar [not null]

  created_at timestamp
  created_by bigint
  updated_at timestamp
  updated_by bigint
  deleted_at timestamp
  deleted_by bigint
  is_deleted boolean
}

Ref: departments.faculty_id > faculties.id


Table study_programs {
  id bigserial [pk]
  department_id bigint [not null]
  name varchar [not null]

  created_at timestamp
  created_by bigint
  updated_at timestamp
  updated_by bigint
  deleted_at timestamp
  deleted_by bigint
  is_deleted boolean
}

Ref: study_programs.department_id > departments.id


Table student_groups {
  id bigserial [pk]
  study_program_id bigint [not null]
  code varchar [not null]
  start_year integer

  created_at timestamp
  created_by bigint
  updated_at timestamp
  updated_by bigint
  deleted_at timestamp
  deleted_by bigint
  is_deleted boolean
}

Ref: student_groups.study_program_id > study_programs.id


/////////////////////////////////////////////////////////////
// STUDENTS & TEACHERS
/////////////////////////////////////////////////////////////

Table students {
  id bigserial [pk]
  user_id bigint [unique, not null]
  group_id bigint [not null]
  student_code varchar [unique, not null]

  created_at timestamp
  created_by bigint
  updated_at timestamp
  updated_by bigint
  deleted_at timestamp
  deleted_by bigint
  is_deleted boolean
}

Ref: students.user_id > users.id
Ref: students.group_id > student_groups.id


Table teachers {
  id bigserial [pk]
  user_id bigint [unique, not null]
  department_id bigint [not null]
  position varchar

  created_at timestamp
  created_by bigint
  updated_at timestamp
  updated_by bigint
  deleted_at timestamp
  deleted_by bigint
  is_deleted boolean
}

Ref: teachers.user_id > users.id
Ref: teachers.department_id > departments.id


/////////////////////////////////////////////////////////////
// COURSES & COURSE INSTANCES
/////////////////////////////////////////////////////////////

Table courses {
  id bigserial [pk]
  department_id bigint [not null]
  code varchar [unique, not null]
  name varchar [not null]
  description text
  search_vector tsvector

  created_at timestamp
  created_by bigint
  updated_at timestamp
  updated_by bigint
  deleted_at timestamp
  deleted_by bigint
  is_deleted boolean
}

Ref: courses.department_id > departments.id


Table course_instances {
  id bigserial [pk]
  course_id bigint [not null]
  teacher_id bigint [not null]
  academic_year varchar [not null]
  semester integer [not null]

  created_at timestamp
  created_by bigint
  updated_at timestamp
  updated_by bigint
  deleted_at timestamp
  deleted_by bigint
  is_deleted boolean
}

Ref: course_instances.course_id > courses.id
Ref: course_instances.teacher_id > teachers.id


/////////////////////////////////////////////////////////////
// ENROLLMENTS & GRADES
/////////////////////////////////////////////////////////////

Table enrollments {
  id bigserial [pk]
  student_id bigint [not null]
  course_instance_id bigint [not null]

  created_at timestamp
  created_by bigint
  updated_at timestamp
  updated_by bigint
  deleted_at timestamp
  deleted_by bigint
  is_deleted boolean

  indexes {
    (student_id, course_instance_id) [unique]
  }
}

Ref: enrollments.student_id > students.id
Ref: enrollments.course_instance_id > course_instances.id


Table grades {
  id bigserial [pk]
  enrollment_id bigint [not null]
  grade_value numeric
  grade_type varchar

  created_at timestamp
  created_by bigint
  updated_at timestamp
  updated_by bigint
  deleted_at timestamp
  deleted_by bigint
  is_deleted boolean
}

Ref: grades.enrollment_id > enrollments.id


/////////////////////////////////////////////////////////////
// CLASSROOMS & SCHEDULE
/////////////////////////////////////////////////////////////

Table classrooms {
  id bigserial [pk]
  building varchar
  room_number varchar

  created_at timestamp
  created_by bigint
  updated_at timestamp
  updated_by bigint
  deleted_at timestamp
  deleted_by bigint
  is_deleted boolean
}


Table schedule_entries {
  id bigserial [pk]
  course_instance_id bigint [not null]
  classroom_id bigint [not null]
  starts_at timestamptz
  ends_at timestamptz

  created_at timestamp
  created_by bigint
  updated_at timestamp
  updated_by bigint
  deleted_at timestamp
  deleted_by bigint
  is_deleted boolean
}
Ref: schedule_entries.course_instance_id > course_instances.id
Ref: schedule_entries.classroom_id > classrooms.id
```
## 4. Soft delete та аудит змін

Для більшості таблиць (users, roles, faculties, departments, study_programs, student_groups, students, teachers, courses, course_instances, enrollments, grades, classrooms, schedule_entries) реалізовано:

**Soft delete:**

поля: is_deleted, deleted_at, deleted_by

BEFORE DELETE тригер → викликає функцію soft_delete_row(), яка змінює is_deleted = true і зберігає дату/користувача замість фізичного DELETE.

**Аудит змін:**

поля: created_at, created_by, updated_at, updated_by

BEFORE UPDATE тригери викликають функцію set_audit_fields(), яка виставляє updated_at = now() та updated_by з current_setting('app.current_user_id').

Таким чином, виконані вимоги п.3: частина сутностей підтримує soft delete, частина — дату останньої зміни та користувача.

## 5. PostgreSQL-модель

База реалізована у PostgreSQL (умова п.4).
Скрипти створення таблиць, функцій, процедур, тригерів та в’юх згруповані в окремому .sql файлі, наприклад:

db/schema.sql – таблиці, індекси, в’юхи;

db/functions_and_triggers.sql – процедури, функції, тригери.

(Назви файлів можна змінити під свій проєкт.)

## 6. Збережені процедури, функції, тригери, VIEW (п.5)
**Основні збережені процедури:**

set_current_user(p_user_id bigint) – виставляє app.current_user_id для аудиту/soft delete.

enroll_student(p_student_id bigint, p_course_instance_id bigint) – запис студента на потік курсу (перевіряє, чи студент вже записаний, і не дублює запис).

delete_student(p_student_id bigint) – логічне видалення студента через DELETE + soft delete-тригер.

add_grade(p_enrollment_id bigint, p_grade_value numeric, p_grade_type text) – додавання оцінки студенту.

**Користувацькі функції:**

set_audit_fields() – тригерна функція, виставляє updated_at та updated_by.

soft_delete_row() – тригерна функція для soft delete з динамічним SQL.

calculate_gpa(p_student_id bigint) – повертає середній бал студента за його оцінками.

get_student_courses(p_student_id bigint) – таблична функція, повертає всі курси студента.

(опційно) prevent_duplicate_enrollment() – тригерна функція для заборони дублюючих записів на курс (може бути замінена логікою в enroll_student).

**Тригери:**

*_audit – BEFORE UPDATE на всі основні таблиці (оновлення полів аудиту).

*_soft_delete – BEFORE DELETE на всі основні таблиці (soft delete).

(опційно) trg_enrollments_no_duplicate – BEFORE INSERT на enrollments для перевірки дублів.

**View (розрізи даних):**

v_students_active – активні (не видалені) студенти з ПІБ та кодом групи.

v_courses_active – активні курси з назвами кафедр.

v_student_enrollments – всі записи студента на курси з викладачами.

v_teacher_schedule – розклад викладача (курс + аудиторія + час).

Разом цих об’єктів (процедури + функції + тригери + в’юхи) значно більше 10 (умова п.5 виконана).

## 7. Індекси (п.6)

**Реалізовано як мінімум два різні типи індексів:**

**B-Tree індекси:**

idx_users_full_name на users(full_name) – для пошуку користувачів по ПІБ.

idx_schedule_entries_starts_at на schedule_entries(starts_at) – для швидкого пошуку в розкладі за часом.

**GIN-індекс для повнотекстового пошуку:**

стовпець courses.search_vector (tsvector, згенерований з code, name, description);

індекс idx_courses_search_vector – USING GIN (search_vector).

Це задовольняє умову “хоча б 2 різні типи індексів”.

## 8. Код застосунку: Repository + Unit of Work (п.7)

**Частина з коду реалізована на C# (.NET) з бібліотекою Npgsql (без ORM, все через ADO.NET):**

Патерн Unit of Work – клас UnitOfWork:

створює й утримує NpgsqlConnection;

керує транзакцією (Begin, Commit, Rollback);

при BeginAsync(currentUserId) викликає CALL set_current_user(@uid);,
щоб усі тригери аудиту / soft delete знали поточного користувача;

надає доступ до репозиторіїв: Students, Grades (можна розширити).

Патерн Repository – окремі репозиторії:

StudentRepository:

читання студентів тільки через VIEW:

GetActiveAsync() → v_students_active;

GetEnrollmentsAsync(studentId) → v_student_enrollments.

запис/логічне видалення тільки через процедури:

EnrollToCourseAsync(studentId, courseInstanceId) → CALL enroll_student(...);

DeleteStudentAsync(studentId) → CALL delete_student(...);

GetGpaAsync(studentId) → SELECT calculate_gpa(...).

GradeRepository:

AddGradeAsync(enrollmentId, gradeValue, gradeType) → CALL add_grade(...).

Усі запити з коду до БД виконуються або через збережені процедури, або через VIEW, що відповідає вимозі п.7:

"усі запити будуть йти через використання збережених процедур та розрізів даних".

Демонстраційний сценарій (Main)

У Program.Main виконується послідовність:

BeginAsync(currentUserId) – старт транзакції та установка поточного користувача.

Вивід активних студентів (v_students_active).

Запис студента на курс:

await uow.Students.EnrollToCourseAsync(studentId, courseInstanceId);


Процедура enroll_student:

перевіряє, чи студент вже записаний;

якщо ні — додає запис у enrollments;

якщо так — нічого не робить.

Отримання списку записів студента (v_student_enrollments).

Додавання оцінки до першого enrollment (CALL add_grade(...)).

Обчислення середнього балу (calculate_gpa).

(опціонально) логічне видалення студента через CALL delete_student(...).

CommitAsync() – підтвердження транзакції.

## 9. Як запустити проєкт

PostgreSQL:

створити базу даних, наприклад uni;

виконати SQL-скрипти зі створення таблиць, функцій, процедур, тригерів, view.

Налаштувати app:

у Program.cs змінити рядок підключення:

private const string ConnectionString =
    "Host=localhost;Port=5432;Database=uni;Username=postgres;Password=your_password";


Заповнити мінімальні дані:

хоча б одного користувача-адміна;

одного студента, прив’язаного до users;

одного викладача;

один курс і один course_instance.

Запустити консольний проєкт:

побачити вивід:

список студентів;

"Enroll checked (insert or skip).";

список записів студента;

"Grade added.";

"GPA: …".

## 10. Висновок

У проєкті реалізовано:

Повноцінну схему БД з 15+ сутностями та чіткими зв’язками (п.1, п.2).

Механізми soft delete та аудиту змін для ключових сутностей (п.3).

Модель у PostgreSQL з використанням специфічних типів (timestamptz, tsvector) (п.4).

Розвинений набір об’єктів БД: процедури, функції, тригери та в’юхи (понад 10) (п.5).

Декілька індексів різних типів: B-Tree + GIN (п.6).

Роботу з БД з коду через патерни Repository + Unit of Work з використанням лише процедур та view (п.7).
