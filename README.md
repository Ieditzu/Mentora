<p align="center">
  <img src="images/mentora-logo.png" alt="Sigla Mentora" width="160" />
</p>

<h1 align="center">Mentora</h1>

<p align="center">
  <strong>Un ecosistem educațional bazat pe IA pentru a învăța programare prin joc, practică și feedback real.</strong>
</p>

<p align="center">
  Joc Unity pentru elevi · aplicații Android/iOS pentru părinți · creator web · backend Java securizat
</p>

<p align="center">
  <a href="https://github.com/Ieditzu/Mentora/actions/workflows/integration-tests.yml"><img alt="Teste de integrare" src="https://github.com/Ieditzu/Mentora/actions/workflows/integration-tests.yml/badge.svg" /></a>
</p>

<p align="center">
  <img alt="Unity 2022.3" src="https://img.shields.io/badge/Unity-2022.3.62f3-000000?style=flat-square&logo=unity&logoColor=white" />
  <img alt="Java 21" src="https://img.shields.io/badge/Java-21-ED8B00?style=flat-square&logo=openjdk&logoColor=white" />
  <img alt="Spring Boot 3.2" src="https://img.shields.io/badge/Spring_Boot-3.2.12-6DB33F?style=flat-square&logo=springboot&logoColor=white" />
  <img alt="Kotlin Multiplatform" src="https://img.shields.io/badge/Kotlin_Multiplatform-2.2.10-7F52FF?style=flat-square&logo=kotlin&logoColor=white" />
  <img alt="React 19" src="https://img.shields.io/badge/React-19-61DAFB?style=flat-square&logo=react&logoColor=061A24" />
  <img alt="PostgreSQL 16" src="https://img.shields.io/badge/PostgreSQL-16-4169E1?style=flat-square&logo=postgresql&logoColor=white" />
  <img alt="Docker" src="https://img.shields.io/badge/Docker-Sandboxed-2496ED?style=flat-square&logo=docker&logoColor=white" />
</p>

<p align="center">
  <a href="#arhitectura-sistemului">Arhitectură</a> ·
  <a href="#stiva-tehnologică">Tehnologii</a> ·
  <a href="#note-despre-securitate">Securitate</a> ·
  <a href="#rularea-proiectului">Pornire locală</a> ·
  <a href="#starea-actuală-a-testării">Testare</a> ·
  <a href="presentation-slidev/mentora-presentation.pdf">Prezentare PDF</a>
</p>

---

## Despre Mentora

Mentora conectează experiența de învățare a copilului cu instrumentele de monitorizare ale părintelui și cu un creator de conținut. Elevii scriu și rulează cod real în Python și C++, rezolvă probleme AI/ML evaluate pe teste ascunse și primesc feedback persistent, iar părinții urmăresc progresul și își protejează contul prin autentificare TOTP în doi pași.

| Experiență | Pentru cine | Ce oferă |
| --- | --- | --- |
| 🎮 **Joc Unity** | Elevi | Python, C++, AI/ML, quiz-uri, CodeWorld, VR și multiplayer LAN |
| 📱 **Aplicații mobile** | Părinți | Progres, obiective, rapoarte, sesiuni live și TOTP 2FA |
| 🧩 **Creator web** | Creatori | Publicarea și administrarea cursurilor și problemelor |
| 🛡️ **Backend** | Platformă | PostgreSQL, protocol binar, evaluare ascunsă și sandbox Docker |

> **Stadiu:** protocol v2 cu 2FA și sesiuni rotite, probleme ML evaluate pe date ascunse, progres persistent și multiplayer Unity prin LAN.

<p align="center">
  <img src="images/entire_map_v2.png" alt="Lumea Mentora" width="78%" />
</p>

## Prezentare vizuală

### Jocul Unity

| Harta lumii | Harta actualizată | Scenă din joc |
| --- | --- | --- |
| ![Harta lumii](images/entire_map_in_game.png) | ![Harta actualizată a lumii](images/entire_map_v2.png) | ![Scenă din joc](images/game_picture.png) |

| Joc pe mobil | Insula codului | Insula Python |
| --- | --- | --- |
| ![Joc pe telefon](images/playing_game_on_phone.png) | ![Insula codului](images/codeIsland.png) | ![Insula Python](images/python_island.png) |

| Secțiunea Python | Programare în Python | Secțiunea C++ |
| --- | --- | --- |
| ![Secțiunea Python](images/python_section_in_game.png) | ![Programare în Python](images/python_coding.png) | ![Secțiunea C++](images/cpp.png) |

| Interacțiune cu IA în C++ | Provocare Python generată de IA | Mentor IA pentru Python | Test |
| --- | --- | --- | --- |
| ![Interacțiune cu IA în C++](images/cppAI.png) | ![Provocare Python generată de IA](images/pythonAiChallenge.png) | ![Mentor IA pentru Python](images/ai_python.png) | ![Test](images/quiz.png) |

| Întrebare de test | Meniul de pauză | Ghidul lui Rudolf |
| --- | --- | --- |
| ![Întrebare de test](images/quiz2.png) | ![Test în meniul de pauză](images/pauseMenuQuiz.png) | ![Ghidul lui Rudolf](images/rudolfGuide.png) |

| Continuarea ghidului lui Rudolf | Răspunsul lui Rudolf | Rudolf pe PC |
| --- | --- | --- |
| ![Continuarea ghidului lui Rudolf](images/RudolfGuide2.png) | ![Răspunsul lui Rudolf](images/rudolfAnswer.png) | ![Rudolf pe PC](images/rudolf_pc.png) |

#### Experiența în realitate virtuală

| Jocul Mentora în VR | Conversație cu Rudolf în VR |
| --- | --- |
| ![Experiență de joc Mentora în realitate virtuală](images/vr.jpeg) | ![Utilizatorul VR discută cu Rudolf în lumea Mentora](images/vrRudolf.jpeg) |

Mentora poate fi explorată în realitate virtuală, folosind controlere VR pentru deplasare și interacțiune. Companionul educațional Rudolf rămâne disponibil și în această experiență, unde utilizatorul poate comunica direct cu el și poate primi îndrumare în lumea 3D.

| Comunitate | Proiectil | Obiect de tip cutie |
| --- | --- | --- |
| ![Comunitate](images/community.png) | ![Rachetă](images/missile.png) | ![Cutie](images/box.png) |

### Aplicația pentru părinți

| Panoul copiilor | Detalii din panoul copiilor | Obiective |
| --- | --- | --- |
| ![Panoul Copiii mei](images/appMyKids.png) | ![Detalii despre Copiii mei](images/appMyKids2.png) | ![Obiective](images/appGoals.png) |

| Profilul părintelui | Setări | Tema setărilor |
| --- | --- | --- |
| ![Profilul părintelui](images/appProfile.png) | ![Setări](images/appSettings.png) | ![Tema setărilor](images/appSettings2.png) |

