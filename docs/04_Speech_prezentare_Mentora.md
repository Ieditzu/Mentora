MENTORA – DISCURS DE PREZENTARE PENTRU DOUĂ PERSOANE

Durată estimată: 10–12 minute

PERSOANA 1 – INTRODUCERE ȘI PROBLEMA REZOLVATĂ

Bună ziua! Proiectul nostru se numește Mentora și este un ecosistem educațional creat pentru a transforma învățarea programării într-o experiență practică, interactivă și personalizată.

Am pornit de la o problemă simplă: multe platforme educaționale îi oferă elevului aceeași succesiune de lecții și îi spun doar dacă răspunsul este corect sau greșit. Mentora merge mai departe. Elevul scrie și execută cod real, vede efectele lui într-o lume 3D, primește explicații și indicii, iar sistemul reține progresul, conceptele dificile și greșelile frecvente. Astfel, fiecare activitate contribuie la un profil individual de învățare.

Proiectul leagă trei categorii de utilizatori. Elevul învață în jocul 3D. Părintele folosește aplicația mobilă pentru a urmări progresul, obiectivele și sesiunile copilului. Profesorul sau creatorul de conținut folosește Creator-ul Web pentru a crea și publica noi cursuri și quiz-uri. Toate componentele sunt conectate printr-un server central.

PERSOANA 2 – EXPERIENȚA ELEVULUI ȘI FUNCȚIONALITĂȚILE PRINCIPALE

În joc, elevul nu este un spectator, ci factorul activ al procesului de învățare. El explorează o lume 3D formată din insule, portaluri, coding pads și experimente interactive. Poate rezolva exerciții Python și C++, poate participa la quiz-uri, poate urma cursurile publicate de comunitate și poate controla obiecte virtuale prin cod.

Un exemplu important este Code Quest Island. Insula este generată procedural și conține cinci portaluri: Easy – Build, Medium – Fix, Hard – Systems, AI Profile Quest și Free Sandbox. În misiunea Easy, elevul repară un beacon, îl poziționează corect, îi modifică dimensiunea și îl colorează în roșu. În misiunea Medium, șterge obstacolele și repară un pod prin crearea mai multor scânduri și a unui steag verde. În misiunea Hard, elimină un nucleu defect, creează un power core cyan și construiește patru elemente de protecție. Portalul AI generează o provocare adaptată profilului copilului, iar în Free Sandbox elevul poate crea liber obiecte, le poate muta, redimensiona și colora prin Python. Insula poate fi generată direct din meniul de pauză pentru o sesiune nouă de freestyle.

În CodeWorld, codul Python controlează lumea 3D. Elevul vede imediat legătura dintre o instrucțiune și rezultatul ei vizual, iar în modul LAN mai mulți jucători pot lucra în același editor, cu sincronizarea comenzilor, obiectelor și cursorilor.

Quiz Island permite evaluare individuală sau multiplayer, iar scorul ia în calcul atât corectitudinea, cât și timpul de răspuns. Community Island încarcă direct cursurile publicate din Creator-ul Web. Rocket Landing este un experiment virtual în care elevul configurează și controlează o rachetă pentru aterizare, aplicând programarea într-o simulare vizuală.

În aplicația mobilă, părintele își conectează copilul la joc prin scanarea unui cod QR. Apoi poate vedea starea online, punctele, istoricul activităților, obiectivele, profilurile AI și raportul săptămânal. Poate monitoriza o sesiune activă și poate trimite copilului o provocare personalizată, iar finalizarea este transmisă înapoi în aplicație.

PERSOANA 1 – ARHITECTURA ȘI TEHNOLOGIILE ALESE

Din punct de vedere arhitectural, Mentora folosește un model cu mai mulți clienți specializați și un backend central. Am ales tehnologia potrivită pentru responsabilitatea fiecărei componente.

Jocul este realizat în Unity 2022.3, folosind C# și HDRP. Unity ne oferă randare 3D, fizică, interacțiune, simulări și suport pentru desktop, telefon și realitate virtuală. Aplicația parentală folosește Kotlin și Jetpack Compose, potrivite pentru o interfață mobilă modernă și reactivă, împreună cu o componentă Kotlin Multiplatform pentru funcțiile comune destinate iOS. Creator-ul Web este construit cu React, Vite, Tailwind CSS și Framer Motion, deoarece avem nevoie de o interfață rapidă, modulară și adaptabilă pentru administrarea conținutului.

Serverul folosește Java 21, Spring Boot, Spring Data JPA și Hibernate. Aceste tehnologii oferă o bază solidă pentru autentificare, validare, servicii, execuție de cod și comunicarea cu mai mulți clienți. PostgreSQL păstrează utilizatorii, cursurile, progresul, obiectivele și sesiunile. Pentru funcțiile inteligente folosim Groq și un model LLaMA, izolate într-un serviciu dedicat, astfel încât furnizorul AI să poată fi schimbat sau extins fără rescrierea întregii aplicații.

