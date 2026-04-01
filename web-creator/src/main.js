import "./styles.css";

const API_BASE = (import.meta.env.VITE_API_BASE || "https://neuro.serenityutils.club").replace(/\/$/, "");

const state = {
  token: localStorage.getItem("mentora_creator_token") || "",
  parentId: localStorage.getItem("mentora_creator_parent_id") || "",
  courses: []
};

const authState = {
  step: "email",
  mode: "login",
  email: ""
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
  <div class="app-shell">
    <div class="ambient-grid"></div>
    <div class="ambient-orb ambient-orb-a"></div>
    <div class="ambient-orb ambient-orb-b"></div>

    <aside class="control-rail">
      <div class="brand-block">
        <p class="eyebrow">Mentora // Creator</p>
        <h1>Web Creator</h1>
        <p class="muted">Design and publish in-game learning paths from one local control surface.</p>
      </div>

      <div class="rail-card status-card">
        <div class="status-line">
          <span class="status-dot"></span>
          <span id="authStatus">Logged out</span>
        </div>
        <p id="authHint" class="muted">Authenticate to load your course network.</p>
        <button id="logoutButton" class="ghost-button" hidden>Log out</button>
      </div>

      <div class="rail-card">
        <p class="eyebrow">Endpoint</p>
        <p class="mono-line">${escapeHtml(API_BASE)}</p>
      </div>
    </aside>

    <main class="main-stage">
      <section class="hero-panel panel">
        <div class="hero-copy">
          <p class="eyebrow">Futuristic Authoring Surface</p>
          <h2>Build quiz worlds that feel native to the Mentora universe.</h2>
          <p class="muted">Shape titles, publish states, reward values, and question logic from a single tactical editor.</p>
        </div>
        <div class="hero-metrics">
          <div class="metric-card">
            <span class="metric-label">Courses</span>
            <strong id="courseCount">0</strong>
          </div>
          <div class="metric-card">
            <span class="metric-label">Questions</span>
            <strong id="questionCount">1</strong>
          </div>
          <div class="metric-card">
            <span class="metric-label">Mode</span>
            <strong id="publishBadge">Draft</strong>
          </div>
        </div>
      </section>

      <section id="authSection" class="panel auth-panel">
        <div class="section-heading">
          <div>
            <p class="eyebrow">Creator Access</p>
            <h2 id="authTitle">Authenticate the console</h2>
          </div>
        </div>
        <p id="authLead" class="muted">Enter your email first. Existing accounts go to sign in. New addresses go to sign up.</p>

        <div id="emailStep">
          <label>
            <span>Email</span>
            <input id="emailInput" type="email" placeholder="you@example.com">
          </label>
          <div class="button-row">
            <button id="continueButton">Continue</button>
          </div>
        </div>

        <div id="passwordStep" hidden>
          <div class="auth-email-pill">
            <span class="chip-label">Email</span>
            <strong id="authEmailValue"></strong>
          </div>
          <label>
            <span>Password</span>
            <input id="passwordInput" type="password" placeholder="Password">
          </label>
          <div class="button-row">
            <button id="authSubmitButton">Continue</button>
            <button id="changeEmailButton" class="secondary-button">Change email</button>
          </div>
        </div>
      </section>

      <section id="workspaceSection" class="workspace-section" hidden>
        <div class="workspace-grid">
          <section class="panel library-panel">
            <div class="section-heading">
              <div>
                <p class="eyebrow">Course Library</p>
                <h2>Drafts and published tracks</h2>
              </div>
              <button id="newCourseButton">New course</button>
            </div>
            <div id="courseList" class="course-list"></div>
          </section>

          <section class="panel editor-panel">
            <div class="section-heading editor-heading">
              <div>
                <p class="eyebrow">Editor Core</p>
                <h2 id="editorTitle">Create a quiz course</h2>
              </div>
              <div class="button-row compact">
                <button id="saveCourseButton">Save</button>
                <button id="deleteCourseButton" class="danger-button" hidden>Delete</button>
              </div>
            </div>

            <div class="editor-meta">
              <div class="info-chip">
                <span class="chip-label">API</span>
                <span class="chip-value">Live</span>
              </div>
              <div class="info-chip">
                <span class="chip-label">Questions</span>
                <span id="inlineQuestionCount" class="chip-value">1</span>
              </div>
              <div class="info-chip">
                <span class="chip-label">Visibility</span>
                <span id="inlinePublishBadge" class="chip-value">Draft</span>
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
                <p class="eyebrow">Question Matrix</p>
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
  authHint: document.getElementById("authHint"),
  authTitle: document.getElementById("authTitle"),
  authLead: document.getElementById("authLead"),
  logoutButton: document.getElementById("logoutButton"),
  emailStep: document.getElementById("emailStep"),
  passwordStep: document.getElementById("passwordStep"),
  emailInput: document.getElementById("emailInput"),
  continueButton: document.getElementById("continueButton"),
  passwordInput: document.getElementById("passwordInput"),
  authEmailValue: document.getElementById("authEmailValue"),
  authSubmitButton: document.getElementById("authSubmitButton"),
  changeEmailButton: document.getElementById("changeEmailButton"),
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
  courseCount: document.getElementById("courseCount"),
  questionCount: document.getElementById("questionCount"),
  publishBadge: document.getElementById("publishBadge"),
  inlineQuestionCount: document.getElementById("inlineQuestionCount"),
  inlinePublishBadge: document.getElementById("inlinePublishBadge"),
  toast: document.getElementById("toast")
};

