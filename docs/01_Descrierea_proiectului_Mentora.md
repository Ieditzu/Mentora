MENTORA – DESCRIEREA PROIECTULUI

1. DATE GENERALE

Mentora este un ecosistem educațional interactiv destinat învățării programării. Platforma combină o experiență de joc 3D realizată în Unity cu o aplicație Android pentru părinți, un editor web pentru cursuri și un server central care gestionează conturile, conținutul, progresul și serviciile de inteligență artificială.

Scopul proiectului este să transforme învățarea programării într-o activitate practică, vizibilă și motivantă. Utilizatorul nu parcurge doar lecții teoretice, ci scrie și execută cod, rezolvă provocări, primește explicații pentru greșeli și își construiește în timp un profil de învățare.

2. PROBLEMA ABORDATĂ ȘI SOLUȚIA PROPUSĂ

Într-un mediu educațional obișnuit, elevul primește deseori exerciții identice, iar feedbackul este limitat la corect sau greșit. Mentora urmărește să ofere un parcurs mai personalizat:

- elevul explorează o lume 3D cu insule educaționale, coding pads, portaluri și experiențe interactive;
- elevul parcurge provocări Python și C++, quiz-uri, cursuri comunitare, exerciții vizuale și misiuni CodeWorld;
- codul este rulat efectiv, iar rezultatul este evaluat;
- un mentor bazat pe inteligență artificială poate oferi indicii și explicații;
- greșelile și reușitele sunt memorate într-un profil individual;
- părintele poate urmări progresul, poate defini obiective și poate trimite provocări;
- profesorul sau creatorul de conținut poate publica propriile cursuri și quiz-uri;
- activitățile pot fi desfășurate și colaborativ, în sesiuni multiplayer locale.

Astfel, Mentora leagă învățarea, evaluarea, feedbackul și implicarea familiei într-un singur sistem.

3. UTILIZATORI ȘI COMPONENTE

Elevul

Elevul folosește jocul Unity pentru a explora insulele, a intra în coding pads, a rezolva sarcini și a participa la quiz-uri. Are acces la Code Quest Island, cu portaluri pentru dificultăți diferite, provocări AI și sandbox; la Quiz Island, cu întrebări și scor calculat inclusiv după timpul de răspuns; la Community Island, de unde poate încărca și parcurge cursurile publicate de creatori; și la CodeWorld, în care comenzile scrise modifică elemente ale lumii. Jocul include și provocări Python și C++, exerciții vizuale, experimentul interactiv Rocket Landing și companionul educațional Rudolf.

Părintele

Aplicația Android oferă autentificare, conectarea copilului prin cod QR, vizualizarea copiilor și a stării lor online, istoricul activităților, obiective, statistici, profiluri AI, rapoarte săptămânale și monitorizarea unei sesiuni live.

Profesorul sau creatorul de cursuri

Interfața web permite autentificarea și administrarea cursurilor. Un curs poate conține titlu, acronim, limbaj, nivel de dificultate, descriere, recompensă și întrebări cu patru variante, răspuns corect și explicație. Cursurile publicate sunt disponibile în joc prin Community Island.

4. FUNCȚIONALITĂȚI PRINCIPALE

Învățare prin practică

Mentora include mai multe moduri de învățare prin practică:

- **Coding pads Python și C++** pentru rezolvarea de sarcini cu operații, condiții, bucle, funcții, verificări și explicații;
- **Code Quest Island**, cu portaluri pentru provocări ușoare, medii, dificile, adaptate profilului AI și sandbox liber;
- **Quiz Island**, cu quiz-uri individuale sau multiplayer, feedback la răspuns și scor influențat de rapiditatea răspunsului;
- **Community Island**, unde elevul alege cursuri publicate din Creator-ul Web, răspunde la întrebări și primește recompensa cursului;
- **CodeWorld**, spațiu în care codul Python controlează obiecte, poziții, culori și comportamente din lumea 3D, inclusiv colaborativ în LAN;
- **Rocket Landing**, un experiment de programare și simulare în care elevul configurează și controlează o rachetă pentru aterizare;
- **AI Challenge**, care generează activități adaptate progresului și oferă dialog de mentorat.

Codul elevului este trimis serverului, executat într-un mediu controlat, iar rezultatul este transmis înapoi în joc.

Feedback și evaluare

Rezolvarea unei sarcini produce feedback imediat. În funcție de context, sistemul poate afișa rezultatul execuției, o explicație AI, indicii, evaluarea răspunsului sau motivul pentru care o variantă de quiz este corectă ori incorectă. Activitățile sunt reflectate în istoricul copilului și în profilul său de învățare.

Personalizare prin inteligență artificială

Serverul menține profiluri separate pentru Python, C++ și zona generală. Sunt urmărite, printre altele, numărul de interacțiuni, răspunsurile corecte și greșite, indiciile folosite, conceptele dificile și greșelile frecvente. Aceste informații sunt folosite pentru conversații de mentorat, provocări generate AI, companionul din joc și rapoarte pentru părinte.

Cursuri gestionate din program

Cursurile nu sunt limitate la un set fix inclus în joc. Editorul web permite creare, citire, modificare și ștergere, iar serverul validează câmpurile și verifică proprietarul cursului. Elevul poate parcurge cursurile publicate și poate primi puncte pentru finalizare.

Obiective și implicarea părintelui

Părintele poate defini obiective bazate pe puncte sau pe finalizarea unei sarcini. De asemenea, poate trimite o provocare personalizată către o sesiune de joc activă și poate primi actualizarea finalizării acesteia.

