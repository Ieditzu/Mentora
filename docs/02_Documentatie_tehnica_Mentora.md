MENTORA – DOCUMENTAȚIE TEHNICĂ

1. SCOPUL DOCUMENTULUI

Documentul descrie arhitectura, tehnologiile, organizarea codului, fluxurile principale, instalarea, utilizarea și verificarea Mentora. Structura urmărește domeniile evaluate la un proiect software: arhitectură, implementare, testare, interfață, conținut, securitate și posibilitatea de distribuire. Cerințele de sistem sunt prezentate separat.

2. ARHITECTURA APLICAȚIEI

Mentora folosește o arhitectură cu mai mulți clienți și un backend central. Jocul este realizat cu Unity `2022.3.62f3`, C# și HDRP și oferă experiența elevului, codare, quiz-uri, companion și multiplayer. Aplicația parentală folosește Kotlin, Jetpack Compose și Java-WebSocket pentru monitorizare, obiective, conectare QR, rapoarte și sesiuni live.

Editorul web este construit cu React 19, Vite, Tailwind CSS și Framer Motion și permite crearea și administrarea cursurilor. Serverul utilizează Java 21, Spring Boot și Spring Data JPA/Hibernate pentru API, autentificare, logică educațională, AI și execuție de cod. PostgreSQL păstrează conturile, copiii, progresul, cursurile, obiectivele și sesiunile. Integrarea Groq API cu modelul LLaMA 3.3 70B susține mentoratul, evaluarea, provocările și rapoartele AI.

Clienții Android și Unity comunică prin WebSocket cu pachete binare criptate. Editorul web folosește endpointuri REST. Jocul mai are o rețea LAN separată pentru multiplayer, cu TCP pentru sesiune și UDP pentru descoperirea hostului.

2.1. Straturile backendului

Backendul este organizat pe responsabilități:

- `database/entity` conține entitățile persistente: `Parent`, `Child`, `Task`, `Course`, `Goal`, `GameSession` și progresul la cursuri;
- `database/repository` oferă accesul la PostgreSQL prin Spring Data JPA;
- `database/services` conține logica de domeniu pentru copii, cursuri, obiective, taskuri, sesiuni și profilul de învățare;
- `packet` și `packet/impl` definesc protocolul binar și mesajele concrete;
- `client/ClientHandler` primește pachetele, verifică sesiunea și le distribuie către acțiunea corespunzătoare;
- `web` expune autentificarea și administrarea cursurilor prin HTTP;
- `python`, `cpp` și `utility` implementează execuția controlată, AI-ul, hashingul și criptarea.

Această separare limitează dependențele dintre interfață, transport, logică și persistență și face posibilă extinderea cu noi tipuri de pachete, servicii sau clienți.

2.2. Modelul de date

Relațiile principale sunt:

- un părinte poate avea mai mulți copii;
- un copil poate avea o sesiune persistentă, taskuri finalizate, obiective și progres la cursuri;
- un părinte poate deține mai multe cursuri;
- un curs are întrebări ordonate și poate avea progres separat pentru fiecare copil;
- profilul de învățare este păstrat în coloana JSONB `game_stats`, astfel încât structura poate evolua fără migrații pentru fiecare statistică nouă.

La finalizarea unui curs sunt memorate încercările, scorul ultimei încercări, cel mai bun scor, numărul de întrebări, timpul ultimei încercări și starea recompensei.

3. TEHNOLOGII ȘI JUSTIFICAREA ALEGERILOR

Java și Spring Boot sunt potrivite pentru un server cu logică de domeniu, validare, acces la baze de date și mai multe tipuri de clienți. Spring Data JPA reduce codul repetitiv pentru operațiile de persistență, iar PostgreSQL oferă relații robuste și suport pentru JSONB.

Unity și C# permit construirea unei lumi 3D interactive, a controalelor pentru desktop, mobil și VR și a scenelor în care rezultatul activității elevului este vizibil. Kotlin și Jetpack Compose simplifică realizarea unei interfețe Android moderne, reactive și adaptabile. React și Vite oferă un editor web SPA rapid, cu componente reutilizabile și build optimizat.

WebSocket este folosit pentru actualizări în timp real, autentificare QR, progres și sesiuni live. REST este potrivit pentru operațiile web de tip creare, citire, actualizare și ștergere. Groq este izolat într-un wrapper (`GroqAI`), care include cache LRU, timeout și rotația cheilor.

4. PROTOCOL ȘI FLUXURI DE DATE

4.1. Pachete WebSocket

Pachetul are forma conceptuală:

```text
[lungime seed][seed criptat][payload criptat]
```

