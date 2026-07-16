using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Global runtime localization for Mentora's legacy uGUI menus.
/// The selected language is stored locally and every registered menu label is refreshed immediately.
/// </summary>
public enum MentoraLanguage
{
    Romanian = 0,
    English = 1,
    French = 2,
    German = 3
}

public sealed class MentoraLocalization : MonoBehaviour
{
    private const string LanguagePreferenceKey = "Mentora.Language";
    private const float ScanIntervalSeconds = 0.75f;

    private static readonly Dictionary<string, string> RomanianByEnglish =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "PAUSED", "PAUZĂ" },
            { "Not logged in", "Neautentificat" },
            { "Generate QR Login", "Generează autentificarea QR" },
            { "Log Out", "Deconectare" },
            { "Quick Actions", "Acțiuni rapide" },
            { "Resume", "Continuă" },
            { "View Goals", "Vezi obiectivele" },
            { "Settings", "Setări" },
            { "Multiplayer", "Multiplayer" },
            { "AI & ML Island", "Insula AI și ML" },
            { "Quit Game", "Ieși din joc" },
            { "Dev Options", "Opțiuni dezvoltator" },
            { "SETTINGS", "SETĂRI" },
            { "Back", "Înapoi" },
            { "DEV OPTIONS", "OPȚIUNI DEZVOLTATOR" },
            { "Browse and enter any child profile from the server", "Alege și deschide un profil de copil de pe server" },
            { "Server", "Server" },
            { "Create Profile", "Creează profil" },
            { "Refresh", "Reîmprospătează" },
            { "Port", "Port" },
            { "Official", "Oficial" },
            { "Local", "Local" },
            { "MULTIPLAYER", "MULTIPLAYER" },
            { "Session", "Sesiune" },
            { "Host Game", "Găzduiește joc" },
            { "Join Game", "Alătură-te jocului" },
            { "Friends / LAN", "Prieteni / LAN" },
            { "JOIN GAME", "ALĂTURĂ-TE JOCULUI" },
            { "Host IP", "IP gazdă" },
            { "Port: 7777 (auto)", "Port: 7777 (automat)" },
            { "Join", "Alătură-te" },
            { "Cancel", "Anulează" },
            { "FRIENDS ON LAN", "PRIETENI ÎN LAN" },
            { "Looking for hosted games...", "Se caută jocuri găzduite..." },
            { "Manual IP", "IP manual" },
            { "Close", "Închide" },
            { "Disconnect", "Deconectează" },
            { "Offline", "Deconectat" },
            { "Your Name", "Numele tău" },
            { "Enter your name", "Introdu numele" },
            { "Model, microphone, and voice mode are in Settings.", "Modelul, microfonul și modul vocal sunt în Setări." },
            { "Open Settings", "Deschide setările" },
            { "Quiz Options", "Opțiuni quiz" },
            { "HOST GAME", "GĂZDUIEȘTE JOC" },
            { "Choose which multiplayer mode to host:", "Alege modul multiplayer pe care îl găzduiești:" },
            { "Local Island", "Insula locală" },
            { "Host on the main island. Players spawn at the default location.", "Găzduiește pe insula principală. Jucătorii apar în poziția implicită." },
            { "Quiz Island", "Insula quiz" },
            { "Host on the Quiz Island. All players will spawn there.", "Găzduiește pe Insula quiz. Toți jucătorii apar acolo." },
            { "Code Quest Island", "Insula Code Quest" },
            { "Host the Code Quest hub. Pick Easy, Medium, Hard, AI Profile, or Free Sandbox portals there.", "Găzduiește hubul Code Quest. Alege portalurile Ușor, Mediu, Greu, Profil AI sau Sandbox liber." },
            { "QUIZ OPTIONS", "OPȚIUNI QUIZ" },
            { "Quiz Controls", "Control quiz" },
            { "↻  Fetch Quizzes", "↻  Încarcă quiz-uri" },
            { "✦  AI Profile Quiz", "✦  Quiz profil AI" },
            { "▶  Start Quiz", "▶  Începe quiz-ul" },
            { "Press Fetch to load quizzes.", "Apasă Încarcă pentru a prelua quiz-urile." },
            { "PARENT GOALS", "OBIECTIVELE PĂRINTELUI" },
            { "No hosted LAN games found yet.", "Nu a fost găsit încă niciun joc LAN găzduit." },
            { "Ask your friend to host a game, then press Refresh.\nManual IP is still available.", "Cere-i prietenului să găzduiască un joc, apoi apasă Reîmprospătează.\nIP-ul manual rămâne disponibil." },
            { "No child profiles found on the server.", "Nu au fost găsite profiluri de copii pe server." },
            { "Log in to see goals", "Autentifică-te pentru a vedea obiectivele" },
            { "No goals set by parent yet", "Părintele nu a stabilit încă obiective" },
            { "Reward: ", "Recompensă: " },
            { "Sensitivity", "Sensibilitate" },
            { "Lower = steadier aim, higher = faster turn.", "Mai mică = țintire stabilă, mai mare = întoarcere rapidă." },
            { "Player + Voice", "Jucător și voce" },
            { "Optional OpenAI TTS Key", "Cheie OpenAI TTS opțională" },
            { "Used by multiplayer voice and Rudolf.", "Folosită de vocea multiplayer și de Rudolf." },
            { "Language: Romanian", "Limbă: română" },
            { "Language: English", "Limbă: engleză" },
            { "Model: Girl", "Model: fată" },
            { "Mode: Always On", "Mod: mereu activ" },
            { "Rudolf: Always On", "Rudolf: mereu activ" },
            { "Mic: Default", "Microfon: implicit" },
            { "Rudolf Path Lines: Off", "Linii traseu Rudolf: oprite" },
            { "Rudolf Path Lines: On", "Linii traseu Rudolf: pornite" },
            { "Generating...", "Se generează..." },
            { "Scan the QR code below", "Scanează codul QR de mai jos" },
            { "LOGIN FAILED / EXPIRED", "AUTENTIFICARE EȘUATĂ / EXPIRATĂ" },
            { "Loading profiles...", "Se încarcă profilurile..." },
            { "Switching profile...", "Se schimbă profilul..." },
            { "Switching...", "Se schimbă..." },
            { "Current: ", "Curent: " },
            { "day streak", "zile consecutive" },
            { "tasks", "sarcini" },
            { "Community", "Comunitate" },
            { "Community Courses", "Cursuri comunitare" },
            { "✕ Close", "✕ Închide" },
            { "Start", "Începe" },
            { "Option 0", "Opțiunea 0" },
            { "Option 1", "Opțiunea 1" },
            { "Option 2", "Opțiunea 2" },
            { "Option 3", "Opțiunea 3" },
            { "Enroll Now", "Înscrie-te acum" },
            { "Course Results", "Rezultatele cursului" },
            { "Finish & Claim Reward", "Finalizează și revendică recompensa" },
            { "YOUR CODE CONTROLS THE WORLD", "CODUL TĂU CONTROLEAZĂ LUMEA" },
            { "STOP", "OPREȘTE" },
            { "RUN", "RULEAZĂ" },
            { "Press Ctrl+Enter to run Python on the server. Press Esc or STOP to stop waiting. Press ` to hide. Example:", "Apasă Ctrl+Enter pentru a rula Python pe server. Apasă Esc sau OPREȘTE pentru a anula așteptarea. Apasă ` pentru a ascunde. Exemplu:" },
            { "Type Python code here...", "Scrie cod Python aici..." },
            { "History: none", "Istoric: gol" },
            { "CODE ISLAND AI", "AI INSULA CODULUI" },
            { "Ask about Code Island Python, mentora_world functions, vectors, colors, loops, or how to control objects.", "Întreabă despre Python pentru Insula Codului, funcțiile mentora_world, vectori, culori, bucle sau controlul obiectelor." },
            { "Ask about Code Island Python... Press Enter to send.", "Întreabă despre Python pentru Insula Codului... Apasă Enter pentru a trimite." },
            { "CODE QUEST", "MISUNE DE COD" },
            { "VERIFY", "VERIFICĂ" },
            { "RESET", "RESETEAZĂ" },
            { "SANDBOX", "SANDBOX" },
            { "ROCKET CODE", "CODUL RACHETEI" },
            { "Running…", "Se rulează…" },
            { "Analysing your learning profile…", "Se analizează profilul tău de învățare…" },
            { "✓  CORRECT!", "✓  CORECT!" },
            { "✗  INCORRECT", "✗  INCORECT" },
            { "Press Fetch Courses to load community quizzes.", "Apasă Încarcă pentru a prelua quiz-urile comunității." },
            { "Fetching community quizzes from the server...", "Se preiau quiz-urile comunității de pe server..." },
            { "Game client is not available in this scene.", "Clientul jocului nu este disponibil în această scenă." },
            { "Could not connect to the server.", "Nu s-a putut realiza conexiunea la server." },
            { "Fetch failed: {0}", "Preluarea a eșuat: {0}" },
            { "Unknown error.", "Eroare necunoscută." },
            { "No published community quizzes were returned.", "Nu au fost găsite quiz-uri comunitare publicate." },
            { "Course details could not be loaded.", "Detaliile cursului nu au putut fi încărcate." },
            { "Course completed successfully!", "Curs finalizat cu succes!" },
            { "Failed to submit course completion.", "Trimiterea finalizării cursului a eșuat." },
            { "Loading course details...", "Se încarcă detaliile cursului..." },
            { "Failed to load course: {0}", "Încărcarea cursului a eșuat: {0}" },
            { "Submitting score...", "Se trimite scorul..." },
            { "Failed to submit score: {0}", "Trimiterea scorului a eșuat: {0}" },
            { "{0} of {1}", "{0} din {1}" },
            { "Question {0} of {1}", "Întrebarea {0} din {1}" },
            { "Language", "Limbă" },
            { "Difficulty", "Dificultate" },
            { "Questions", "Întrebări" },
            { "Reward", "Recompensă" },
            { "[COMPLETED]", "[FINALIZAT]" },
            { "Congratulations!", "Felicitări!" },
            { "Your final score is", "Scorul tău final este" },
            { "Question {0} of {1}  |  {2}s  |  Press 1-4 to answer", "Întrebarea {0} din {1}  |  {2}s  |  Apasă 1-4 pentru răspuns" },
            { "Time's up! See the correct answer above.", "Timpul a expirat! Vezi răspunsul corect mai sus." },
            { "{0}.  {1}   {2} pts", "{0}.  {1}   {2} pct" },
            { "{0} player in the session", "{0} jucător în sesiune" },
            { "{0} players in the session", "{0} jucători în sesiune" },
            { "Quiz is starting…", "Quiz-ul începe…" },
            { "Get ready!", "Pregătește-te!" },
            { "Quiz Over! Final Scores:", "Quiz încheiat! Scoruri finale:" },
            { "▶  Run", "▶  Rulează" },
            { "✕  Exit", "✕  Ieșire" },
            { "Challenge from parent", "Provocare de la părinte" },
            { "Try one more challenge tonight.", "Încearcă încă o provocare în seara aceasta." }
        };

    // French and German deliberately use the English source keys as a stable catalog ID.
    // Text without a translation safely falls back to English.
    private static readonly Dictionary<string, string> FrenchByEnglish =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "PAUSED", "PAUSE" }, { "Not logged in", "Non connecté" }, { "Generate QR Login", "Générer la connexion QR" },
            { "Log Out", "Se déconnecter" }, { "Quick Actions", "Actions rapides" }, { "Resume", "Reprendre" },
            { "View Goals", "Voir les objectifs" }, { "Settings", "Paramètres" }, { "Multiplayer", "Multijoueur" },
            { "Quit Game", "Quitter le jeu" }, { "Dev Options", "Options développeur" }, { "SETTINGS", "PARAMÈTRES" },
            { "Back", "Retour" }, { "DEV OPTIONS", "OPTIONS DÉVELOPPEUR" }, { "Create Profile", "Créer un profil" },
            { "Refresh", "Actualiser" }, { "Port", "Port" }, { "Official", "Officiel" }, { "Local", "Local" },
            { "MULTIPLAYER", "MULTIJOUEUR" }, { "Session", "Session" }, { "Host Game", "Héberger une partie" },
            { "Join Game", "Rejoindre une partie" }, { "Friends / LAN", "Amis / LAN" }, { "JOIN GAME", "REJOINDRE UNE PARTIE" },
            { "AI & ML Island", "Île IA et ML" },
            { "Host IP", "IP de l'hôte" }, { "Port: 7777 (auto)", "Port : 7777 (auto)" }, { "Join", "Rejoindre" },
            { "Cancel", "Annuler" }, { "FRIENDS ON LAN", "AMIS SUR LE LAN" }, { "Manual IP", "IP manuelle" },
            { "Close", "Fermer" }, { "Disconnect", "Déconnecter" }, { "Offline", "Hors ligne" },
            { "Your Name", "Votre nom" }, { "Enter your name", "Saisissez votre nom" }, { "Open Settings", "Ouvrir les paramètres" },
            { "Quiz Options", "Options du quiz" }, { "HOST GAME", "HÉBERGER UNE PARTIE" }, { "Local Island", "Île locale" },
            { "Quiz Island", "Île du quiz" }, { "Code Quest Island", "Île Code Quest" }, { "QUIZ OPTIONS", "OPTIONS DU QUIZ" },
            { "Quiz Controls", "Contrôles du quiz" }, { "↻  Fetch Quizzes", "↻  Charger les quiz" },
            { "✦  AI Profile Quiz", "✦  Quiz de profil IA" }, { "▶  Start Quiz", "▶  Démarrer le quiz" },
            { "PARENT GOALS", "OBJECTIFS DES PARENTS" }, { "Log in to see goals", "Connectez-vous pour voir les objectifs" },
            { "No goals set by parent yet", "Aucun objectif défini par le parent" }, { "Reward: ", "Récompense : " },
            { "Sensitivity", "Sensibilité" }, { "Player + Voice", "Joueur et voix" }, { "Language: Romanian", "Langue : roumain" },
            { "Language: English", "Langue : anglais" }, { "Language: French", "Langue : français" }, { "Language: German", "Langue : allemand" },
            { "Model: Girl", "Modèle : fille" }, { "Mode: Always On", "Mode : toujours actif" },
            { "Mic: Default", "Micro : par défaut" }, { "Generating...", "Génération..." }, { "Scan the QR code below", "Scannez le code QR ci-dessous" },
            { "Loading profiles...", "Chargement des profils..." }, { "Switching...", "Changement..." }, { "Current: ", "Actuel : " },
            { "day streak", "jours consécutifs" }, { "tasks", "tâches" }, { "Community", "Communauté" },
            { "Community Courses", "Cours communautaires" }, { "✕ Close", "✕ Fermer" }, { "Start", "Démarrer" },
            { "Enroll Now", "S'inscrire maintenant" }, { "Course Results", "Résultats du cours" },
            { "Finish & Claim Reward", "Terminer et obtenir la récompense" }, { "STOP", "ARRÊTER" }, { "RUN", "EXÉCUTER" },
            { "History: none", "Historique : aucun" }, { "VERIFY", "VÉRIFIER" }, { "RESET", "RÉINITIALISER" },
            { "Running…", "Exécution…" }, { "✓  CORRECT!", "✓  CORRECT !" }, { "✗  INCORRECT", "✗  INCORRECT" },
            { "Language", "Langue" }, { "Difficulty", "Difficulté" }, { "Questions", "Questions" }, { "Reward", "Récompense" },
            { "Congratulations!", "Félicitations !" }, { "Your final score is", "Votre score final est" },
            { "Get ready!", "Préparez-vous !" }, { "Quiz Over! Final Scores:", "Quiz terminé ! Scores finaux :" },
            { "▶  Run", "▶  Exécuter" }, { "✕  Exit", "✕  Quitter" }, { "Challenge from parent", "Défi du parent" }
        };

    private static readonly Dictionary<string, string> GermanByEnglish =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "PAUSED", "PAUSIERT" }, { "Not logged in", "Nicht angemeldet" }, { "Generate QR Login", "QR-Anmeldung erstellen" },
            { "Log Out", "Abmelden" }, { "Quick Actions", "Schnellaktionen" }, { "Resume", "Fortsetzen" },
            { "View Goals", "Ziele anzeigen" }, { "Settings", "Einstellungen" }, { "Multiplayer", "Mehrspieler" },
            { "Quit Game", "Spiel beenden" }, { "Dev Options", "Entwickleroptionen" }, { "SETTINGS", "EINSTELLUNGEN" },
            { "Back", "Zurück" }, { "DEV OPTIONS", "ENTWICKLEROPTIONEN" }, { "Create Profile", "Profil erstellen" },
            { "Refresh", "Aktualisieren" }, { "Port", "Port" }, { "Official", "Offiziell" }, { "Local", "Lokal" },
            { "MULTIPLAYER", "MEHRSPIELER" }, { "Session", "Sitzung" }, { "Host Game", "Spiel hosten" },
            { "Join Game", "Spiel beitreten" }, { "Friends / LAN", "Freunde / LAN" }, { "JOIN GAME", "SPIEL BEITRETEN" },
            { "AI & ML Island", "KI- & ML-Insel" },
            { "Host IP", "Host-IP" }, { "Port: 7777 (auto)", "Port: 7777 (automatisch)" }, { "Join", "Beitreten" },
            { "Cancel", "Abbrechen" }, { "FRIENDS ON LAN", "FREUNDE IM LAN" }, { "Manual IP", "Manuelle IP" },
            { "Close", "Schließen" }, { "Disconnect", "Trennen" }, { "Offline", "Offline" },
            { "Your Name", "Dein Name" }, { "Enter your name", "Gib deinen Namen ein" }, { "Open Settings", "Einstellungen öffnen" },
            { "Quiz Options", "Quizoptionen" }, { "HOST GAME", "SPIEL HOSTEN" }, { "Local Island", "Lokale Insel" },
            { "Quiz Island", "Quiz-Insel" }, { "Code Quest Island", "Code-Quest-Insel" }, { "QUIZ OPTIONS", "QUIZOPTIONEN" },
            { "Quiz Controls", "Quizsteuerung" }, { "↻  Fetch Quizzes", "↻  Quiz laden" },
            { "✦  AI Profile Quiz", "✦  KI-Profilquiz" }, { "▶  Start Quiz", "▶  Quiz starten" },
            { "PARENT GOALS", "ELTERNZIELE" }, { "Log in to see goals", "Melde dich an, um Ziele zu sehen" },
            { "No goals set by parent yet", "Noch keine Elternziele festgelegt" }, { "Reward: ", "Belohnung: " },
            { "Sensitivity", "Empfindlichkeit" }, { "Player + Voice", "Spieler und Stimme" }, { "Language: Romanian", "Sprache: Rumänisch" },
            { "Language: English", "Sprache: Englisch" }, { "Language: French", "Sprache: Französisch" }, { "Language: German", "Sprache: Deutsch" },
            { "Model: Girl", "Modell: Mädchen" }, { "Mode: Always On", "Modus: Immer aktiv" },
            { "Mic: Default", "Mikrofon: Standard" }, { "Generating...", "Wird erstellt..." }, { "Scan the QR code below", "Scanne den QR-Code unten" },
            { "Loading profiles...", "Profile werden geladen..." }, { "Switching...", "Wird gewechselt..." }, { "Current: ", "Aktuell: " },
            { "day streak", "Tage in Folge" }, { "tasks", "Aufgaben" }, { "Community", "Community" },
            { "Community Courses", "Community-Kurse" }, { "✕ Close", "✕ Schließen" }, { "Start", "Starten" },
            { "Enroll Now", "Jetzt anmelden" }, { "Course Results", "Kursergebnisse" },
            { "Finish & Claim Reward", "Abschließen und Belohnung erhalten" }, { "STOP", "STOPP" }, { "RUN", "AUSFÜHREN" },
            { "History: none", "Verlauf: keiner" }, { "VERIFY", "PRÜFEN" }, { "RESET", "ZURÜCKSETZEN" },
            { "Running…", "Wird ausgeführt…" }, { "✓  CORRECT!", "✓  RICHTIG!" }, { "✗  INCORRECT", "✗  FALSCH" },
            { "Language", "Sprache" }, { "Difficulty", "Schwierigkeit" }, { "Questions", "Fragen" }, { "Reward", "Belohnung" },
            { "Congratulations!", "Glückwunsch!" }, { "Your final score is", "Dein Endergebnis ist" },
            { "Get ready!", "Mach dich bereit!" }, { "Quiz Over! Final Scores:", "Quiz beendet! Endergebnisse:" },
            { "▶  Run", "▶  Ausführen" }, { "✕  Exit", "✕  Beenden" }, { "Challenge from parent", "Herausforderung vom Elternteil" }
        };

    private static MentoraLocalization instance;
    private static MentoraLanguage currentLanguage;
    private float nextScanTime;

    public static event Action<MentoraLanguage> LanguageChanged;

    public static MentoraLanguage CurrentLanguage => currentLanguage;
    public static bool IsRomanian => currentLanguage == MentoraLanguage.Romanian;
    public static string CurrentLanguageLabel => currentLanguage switch
    {
        MentoraLanguage.Romanian => "Romanian",
        MentoraLanguage.French => "French",
        MentoraLanguage.German => "German",
        _ => "English"
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        currentLanguage = (MentoraLanguage)PlayerPrefs.GetInt(LanguagePreferenceKey, (int)MentoraLanguage.Romanian);
        if (instance != null)
        {
            return;
        }

        GameObject root = new GameObject("MentoraLocalization");
        instance = root.AddComponent<MentoraLocalization>();
        DontDestroyOnLoad(root);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        if (Time.unscaledTime < nextScanTime)
        {
            return;
        }

        nextScanTime = Time.unscaledTime + ScanIntervalSeconds;
        RegisterKnownSceneTexts();
    }

    public static void SetLanguage(MentoraLanguage language)
    {
        currentLanguage = language;
        PlayerPrefs.SetInt(LanguagePreferenceKey, (int)language);
        PlayerPrefs.Save();
        LanguageChanged?.Invoke(language);

        if (instance != null)
        {
            instance.RegisterKnownSceneTexts();
            instance.RefreshAllRegisteredTexts();
        }
    }

    public static string Localize(string englishText)
    {
        if (string.IsNullOrEmpty(englishText))
        {
            return englishText;
        }

        Dictionary<string, string> catalog = currentLanguage switch
        {
            MentoraLanguage.Romanian => RomanianByEnglish,
            MentoraLanguage.French => FrenchByEnglish,
            MentoraLanguage.German => GermanByEnglish,
            _ => null
        };

        return catalog != null && catalog.TryGetValue(englishText, out string translated) ? translated : englishText;
    }

    public static string Format(string englishFormat, params object[] arguments)
    {
        return string.Format(Localize(englishFormat), arguments);
    }

    public static void Register(Text text, string englishText)
    {
        if (text == null)
        {
            return;
        }

        MentoraLocalizedText localized = text.GetComponent<MentoraLocalizedText>();
        if (localized == null)
        {
            localized = text.gameObject.AddComponent<MentoraLocalizedText>();
        }

        localized.SetSource(englishText);
    }

    public static void SetText(Text text, string englishText)
    {
        Register(text, englishText);
    }

    /// <summary>Localizes a legacy 3D TextMesh label, used by world-space puzzle objects.</summary>
    public static void SetText(TextMesh text, string englishText)
    {
        if (text == null)
        {
            return;
        }

        MentoraLocalizedWorldText localized = text.GetComponent<MentoraLocalizedWorldText>();
        if (localized == null)
        {
            localized = text.gameObject.AddComponent<MentoraLocalizedWorldText>();
        }

        localized.SetSource(englishText);
    }

    private void RegisterKnownSceneTexts()
    {
        Text[] texts = FindObjectsOfType<Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            Text text = texts[i];
            if (text == null || text.GetComponent<MentoraLocalizedText>() != null)
            {
                continue;
            }

            string value = text.text;
            if (RomanianByEnglish.ContainsKey(value) || FrenchByEnglish.ContainsKey(value) || GermanByEnglish.ContainsKey(value))
            {
                Register(text, value);
            }
        }
    }

    private void RefreshAllRegisteredTexts()
    {
        MentoraLocalizedText[] localizedTexts = FindObjectsOfType<MentoraLocalizedText>(true);
        for (int i = 0; i < localizedTexts.Length; i++)
        {
            localizedTexts[i].Refresh();
        }

        MentoraLocalizedWorldText[] worldTexts = FindObjectsOfType<MentoraLocalizedWorldText>(true);
        for (int i = 0; i < worldTexts.Length; i++)
        {
            worldTexts[i].Refresh();
        }
    }
}