| Analiza competențelor | Radarul competențelor | Istoricul sarcinilor |
| --- | --- | --- |
| ![Analiza competențelor](images/appSkill.png) | ![Radarul competențelor](images/appSkillRadar.png) | ![Istoricul sarcinilor](images/appTaskHistory.png) |

### Creatorul web de cursuri

| Panoul creatorului | Biblioteca de cursuri și editorul | Interfața creatorului în franceză |
| --- | --- | --- |
| ![Panoul creatorului web](images/web_creator.png) | ![Cursurile creatorului web](images/web_creator_courses.png) | ![Creatorul web în franceză](images/web_creator-fr.png) |

## Arhitectura sistemului

Mentora folosește o arhitectură cu mai mulți clienți și un singur backend. Backendul Java este sursa de adevăr pentru identitate, progres, datele profilului IA, conținutul cursurilor, finalizarea sarcinilor, obiective și executarea codului. Unity și Android comunică cu acesta printr-un protocol WebSocket binar criptat. Creatorul web folosește REST.

```mermaid
flowchart LR
    Web[Creator web de cursuri<br/>React + Vite] -->|HTTP REST :8085| Backend[Backend Java/Spring]
    Android[Aplicație Android pentru părinți<br/>Kotlin + Compose] -->|WebSocket binar criptat :49154| Backend
    Unity[Client de joc Unity<br/>C# + HDRP] -->|WebSocket binar criptat :49154| Backend

    Backend --> DB[(PostgreSQL)]
    Backend --> Groq[Groq API<br/>LLaMA 3.3 70B]
    Backend --> Python[Container Python fără rețea]
    Backend --> Cpp[Container C++ fără rețea]
    Backend --> ML[Container NumPy/pandas/scikit-learn]

    Unity <--> LAN[Multiplayer Unity prin LAN<br/>TCP 7777 + descoperire UDP 7776]
```

Clientul Unity conține și un al doilea nivel de rețea, separat de backendul Java. `MultiplayerSessionManager.cs` gestionează descoperirea în LAN, găzduirea sesiunilor și conectarea la acestea, avatarurile jucătorilor la distanță, pachetele de test, chatul vocal, sincronizarea CodeWorld și partajarea profilurilor în modul multiplayer.

```mermaid
flowchart TD
    Host[Gazdă Unity] <-->|Pachete TCP de joc/sesiune| PeerA[Client Unity A]
    Host <-->|Pachete TCP de joc/sesiune| PeerB[Client Unity B]
    Host -.->|Semnal UDP de descoperire<br/>MENTORA_MP_DISCOVERY_V1| LAN[Rețea locală]
    PeerA -->|Sincronizarea editorului CodeWorld<br/>răspunsuri la teste<br/>cadre audio| Host
    PeerB -->|rezumatul profilului<br/>pozițiile cursorului<br/>starea jucătorului| Host
```

## Structura repository-ului

| Cale | Scop |
| --- | --- |
| `java-server/Java-Server/` | Backend Spring Boot, server WebSocket, API REST, integrare IA, executarea codului, persistență |
| `unity/` | Client de joc Unity 2022.3.62f3 HDRP |
| `kotlin-app/` | Aplicații Android/iOS pentru părinți și modul shared Kotlin Multiplatform |
| `web-creator/` | Platformă React/Vite pentru crearea cursurilor |
| `images/` | Capturi de ecran ale proiectului, folosite în acest README și în materialele de prezentare |
| `presentation-slidev/` | Resurse pentru prezentarea Slidev |

## Stiva tehnologică

| Strat | Tehnologii |
| --- | --- |
| Backend | Java 21, Spring Boot 3.2.12, Spring Data JPA, Hibernate, PostgreSQL |
| Protocol backend în timp real | Java-WebSocket, pachete binare criptate personalizate |
| IA | Groq API, `llama-3.3-70b-versatile`, memorie cache pentru răspunsuri, rotația cheilor API |
| Executarea codului | Containere Docker efemere pentru Python, C++, CodeWorld și NumPy/pandas/scikit-learn |
| Joc | Unity 2022.3.62f3, C#, HDRP |
| Android | Kotlin, Jetpack Compose, CameraX, ZXing pentru scanarea codurilor QR, Coil |
| Web | React 19, Vite 7, Tailwind CSS v4, Framer Motion, lucide-react |

## Backend

Backendul este autoritatea centrală a platformei. Acesta gestionează datele conturilor, profilurile copiilor, istoricul învățării, cursurile publicate, sarcinile, obiectivele, starea sesiunilor în timp real, provocările trimise de părinți, rapoartele săptămânale, apelurile către IA și executarea codului.

Fișiere importante:

| Fișier | Rol |
| --- | --- |
| `client/ClientHandler.java` | Dispecerul principal pentru pachetele WebSocket și punctul de control al autorizării |
| `packet/Packet.java` | Clasa de bază a pachetelor, criptare/decriptare, serializarea șirurilor de caractere |
| `packet/PacketManager.java` | Fabrica de pachete pentru ID-urile pachetelor backend |
| `database/services/LearningProfileService.java` | Actualizări ale profilului IA pentru fiecare copil, rezumate, rapoarte săptămânale |
| `machinelearning/MachineLearningService.java` | Catalogul celor nouă probleme AI/ML, evaluarea ascunsă, progresul și recompensele |
| `machinelearning/MachineLearningExecutor.java` | Rularea soluțiilor AI/ML în containerul științific Python |
| `database/services/CourseService.java` | Operații CRUD pentru cursuri, publicare, finalizare, logica recompenselor |
| `database/services/TaskService.java` | Popularea inițială a sarcinilor globale și finalizarea sarcinilor |
| `utility/GroqAI.java` | Componentă de integrare pentru API-ul de chat Groq, memorie cache pentru răspunsuri, rotația cheilor |
| `python/PythonExecutor.java` | Executarea Python într-un mediu izolat |
| `cpp/CppExecutor.java` | Compilarea și executarea C++ într-un mediu izolat |
| `utility/ContainerExecution.java` | Politica comună de izolare Docker pentru tot codul trimis de elevi |
| `web/WebAuthController.java` | Endpointuri web pentru autentificare |
| `web/WebCourseController.java` | API REST web pentru cursuri |

`Server.java` păstrează starea activă din timpul rulării:

- `activeConnections` pentru clienții conectați.
- `pendingQRLogins` pentru asocierea autentificărilor prin QR.
- `latestLiveSessionStates` pentru monitorizarea în timp real de către părinți.
- `liveSessionSpectators` pentru clienții abonați de tip părinte.
- `activeParentChallenges` pentru provocările trimise de părinți.

## Criptarea pachetelor binare

Unity și Android nu trimit JSON în clar către WebSocket-ul backendului. Acestea folosesc un format binar personalizat pentru pachete, implementat în Java, C# și Kotlin.