Seed-ul este generat dinamic pentru fiecare mesaj. Payloadul este serializat cu lungimi și bytes UTF-8 și este criptat cu AES/CBC. Clientul și serverul au implementări compatibile în Java, Kotlin și C#. Serverul validează dimensiunea seed-ului înainte de decriptare și verifică starea de autentificare înainte de a executa operații protejate.

4.2. Autentificare și conectare QR

Fluxul pentru copil este următorul: jocul solicită un token QR, aplicația Android îl scanează și îl revendică pentru copilul ales, iar serverul transmite răspunsul către clientul Unity care așteaptă. Ulterior, jocul poate relua sesiunea cu tokenul persistent.

Pentru editorul web, autentificarea folosește endpointurile `/api/web/auth/lookup`, `/register` și `/login`. Tokenul este transmis ulterior prin antetul `Authorization: Bearer ...`, iar accesul la cursuri este condiționat de proprietarul sesiunii.

4.3. Execuția codului elevului

Python este rulat cu `python3 -I -B -S`, iar C++ este compilat cu `g++ -O2`. Pentru ambele limbaje, serverul creează un director temporar, aplică limite de memorie, CPU, fișiere și procese, dezactivează rețeaua prin `unshare --net` și șterge fișierele temporare după rulare. Există și timeout pentru procesul Java.

5. ORGANIZAREA CLIENȚILOR

În Unity, responsabilitățile sunt împărțite între `GameClient`, pad-urile Python și C++, `CodeWorldRuntime`, `CodeWorldQuestIsland`, `AiChallengePad`, `CommunityIslandMenu`, `MultiplayerQuizManager`, `RocketLandingPuzzle`, `RobotCompanion`, `MultiplayerSessionManager` și managerii de interfață. Aceste componente susțin Code Quest Island, Quiz Island, Community Island, CodeWorld, provocările AI, experimentul Rocket Landing, companionul Rudolf și multiplayerul LAN cu voce. `MentoraLocalization` păstrează alegerea limbii în `PlayerPrefs`, actualizează etichetele uGUI înregistrate și oferă un punct unic pentru adăugarea traducerilor. În Android, `SocketViewModel` centralizează starea, comunicarea și limba salvată în preferințe, iar `AppLanguages` și `MainActivity` selectează contextul Compose pentru limba aleasă sau limba sistemului. Interfața Android include română, engleză, franceză și germană, alături de limbile suplimentare din selector. În web, `App.jsx` gestionează navigarea, autentificarea, editorul și preferința de limbă, `src/lib/i18n.js` păstrează catalogul de traduceri, iar `src/lib/api.js` standardizează apelurile HTTP și tratarea erorilor.

Denumirile claselor și ale serviciilor descriu responsabilitatea lor, iar pachetele sunt grupate după domeniu: autentificare, copil, curs, joc, limbaj, AI și companion. Protocolul este extensibil prin adăugarea unui packet type, a unei clase concrete și a înregistrării în `PacketManager`.

5.1. Internaționalizare în Unity

În Unity, interfața este disponibilă în română, engleză, franceză și germană. Componenta `MentoraLocalization` definește limbile `Romanian`, `English`, `French` și `German`, reține preferința utilizatorului în `PlayerPrefs` și emite un eveniment la schimbarea limbii. Clasele de interfață folosesc catalogul centralizat pentru etichete fixe și mesaje de sistem, iar textele se actualizează fără repornirea jocului. Sunt suportate atât textele uGUI (`UnityEngine.UI.Text`), cât și etichetele 3D de tip `TextMesh`.

Selectorul este disponibil în meniul Settings. Aceeași preferință este preluată de ecranele de exerciții Python/C++, quiz-ul C++, Community Island, CodeWorld, Quiz Island, AI Challenge și notificările părintelui. Datele provenite de la utilizator, de la creatorul de curs sau de la AI nu sunt traduse automat; aplicația păstrează intenționat conținutul original.

5.2. Internaționalizare în Creator-ul Web

Creator-ul Web este disponibil în română, engleză, franceză și germană și păstrează alegerea `en`, `ro`, `fr` sau `de` în `localStorage`, sub cheia `mentora_creator_language`. Componenta principală transmite funcția de traducere către autentificare și editor, iar catalogul `src/lib/i18n.js` convertește etichetele fixe, mesajele de confirmare, titlurile, acțiunile, formularele și placeholder-ele. Selectorul EN/RO/FR/DE este accesibil înainte și după autentificare, iar schimbarea limbii actualizează interfața fără reîncărcarea paginii. Titlurile, rezumatele și întrebările cursurilor nu sunt traduse automat, deoarece reprezintă conținut educațional introdus de creator.

6. INTERFAȚĂ ȘI UTILIZARE

6.1. Jocul