[DisallowMultipleComponent]
public sealed class MentoraLocalizedText : MonoBehaviour
{
    [SerializeField] private string sourceEnglishText;
    private Text target;

    private void Awake()
    {
        target = GetComponent<Text>();
    }

    private void OnEnable()
    {
        Refresh();
    }

    public void SetSource(string englishText)
    {
        sourceEnglishText = englishText ?? string.Empty;
        Refresh();
    }

    public void Refresh()
    {
        if (target == null)
        {
            target = GetComponent<Text>();
        }

        if (target != null && !string.IsNullOrEmpty(sourceEnglishText))
        {
            target.text = MentoraLocalization.Localize(sourceEnglishText);
        }
    }
}

[DisallowMultipleComponent]
public sealed class MentoraLocalizedWorldText : MonoBehaviour
{
    [SerializeField] private string sourceEnglishText;
    private TextMesh target;

    private void Awake()
    {
        target = GetComponent<TextMesh>();
    }

    public void SetSource(string englishText)
    {
        sourceEnglishText = englishText ?? string.Empty;
        Refresh();
    }

    public void Refresh()
    {
        if (target == null)
        {
            target = GetComponent<TextMesh>();
        }

        if (target != null && !string.IsNullOrEmpty(sourceEnglishText))
        {
            target.text = MentoraLocalization.Localize(sourceEnglishText);
        }
    }
}