function showToast(message) {
  els.toast.textContent = message;
  els.toast.hidden = false;
  els.toast.classList.remove("toast-live");
  requestAnimationFrame(() => els.toast.classList.add("toast-live"));
  clearTimeout(showToast.timeout);
  showToast.timeout = setTimeout(() => {
    els.toast.hidden = true;
    els.toast.classList.remove("toast-live");
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
  els.authStatus.textContent = loggedIn ? `Creator #${state.parentId} linked` : "Logged out";
  els.authHint.textContent = loggedIn
    ? "Remote authoring access confirmed."
    : "Authenticate to load your course network.";
  updateDashboardStats();
}

function renderAuthFlow() {
  const inPasswordStep = authState.step === "password";
  const isLogin = authState.mode === "login";

  els.emailStep.hidden = inPasswordStep;
  els.passwordStep.hidden = !inPasswordStep;
  els.authEmailValue.textContent = authState.email;
  els.authTitle.textContent = inPasswordStep
    ? (isLogin ? "Welcome back" : "Create your account")
    : "Authenticate the console";
  els.authLead.textContent = inPasswordStep
    ? (isLogin
        ? "We found your email. Enter your password to sign in."
        : "This email is new. Create a password to open a creator account.")
    : "Enter your email first. Existing accounts go to sign in. New addresses go to sign up.";
  els.authSubmitButton.textContent = isLogin ? "Sign in" : "Sign up";
}

function updateDashboardStats() {
  const questionTotal = editorCourse.questions.length || 0;
  const publishedLabel = editorCourse.published ? "Published" : "Draft";

  els.courseCount.textContent = String(state.courses.length);
  els.questionCount.textContent = String(questionTotal);
  els.publishBadge.textContent = publishedLabel;
  els.inlineQuestionCount.textContent = String(questionTotal);
  els.inlinePublishBadge.textContent = publishedLabel;
}

function renderCourseList() {
  els.courseList.innerHTML = "";
  if (!state.courses.length) {
    els.courseList.innerHTML = `
      <div class="empty-state">
        <p class="eyebrow">Empty Library</p>
        <h3>No courses online yet</h3>
        <p class="muted">Create your first course to seed the Mentora catalog.</p>
      </div>
    `;
    updateDashboardStats();
    return;
  }

  state.courses.forEach((course) => {
    const card = document.createElement("button");
    card.type = "button";
    card.className = `course-card ${course.id === editorCourse.id ? "active" : ""}`;
    card.innerHTML = `
      <div class="course-card-top">
        <div>
          <strong>${escapeHtml(course.title)}</strong>
          <p class="muted">${escapeHtml(course.acronym || "NO-ACRONYM")}</p>
        </div>
        <span class="chip ${course.published ? "chip-success" : ""}">${course.published ? "Published" : "Draft"}</span>
      </div>
      <p class="course-summary">${escapeHtml(course.summary || "No summary yet.")}</p>
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

  updateDashboardStats();
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
        <div>
          <p class="eyebrow">Question ${index + 1}</p>
          <strong>Response logic node</strong>
        </div>
        <button type="button" class="danger-button" data-remove="${index}">Remove</button>
      </div>
      <label>
        <span>Prompt</span>
        <textarea data-field="prompt" data-index="${index}" rows="3">${escapeHtml(question.prompt || "")}</textarea>
      </label>
      <div class="question-options">
        ${["A", "B", "C", "D"].map((letter, optionIndex) => `
          <div class="option-card">
            <label class="option-row">
              <input type="radio" name="correct-${index}" ${question.correctIndex === optionIndex ? "checked" : ""} data-correct="${index}" value="${optionIndex}">
              <span>Correct ${letter}</span>
            </label>
            <label>
              <span>Option ${letter}</span>
              <input type="text" data-field="option${letter}" data-index="${index}" value="${escapeHtml(question[`option${letter}`] || "")}">
            </label>
          </div>
        `).join("")}
      </div>
      <label>
        <span>Explanation</span>
        <textarea data-field="explanation" data-index="${index}" rows="2">${escapeHtml(question.explanation || "")}</textarea>
      </label>
    `;
    els.questionList.appendChild(card);
  });

  els.questionList.querySelectorAll("[data-field]").forEach((input) => {
    input.addEventListener("input", (event) => {
      const index = Number(event.target.dataset.index);
      const field = event.target.dataset.field;
      editorCourse.questions[index][field] = event.target.value;
      updateDashboardStats();
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

  updateDashboardStats();
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

async function continueAuthFlow() {
  const email = els.emailInput.value.trim();
  if (!email) {
    showToast("Enter your email");
    return;
  }

  const response = await api("/api/web/auth/lookup", {
    method: "POST",
    body: JSON.stringify({ email })
  });

  authState.email = email;
  authState.mode = response.exists ? "login" : "register";
  authState.step = "password";
  els.passwordInput.value = "";
  renderAuthFlow();
  els.passwordInput.focus();
}

async function handleAuth(mode) {
  const response = await api(`/api/web/auth/${mode}`, {
    method: "POST",
    body: JSON.stringify({
      email: authState.email || els.emailInput.value.trim(),
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
  authState.step = "email";
  authState.mode = "login";
  authState.email = "";
  els.passwordInput.value = "";
  syncAuthUI();
  renderAuthFlow();
  renderCourseList();
  renderEditor();
}

function wireEvents() {
  els.continueButton.addEventListener("click", () => continueAuthFlow().catch((error) => showToast(error.message)));
  els.authSubmitButton.addEventListener("click", () => handleAuth(authState.mode).catch((error) => showToast(error.message)));
  els.changeEmailButton.addEventListener("click", () => {
    authState.step = "email";
    authState.mode = "login";
    authState.email = "";
    els.passwordInput.value = "";
    renderAuthFlow();
    els.emailInput.focus();
  });
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
  els.coursePublished.addEventListener("change", () => {
    editorCourse.published = els.coursePublished.checked;
    updateDashboardStats();
  });
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
  renderAuthFlow();
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