```mermaid
sequenceDiagram
    participant Client as Client Unity/Android
    participant Packet as Packet.encode()
    participant Server as ClientHandler Java
    participant Manager as PacketManager

    Client->>Packet: Construiește conținutul pachetului
    Packet->>Packet: Generează dynamicSeed din nanoTime/ticks
    Packet->>Packet: Criptează dynamicSeed cu cheia de bază partajată
    Packet->>Packet: Criptează conținutul cu SHA-256(dynamicSeed)
    Packet->>Server: [seedLength][encryptedSeed][encryptedPayload]
    Server->>Server: Validează seedLength
    Server->>Server: Decriptează seed cu cheia de bază
    Server->>Server: Decriptează conținutul cu seed-ul dinamic
    Server->>Manager: Instanțiază pachetul după ID
    Manager-->>Server: Pachet concret
    Server->>Server: Autorizează și distribuie
```

Structura cadrului:

```text
[lungimea seed-ului pe 4 octeți][seed criptat][conținut criptat]
```

Detalii despre criptare confirmate în cod:

- Algoritm: `AES/CBC/PKCS5Padding` în Java, `Aes` cu `CBC` și `PKCS7` în C#.
- Derivarea cheii: hashul SHA-256 al șirului furnizat pentru parolă/seed.
- IV: un IV aleatoriu de 16 octeți, adăugat înaintea textului cifrat.
- Seed dinamic: generat pentru fiecare pachet, criptat cu `Data.baseKey`, apoi folosit drept cheie pentru conținut.
- Validare defensivă: lungimea seed-ului trebuie să fie pozitivă și să nu depășească `1024`.

Serializarea șirurilor în interiorul pachetelor folosește:

```text
[lungime int][octeți UTF-8]
```

## Referință pentru pachete

Există două sisteme de pachete:

- Pachetele WebSocket ale backendului, gestionate de `java-server/Java-Server/.../PacketManager.java`.
- Pachetele multiplayer locale din Unity, gestionate de `unity/Assets/Scripts/Runtime/Network/PacketManager.cs`.

### Pachete WebSocket ale backendului

| ID | Pachet | Scop |
| --- | --- | --- |
| `1` | `HandShakePacket` | Clientul se identifică după conexiunea WebSocket |
| `2` | `AuthPacket` | Autentificarea părintelui prin WebSocket |
| `3` | `RegisterParentPacket` | Înregistrarea părintelui prin WebSocket |
| `4` | `AddChildPacket` | Adăugarea unui copil părintelui autentificat |
| `5` | `AddGoalPacket` | Crearea unui obiectiv pentru copil |
| `8` | `CompleteTaskPacket` | Marcarea sarcinii drept finalizată și acordarea punctelor aferente |
| `9` | `ActionResponsePacket` | Răspuns generic de succes/eroare |
| `10` | `AuthResponsePacket` | Răspunsul la autentificarea părintelui |
| `11/12` | `FetchTasksPacket` / răspuns | Catalogul global de sarcini |
| `13/14` | `FetchGoalsPacket` / răspuns | Obiectivele unui copil |
| `15/16` | `FetchChildrenPacket` / răspuns | Lista copiilor părintelui și indicatorii stării online |
| `17/18` | `FetchCompletedTasksPacket` / răspuns | Istoricul sarcinilor finalizate |
| `19/20` | `GenerateQRLoginPacket` / răspuns | Crearea tokenului pentru autentificare prin QR |
| `21` | `ClaimQRLoginPacket` | Aplicația părintelui revendică tokenul QR pentru copil |
| `22` | `ChildAuthResponsePacket` | Răspunsul la autentificarea copilului în joc |
| `23/24` | `FetchChildStatsPacket` / răspuns | Statisticile copilului și profilul de joc în format JSON |
| `25` | `VerifySessionPacket` | Reluarea sesiunii de joc |
| `26` | `UpdatePfpPacket` | Actualizarea imaginii de profil a părintelui sau a copilului |
| `27` | `RemoveChildPacket` | Ștergerea profilului copilului |
| `28/29` | `ExecuteCPPCodePacket` / răspuns | Compilarea și rularea codului C++ |
| `30/31` | `AskAiPacket` / `AiResponsePacket` | Conversația cu mentorul IA și evaluarea |
| `32` | `FetchChildStatsByParentPacket` | Părintele preia statisticile copilului fără a-i actualiza seria |
| `33` | `RecordLearningEventPacket` | Scrierea unui eveniment de învățare în profilul copilului |
| `34/35` | `ExecutePythonCodePacket` / răspuns | Rularea codului Python |
| `36/37` | `FetchPublishedCoursesPacket` / răspuns | Catalogul cursurilor publicate |
| `38/39` | `FetchCourseDetailPacket` / răspuns | Detaliile cursului publicat |
| `40` | `SubmitCourseCompletionPacket` | Salvarea încercării la curs și a eventualei recompense |
| `41/42` | `FetchAllChildrenPacket` / răspuns | Listarea copiilor pentru dezvoltare/administrare |
| `43` | `DevLoginAsChildPacket` | Scurtătură pentru dezvoltatori pentru autentificarea drept copil |
| `44` | `DevCreateChildProfilePacket` | Scurtătură pentru dezvoltatori pentru crearea unui profil de copil |
| `45/46` | `GenerateAiTaskPacket` / răspuns | Provocare generată de IA |
| `47/48` | `CompanionSpeakPacket` / răspuns | Răspunsul text al companionului |
| `58/59` | `CompanionVoiceTextPacket` / `CompanionVoiceAudioPacket` | Intrare vocală/text pentru companion |
| `64/65` | `SubscribeLiveSessionPacket` / `LiveSessionUpdatePacket` | Monitorizarea în direct de către părinte a sesiunii |
| `66/67/68` | Pachete pentru provocări trimise de părinte | Părintele trimite provocarea și primește confirmarea finalizării |
| `69/70` | Pachete pentru raportul săptămânal | Raportul IA săptămânal pentru părinte |
| `71/72` | Pachete cu rezumatul profilului de programare | Rezumatul profilului copilului pentru contextul jocului/modului multiplayer |
| `74/75` | `CodeWorldPythonRunPacket` / răspuns | Rularea corelată a codului Python care controlează CodeWorld |
| `76` | `SetClientLanguagePacket` | Limba preferată pentru răspunsurile serverului |
| `77/78` | `FetchMachineLearningProblemsPacket` / răspuns | Catalogul AI/ML și progresul copilului |
| `79/80` | `SubmitMachineLearningSolutionPacket` / rezultat | Rularea, evaluarea ascunsă și recompensa unei soluții AI/ML |
| `81/82` | Solicitare/verificare al doilea factor | Provocarea de autentificare TOTP sau prin cod de recuperare |
| `83/84` | Pornire/configurare TOTP | Generează secretul și URI-ul `otpauth://` pentru aplicația Authenticator |
| `85/86` | Confirmare/rezultat configurare TOTP | Confirmă primul cod și livrează o singură dată codurile de recuperare |
| `87` | `DisableParentTotpPacket` | Dezactivează TOTP după reverificarea parolei și a celui de-al doilea factor |
| `88/89` | Solicitare/răspuns stare de securitate | Returnează starea TOTP și numărul codurilor de recuperare rămase |
| `90` | `ParentAuthSessionPacket` | Livrează tokenul opac al sesiunii de părinte și expirarea sa |
| `91` | `ResumeParentSessionPacket` | Reia și rotește o sesiune legată de dispozitiv |
| `92` | `RevokeParentSessionPacket` | Revocă sesiunea curentă sau toate sesiunile părintelui |

