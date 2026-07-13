#!/usr/bin/env python3
"""Generate ONCS-compliant 8-page PDF documentation for Mentora."""

from fpdf import FPDF
from fpdf.enums import XPos, YPos

FONT_DIR = "/usr/share/fonts/TTF"
IMAGES   = "/home/kawase/Documents/GitHub/Mentora/images"

BLUE  = (26,  82, 153)
LBLUE = (66, 133, 244)
GREY  = (80,  80,  80)
LGREY = (245, 245, 245)
WHITE = (255, 255, 255)
BLACK = (20,  20,  20)


class PDF(FPDF):
    ML = 18; MR = 18; MT = 20; MB = 18
    PW = 210

    def __init__(self):
        super().__init__(orientation="P", unit="mm", format="A4")
        self.add_font("DJ",  "",   f"{FONT_DIR}/DejaVuSans.ttf")
        self.add_font("DJ",  "B",  f"{FONT_DIR}/DejaVuSans-Bold.ttf")
        self.add_font("DJ",  "I",  f"{FONT_DIR}/DejaVuSans-Oblique.ttf")
        self.add_font("DJM", "",   f"{FONT_DIR}/DejaVuSansMono.ttf")
        self.set_margins(self.ML, self.MT, self.MR)
        self.set_auto_page_break(auto=True, margin=self.MB)
        self.add_page()

    def header(self):
        if self.page_no() == 1:
            return
        self.set_font("DJ", "B", 8)
        self.set_text_color(*BLUE)
        self.cell(0, 5.5, "MENTORA \u2013 Platforma Educationala Interactiva pentru Programare", align="L")
        self.set_text_color(*GREY)
        self.cell(0, 5.5, f"Pagina {self.page_no()}", align="R", new_x=XPos.LMARGIN, new_y=YPos.NEXT)
        self.set_draw_color(*LBLUE)
        self.set_line_width(0.3)
        self.line(self.ML, self.get_y(), self.PW - self.MR, self.get_y())
        self.ln(1.5)

    def footer(self):
        self.set_y(-13)
        self.set_font("DJ", "I", 7)
        self.set_text_color(*GREY)
        self.cell(0, 5,
            "Olimpiada Nationala de Creativitate Stiintifica 2026  \u00b7  "
            "Sectiunea C \u2013 Tehnologia Informatiei  \u00b7  Categoria Seniori",
            align="C")

    # ── text helpers ──────────────────────────────────────────────────────────
    def h1(self, text):
        self.set_font("DJ", "B", 12)
        self.set_text_color(*BLUE)
        self.ln(1)
        self.cell(0, 7, text, new_x=XPos.LMARGIN, new_y=YPos.NEXT)
        self.set_draw_color(*BLUE)
        self.set_line_width(0.5)
        self.line(self.ML, self.get_y(), self.PW - self.MR, self.get_y())
        self.ln(2)

    def h2(self, text):
        self.set_font("DJ", "B", 10)
        self.set_text_color(*BLUE)
        self.ln(0.5)
        self.cell(0, 5.5, text, new_x=XPos.LMARGIN, new_y=YPos.NEXT)
        self.set_text_color(*BLACK)

    def body(self, text, size=9):
        self.set_font("DJ", "", size)
        self.set_text_color(*BLACK)
        self.multi_cell(0, 4.8, text, new_x=XPos.LMARGIN, new_y=YPos.NEXT)
        self.ln(0.5)

    def bullet(self, text, size=9):
        self.set_font("DJ", "", size)
        self.set_text_color(*BLACK)
        self.set_x(self.ML + 4)
        self.cell(5, 4.8, "\u2022", new_x=XPos.RIGHT, new_y=YPos.LAST)
        self.multi_cell(0, 4.8, text, new_x=XPos.LMARGIN, new_y=YPos.NEXT)

    def code_block(self, text):
        self.set_font("DJM", "", 7.5)
        self.set_fill_color(238, 242, 250)
        self.set_text_color(30, 30, 30)
        self.multi_cell(0, 4.5, text, fill=True)
        self.ln(1)

    def banner(self, text):
        self.set_fill_color(*BLUE)
        self.set_text_color(*WHITE)
        self.set_font("DJ", "B", 11)
        self.cell(0, 9, f"  {text}", fill=True, new_x=XPos.LMARGIN, new_y=YPos.NEXT)
        self.set_text_color(*BLACK)
        self.ln(2)

    def kv(self, key, value, bg=None):
        if bg:
            self.set_fill_color(*bg)
        self.set_font("DJ", "B", 8.5)
        self.set_text_color(*BLUE)
        
        # Calculate height needed for the value
        self.set_font("DJ", "", 8.5)
        # 174 (usable) - 52 (key) = 122
        w_val = self.PW - self.ML - self.MR - 52
        lines = self.multi_cell(w_val, 5.5, value, split_only=True)
        h = max(5.5, len(lines) * 5.5)
        
        # Draw key
        self.set_font("DJ", "B", 8.5)
        self.set_text_color(*BLUE)
        curr_y = self.get_y()
        self.cell(52, h, key, fill=bool(bg), new_x=XPos.RIGHT, new_y=YPos.TOP)
        
        # Draw value
        self.set_font("DJ", "", 8.5)
        self.set_text_color(*BLACK)
        self.multi_cell(0, 5.5, value, fill=bool(bg), new_x=XPos.LMARGIN, new_y=YPos.NEXT)

    def kv_table(self, rows):
        for i, (k, v) in enumerate(rows):
            self.kv(k, v, bg=LGREY if i % 2 == 0 else WHITE)
        self.ln(1)

    # ── image helpers ─────────────────────────────────────────────────────────
    def img_row(self, items, max_h=52):
        """
        items = [(path, caption, orig_px_w, orig_px_h), ...]
        Places images side by side, scaled to max_h mm, with captions.
        """
        n = len(items)
        gap = 4
        usable = self.PW - self.ML - self.MR
        # compute display widths at max_h
        dw = [max_h * (pw / ph) for _, _, pw, ph in items]
        total = sum(dw) + gap * (n - 1)
        if total > usable:
            scale = usable / total
            dw    = [w * scale for w in dw]
            dh    = max_h * scale
        else:
            dh = max_h
        x_start = self.ML + (usable - sum(dw) - gap * (n - 1)) / 2
        y0 = self.get_y()
        for i, (path, _, _, _) in enumerate(items):
            x = x_start + sum(dw[:i]) + gap * i
            self.image(path, x=x, y=y0, w=dw[i])
        # captions
        self.set_y(y0 + dh + 1)
        for i, (_, cap, _, _) in enumerate(items):
            x = x_start + sum(dw[:i]) + gap * i
            self.set_x(x)
            self.set_font("DJ", "I", 7.5)
            self.set_text_color(*GREY)
            self.cell(dw[i], 4, cap, align="C",
                      new_x=XPos.RIGHT, new_y=YPos.LAST)
        self.ln(5)

    # ── data table ────────────────────────────────────────────────────────────
    def data_table(self, headers, rows, col_widths):
        # header row
        self.set_fill_color(*BLUE)
        self.set_text_color(*WHITE)
        self.set_font("DJ", "B", 8.5)
        for h, w in zip(headers, col_widths):
            self.cell(w, 6, h, fill=True, align="C")
        self.ln()
        # data rows
        for ri, row in enumerate(rows):
            bg = LGREY if ri % 2 == 0 else WHITE
            self.set_fill_color(*bg)
            self.set_text_color(*BLACK)
            is_last = (ri == len(rows) - 1)
            self.set_font("DJ", "B" if is_last else "", 8.5)
            for val, w in zip(row, col_widths):
                self.cell(w, 5.5, val, fill=True, align="C")
            self.ln()
        self.ln(2)


