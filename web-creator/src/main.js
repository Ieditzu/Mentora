import "./styles.css";

const API_BASE = (import.meta.env.VITE_API_BASE || "https://neurokey.serenityutils.club").replace(/\/$/, "");

const state = {
  token: localStorage.getItem("mentora_creator_token") || "",
  parentId: localStorage.getItem("mentora_creator_parent_id") || "",
  courses: []
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
    questions: [blankQuestion()]
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
    explanation: ""
  };
}

let editorCourse = blankCourse();

document.querySelector("#app").innerHTML = `
  <div class="page-shell">
    <aside class="sidebar">
      <div>
        <p class="eyebrow">Mentora</p>
        <h1>Course Creator</h1>
        <p class="muted">Local frontend, remote backend.</p>
        <p class="muted">API: ${escapeHtml(API_BASE)}</p>
      </div>
      <div class="sidebar-panel">
        <div id="authStatus" class="status-chip">Logged out</div>
        <button id="logoutButton" class="ghost-button" hidden>Log out</button>
      </div>
    </aside>
    <main class="main-content">
      <section id="authSection" class="panel auth-panel">
        <div>
          <p class="eyebrow">Creator Access</p>
          <h2>Login or register</h2>
          <p class="muted">This local app talks directly to the VPS backend.</p>
        </div>
        <div class="grid-two">
          <label>
            <span>Email</span>
            <input id="emailInput" type="email" placeholder="you@example.com">
          </label>
          <label>
            <span>Password</span>
            <input id="passwordInput" type="password" placeholder="Password">
          </label>
        </div>
        <div class="button-row">
          <button id="loginButton">Login</button>
          <button id="registerButton" class="secondary-button">Register</button>
        </div>
      </section>
      <section id="workspaceSection" hidden>
        <div class="workspace-grid">
          <section class="panel">
            <div class="section-heading">
              <div>
                <p class="eyebrow">Your Courses</p>
                <h2>Drafts and published content</h2>
              </div>
              <button id="newCourseButton">New course</button>
            </div>
            <div id="courseList" class="course-list"></div>
          </section>
          <section class="panel editor-panel">
            <div class="section-heading">
              <div>
                <p class="eyebrow">Editor</p>
                <h2 id="editorTitle">Create a quiz course</h2>
              </div>
              <div class="button-row compact">
                <button id="saveCourseButton">Save</button>
                <button id="deleteCourseButton" class="danger-button" hidden>Delete</button>
              </div>
            </div>
            <div class="grid-two">
              <label>
                <span>Title</span>
                <input id="courseTitle" type="text" placeholder="Intro to Variables">
              </label>
              <label>
                <span>Acronym</span>
                <input id="courseAcronym" type="text" placeholder="INTRO-VARS">
              </label>
              <label>
                <span>Language</span>
                <select id="courseLanguage">
                  <option value="general">General</option>
                  <option value="cpp">C++</option>
                  <option value="python">Python</option>
                </select>
              </label>
              <label>
                <span>Difficulty</span>
                <select id="courseDifficulty">
                  <option value="beginner">Beginner</option>
                  <option value="intermediate">Intermediate</option>
                  <option value="advanced">Advanced</option>
                </select>
              </label>
              <label>
                <span>Point reward</span>
                <input id="coursePoints" type="number" min="0" value="50">
              </label>
              <label class="checkbox-row">
                <input id="coursePublished" type="checkbox">
                <span>Published and visible in the game</span>
              </label>
            </div>
            <label>
              <span>Summary</span>
              <input id="courseSummary" type="text" maxlength="280" placeholder="A short one-line description for the game browser">
            </label>
            <label>
              <span>Description</span>
              <textarea id="courseDescription" rows="4" placeholder="What does this course teach?"></textarea>
            </label>
            <div class="section-heading subheading">
              <div>
                <p class="eyebrow">Quiz Questions</p>
                <h3>Question set</h3>
              </div>
              <button id="addQuestionButton" class="secondary-button">Add question</button>
            </div>
            <div id="questionList" class="question-list"></div>
          </section>
        </div>
      </section>
    </main>
  </div>
  <div id="toast" class="toast" hidden></div>
`;

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
  toast: document.getElementById("toast")
};

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
    ...(options.headers || {})
  };
  if (state.token) {
    headers.Authorization = `Bearer ${state.token}`;
  }

  const response = await fetch(`${API_BASE}${path}`, { ...options, headers });
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

  state.courses.forEach((course) => {
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

  els.questionList.querySelectorAll("[data-field]").forEach((input) => {
    input.addEventListener("input", (event) => {
      const index = Number(event.target.dataset.index);
      const field = event.target.dataset.field;
      editorCourse.questions[index][field] = event.target.value;
    });
  });

  els.questionList.querySelectorAll("[data-correct]").forEach((input) => {
    input.addEventListener("change", (event) => {
      const index = Number(event.target.dataset.correct);
      editorCourse.questions[index].correctIndex = Number(event.target.value);
    });
  });

  els.questionList.querySelectorAll("[data-remove]").forEach((button) => {
    button.addEventListener("click", (event) => {
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
      explanation: question.explanation?.trim() || ""
    }))
  };
}

async function saveCourse() {
  const payload = collectCourseFromForm();
  const path = editorCourse.id ? `/api/web/courses/${editorCourse.id}` : "/api/web/courses";
  const method = editorCourse.id ? "PUT" : "POST";
  editorCourse = await api(path, {
    method,
    body: JSON.stringify(payload)
  });
  await loadCourses();
  renderEditor();
  showToast("Course saved");
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
  const response = await api(`/api/web/auth/${mode}`, {
    method: "POST",
    body: JSON.stringify({
      email: els.emailInput.value.trim(),
      password: els.passwordInput.value
    })
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
  els.loginButton.addEventListener("click", () => handleAuth("login").catch((error) => showToast(error.message)));
  els.registerButton.addEventListener("click", () => handleAuth("register").catch((error) => showToast(error.message)));
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
  els.saveCourseButton.addEventListener("click", () => saveCourse().catch((error) => showToast(error.message)));
  els.deleteCourseButton.addEventListener("click", () => deleteCourse().catch((error) => showToast(error.message)));
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
  if (!state.token) {
    return;
  }
  try {
    await loadCourses();
    renderEditor();
  } catch (error) {
    logout();
    showToast(error.message);
  }
}

bootstrap();