`ClientHandler.java` aplică o politică explicită pentru rolurile `UNAUTHENTICATED`, `PASSWORD_VERIFIED_PENDING_TOTP`, `PARENT` și `CHILD`. Operațiile vocale/STT și cele care execută cod sunt disponibile numai unei sesiuni de copil. Pachetele de dezvoltare `41`, `43` și `44` sunt dezactivate implicit; chiar dacă `MENTORA_DEV_PACKETS_ENABLED=true`, acestea cer o sesiune de părinte și verifică proprietatea asupra profilului de copil.

### Pachete multiplayer locale Unity

Aceste pachete sunt definite în clientul Unity și aparțin modului multiplayer prin LAN, nu backendului Java:

| ID | Pachet | Scop |
| --- | --- | --- |
| `49/50` | `MultiplayerJoinPacket` / `MultiplayerWelcomePacket` | Alăturarea la sesiunea gazdă |
| `51/52` | `MultiplayerPlayerStatePacket` / `MultiplayerPlayerLeftPacket` | Starea jucătorului la distanță |
| `53/54/55` | `QuizStartPacket` / `QuizAnswerPacket` / `QuizResultPacket` | Fluxul testului multiplayer |
| `56/57` | `MultiplayerVoicePacket` / `MultiplayerUdpHelloPacket` | Voce și descoperire UDP |
| `60/61` | `CodeWorldCommandPacket` / `CodeWorldStatePacket` | Sincronizarea comenzilor și stării CodeWorld |
| `62/63` | `CodeWorldEditorSyncPacket` / `CodeWorldCursorPacket` | Textul partajat al editorului și cursoarele cu nume |
| `73` | `MultiplayerProfileSummaryPacket` | Partajarea profilului de programare al fiecărui jucător |

## Autentificare

Mentora are fluxuri separate pentru părinți și copii.

### Autentificarea părinților

Părinții se pot autentifica prin pachete WebSocket în aplicațiile Android/iOS sau prin API-ul REST web. Dacă TOTP este activat, verificarea parolei produce o provocare scurtă, legată de conexiune; sesiunea este emisă numai după un cod TOTP valid sau un cod de recuperare nefolosit. API-ul web expune:

| Metodă | Endpoint | Scop |
| --- | --- | --- |
| `POST` | `/api/web/auth/lookup` | Verifică dacă există o adresă de e-mail |
| `POST` | `/api/web/auth/register` | Creează părintele și returnează tokenul |
| `POST` | `/api/web/auth/login` | Verifică parola; returnează tokenul sau HTTP `202` cu provocarea 2FA |
| `POST` | `/api/web/auth/login/totp` | Verifică TOTP/codul de recuperare și emite sesiunea |
| `GET` | `/api/web/auth/security` | Returnează starea TOTP pentru sesiunea autentificată |
| `POST` | `/api/web/auth/totp/setup` | Începe configurarea TOTP după reverificarea parolei |
| `POST` | `/api/web/auth/totp/enable` | Confirmă primul cod și activează TOTP |
| `DELETE` | `/api/web/auth/totp` | Dezactivează TOTP după reverificarea ambilor factori |

Pentru compatibilitatea protocolului existent, clienții derivă mai întâi o acreditare cu `SHA-256`; backendul nu o mai stochează direct, ci o protejează cu un hash adaptiv BCrypt, sărat. Înregistrările vechi care conțin acreditarea rapidă sunt migrate automat după prima autentificare reușită. Sesiunile de părinte folosesc tokenuri opace generate aleatoriu; baza de date păstrează numai hashul tokenului și al identificatorului de dispozitiv. Reluarea rotește tokenul, iar activarea sau dezactivarea TOTP revocă sesiunile anterioare. Secretele TOTP sunt criptate la stocare cu AES-256-GCM, codurile de recuperare sunt stocate numai sub formă de hash și sunt consumate o singură dată. Bugetul de încercări 2FA este comun contului și nu poate fi resetat prin solicitarea repetată a unor challenge-uri noi.

### Autentificarea copiilor prin QR

Copiii nu introduc acreditări în joc. Jocul folosește asocierea prin QR:

1. Unity trimite `GenerateQRLoginPacket`.
2. Backendul returnează un token scurt prin `QRLoginResponsePacket`.
3. Părintele scanează codul QR din Android.
4. Android trimite `ClaimQRLoginPacket` împreună cu tokenul și ID-ul copilului.
5. Backendul trimite `ChildAuthResponsePacket` clientului de joc aflat în așteptare.
6. Unity stochează ID-ul copilului/tokenul de sesiune, iar ulterior reia sesiunea prin `VerifySessionPacket`.

## Profil de învățare pentru fiecare elev

Fiecare copil are o coloană JSONB `game_stats` în tabelul `children`. Profilul este flexibil în mod intenționat și este gestionat de `LearningProfileService`.

```mermaid
flowchart LR
    CodeRun[Rezultatul rulării codului] --> Profile[game_stats JSONB]
    Quiz[Încercare la test sau curs] --> Profile
    Hint[Indiciu/chat IA] --> Profile
    Task[Finalizarea sarcinii] --> Profile

    Profile --> Cpp[aiProfileCpp]
    Profile --> Python[aiProfilePython]
    Profile --> General[aiProfileGeneral]

    Cpp --> AI[Context pentru promptul IA]
    Python --> AI
    General --> Parent[Rezumate și rapoarte pentru părinți]
    General --> Multiplayer[Contextul profilului combinat pentru multiplayer]
```

Câmpurile urmărite includ:

- `correctCount`
- `incorrectCount`
- `hintsUsed`
- `chatTurns`
- `totalInteractions`
- statistici pe subiecte
- concepte bine stăpânite și concepte dificile
- greșeli frecvente
- evenimente recente de învățare
- `summaryText`
- `summaryOneLine`
- `summaryThreeLine`
- marca temporală a actualizării rezumatului

`recordLearningEvent()` actualizează profilurile specifice limbajului și pe cele generale. `recordAiInteraction()` omite contextele care conțin `eval`, astfel încât evaluarea IA să nu mărească artificial numărul utilizărilor de indicii/chat.

`buildProfileSummary()` clasifică elevul drept `beginner`, `intermediate` sau `advanced` pe baza numărului de interacțiuni și a acurateței. `buildAiHelpProfileContext()` serializează profilul copilului ca text pentru prompturile IA. `buildMultiplayerProgrammingProfileSummary()` produce un șir compact al profilului, folosit de Unity la combinarea profilurilor multiplayer.

## Sistemul de IA

Stratul de IA are în centru Groq și `llama-3.3-70b-versatile`.

Capabilitățile din baza de cod actuală:

