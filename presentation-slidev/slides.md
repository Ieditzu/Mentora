---
theme: seriph
title: Mentora — platformă educațională interactivă
info: |
  Prezentare Mentora pentru două persoane.
  Acoperă arhitectura, implementarea, interfața, conținutul,
  originalitatea și documentația proiectului.
transition: slide-left
colorSchema: dark
highlighter: shiki
lineNumbers: false
drawings:
  persist: false
canvasWidth: 1280
layout: cover
background: /entire_map_v2.png
htmlAttrs:
  lang: ro
---

<div class="speaker-tag">Persoana 1</div>
<img src="./public/entire_map_v2.png" style="position:absolute;inset:0;width:100%;height:100%;object-fit:cover" />
<div class="cover-overlay" />
<div class="cover-content">
  <div class="kicker">Prezentare proiect · Software educațional</div>
  <h1 class="cover-title">Mentora<span>.</span></h1>
  <p class="cover-subtitle">Ecosistem educațional în care elevul scrie cod real, îl vede în acțiune într-o lume 3D și primește feedback adaptat progresului său.</p>
</div>

<!--
PERSOANA 1: Bună ziua. Vă prezentăm Mentora, un ecosistem educațional pentru învățarea practică a programării. Nu este doar un joc și nu este doar o aplicație: leagă elevul, părintele și creatorul de conținut într-un singur circuit de învățare.
-->

---

<div class="speaker-tag">Persoana 1</div>
<div class="kicker">Problema și soluția</div>

# De la „corect sau greșit” la învățare vizibilă

<div class="grid-2" style="margin-top:1.2rem">
  <div class="card red">
    <span class="icon icon-word">01 / PROBLEMĂ</span>
    <h2>Problema</h2>
    <p>Elevul primește frecvent exerciții identice și feedback minimal. Programarea rămâne abstractă, iar părintele vede prea puțin din procesul real de învățare.</p>
  </div>
  <div class="card cyan">
    <span class="icon icon-word">02 / SOLUȚIE</span>
    <h2>Soluția Mentora</h2>
    <p>Cod real, provocări 3D, evaluare automată, explicații AI, profil persistent, conținut actualizabil și legătura directă părinte–copil–creator.</p>
  </div>
</div>

<div class="flow">
  <div class="flow-step"><span class="num">01</span><strong>Scrie</strong><span>Python, C++ sau AI/ML</span></div>
  <div class="flow-step"><span class="num">02</span><strong>Rulează</strong><span>în container Docker efemer</span></div>
  <div class="flow-step"><span class="num">03</span><strong>Observă</strong><span>efectul în lume</span></div>
  <div class="flow-step"><span class="num">04</span><strong>Înțelege</strong><span>feedback și indicii</span></div>
  <div class="flow-step"><span class="num">05</span><strong>Evoluează</strong><span>profil personalizat</span></div>
</div>

<!--
PERSOANA 1: Ideea centrală este ca elevul să fie activ. El formulează soluția, o testează, vede rezultatul, primește explicații și își poate corecta soluția. Astfel, greșeala devine parte din procesul de învățare.
-->

---

<div class="speaker-tag">Persoana 2</div>
<div class="kicker">Ecosistemul Mentora</div>

# Patru componente, un singur parcurs educațional

<div class="grid-4" style="margin-top:1.15rem">
  <div class="card violet"><span class="icon icon-word">01 / PLAY</span><h3>Joc Unity</h3><p>Explorare 3D, coding pads, quiz-uri, Code Quest, CodeWorld, Rudolf și multiplayer LAN.</p></div>
  <div class="card green"><span class="icon icon-word">02 / TRACK</span><h3>Aplicație parentală</h3><p>QR, progres, obiective, profil AI, raport săptămânal și sesiune live.</p></div>
  <div class="card cyan"><span class="icon icon-word">03 / CREATE</span><h3>Creator Web</h3><p>Crearea, editarea și publicarea cursurilor și quiz-urilor pentru Community Island.</p></div>
  <div class="card orange"><span class="icon icon-word">04 / CONNECT</span><h3>Backend central</h3><p>Autentificare, persistență, AI, evaluare AI/ML și execuție izolată în containere Docker.</p></div>
</div>

<div class="grid-3" style="margin-top:1.2rem">
  <div class="metric"><span class="value">3</span><span class="label">roluri: elev, părinte, creator</span></div>
  <div class="metric" style="border-color:var(--mentora-cyan)"><span class="value">4</span><span class="label">interfețe în română, engleză, franceză, germană</span></div>
  <div class="metric" style="border-color:var(--mentora-green)"><span class="value">500+</span><span class="label">commit-uri Git cumulate în mai multe repository-uri</span></div>
</div>

<!--
PERSOANA 2: Mentora nu funcționează ca aplicații izolate. Un curs creat în web apare în joc, progresul elevului este salvat de server, iar părintele îl poate urmări din mobil. Acesta este circuitul complet al proiectului.
-->

---
layout: image-right
image: /entire_map_v2.png
backgroundSize: cover
class: visual-split
---

<div class="speaker-tag">Persoana 2</div>
<div class="kicker">Conținut interactiv</div>

# Lumea 3D transformă codul în experiență

