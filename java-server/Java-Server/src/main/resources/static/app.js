const state = {
  token: localStorage.getItem("mentora_creator_token") || "",
  parentId: localStorage.getItem("mentora_creator_parent_id") || "",
  courses: [],
  selectedCourseId: null,
};

const els = {
  authSection: document.getElementById("authSection"),
  workspaceSection: document.getElementById("workspaceSection"),
  authStatus: document.getElementById("authStatus"),
  logoutButton: document.getElementById("logoutButton"),
  emailInput: document.getElementById("emailInput"),
  passwordInput: document.getElementById("passwordInput"),
  loginButton: document.getElementById("loginButton"),
  registerButton: document.getElementById("registerButton"),
  newCourseButton: document.getElementById("newCourseButton"),
  saveCourseButton: document.getElementById("saveCourseButton"),
  deleteCourseButton: document.getElementById("deleteCourseButton"),
  addQuestionButton: document.getElementById("addQuestionButton"),
  courseList: document.getElementById("courseList"),
  questionList: document.getElementById("questionList"),
  editorTitle: document.getElementById("editorTitle"),
  courseTitle: document.getElementById("courseTitle"),
  courseAcronym: document.getElementById("courseAcronym"),
  courseLanguage: document.getElementById("courseLanguage"),
  courseDifficulty: document.getElementById("courseDifficulty"),
  coursePoints: document.getElementById("coursePoints"),
  courseSummary: document.getElementById("courseSummary"),
  courseDescription: document.getElementById("courseDescription"),
  coursePublished: document.getElementById("coursePublished"),
  toast: document.getElementById("toast"),
};

function blankCourse() {
  return {
    id: null,
    title: "",
    acronym: "",
    language: "general",
    difficulty: "beginner",
    summary: "",
    description: "",
    pointReward: 50,
    published: false,
    questions: [blankQuestion()],
  };
}

function blankQuestion() {
  return {
    prompt: "",
    optionA: "",
    optionB: "",
    optionC: "",
    optionD: "",
    correctIndex: 0,
    explanation: "",
  };
}

let editorCourse = blankCourse();

function showToast(message) {
  els.toast.textContent = message;
  els.toast.hidden = false;
  clearTimeout(showToast.timeout);
  showToast.timeout = setTimeout(() => {
    els.toast.hidden = true;
  }, 3000);
}

async function api(path, options = {}) {
  const headers = {
    "Content-Type": "application/json",
    ...(options.headers || {}),
  };
  if (state.token) {
    headers.Authorization = `Bearer ${state.token}`;
  }

  const response = await fetch(path, { ...options, headers });
  const data = await response.json().catch(() => ({}));
  if (!response.ok || data.error) {
    throw new Error(data.error || "Request failed");
  }
  return data;
}

function syncAuthUI() {
  const loggedIn = Boolean(state.token);
  els.authSection.hidden = loggedIn;
  els.workspaceSection.hidden = !loggedIn;
  els.logoutButton.hidden = !loggedIn;
  els.authStatus.textContent = loggedIn ? `Logged in as creator #${state.parentId}` : "Logged out";
}

function renderCourseList() {
  els.courseList.innerHTML = "";
  if (!state.courses.length) {
    els.courseList.innerHTML = `<div class="course-card"><p class="muted">No courses yet. Create your first quiz course.</p></div>`;
    return;
  }

  state.courses.forEach(course => {
    const card = document.createElement("button");
    card.type = "button";
    card.className = `course-card ${course.id === editorCourse.id ? "active" : ""}`;
    card.innerHTML = `
      <div class="section-heading">
        <strong>${escapeHtml(course.title)}</strong>
        <span class="chip">${course.published ? "Published" : "Draft"}</span>
      </div>
      <p class="muted">${escapeHtml(course.summary || "No summary yet.")}</p>
      <div class="course-meta">
        <span class="chip">${escapeHtml(course.language)}</span>
        <span class="chip">${escapeHtml(course.difficulty)}</span>
        <span class="chip">${course.questionCount} questions</span>
        <span class="chip">${course.pointReward} pts</span>
      </div>
    `;
    card.addEventListener("click", () => loadCourse(course.id));
    els.courseList.appendChild(card);
  });
}