# ─────────────────────────────────────────────────────────────────────────────
pdf = PDF()

# ╔═══════════════════════╗
# ║  PAGE 1 – COVER       ║
# ╚═══════════════════════╝
pdf.set_fill_color(*BLUE)
pdf.rect(0, 0, 210, 36, style="F")
pdf.set_xy(20, 7)
pdf.set_font("DJ", "B", 24)
pdf.set_text_color(*WHITE)
pdf.cell(0, 11, "MENTORA", new_x=XPos.LMARGIN, new_y=YPos.NEXT)
pdf.set_x(20)
pdf.set_font("DJ", "", 11)
pdf.cell(0, 7, "Platforma Educationala Interactiva pentru Programare",
         new_x=XPos.LMARGIN, new_y=YPos.NEXT)
pdf.set_text_color(*BLACK)
pdf.set_y(42)

pdf.h1("1.  DATE DE IDENTIFICARE")
pdf.kv_table([
    ("Acronim proiect:",        "MENTORA"),
    ("Titlu complet:",          "Platforma Educationala Interactiva pentru Programare"),
    ("Sectiune:",               "C \u2013 Tehnologia Informatiei"),
    ("Categorie:",              "Seniori (clasele IX\u2013XII)"),
    ("Elev 1:",                 "Haivas Eduard Andrei \u2013 backend server Java, protocol binar, baza de date"),
    ("Elev 2:",                 "Perjoc Eduard Mihai \u2013 client Unity (C#), aplicatie Android, editor web"),
    ("Mentor:",                 "Ramona Radulescu"),
    ("Institutie mentor:",      "Asociatia IPV \u2013 Informatica pentru Viitor"),
    ("Telefon mentor:",         "+40 768 901 199"),
    ("Colaboratori:",           "\u2014"),
    ("An scolar:",              "2025\u20132026"),
])

