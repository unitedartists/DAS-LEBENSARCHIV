using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace DAS_LEBENSARCHIV
{
    // ============================================================
    // BUILD 1.3: JAMES SPRICHT
    // ============================================================
    // Architekturbeschluss 006: Bevor weitere Funktionen entstehen, muss
    // James zuerst lernen zu antworten. Diese Klasse ist die zentrale
    // Sprachschicht - jeder Text, den der Benutzer von James liest, liegt
    // ab jetzt hier statt verstreut über MainWindow.xaml.cs.
    //
    // Namensgebung (bewusste Abweichung vom Vorschlag "JamesDialogService"):
    // In MainWindow.xaml.cs stand schon lange die Notiz "James soll
    // dereinst als eigene Klasse (James.cs) ... liefern". Diese Klasse
    // löst genau dieses Vorhaben ein. Der schlichte Name "James" wurde
    // gewählt, weil sich jeder Aufruf dadurch wie eine direkte Aussage
    // von James selbst liest (z.B. James.Hinweis(...), statt über den
    // Umweg eines abstrakten "Dialog-Service"). Das passt gut zur
    // eigentlichen Absicht dieses Builds: Nicht das Programm spricht,
    // sondern James.
    //
    // WICHTIG: Dieser Build erschafft bewusst KEINE neue Funktion für
    // den Benutzer. Alle bisherigen Abläufe bleiben unverändert - nur
    // die Formulierungen liegen jetzt zentral an einem Ort. Dadurch kann
    // James in künftigen Builds mit wenig Aufwand persönlicher und
    // menschlicher werden (z.B. situationsabhängige Begrüßungen),
    // ohne dass an vielen Stellen im Code gesucht werden müsste.
    // ============================================================
    public static class James
    {
        // ---- Standard-Titel für Dialoge ----
        public const string TitelHinweis = "Ein Hinweis von James";
        public const string TitelEntscheidung = "Gemeinsam entscheiden";
        public const string TitelEndgueltigeEntscheidung = "Eine endgültige Entscheidung";
        public const string TitelProblem = "Das hat leider nicht geklappt";
        public const string TitelWiederhergestellt = "Wiederhergestellt";

        // ============================================================
        // GRUNDBAUSTEINE: WIE James spricht (nicht WAS er sagt)
        // ============================================================

        // Zeigt einen einfachen Hinweis (z.B. "bitte erst etwas auswählen").
        public static void Hinweis(string text, string titel = TitelHinweis)
        {
            MessageBox.Show(text, titel, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // Zeigt, dass etwas technisch nicht geklappt hat.
        public static void Problem(string text)
        {
            MessageBox.Show(text, TitelProblem, MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        // Stellt eine Ja/Nein-Frage und gibt zurück, ob mit "Ja" geantwortet wurde.
        public static bool FrageJaNein(string text, string titel = TitelEntscheidung, MessageBoxImage bild = MessageBoxImage.Question)
        {
            return MessageBox.Show(text, titel, MessageBoxButton.YesNo, bild) == MessageBoxResult.Yes;
        }

        // ============================================================
        // KONKRETE SÄTZE, DIE JAMES IM PROGRAMM SAGT
        // (nach Themenbereich sortiert, wie im Programm verwendet)
        // ============================================================

        // ---- Allgemein / Personen ----

        public static string BitteErstNamenEingeben =>
            "Bitte überprüfen wir die Eingabe gemeinsam. Es fehlt noch mindestens ein Name.";

        public static string BittePersonAuswaehlen =>
            "Bitte wählen wir zuerst eine Person in der Liste aus.";

        public static string BittePersonenAuswaehlen =>
            "Bitte wählen wir zuerst eine oder mehrere Personen in der Liste aus.";

        public static string BitteArchivPersonAuswaehlen =>
            "Bitte wählen wir zuerst eine Person im Archiv aus.";

        public static string BittePapierkorbAuswaehlen =>
            "Bitte wählen wir zuerst eine oder mehrere Personen im Papierkorb aus.";

        public static string ErinnerungGespeichert(string name) =>
            "Die Erinnerung an " + name + " wurde gespeichert.";

        public static string ErinnerungAktualisiert(string name) =>
            "Die Erinnerung an " + name + " wurde aktualisiert.";

        public static string FotoZugeordnet(string name) =>
            "Das Foto wurde " + name + " zugeordnet.";

        public static string FrageInPapierkorbEinzeln(string name) =>
            "Möchten Sie die Erinnerung an \"" + name + "\" in den Papierkorb legen?";

        public static string FrageInPapierkorbMehrere(int anzahl) =>
            "Möchten Sie die Erinnerungen an " + anzahl + " Personen gemeinsam in den Papierkorb legen?";

        public static string InPapierkorbGelegtEinzeln(string name) =>
            "Die Erinnerung an " + name + " wurde in den Papierkorb gelegt.";

        public static string InPapierkorbGelegtMehrere(int anzahl) =>
            anzahl + " Erinnerungen wurden in den Papierkorb gelegt.";

        public static string ImArchivAngekommen(string name) =>
            "Die Erinnerung an " + name + " hat ihren Platz im Archiv gefunden.";

        public static string ImArchivAngekommenMehrere(int anzahl) =>
            anzahl + " Erinnerungen haben ihren Platz im Archiv gefunden.";

        public static string ZurueckAufSchreibtisch(string name) =>
            "Die Erinnerung an " + name + " ist zurück auf Ihrem Schreibtisch.";

        public static string WiederhergestelltEinzeln(string name) =>
            "Die Erinnerung an " + name + " wurde wiederhergestellt und ist zurück auf Ihrem Schreibtisch.";

        public static string WiederhergestelltMehrere(int anzahl) =>
            anzahl + " Erinnerungen wurden wiederhergestellt und sind zurück auf Ihrem Schreibtisch.";

        public static string FrageEndgueltigLoeschenEinzeln(string name) =>
            "Die Erinnerung an \"" + name + "\" würde damit endgültig und unwiderruflich gelöscht. Sind Sie sicher?";

        public static string FrageEndgueltigLoeschenMehrere(int anzahl) =>
            "Die Erinnerungen an " + anzahl + " Personen würden damit endgültig und unwiderruflich gelöscht. Sind Sie sicher?";

        public static string FrageArchivEndgueltigEntfernen(string name) =>
            "Die Erinnerung an \"" + name + "\" würde damit endgültig und unwiderruflich aus dem Archiv entfernt. Sind Sie sicher?";

        // ---- James-Suche (Build 0.8) ----

        public static string SucheEinTreffer =>
            "Ich glaube, diese Erinnerung könnte gemeint sein.";

        public static string SucheMehrereTreffer(int anzahl) =>
            "Ich glaube, diese " + anzahl + " Erinnerungen könnten gemeint sein.";

        // Build 6.0, Punkt 7: Statusmeldung während einer laufenden Suche -
        // aktuell noch für den Bruchteil einer Sekunde sichtbar (synchrone
        // Suche), bereitet aber bereits die Bedienlogik für spätere,
        // länger laufende Suchvorgänge vor.
        public static string SucheLaeuftErinnerungen =>
            "James durchsucht derzeit Ihre Erinnerungen...";

        public static string SucheNichtsGefunden =>
            "Zu diesem Thema habe ich in den bisher beschriebenen Erinnerungen nichts gefunden. Ich kann aktuell nur Erinnerungen durchsuchen, die bereits einen Titel, Stichwörter oder eine Beschreibung besitzen - ich kenne den Inhalt von Bildern selbst noch nicht. Eine automatische Bilderkennung ist für einen späteren Ausbau vorgesehen.";

        public static string FrageTrefferImArchiv(string name) =>
            "Die Erinnerung an \"" + name + "\" liegt aktuell im Archiv. Möchten Sie die Person zurück auf den Schreibtisch holen, um das Ereignis zu bearbeiten?";

        // ---- Ereignisse (Build 0.5 ff.) ----

        public static string BittePersonUndEreignisAuswaehlen =>
            "Bitte wählen wir zuerst eine Person und ein Ereignis aus.";

        public static string BitteEreignisAuswaehlen =>
            "Bitte wählen wir zuerst ein Ereignis aus.";

        public static string BitteEreignisTitelEingeben =>
            "Bitte geben wir dem Ereignis zumindest einen Titel.";

        public static string FotoZuEreignisZugeordnet(string ereignisTitel) =>
            "Das Foto wurde dem Ereignis \"" + ereignisTitel + "\" zugeordnet.";

        public static string EreignisZugeordnet(string ereignisTitel, string personName) =>
            "Das Ereignis \"" + ereignisTitel + "\" wurde erfolgreich angelegt und " + personName + " zugeordnet.";

        public static string BewertungGespeichert(string ereignisTitel) =>
            "Die Bewertung für \"" + ereignisTitel + "\" wurde gespeichert.";

        // ---- Werkzeuge: Computer kennenlernen / Rundgang (Build 0.4 / 1.1) ----

        public static string ComputerKennenlernenEinladung =>
            "Ich bin James, Ihr Butler.\n\n" +
            "Viele Erinnerungen liegen verstreut. Manche im Keller, manche auf dem Dachboden, manche längst vergessen.\n\n" +
            "Sie entscheiden, welche Ordner ich mir ansehen darf - ich durchsuche niemals ungefragt den gesamten Computer. " +
            "Dabei werde ich nichts verändern, nichts löschen und nichts verschieben. " +
            "Ich verschaffe mir zunächst lediglich einen Überblick. Erst danach entscheiden wir gemeinsam über die nächsten Schritte.\n\n" +
            "Möchten Sie, dass ich damit beginne?";

        public static string BitteOrdnerAuswaehlen =>
            "Bitte wählen wir zuerst mindestens einen Ordner aus, indem wir ihn im Baum ankreuzen.";

        public static string RundgangLaeuft(int anzahlBisher) =>
            "James durchsucht die ausgewählten Ordner sorgfältig. Das kann je nach Größe der Ordner einen Moment dauern - bitte etwas Geduld.\n\n" + anzahlBisher + " Erinnerungen bisher durchsucht.";

        public static string RundgangZusammenfassung(bool wurdeAbgebrochen, Dictionary<string, int> zaehlerProTyp)
        {
            string text = wurdeAbgebrochen
                ? "Der Rundgang wurde abgebrochen. Bis dahin habe ich gefunden:\n"
                : "Ich habe meinen Rundgang beendet.\n\nIch habe gefunden:\n";

            foreach (KeyValuePair<string, int> eintrag in zaehlerProTyp.OrderByDescending(e => e.Value))
            {
                text += eintrag.Value + " " + eintrag.Key + "\n";
            }

            text += "\nZurzeit wurde noch nichts verändert. Ich habe lediglich ein Erinnerungsverzeichnis erstellt und mir die ausgewählten Ordner gemerkt.";

            return text;
        }

        public static string OrdnergedaechtnisKeineOrdner =>
            "Ich kenne noch keine Ordner aus früheren Rundgängen. Wählen wir gemeinsam aus, wo ich beginnen darf.";

        public static string OrdnergedaechtnisBegruessung(List<OrdnerErinnerung> ordner)
        {
            if (ordner == null || ordner.Count == 0)
            {
                return OrdnergedaechtnisKeineOrdner;
            }

            List<string> zeilen = new List<string>();

            zeilen.Add(ordner.Count == 1
                ? "Ich kenne bereits einen Ordner aus einem früheren Rundgang:"
                : "Ich kenne bereits " + ordner.Count + " Ordner aus früheren Rundgängen:");

            foreach (OrdnerErinnerung eintrag in ordner.OrderByDescending(o => o.LetzterScan))
            {
                zeilen.Add("- " + eintrag.Pfad + " (zuletzt am " + eintrag.LetzterScan.ToString("dd.MM.yyyy") + ", " + eintrag.AnzahlDateien + " Dateien)");
            }

            zeilen.Add("");
            zeilen.Add("Wählen wir gemeinsam aus, welche Ordner ich mir heute erneut ansehen darf.");

            return string.Join("\n", zeilen);
        }

        // ---- Werkzeuge: Erinnerungen aufräumen (Build 1.0) ----

        public static string DoppelgaengerEinladung =>
            "Ich habe Ihr Erinnerungsverzeichnis überprüft. Dabei sind mir einige Dateien aufgefallen, die möglicherweise zusammengehören.\n\n" +
            "Ich habe nichts verändert. Lassen Sie uns gemeinsam entscheiden, wie wir weiter vorgehen.\n\n" +
            "Möchten Sie, dass ich Ihnen die echten Doppelgänger zeige?";

        public static string KeinErinnerungsverzeichnisGefunden =>
            "Ich habe noch kein Erinnerungsverzeichnis gefunden. Bitte zuerst \"Computer kennenlernen\" durchführen.";

        public static string DoppelgaengerGefunden(int anzahlDateien, int anzahlGruppen) =>
            "Ich habe " + anzahlDateien + " echte Doppelgänger in " + anzahlGruppen + " Gruppen gefunden.";

        public static string KeineDoppelgaengerGefunden =>
            "Ich habe keine echten Doppelgänger gefunden.";

        // ---- Arbeitsmappe (Build 2.1) ----
        // Architekturbeschluss 013: James legt gefundene Erinnerungen
        // übersichtlich aus, statt Dateilisten zu zeigen.

        public static string ArbeitsmappeUeberschrift(int anzahl)
        {
            if (anzahl == 0)
            {
                return "Ich habe noch keine Erinnerungen zum Auslegen. Führen wir zuerst unter \"Computer kennenlernen\" einen Rundgang durch.";
            }

            if (anzahl == 1)
            {
                return "Ich habe eine Erinnerung für Sie ausgelegt.";
            }

            return "Ich habe " + anzahl + " Erinnerungen für Sie ausgelegt.";
        }

        public static string ArbeitsmappeKeinemZugeordnet =>
            "Noch nicht eingeordnet";

        public static string ArbeitsmappeBereitsZugeordnet =>
            "Bereits zugeordnet ✓";

        public static string ArbeitsmappeDateiNichtGefunden =>
            "Diese Erinnerung wurde auf der Festplatte nicht mehr gefunden.";

        public static string ArbeitsmappeAuswahlText(int anzahl)
        {
            if (anzahl == 0)
            {
                return "Keine Erinnerung ausgewählt.";
            }

            if (anzahl == 1)
            {
                return "1 Erinnerung ausgewählt. Womit möchten wir beginnen?";
            }

            return anzahl + " Erinnerungen ausgewählt. Womit möchten wir beginnen?";
        }

        public static string ArbeitsmappeNurBilder =>
            "Das funktioniert aktuell nur mit Bildern - andere Erinnerungen lassen sich schon ansehen, aber noch nicht auf diese Weise verbinden.";

        public static string ArbeitsmappeVerbunden(string ereignisTitel, string personName, int gesamtErinnerungenPerson) =>
            "Verbunden mit \"" + ereignisTitel + "\" (" + personName + "). " + personName + " besitzt jetzt " + gesamtErinnerungenPerson + " Erinnerungen.";

        public static string ArbeitsmappeVerbundenMehrere(int anzahl, string ereignisTitel, string personName, int gesamtErinnerungenPerson) =>
            anzahl + " Erinnerungen wurden mit \"" + ereignisTitel + "\" (" + personName + ") verbunden. " + personName + " besitzt jetzt " + gesamtErinnerungenPerson + " Erinnerungen.";

        public static string ArbeitsmappeEreignisAngelegtUndVerbunden(string ereignisTitel, string personName, int gesamtErinnerungenPerson) =>
            "Das Ereignis \"" + ereignisTitel + "\" wurde erfolgreich angelegt und " + personName + " zugeordnet. " + personName + " besitzt jetzt " + gesamtErinnerungenPerson + " Erinnerungen.";

        public static string ArbeitsmappeErinnerungZugeordnet(string personName, int gesamtErinnerungenPerson) =>
            "Der Erinnerungssammlung von " + personName + " hinzugefügt. " + personName + " besitzt jetzt " + gesamtErinnerungenPerson + " Erinnerungen.";

        public static string ArbeitsmappeErinnerungenZugeordnetMehrere(int anzahl, string personName, int gesamtErinnerungenPerson) =>
            anzahl + " Erinnerungen wurden der Sammlung von " + personName + " hinzugefügt. " + personName + " besitzt jetzt " + gesamtErinnerungenPerson + " Erinnerungen.";

        public static string ArbeitsmappeEreignisOeffnenButtonSichtbar =>
            "Ereignis öffnen";

        public static string FehlerBeimOeffnenDerErinnerung(string fehlermeldung) =>
            "Das hat diesmal leider nicht geklappt. Beim Öffnen ist ein Fehler aufgetreten:\n" + fehlermeldung;

        // ---- Personen-Erinnerungen (Build 2.3) ----
        // Architekturbeschluss "Abschluss Etappe A": eine Person besitzt
        // nicht nur ein Titelbild, sondern auch die Erinnerungen, die
        // über ihre Ereignisse mit ihr verbunden sind. Das Titelbild
        // bleibt lediglich "das Gesicht" der Person.

        public static string PersonErinnerungenLink(int anzahl)
        {
            if (anzahl == 0)
            {
                return "Noch keine Erinnerungen";
            }

            if (anzahl == 1)
            {
                return "Erinnerungen (1) ▾";
            }

            return "Erinnerungen (" + anzahl + ") ▾";
        }

        public static string PersonBesitztErinnerungen(int anzahl)
        {
            if (anzahl == 1)
            {
                return "Die Person besitzt jetzt 1 Erinnerung.";
            }

            return "Die Person besitzt jetzt " + anzahl + " Erinnerungen.";
        }

        // ---- Erinnerungen zuerst (Build 2.2) ----
        // Architekturbeschluss: die Arbeitsmappe wird zum Ausgangspunkt.
        // James führt den Benutzer, statt ihn zwischen Reitern hin- und
        // herspringen zu lassen (James-Regel Nr. 2: Der Benutzer folgt
        // James, nicht umgekehrt).

        public static string ArbeitsmappeRueckkehrHinweis =>
            "Sobald wir hier fertig sind, kehren wir automatisch zur Arbeitsmappe zurück.";

        public static string ArbeitsmappeBittePersonZuerst =>
            "Bevor wir ein Ereignis anlegen können, brauchen wir mindestens eine Person. Legen wir zuerst eine an?";

        public static string ArbeitsmappeBitteEreignisPersonAuswaehlen =>
            "Bitte wählen wir zuerst aus, zu welcher Person dieses Ereignis gehören soll.";

        // ---- Build 2.5: Ein einziger Weg für Erinnerungen ----
        // Architekturbeschluss: Die Arbeitsmappe ist der einzige
        // Einstiegspunkt, um Erinnerungen einer Person oder einem
        // Ereignis zuzuordnen. Die früheren, direkten Wege über einen
        // Windows-Dateidialog führen jetzt stattdessen dorthin.

        public static string ArbeitsmappeUmleitungPerson(string personName) =>
            "Erinnerungen ordnen wir jetzt über die Arbeitsmappe zu. Wählen wir dort die gewünschte(n) Erinnerung(en) aus und dann \"Erinnerungen zuordnen\" - " + personName + " lässt sich in der Liste auswählen.";

        public static string ArbeitsmappeUmleitungEreignis(string ereignisTitel) =>
            "Erinnerungen ordnen wir jetzt über die Arbeitsmappe zu. Wählen wir dort die gewünschte(n) Erinnerung(en) aus und dann \"Vorhandenem Ereignis zuordnen\" - \"" + ereignisTitel + "\" lässt sich in der Liste auswählen.";

        // ---- Build 5.0: Erinnerungen über Personen ODER Ereignisse ----
        // Ein "freies" Ereignis ist eigenständig, unabhängig von jeder
        // Person. James macht dabei keine eigenen Namensvorschläge - der
        // Benutzer vergibt den Namen selbst.

        public static string BitteErstFreiesEreignisAnlegen =>
            "Es gibt noch kein besonderes Ereignis. Legen wir zuerst eines an?";

        // ---- Etappe A.5: Aufräumen und Bedienbarkeit ----
        // Punkt 2 (Papierkorb) verwendet bewusst dieselben, bereits
        // vorhandenen Texte wie beim Personen-Papierkorb (FrageInPapierkorb...,
        // InPapierkorbGelegt..., Wiederhergestellt..., FrageEndgueltigLoeschen...) -
        // nur die beiden folgenden Texte sind neu, weil "Personen" dort
        // wörtlich vorkam und nicht zu Ereignissen passt.

        public static string BitteFreieEreignisseAuswaehlen =>
            "Bitte wählen wir zuerst ein oder mehrere besondere Ereignisse aus.";

        public static string BitteEreignisPapierkorbAuswaehlen =>
            "Bitte wählen wir zuerst ein oder mehrere Ereignisse im Papierkorb aus.";

        // Punkt 3 (Dubletten vermeiden)
        public static string FrageEreignisBereitsVorhanden(string ereignisTitel) =>
            "Ein Ereignis namens \"" + ereignisTitel + "\" gibt es bereits. Möchten Sie dieses bestehende Ereignis verwenden? (Bei \"Nein\" legen wir bewusst ein weiteres, neues Ereignis mit diesem Namen an.)";

        public static string FreiesEreignisVerbunden(string ereignisTitel) =>
            "Verbunden mit \"" + ereignisTitel + "\".";

        public static string FreiesEreignisVerbundenMehrere(int anzahl, string ereignisTitel) =>
            anzahl + " Erinnerungen wurden mit \"" + ereignisTitel + "\" verbunden.";

        public static string FreiesEreignisArchiviert(string ereignisTitel) =>
            "\"" + ereignisTitel + "\" wurde archiviert.";

        public static string FreiesEreignisAngelegt(string ereignisTitel) =>
            "Das Ereignis \"" + ereignisTitel + "\" wurde angelegt.";

        public static string FreiesEreignisAktualisiert(string ereignisTitel) =>
            "\"" + ereignisTitel + "\" wurde aktualisiert.";

        // ---- Build 6.0: Die Ereignismappe ----

        public static string EreignismappeAngelegt(string ereignisTitel) =>
            "Die Mappe \"" + ereignisTitel + "\" wurde angelegt.";

        public static string EreignismappeBilderHinweis(string ereignisTitel) =>
            "Bitte in der Arbeitsmappe die gewünschten Bilder auswählen und dann \"Vorhandenem Ereignis zuordnen\" wählen - \"" + ereignisTitel + "\" lässt sich dort in der Liste auswählen.";

        // ---- Build 2.7: sichtbare Diagnose ----
        // Diese Texte dienen ausschließlich dazu, den tatsächlichen Ablauf
        // Schritt für Schritt sichtbar zu machen (keine Annahmen mehr,
        // sondern überprüfbare Fakten direkt auf dem Bildschirm).

        public static string DiagnosePersonAusgewaehltFuerEreignis(string personName, int anzahlEreignisse)
        {
            if (anzahlEreignisse == 0)
            {
                return "🔍 Diagnose: \"" + personName + "\" ausgewählt. Diese Person besitzt aktuell 0 Ereignisse - die Ereignisliste bleibt deshalb leer. Bitte zuerst \"Neues Ereignis anlegen\" verwenden.";
            }

            return "🔍 Diagnose: \"" + personName + "\" ausgewählt. Diese Person besitzt " + anzahlEreignisse + " Ereignis(se) - bitte darunter eines auswählen.";
        }

        public static string DiagnosePersonAusgewaehltFuerErinnerungen(string personName, int anzahlErinnerungenVorher)
        {
            return "🔍 Diagnose: \"" + personName + "\" ausgewählt. Aktueller Stand vor der Zuordnung: " + anzahlErinnerungenVorher + " Erinnerungen. Bitte jetzt \"Zuordnen\" klicken.";
        }

        public static string DiagnoseNachZuordnung(string personName, int anzahlErinnerungenNachher) =>
            "🔍 Diagnose: Zuordnung abgeschlossen. \"" + personName + "\" besitzt jetzt laut Datenbestand " + anzahlErinnerungenNachher + " Erinnerungen.";

        // ---- Build 2.9: James zeigt Erinnerungen an (Etappe B.1) ----

        public static string ErinnerungenFensterTitelPerson(string personName) =>
            "Erinnerungen von " + personName;

        public static string ErinnerungenFensterTitelEreignis(string ereignisTitel) =>
            "Erinnerungen zu \"" + ereignisTitel + "\"";

        // ---- Einstellungen (Build 2.0) ----
        // Architekturbeschluss 012: James lernt seinen Besitzer kennen -
        // freiwillige, jederzeit änderbare Einstellungen. Keine KI, keine
        // Bewertung, keine Analyse.

        public static string EinstellungenGespeichert =>
            "Die Einstellungen wurden gespeichert. Wir können sie jederzeit wieder ändern.";

        public static string FehlerBeimSpeichernEinstellungen(string fehlermeldung) =>
            "Das hat diesmal leider nicht geklappt. Beim Speichern der Einstellungen ist ein Fehler aufgetreten:\n" + fehlermeldung;

        public static string VorschlagEinstellungenKennenlernen() =>
            "🙂 Darf ich Sie kurz kennenlernen? Unter \"Einstellungen\" können Sie mir ein paar freiwillige Fragen beantworten.";

        // ---- Arbeitsstand (Build 1.9) ----
        // Architekturbeschluss 011 (Fortsetzung): James merkt sich, woran
        // zuletzt gearbeitet wurde, und bietet - niemals automatisch -
        // an, dort weiterzumachen.

        public static string ArbeitFortsetzenFrage(string beschreibung) =>
            "Beim letzten Mal haben wir an \"" + beschreibung + "\" gearbeitet. Möchten wir dort weitermachen?";

        // ---- Werkzeuge: James macht Vorschläge (Build 1.7) ----
        // Architekturbeschluss 010: James entscheidet niemals, er schlägt
        // ausschließlich vor. Alle Texte bleiben bewusst zurückhaltend
        // formuliert ("vielleicht", "könnte") statt bestimmend.

        public static string VorschlaegeUeberschrift(int anzahl)
        {
            string gruss = TageszeitGruss();

            if (anzahl == 1)
            {
                return gruss + ". Ich hätte heute einen Vorschlag für Sie:";
            }

            return gruss + ". Ich hätte heute " + anzahl + " Vorschläge für Sie:";
        }

        // Build 6.0: Gruß-Zeile für den Vorschlag auf der neuen Startseite -
        // nutzt denselben Tagesrhythmus wie VorschlaegeUeberschrift.
        public static string StartseiteVorschlagGruss() =>
            TageszeitGruss() + ". Heute möchte ich Ihnen folgenden Vorschlag machen:";

        // Einfacher Tagesrhythmus nur für diese Begrüßung (noch nicht die
        // vom Architekten angedachte umfassende "Stimmung" von James -
        // das bleibt bewusst einem späteren Build vorbehalten).
        private static string TageszeitGruss()
        {
            int stunde = DateTime.Now.Hour;

            if (stunde < 11)
            {
                return "🌿 Guten Morgen";
            }

            if (stunde < 18)
            {
                return "🌿 Guten Tag";
            }

            return "🌿 Guten Abend";
        }

        public static string VorschlagFavorit(string ereignisTitel, string personName) =>
            "⭐ " + ereignisTitel + " (" + personName + ")";

        public static string VorschlagJahreszeit(string jahreszeit, string ereignisTitel, string personName) =>
            "🌿 Passend zum " + jahreszeit.ToLower() + ": " + ereignisTitel + " (" + personName + ")";

        public static string VorschlagHeuteVor(int jahre, string ereignisTitel, string personName) =>
            "📅 Heute vor " + jahre + (jahre == 1 ? " Jahr: " : " Jahren: ") + ereignisTitel + " (" + personName + ")";

        public static string VorschlagUnvollstaendig(string ereignisTitel, string personName) =>
            "📷 Zu diesem Ereignis fehlt noch ein Bild: " + ereignisTitel + " (" + personName + ")";

        public static string VorschlagUnzugeordneteDateien(int anzahlBilder) =>
            "🗂️ In Ihrem Erinnerungsverzeichnis liegen " + anzahlBilder + " Bilder, die Sie sich vielleicht noch ansehen möchten.";

        // ---- Technische Fehlerfälle ----

        public static string FehlerBeimSpeichernFoto(string fehlermeldung) =>
            "Das hat diesmal leider nicht geklappt. Beim Speichern des Fotos ist ein Fehler aufgetreten:\n" + fehlermeldung;

        public static string FehlerBeimSpeichernErinnerungsverzeichnis(string fehlermeldung) =>
            "Das hat diesmal leider nicht geklappt. Beim Speichern des Erinnerungsverzeichnisses ist ein Fehler aufgetreten:\n" + fehlermeldung;

        public static string FehlerBeimSpeichernOrdnergedaechtnis(string fehlermeldung) =>
            "Das hat diesmal leider nicht geklappt. Beim Speichern des Ordnergedächtnisses ist ein Fehler aufgetreten:\n" + fehlermeldung;

        public static string FehlerBeimLesenErinnerungsverzeichnis(string fehlermeldung) =>
            "Das hat diesmal leider nicht geklappt. Beim Lesen des Erinnerungsverzeichnisses ist ein Fehler aufgetreten:\n" + fehlermeldung;

        public static string FehlerBeimLadenDaten(string fehlermeldung) =>
            "Das hat diesmal leider nicht geklappt. Beim Laden der gespeicherten Erinnerungen ist ein Fehler aufgetreten:\n" + fehlermeldung;

        public static string FehlerBeimSpeichernDaten(string fehlermeldung) =>
            "Das hat diesmal leider nicht geklappt. Beim dauerhaften Speichern ist ein Fehler aufgetreten:\n" + fehlermeldung;
    }
}
