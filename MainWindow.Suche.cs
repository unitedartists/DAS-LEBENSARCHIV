using System;
using System.Windows;
using System.Collections.Generic;
using System.Windows;
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
        // Architekturbeschluss 008: Die UND-Verknuepfung mehrerer Woerter
        // gab es technisch schon seit Build 0.8 - was fehlte, war, dass
        // der durchsuchte Text nur Ereignis-Felder umfasste. Eine Eingabe
        // wie "Bruder Garten 1998" konnte deshalb nichts finden, selbst
        // wenn genau das gemeint war: die Beziehung "Bruder" stand nur
        // an der Person, nicht am Ereignis, und das Jahr stand nur im
        // freien Datumsfeld, das bisher gar nicht durchsucht wurde.
        //
        // Der durchsuchte Text pro Ereignis umfasst deshalb jetzt
        // zusaetzlich:
        // - Vorname/Nachname der zugehoerigen Person
        // - Beziehung der Person (z.B. "Bruder", oder die eigene
        //   Bezeichnung bei "Sonstige")
        // - Das Datum des Ereignisses (als freier Text - ein Suchwort
        //   wie "1998" findet damit jedes Ereignis, dessen Datumsfeld
        //   diese Ziffernfolge enthaelt)
        //
        // Dadurch kombiniert sich eine Eingabe wie "Bruder Garten 1998"
        // von selbst zu einer gemeinsamen Suche ueber Person, Ereignis
        // und Zeitpunkt - ganz ohne KI, nur durch die bereits bestehende
        // UND-Verknuepfung ueber einen erweiterten Suchtext.

        private void JamesSucheTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            FuehreJamesSucheAus();
        }

        // Build 6.0, Punkt 7: "Suche starten" löst genau denselben Ablauf
        // aus wie jede Eingabe im Suchfeld - nur mit einer kurz
        // sichtbaren Statusmeldung davor, als Rahmen für spätere, länger
        // laufende Suchvorgänge (z.B. Bildanalyse). Die Suche selbst
        // bleibt in diesem Bauabschnitt bewusst synchron.
        private void SucheStarten_Click(object sender, RoutedEventArgs e)
        {
            SucheStartenButton.IsEnabled = false;
            SucheAbbrechenButton.IsEnabled = true;

            JamesSucheStatusText.Text = James.SucheLaeuftErinnerungen;

            FuehreJamesSucheAus();

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

                if (treffer.Count == 1)
                {
                    JamesSucheStatusText.Text = James.SucheEinTreffer;
                }
                else
                {
                    JamesSucheStatusText.Text = James.SucheMehrereTreffer(treffer.Count);
                }
            }
            else
            {
                JamesTrefferListe.Visibility = Visibility.Collapsed;
                JamesSucheStatusText.Text = James.SucheNichtsGefunden;
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