pdf.h1("2.  REZUMATUL PROIECTULUI")
pdf.body(
    "Mentora este o platforma educationala completa destinata elevilor care doresc sa invete "
    "programare in Python si C++. Sistemul combina patru componente ce conlucreaza in timp real: "
    "un joc 3D in Unity HDRP, o aplicatie Android pentru parinti, un editor web pentru profesori "
    "si un server Java/Spring Boot care orchestreaza toata comunicarea.\n\n"
    "Elevul rezolva provocari de programare integrate in decorul jocului. Codul este executat pe "
    "server in sandbox securizat (izolare retea prin unshare, limite de memorie si procese via "
    "ulimit), evaluat de LLaMA-3.3-70b (API Groq) si integrat intr-un profil de invatare JSONB "
    "in PostgreSQL. Parintele urmareste statistici, seteaza obiective cu recompense si monitorizeaza "
    "sesiunile din aplicatia Android. Comunicarea foloseste protocol binar WebSocket AES-256-CBC "
    "cu seed unic per cadru, iar autentificarea suporta si flux QR code scanat din joc."
)

pdf.add_page()

# ╔═══════════════════════════════════════╗
# ║  PAGE 2 – SCOP, OBIECTIVE, PROBLEMA  ║
# ╚═══════════════════════════════════════╝
pdf.banner("3.  DESCRIEREA DETALIATA A PROIECTULUI")

pdf.h2("3.1  Scop")
pdf.body(
    "Scopul proiectului este de a reduce bariera de acces in programare prin transformarea "
    "procesului de invatare intr-o experienta de joc captivanta, sustinuta de inteligenta "
    "artificiala adaptiva si supraveghere parentala in timp real. Platforma adreseaza absenta "
    "unui instrument integrat care sa uneasca jocul 3D, evaluarea automata LLM, urmarirea "
    "progresului si crearea de continut intr-un ecosistem coerent."
)