- **Insule Python și C++** pentru exerciții, debugging și concepte algoritmice.
- **Insulă AI/ML** cu nouă probleme Python, de la pregătirea datelor la bazele LLM.
- **Quiz Island** pentru evaluare individuală sau multiplayer.
- **Community Island** pentru cursurile publicate din Creator-ul Web.
- **Code Quest Island** pentru provocări construite direct în lume.
- **CodeWorld** pentru controlarea obiectelor 3D prin Python.
- **Rocket Landing** pentru experiment virtual și simulare.

<div class="card violet" style="margin-top:.8rem"><strong>Beneficiu educațional:</strong> elevul vede imediat cum o instrucțiune schimbă un obiect, un scor, o simulare sau o parte a lumii 3D.</div>

<!--
PERSOANA 2: Harta este organizată pe activități. Fiecare zonă are un scop educațional clar, iar elevul poate trece natural de la exerciții introductive la provocări, evaluare și lucru creativ.
-->

---

<div class="speaker-tag">Persoana 2</div>
<div class="kicker">Python Island</div>

# Python: de la primele funcții la provocări vizuale

<div class="track-gallery">
  <figure><img src="./public/python_island.png" /><figcaption>Portaluri Easy · Medium · Hard</figcaption></figure>
  <figure><img src="./public/python_section_in_game.png" /><figcaption>Insula integrată în lumea Mentora</figcaption></figure>
  <figure class="main"><img src="./public/python_coding.png" /><figcaption>Editor, cerință, încercări, indiciu AI, rulare și verificare</figcaption></figure>
</div>

<div class="mobile-feature-bar" style="grid-template-columns:repeat(4,1fr);margin-top:.75rem">
  <span>Easy · bazele</span><span>Medium · debugging</span><span>Hard · vizual</span><span>Docker Python</span>
</div>

<div class="card violet" style="min-height:5.2rem;margin-top:.7rem;text-align:center"><strong>Buclă completă:</strong> elevul citește cerința, modifică sursa, rulează codul real, verifică rezultatul și poate cere un indiciu contextual.</div>

<!--
PERSOANA 2: Insula Python are trasee Easy, Medium și Hard. Elevul începe cu funcții și operații de bază, continuă cu debugging și ajunge la probleme vizuale. Editorul păstrează cerința și numărul de încercări, oferă indicii și asistent AI, iar butoanele Run și Verify separă observarea rezultatului de evaluarea finală. Codul este executat în containerul Python izolat.
-->

---

<div class="speaker-tag">Persoana 1</div>
<div class="kicker">C++ Island</div>

# C++: quiz, debugging și compilare cu feedback

<div class="grid-2" style="margin-top:.6rem;align-items:start">
  <figure style="margin:0"><img class="shot" src="./public/cpp.png" style="height:18.2rem" /><figcaption class="image-caption">Provocare Medium: repararea unei funcții C++ direct în editor</figcaption></figure>
  <figure style="margin:0"><img class="shot" src="./public/cppAI.png" style="height:18.2rem" /><figcaption class="image-caption">Asistent AI contextual: ghidează raționamentul fără a livra direct soluția</figcaption></figure>
</div>

<div class="mobile-feature-bar" style="grid-template-columns:repeat(4,1fr);margin-top:.75rem">
  <span>Easy · quiz</span><span>Medium · debugging</span><span>Hard · coding</span><span>Docker C++</span>
</div>

<div class="card cyan" style="min-height:5.2rem;margin-top:.7rem;text-align:center"><strong>Feedback tehnic real:</strong> sursa este compilată și executată, iar elevul vede outputul, erorile, verificarea cerinței și progresul salvat.</div>

<!--
PERSOANA 1: Traseul C++ combină întrebări introductive, debugging la nivel Medium și probleme de programare la nivel Hard. Elevul lucrează cu sursă C++ reală, poate cere un indiciu sau discuta cu asistentul AI, apoi compilează și rulează soluția într-un container separat. Rezultatul și conceptele exersate actualizează profilul C++ al copilului.
-->

---

<div class="speaker-tag">Persoana 1</div>
<div class="kicker">Personalizare pe profil</div>

# Fiecare traseu construiește un profil de învățare separat

<div class="grid-3" style="margin-top:.55rem;align-items:start">
  <figure style="margin:0"><img class="shot" src="./public/pythonAiChallenge.png" style="height:12.8rem" /><figcaption class="image-caption">Python · provocare generată după punctele slabe</figcaption></figure>
  <figure style="margin:0"><img class="shot" src="./public/cppAI.png" style="height:12.8rem" /><figcaption class="image-caption">C++ · ajutor legat de codul și greșeala curentă</figcaption></figure>
  <figure style="margin:0"><img class="shot" src="./public/ml_island_front.png" style="height:12.8rem" /><figcaption class="image-caption">AI/ML + LLM · progres separat pe competențe</figcaption></figure>
</div>

<div class="grid-3" style="margin-top:.75rem">
  <div class="card violet" style="min-height:8.1rem"><h3>`aiProfilePython`</h3><p>Reține concepte, greșeli, indicii și acuratețe; generează schelete de cod care țintesc dificultățile elevului.</p></div>
  <div class="card cyan" style="min-height:8.1rem"><h3>`aiProfileCpp`</h3><p>Alimentează provocări C++ personalizate și explicații contextuale pentru sursa compilată.</p></div>
  <div class="card green" style="min-height:8.1rem"><h3>`aiProfileMachineLearning`</h3><p>Urmărește Data Prep, Regression, Classification, Evaluation, Neural Networks și LLMs.</p></div>
