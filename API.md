# ASU Faculty of Engineering — Student API (Unofficial Reference)

> **Unofficial documentation.** The Faculty of Engineering, Ain Shams University ships a mobile app backed by a JSON API at `eng.asu.edu.eg/api`. This reference documents that API, reconstructed by decompiling the official `ASUENG Student` Flutter app and probing each endpoint.
>
> It is provided **as-is**, for interoperability and educational purposes, and may break without notice if the faculty changes the API. Use it only with your own credentials and your own account.
>
> All example values below are **fictional placeholders** — they do not represent any real student, grades, or records.

- **Base URL:** `https://eng.asu.edu.eg/api`
- **Version host (app updates):** `https://portal.eng.asu.edu.eg/api`
- **Auth:** JWT Bearer (obtained from [`/login`](#post-login))
- **Encoding:** `application/json`, UTF-8 (responses contain both English and Arabic text)

---

## Table of Contents

1. [Conventions](#conventions)
   - [Authentication flow](#authentication-flow)
   - [Request headers](#request-headers)
   - [Response envelope](#response-envelope)
   - [Status codes](#status-codes)
   - [Bilingual fields & enums](#bilingual-fields--enums)
2. [Authentication](#authentication)
   - [`POST /login`](#post-login)
   - [`POST /logout`](#post-logout)
3. [Profile](#profile)
   - [`POST /students/my_details`](#post-studentsmy_details)
4. [Academics](#academics)
   - [`GET /students/my_courses`](#get-studentsmy_courses)
   - [`GET /students/my_results`](#get-studentsmy_results)
   - [`GET /students/my_grades_recheck`](#get-studentsmy_grades_recheck)
   - [`GET /students/schedules/my_schedule`](#get-studentsschedulesmy_schedule)
5. [Finance](#finance)
   - [`GET /students/my_fees`](#get-studentsmy_fees)
6. [Requests & Services](#requests--services)
   - [`GET /students/my_excuses`](#get-studentsmy_excuses)
   - [`GET /students/my_certificates_requests`](#get-studentsmy_certificates_requests)
   - [`GET /students/my_trainings`](#get-studentsmy_trainings)
   - [`GET /students/available_trainings`](#get-studentsavailable_trainings)
7. [Support & Tickets](#support--tickets)
   - [`POST /students/tickets/list`](#post-studentsticketslist)
   - [`POST /students/academic_discussion/list`](#post-studentsacademic_discussionlist)
   - [`POST /students/tickets/add`](#post-studentsticketsadd)
   - [Ticket messages](#ticket-messages)
8. [Notifications](#notifications)
   - [`GET /students/notifications/list`](#get-studentsnotificationslist)
   - [`POST /students/notifications/seen`](#post-studentsnotificationsseen)
   - [`POST /students/notifications/clear`](#post-studentsnotificationsclear)
9. [Sessions](#sessions)
   - [`GET /students/active_sessions`](#get-studentsactive_sessions)
10. [App Version](#app-version)
    - [`GET /mobileapp/latest`](#get-mobileapplatest)
11. [Appendix: Methodology](#appendix-methodology)

Each endpoint is tagged **✅ Verified** (a live response was captured and the schema below reflects it) or **⚠️ Unverified** (the route exists in the official app binary but its request/response shape was not independently confirmed here — typically state-changing actions that were not exercised).

---

## Conventions

### Authentication flow

1. `POST /login` with the student's email and password → returns a JWT `accessToken`.
2. Send that token as `Authorization: Bearer <accessToken>` on every subsequent request.
3. When the token expires, any endpoint returns `code: 401` with `error[].key = "access_token"` and message `"Your Session is expired please Login Again!"`. Re-run `/login` to obtain a fresh token.

There is **no CAPTCHA** on the API login (unlike the website's browser login).

### Request headers

| Header | Value | Required |
|---|---|---|
| `Authorization` | `Bearer <accessToken>` | All endpoints except `/login` |
| `Accept-Language` | `en` or `ar` | Recommended — controls localized strings |
| `Content-Type` | `application/json` | On `POST` requests |

### Response envelope

Every endpoint returns the same wrapper:

```json
{
  "data": { },
  "status": true,
  "message": null,
  "error": [],
  "code": 200
}
```

| Field | Type | Description |
|---|---|---|
| `data` | object \| array \| null | The payload. Shape depends on the endpoint. |
| `status` | boolean | `true` on success, `false` on failure. |
| `message` | string \| null | Human-readable message, usually `null`. |
| `error` | array | Empty on success; on failure holds `{ "key": "...", "error": "..." }` items. |
| `code` | integer | Mirrors the semantic HTTP status (`200`, `401`, …). The transport-level HTTP status is often `200` even when `code` is `401`, so read `code` rather than the HTTP status. |

### Status codes

| `code` | Meaning |
|---|---|
| `200` | Success. |
| `401` | Missing/expired token. `error[0].key = "access_token"`. |
| `422` | Validation error (bad/missing request fields). |

### Bilingual fields & enums

- Most named entities appear twice: `en_*` (English) and `ar_*` (Arabic). Example: `en_name` / `ar_name`, `en_term` / `ar_term`.
- List endpoints commonly return an `enums` object alongside the data, mapping integer codes to labels (e.g. fee `status`, ticket `status`). Resolve coded fields against these maps rather than hard-coding.
- Terms are formatted `"<Season> <Year>"` — `Fall`, `Spring`, or `Summer` (e.g. `"Spring 2026"`).

---

## Authentication

### `POST /login`

**✅ Verified** · Authenticates a student and returns a JWT. No token required.

**Request body**

| Field | Type | Required | Description |
|---|---|---|---|
| `email` | string | yes | `<student_id>@eng.asu.edu.eg` (e.g. `23P0001@eng.asu.edu.eg`). |
| `password` | string | yes | Faculty portal password. |

```json
{ "email": "23P0001@eng.asu.edu.eg", "password": "••••••••" }
```

**Response** — `data`

| Field | Type | Description |
|---|---|---|
| `security.accessToken` | string | JWT to use as the Bearer token. |
| `security.tokenType` | string | Always `"Bearer"`. |
| `general` | object | Profile block — see [`/students/my_details`](#post-studentsmy_details). |
| `study` | object | Current enrollment block — see below. |

`data.study` fields: `bylaw`, `en_minor`/`ar_minor`, `en_major`/`ar_major`, `en_term`/`ar_term` (current term), `type` (e.g. `"credit_hours"`), `ar_program`, `en_level`/`ar_level`.

```json
{
  "data": {
    "security": { "accessToken": "eyJ0eXAiOiJKV1Q...", "tokenType": "Bearer" },
    "general":  { "id": 100000, "code": "23P0001", "en_name": "Student Name", "email": "23P0001@eng.asu.edu.eg" },
    "study":    { "bylaw": "UG2023", "en_term": "Spring 2026", "en_level": "Junior",
                  "type": "credit_hours" }
  },
  "status": true, "message": null, "error": [], "code": 200
}
```

> **Invalid credentials** return `status: false` with a populated `error` array and no `security` block.

### `POST /logout`

**⚠️ Unverified** · Present in the app binary; invalidates the current token. Requires the Bearer token. Not exercised here (state-changing).

---

## Profile

### `POST /students/my_details`

**✅ Verified** · Returns the full profile and current-enrollment blocks. Empty JSON body (`{}`).

**Response** — `data.general`

| Field | Type | Notes |
|---|---|---|
| `id` | integer | Internal user id. |
| `code` | string | Student ID (e.g. `23P0001`). |
| `en_name` / `ar_name` | string | Full name. |
| `first_name` / `middle_name` / `last_name` | string | |
| `email` | string | |
| `phone` / `mobile` | string \| null | |
| `address` / `second_address_line` / `postal_code` | string \| null | |
| `gender` | integer | |
| `birth_date` | string (`YYYY-MM-DD`) | |
| `image_url` | string | Profile photo URL. |
| `can_request_excuse` | boolean | Feature gate. |
| `can_request_training` | boolean | Feature gate. |
| `can_request_grade_recheck` | boolean | Feature gate. |
| `training_archive_id` | integer \| null | |

`data.study` matches the [`/login`](#post-login) `study` block. `study.en_term` is the **current term** — useful for separating in-progress courses from completed ones.

---

## Academics

### `GET /students/my_courses`

**✅ Verified** · Live grade breakdown for the student's **currently enrolled** courses.

> **Note:** this endpoint returns the current term **and the most recently completed term** — there is no per-course term field. To isolate the current term, exclude any course whose `code` already appears in [`/students/my_results`](#get-studentsmy_results) (i.e. courses that already have a final grade).

**Response** — `data.studies[]`

| Field | Type | Description |
|---|---|---|
| `id` | integer | Enrollment (study) id. |
| `committee_id` | integer | Course offering id (unique per term offering). |
| `course_id` | integer | Catalog course id. |
| `code` | string | Course code (e.g. `CSE000`). |
| `en_name` / `ar_name` | string | Course name. |
| `credit_hours` | integer | |
| `midterm` / `max_midterm` | integer | Midterm score / max. |
| `activities` / `max_activities` | integer | |
| `external_activities` / `max_external_activities` | integer | |
| `practical` / `max_practical` | integer | |
| `grades_detailes` | array | Per-component breakdown — `{ en_name, ar_name, degree, max_degree }`. `degree` may be `null` if not yet graded. |
| `grade` | object | The final course grade once released; `{}` while in progress. |
| `teachers` | array | `{ id, en_name, ar_name }`. |
| `exam`, `open_attendance`, `attendance_history`, `attendance_statistics` | object/array | Attendance & exam metadata (not covered here). |

`data` also includes a top-level `open_attendances` array (currently open attendance sessions).

```json
{
  "code": 200,
  "data": {
    "studies": [
      {
        "id": 1000001, "committee_id": 40000, "course_id": 10000,
        "code": "CSE000", "en_name": "Example Course", "credit_hours": 3,
        "midterm": 18, "max_midterm": 20,
        "activities": 24, "max_activities": 25,
        "external_activities": 5, "max_external_activities": 5,
        "grades_detailes": [
          { "en_name": "Midterm", "ar_name": "نصف العام", "degree": 18, "max_degree": 20 },
          { "en_name": "Activities", "ar_name": "أنشطة", "degree": 24, "max_degree": 25 },
          { "en_name": "External Activities", "ar_name": "الأنشطة الخارجية", "degree": 5, "max_degree": 5 }
        ],
        "grade": {},
        "teachers": [ { "id": 100, "en_name": "Instructor Name" } ]
      }
    ],
    "open_attendances": []
  }
}
```

### `GET /students/my_results`

**✅ Verified** · Final results for **every completed semester**, plus cumulative GPA.

**Response** — `data.results[]` (ordered oldest → newest)

| Field | Type | Description |
|---|---|---|
| `en_term` / `ar_term` | string | Semester. |
| `years` | string | Academic year (e.g. `"2023/2024"`). |
| `bylaw`, `en_major`/`ar_major`, `en_minor`/`ar_minor`, `en_program`/`ar_program`, `en_level`/`ar_level` | string | Standing for that term. |
| `grade.cumulative_gpa` | float | **Cumulative GPA** as of this term (the last element = current overall CGPA). |
| `grade.cumulative_credit_hours` | integer | |
| `grade.cumulative_passed_hours` | integer | |
| `grade.grade_letter` | string | Cumulative letter. |
| `grades[]` | array | Courses taken that term (below). |

`results[].grades[]` fields:

| Field | Type | Description |
|---|---|---|
| `code` | string | Course code. |
| `en_name` / `ar_name` | string | Course name. |
| `credit_hours` | integer | |
| `grade_gpa` | float | Course GPA points. |
| `grade.grade` / `grade.en_grade` / `grade.ar_grade` | string | Final letter grade (e.g. `"A"`, `"B-"`). |

```json
{
  "code": 200,
  "data": {
    "results": [
      {
        "en_term": "Fall 2025", "years": "2025/2026",
        "grade": { "cumulative_gpa": 3.50, "cumulative_credit_hours": 90, "cumulative_passed_hours": 90, "grade_letter": "B+" },
        "grades": [
          { "code": "CSE343", "en_name": "Web Development", "credit_hours": 3, "grade_gpa": 4.0,
            "grade": { "en_grade": "A+", "ar_grade": "A+", "grade": "A+" } }
        ]
      }
    ],
    "enums": {}
  }
}
```

### `GET /students/my_grades_recheck`

**✅ Verified** · The student's grade-recheck (remark) requests.

**Response** — `data.gradesRecheck[]`

| Field | Type | Description |
|---|---|---|
| `id` | integer | Request id. |
| `created_at` | string (datetime) | |
| `course_code` / `course_name` / `course_id` | | The course. |
| `term_id` / `term_name` | | Term of the course. |
| `committee_id` | integer | Course offering. |
| `control_decision` | integer | Decision code. |
| `control_decision_label` | string | Localized decision text. |

### `GET /students/schedules/my_schedule`

**✅ Verified** · Current-term weekly timetable.

**Response** — `data.my_schedule` is an object keyed by weekday (`"Sat"`, `"Sun"`, …); each value is an array of sessions:

| Field | Type | Description |
|---|---|---|
| `id` | integer | Session id. |
| `from` / `to` | string | Start/end time (e.g. `"8.00"`, `"9.50"`). |
| `short_name` | string | Course code. |
| `course_name` | string | |
| `committee_id` | integer | Course offering. |
| `course_type` | string | e.g. `"Lab"`, `"Lecture"`. |
| `location_name` | string \| null | |
| `term_name` | string | e.g. `"Spring 2026"`. |
| `online_status` | string | `"Online"` / `"Offline"`. |
| `has_attendance_session` | boolean | |
| `attendance_session` / `attendance_status` / `attendance_status_label` | varies | Live attendance state. |

> Only courses with a published timetable slot appear here, so it is **not** a complete current-course list.

`data.enums` accompanies the schedule.

---

## Finance

### `GET /students/my_fees`

**✅ Verified** · Tuition and fee transactions.

**Response** — `data`

`payments[]`:

| Field | Type | Description |
|---|---|---|
| `id` | integer | Transaction id. |
| `amount` | float | Amount due. |
| `paid` | float | Amount paid. |
| `method` | integer | See `enums.methods`. |
| `due_date` | string (`YYYY-MM-DD`) | |
| `notes` | string \| null | |
| `currency` | string | e.g. `"EGP"`. |
| `service_name` | string | e.g. `"Tuition Fees"`. |
| `status` | integer | See `enums.status`. |
| `en_term` / `ar_term` | string | |
| `full_pay` | integer | |

`enums.status`: `0 Unpaid`, `1 Paid`, `2 Partially Paid`, `3 Over Pay`, `4 Conflict`, `7 Freeze`.
`enums.methods`: `0 One Time`, `1 Installments`.
`statistics.totals`: aggregate totals object.

---

## Requests & Services

### `GET /students/my_excuses`

**✅ Verified** · Absence-excuse requests.

**Response** — `data.excuses[]` (empty when none) plus `data.enums.excuses` mapping:
`-1 Incomplete`, `0 Paper Authenticated`, `1 Waiting for the program`, `2 Waiting for the student`, `3 Waiting for the council`, `4 Accepted`, `5 Rejected`, `6 Canceled`, `7 No Response`, `8 Needs More Documents`.

### `GET /students/my_certificates_requests`

**✅ Verified** · Official document / certificate requests.

**Response** — `data.certificates[]`

| Field | Type | Description |
|---|---|---|
| `certificate_type_id` | integer | See `enums.certificatesTypes`. |
| `en_certificate` / `ar_certificate` | string | Certificate name. |
| `status` | integer | Request status. |
| `print_count` | integer | |
| `parameters` | string (JSON) | Request-specific parameters. |
| `apply_to` | string | Delivery/target. |
| `tracking_number` | string \| null | |
| `post_fees` | integer | |
| `from` / `to` / `notes` | varies | |

`data.enums.certificatesTypes` maps ~30 certificate type ids to names (Arabic/English transcripts, graduation certificates, statements, etc.).

### `GET /students/my_trainings`

**✅ Verified** · The student's training/internship records.

**Response** — `data.trainings[]`

| Field | Type | Description |
|---|---|---|
| `id` / `training_id` | integer | |
| `title` | string | e.g. `"Example Training"`. |
| `en_term` / `ar_term` | string | |
| `years` | string | |
| `type` | string | e.g. `"Internal"`. |
| `approved_num_of_weeks` / `num_of_weeks` | integer | |
| `cv_required` / `interview_required` | integer (0/1) | |
| `application_start_date` / `application_end_date` | string (date) | |
| `start_date` / `end_date` | string (date) | |
| `admission_date` | string (datetime) | |
| `status` | integer | See `enums.status`. |

`enums.status`: `-1 Request`, `0 In Review`, `1 Accepted`, `2 Rejected`, `3 Approved`, `6 Finished`, `7 Semi Accepted`, `8 Disapproved`, `9 Canceled`, `10 Absent`.

### `GET /students/available_trainings`

**⚠️ Unverified** · Present in the app binary (the catalog of trainings open for application). The plain `GET`/`POST` calls tried here returned the portal's HTML shell rather than JSON, so it likely requires query parameters (e.g. paging/term) that were not determined. Schema not confirmed.

---

## Support & Tickets

Tickets share a common status enum:
`-1 Incomplete`, `0 New`, `1 Updated`, `2 Closed`, `3 Feedback`, `4 No-Response`, `5 Pending`.

### `POST /students/tickets/list`

**✅ Verified** · Lists the student's support tickets. Empty JSON body (`{}`).

**Response** — `data`

`tickets[]`:

| Field | Type | Description |
|---|---|---|
| `id` | integer | Ticket id. |
| `title` | string | |
| `description` | string | |
| `ticket_type_id` | integer | See `enums.tickets_types`. |
| `ticket_type` | string | Localized type. |
| `status` | integer | See `enums.tickets_statues`. |
| `created_by` / `staff_name` | string | |
| `user_id` | integer | |
| `created_at` / `updated_at` | string (datetime) | |
| `unseen_count` | integer \| null | Unread replies. |
| `related_id` | integer \| null | |

`enums.tickets_types[]`: `{ id, en_name, ar_name }` (e.g. `11 Academic Discussion`, `12 Technical Support`, `26 Innovative Ideas`, `19000 Others`).
`enums.tickets_statues`: the status map above.

### `POST /students/academic_discussion/list`

**✅ Verified** · The student's academic-discussion ticket (with the academic advisor). Empty JSON body (`{}`).

**Response** — `data.ticket` (a single ticket object, same shape as a `tickets/list` item) plus `data.enums` (`tickets_types`, `tickets_statues`).

### `POST /students/tickets/add`

**⚠️ Unverified** · Present in the app binary; creates a support ticket. Body likely includes `ticket_type_id`, `title`, `description`. Not exercised here (state-changing).

### Ticket messages

**⚠️ Unverified** · The app binary exposes a per-ticket chat:

| Route | Purpose |
|---|---|
| `/students/messages/list/{ticketId}` | Fetch the message thread for a ticket. |
| `/students/messages/add/{ticketId}` | Post a message to a ticket. |
| `/students/messages/edit/{ticketId}` | Edit a message. |

The `GET /messages/list/{id}` shape was not confirmed (the call tried returned HTML, so it likely uses a different method or path parameter). `add`/`edit` are state-changing and were not exercised.

---

## Notifications

### `GET /students/notifications/list`

**✅ Verified** · Paginated notifications.

**Query parameters**

| Param | Type | Default | Description |
|---|---|---|---|
| `page` | integer | `1` | Page number. |
| `limit` | integer | `10` | Page size. |

**Response** — `data`

| Field | Type | Description |
|---|---|---|
| `notifications` | array | Notification items. |
| `unseen` | integer | Count of unseen notifications. |
| `enums.model_types` | object | `1 Ticket`, `2 FinancialTransaction`, `3 StudentFee` — the subject type a notification links to. |

### `POST /students/notifications/seen`

**⚠️ Unverified** · Present in the app binary; marks notification(s) as seen. State-changing — not exercised.

### `POST /students/notifications/clear`

**⚠️ Unverified** · Present in the app binary; clears notifications. State-changing — not exercised.

---

## Sessions

### `GET /students/active_sessions`

**✅ Verified** · Currently active login sessions for the account. `data` is an array (empty when none/all current).

---

## App Version

### `GET /mobileapp/latest`

**✅ Verified** · Served from the **version host** `https://portal.eng.asu.edu.eg/api`. Used by the mobile app for force-update checks.

**Query parameters**

| Param | Type | Description |
|---|---|---|
| `platform` | integer | Client platform code (e.g. `2`). |
| `os` | integer | OS code (e.g. `2`). |

**Response** — this endpoint returns the object **directly** (not wrapped in the standard envelope):

| Field | Type | Description |
|---|---|---|
| `version_number` | string | Latest version. |
| `version_date` | string (date) | Release date. |
| `forced` | boolean | Whether the update is mandatory. |
| `build_number` | integer | |
| `url` | string | Store URL. |

```json
{ "version_number": "0.0.0", "version_date": "2026-01-01", "forced": true, "build_number": 1,
  "url": "https://apps.apple.com/eg/app/asueng-student/id1573567276" }
```

---

## Appendix: Methodology

This reference was produced in two passes:

1. **Static analysis** — the official `ASUENG Student` Android app (Flutter) was unpacked and its `libapp.so` scanned for endpoint string literals, yielding the complete route table under `/api/students/…` plus the auth and version routes.
2. **Dynamic verification** — each read-only endpoint was called to derive the field tables above. **All example values in this document are fictional placeholders** and do not represent any real student or record.

State-changing endpoints (`add`, `edit`, `seen`, `clear`, `logout`) and endpoints that did not return JSON for the parameters tried are documented from the app binary and clearly marked **⚠️ Unverified**. Everything marked **✅ Verified** reflects a confirmed response shape.

> Maintained as part of the [grade-monitor](README.md) project.