- Conversații cu mentorul IA prin `AskAiPacket`.
- Contexte de evaluare a codului asistate de IA.
- Flux de sarcini/provocări generate de IA prin `GenerateAiTaskPacket`.
- Replici ale companionului IA prin `CompanionSpeakPacket`.
- Intrare vocală pentru companion prin transcriere sau pachete audio PCM.
- Rezumate și rapoarte săptămânale destinate părinților.
- Generarea contextului pentru teste/profiluri multiplayer din profilurile combinate ale jucătorilor.

`GroqAI.java` include:

- Un cache LRU de răspunsuri cu 200 de intrări.
- Un TTL al cache-ului de 5 minute.
- O limită de timp de 35 de secunde pentru solicitări.
- Rotirea cheilor API atunci când acestea expiră sau întâlnesc coduri de stare pentru care solicitarea poate fi reîncercată.
- Șiruri de eroare de rezervă atunci când nu este configurată nicio cheie sau Groq nu este disponibil.

## Executarea securizată a codului

Codul elevului este executat pe server, dar nu direct în procesul backendului.

Toate căile de execuție — Python, C++, CodeWorld Python și problemele AI/ML — folosesc imagini Docker fixate și containere noi pentru fiecare rulare. Procesul Java nu mai pornește interpretorul sau compilatorul direct pe gazdă. Imaginile se construiesc prin `sh code-runners/build-images.sh`.

Politica sandboxului:

| Limită | Valoare |
| --- | --- |
| Identitate | UID/GID `65532`, fără privilegii root |
| Rețea | `--network none` |
| Sistem de fișiere | rădăcină și surse read-only; doar `/tmp` temporar |
| Privilegii | toate capabilitățile eliminate, `no-new-privileges` |
| Resurse | un CPU, memorie fixată, maximum 64 procese și 64 descriptori |
| Fișiere/ieșire | fișiere de maximum 2 MB; stdout/stderr limitate la 16 KB |
| Timp | timeout Java; containerul este eliminat forțat dacă expiră |

Seturile de date de antrenare și caracteristicile de test sunt montate în containerul AI/ML, însă etichetele ascunse rămân în procesul Java. Corectitudinea și recompensele sunt astfel stabilite exclusiv de evaluatorul serverului, nu de client sau de un răspuns LLM.

## Jocul Unity

Jocul Unity reprezintă experiența principală a elevului. Acesta include panouri de programare, insule cu teste, explorarea cursurilor comunității, provocări generate de IA, un personaj companion, mod multiplayer în rețeaua LAN și CodeWorld.

Scripturi importante:

| Script | Responsabilitate |
| --- | --- |
| `GameClient.cs` | Conectarea prin WebSocket la backend și trimiterea și primirea pachetelor criptate |
| `PauseMenuManager.cs` | Centrul interfeței, fluxul de autentificare, meniurile multiplayer și punctele de acces către CodeWorld și Quiz Island |
| `MultiplayerSessionManager.cs` | Găzduirea sesiunilor în LAN și conectarea la acestea, descoperirea sesiunilor, avatarurile la distanță, comunicarea vocală, pachetele pentru teste și sincronizarea profilurilor |
| `CodeWorldRuntime.cs` | Editorul „Your Code Controls The World” și modificarea scenei în timpul rulării |
| `MultiplayerQuizManager.cs` | Starea testului multiplayer, punctajul și cronometrarea răspunsurilor |
| `CommunityIslandMenu.cs` | Explorarea cursurilor publicate și parcurgerea testelor acestora |
| `AiChallengePad.cs` | Fluxul provocărilor personalizate generate de IA |
| `RobotCompanion.cs` | Comportamentul companionului în joc și declanșatoarele text/vocale |
| `PythonDebugPadCinematic.cs` | Provocările Python și fluxul de evaluare cu IA |
| `CodeChallengePadCinematic.cs` | Panourile de programare/depanare C++ |
| `CppQuestionPadCinematic.cs` | Panoul cu întrebări C++ cu variante multiple de răspuns |

### AI & Machine Learning Island

Insula AI/ML este salvată în scena principală și conține portaluri deschise pentru `Easy`, `Medium` și `Hard`. Catalogul este livrat de server prin pachetele `77/78`, iar soluțiile corelate prin `requestId` sunt trimise și evaluate prin `79/80`. Cele nouă probleme acoperă pregătirea datelor, regresia, clasificarea, evaluarea modelelor, rețele neuronale, NLP și bazele predicției următorului token. Progresul și recompensele sunt persistente și idempotente în `ChildMachineLearningProgress`.

Profilul rezultat este stocat sub cheia JSON `aiProfileMachineLearning`. Aplicațiile Android și iOS îl afișează separat, cu radar pentru Data Prep, Regression, Classification, Evaluation, Neural Networks și LLMs; clienții mai vechi ignoră în siguranță cheia nouă.

### CodeWorld

CodeWorld este accesibil din `PauseMenuManager -> Multiplayer -> Host Game -> Your Code Controls The World`. Jucătorul este teleportat la `CodeWorldRuntime.SpawnPosition`, iar un editor de cod care poate fi afișat sau ascuns în joc este activat.

Funcționalitățile implementate includ:

- editor de cod afișat ca suprapunere/fereastră.
- editarea comenzilor cu ajutorul tastaturii.
- crearea și manipularea obiectelor prin comenzi asemănătoare codului.
- primitive de tip cub, sferă, dreptunghi/cerc, în funcție de comenzile acceptate de `CodeWorldRuntime`.
- suport pentru bucle și interpretarea comenzilor în interpretorul CodeWorld.
- istoric local și serializarea stării.
- sincronizarea în timp real a editorului în modul multiplayer.
- cursoare denumite pentru utilizatorii la distanță.
- resincronizarea instantaneului pentru clienții care se alătură după gazdă.
- ascunderea companionului obișnuit atunci când acest lucru este potrivit pentru modul CodeWorld.

Pachetele multiplayer CodeWorld sunt pachete locale ale sesiunii Unity:

- `CodeWorldCommandPacket`
- `CodeWorldStatePacket`
- `CodeWorldEditorSyncPacket`
- `CodeWorldCursorPacket`

### Quiz Island

Quiz Island este găzduită prin meniul multiplayer. Aceasta oferă:

- opțiuni de test controlate de gazdă.
- preluarea conținutului testelor/cursurilor.
- generarea testelor pe baza profilului IA.
- colectarea răspunsurilor în modul multiplayer.
- calcularea punctajului în funcție de timpul de răspuns în `MultiplayerQuizManager`.
- încheierea întrebării după ce toți jucătorii au răspuns, urmată de o scurtă întârziere înainte de continuare.
- un context combinat al profilurilor de programare atunci când în lobby se află mai mulți jucători.

### Combinarea profilurilor multiplayer

`MultiplayerSessionManager` stochează intrări `ProgrammingProfileSnapshot` pentru jucătorii locali și cei la distanță:

- `ClientId`
- `PlayerName`
- `ChildId`
- `ChildName`
- `TotalPoints`
- `Streak`
- `CompletedTaskCount`
- `TotalTaskCount`
- `ProfileSummary`