</div>

<div class="card orange" style="min-height:4.6rem;margin-top:.65rem;text-align:center"><strong>Generare adaptivă:</strong> Python, C++ și CodeWorld primesc probleme noi din profil. AI/ML–LLM păstrează evaluarea deterministă a catalogului, dar contribuie la profil, ghidaj și recomandarea următorului pas.</div>

<!--
PERSOANA 1: Personalizarea nu folosește un singur scor general. Serverul păstrează profiluri separate pentru Python, C++ și AI/Machine Learning. Pentru Python și C++, AI-ul primește punctele forte, dificultățile, greșelile și evenimentele recente și generează o cerință cu sursă incompletă, recompensă și nivel potrivit. CodeWorld folosește același mecanism pentru o misiune construită în lumea 3D. Traseul AI/ML și LLM actualizează propriul profil pe șase competențe, dar cele nouă probleme sunt intenționat evaluate determinist pe server.
-->

---
layout: image-right
image: /codeIsland.png
backgroundSize: cover
class: visual-split
---

<div class="speaker-tag">Persoana 2</div>
<div class="kicker">Code Quest Island</div>

# Programare cu obiective verificabile în lumea 3D

<div class="pill-row">
  <span class="pill">Easy · Build</span><span class="pill">Medium · Fix</span><span class="pill">Hard · Systems</span><span class="pill">AI Profile Quest</span><span class="pill">Free Sandbox</span>
</div>

- **Easy:** construirea, poziționarea, scalarea și colorarea unui beacon.
- **Medium:** eliminarea obstacolelor și repararea unui pod cu obiecte `plank_`.
- **Hard:** restaurarea unui `power_core` și construirea elementelor de protecție.
- **AI Profile:** provocare generată după profilul de învățare al elevului.
- **Sandbox:** spațiu liber pentru a crea, muta, redimensiona și colora obiecte cu Python.

<div class="card cyan" style="margin-top:.8rem"><strong>Implementare reală:</strong> `CodeWorldQuestIsland` generează procedural platforma, portalurile, coliziunile și decorul; `CodeWorldQuestPortal` activează modul sau provocarea aleasă.</div>

<!--
PERSOANA 2: Code Quest Island este una dintre cele mai concrete funcții ale proiectului. Nu oferă doar întrebări: fiecare cerință este verificată în lume după nume, poziție, dimensiune, culoare sau număr de obiecte. Insula poate fi generată direct din Pause Menu pentru o sesiune nouă de freestyle.
-->

---
layout: image-right
image: /codeIsland.png
backgroundSize: cover
class: visual-split
---

<div class="speaker-tag">Persoana 1</div>
<div class="kicker">Evaluare și feedback</div>

# CodeWorld: codul devine obiect și rezultat

<div class="grid-2">
  <div class="card violet"><h3>Editor Python în joc</h3><p>Comenzile creează și modifică obiecte, iar scena este actualizată vizual.</p></div>
  <div class="card green"><h3>Checklist de provocare</h3><p>Fiecare obiectiv este confirmat separat: existență, poziție, scară, culoare sau cantitate.</p></div>
  <div class="card cyan"><h3>Feedback imediat</h3><p>Elevul vede ce a fost corect și ce mai trebuie reparat, nu doar un rezultat final.</p></div>
  <div class="card orange"><h3>Colaborare LAN</h3><p>Editorul, comenzile, obiectele și cursorii se sincronizează între jucători.</p></div>
</div>

<blockquote>„Învățarea este activă: formulezi o soluție, o rulezi, observi efectul și o îmbunătățești.”</blockquote>

<!--
PERSOANA 1: CodeWorld răspunde direct criteriului de utilitate și interactivitate. Elevul are libertate în Sandbox, dar și obiective clare în provocări. Feedbackul îl ajută să identifice exact ce trebuie corectat.
-->

---
layout: image-right
image: /ml_island_front.png
backgroundSize: cover
class: visual-split
---

<div class="speaker-tag">Persoana 1</div>
<div class="kicker">AI & Machine Learning Island</div>

# AI/ML în Python: de la date la bazele LLM

<div class="pill-row">
  <span class="pill">3 Easy</span><span class="pill">3 Medium</span><span class="pill">3 Hard</span><span class="pill">20–50 puncte</span>
</div>

- **9 probleme Python:** pregătirea datelor, regresie, metrici, pipeline-uri, clasificare, selecția modelelor și rețele neuronale.
- **Două provocări LLM:** clasificarea intențiilor cu TF-IDF și predicția deterministă a următorului token cu un model bigram.
- **Evaluare reală:** rezultate verificate prin exactitate, MAE sau accuracy pe date ascunse păstrate de server.
- **Execuție științifică izolată:** NumPy, pandas și scikit-learn într-un container Docker fără rețea.

<div class="card cyan" style="margin-top:.7rem"><strong>Progres persistent:</strong> încercările, scorul maxim, finalizarea și recompensa sunt salvate, iar profilul parental include competențe AI/ML și LLM.</div>