function renderEditor() {
  els.editorTitle.textContent = editorCourse.id ? `Editing ${editorCourse.title || "course"}` : "Create a quiz course";
  els.deleteCourseButton.hidden = !editorCourse.id;
  els.courseTitle.value = editorCourse.title || "";
  els.courseAcronym.value = editorCourse.acronym || "";
  els.courseLanguage.value = editorCourse.language || "general";
  els.courseDifficulty.value = editorCourse.difficulty || "beginner";
  els.coursePoints.value = editorCourse.pointReward ?? 50;
  els.courseSummary.value = editorCourse.summary || "";
  els.courseDescription.value = editorCourse.description || "";
  els.coursePublished.checked = Boolean(editorCourse.published);

  els.questionList.innerHTML = "";
  editorCourse.questions.forEach((question, index) => {
    const card = document.createElement("div");
    card.className = "question-card";
    card.innerHTML = `
      <div class="section-heading">
        <strong>Question ${index + 1}</strong>
        <button type="button" class="danger-button" data-remove="${index}">Remove</button>
      </div>
      <label><span>Prompt</span><textarea data-field="prompt" data-index="${index}" rows="3">${escapeHtml(question.prompt || "")}</textarea></label>
      <div class="question-options">
        ${["A", "B", "C", "D"].map((letter, optionIndex) => `
          <label class="option-row">
            <input type="radio" name="correct-${index}" ${question.correctIndex === optionIndex ? "checked" : ""} data-correct="${index}" value="${optionIndex}">
            <span>Correct ${letter}</span>
          </label>
          <label>
            <span>Option ${letter}</span>
            <input type="text" data-field="option${letter}" data-index="${index}" value="${escapeHtml(question[`option${letter}`] || "")}">
          </label>
        `).join("")}
      </div>
      <label><span>Explanation</span><textarea data-field="explanation" data-index="${index}" rows="2">${escapeHtml(question.explanation || "")}</textarea></label>
    `;
    els.questionList.appendChild(card);
  });

  els.questionList.querySelectorAll("[data-field]").forEach(input => {
    input.addEventListener("input", event => {
      const index = Number(event.target.dataset.index);
      const field = event.target.dataset.field;
      editorCourse.questions[index][field] = event.target.value;
    });
  });

  els.questionList.querySelectorAll("[data-correct]").forEach(input => {
    input.addEventListener("change", event => {
      const index = Number(event.target.dataset.correct);
      editorCourse.questions[index].correctIndex = Number(event.target.value);
    });
  });

  els.questionList.querySelectorAll("[data-remove]").forEach(button => {
    button.addEventListener("click", event => {
      const index = Number(event.target.dataset.remove);
      editorCourse.questions.splice(index, 1);
      if (!editorCourse.questions.length) {
        editorCourse.questions.push(blankQuestion());
      }
      renderEditor();
    });
  });
}

async function loadCourses() {
  state.courses = await api("/api/web/courses/mine");
  renderCourseList();
}

async function loadCourse(courseId) {
  editorCourse = await api(`/api/web/courses/${courseId}`);
  state.selectedCourseId = courseId;
  renderCourseList();
  renderEditor();
}

function collectCourseFromForm() {
  return {
    ...editorCourse,
    title: els.courseTitle.value.trim(),
    acronym: els.courseAcronym.value.trim(),
    language: els.courseLanguage.value,
    difficulty: els.courseDifficulty.value,
    pointReward: Number(els.coursePoints.value || 0),
    summary: els.courseSummary.value.trim(),
    description: els.courseDescription.value.trim(),
    published: els.coursePublished.checked,
    questions: editorCourse.questions.map((question, index) => ({
      orderIndex: index,
      prompt: question.prompt?.trim() || "",
      optionA: question.optionA?.trim() || "",
      optionB: question.optionB?.trim() || "",
      optionC: question.optionC?.trim() || "",
      optionD: question.optionD?.trim() || "",
      correctIndex: Number(question.correctIndex || 0),
      explanation: question.explanation?.trim() || "",
    })),
  };
}

async function saveCourse() {
  const payload = collectCourseFromForm();
  const path = editorCourse.id ? `/api/web/courses/${editorCourse.id}` : "/api/web/courses";
  const method = editorCourse.id ? "PUT" : "POST";
  editorCourse = await api(path, {
    method,
    body: JSON.stringify(payload),
  });
  await loadCourses();
  showToast("Course saved");
  renderEditor();
}

async function deleteCourse() {
  if (!editorCourse.id) {
    return;
  }
  await api(`/api/web/courses/${editorCourse.id}`, { method: "DELETE" });
  editorCourse = blankCourse();
  await loadCourses();
  renderEditor();
  showToast("Course deleted");
}

async function handleAuth(mode) {
  const email = els.emailInput.value.trim();
  const password = els.passwordInput.value;
  const response = await api(`/api/web/auth/${mode}`, {
    method: "POST",
    body: JSON.stringify({ email, password }),
  });
  state.token = response.token;
  state.parentId = String(response.parentId);
  localStorage.setItem("mentora_creator_token", state.token);
  localStorage.setItem("mentora_creator_parent_id", state.parentId);
  syncAuthUI();
  await loadCourses();
  renderEditor();
  showToast(mode === "login" ? "Logged in" : "Account created");
}

function logout() {
  state.token = "";
  state.parentId = "";
  state.courses = [];
  editorCourse = blankCourse();
  localStorage.removeItem("mentora_creator_token");
  localStorage.removeItem("mentora_creator_parent_id");
  syncAuthUI();
  renderCourseList();
  renderEditor();
}

function wireEvents() {
  els.loginButton.addEventListener("click", () => handleAuth("login").catch(error => showToast(error.message)));
  els.registerButton.addEventListener("click", () => handleAuth("register").catch(error => showToast(error.message)));
  els.logoutButton.addEventListener("click", logout);
  els.newCourseButton.addEventListener("click", () => {
    editorCourse = blankCourse();
    renderCourseList();
    renderEditor();
  });
  els.addQuestionButton.addEventListener("click", () => {
    editorCourse.questions.push(blankQuestion());
    renderEditor();
  });
  els.saveCourseButton.addEventListener("click", () => saveCourse().catch(error => showToast(error.message)));
  els.deleteCourseButton.addEventListener("click", () => deleteCourse().catch(error => showToast(error.message)));
}

function escapeHtml(value) {
  return String(value)
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;");
}

async function bootstrap() {
  syncAuthUI();
  renderCourseList();
  renderEditor();
  wireEvents();
  if (state.token) {
    try {
      await loadCourses();
      renderEditor();
    } catch (error) {
      logout();
      showToast(error.message);
    }
  }
}

bootstrap();