`BuildMergedProgrammingProfileContext()` combină profilurile disponibile ale jucătorilor, astfel încât testele multiplayer generate de IA să poată ține cont de toți participanții, nu doar de gazdă.

### Voce și companion

Jocul Unity oferă suport pentru:

- răspunsuri text din partea companionului.
- pachete cu transcrierea vocii.
- pachete audio vocale PCM neprelucrate.
- chat vocal local în modul multiplayer.
- modurile vocale `AlwaysOn`, `PushToTalk`, `Muted`.
- declanșatoare contextuale pentru companion, precum reușita/eșecul unei provocări și accesarea panourilor de programare.

## Aplicațiile mobile pentru părinți

Clienții Android și iOS formează o suită de monitorizare parentală, nu doar un mecanism de autentificare. Logica de protocol comună este partajată prin Kotlin Multiplatform, iar fiecare platformă își protejează sesiunea prin mecanismul securizat nativ.

Fișiere principale:

| Fișier | Responsabilitate |
| --- | --- |
| `MainActivity.kt` | Punctul de intrare al aplicației Android |
| `ui/AuthScreen.kt` | Interfața de autentificare/înregistrare pentru părinți |
| `ui/MainDashboard.kt` | Tabloul de bord Compose, copiii, obiectivele, istoricul și setările |
| `ui/SocketViewModel.kt` | Starea WebSocket, pachetele, modelele de date și notificările |
| `socket/ClientSocket.java` | Clientul WebSocket |
| `socket/packet/Packet.java` | Componenta corespondentă pentru serializarea/criptarea pachetelor |
| `iosApp/MentoraIOS/State/MentoraLiveStore.swift` | Starea, reconectarea și fluxurile de securitate iOS |
| `shared/.../IosMentoraClientBridge.kt` | Bridge-ul Kotlin/Native pentru protocolul iOS |

Funcționalitățile implementate ale aplicației includ:

- autentificarea/înregistrarea părinților.
- autentificare în doi pași TOTP, configurare prin cod QR, coduri de recuperare și dezactivare din Setări.
- sesiuni persistente rotite, stocate prin Android Keystore sau iOS Keychain.
- o buclă de reconectare.
- un tablou de bord pentru copii, cu punctaj și stare online.
- un flux de scanare a codurilor QR pentru asocierea sesiunilor de joc.
- fotografii de profil pentru copii.
- suport pentru fotografia de profil a părintelui.
- istoricul sarcinilor.
- obiective.
- starea abonării la sesiunea în timp real.
- profiluri IA pentru fiecare limbaj și un profil general.
- rapoarte săptămânale.
- notificări de sistem la primirea activității copilului.
- personalizarea temei și modul întunecat.

## Creatorul web de cursuri

Creatorul web le permite părinților sau educatorilor să gestioneze conținutul cursurilor care apare în jocul Unity.

Fișiere importante:

| Fișier | Responsabilitate |
| --- | --- |
| `src/App.jsx` | Aplicația SPA principală, starea autentificării, tabloul de bord și editorul de cursuri |
| `src/lib/api.js` | Funcții auxiliare pentru API-ul REST |
| `src/main.jsx` | Punctul de intrare React |
| `src/styles.css` | Stilizarea Tailwind/CSS |

API REST:

| Metodă | Endpoint | Scop |
| --- | --- | --- |
| `POST` | `/api/web/auth/lookup` | Determină fluxul de autentificare/înregistrare |
| `POST` | `/api/web/auth/register` | Înregistrează părintele |
| `POST` | `/api/web/auth/login` | Autentifică părintele |
| `GET` | `/api/web/courses/mine` | Listează cursurile deținute |
| `GET` | `/api/web/courses/{courseId}` | Preia detaliile unui curs deținut |
| `POST` | `/api/web/courses` | Creează un curs |
| `PUT` | `/api/web/courses/{courseId}` | Actualizează cursul și întrebările |
| `DELETE` | `/api/web/courses/{courseId}` | Șterge cursul |
| `POST` | `/api/web/ml-problems` | Publică o problemă de cod/ML cu date ascunse |
| `GET` | `/api/web/ml-problems/mine` | Listează problemele de cod/ML deținute |
| `GET` | `/api/web/ml-problems/children/{childId}/progress` | Arată părintelui feedbackul și progresul copilului |

Validarea cursurilor în `CourseService`:

- titlul este obligatoriu.
- este necesară cel puțin o întrebare de test.
- fiecare întrebare trebuie să aibă un enunț.
- fiecare întrebare are patru variante de răspuns.
- `correctIndex` trebuie să fie în intervalul `0..3`.
- acronimul este curățat pe baza acronimului/titlului.
- rezumatul este limitat la 280 de caractere.
- dreptul de proprietate asupra cursului este verificat la citire/actualizare/ștergere.

## Cursuri, sarcini, obiective și rapoarte

### Cursuri

Cursurile sunt create în creatorul web și parcurse în Unity prin Community Island.

Câmpurile unui curs:

- titlu.
- acronim.
- limbaj.
- dificultate.
- rezumat.
- descriere.
- recompensă în puncte.
- indicator de publicare.
- întrebări de test ordonate.

`recordCourseCompletion()` urmărește încercările, ultimul punctaj, cel mai bun punctaj, numărul total de întrebări, momentul ultimei încercări, finalizarea și starea recompensei. Punctele pentru curs sunt acordate o singură dată, deoarece `rewardGranted` este verificat înainte de acordarea lor.

### Sarcini globale

Sarcinile globale sunt inițializate din `DefaultTaskType`:

| Categorie | Exemple |
| --- | --- |
| C++ introductiv | `C++ Starter Quiz: Complete All Questions` |
| C++ depanare, nivel mediu | înmulțire, sumă, verificarea parității, transmitere prin referință |
| C++ nivel avansat | `IsEven`, `MaxOfTwo`, `Square`, `Sum3`, `Factorial3` |
| Python nivel mediu | înmulțire, adunare, verificarea parității, suma dintr-o buclă |
| Python vizual, nivel avansat | linie cu bare, bară de progres, grilă pătrată, scară, model alternant |
| Puzzle-uri logice | puterea/fizica saltului, dezvăluirea insulei, dezvăluirea podului |

`TaskService.completeTask()` blochează profilul copilului pe durata tranzacției și verifică perechea copil-sarcină înainte de a crea un `CompletedTask`. Prima finalizare adaugă punctele, incrementează `game_stats["tasks_completed"]` și verifică obiectivele; trimiterile repetate devin idempotente. O constrângere unică în baza de date protejează aceeași regulă și la nivel de persistență.

### Obiective

Obiectivele sunt create de părinți pentru copii. Un obiectiv se poate baza pe:

- punctajul necesar.
- sarcina necesară.

`GoalService` verifică obiectivele după finalizarea unei sarcini și poate trimite actualizări clienților conectați.

### Provocări de la părinți

Provocările de la părinți sunt solicitări în timp real trimise din aplicația Android către sesiunea unui copil:

- `SendParentChallengePacket`
- `ParentChallengePacket`
- `ParentChallengeCompletedPacket`

Provocările active sunt păstrate în `Server.activeParentChallenges`.

### Rapoarte săptămânale

`LearningProfileService.generateWeeklyParentReport()`:

- calculează săptămâna curentă, de luni până duminică.
- colectează sarcinile finalizate în timpul săptămânii.
- citește datele profilurilor pentru C++ și Python, precum și datele profilului general.
- colectează evenimentele de învățare recente.
- construiește un prompt pentru IA destinat unui raport pentru părinți.
- folosește un raport determinist de rezervă dacă apelul către IA eșuează.

## Modelul bazei de date

Modelul entităților este implementat în `java-server/Java-Server/src/main/java/io/github/kawase/database/entity/`.

```mermaid
erDiagram
    PARENT ||--o{ CHILD : deține
    CHILD ||--o| GAME_SESSION : are
    CHILD ||--o{ COMPLETED_TASK : finalizează
    TASK ||--o{ COMPLETED_TASK : apare_în
    PARENT ||--o{ GOAL : creează
    PARENT ||--o{ PARENT_SESSION : autentifică
    CHILD ||--o{ GOAL : primește
    TASK ||--o{ GOAL : poate_impune
    PARENT ||--o{ COURSE : creează
    COURSE ||--o{ COURSE_QUIZ_QUESTION : conține
    CHILD ||--o{ CHILD_COURSE_PROGRESS : urmărește
    COURSE ||--o{ CHILD_COURSE_PROGRESS : urmărește

    PARENT {
        long id
        string email
        string passwordHash
        text profilePicture
    }

    CHILD {
        long id
        string name
        text profilePicture
        jsonb gameStats
        int totalPoints
        int streak
        date lastLoginDate
    }

    COURSE {
        long id
        string title
        string acronym
        string language
        string difficulty
        string summary
        int pointReward
        bool published
    }
```

Concepte persistente importante:

- `children.game_stats` stochează profilul de IA aflat în continuă evoluție, în format JSONB.
- `game_sessions` stochează tokenurile persistente ale sesiunilor copiilor.
- `parent_sessions` stochează hashurile tokenurilor și ale dispozitivelor, expirarea și revocarea sesiunilor părinților.
- `completed_tasks` stochează înregistrările sarcinilor finalizate, unice pentru fiecare pereche copil-sarcină.
- `goals` stochează obiectivele cu recompense create de părinți.
- `child_course_progress` stochează încercările la cursuri, punctajele și starea recompenselor.

## Note despre securitate

Protecții implementate:

- pachete WebSocket binare criptate între backend și Unity/Android.
- seed de criptare dinamic pentru fiecare pachet.
- limite stricte pentru cadre, seed, șiruri UTF-8 și date rămase în pachet.
- acreditare de protocol derivată cu SHA-256, apoi stocată pe server numai ca hash adaptiv BCrypt sărat; rândurile vechi sunt migrate la autentificare.
- TOTP conform RFC 6238, protecție la reutilizarea aceluiași pas temporal și coduri de recuperare de unică folosință.
- buget comun pe cont și cooldown pentru încercările TOTP, inclusiv între challenge-uri nou emise.
- secrete TOTP criptate cu AES-256-GCM și tokenuri de sesiune persistate numai ca hash.
- tokenuri Bearer cu TTL, legare de dispozitiv, rotație la reluare și revocare.
- autorizare pe rol pentru fiecare ID de pachet; pachetele de dezvoltare sunt dezactivate implicit.
- validarea proprietarului cursului.
- verificarea apartenenței copilului în operațiunile efectuate de părinți.
- finalizarea idempotentă a sarcinilor și unicitate în baza de date pentru perechea copil-sarcină.
- executarea codului într-un sandbox cu restricții pentru procese, memorie, fișiere, CPU și rețea.

Limitări cunoscute, vizibile în cod:

- challenge-urile și bugetele temporare 2FA sunt păstrate în memoria instanței; un deployment cu mai multe instanțe trebuie să le mute într-un store distribuit.
- cheia folosită pentru criptarea secretelor TOTP nu are încă un flux automat de rotație; aceasta trebuie păstrată stabilă și protejată prin managerul de secrete al mediului.

## Rularea proiectului

### Backend

Pentru TOTP, generați o singură cheie AES-256 și păstrați-o într-un manager de secrete. Comanda afișează o valoare Base64 care conține exact 32 de octeți aleatori:

```bash
openssl rand -base64 32
```

Configurați valoarea generată înainte de pornire:

```bash
cd java-server/Java-Server
export MENTORA_TOTP_ENCRYPTION_KEY='<valoarea-Base64-generată-o-singură-dată>'
./gradlew bootRun
```

Nu reutilizați cheia demonstrativă din workflow-ul CI și nu comiteți cheia reală. Aceeași valoare trebuie restaurată după fiecare repornire/deploy; schimbarea ei fără o migrare face imposibilă decriptarea secretelor TOTP deja înrolate. Backendul poate porni fără variabilă, dar configurarea TOTP este indisponibilă, iar conturile care au deja TOTP activat nu pot finaliza autentificarea.

Servicii disponibile:

- HTTP REST: `:8085`
- WebSocket: `:49154`

Condiții preliminare pentru backend:

- Java 21.
- PostgreSQL.
- un fișier `application.properties` configurat.
- un fișier `api-keys.json` care conține configurația cheilor API Groq.

Exemplu de fișier cu chei Groq:

```json
{
  "groq_api_keys": ["gsk_first_key", "gsk_second_key"]
}
```

### Creatorul web de cursuri

```bash
cd web-creator
npm install
npm run dev
```

Setați `VITE_API_BASE` dacă backendul nu rulează la endpointul implicit configurat.

### Aplicația Android

Deschideți `kotlin-app/` în Android Studio. Aplicația vizează versiuni moderne ale SDK-ului Android și folosește pachete WebSocket compatibile cu protocolul backendului.

### Jocul Unity

Deschideți `unity/` în Unity Hub folosind Unity `2022.3.62f3`. Actualizați adresa URL a serverului în `GameClient.cs` dacă folosiți un backend local în locul endpointului din mediul de producție.

Adresa URL implicită a backendului din codul Unity este:

```text
wss://neuro.serenityutils.club
```

## Starea actuală a testării

Testele sunt împărțite intenționat pe straturi, pentru ca verificările rapide să nu depindă de Docker, iar testele de infrastructură să folosească aceleași limite și motoare ca producția.