<!--
PERSOANA 1: Noua insulă AI și Machine Learning are trei portaluri de dificultate și nouă probleme scrise în Python. Traseul ajunge până la NLP și fundamentele modelelor de limbaj: elevul clasifică intenții și construiește un predictor simplu de următor token. Soluția nu este notată de un LLM, ci determinist, pe server, folosind date ascunse.
-->

---

<div class="speaker-tag">Persoana 2</div>
<div class="kicker">Companion educațional</div>

# Rudolf: ghid personal, contextual și vocal

<div class="rudolf-layout">
  <div class="rudolf-copy">
    <ul>
      <li>Îl conduce pe elev către Python, C++, Code Quest, Quiz, Community sau CodeWorld.</li>
      <li>Reacționează la poziție, activitatea aleasă și rezultatele obținute.</li>
      <li>Folosește dialog în joc, orientare vizuală, voice bridge și text-to-speech.</li>
      <li>Are acces contextual la profil: progres, răspunsuri, indicii, puncte și obiective.</li>
    </ul>
    <div class="card cyan"><strong>Rolul lui Rudolf:</strong> transformă datele despre progres într-o recomandare clară, pe înțelesul copilului.</div>
  </div>
  <div class="rudolf-gallery">
    <figure><img src="./public/RudolfGuide2.png" /><figcaption>Ghidare vizuală către insula potrivită</figcaption></figure>
    <figure><img src="./public/rudolfAnswer.png" /><figcaption>Dialog contextual și răspuns personalizat</figcaption></figure>
  </div>
</div>

<!--
PERSOANA 2: Rudolf este mai mult decât un personaj decorativ. El leagă lumea 3D de profilul elevului: poate oferi ghidaj către activitatea potrivită și explicații adaptate contextului real al copilului.
-->

---

<div class="speaker-tag">Persoana 2</div>
<div class="kicker">Experimente și comunitate</div>

# Quiz, cursuri comunitare și simulare

<div class="grid-3" style="margin-top:1rem">
  <div><img class="shot" src="./public/pauseMenuQuiz.png" /></div>
  <div><img class="shot" src="./public/community.png" /></div>
  <div><img class="shot" src="./public/missile.png" /></div>
</div>

<div class="grid-3" style="margin-top:.85rem">
  <div class="card violet"><h3>Quiz</h3><p>Răspuns corect, explicație, punctaj și timp; disponibil și în multiplayer.</p></div>
  <div class="card cyan"><h3>Conținut actualizabil</h3><p>Un creator publică un curs, iar elevul îl poate parcurge din joc.</p></div>
  <div class="card orange"><h3>Simulare</h3><p>Elevul configurează și controlează racheta, observând consecințele parametrilor.</p></div>
</div>

<!--
PERSOANA 2: Aceste activități arată diversitatea conținutului. Avem evaluare, conținut creat de utilizatori și experiment virtual. În toate cazurile, elevul face o acțiune, vede rezultatul și primește feedback.
-->

---

<div class="speaker-tag">Persoana 1</div>
<div class="kicker">Aplicația parentală · Android și iOS</div>

# Părintele vede progresul și poate interveni constructiv

<div style="display:flex;gap:1.25rem;align-items:center;justify-content:center;margin-top:.55rem">
  <div><img class="phone-shot" src="./public/appMyKids.png" /><div class="image-caption">Android · copii și conectare QR</div></div>
  <div><img class="phone-shot" src="./public/iphoneMyKids.png" /><div class="image-caption">iPhone · obiective, sesiune live și provocări</div></div>
  <div><img class="phone-shot" src="./public/appSkillRadar.png" /><div class="image-caption">Profil AI și radar de competențe</div></div>
</div>

<div class="mobile-feature-bar"><span>Conectare QR</span><span>Profil AI</span><span>Obiective</span><span>Sesiune live</span><span>Rapoarte</span></div>

<!--
PERSOANA 1: Aici se vede explicit suportul pe Android și iOS. Părintele conectează copilul prin QR, consultă istoricul, profilurile AI și rapoartele, urmărește sesiunea live și poate trimite provocări în joc.
-->

---

<div class="speaker-tag">Persoana 1</div>
<div class="kicker">Creator Web</div>

# Conținutul nu este fix: poate fi creat și publicat din platformă

<div class="grid-2" style="align-items:center">
  <div>
    <img class="web-shot" src="./public/web_creator_courses.png" />
    <div class="image-caption">Bibliotecă de cursuri: drafturi și cursuri publicate</div>
  </div>
  <div>
    <img class="web-shot" src="./public/web_creator.png" />
    <div class="image-caption">Editor: metadate, limbaj, dificultate, puncte și întrebări</div>
  </div>
</div>

<div class="grid-3" style="margin-top:.9rem">
  <div class="card cyan"><h3>CRUD complet</h3><p>Creare, citire, editare și ștergere a cursurilor.</p></div>
  <div class="card violet"><h3>Evaluare controlată</h3><p>Patru variante, răspuns corect și explicație pentru fiecare întrebare.</p></div>
  <div class="card green"><h3>Publicare în joc</h3><p>Cursurile ajung în Community Island fără actualizarea jocului.</p></div>
