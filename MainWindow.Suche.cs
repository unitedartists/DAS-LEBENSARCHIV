using System;
using System.Windows;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;

namespace DAS_LEBENSARCHIV
{
    public partial class MainWindow
    {
        // ============================================================
        // BUILD 0.8: JAMES FINDET ERINNERUNGEN
        // ============================================================
        // James zerlegt die Eingabe zunaechst nur in einzelne Woerter
        // (noch keine KI, keine natuerliche Sprachverarbeitung) und
        // vergleicht sie gegen Titel/Beschreibung/Ort/Jahreszeit/
        // Stichwoerter aller Ereignisse - sowohl bei aktiven als auch
        // bei archivierten Personen. Mehrere Woerter muessen ALLE
        // zutreffen (UND-Verknuepfung).
        //
        // ============================================================
        // BUILD 1.5: JAMES KOMBINIERT ERINNERUNGEN
        // ============================================================
        // Der durchsuchte Text pro Ereignis umfasst zusaetzlich Person/
        // Beziehung/Datum - eine Eingabe wie "Bruder Garten 1998"
        // kombiniert sich dadurch von selbst zu einer gemeinsamen Suche.
        //
        // ============================================================
        // A/OPA-ARCHITEKTURAUFTRAG "JAMES-SUCHE -> AM ALS EINZIGER
        // ARBEITSBEREICH" (16.08.)
        // ============================================================
        // ERSETZT den bisherigen Ansatz aus dem "James-Einzug" (12.08.):
        // dort zeigte die James-Suchleiste Treffer des neuen Modells in
        // einem EIGENEN, zweiten Ergebnisbereich direkt unter der
        // Suchleiste an ("Doppel-Maske"). A/Opa haben das nach dem
        // Praxistest ausdruecklich verworfen (unuebersichtlich, drueckte
        // ausserdem die Hauptnavigation aus dem Fenster). Der gesamte
        // JamesNeuesModellErgebnisPanel-Bereich (samt allen zugehoerigen
        // Methoden) wurde deshalb ENTFERNT.
        //
        // NEUES Prinzip: Klickt Opa auf "Suche starten" (nicht schon bei
        // jedem Tastendruck!), wird NUR der Suchbegriff an die bereits
        // bestehende AM-Direktsuche (AmDirekteSucheTextBox in MainWindow.
        // ErinnerungsmodellZustand.cs) uebergeben und automatisch zur
        // Arbeitsmappe gewechselt. Die AM selbst macht ab dort alles
        // Weitere (Suchen/Sortieren/Markieren/Zuordnen/Papierkorb) - keine
        // zweite Suchlogik, keine zweite Ergebnisanzeige. Suchergebnis
        // (was AmDirekteAuswahlListe zeigt), Markierung (was dort
        // ausgewaehlt ist) und Arbeitsauswahl (AmArbeitsauswahlListe,
        // erst nach ausdruecklichem "hinzufuegen") bleiben dabei die drei
        // bereits bestehenden, sauber getrennten Ebenen der AM - unver-
        // aendert, nichts wird automatisch uebernommen oder zugeordnet.
        //
        // Die Alt-Modell-Suche (JamesTrefferListe/Erinnerungskarte, live
        // bei jedem Tastendruck) bleibt unveraendert bestehen - sie ist
        // kein zweiter "Arbeitsbereich" mit Markieren/Zuordnen, sondern
        // nur eine Kurzanzeige, und war nicht Gegenstand dieses Auftrags.

        // A/Opa-BAUAUFTRAG "AM-ABSCHLUSS" (16.08.), Punkt 6: die Alt-Modell-
        // Live-Suche (FuehreJamesSucheAus, JamesTrefferListe/Erinnerungskarte)
        // wird bei jedem Tastendruck NICHT mehr aufgerufen - sie war fuer
        // Opa ohnehin nicht mehr sichtbar gedacht (siehe MainWindow.xaml,
        // Kommentar an dieser Stelle) und haette nur unnoetig bei jedem
        // Buchstaben unsichtbare Arbeit verrichtet. Methode selbst bewusst
        // NICHT geloescht (siehe dort).
        private void JamesSucheTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
        }