Comunicarea în timp real dintre Unity, aplicația mobilă și server se face prin WebSocket, iar Creator-ul Web folosește REST pentru operațiile de creare, citire, actualizare și ștergere. Multiplayerul Unity are un strat separat pentru rețeaua locală: UDP pentru descoperirea sesiunii și TCP pentru sincronizarea jocului, quiz-urilor, vocii și CodeWorld.

PERSOANA 2 – PROIECTAREA ARHITECTURALĂ ȘI CALITATEA IMPLEMENTĂRII

Arhitectura este împărțită pe straturi și responsabilități. În backend, entitățile bazei de date, repository-urile, serviciile, protocolul, controllerele web, execuția de cod și integrarea AI sunt separate. Interfața nu accesează direct baza de date, iar logica de domeniu nu depinde de un anumit ecran.

Am folosit programare orientată pe obiecte, încapsulare, compoziție, evenimente, programare asincronă și interfețe reactive. De exemplu, protocolul poate fi extins prin adăugarea unui nou tip de pachet și a handlerului corespunzător. În Unity, clase precum GameClient, CodeWorldRuntime, CodeWorldQuestIsland, MultiplayerQuizManager, CommunityIslandMenu și RobotCompanion au responsabilități distincte. În web, formularele și ecranele sunt împărțite în componente reutilizabile, iar în aplicația mobilă starea și comunicarea sunt centralizate în ViewModel.

Această organizare face clasele și modulele mai ușor de refolosit, testat și extins. Numele variabilelor și metodelor descriu scopul lor, codul păstrează un stil consecvent, iar fluxurile importante și metodele publice sunt documentate. Complexitatea tehnică este dată de integrarea a patru platforme, două protocoale de comunicare, execuția securizată a două limbaje, inteligența artificială, lumea 3D și sincronizarea multiplayer în timp real.

PERSOANA 1 – PORTABILITATE, INTERFAȚĂ ȘI INTERNAȚIONALIZARE

Mentora este proiectată pentru mai multe dispozitive. Jocul Unity poate fi construit pentru Windows, Linux, Android, iOS și dispozitive VR compatibile Unity, OpenXR sau Meta Quest. Controalele se adaptează pentru tastatură și mouse, ecran tactil, controlere VR și hand tracking. Aplicația parentală este disponibilă pentru Android și iOS, Creator-ul Web rulează într-un browser modern, iar backendul poate fi instalat pe un sistem Linux.

Interfața este adaptată fiecărui utilizator și păstrează un aspect vizual coerent și plăcut: elevul primește o lume 3D clară și meniuri intuitive, părintele primește un dashboard cu informațiile esențiale, iar creatorul primește un editor organizat pe pași. Layouturile web și mobile se adaptează la rezoluții diferite, iar meniurile jocului includ suport pentru desktop, touch și VR. Navigarea este consecventă, acțiunile au denumiri clare, iar Rudolf poate ghida elevul către activitatea dorită.

Întregul ecosistem oferă internaționalizare. Jocul Unity, aplicația mobilă și Creator-ul Web pot afișa interfața în română, engleză, franceză și germană. Limba se schimbă din interfață, alegerea este memorată și textele sunt actualizate imediat. Sistemele de localizare sunt centralizate, astfel încât o limbă nouă poate fi adăugată prin extinderea catalogului, fără rescrierea meniurilor. Textele au fost formulate și verificate pentru exprimare clară și corectitudine gramaticală.

PERSOANA 2 – RUDOLF, PERSONALIZAREA ȘI FEEDBACKUL

Rudolf este companionul educațional care unește explorarea, profilul elevului și inteligența artificială. El poate ghida copilul către insulele Python, C++, Code Quest, Quiz, Community și CodeWorld. Comunică prin mesaje și voce, folosește text-to-speech și se orientează vizual către elev sau către punctul de interes.

Rudolf are acces contextual la profilul copilului despre care vorbește: progresul la Python și C++, răspunsurile corecte și greșite, conceptele exersate, indiciile utilizate, punctele, obiectivele și provocările finalizate. Pe baza acestora, poate explica o greșeală pe înțelesul elevului, poate aprecia evoluția și poate recomanda activitatea următoare.

Evaluarea nu se limitează la o notă. Pentru cod, elevul primește rezultatul execuției, validarea cerințelor, indicii și explicații. Pentru quiz-uri, vede dacă răspunsul este corect și primește explicația asociată. Pentru Code Quest, fiecare obiectiv este verificat separat: existență, poziție, dimensiune, culoare sau număr de obiecte. Rezultatele sunt salvate în istoricul și profilul elevului, astfel încât feedbackul viitor să țină cont de activitatea anterioară.