</div>

<!--
PERSOANA 1: Creator-ul Web dovedește că platforma poate fi actualizată și gestionată din program. Un profesor sau creator poate pregăti conținut nou, îl poate publica, iar el ajunge la elev în Community Island.
-->

---

<div class="speaker-tag">Persoana 1</div>
<div class="section-slide">
  <div class="chapter-number">I</div>
  <div class="kicker">Capitolul I · 10 puncte</div>
  <h1>Arhitectura aplicației</h1>
  <p>Tehnologii potrivite, componente separate, comunicație în timp real și portabilitate pe mai multe dispozitive.</p>
</div>

<!--
PERSOANA 1: În continuare prezentăm partea tehnică. Fiecare tehnologie este aleasă pentru o responsabilitate precisă, iar componentele sunt separate astfel încât proiectul să poată evolua fără a rescrie întregul sistem.
-->

---

<div class="speaker-tag">Persoana 1</div>
<div class="kicker">I.1 · Tehnologii și arhitectură</div>

# Arhitectură multi-client cu backend central

```mermaid {scale: 1}
flowchart TB
  U["UNITY / C#\nJoc 3D, CodeWorld, multiplayer"] -->|"WebSocket criptat"| S
  A["KOTLIN + COMPOSE\nAndroid / punte iOS"] -->|"WebSocket criptat"| S
  W["REACT 19 + VITE 7\nCreator Web"] -->|"REST + Bearer"| S
  S["JAVA 21 + SPRING BOOT\nservicii, validare, AI, protocol"] --> D[("POSTGRESQL\nconturi, cursuri, progres, JSONB")]
  S --> G["GROQ / LLAMA\nmentorat și provocări"]
  S --> X["DOCKER RUNNERS EFEMERE\nPython · C++ · CodeWorld · AI/ML"]
  U <-. "TCP sesiune + UDP discovery" .-> L["MULTIPLAYER LAN"]
```

<div class="pill-row" style="margin-top:.8rem"><span class="pill">Unity 2022.3.62f3</span><span class="pill">Java 21 + Spring Boot 3.2</span><span class="pill">PostgreSQL 42.7.10</span><span class="pill">Docker</span><span class="pill">Kotlin + Compose</span><span class="pill">React 19 + Vite 7</span></div>

<!--
PERSOANA 1: Acesta este centrul arhitecturii. Unity și mobil comunică în timp real prin WebSocket, web-ul folosește REST pentru administrare, iar serverul izolează logica de domeniu, datele și AI-ul. Orice cod al elevului rulează într-un container Docker nou, separat de procesul Java.
-->

---

<div class="speaker-tag">Persoana 2</div>
<div class="kicker">I.2 · Proiectare arhitecturală</div>

# Module cu responsabilități clare și extensibile

<div class="grid-4" style="margin-top:1.05rem">
  <div class="card violet"><h3>Backend</h3><p>Entity · Repository · Service · Packet · Web Controller. Separă persistența, domeniul, transportul și API-ul.</p></div>
  <div class="card cyan"><h3>Unity</h3><p>`GameClient`, `CodeWorldRuntime`, `CodeWorldQuestIsland`, `RobotCompanion`, manageri UI și multiplayer.</p></div>
  <div class="card green"><h3>Mobil</h3><p>`SocketViewModel` centralizează starea; Compose redă ecrane reactive bazate pe date.</p></div>
  <div class="card orange"><h3>Web</h3><p>Componente React reutilizabile, `api.js` pentru apeluri și `i18n.js` pentru traduceri.</p></div>
</div>

<div class="grid-3" style="margin-top:1rem">
  <div class="metric"><span class="value">OOP</span><span class="label">încapsulare, compoziție, clase cu scop clar</span></div>
  <div class="metric" style="border-color:var(--mentora-cyan)"><span class="value">Event-driven</span><span class="label">pachete, evenimente de UI și sincronizare</span></div>
  <div class="metric" style="border-color:var(--mentora-green)"><span class="value">Async</span><span class="label">rețea, AI, sesiuni și actualizări reactive</span></div>
</div>

<!--
PERSOANA 2: Am folosit programare orientată pe obiecte, încapsulare, compoziție, programare asincronă și fluxuri reactive. De exemplu, protocolul poate fi extins printr-un nou tip de pachet, fără a modifica toate ecranele sau serviciile.
-->

---

<div class="speaker-tag">Persoana 1</div>
<div class="kicker">I.3 · Portabilitate</div>

# Mentora rulează pe platformele relevante pentru fiecare utilizator

<div class="grid-4 tall-grid" style="margin-top:1rem">
  <div class="card violet"><span class="icon icon-word">01 / DESKTOP</span><h3>Windows + Linux</h3><p>Joc Unity pentru desktop; server Java configurabil pentru mediul de rulare.</p></div>
  <div class="card green"><span class="icon icon-word">02 / MOBILE</span><h3>Android + iOS</h3><p>Joc și aplicație parentală mobilă prin Unity și Kotlin Multiplatform.</p></div>
  <div class="card cyan"><span class="icon icon-word">03 / IMMERSIVE</span><h3>VR + Meta Quest</h3><p>OpenXR, controlere, ray pointer și hand tracking.</p></div>
  <div class="card orange"><span class="icon icon-word">04 / WEB</span><h3>Browser</h3><p>Creator Web modern, cu layout adaptabil la rezoluții diferite.</p></div>