        // A/Opa-BAUAUFTRAG "AM-ABSCHLUSS" (16.08.), Punkt 5+6: verbindliche,
        // EHRLICHE Suchverlaufs-/Statusanzeige. Die eigentliche Suche
        // (ZentraleErinnerungsSuche) beruehrt keine UI-Elemente und laeuft
        // deshalb echt im Hintergrund (Task.Run) - kein erfundener
        // Fortschrittsbalken, sondern eine unbestimmte Aktivitaetsanzeige
        // waehrend tatsaechlich gerechnet wird. Die alte, jetzt stillgelegte
        // Live-Suche (FuehreJamesSucheAus) wird hier nicht mehr aufgerufen.
        private async void SucheStarten_Click(object sender, RoutedEventArgs e)
        {
            SucheStartenButton.IsEnabled = false;
            SucheAbbrechenButton.IsEnabled = true;

            string eingabe = JamesSucheTextBox.Text.Trim();

            if (eingabe == "")
            {
                SucheNeuerStatusText.Text = "";
                SucheFortschrittsanzeige.Visibility = Visibility.Collapsed;
                SucheStartenButton.IsEnabled = true;
                SucheAbbrechenButton.IsEnabled = false;
                return;
            }

            SucheNeuerStatusText.Text = "James sucht: \"" + eingabe + "\" ...";
            SucheFortschrittsanzeige.Visibility = Visibility.Visible;

            int trefferAnzahl = await System.Threading.Tasks.Task.Run(
                () => ZentraleErinnerungsSuche(eingabe, SortierModus.DatumNeuesteZuerst).Count);

            SucheFortschrittsanzeige.Visibility = Visibility.Collapsed;

            SucheNeuerStatusText.Text = trefferAnzahl > 0
                ? "James hat " + (trefferAnzahl == 1 ? "1 Erinnerung" : trefferAnzahl + " Erinnerungen") + " gefunden und in der Arbeitsmappe aufgelegt."
                : "James hat nichts gefunden.";

            UebergibSucheAnArbeitsmappe(eingabe);

            SucheStartenButton.IsEnabled = true;
            SucheAbbrechenButton.IsEnabled = false;
        }

        // Build 6.0, Punkt 7: bewusst noch ohne Funktion - die Suche
        // arbeitet aktuell synchron und ist bereits beendet, bevor
        // überhaupt abgebrochen werden könnte. Der Button bereitet
        // lediglich die spätere Bedienlogik vor (echte Hintergrundsuche
        // mit Abbruchmöglichkeit folgt in einem eigenen Bauabschnitt).
        private void SucheAbbrechen_Click(object sender, RoutedEventArgs e)
        {
        }

        // A/Opa-ARCHITEKTURAUFTRAG (16.08.): einziger neuer Code dieses
        // Auftrags. Wechselt zur Arbeitsmappe und uebertraegt den
        // Suchbegriff in die dort bereits bestehende Direktsuche
        // (AmDirekteSucheTextBox) - deren TextChanged-Handler (bereits
        // vorhanden, unveraendert) fuehrt darueber automatisch
        // ZentraleErinnerungsSuche aus und befuellt AmDirekteAuswahlListe.
        // Keine neue Suchlogik, keine neue Anzeige, keine automatische
        // Zuordnung oder Uebernahme in die Arbeitsauswahl - Opa markiert
        // und entscheidet in der AM selbst weiter, wie bisher.
        private void UebergibSucheAnArbeitsmappe(string suchbegriff)
        {
            HauptTabControl.SelectedIndex = ArbeitsmappeTabIndex;

            // A/Opa-UMBAU "AM GRUNDLEGEND VEREINFACHEN" (17.08.), §7: eine
            // neue Suche beendet den "leer"-Zustand ("Arbeitsmappe leeren").
            amArbeitstischGeleert = false;

            AmDirekteSucheTextBox.Text = suchbegriff;
        }

