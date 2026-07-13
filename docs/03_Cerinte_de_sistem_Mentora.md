MENTORA – CERINȚE DE SISTEM

1. SCOP

Acest document prezintă resursele hardware, software și de rețea necesare pentru instalarea și utilizarea componentelor Mentora: server, joc Unity, aplicație Android și editor web.

2. CERINȚE COMUNE

- Conexiune de rețea între clienți și server.
- PostgreSQL disponibil pentru componenta backend.
- Acces la serviciul Groq și un fișier local `api-keys.json` configurat pentru funcțiile bazate pe inteligență artificială.
- Pentru execuția securizată a codului, serverul trebuie să permită crearea de directoare temporare și procese izolate.
- Pentru modul multiplayer LAN, toate dispozitivele Unity trebuie să fie conectate la aceeași rețea locală.

3. CERINȚE PENTRU SERVER

Hardware minim recomandat pentru demonstrație

- Procesor cu cel puțin 2 nuclee.
- Minimum 4 GB memorie RAM.
- Minimum 10 GB spațiu liber.
- Conexiune stabilă la internet și rețea locală.

Software obligatoriu

- Sistem de operare Linux, potrivit pentru izolarea proceselor.
- JDK 21.
- PostgreSQL.
- `python3` pentru execuția Python.
- `g++` pentru compilarea C++.
- `unshare`, `timeout` și shell Bash pentru izolarea proceselor.
- Gradle Wrapper inclus în proiect pentru build.

Serverul REST folosește implicit portul TCP `8085`, iar serverul WebSocket folosește portul `49154`. Valorile pot fi modificate în configurația aplicației. Pentru folosirea executorului de cod, sunt necesare capabilități Linux pentru `unshare --net --user --map-root-user`.

4. CERINȚE PENTRU JOCUL MENTORA

Pentru rulare

- **Desktop:** Windows 10/11 sau Linux compatibil cu buildul Unity, procesor cu 4 nuclee, 8 GB memorie RAM, placă video compatibilă DirectX 11/12 sau Vulkan și minimum 5 GB spațiu liber.
- **Telefon Android:** dispozitiv Android compatibil cu buildul Unity, ecran tactil, conexiune la server și resurse suficiente pentru experiența 3D.
- **VR:** headset compatibil Unity/OpenXR sau Meta Quest, controlere asociate și runtime-ul producătorului. Jocul include interacțiune prin controlere, ray pointer pentru meniu, calibrare de tracking și suport pentru hand tracking.
- **Rețea:** internet pentru server și LAN pentru multiplayer local.

Mentora poate fi rulat pe Windows, Linux, telefoane Android și dispozitive VR. Sistemul adaptează controalele pentru tastatură și mouse, touch, controlere VR și hand tracking, iar profilurile de performanță VR sunt aplicate pentru desktop și Android/Quest.

Pentru dezvoltare

- Unity Hub.
- Unity `2022.3.62f3`.
- Minimum 15 GB spațiu liber pentru proiect, cache și pachete.
- Placă video dedicată recomandată pentru editarea scenelor HDRP.

5. CERINȚE PENTRU APLICAȚIA ANDROID

- Android 7.0 sau mai nou (`minSdk 24`).
- Minimum 2 GB memorie RAM.
- Aproximativ 100 MB spațiu liber pentru aplicație și date.
- Internet sau acces la rețeaua serverului.
- Cameră pentru scanarea QR.
- Permisiune pentru notificări pe versiunile Android care o solicită.

Aplicația utilizează permisiuni pentru internet, verificarea stării rețelei, cameră și notificări, oferind conectare QR și actualizări relevante pentru părinte.

Pentru dezvoltare sunt necesare Android Studio, Android SDK compatibil, JDK configurat de Android Studio și un emulator ori dispozitiv fizic cu Android 7.0+.

6. CERINȚE PENTRU EDITORUL WEB

Utilizare

- Browser modern: Google Chrome, Mozilla Firefox, Microsoft Edge sau echivalent actualizat.
- JavaScript activat.
- Suport pentru ES modules, Fetch API, localStorage și WebSocket.
- Conexiune la serverul Mentora.

Dezvoltare și build

- Node.js compatibil cu Vite 7.
- npm pentru gestionarea pachetelor.
- React 19.
- Vite 7 pentru build.

Pentru rulare locală se folosesc `npm install` și `npm run dev`. Pentru distribuirea versiunii web statice se folosește `npm run build`.

7. CONFIGURARE DE REȚEA

- Backendul REST utilizează HTTP pe portul `8085` pentru autentificare și administrarea cursurilor din web.
- Backendul în timp real utilizează WebSocket pe portul `49154` pentru joc, Android, QR, progres și sesiuni live.
- Descoperirea sesiunilor multiplayer utilizează UDP pe portul `7776` în LAN.
- Multiplayerul local utilizează TCP pe portul `7777` pentru datele de sesiune, quiz, CodeWorld și voce.

Configurarea rețelei permite dispozitivelor din LAN să comunice pe porturile necesare pentru multiplayer. Pentru distribuire publică, proiectul poate utiliza HTTPS/WSS, certificate valide și reguli firewall dedicate.

8. CONFIGURAȚII EXTERNE ȘI DATE SENSIBILE

- Datele PostgreSQL se configurează în `application.properties` sau prin variabile de mediu.
- Cheile Groq se configurează în `api-keys.json`, pornind de la `api-keys.example.json`.
- Cheile API, parolele bazei de date și tokenurile sunt gestionate în configurația mediului.
- URL-ul backendului din joc și baza API pentru clientul web sunt configurabile pentru fiecare mediu de rulare.

9. REZUMAT

Mentora poate fi demonstrată pe un calculator Linux care rulează serverul, PostgreSQL, Python și G++, împreună cu un dispozitiv Android, un browser modern și una sau mai multe instanțe Unity. Arhitectura permite dimensionarea resurselor serverului în funcție de numărul de utilizatori simultani și de frecvența execuțiilor de cod sau a cererilor AI.