</div>

<div class="card" style="margin-top:1.15rem;text-align:center"><strong>Adaptarea controalelor:</strong> tastatură și mouse · touch · controlere VR · hand tracking.</div>

<!--
PERSOANA 1: Portabilitatea nu este doar declarată. În proiect există setări Unity pentru Android și iPhone, module mobile comune pentru iOS, suport VR și o aplicație web bazată pe standardele browserului.
-->

---

<div class="speaker-tag">Persoana 1</div>
<div class="kicker">I.3 · VR și Meta Quest</div>

# Mentora în VR: explorare și ghidaj cu Rudolf

<div class="grid-2" style="margin-top:.65rem;align-items:start">
  <figure style="margin:0">
    <img class="shot" src="./public/vr.jpeg" style="height:17.2rem" />
    <figcaption class="image-caption">Explorarea lumii Mentora cu un controller VR urmărit în spațiu</figcaption>
  </figure>
  <figure style="margin:0">
    <img class="shot" src="./public/vrRudolf.jpeg" style="height:17.2rem" />
    <figcaption class="image-caption">Dialogul contextual cu Rudolf rămâne disponibil în experiența imersivă</figcaption>
  </figure>
</div>

<div class="mobile-feature-bar" style="grid-template-columns:repeat(4,1fr);margin-top:.75rem">
  <span>OpenXR</span><span>Meta Quest</span><span>Controlere VR</span><span>Hand tracking</span>
</div>

<div class="card cyan" style="min-height:5.4rem;margin-top:.75rem;text-align:center"><strong>Aceeași platformă educațională:</strong> explorare 3D, meniuri cu ray pointer și companion AI, adaptate interacțiunii imersive.</div>

<!--
PERSOANA 1: Acestea sunt capturi reale din versiunea VR. Elevul poate explora lumea Mentora folosind controlere urmărite în spațiu, poate interacționa cu meniurile prin ray pointer și poate discuta cu Rudolf fără să părăsească experiența imersivă. Implementarea folosește OpenXR și include suport pentru Meta Quest și hand tracking.
-->

---

<div class="speaker-tag">Persoana 1</div>
<div class="section-slide">
  <div class="chapter-number">II</div>
  <div class="kicker">Capitolul II · 20 puncte</div>
  <h1>Implementare, testare și securitate</h1>
  <p>Implementarea combină module extensibile, un protocol propriu, execuție izolată de cod și verificări de integrare între toate componentele.</p>
</div>

---

<div class="speaker-tag">Persoana 1</div>
<div class="kicker">II.1 · Eleganța implementării</div>

# Cod extensibil, lizibil și organizat

<div class="grid-2 large-grid">
  <div class="card violet"><h2>Extensibilitate</h2><ul><li>Servicii Spring și repository-uri JPA pentru logica de domeniu.</li><li>Clase Unity separate pentru lume, portaluri, companion, UI și multiplayer.</li><li>Catalog central de traduceri și API web centralizat.</li><li>Protocol extensibil prin clase de pachete și manager de pachete.</li></ul></div>
  <div class="card cyan"><h2>Calitate</h2><ul><li>Nume semnificative: `CodeWorldQuestIsland`, `LearningProfileService`, `SocketViewModel`.</li><li>Metode cu responsabilitate precisă și fluxuri documentate.</li><li>Compoziție și încapsulare pentru reducerea dependențelor.</li><li>Cod consecvent între client, server și aplicațiile conexe.</li></ul></div>
</div>

<div class="card green" style="margin-top:1rem"><strong>Complexitate tehnică:</strong> lume 3D procedurală, containere Docker pentru Python/C++/AI-ML, evaluare ascunsă, AI cu cache și timeout, protocol binar, QR, aplicație mobilă, web și multiplayer LAN.</div>

<!--
PERSOANA 1: Eleganța nu înseamnă cod puțin, ci cod împărțit corect. Fiecare modul are o sarcină clară și poate fi extins: de exemplu, putem adăuga o insulă, un packet sau un ecran fără să schimbăm arhitectura de bază.
-->

---

<div class="speaker-tag">Persoana 2</div>
<div class="kicker">II.5 · Securitate</div>

# Cod real, executat într-un mediu controlat

<div class="grid-2 security-grid">
  <div class="card red"><h2>Protecție la execuție</h2>
<pre><code>docker run --rm
  --network none --read-only
  --cap-drop ALL
  --security-opt no-new-privileges
  --pids-limit 64 --cpus 1
  --memory 256m/512m
  --user 65532:65532</code></pre>
<p>Un container nou pentru fiecare rulare; sursele sunt montate read-only, iar `/tmp` este temporar.</p></div>
  <div class="card cyan"><h2>Protecție la acces</h2><ul><li>Pachete WebSocket criptate AES/CBC cu seed dinamic și validare de lungime.</li><li>Tokenuri Bearer cu expirare pentru Creator-ul Web.</li><li>Verificarea proprietarului cursurilor și a relației părinte–copil.</li><li>Cheile AI și configurațiile sensibile sunt separate de cod.</li></ul></div>