Colaborare și interactivitate

Modul multiplayer LAN permite descoperirea sesiunilor, conectarea la un host, sincronizarea avatarurilor, quiz-uri comune, voce, profiluri de programare și editarea colaborativă în CodeWorld. Scorul quiz-ului ține cont și de timpul de răspuns.

Companionul educațional Rudolf

Rudolf este companionul educațional al elevului și îl însoțește în explorarea lumii Mentora. El reacționează la intrarea în zonele de programare, la accesarea insulelor și la rezultatele activităților. Prin ghidaj contextual, Rudolf poate conduce elevul către zona Python, zona C++, Code Quest Island, Quiz Island, Community Island și CodeWorld. Astfel, elevul primește orientare clară către activitatea potrivită, fie că dorește să rezolve o sarcină de cod, să participe la un quiz, să descopere un curs comunitar sau să controleze obiecte din lume prin programare.

Rudolf comunică prin mesaje afișate în joc și prin voce. Sistemul include moduri configurabile pentru voce, microfon și modelul vocal, integrare TTS și legătură cu serviciile AI. Companionul are acces contextual la profilul de învățare al copilului: progresul la Python și C++, numărul de răspunsuri corecte, conceptele exersate, indiciile utilizate, punctele acumulate, obiectivele active și provocările finalizate. Pe baza acestor informații, Rudolf poate formula explicații pe înțelesul elevului, poate aprecia evoluția lui, poate propune activitatea următoare și poate oferi sprijin personalizat atunci când copilul întâmpină dificultăți.

În modul multiplayer, Rudolf funcționează împreună cu sesiunile de joc și cu activitățile colaborative. Comportamentul său este susținut de componentele `RobotCompanion`, `RobotVoiceBridge`, `RobotLookAt` și `RudolfIslandGuideTarget`, care gestionează reacțiile, vocea, orientarea vizuală și ghidarea către insule.

5. ELEMENTE DE ORIGINALITATE

Originalitatea soluției rezultă din combinarea unor mecanisme care, de regulă, sunt separate:

1. un joc 3D în care învățarea programării este legată de explorare și provocări;
2. un profil de învățare persistent, folosit pentru feedback și conținut personalizat;
3. un circuit elev–părinte–creator de conținut, cu date comune gestionate de server;
4. CodeWorld, unde programarea poate controla obiecte și poate fi sincronizată între mai mulți jucători;
5. provocări AI, companion conversațional și rapoarte educaționale în același produs.

6. ACCESIBILITATE, INTERFAȚĂ ȘI PORTABILITATE

Fiecare tip de utilizator are o interfață adaptată: joc 3D pentru elev, dashboard Compose pentru părinte și aplicație web pentru creator. Aplicația Android include suport pentru ecrane tactile și scanare QR, iar jocul conține controale mobile și componente pentru VR. Interfețele web și Android folosesc layout-uri responsive și componente reutilizabile.

Proiectul este împărțit pe platforme pentru a putea fi folosit pe dispozitive diferite: Windows și Linux pentru server și dezvoltare, Windows/Linux pentru jocul desktop, Android și iOS pentru aplicația mobilă parentală, browser modern pentru editorul web și dispozitive compatibile Unity/OpenXR pentru jocul VR.

Internaționalizare

Jocul Unity, aplicația Android pentru părinți și Creator-ul Web oferă interfață în română, engleză, franceză și germană. Aplicația Android include și alegerea limbii sistemului, precum și alte limbi disponibile în selector. În joc, selectorul persistent „Language” se află în Settings; în aplicația Android, limba se alege din Settings; în Creator-ul Web, butonul de limbă este disponibil atât înainte, cât și după autentificare. Alegerea se memorează local și este aplicată imediat textelor de interfață. Sunt localizate meniul de pauză și Settings, Multiplayer și Quiz Island, Community Island, CodeWorld, provocările Python și C++, quiz-ul C++, AI Challenge, notificările de provocări de la părinte, autentificarea și dashboardul Android, precum și autentificarea, dashboardul, biblioteca, formularul de curs și editorul de întrebări din Creator-ul Web. Conținutul educațional creat de utilizatori este păstrat fidel în limba sa originală.

Sistemul centralizat de localizare permite adăugarea ulterioară a altor limbi prin completarea catalogului de traduceri, fără rescrierea fluxurilor de interfață.

7. BENEFICII EDUCAȚIONALE

Mentora dezvoltă rezolvarea de probleme, gândirea algoritmică, capacitatea de a interpreta erori și perseverența. Elevul este un participant activ: formulează soluții, testează ipoteze, observă rezultatul, corectează codul și își verifică progresul. Părintele primește o imagine mai clară asupra procesului de învățare, nu doar asupra notei finale.

8. STADIUL PROIECTULUI

Codul sursă include componente funcționale pentru server, joc, aplicația Android și editorul web, precum și integrarea cu PostgreSQL și Groq. Arhitectura modulară și mecanismele de configurare permit rularea, demonstrarea și distribuirea proiectului pe platformele vizate.

9. CONCLUZIE

Mentora este o platformă educațională complexă, cu o idee clară și o implementare multi-platformă. Ea combină jocul, programarea practică, evaluarea automată, inteligența artificială, analiza progresului și colaborarea într-o experiență coerentă. Prin editorul de cursuri și profilul individual de învățare, conținutul poate evolua odată cu utilizatorii și poate fi adaptat unor contexte educaționale diferite.
