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
    English = 1
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

    private static MentoraLocalization instance;
    private static MentoraLanguage currentLanguage;
    private float nextScanTime;

    public static event Action<MentoraLanguage> LanguageChanged;

    public static MentoraLanguage CurrentLanguage => currentLanguage;
    public static bool IsRomanian => currentLanguage == MentoraLanguage.Romanian;
    public static string CurrentLanguageLabel => IsRomanian ? "Romanian" : "English";

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
        if (!IsRomanian || string.IsNullOrEmpty(englishText))
        {
            return englishText;
        }

        return RomanianByEnglish.TryGetValue(englishText, out string romanian) ? romanian : englishText;
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
            if (RomanianByEnglish.ContainsKey(value))
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