</div>

<!--
PERSOANA 2: Securitatea este importantă deoarece elevul rulează cod. Python, C++, CodeWorld și problemele AI/ML pornesc fiecare într-un container Docker efemer, fără rețea și fără capabilități Linux. Containerul are filesystem read-only, utilizator fără privilegii, CPU, memorie, procese și fișiere limitate; la timeout este eliminat forțat. În plus, accesul la date este verificat prin autentificare și ownership.
-->

---

<div class="speaker-tag">Persoana 1</div>
<div class="kicker">II.2 · II.4 · Testare și maturitate</div>

# Verificare continuă și proiect pregătit pentru demonstrare

<div class="grid-3 test-grid">
  <div class="card violet"><span class="icon icon-word">01 / TEST</span><h3>Testare funcțională</h3><p>Autentificare, QR, soluții corecte și incorecte, evaluare AI/ML, indicii AI, cursuri, quiz, obiective și multiplayer.</p></div>
  <div class="card cyan"><span class="icon icon-word">02 / INTEGRATE</span><h3>Integrare</h3><p>Teste pentru serviciul și pachetele AI/ML, fluxuri server–Unity–mobil–web și builduri verificate înaintea demonstrației.</p></div>
  <div class="card green"><span class="icon icon-word">03 / SHIP</span><h3>Maturitate</h3><p>Fluxuri complete pentru conturi, copii, conținut, AI, execuție de cod, rapoarte, multiplayer și localizare.</p></div>
</div>

<div class="grid-2" style="margin-top:1rem">
  <div class="card orange"><h3>Git</h3><p>Mai multe repository-uri, cu peste <strong>500 de commit-uri cumulate</strong>, colaborare pe componente și revenire controlată la versiuni anterioare.</p></div>
  <div class="card"><h3>Distribuire</h3><p>Configurații separate pe medii, instrucțiuni pentru backend, web, Android/iOS și Unity, plus cerințe de sistem documentate.</p></div>
</div>

<!--
PERSOANA 1: Testarea urmărește traseul complet al utilizatorului și integrarea dintre componente. Proiectul este matur deoarece nu avem doar prototipuri de ecrane: avem fluxuri complete de la autentificare până la progres și raportare.
-->

---

<div class="speaker-tag">Persoana 2</div>
<div class="section-slide">
  <div class="chapter-number">III + IV</div>
  <div class="kicker">Capitolele III și IV · 50 puncte</div>
  <h1>Interfață, conținut, evaluare și feedback</h1>
  <p>Mentora combină o experiență vizuală accesibilă cu activități care cer elevului să experimenteze, să verifice și să corecteze.</p>
</div>

---

<div class="speaker-tag">Persoana 2</div>
<div class="kicker">III.1 · III.2 · Interfață</div>

# Interfață intuitivă, adaptabilă și multilingvă

<div class="grid-2" style="align-items:center">
  <div>
    <img class="web-shot" src="./public/web_creator-fr.png" />
    <div class="image-caption">Creator Web localizat în franceză</div>
  </div>
  <div class="card cyan">
    <h2>Patru limbi în întregul ecosistem</h2>
    <div class="pill-row"><span class="pill">Română</span><span class="pill">English</span><span class="pill">Français</span><span class="pill">Deutsch</span></div>
    <ul><li>Unity actualizează textele fără repornire și salvează preferința în `PlayerPrefs`.</li><li>Web-ul păstrează limba în `localStorage`.</li><li>Aplicația mobilă permite limba sistemului sau o limbă aleasă explicit.</li><li>Layouturi responsive pentru browser, telefon, desktop și VR.</li></ul>
  </div>
</div>

<div class="card violet" style="margin-top:.9rem"><strong>Ușurință în folosire:</strong> navigare pe pași, etichete clare, feedback vizual, butoane adaptate controlului ales și Rudolf pentru ghidaj contextual.</div>

<!--
PERSOANA 2: Interfața este adaptată rolului utilizatorului: joc pentru elev, dashboard pentru părinte, editor pentru creator. Alegerea limbii este persistentă și este disponibilă în română, engleză, franceză și germană.
-->

---

<div class="speaker-tag">Persoana 2</div>
<div class="kicker">IV.1–IV.4 · Conținut</div>

# Conținut care implică, evaluează și se actualizează

<div class="grid-4 content-grid" style="margin-top:1rem">
  <div class="card violet"><span class="icon icon-word">01 / INTERACT</span><h3>Interactivitate</h3><p>Exerciții Python/C++, AI/ML și LLM, lumi modificabile prin cod, portaluri, quiz-uri, multiplayer și simulare.</p></div>
  <div class="card cyan"><span class="icon icon-word">02 / EVALUATE</span><h3>Evaluare</h3><p>Teste ascunse, exactitate, MAE, accuracy, checklist, scor, explicație, indicii AI și istoric persistent.</p></div>
  <div class="card green"><span class="icon icon-word">03 / MANAGE</span><h3>Gestionare</h3><p>Creator-ul poate crea, edita, publica și șterge cursuri și întrebări din aplicație.</p></div>
  <div class="card orange"><span class="icon icon-word">04 / VERIFY</span><h3>Corectitudine</h3><p>Concepte de programare verificabile, răspunsuri corecte definite și explicații asociate fiecărei întrebări.</p></div>