Conținutul educațional poate fi actualizat direct din program. Creatorul poate adăuga, edita, publica și șterge cursuri, poate stabili limbajul, dificultatea și recompensa și poate introduce întrebări cu patru variante, răspuns corect și explicație. Serverul validează aceste informații, iar cursurile publicate devin disponibile în Community Island. Conținutul de programare folosește concepte și rezultate verificabile, iar răspunsurile corecte și explicațiile quiz-urilor sunt definite explicit, pentru a păstra corectitudinea științifică și tehnică a informațiilor educaționale.

PERSOANA 1 – SECURITATE, TESTARE ȘI CONTROLUL VERSIUNILOR

Securitatea a fost tratată la nivel de transport, acces și execuție. Pachetele WebSocket sunt criptate cu AES/CBC și folosesc un seed dinamic pentru fiecare mesaj. Serverul validează dimensiunile pachetelor și starea de autentificare înainte de operațiile protejate. Creator-ul Web folosește tokenuri Bearer cu expirare și verifică proprietarul cursului, iar relația dintre părinte și copil este verificată înainte de accesarea datelor.

Codul Python și C++ trimis de elev nu este executat direct în procesul serverului. Pentru fiecare rulare este creat un director temporar, sunt aplicate limite pentru memorie, procesor, fișiere și numărul de procese, accesul la rețea este dezactivat, există timeout, iar fișierele temporare sunt șterse. Cheile AI, parolele și configurațiile de mediu sunt păstrate separat de codul sursă.

Testarea include teste unitare și de instrumentare pentru aplicația Android, verificări de compilare și scenarii funcționale și de integrare. Sunt verificate autentificarea, conectarea QR, reluarea sesiunii, soluțiile Python și C++ corecte și incorecte, explicațiile AI, actualizarea profilului, operațiile asupra cursurilor, quiz-urile, obiectivele, sesiunile live și multiplayerul dintre două instanțe Unity. Înaintea distribuirii sunt verificate buildurile și consola fiecărei componente, astfel încât versiunea prezentată să ruleze fără erori de compilare și fără erori blocante în fluxurile demonstrate.

Dezvoltarea este gestionată cu Git. Istoricul modificărilor arată evoluția proiectului, permite colaborarea între membri, separarea lucrului pe componente și revenirea controlată la versiuni anterioare. Fișierele sensibile nu sunt incluse în repository, iar configurațiile au exemple separate.

PERSOANA 2 – ORIGINALITATE, MATURITATE ȘI DOCUMENTAȚIE

Originalitatea Mentora nu constă într-o singură funcție, ci în legătura dintre toate componentele. Programarea produce efecte vizibile într-un joc 3D. Inteligența artificială nu oferă doar răspunsuri generale, ci folosește profilul persistent al elevului. Părintele poate urmări progresul în timp real și poate trimite obiective sau provocări. Creatorul poate publica un curs care apare apoi în lumea elevului. CodeWorld permite programare și colaborare în aceeași scenă, iar Rudolf transformă datele de progres într-un ghid personal și vocal.

Proiectul se află într-un stadiu matur și poate fi distribuit pe platformele vizate. Există fluxuri complete pentru conturi, autentificare QR, profiluri de copii, execuție Python și C++, quiz-uri, cursuri, obiective, rapoarte, AI, multiplayer, voce și localizare. Componentele au configurații separate pentru mediul de rulare și instrucțiuni de build și instalare.

Documentația proiectului este împărțită în trei documente principale. Descrierea proiectului prezintă scopul, utilizatorii și funcționalitățile. Documentația tehnică explică arhitectura, tehnologiile și motivele alegerii lor, protocolul, securitatea, testarea, instalarea și utilizarea. Cerințele de sistem prezintă separat resursele hardware, software și de rețea pentru server, joc, aplicația mobilă și Creator-ul Web. Prin urmare, documentația conține atât prezentarea generală, cât și ghidul de instalare și utilizare, arhitectura și justificarea tehnologiilor.

PERSOANA 1 – CONCLUZIE

În concluzie, Mentora este o platformă educațională completă, modulară, sigură și multiplatformă. Ea combină programarea practică, simularea 3D, evaluarea automată, inteligența artificială, colaborarea și implicarea părintelui într-o experiență unitară.

PERSOANA 2 – ÎNCHEIERE

Prin Mentora, elevul nu doar citește despre programare, ci scrie, testează, observă, corectează și construiește. Vă mulțumim și vă invităm să urmăriți demonstrația proiectului nostru.
