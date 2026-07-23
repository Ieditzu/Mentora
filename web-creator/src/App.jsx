import React, { useState, useEffect, useCallback } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { 
  Plus, Save, Trash2, LogOut, Globe, Terminal, 
  AlertCircle, CheckCircle2, Layout,
  Layers, Sparkles, BookOpen, Zap,
  ChevronRight, MoreVertical, Activity, Settings, User, FileText,
  Clock, Code, Cpu, ShieldCheck
} from 'lucide-react';
import { api, API_BASE } from './lib/api';
import { translate } from './lib/i18n';
import { clsx } from 'clsx';
import { twMerge } from 'tailwind-merge';

function cn(...inputs) {
  return twMerge(clsx(inputs));
}

const blankQuestion = () => ({
  prompt: "",
  optionA: "",
  optionB: "",
  optionC: "",
  optionD: "",
  correctIndex: 0,
  explanation: ""
});

const blankCourse = () => ({
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
});

// Helper to fix the bug where options were empty when editing
const mapBackendToFrontend = (courseData) => {
  if (!courseData.questions) return courseData;
  
  return {
    ...courseData,
    questions: courseData.questions.map(q => {
      // If backend sends 'options' array, map to optionA/B/C/D
      if (q.options && Array.isArray(q.options)) {
        return {
          ...q,
          optionA: q.options[0] || "",
          optionB: q.options[1] || "",
          optionC: q.options[2] || "",
          optionD: q.options[3] || "",
        };
      }
      return q;
    })
  };
};