1. Se pornește jocul și se efectuează autentificarea copilului prin QR.
2. Elevul explorează harta și intră într-o zonă de programare sau quiz.
3. Scrie codul sau selectează răspunsul.
4. Trimite soluția și primește rezultatul, scorul și explicația.
5. Progresul, punctele și evenimentele de învățare sunt salvate pe server.

6.2. Aplicația Android

1. Părintele își creează cont sau se autentifică.
2. Adaugă ori selectează copilul.
3. Scanează codul QR afișat de joc.
4. Consultă copiii, punctele, obiectivele, istoricul, profilurile AI și raportul săptămânal.
5. Poate urmări sesiunea live și trimite provocări personalizate.

6.3. Editorul web

1. Creatorul introduce adresa de e-mail; sistemul stabilește dacă este necesară înregistrarea sau autentificarea.
2. Din dashboard poate crea un curs.
3. Completează metadatele și adaugă întrebări cu patru opțiuni.
4. Salvează, editează sau șterge cursul.
5. Cursurile publicate pot fi încărcate și jucate din Unity.

7. INSTALARE ȘI CONFIGURARE

7.1. Backend

1. Se instalează Java 21 și PostgreSQL.
2. Se creează baza de date și utilizatorul conform configurației locale.
3. Se verifică `java-server/Java-Server/src/main/resources/application.properties`.
4. Se creează `api-keys.json` pornind de la `api-keys.example.json`; cheile reale nu se introduc în Git.
5. Se pornește serverul:

```bash
cd java-server/Java-Server
./gradlew bootRun
```

7.2. Editor web

```bash
cd web-creator
npm install
npm run dev
```

Dacă backendul nu este la adresa proxy implicită, se configurează `VITE_API_BASE`.

7.3. Android

Se deschide directorul `kotlin-app` în Android Studio, se sincronizează Gradle, se selectează un emulator sau un dispozitiv cu Android 7.0+ și se rulează configurația `app`. Dispozitivul trebuie să poată ajunge la serverul WebSocket.

7.4. Unity

Se deschide directorul `unity` în Unity Hub cu versiunea 2022.3.62f3. În `GameClient.cs` se verifică URL-ul backendului. Pentru o sesiune multiplayer, dispozitivele Unity se conectează la aceeași rețea locală.

8. TESTARE ȘI VERIFICARE

Mentora este verificată prin scenarii funcționale și de integrare care acoperă comunicarea dintre server, Unity, Android și Creator-ul Web. Repository-ul include teste Android de unitate și de instrumentare, iar scenariile de demonstrație validează fluxurile educaționale și colaborative.

Scenariile de verificare includ:

- pornirea serverului și conectarea unui client Unity;
- autentificarea părintelui în Android și web;
- scanarea QR și reluarea sesiunii copilului;
- executarea unei soluții Python și C++ corecte și incorecte;
- solicitarea unui indiciu AI și verificarea actualizării profilului;
- crearea, validarea, editarea și ștergerea unui curs;
- parcurgerea unui quiz și verificarea scorului și explicației;
- crearea unui obiectiv și finalizarea lui prin activitate;
- monitorizarea unei sesiuni live;
- conectarea a două instanțe Unity în LAN și folosirea unui quiz/CodeWorld colaborativ.

9. SECURITATE

Implementarea include criptarea pachetelor WebSocket, seed dinamic per pachet, validare de lungime, tokenuri Bearer cu expirare, verificarea proprietarului cursului, verificări ale drepturilor părinte–copil și sandbox pentru codul trimis de elev. Configurațiile externe, cheia AI și datele de acces sunt separate de logica aplicației, iar API-ul validează datele primite înainte de procesare.

10. CONTROLUL VERSIUNILOR ȘI MENTENANȚĂ

Proiectul este gestionat cu Git. Componentele, configurațiile și istoricul modificărilor sunt păstrate în același repository, ceea ce permite revenirea la versiuni anterioare, urmărirea evoluției și colaborarea pe module. Fișierele de configurare separă datele specifice mediului de codul aplicației.

Structura pe componente permite dezvoltarea independentă a serverului, jocului, aplicației Android și editorului web. Versionarea, documentarea protocolului și buildurile componentelor susțin mentenanța și evoluția continuă a proiectului.

11. CONCLUZIE TEHNICĂ

Mentora are o arhitectură coerentă, modulară și extensibilă, cu un backend central și clienți specializați. Alegerea tehnologiilor este justificată de natura fiecărei componente: Unity pentru simulare 3D, Compose pentru mobil, React pentru administrare web și Spring/PostgreSQL pentru servicii și persistență. Funcțiile de criptare, autorizare, validare și sandboxing susțin o experiență educațională sigură, interactivă și pregătită pentru distribuire.