| Strat | Comandă locală | Ce verifică | Dependențe |
| --- | --- | --- | --- |
| Backend unit/contract | `./gradlew test` în `java-server/Java-Server/` | autorizare pe rol, TOTP/sesiuni, evaluator, validări, idempotentizarea sarcinilor, cadre binare invalide și fixture-ul protocolului | Java 21 |
| Backend + PostgreSQL | `./gradlew integrationTest` | creatorul REST de cursuri, ciclul REST TOTP și persistența separată a cursurilor/progresului pe PostgreSQL real | Java 21 și Docker |
| Docker/evaluator + golden path | `./gradlew dockerAdversarialTest` | creatorul publică o problemă ML → catalogul copilului ascunde datele private → evaluatorul Docker rulează testele ascunse → feedbackul/progresul se persistă → părintele vede actualizarea; plus izolarea adversarială, Python/C++/ML și eliminarea containerului | Docker, PostgreSQL Testcontainers și imaginile Mentora |
| Rapoarte backend | `./gradlew test integrationTest dockerAdversarialTest jacocoTestReport jacocoTestCoverageVerification` | JUnit și raport JaCoCo XML/HTML pentru straturile unitare, PostgreSQL și Docker | Java 21 și Docker |
| Android + Kotlin shared | `./gradlew :shared:testAndroidHostTest :app:testDebugUnitTest :app:assembleDebugAndroidTest` în `kotlin-app/` | contracte shared, fluxuri 2FA și fixture binar; compilează și testele instrumentate | Java 21 și Android SDK |
| Android pe dispozitiv | `./gradlew :app:connectedDebugAndroidTest` | persistența sesiunii criptate prin Android Keystore | emulator/dispozitiv Android |
| Kotlin/iOS shared | `./gradlew :shared:iosSimulatorArm64Test` | contractul Kotlin/Native și fluxurile de securitate iOS | macOS/Xcode |
| iOS nativ | `xcodebuild test ...` din exemplul de mai jos | fixture-ul Swift și integrarea aplicației cu bridge-ul shared | macOS, Xcode și XcodeGen |
| Unity EditMode | comanda Unity de mai jos | decodificarea/codificarea fixture-ului comun și limitele defensive ale protocolului | Unity `2022.3.62f3` |

Testele PostgreSQL folosesc Testcontainers cu imaginea `postgres:16-alpine`; nu există fallback H2. Dacă Docker nu este disponibil, clasele marcate `disabledWithoutDocker` sunt raportate ca omise. Testele adversariale nu fac parte din taskul Gradle `check`: invocarea explicită a `dockerAdversarialTest` este opt-in-ul local. Odată invocat, taskul eșuează dacă Docker sau una dintre imaginile Mentora lipsește, pentru a evita un rezultat fals pozitiv.

Construiți imaginile runner înaintea testelor adversariale:

```bash
cd java-server/Java-Server
sh code-runners/build-images.sh
./gradlew dockerAdversarialTest
```

Rapoartele backend sunt scrise în `build/test-results/`, `build/reports/tests/` și `build/reports/jacoco/`.

### Fixture-uri canonice pentru protocol

[`test-fixtures/protocol/v1/packets.json`](test-fixtures/protocol/v1/packets.json) conține 24 de vectori canonici cu payloadul decriptat și cadrul WebSocket criptat determinist. Fixture-ul este consumat independent de backendul Java, jocul Unity, clientul Kotlin/Android și testul nativ Swift; fiecare client verifică direcțiile și ID-urile pe care le implementează, în timp ce backendul validează întregul registru. Vectorii includ metadatele handshake v1/v2, date Unicode/binare și contractele de securitate `81`–`92`.

Generatorul de referință este independent de implementările de producție. Necesită Python 3 și pachetul `cryptography`:

```bash
python3 -m pip install cryptography
python3 test-fixtures/protocol/v1/generate_vectors.py --check
```

După o schimbare deliberată de protocol:

```bash
python3 test-fixtures/protocol/v1/generate_vectors.py --write
python3 test-fixtures/protocol/v1/generate_vectors.py --check
```

Orice modificare a ordinii/codificării câmpurilor trebuie revizuită împreună cu fixture-ul și reprezentată printr-o versiune nouă, nu prin rescrierea silențioasă a contractului v1.

### Comenzi Unity și iOS

Testele Unity pot fi rulate din `Window > General > Test Runner > EditMode` sau fără interfață:

```bash
export UNITY_EDITOR_PATH='/cale/către/Unity/Hub/Editor/2022.3.62f3/Editor/Unity'
"$UNITY_EDITOR_PATH" \
  -batchmode -nographics \
  -projectPath "$PWD/unity" \
  -runTests -testPlatform EditMode \
  -testResults "$PWD/unity-editmode-results.xml" \
  -logFile -
```

Pe macOS, testele shared și cele Swift se rulează astfel:

```bash
cd kotlin-app
./gradlew :shared:iosSimulatorArm64Test
xcodegen generate --spec iosApp/project.yml --project iosApp
xcodebuild test \
  -project iosApp/MentoraIOS.xcodeproj \
  -scheme MentoraIOS \
  -destination 'platform=iOS Simulator,name=iPhone 16 Pro,OS=latest' \
  CODE_SIGNING_ALLOWED=NO CODE_SIGNING_REQUIRED=NO CODE_SIGN_IDENTITY=""
```

### Integrare continuă

Workflow-ul [`integration-tests.yml`](.github/workflows/integration-tests.yml) construiește imaginile runner și rulează în paralel:

- testele backend unitare, PostgreSQL/Testcontainers, evaluator și sandbox adversarial, cu rezultate JUnit și JaCoCo publicate ca artefacte.
- testele host Android/Kotlin shared, urmate de testul Android Keystore instrumentat pe un emulator API 35.
- testele Unity EditMode prin GameCI, disponibile manual prin opțiunea `run_unity`; acestea nu rulează la fiecare push sau pull request.

Jobul Unity este dezactivat implicit inclusiv la pornirea manuală a workflow-ului. Dacă este solicitat explicit, necesită secretele de licențiere potrivite tipului de licență: `UNITY_LICENSE`, `UNITY_EMAIL` și `UNITY_PASSWORD` pentru Personal sau `UNITY_EMAIL`, `UNITY_PASSWORD` și `UNITY_SERIAL` pentru Professional. Workflow-ul [`ios-simulator-build.yml`](.github/workflows/ios-simulator-build.yml) rulează separat testele Kotlin/Native și Swift pe un runner macOS, publică bundle-ul `.xcresult`, apoi construiește artefactul iOS.

## Corelarea cu criteriile competiției/proiectului

Mentora îndeplinește criterii uzuale de evaluare a software-ului educațional:

- Arhitectură: sistem cu mai mulți clienți, alcătuit din backend, joc, aplicație mobilă și creator web.
- Implementare: protocol de pachete personalizat, executarea codului într-un sandbox, serviciu pentru profilul de IA și sisteme multiplayer.
- Interfață: interfața jocului, panoul de control Android și instrumentul web de creare a conținutului.
- Conținut: cursuri editabile, sarcini de programare, chestionare, provocări generate de IA și feedback în timp real.
- Evaluare și feedback: finalizarea sarcinilor, explicații oferite de IA, rezumate pentru părinți și rapoarte săptămânale.
- Originalitate: profil de IA persistent pentru fiecare elev, CodeWorld colaborativ, context de programare combinat pentru multiplayer și o buclă educațională părinte-copil în timp real.