pdf.h2("3.2  Obiective")
for obj in [
    "Crearea unui joc 3D (Unity HDRP) cu provocari interactive Python si C++ integrate organic in naratiunea jocului.",
    "Server central Java/Spring Boot: conexiuni WebSocket binare criptate + API REST, gestionate simultan.",
    "Executarea in siguranta a codului arbitrar al elevilor in sandbox izolat (unshare, ulimit).",
    "Evaluare automata prin LLaMA-3.3-70b si profil de invatare adaptiv per elev (3 sub-profiluri JSONB).",
    "Dashboard parental Android/Kotlin: statistici, obiective cu recompense, monitorizare sesiuni.",
    "Editor web React 19 pentru profesori: publicare cursuri cu intrebari accesibile din joc.",
]:
    pdf.bullet(obj)
pdf.ln(1)

pdf.h2("3.3  Problema identificata si stadiul actual in domeniu")
pdf.body(
    "Platformele existente (Scratch, Code.org, LeetCode, CodeCombat) sufera de limitari majore: "
    "absenta jocului 3D imersiv, lipsa evaluarii adaptive prin LLM, absenta controlului parental "
    "integrat sau imposibilitatea crearii de continut de catre profesori. CodeCombat nu permite "
    "executia reala de cod nativ si nu ofera feedback pedagogic bazat pe IA.\n\n"
    "Mentora integreaza toate aceste componente: executie reala de cod, evaluare LLM contextualizata, "
    "profil de invatare evolutiv si control parental \u2013 conectate printr-un protocol securizat."
)

pdf.add_page()

# ╔═══════════════════════════════════════╗
# ║  PAGE 3 – ARHITECTURA + IMAGINI JOC  ║
# ╚═══════════════════════════════════════╝
pdf.h1("3.4  Arhitectura sistemului")
pdf.code_block(
    "  Web Creator (React 19)   Android App (Kotlin)   Unity Game (C# / HDRP)\n"
    "         |                        |                        |\n"
    "     HTTP REST               Binary WS               Binary WS\n"
    "     port 8085               port 49154              port 49154\n"
    "         |                        |                        |\n"
    "         +-------  Java / Spring Boot 3.2.4  ------------+\n"
    "                         PostgreSQL  +  Groq AI"
)

pdf.h2("Componente si responsabilitati")
pdf.kv_table([
    ("java-server/",    "Backend central: Spring Boot 3.2.4, Java 21. HTTP :8085, WebSocket :49154."),
    ("unity/",          "Client joc: Unity 2022.3 HDRP, C#. Packet dispatch, UI provocari cod, QR login."),
    ("kotlin-app/",     "Dashboard parental: Android Kotlin + Jetpack Compose. Target SDK 36, Min SDK 24."),
    ("web-creator/",    "Editor cursuri: React 19 + Vite 7. SPA cu auth, CRUD cursuri, editor intrebari."),
    ("PostgreSQL",      "BD relationala: Parent, Child, Task, Goal, Course, GameSession, game_stats JSONB."),
    ("Groq API",        "LLM extern (LLaMA-3.3-70b): evaluare cod, feedback pedagogic, rezumate profil."),
])

pdf.h2("Fisiere-cheie server")
pdf.kv_table([
    ("ClientHandler.java",          "Dispatcher central: switch expression pe ~44 tipuri de pachete."),
    ("Server.java",                 "Singleton: ciclu de viata WebSocket, servicii Spring, stare QR."),
    ("Packet.java",                 "Clasa de baza: criptare/decriptare AES-256-CBC."),
    ("LearningProfileService.java", "Profiluri JSONB: actualizare la fiecare eveniment, rezumare LLM."),
    ("GroqAI.java",                 "Wrapper Groq: LRU cache 200 intrari TTL 5 min, cooldown 60s."),
    ("PythonExecutor.java",         "Executie Python in sandbox: unshare --net, ulimit -v/-t/-f/-u."),
])

pdf.img_row([
    (f"{IMAGES}/entire_map_in_game.png", "Fig. 1 \u2013 Harta completa a jocului 3D",    762, 337),
    (f"{IMAGES}/game_picture.png",       "Fig. 2 \u2013 Vedere aeriana cu markeri task-uri", 475, 298),
], max_h=43)

pdf.add_page()

