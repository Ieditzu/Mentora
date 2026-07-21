# Release automat pentru jocul Unity

Workflow-ul `Build Unity game releases` compilează jocul Mentora pentru Windows, Linux, Android și iOS.

## Ce produce

- `Mentora-Windows.zip` — jocul pentru Windows 64-bit;
- `Mentora-Linux.zip` — jocul pentru Linux 64-bit;
- `Mentora-Android.apk` — pachetul Android;
- `Mentora-iOS.ipa` — pachetul iOS, generat în `releases/ios/Mentora.ipa` de etapa macOS;
- `Mentora-iOS-Xcode.zip` — proiectul Xcode generat de Unity pentru iOS.

La publicarea unui GitHub Release, aceste fișiere sunt atașate automat la release. Workflow-ul se poate porni și manual din fila **Actions**. În acest caz, dacă se activează `publish_release`, se completează și tagul release-ului.

## Configurare necesară o singură dată

În repository, în **Settings → Secrets and variables → Actions**, se adaugă credentialele Unity:

- `UNITY_LICENSE` — fișierul de licență Unity activat și codificat Base64, pentru Unity Personal;
- sau `UNITY_EMAIL`, `UNITY_PASSWORD` și, pentru licență Pro, `UNITY_SERIAL`.

## iOS

Unity generează proiectul Xcode, apoi runnerul macOS îl arhivează și pune IPA-ul în `releases/ios/Mentora.ipa`. Proiectul Xcode este inclus și el în release pentru semnarea cu echipa Apple Developer și distribuirea pe iPhone/iPad.

Înainte de publicare, identificatorul iOS din Unity trebuie să fie un Bundle Identifier al proiectului Apple Developer, nu valoarea implicită Unity.
