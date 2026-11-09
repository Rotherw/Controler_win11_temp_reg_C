# CoolWatch

Mały, przeciągany widget na pulpit Windows 11, zawsze na wierzchu. Pomaga pilnować temperatury laptopa.

## Funkcje
- Temperatura i obciążenie procesora, temperatura karty graficznej (LibreHardwareMonitor).
- **❄ MAX chłodzenie** – jeden przycisk: aktywna polityka chłodzenia Windows (wentylator przed spowolnieniem procesora) + wentylatory na 100%, jeśli laptop pozwala nimi sterować. Wyłączenie lub zamknięcie programu przywraca ustawienia.
- **Suwak limitu procesora (50–100%)** – działa na każdym laptopie przez plan zasilania Windows i realnie zmniejsza grzanie.
- **Auto-chłodzenie** – po przekroczeniu 85°C obniża limit do 70%, a po ochłodzeniu (poniżej 77°C) przywraca poprzedni.
- **Regulacja wentylatorów** – tylko jeśli laptop udostępnia sterowanie (wiele modeli nie udostępnia; wtedy program o tym informuje).
  Przycisk „Auto" oddaje sterowanie z powrotem producentowi. Przy zamknięciu programu wentylatory wracają do trybu automatycznego.

## Konsola widgetu
Widget pokazuje też: wykres temperatury CPU, użycie RAM, dyski (zajętość i temperatura), prędkość sieci oraz **wybór planu zasilania Windows**
(np. Zrównoważony / Wysoka wydajność / Oszczędzanie energii). Wartości, których sprzęt nie udostępnia, są pomijane.

## Uwagi
- Program wymaga uprawnień administratora (odczyt czujników).
- Limit procesora zostaje ustawiony także po zamknięciu programu (poza trybem auto-chłodzenia) – przywróć 100% suwakiem, jeśli nie jest potrzebny.
- Instalator buduje GitHub Actions: pobierz `CoolWatch-Setup.exe` z zakładki Releases.

## Procesy w tle
Rozwijany panel „Procesy w tle” pokazuje programy bieżącej sesji z obciążeniem CPU i pamięcią (najbardziej obciążające na górze).
Przycisk **Zakończ** (po potwierdzeniu) wyłącza wskazany proces (procesy potomne widać osobno na liście). Procesy systemowe (pliki z folderu Windows, explorer, dwm, svchost, antywirus itp.)
są zablokowane, żeby nie zawiesić Windowsa. Niezapisane dane w zakończonym programie przepadają.