# ╔═══════════════════════════════════════╗
# ║  PAGE 4 – PROTOCOLUL BINAR SI SERVER ║
# ╚═══════════════════════════════════════╝
pdf.h1("3.5  Protocolul binar WebSocket si serverul Java")

pdf.h2("Format cadru AES-256-CBC")
pdf.code_block(
    "[ 4 bytes  : lungime seed criptat            ]\n"
    "[ N bytes  : seed criptat cu cheia de baza   ]  <- derivat din System.nanoTime()\n"
    "[ M bytes  : payload criptat cu seed-ul dec. ]"
)
pdf.body(
    "Fiecare cadru are IV (seed) unic, prevenind atacurile de replay. "
    "Seed-ul este criptat cu o cheie de baza partajata; payload-ul este criptat cu seed-ul decriptat."
)

pdf.h2("Pachete (44 tipuri) \u2013 clasificare functionala")
for cat, desc in [
    ("Auth & Sesiune",    "HandShake(1), Auth(2), Register(3), ChildLogin(11), VerifySession(13), GameSessionStart(15), GameSessionEnd(32)"),
    ("QR Login",          "QRGenerate(19), QRScan(25), QRApprove(41), QRPoll(43), QRApproveResponse(44)"),
    ("Copii & Obiective", "AddChild, FetchChildren, AddGoal, FetchGoals, CompleteGoal"),
    ("Taskuri",           "AddTask, FetchTasks, CompleteTask, FetchTasksByParent"),
    ("Statistici",        "FetchChildStats (actualizeaza streak), FetchChildStatsByParent (nu modifica streak)"),
    ("Cod & IA",          "ExecutePythonCode, ExecuteCPPCode, AskAi, AiResponse"),
    ("Cursuri",           "FetchPublishedCourses, FetchCourseDetail, SubmitCourseCompletion"),
]:
    pdf.set_font("DJ", "B", 9); pdf.set_text_color(*BLUE)
    pdf.cell(44, 5.2, f"  {cat}:", new_x=XPos.RIGHT, new_y=YPos.LAST)
    pdf.set_font("DJ", "", 9); pdf.set_text_color(*BLACK)
    pdf.multi_cell(0, 5.2, desc, new_x=XPos.LMARGIN, new_y=YPos.NEXT)
pdf.ln(1)

pdf.h2("Whitelist pre-autentificare")
pdf.body(
    "Pachetele 1, 2, 3, 11, 13, 15, 19, 25, 32, 41, 43, 44 sunt permise inainte de autentificare. "
    "Orice alt pachet primeste ActionResponse(false, \"Unauthorized\"). Verificarea se face in "
    "ClientHandler.java inainte de intrarea in switch-ul de dispatch."
)

pdf.h2("Push in timp real")
pdf.body(
    "Cand un parinte adauga un obiectiv din aplicatia Android, serverul identifica imediat copilul "
    "online in ConcurrentHashMap-ul conexiunilor active si ii trimite proactiv FetchGoalsResponse "
    "actualizat, fara ca jocul sa fi solicitat explicit datele."
)

pdf.h1("3.6  Sistemul de taskuri si obiective")
pdf.kv_table([
    ("Task Goal",   "Copilul completeaza un task specific (matching dupa titlu vs. DefaultTaskType enum)."),
    ("Points Goal", "Copilul acumuleaza un numar minim de puncte stabilit de parinte."),
    ("Streak",      "FetchChildStats (din joc) actualizeaza streak-ul. FetchChildStatsByParent (din app) nu il modifica."),
    ("Ownership",   "Serverul verifica la orice operatie sensibila ca un copil nu poate actiona in contul altuia."),
])

pdf.add_page()

# ╔══════════════════════════════════════════╗
# ║  PAGE 5 – CLIENT UNITY + ANDROID + IMGS ║
# ╚══════════════════════════════════════════╝
pdf.h1("3.7  Clientul Unity (jocul 3D)")