export default function App() {
  const [token, setToken] = useState(localStorage.getItem("mentora_creator_token") || "");
  const [parentId, setParentId] = useState(localStorage.getItem("mentora_creator_parent_id") || "");
  const [language, setLanguage] = useState(localStorage.getItem("mentora_creator_language") || "en");
  const [courses, setCourses] = useState([]);
  const [editorCourse, setEditorCourse] = useState(blankCourse());
  const [authState, setAuthState] = useState({ step: "email", mode: "login", email: "" });
  const [loading, setLoading] = useState(false);
  const [toast, setToast] = useState(null);
  
  // Navigation State
  const [activeTab, setActiveTab] = useState('dashboard'); // dashboard, library, editor

  const t = useCallback((text, variables) => translate(language, text, variables), [language]);

  const toggleLanguage = () => {
    const supportedLanguages = ["en", "ro", "fr", "de"];
    const nextLanguage = supportedLanguages[(supportedLanguages.indexOf(language) + 1) % supportedLanguages.length];
    setLanguage(nextLanguage);
    localStorage.setItem("mentora_creator_language", nextLanguage);
  };

  const showToast = useCallback((message, type = "info") => {
    setToast({ message, type });
    setTimeout(() => setToast(null), 3000);
  }, []);

  const loadCourses = useCallback(async (authToken) => {
    try {
      const data = await api("/api/web/courses/mine", {}, authToken);
      setCourses(data);
    } catch (err) {
      showToast(err.message, "error");
    }
  }, [showToast]);

  useEffect(() => {
    if (token) {
      loadCourses(token);
    }
  }, [token, loadCourses]);

  const handleLogout = () => {
    setToken("");
    setParentId("");
    setCourses([]);
    setEditorCourse(blankCourse());
    localStorage.removeItem("mentora_creator_token");
    localStorage.removeItem("mentora_creator_parent_id");
    setAuthState({ step: "email", mode: "login", email: "" });
    showToast(t("Logged out successfully"));
  };

  const handleAuthLookup = async (email) => {
    setLoading(true);
    try {
      const response = await api("/api/web/auth/lookup", {
        method: "POST",
        body: JSON.stringify({ email })
      });
      setAuthState({ email, mode: response.exists ? "login" : "register", step: "password" });
    } catch (err) {
      showToast(err.message, "error");
    } finally {
      setLoading(false);
    }
  };

  const handleAuthSubmit = async (password) => {
    setLoading(true);
    try {
      const response = await api(`/api/web/auth/${authState.mode}`, {
        method: "POST",
        body: JSON.stringify({ email: authState.email, password })
      });
      if (response.requiresTotp) {
        setAuthState({
          ...authState,
          step: "totp",
          challengeId: response.challengeId,
          expiresInSeconds: response.expiresInSeconds
        });
        return;
      }
      completeWebAuthentication(response, authState.mode === "login" ? "Welcome back!" : "Account created!");
    } catch (err) {
      showToast(err.message, "error");
    } finally {
      setLoading(false);
    }
  };

  const handleTotpSubmit = async (code) => {
    setLoading(true);
    try {
      const response = await api("/api/web/auth/login/totp", {
        method: "POST",
        body: JSON.stringify({ challengeId: authState.challengeId, code: code.trim() })
      });
      completeWebAuthentication(response, "Two-factor sign-in complete");
    } catch (err) {
      showToast(err.message, "error");
    } finally {
      setLoading(false);
    }
  };

  const completeWebAuthentication = (response, message) => {
    setToken(response.token);
    setParentId(String(response.parentId));
    localStorage.setItem("mentora_creator_token", response.token);
    localStorage.setItem("mentora_creator_parent_id", String(response.parentId));
    setAuthState({ step: "email", mode: "login", email: "" });
    showToast(t(message));
    setActiveTab('dashboard');
  };

  const handleSaveCourse = async () => {
    setLoading(true);
    try {
      const method = editorCourse.id ? "PUT" : "POST";
      const path = editorCourse.id ? `/api/web/courses/${editorCourse.id}` : "/api/web/courses";
      const data = await api(path, {
        method,
        body: JSON.stringify(editorCourse)
      }, token);
      
      setEditorCourse(mapBackendToFrontend(data));
      await loadCourses(token);
      showToast(t("Course saved successfully"));
    } catch (err) {
      showToast(err.message, "error");
    } finally {
      setLoading(false);
    }
  };

  const handleDeleteCourse = async () => {
    if (!editorCourse.id) return;
    if (!confirm(t("Are you sure you want to delete this course?"))) return;
    setLoading(true);
    try {
      await api(`/api/web/courses/${editorCourse.id}`, { method: "DELETE" }, token);
      setEditorCourse(blankCourse());
      await loadCourses(token);
      showToast(t("Course deleted"));
      setActiveTab('library');
    } catch (err) {
      showToast(err.message, "error");
    } finally {
      setLoading(false);
    }
  };

  const loadCourseDetail = async (id) => {
    try {
      const data = await api(`/api/web/courses/${id}`, {}, token);
      setEditorCourse(mapBackendToFrontend(data));
      setActiveTab('editor');
    } catch (err) {
      showToast(err.message, "error");
    }
  };

  // Main UI Shell
  return (
    <div className="relative min-h-screen bg-bg-base text-text selection:bg-brand/30 selection:text-brand overflow-hidden font-body">
      <button
        type="button"
        onClick={toggleLanguage}
        className="absolute right-5 top-5 z-50 rounded-lg border border-border bg-surface-raised px-3 py-2 text-xs font-bold text-text hover:border-brand hover:text-white transition-colors"
        aria-label={t("Change language")}
      >
        {language.toUpperCase()}
      </button>
      {/* Background Decorative Elements */}
      <div className="absolute inset-0 z-0 pointer-events-none overflow-hidden">
        <div className="absolute top-[-10%] left-[-10%] w-[40%] h-[40%] rounded-full bg-brand/10 blur-[120px] animate-blob" />
        <div className="absolute bottom-[-10%] right-[-10%] w-[40%] h-[40%] rounded-full bg-purple-500/10 blur-[120px] animate-blob" style={{ animationDelay: '4s' }} />
        <div className="absolute inset-0 bg-grid-pattern opacity-[0.03]" />
      </div>

      {/* App Content */}
      <div className="relative z-10 flex h-screen">
        {!token ? (
          <div className="flex-1 flex items-center justify-center p-6">
            <AuthSection 
              state={authState} 
              loading={loading}
              t={t}
              onLookup={handleAuthLookup} 
              onSubmit={handleAuthSubmit}
              onTotp={handleTotpSubmit}
              onReset={() => setAuthState({ step: "email", mode: "login", email: "" })}
            />
          </div>
        ) : (
          <>
            {/* Sidebar */}
            <aside className="w-64 flex-shrink-0 bg-surface/50 border-r border-border backdrop-blur-xl flex flex-col z-20">
              <div className="p-6 flex items-center gap-3">
                <div className="w-10 h-10 rounded-xl overflow-hidden flex items-center justify-center shadow-lg shadow-brand/20">
                  <img src="/logo.png" alt="Mentora Logo" className="w-full h-full object-cover" />
                </div>
                <div>
                  <h1 className="text-lg font-heading font-bold text-white tracking-wide">Mentora</h1>
                  <p className="text-[10px] text-brand font-mono uppercase tracking-widest leading-none">{t("Creator")}</p>
                </div>
              </div>

              <div className="flex-1 px-4 py-6 space-y-8 overflow-y-auto">
                <div className="space-y-1">
                  <p className="px-3 text-[10px] font-bold text-text-muted uppercase tracking-widest mb-3">{t("Workspace")}</p>
                  <SidebarItem icon={Layout} label={t("Dashboard")} active={activeTab === 'dashboard'} onClick={() => setActiveTab('dashboard')} />
                  <SidebarItem icon={BookOpen} label={t("Course Library")} active={activeTab === 'library'} onClick={() => setActiveTab('library')} />
                  <SidebarItem icon={FileText} label={t("Active Editor")} active={activeTab === 'editor'} onClick={() => setActiveTab('editor')} />
                </div>
              </div>

              <div className="p-4 border-t border-border bg-surface/30">
                <div className="flex items-center gap-3 px-3 py-2 mb-2">
                  <div className="w-8 h-8 rounded-full bg-surface-raised border border-border flex items-center justify-center text-text-muted">
                    <User size={14} />
                  </div>
                  <div className="flex-1 overflow-hidden">
                    <p className="text-xs font-bold text-white truncate">{t("Creator")} #{parentId}</p>
                    <p className="text-[10px] text-success flex items-center gap-1">
                      <span className="w-1.5 h-1.5 rounded-full bg-success animate-pulse" /> {t("Connected")}
                    </p>
                  </div>
                </div>
                <button 
                  onClick={handleLogout}
                  className="w-full flex items-center gap-2 px-3 py-2 rounded-lg text-text-muted hover:text-danger hover:bg-danger/10 transition-colors text-xs font-semibold"
                >
                  <LogOut size={14} />
                  {t("Sign Out")}
                </button>
              </div>
            </aside>

            {/* Main View Area */}
            <main className="flex-1 flex flex-col h-screen overflow-hidden relative">
              {/* Header */}
              <header className="h-16 border-b border-border/50 bg-bg-base/80 backdrop-blur-md flex items-center justify-between px-8 z-10 shrink-0">
                <div className="flex items-center gap-2 text-sm text-text-muted">
                  <span>{t(activeTab === 'dashboard' ? "Dashboard" : activeTab === 'library' ? "Course Library" : "Active Editor")}</span>
                  {activeTab === 'editor' && editorCourse.title && (
                    <>
                      <ChevronRight size={14} />
                      <span className="text-text font-medium">{editorCourse.title}</span>
                    </>
                  )}
                </div>
              </header>

              {/* View Content */}
              <div className="flex-1 overflow-y-auto p-8 relative">
                <AnimatePresence mode="wait">
                  {activeTab === 'dashboard' && (
                    <motion.div 
                      key="dashboard"
                      initial={{ opacity: 0, y: 10 }}
                      animate={{ opacity: 1, y: 0 }}
                      exit={{ opacity: 0, y: -10 }}
                      className="max-w-5xl mx-auto space-y-8"
                    >
                      <div className="space-y-2">
                        <h2 className="text-3xl font-heading font-bold text-white">{t("Welcome back.")}</h2>
                        <p className="text-text-muted">{t("Here is the current status of your courses.")}</p>
                      </div>

                      <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
                        <StatCard label={t("Total Courses")} value={courses.length} icon={BookOpen} trend={t("+2 this week")} />
                        <StatCard label={t("Total Questions")} value={courses.reduce((acc, c) => acc + (c.questionCount || 0), 0)} icon={Layers} trend={t("Active")} />
                        <StatCard label={t("Published")} value={courses.filter(c => c.published).length} icon={Globe} trend={t("Live")} />
                      </div>

                      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6 mt-8">
                        <div className="card p-6 border-border/50 bg-surface/30">
                          <h3 className="text-sm font-bold text-white mb-4 flex items-center gap-2"><Activity size={16} className="text-brand" /> {t("Recent Activity")}</h3>
                          {courses.length === 0 ? (
                            <div className="text-center py-8 text-text-muted">
                              <p className="text-sm">{t("No activity recorded yet.")}</p>
                              <button onClick={() => { setEditorCourse(blankCourse()); setActiveTab('editor'); }} className="text-brand text-sm font-medium hover:underline mt-2">{t("Create your first course")}</button>
                            </div>
                          ) : (
                            <div className="space-y-4">
                              {courses.slice(0, 4).map(c => (
                                <div key={c.id} className="flex items-center justify-between p-3 rounded-xl bg-surface hover:bg-surface-raised cursor-pointer transition-colors border border-border/50" onClick={() => loadCourseDetail(c.id)}>
                                  <div>
                                    <p className="text-sm font-bold text-white">{c.title}</p>
                                    <p className="text-[10px] text-text-muted uppercase font-mono mt-0.5">{c.acronym || t("UNTITLED")}</p>
                                  </div>
                                  <div className={cn("w-2 h-2 rounded-full", c.published ? "bg-success" : "bg-text-dim")} />
                                </div>
                              ))}
                            </div>
                          )}
                        </div>
                        
                        <div className="card p-6 border-border/50 bg-gradient-to-br from-brand/10 to-transparent flex flex-col justify-center items-center text-center">
                          <div className="w-16 h-16 rounded-2xl bg-brand/20 flex items-center justify-center text-brand mb-4">
                            <Plus size={32} />
                          </div>
                          <h3 className="text-lg font-bold text-white mb-2">{t("Create New Course")}</h3>
                          <p className="text-sm text-text-muted mb-6 max-w-xs">{t("Start building a new learning path for Mentora.")}</p>
                          <button onClick={() => { setEditorCourse(blankCourse()); setActiveTab('editor'); }} className="btn-primary">
                            {t("Create Course")}
                          </button>
                        </div>
                      </div>
                    </motion.div>
                  )}

                  {activeTab === 'library' && (
                    <motion.div 
                      key="library"
                      initial={{ opacity: 0, y: 10 }}
                      animate={{ opacity: 1, y: 0 }}
                      exit={{ opacity: 0, y: -10 }}
                      className="max-w-6xl mx-auto space-y-6"
                    >
                      <div className="flex items-center justify-between">
                        <div>
                          <h2 className="text-2xl font-heading font-bold text-white">{t("Course Library")}</h2>
                          <p className="text-text-muted text-sm">{t("Manage your draft and published courses.")}</p>
                        </div>
                        <button onClick={() => { setEditorCourse(blankCourse()); setActiveTab('editor'); }} className="btn-primary">
                          <Plus size={16} /> {t("New Course")}
                        </button>
                      </div>

                      {courses.length === 0 ? (
                        <div className="card p-16 flex flex-col items-center justify-center text-center border-dashed border-border">
                          <BookOpen size={48} className="text-text-dim mb-4 opacity-50" />
                          <h3 className="text-lg font-bold text-white mb-2">{t("Library Empty")}</h3>
                          <p className="text-text-muted max-w-sm mb-6">{t("You haven't created any courses yet. Begin by creating a new course in the editor.")}</p>
                        </div>
                      ) : (
                        <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-6">
                          <AnimatePresence>
                            {courses.map((course) => (
                              <motion.div
                                key={course.id}
                                layout
                                initial={{ opacity: 0, scale: 0.95 }}
                                animate={{ opacity: 1, scale: 1 }}
                                exit={{ opacity: 0, scale: 0.9 }}
                                className="card group hover:border-brand/40 transition-all duration-500 flex flex-col bg-surface/40 backdrop-blur-md relative overflow-hidden"
                              >
                                {/* Subtle background glow on hover */}
                                <div className="absolute inset-0 bg-gradient-to-br from-brand/5 via-transparent to-purple-500/5 opacity-0 group-hover:opacity-100 transition-opacity duration-500" />
                                
                                <div className="p-6 flex-1 relative z-10">
                                  <div className="flex justify-between items-start mb-5">
                                    <div className="flex flex-col gap-1">
                                      <div className="flex items-center gap-2">
                                        <div className="p-1.5 rounded-lg bg-surface-raised border border-border group-hover:border-brand/30 transition-colors">
                                          {course.language === 'cpp' || course.language === 'python' || course.language === 'javascript' ? (
                                            <Code size={14} className="text-brand" />
                                          ) : (
                                            <BookOpen size={14} className="text-text-muted" />
                                          )}
                                        </div>
                                        <span className="text-[10px] font-mono font-bold text-brand uppercase tracking-wider">{course.acronym || 'ID:N/A'}</span>
                                      </div>
                                    </div>
                                    <span className={cn(
                                      "text-[9px] font-black px-2.5 py-1 rounded-full uppercase border tracking-tighter",
                                      course.published 
                                        ? "bg-success/10 text-success border-success/30" 
                                        : "bg-surface-raised text-text-dim border-border"
                                    )}>
                                      {course.published ? t("Published") : t("Draft")}
                                    </span>
                                  </div>

                                  <h4 className="text-xl font-bold text-white mb-3 group-hover:text-brand transition-colors duration-300 leading-tight">
                                    {course.title}
                                  </h4>
                                  <p className="text-sm text-text-muted line-clamp-2 leading-relaxed opacity-80 group-hover:opacity-100 transition-opacity">
                                    {course.summary || t("No description sequence defined.")}
                                  </p>
                                </div>

                                <div className="px-6 py-4 border-t border-border/50 bg-black/20 flex items-center justify-between relative z-10">
                                  <div className="flex items-center gap-4 text-[10px] font-bold text-text-dim uppercase tracking-widest">
                                    <div className="flex items-center gap-1.5"><Layers size={14}/> {course.questionCount || 0}</div>
                                    <div className="flex items-center gap-1.5"><Zap size={14}/> {t(course.difficulty === 'beginner' ? "Beginner" : course.difficulty === 'intermediate' ? "Intermediate" : "Advanced")}</div>
                                  </div>
                                  <button 
                                    onClick={() => loadCourseDetail(course.id)}
                                    className="w-10 h-10 rounded-xl bg-surface-raised text-text-muted group-hover:bg-brand group-hover:text-white group-hover:shadow-[0_0_20px_rgba(129,140,248,0.3)] transition-all duration-300 flex items-center justify-center"
                                  >
                                    <ChevronRight size={20} />
                                  </button>
                                </div>
                              </motion.div>
                            ))}
                          </AnimatePresence>
                        </div>
                      )}
                    </motion.div>
                  )}

                  {activeTab === 'editor' && (
                    <motion.div 
                      key="editor"
                      initial={{ opacity: 0, y: 10 }}
                      animate={{ opacity: 1, y: 0 }}
                      exit={{ opacity: 0, y: -10 }}
                      className="max-w-4xl mx-auto pb-24"
                    >
                      <div className="flex items-center justify-between mb-8 sticky top-0 bg-bg-base/90 backdrop-blur-md py-4 z-20 border-b border-transparent transition-all">
                        <div>
                          <h2 className="text-2xl font-heading font-bold text-white">
                            {t(editorCourse.id ? "Edit Course" : "Create Course")}
                          </h2>
                          <p className="text-text-muted text-sm">{t("Configure course details and questions.")}</p>
                        </div>
                        <div className="flex items-center gap-3">
                          {editorCourse.id && (
                            <button onClick={handleDeleteCourse} disabled={loading} className="btn-danger">
                              <Trash2 size={16} /> {t("Delete")}
                            </button>
                          )}
                          <button onClick={handleSaveCourse} disabled={loading} className="btn-primary">
                            {loading ? <div className="w-4 h-4 border-2 border-white/30 border-t-white rounded-full animate-spin" /> : <Save size={16} />}
                            {t("Save Course")}
                          </button>
                        </div>
                      </div>

                      <div className="space-y-8">
                        {/* Meta Settings */}
                        <div className="card p-8 space-y-6 bg-surface/50 backdrop-blur-sm border-white/5">
                          <h3 className="text-sm font-bold text-white border-b border-border/50 pb-2 flex items-center gap-2">
                            <Settings size={16} className="text-brand" /> {t("Course Settings")}
                          </h3>
                          
                          <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                            <InputField label={t("Course Title")} value={editorCourse.title} onChange={v => setEditorCourse({...editorCourse, title: v})} placeholder={t("e.g. Intro to Python")} />
                            <InputField label={t("Acronym")} value={editorCourse.acronym} onChange={v => setEditorCourse({...editorCourse, acronym: v})} placeholder="e.g. PY-101" />
                            <SelectField 
                              label={t("Language")}
                              value={editorCourse.language} 
                              onChange={v => setEditorCourse({...editorCourse, language: v})}
                              options={[{ label: t('General'), value: 'general' }, { label: 'C++', value: 'cpp' }, { label: 'Python', value: 'python' }, { label: 'JavaScript', value: 'javascript' }]}
                            />
                            <SelectField 
                              label={t("Difficulty")}
                              value={editorCourse.difficulty} 
                              onChange={v => setEditorCourse({...editorCourse, difficulty: v})}
                              options={[{ label: t('Beginner'), value: 'beginner' }, { label: t('Intermediate'), value: 'intermediate' }, { label: t('Advanced'), value: 'advanced' }]}
                            />
                            <InputField label={t("Points")} type="number" value={editorCourse.pointReward} onChange={v => setEditorCourse({...editorCourse, pointReward: Number(v)})} />
                            
                            <div className="flex flex-col justify-center pt-6">
                              <label className="flex items-center gap-3 cursor-pointer group">
                                <div className="relative flex items-center justify-center">
                                  <input 
                                    type="checkbox" 
                                    checked={editorCourse.published} 
                                    onChange={e => setEditorCourse({...editorCourse, published: e.target.checked})}
                                    className="peer sr-only"
                                  />
                                  <div className="w-10 h-6 bg-surface-raised rounded-full border border-border peer-checked:bg-brand transition-colors"></div>
                                  <div className="absolute left-1 w-4 h-4 bg-text-muted rounded-full peer-checked:translate-x-4 peer-checked:bg-white transition-transform"></div>
                                </div>
                                <span className="text-sm font-medium text-text group-hover:text-white transition-colors">{t("Publish course")}</span>
                              </label>
                            </div>
                          </div>

                          <div className="space-y-6 pt-4">
                            <TextareaField label={t("Summary")} rows={2} value={editorCourse.summary} onChange={v => setEditorCourse({...editorCourse, summary: v})} placeholder={t("A brief description for the course card...")} />
                            <TextareaField label={t("Description")} rows={4} value={editorCourse.description} onChange={v => setEditorCourse({...editorCourse, description: v})} placeholder={t("Full overview of the course content...")} />
                          </div>
                        </div>

                        {/* Logic Nodes */}
                        <div className="space-y-6">
                          <div className="flex items-center justify-between">
                            <div>
                              <h3 className="text-lg font-bold text-white flex items-center gap-2">
                                <Layers size={20} className="text-brand" /> {t("Questions")}
                              </h3>
                              <p className="text-xs text-text-muted">{t("Add questions to your course.")}</p>
                            </div>
                            <button 
                              onClick={() => setEditorCourse({...editorCourse, questions: [...editorCourse.questions, blankQuestion()]})}
                              className="btn-secondary text-sm py-2 px-4"
                            >
                              <Plus size={14} /> {t("Add Question")}
                            </button>
                          </div>

                          <div className="space-y-6">
                            <AnimatePresence>
                              {editorCourse.questions.map((q, idx) => (
                                <QuestionNode 
                                  key={idx} 
                                  index={idx} 
                                  question={q} 
                                  onChange={newQ => {
                                    const qs = [...editorCourse.questions];
                                    qs[idx] = newQ;
                                    setEditorCourse({...editorCourse, questions: qs});
                                  }}
                                  onRemove={() => {
                                    const qs = editorCourse.questions.filter((_, i) => i !== idx);
                                    setEditorCourse({...editorCourse, questions: qs.length ? qs : [blankQuestion()]});
                                  }}
                                  t={t}
                                />
                              ))}
                            </AnimatePresence>
                          </div>
                        </div>
                      </div>
                    </motion.div>
                  )}
                </AnimatePresence>
              </div>
            </main>
          </>
        )}
      </div>

      {/* Toast Notification */}
      <AnimatePresence>
        {toast && (
          <motion.div 
            initial={{ opacity: 0, y: 50, scale: 0.9 }} 
            animate={{ opacity: 1, y: 0, scale: 1 }} 
            exit={{ opacity: 0, y: 20, scale: 0.9 }}
            className={cn(
              "fixed bottom-8 right-8 z-[100] px-5 py-4 rounded-xl shadow-2xl flex items-center gap-3 border backdrop-blur-xl bg-surface/90",
              toast.type === 'error' ? "border-danger/50" : "border-success/50"
            )}
          >
            <div className={cn("w-8 h-8 rounded-full flex items-center justify-center bg-opacity-20", toast.type === 'error' ? "bg-danger text-danger" : "bg-success text-success")}>
              {toast.type === 'error' ? <AlertCircle size={16} /> : <CheckCircle2 size={16} />}
            </div>
            <span className="text-sm font-semibold text-white">{toast.message}</span>
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  );
}

// Subcomponents

function SidebarItem({ icon: Icon, label, active, onClick }) {
  return (
    <button 
      onClick={onClick}
      className={cn(
        "w-full flex items-center gap-3 px-3 py-2.5 rounded-xl transition-all duration-200 text-sm font-medium",
        active 
          ? "bg-brand/10 text-brand border border-brand/20 shadow-inner" 
          : "text-text-muted hover:bg-surface-raised hover:text-text border border-transparent"
      )}
    >
      <Icon size={18} className={cn("transition-colors", active ? "text-brand" : "text-text-dim")} />
      {label}
    </button>
  );
}

function StatCard({ label, value, icon: Icon, trend }) {
  return (
    <div className="card p-6 border-white/5 bg-surface/40 backdrop-blur-sm relative overflow-hidden group">
      <div className="absolute -right-4 -top-4 w-24 h-24 bg-brand/5 rounded-full blur-2xl group-hover:bg-brand/10 transition-colors" />
      <div className="flex items-start justify-between mb-4 relative z-10">
        <div className="w-10 h-10 rounded-xl bg-surface-raised border border-border flex items-center justify-center text-brand">
          <Icon size={20} />
        </div>
        {trend && <span className="text-[10px] font-bold text-success bg-success-bg px-2 py-1 rounded-md">{trend}</span>}
      </div>
      <div className="relative z-10">
        <p className="text-3xl font-heading font-bold text-white mb-1">{value}</p>
        <p className="text-xs text-text-muted uppercase tracking-wider font-bold">{label}</p>
      </div>
    </div>
  );
}

function InputField({ label, ...props }) {
  return (
    <div className="space-y-1">
      <label className="label-text">{label}</label>
      <input className="input-field" {...props} onChange={e => props.onChange(e.target.value)} />
    </div>
  );
}

function SelectField({ label, options, onChange, ...props }) {
  return (
    <div className="space-y-1">
      <label className="label-text">{label}</label>
      <select className="input-field" {...props} onChange={e => onChange(e.target.value)}>
        {options.map(opt => <option key={opt.value} value={opt.value}>{opt.label}</option>)}
      </select>
    </div>
  );
}

function TextareaField({ label, ...props }) {
  return (
    <div className="space-y-1">
      <label className="label-text">{label}</label>
      <textarea className="input-field resize-y min-h-[80px]" {...props} onChange={e => props.onChange(e.target.value)} />
    </div>
  );
}

function AuthSection({ state, onLookup, onSubmit, onTotp, onReset, loading, t }) {
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [totpCode, setTotpCode] = useState("");

  useEffect(() => {
    if (state.step === "totp") {
      setPassword("");
      setTotpCode("");
    }
  }, [state.step]);

  return (
    <motion.div initial={{ opacity: 0, scale: 0.95 }} animate={{ opacity: 1, scale: 1 }} className="w-full max-w-md">
      <div className="glass-panel rounded-3xl p-10 relative overflow-hidden border-white/10">
        <div className="absolute top-0 left-0 w-full h-1 bg-gradient-to-r from-transparent via-brand to-transparent opacity-50" />
        
        <div className="text-center space-y-4 mb-8">
          <div className="w-20 h-20 rounded-3xl overflow-hidden border border-white/10 flex items-center justify-center mx-auto shadow-2xl shadow-brand/20">
            <img src="/logo.png" alt="Mentora Logo" className="w-full h-full object-cover" />
          </div>
          <div>
            <h2 className="text-2xl font-heading font-bold text-white tracking-tight">
              {state.step === "totp" ? t("Verify your sign-in") : t("Sign In")}
            </h2>
            <p className="text-sm text-text-muted mt-1">
              {state.step === 'email'
                ? t("Enter your email address to continue.")
                : state.step === 'totp'
                  ? t("Enter your authenticator or one-time recovery code. This challenge expires in {{seconds}} seconds.", { seconds: state.expiresInSeconds })
                  : t("Enter password for {{email}}", { email: state.email })}
            </p>
          </div>
        </div>

        <div className="space-y-6">
          {state.step === 'email' ? (
            <>
              <InputField label={t("Email Address")} value={email} onChange={setEmail} placeholder="creator@mentora.net" onKeyDown={e => e.key === 'Enter' && onLookup(email)} />
              <button onClick={() => onLookup(email)} disabled={!email || loading} className="btn-primary w-full py-3.5 mt-2">
                {loading ? t("Continuing...") : t("Continue")}
              </button>
            </>
          ) : state.step === 'totp' ? (
            <motion.div initial={{ opacity: 0, x: 20 }} animate={{ opacity: 1, x: 0 }} className="space-y-6">
              <div className="mx-auto flex h-14 w-14 items-center justify-center rounded-2xl border border-brand/30 bg-brand/10 text-brand">
                <ShieldCheck size={28} />
              </div>
              <InputField
                label={t("Authenticator or recovery code")}
                value={totpCode}
                onChange={setTotpCode}
                placeholder="123456"
                autoComplete="one-time-code"
                onKeyDown={e => e.key === 'Enter' && onTotp(totpCode)}
              />
              <div className="flex gap-3 pt-2">
                <button onClick={onReset} className="btn-secondary flex-1 py-3">{t("Cancel")}</button>
                <button onClick={() => onTotp(totpCode)} disabled={!totpCode.trim() || loading} className="btn-primary flex-[2] py-3">
                  {loading ? t("Verifying...") : t("Verify")}
                </button>
              </div>
            </motion.div>
          ) : (
            <motion.div initial={{ opacity: 0, x: 20 }} animate={{ opacity: 1, x: 0 }} className="space-y-6">
              <InputField label={t("Password")} type="password" value={password} onChange={setPassword} placeholder="••••••••" onKeyDown={e => e.key === 'Enter' && onSubmit(password)} />
              <div className="flex gap-3 pt-2">
                <button onClick={onReset} className="btn-secondary flex-1 py-3">{t("Back")}</button>
                <button onClick={() => onSubmit(password)} disabled={!password || loading} className="btn-primary flex-[2] py-3">
                  {loading ? t("Signing in...") : t("Sign In")}
                </button>
              </div>
            </motion.div>
          )}
        </div>
      </div>
    </motion.div>
  );
}

function QuestionNode({ index, question, onChange, onRemove, t }) {
  const handleChange = (f, v) => onChange({...question, [f]: v});
  return (
    <motion.div 
      layout
      initial={{ opacity: 0, scale: 0.98 }} 
      animate={{ opacity: 1, scale: 1 }} 
      exit={{ opacity: 0, height: 0, margin: 0, overflow: 'hidden' }}
      className="card p-6 border-white/5 bg-surface/30 space-y-6 relative group"
    >
      <div className="absolute inset-0 bg-gradient-to-r from-brand/5 to-transparent opacity-0 group-hover:opacity-100 transition-opacity pointer-events-none" />
      
      <div className="flex items-center justify-between relative z-10">
        <div className="flex items-center gap-3">
          <div className="w-8 h-8 rounded-lg bg-surface-raised border border-border flex items-center justify-center text-xs font-bold text-white shadow-inner">
            {index + 1}
          </div>
          <span className="text-xs font-bold uppercase tracking-widest text-text-muted">{t("Question")}</span>
        </div>
        <button onClick={onRemove} className="text-text-dim hover:text-danger hover:bg-danger/10 p-2 rounded-lg transition-colors">
          <Trash2 size={16} />
        </button>
      </div>

      <div className="relative z-10 space-y-6">
        <TextareaField label={t("Question Prompt")} rows={2} value={question.prompt} onChange={v => handleChange('prompt', v)} placeholder={t("Ask your question here...")} />

        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          {['A', 'B', 'C', 'D'].map((l, i) => (
            <div key={l} className={cn(
              "p-3 rounded-xl border transition-all flex items-center gap-3 focus-within:border-brand/50",
              question.correctIndex === i ? "bg-brand/5 border-brand/40 shadow-[0_0_10px_rgba(99,102,241,0.1)]" : "bg-bg-base border-border hover:border-border-hover"
            )}>
              <label className="flex items-center gap-2 cursor-pointer shrink-0">
                <div className="relative flex items-center justify-center">
                  <input 
                    type="radio" 
                    checked={question.correctIndex === i} 
                    onChange={() => handleChange('correctIndex', i)}
                    className="peer sr-only"
                  />
                  <div className="w-5 h-5 rounded-full border border-border peer-checked:border-brand transition-colors flex items-center justify-center">
                    <div className="w-2.5 h-2.5 rounded-full bg-brand scale-0 peer-checked:scale-100 transition-transform"></div>
                  </div>
                </div>
                <span className={cn("text-[10px] font-bold uppercase w-4", question.correctIndex === i ? "text-brand" : "text-text-muted")}>{l}</span>
              </label>
              <input 
                className="flex-1 bg-transparent border-none p-0 text-sm text-text placeholder:text-text-dim outline-none" 
                value={question[`option${l}`]}
                onChange={e => handleChange(`option${l}`, e.target.value)}
                placeholder={t("Option text...")}
              />
            </div>
          ))}
        </div>

        <InputField label={t("Explanation")} value={question.explanation} onChange={v => handleChange('explanation', v)} placeholder={t("Why is this the correct answer?")} />
      </div>
    </motion.div>
  );
}