        private void FuehreJamesSucheAus()
        {
            string eingabe = JamesSucheTextBox.Text.Trim();

            JamesTrefferListe.Items.Clear();

            if (eingabe == "")
            {
                JamesTrefferListe.Visibility = Visibility.Collapsed;
                JamesSucheStatusText.Text = "";
                ErinnerungskartePanel.Visibility = Visibility.Collapsed;
                ErinnerungskartePlatzhalterText.Visibility = Visibility.Collapsed;
                return;
            }

            string[] woerter = eingabe.ToLower().Split(
                new char[] { ' ' },
                StringSplitOptions.RemoveEmptyEntries);

            List<Suchtreffer> treffer = new List<Suchtreffer>();

            foreach (Person person in allePersonen)
            {
                SammleTrefferFuerPerson(person, false, woerter, treffer);
            }

            foreach (object element in ArchivListe.Items)
            {
                Person person = element as Person;

                if (person != null)
                {
                    SammleTrefferFuerPerson(person, true, woerter, treffer);
                }
            }

            foreach (Suchtreffer einzelnerTreffer in treffer)
            {
                JamesTrefferListe.Items.Add(einzelnerTreffer);
            }

            if (treffer.Count > 0)
            {
                JamesTrefferListe.Visibility = Visibility.Visible;
                ErinnerungskartePanel.Visibility = Visibility.Collapsed;
                ErinnerungskartePlatzhalterText.Visibility = Visibility.Visible;
            }
            else
            {
                JamesTrefferListe.Visibility = Visibility.Collapsed;
                ErinnerungskartePanel.Visibility = Visibility.Collapsed;
                ErinnerungskartePlatzhalterText.Visibility = Visibility.Collapsed;
            }
        }

        private void SammleTrefferFuerPerson(Person person, bool istArchiviert, string[] woerter, List<Suchtreffer> treffer)
        {
            if (person.Ereignisse == null)
            {
                return;
            }

            // Personendaten (Build 1.5): einmal pro Person gebildet, nicht
            // pro Ereignis, da sie sich innerhalb der Schleife nicht aendern.
            string personentext = (
                (person.Vorname ?? "") + " " +
                (person.Nachname ?? "") + " " +
                (person.Beziehung != null ? person.Beziehung.ToString() ?? "" : "")
            ).ToLower();

            foreach (Ereignis ereignis in person.Ereignisse)
            {
                string suchtext = (
                    personentext + " " +
                    (ereignis.Titel ?? "") + " " +
                    (ereignis.Beschreibung ?? "") + " " +
                    (ereignis.Ort ?? "") + " " +
                    (ereignis.Datum ?? "") + " " +
                    (ereignis.Jahreszeit ?? "") + " " +
                    (ereignis.Stichwoerter != null ? string.Join(" ", ereignis.Stichwoerter) : "") + " " +
                    (ereignis.Bewertungen != null ? string.Join(" ", ereignis.Bewertungen) : "")
                ).ToLower();

                bool alleWoerterGefunden = true;

                foreach (string wort in woerter)
                {
                    if (!suchtext.Contains(wort))
                    {
                        alleWoerterGefunden = false;
                        break;
                    }
                }

                if (alleWoerterGefunden)
                {
                    treffer.Add(new Suchtreffer
                    {
                        Ereignis = ereignis,
                        Person = person,
                        IstArchiviert = istArchiviert
                    });
                }
            }
        }

        private void JamesTrefferListe_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Suchtreffer treffer = JamesTrefferListe.SelectedItem as Suchtreffer;

            if (treffer == null)
            {
                ErinnerungskartePanel.Visibility = Visibility.Collapsed;

                if (JamesTrefferListe.Visibility == Visibility.Visible)
                {
                    ErinnerungskartePlatzhalterText.Visibility = Visibility.Visible;
                }

                return;
            }

            ZeigeErinnerungskarte(treffer.Person, treffer.Ereignis);

            if (treffer.IstArchiviert)
            {
                bool ergebnis = James.FrageJaNein(James.FrageTrefferImArchiv(treffer.Person.ToString()));

                if (ergebnis)
                {
                    HoleAusArchivZurueckAufSchreibtisch(treffer.Person, treffer.Ereignis);
                }
            }
            else
            {
                // Wichtig: Das lokale Suchfeld auf dem Schreibtisch koennte
                // gerade die Personenliste einschraenken. Erst leeren, damit
                // die gefundene Person sicher in der Liste erscheint, bevor
                // wir sie auswaehlen.
                SucheTextBox.Text = "";

                HauptTabControl.SelectedIndex = 0;
                PersonenListe.SelectedItem = treffer.Person;
                EreignisseListe.SelectedItem = treffer.Ereignis;
            }
        }
    }
}