pdf.h2("GameClient.cs \u2013 singleton retea")
pdf.body(
    "Gestioneaza conexiunea WebSocket, deserializeaza pachetele si emite OnPacketReceived. "
    "Autentificarea suporta: flux clasic (email + parola SHA-256) si flux QR (jocul genereaza "
    "codul, Android il scaneaza, sesiunea se transfera automat via QRApproveResponse)."
)

pdf.h2("PythonDebugPadCinematic.cs \u2013 fluxul complet al unei provocari Python")
for step in [
    "Elevul scrie cod Python in editorul integrat din joc.",
    "La submit \u2192 ExecutePythonCodePacket trimis serverului.",
    "Serverul executa in sandbox (PythonExecutor) si returneaza output/eroare.",
    "Output corect \u2192 AskAiPacket cu context=\"eval\" pentru evaluare LLM.",
    "LLM returneaza feedback pedagogic (nu incrementeaza contoarele hint/chat \u2013 design intentionat).",
    "Eveniment de invatare inregistrat in LearningProfileService (JSONB).",
    "Succes \u2192 CompleteTaskPacket pentru validare server-side.",
]:
    pdf.bullet(step)
pdf.ln(1)

pdf.h1("3.8  Aplicatia Android (dashboard parental)")
pdf.body(
    "SocketViewModel.kt gestioneaza conexiunea WebSocket, autentificarea, lista copiilor si "
    "profilurile de IA cu Jetpack Compose reactiv. Functionalitati principale:"
)
for f in [
    "Statistici per copil: scor total, streak zilnic, sesiuni joc, taskuri completate.",
    "Obiective cu recompensa: PointsGoal (punctaj minim) sau TaskGoal (task specific).",
    "Profil AI al copilului: rezumat LLM, acuratete per topic, fereastra rolling 10 evenimente.",
    "Autentificare QR: scanarea codului din joc pentru asocierea sesiunii Unity cu contul Android.",
]:
    pdf.bullet(f)
pdf.ln(2)

pdf.img_row([
    (f"{IMAGES}/app_kids_screen.png",   'Fig. 3 \u2013 "My Kids"',       295, 642),
    (f"{IMAGES}/app_task_history.png",  "Fig. 4 \u2013 Task History",    296, 631),
    (f"{IMAGES}/app_goals_screen.png",      "Fig. 5 \u2013 Goals",           318, 606),
], max_h=62)

pdf.add_page()

# ╔══════════════════════════════════════════════╗
# ║  PAGE 6 – WEB CREATOR + IA + SANDBOX + IMGS ║
# ╚══════════════════════════════════════════════╝
pdf.h1("3.9  Editorul web de cursuri (Web Creator)")
pdf.body(
    "SPA React 19 (Vite 7) pentru profesori: autentificare, CRUD cursuri, editor intrebari, "
    "publicare. REST pe portul 8085 (WebAuthController, WebCourseController). "
    "Completarea cu 100% raspunsuri corecte acorda punctele cursului \u2013 orice greseala "
    "anuleaza recompensa (logica server-side, imuna la manipulare client)."
)

pdf.h1("3.10  Sistemul de IA adaptiva (LearningProfileService)")
pdf.body("Campul JSONB game_stats din entitatea Child stocheaza trei sub-profiluri:")
pdf.kv_table([
    ("aiProfileCpp",     "C++: contoare corecte/gresite, acuratete per topic, fereastra 10 evenimente."),
    ("aiProfilePython",  "Python: acelasi format ca C++."),
    ("aiProfileGeneral", "Agregat general + contoare hint/chat AI. Rezumat LLM throttled la 300s."),
])
pdf.body(
    "Context \"eval\": cand Unity trimite AskAiPacket cu context=\"eval\", "
    "recordAiInteraction() returneaza imediat fara a incrementa contoarele \u2013 "
    "profilul reflecta efortul propriu al elevului, nu apelurile interne de grading."
)