</div>

<div class="flow" style="margin-top:1rem">
  <div class="flow-step"><span class="num">ELEV</span><strong>Rezolvă</strong><span>cod sau quiz</span></div>
  <div class="flow-step"><span class="num">SISTEM</span><strong>Verifică</strong><span>cerințele</span></div>
  <div class="flow-step"><span class="num">AI</span><strong>Explică</strong><span>și oferă indiciu</span></div>
  <div class="flow-step"><span class="num">PROFIL</span><strong>Reține</strong><span>progresul</span></div>
  <div class="flow-step"><span class="num">PĂRINTE</span><strong>Urmărește</strong><span>evoluția</span></div>
</div>

<!--
PERSOANA 2: Aici sunt acoperite funcționalitatea, utilitatea, evaluarea, actualizarea conținutului și corectitudinea. Elevul este activ, iar sistemul îi explică nu doar dacă a greșit, ci și ce poate îmbunătăți.
-->

---

<div class="speaker-tag">Persoana 1</div>
<div class="kicker">V.1 · Originalitate și inovație</div>

# Ce face Mentora diferită?

<div class="grid-2 innovation-grid" style="margin-top:1rem">
  <div class="card violet"><h2>În joc</h2><p>Programarea produce schimbări vizibile într-o lume 3D, de la obiecte create prin cod până la misiuni verificabile procedural.</p></div>
  <div class="card cyan"><h2>În profil</h2><p>AI-ul, Rudolf și rapoartele folosesc un profil persistent, nu un răspuns generic de moment.</p></div>
  <div class="card green"><h2>Între utilizatori</h2><p>Părintele poate vedea progresul live și poate trimite provocări direct către sesiunea copilului.</p></div>
  <div class="card orange"><h2>Între platforme</h2><p>Creatorul publică un curs în web, elevul îl parcurge în Community Island, iar progresul ajunge în aplicația parentală.</p></div>
</div>

<blockquote>Nu este doar o colecție de exerciții: este un circuit complet de învățare, feedback, creație de conținut și colaborare.</blockquote>

<!--
PERSOANA 1: Originalitatea rezultă din combinarea acestor mecanisme. În mod normal, jocul, profilul AI, aplicația parentală și editorul de cursuri sunt produse separate. În Mentora ele folosesc aceleași date și se completează reciproc.
-->

---

<div class="speaker-tag">Persoana 1</div>
<div class="kicker">VI.2 · Documentație și utilizare</div>

# Proiect documentat de la instalare până la arhitectură

<div class="grid-3 docs-grid" style="margin-top:1.1rem">
  <div class="card violet"><span class="icon icon-word">01 / OVERVIEW</span><h3>Descrierea proiectului</h3><p>Scop, problemă, utilizatori, insule, Rudolf, AI, beneficii și originalitate.</p></div>
  <div class="card cyan"><span class="icon icon-word">02 / TECH</span><h3>Documentația tehnică</h3><p>Arhitectură, tehnologii justificate, protocol, fluxuri, securitate, testare și Git.</p></div>
  <div class="card green"><span class="icon icon-word">03 / SYSTEM</span><h3>Cerințe de sistem</h3><p>Hardware, software și rețea pentru server, Unity, Android/iOS și Creator-ul Web.</p></div>
</div>

<div class="grid-2" style="margin-top:1.1rem">
  <div class="card orange"><h3>Ghid de instalare</h3><p>Java 21, PostgreSQL și imaginile Docker pentru server, `npm install` pentru web, Android Studio/Xcode pentru mobil și Unity Hub pentru joc.</p></div>
  <div class="card"><h3>Ghid de utilizare</h3><p>Autentificare QR, explorarea jocului, rezolvarea activităților, administrarea cursurilor și consultarea progresului părintelui.</p></div>
</div>

<!--
PERSOANA 1: Documentația este separată logic. Avem informațiile generale pentru evaluare, documentația tehnică pentru arhitectură și implementare, plus cerințe de sistem și pași de instalare și utilizare.
-->

---

<div class="speaker-tag">Persoana 1 + Persoana 2</div>
<img src="./public/game_picture.png" style="position:absolute;inset:0;width:100%;height:100%;object-fit:cover" />
<div class="cover-overlay" />
<div class="cover-content">
  <div class="kicker">Concluzie</div>
  <h1 class="cover-title" style="font-size:4.2rem">În Mentora, elevul <span>construiește</span> ca să învețe.</h1>
  <p class="final-motto">Mentora demonstrează nu doar o idee, ci un produs educațional complet, interactiv și extensibil.</p>
  <p class="cover-subtitle">Cod · explorare 3D · feedback · AI · colaborare · familie · conținut creat de comunitate.</p>
  <div class="pill-row" style="margin-top:1.5rem"><span class="pill">Mulțumim!</span><span class="pill">Urmează demonstrația live</span></div>
</div>

<!--
PERSOANA 1: Mentora leagă programarea practică de explorare, feedback și progres real.
PERSOANA 2: Vă mulțumim și vă invităm să urmăriți demonstrația în joc, în aplicația mobilă și în Creator-ul Web.
-->