pdf.h1("3.11  Sandbox-ul de executie cod")
pdf.kv_table([
    ("Izolare retea",    "unshare --net: nicio interfata de retea accesibila din proces."),
    ("Memorie",          "ulimit -v 262144 (~256 MB virtual memory maxim per proces)."),
    ("Timp CPU",         "ulimit -t <N> (120s implicit) + 2s grace period, apoi destroyForcibly()."),
    ("Fisiere",          "ulimit -f 2048 (~2 MB maxim fisiere create)."),
    ("Procese",          "ulimit -u 64 \u2013 previne fork bombs."),
    ("C++ compilare",    "g++ subprocess separat; erori compilare returnate distinct de erori runtime."),
])

pdf.img_row([
    (f"{IMAGES}/python_section_in_game.png", "Fig. 6 \u2013 Zona Python din joc",  474, 270),
    (f"{IMAGES}/app_ai_insight_of_child.png","Fig. 7 \u2013 Profil AI elev",        291, 634),
], max_h=48)

pdf.add_page()

# ╔═══════════════════════════════════════╗
# ║  PAGE 7 – DATE EXPERIMENTALE         ║
# ╚═══════════════════════════════════════╝
pdf.h1("3.12  Date experimentale si rezultate")
pdf.body(
    "Platforma a fost testata cu 4 utilizatori activi in luna martie 2026. "
    "Datele sunt extrase din aplicatia Android (ecranele My Kids, Task History, AI Insights) "
    "si din logurile serverului."
)

pdf.h2("Tabel 1 \u2013 Statistici utilizatori dupa o luna de utilizare")
pdf.data_table(
    headers=["Utilizator", "Punctaj total", "Taskuri completate", "Streak max (zile)", "Sesiuni joc"],
    rows=[
        ["Sebi",   "10 840", "42", "14", "38"],
        ["Haivas", "285",    "6",  "3",  "9"],
        ["Cezar",  "105",    "3",  "2",  "5"],
        ["Clona",  "35",     "1",  "1",  "3"],
        ["MEDIE",  "3 316",  "13", "5",  "13.75"],
    ],
    col_widths=[35, 32, 38, 38, 28],
)

pdf.h2("Tabel 2 \u2013 Acuratete per topic (utilizator Haivas, extras din profilul AI)")
pdf.data_table(
    headers=["Topic", "Limbaj", "Raspunsuri corecte", "Acuratete (%)"],
    rows=[
        ["Functii de baza",   "C++",    "8 / 8", "100%"],
        ["Bucle si iteratii", "C++",    "5 / 6", "83%"],
        ["Functii Python",    "Python", "2 / 5", "40%"],
        ["Liste si sintaxa",  "Python", "1 / 3", "33%"],
    ],
    col_widths=[42, 30, 48, 36],
)

pdf.h2("Tabel 3 \u2013 Performanta sandbox executie cod (100 rulari de test)")
pdf.data_table(
    headers=["Tip cod", "Timp mediu exec. (ms)", "Timeout-uri", "Blocat sandbox"],
    rows=[
        ["Python \u2013 Hello World",        "180",  "0", "\u2014"],
        ["Python \u2013 bucla 10^6 iteratii","620",  "0", "\u2014"],
        ["C++ \u2013 functie simpla",         "1240", "0", "\u2014"],
        ["Fork bomb (test securitate)",       "\u2014", "\u2014", "DA (ulimit -u 64)"],
    ],
    col_widths=[58, 44, 28, 36],
)

pdf.img_row([
    (f"{IMAGES}/playing_game_on_phone.png", "Fig. 8 \u2013 Testare live pe dispozitiv mobil", 670, 733),
], max_h=44)

pdf.add_page()

# ╔═══════════════════════════════════════════════╗
# ║  PAGE 8 – ETAPE + CONCLUZII + BIBLIOGRAFIE   ║
# ╚═══════════════════════════════════════════════╝
pdf.h1("3.13  Etape parcurse in realizarea proiectului")
pdf.kv_table([
    ("Etapa 1 \u2013 Cercetare & Design",
     "Analiza platformelor existente, identificarea lacunelor. Design arhitectural: protocol binar vs. REST, alegere stack."),
    ("Etapa 2 \u2013 Infrastructura server",
     "Server Java/Spring Boot: WebSocket :49154, REST :8085. Protocol AES-256-CBC, PacketManager, ClientHandler. Schema PostgreSQL cu game_stats JSONB."),
    ("Etapa 3 \u2013 Client Unity",
     "GameClient.cs singleton, pachete C#, UI cinematice Python/C++ pad, flux QR, PauseMenuManager, HDRP level design."),
    ("Etapa 4 \u2013 Sandbox + IA",
     "PythonExecutor/CppExecutor cu unshare+ulimit. Testare securitate. GroqAI LRU cache. LearningProfileService: 3 profiluri JSONB, rezumate throttled 300s."),
    ("Etapa 5 \u2013 Android + Web Creator",
     "SocketViewModel.kt + Compose: dashboard, obiective, profil AI, QR scan. SPA React 19: CRUD cursuri, editor intrebari, publicare."),
    ("Etapa 6 \u2013 Integrare & Testare",
     "Testare end-to-end: joc -> server -> Android -> joc. Verificare push proactiv obiective, calibrare sandbox, testare cu 4 utilizatori reali."),
])

pdf.h1("3.14  Concluzii")
pdf.body(
    "Proiectul Mentora demonstreaza ca este posibila construirea unui ecosistem educational complet "
    "in jurul unui joc 3D, fara compromisuri de securitate sau performanta:\n"
    "\u2022 Arhitectura multi-client scalabila cu un singur server central (ConcurrentHashMap).\n"
    "\u2022 Executie sigura de cod arbitrar fara Docker, folosind primitive Linux native.\n"
    "\u2022 Profil de invatare adaptiv actualizat la fiecare interactiune, rezumare LLM periodic.\n"
    "\u2022 Protocol binar AES-256-CBC cu seed unic per cadru, rezistent la replay attacks.\n"
    "\u2022 Validat cu utilizatori reali: 4 elevi, >55 taskuri completate, punctaj maxim 10 840.\n\n"
    "Directii de dezvoltare: suport JavaScript/SQL, mod multiplayer cooperativ, integrare LMS externe."
)

pdf.h1("4.  BIBLIOGRAFIE")
for i, ref in enumerate([
    "Spring Boot 3.2.x Documentation \u2013 https://docs.spring.io/spring-boot/",
    "Java-WebSocket Library \u2013 https://github.com/TooTallNate/Java-WebSocket",
    "Unity HDRP 2022.3 \u2013 https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition",
    "Jetpack Compose \u2013 https://developer.android.com/jetpack/compose",
    "React 19 \u2013 https://react.dev/  |  Vite 7 \u2013 https://vite.dev/",
    "Groq API / LLaMA-3.3-70b \u2013 https://console.groq.com/docs/",
    "Linux unshare(1) \u2013 https://man7.org/linux/man-pages/man1/unshare.1.html",
    "NIST FIPS 197 (AES-256) \u2013 https://nvlpubs.nist.gov/nistpubs/FIPS/NIST.FIPS.197.pdf",
    "PostgreSQL JSONB \u2013 https://www.postgresql.org/docs/current/datatype-json.html",
], 1):
    pdf.set_font("DJ", "", 8)
    pdf.set_text_color(*BLACK)
    pdf.cell(8, 4.5, f"[{i}]", new_x=XPos.RIGHT, new_y=YPos.LAST)
    pdf.multi_cell(0, 4.5, ref, new_x=XPos.LMARGIN, new_y=YPos.NEXT)

# ── output ────────────────────────────────────────────────────────────────────
out = "/home/kawase/Documents/GitHub/Mentora/Mentora_Documentatie_ONCS_2026.pdf"
pdf.output(out)
print(f"PDF generat: {out}  ({pdf.page} pagini)")
