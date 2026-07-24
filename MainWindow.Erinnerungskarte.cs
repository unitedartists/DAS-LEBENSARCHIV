using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace DAS_LEBENSARCHIV
{
    public partial class MainWindow
    {
        // ============================================================
        // BUILD 1.6: JAMES ZEIGT ERINNERUNGEN (ERINNERUNGSKARTE)
        // ============================================================
        // Architekturbeschluss 009: Gefundene Erinnerungen werden nicht
        // mehr nur als Zeile in einer Liste dargestellt, sondern als
        // großzügige Erinnerungskarte neben der Trefferliste. Verwendet
        // ausschließlich bereits vorhandene Daten - keine neuen Felder,
        // keine KI, keine automatische Geschichte. Die Karte darf später
        // wachsen (z.B. um Videos, Dokumente, Tonaufnahmen) und soll
        // dereinst die Grundlage für das Lebensbuch bilden.
        // Build 2.2a: merkt, welche Person/welches Ereignis die
        // Erinnerungskarte gerade zeigt - damit wir sie nach einer
        // Zuordnung aus der Arbeitsmappe nur dann auffrischen, wenn sie
        // tatsächlich betroffen ist (nie eine andere Person/ein anderes
        // Ereignis überschreiben).
        private Person erinnerungskarteAktuellePerson = null;
        private Ereignis erinnerungskarteAktuellesEreignis = null;

        // Aktualisiert die Erinnerungskarte nur dann, wenn sie gerade
        // exakt diese Person (und, falls angegeben, exakt dieses
        // Ereignis) zeigt. So kann nach einer Zuordnung aus der
        // Arbeitsmappe niemals versehentlich die Erinnerung einer
        // anderen Person/eines anderen Ereignisses überschrieben werden.
        private void AktualisiereErinnerungskarteFallsBetroffen(Person person, Ereignis ereignis)
        {
            if (erinnerungskarteAktuellePerson == null || erinnerungskarteAktuellesEreignis == null)
            {
                return;
            }

            bool selbePerson = erinnerungskarteAktuellePerson.Id == person.Id;
            bool selbesEreignis = ereignis == null || erinnerungskarteAktuellesEreignis.Id == ereignis.Id;

            if (selbePerson && selbesEreignis)
            {
                ZeigeErinnerungskarte(erinnerungskarteAktuellePerson, erinnerungskarteAktuellesEreignis);
            }
        }

        // Aktualisiert Titelbild/Ereignisliste auf dem Schreibtisch nur
        // dann, wenn dort gerade exakt diese Person ausgewählt ist -
        // sonst bliebe dort sonst das Bild einer anderen Person stehen
        // bzw. würde durch die Daten einer anderen Person überschrieben.
        private void AktualisiereSchreibtischFallsBetroffen(Person person)
        {
            Person aufSchreibtischAusgewaehlt = PersonenListe.SelectedItem as Person;

            if (aufSchreibtischAusgewaehlt != null && aufSchreibtischAusgewaehlt.Id == person.Id)
            {
                ZeigeFoto(person);
                AktualisiereEreignisseAnzeige(person);
                ZeigePersonErinnerungenLink(person);
            }
        }

        private void ZeigeErinnerungskarte(Person person, Ereignis ereignis)
        {
            erinnerungskarteAktuellePerson = person;
            erinnerungskarteAktuellesEreignis = ereignis;

            ErinnerungskarteTitelText.Text = ereignis.Titel;

            List<string> datumUndJahreszeit = new List<string>();

            if (!string.IsNullOrWhiteSpace(ereignis.Datum))
            {
                datumUndJahreszeit.Add(ereignis.Datum);
            }

            if (!string.IsNullOrWhiteSpace(ereignis.Jahreszeit))
            {
                datumUndJahreszeit.Add(ereignis.Jahreszeit);
            }

            if (datumUndJahreszeit.Count > 0)
            {
                ErinnerungskarteDatumText.Text = "📅 " + string.Join(" · ", datumUndJahreszeit);
                ErinnerungskarteDatumText.Visibility = Visibility.Visible;
            }
            else
            {
                ErinnerungskarteDatumText.Visibility = Visibility.Collapsed;
            }

            string personenzeile = "👤 " + person.ToString();

            if (person.Beziehung != null && !string.IsNullOrWhiteSpace(person.Beziehung.ToString()))
            {
                personenzeile += " (" + person.Beziehung.ToString() + ")";
            }

            ErinnerungskartePersonText.Text = personenzeile;

            if (!string.IsNullOrWhiteSpace(ereignis.Ort))
            {
                ErinnerungskarteOrtText.Text = "📍 " + ereignis.Ort;
                ErinnerungskarteOrtText.Visibility = Visibility.Visible;
            }
            else
            {
                ErinnerungskarteOrtText.Visibility = Visibility.Collapsed;
            }

            if (ereignis.Stichwoerter != null && ereignis.Stichwoerter.Count > 0)
            {
                ErinnerungskarteStichwoerterText.Text = "🏷️ " + string.Join(", ", ereignis.Stichwoerter);
                ErinnerungskarteStichwoerterText.Visibility = Visibility.Visible;
            }
            else
            {
                ErinnerungskarteStichwoerterText.Visibility = Visibility.Collapsed;
            }

            if (ereignis.Bewertungen != null && ereignis.Bewertungen.Count > 0)
            {
                ErinnerungskarteBewertungenText.Text = "⭐ " + string.Join(", ", ereignis.Bewertungen);
                ErinnerungskarteBewertungenText.Visibility = Visibility.Visible;
            }
            else
            {
                ErinnerungskarteBewertungenText.Visibility = Visibility.Collapsed;
            }

            if (ereignis.Beteiligte != null && ereignis.Beteiligte.Count > 0)
            {
                ErinnerungskarteBeteiligteText.Text = "👥 Auch dabei: " + string.Join(", ", ereignis.Beteiligte);
                ErinnerungskarteBeteiligteText.Visibility = Visibility.Visible;
            }
            else
            {
                ErinnerungskarteBeteiligteText.Visibility = Visibility.Collapsed;
            }

            List<string> beschreibungUndBemerkungen = new List<string>();

            if (!string.IsNullOrWhiteSpace(ereignis.Beschreibung))
            {
                beschreibungUndBemerkungen.Add(ereignis.Beschreibung);
            }

            if (!string.IsNullOrWhiteSpace(ereignis.Bemerkungen))
            {
                beschreibungUndBemerkungen.Add(ereignis.Bemerkungen);
            }

            if (beschreibungUndBemerkungen.Count > 0)
            {
                ErinnerungskarteBemerkungenText.Text = "📝 " + string.Join("\n", beschreibungUndBemerkungen);
                ErinnerungskarteBemerkungenText.Visibility = Visibility.Visible;
            }
            else
            {
                ErinnerungskarteBemerkungenText.Visibility = Visibility.Collapsed;
            }

            if (ereignis.WeitereFotoDateinamen != null && ereignis.WeitereFotoDateinamen.Count > 0)
            {
                ErinnerungskarteWeitereFotosText.Text = "📷 + " + ereignis.WeitereFotoDateinamen.Count + " weitere Erinnerungen zu diesem Ereignis";
                ErinnerungskarteWeitereFotosText.Visibility = Visibility.Visible;
            }
            else
            {
                ErinnerungskarteWeitereFotosText.Visibility = Visibility.Collapsed;
            }

            // Titelbild der Karte: bevorzugt das Foto des Ereignisses. Gibt es
            // noch keines, zeigen wir ersatzweise das Titelbild der Person,
            // damit die Karte nicht leer wirkt (reine Darstellungsentscheidung,
            // kein neues Datenfeld).
            bool ereignisBildGezeigt = ZeigeBildFuerKarte(
                ErinnerungsOrdnerFuer(person, ereignis),
                ereignis.EreignisFotoDateiname);

            if (!ereignisBildGezeigt)
            {
                ZeigeBildFuerKarte(PersonErinnerungsOrdner(person), person.TitelbildDateiname);
            }

            ErinnerungskartePlatzhalterText.Visibility = Visibility.Collapsed;
            ErinnerungskartePanel.Visibility = Visibility.Visible;

            ZeigeVerknuepfungen(person, ereignis);
        }

        // Laedt ein Bild in die Erinnerungskarte, falls die Datei existiert.
        // Gibt zurueck, ob tatsaechlich ein Bild angezeigt wurde.
        private bool ZeigeBildFuerKarte(string ordner, string dateiname)
        {
            if (dateiname != null)
            {
                string pfad = Path.Combine(ordner, dateiname);

                if (File.Exists(pfad))
                {
                    BitmapImage bild = new BitmapImage();
                    bild.BeginInit();
                    bild.CacheOption = BitmapCacheOption.OnLoad;
                    bild.UriSource = new Uri(pfad);
                    bild.EndInit();

                    ErinnerungskarteBild.Source = bild;
                    return true;
                }
            }

            ErinnerungskarteBild.Source = null;
            return false;
        }
        // ============================================================
        // BUILD 2.3: PERSON BESITZT ERINNERUNGEN (nicht nur ein Titelbild)
        // ============================================================
        // Architekturbeschluss "Abschluss Etappe A": eine Erinnerung einer
        // Person ist jedes Foto, das über eines ihrer Ereignisse mit ihr
        // verbunden ist (Titelbild+EreignisFoto+WeitereFotos). Das
        // Titelbild selbst zählt bewusst NICHT mit - es bleibt laut
        // Architekt "nur das Gesicht der Person".

        private static int ZaehleErinnerungenDerPerson(Person person)
        {
            if (person == null)
            {
                return 0;
            }

            // Build 2.4: die direkte Erinnerungssammlung der Person
            // (unabhängig von Ereignissen) zählt jetzt mit.
            int anzahl = person.ErinnerungsDateinamen != null ? person.ErinnerungsDateinamen.Count : 0;

            if (person.Ereignisse != null)
            {
                foreach (Ereignis ereignis in person.Ereignisse)
                {
                    if (!string.IsNullOrEmpty(ereignis.EreignisFotoDateiname))
                    {
                        anzahl++;
                    }

                    if (ereignis.WeitereFotoDateinamen != null)
                    {
                        anzahl += ereignis.WeitereFotoDateinamen.Count;
                    }
                }
            }

            return anzahl;
        }

        private void ZeigePersonErinnerungenLink(Person person)
        {
            if (person == null)
            {
                PersonErinnerungenLinkText.Text = "";
                PersonErinnerungenLinkText.Tag = null;
                return;
            }

            int anzahl = ZaehleErinnerungenDerPerson(person);
            PersonErinnerungenLinkText.Text = James.PersonErinnerungenLink(anzahl);
            PersonErinnerungenLinkText.Tag = anzahl > 0 ? person : null;
        }

        // Build 2.9 (Etappe B.1): Öffnet das einfache Erinnerungsfenster mit
        // allen Vorschaubildern dieser Person - sowohl direkt zugeordnete
        // Erinnerungen als auch alle über Ereignisse verknüpften Fotos.
        // Bewusst nur anzeigen (siehe ErinnerungenFenster) - keine
        // Bearbeitung, keine Titelbild-Auswahl mehr an dieser Stelle.
        private void PersonErinnerungenLinkText_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            Person person = PersonErinnerungenLinkText.Tag as Person;

            if (person == null)
            {
                return;
            }

            List<ErinnerungsInfo> erinnerungenListe = SammelErinnerungenFuerPerson(person);

            if (erinnerungenListe.Count == 0)
            {
                return;
            }

            ErinnerungenFenster fenster = new ErinnerungenFenster(James.ErinnerungenFensterTitelPerson(person.ToString()), erinnerungenListe, LiesVisuelleMerkmale, SpeichereVisuelleMerkmale, ZaehleVorkommenVisuellesMerkmal);
            fenster.Owner = this;
            fenster.Show();
        }

        // Sammelt alle Erinnerungen einer Person samt Kontext (Build 3.0) -
        // direkt zugeordnete (ErinnerungsDateinamen, ohne Ereignis-Kontext)
        // und alle über ihre Ereignisse verknüpften Fotos (mit vollem
        // Kontext: Titel, Datum, Ort, Beschreibung, Beteiligte).
        private List<ErinnerungsInfo> SammelErinnerungenFuerPerson(Person person)
        {
            List<ErinnerungsInfo> erinnerungenListe = new List<ErinnerungsInfo>();

            if (person.ErinnerungsDateinamen != null)
            {
                string ordner = PersonErinnerungsOrdner(person);

                foreach (string dateiname in person.ErinnerungsDateinamen)
                {
                    erinnerungenListe.Add(new ErinnerungsInfo
                    {
                        Pfad = Path.Combine(ordner, dateiname),
                        PersonName = person.ToString()
                    });
                }
            }

            if (person.Ereignisse != null)
            {
                foreach (Ereignis ereignis in person.Ereignisse)
                {
                    erinnerungenListe.AddRange(SammelErinnerungenFuerEreignis(person, ereignis));
                }
            }

            return erinnerungenListe;
        }

        // Sammelt alle Erinnerungen eines einzelnen Ereignisses samt
        // Kontext (Titelbild + weitere Fotos, jeweils mit denselben
        // Ereignis-Angaben, da sie zum selben Ereignis gehören).
        // Etappe B.1: unterstützt jetzt auch person == null (freies
        // Ereignis) - ersetzt die bisherige zweite, fast identische
        // Methode SammelErinnerungenFuerFreiesEreignis.
        private List<ErinnerungsInfo> SammelErinnerungenFuerEreignis(Person person, Ereignis ereignis)
        {
            List<ErinnerungsInfo> erinnerungenListe = new List<ErinnerungsInfo>();
            string ordner = ErinnerungsOrdnerFuer(person, ereignis);

            List<string> beschreibungsTeile = new List<string>();

            if (!string.IsNullOrWhiteSpace(ereignis.Beschreibung))
            {
                beschreibungsTeile.Add(ereignis.Beschreibung);
            }

            if (!string.IsNullOrWhiteSpace(ereignis.Bemerkungen))
            {
                beschreibungsTeile.Add(ereignis.Bemerkungen);
            }

            string beschreibung = string.Join("\n", beschreibungsTeile);

            List<string> alleDateinamen = new List<string>();

            if (!string.IsNullOrEmpty(ereignis.EreignisFotoDateiname))
            {
                alleDateinamen.Add(ereignis.EreignisFotoDateiname);
            }

            if (ereignis.WeitereFotoDateinamen != null)
            {
                alleDateinamen.AddRange(ereignis.WeitereFotoDateinamen);
            }

            foreach (string dateiname in alleDateinamen)
            {
                erinnerungenListe.Add(new ErinnerungsInfo
                {
                    Pfad = Path.Combine(ordner, dateiname),
                    Titel = ereignis.Titel,
                    Datum = ereignis.Datum,
                    Ort = ereignis.Ort,
                    Beschreibung = beschreibung,
                    PersonName = person != null ? person.ToString() : "",
                    Beteiligte = ereignis.Beteiligte
                });
            }

            return erinnerungenListe;
        }

        // Build 2.9 (Etappe B.1): Öffnet dasselbe einfache Erinnerungsfenster,
        // diesmal beschränkt auf die Erinnerungen eines einzelnen Ereignisses.
        private void EreignisErinnerungenLinkText_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            Ereignis ereignis = EreignisErinnerungenLinkText.Tag as Ereignis;
            Person person = PersonenListe.SelectedItem as Person;

            if (ereignis == null || person == null)
            {
                return;
            }

            List<ErinnerungsInfo> erinnerungenListe = SammelErinnerungenFuerEreignis(person, ereignis);

            if (erinnerungenListe.Count == 0)
            {
                return;
            }

            ErinnerungenFenster fenster = new ErinnerungenFenster(James.ErinnerungenFensterTitelEreignis(ereignis.Titel), erinnerungenListe, LiesVisuelleMerkmale, SpeichereVisuelleMerkmale, ZaehleVorkommenVisuellesMerkmal);
            fenster.Owner = this;
            fenster.Show();
        }

        // Aktualisiert den "Erinnerungen (N)"-Link für das aktuell
        // ausgewählte Ereignis (analog zu ZeigePersonErinnerungenLink).
        private void ZeigeEreignisErinnerungenLink(Ereignis ereignis)
        {
            if (ereignis == null)
            {
                EreignisErinnerungenLinkText.Text = "";
                EreignisErinnerungenLinkText.Tag = null;
                return;
            }

            int anzahl = (!string.IsNullOrEmpty(ereignis.EreignisFotoDateiname) ? 1 : 0) +
                         (ereignis.WeitereFotoDateinamen != null ? ereignis.WeitereFotoDateinamen.Count : 0);

            EreignisErinnerungenLinkText.Text = James.PersonErinnerungenLink(anzahl);
            EreignisErinnerungenLinkText.Tag = anzahl > 0 ? ereignis : null;
        }

        // ============================================================
        // BUILD 1.8: ERINNERUNGEN VERKNÜPFEN
        // ============================================================
        // Architekturbeschluss 011: James beginnt, Zusammenhänge zwischen
        // Erinnerungen sichtbar zu machen - "Dazu könnten auch gehören"
        // auf der Erinnerungskarte. Ausschließlich aus bereits
        // vorhandenen Daten, in fester Rangfolge (gleiche Person >
        // gleiche Beziehung > gemeinsames Stichwort > gleiche Jahreszeit
        // > ähnliche Zeit > gleicher Ort). James nennt dabei NUR die
        // Kategorie ("gleicher Ort"), niemals eine Erklärung, warum das
        // wichtig sein könnte - das zu beurteilen bleibt dem Benutzer
        // überlassen. Noch keine KI, keine automatische Geschichte,
        // keine neuen Datenfelder.

        private const int VerknuepfungenMax = 5;

        private void ZeigeVerknuepfungen(Person basisPerson, Ereignis basisEreignis)
        {
            List<Verknuepfung> verknuepfungen = ErmittleVerknuepfungen(basisPerson, basisEreignis);

            if (verknuepfungen.Count == 0)
            {
                ErinnerungskarteVerknuepfungenPanel.Visibility = Visibility.Collapsed;
                return;
            }

            ErinnerungskarteVerknuepfungenListe.ItemsSource = verknuepfungen;
            ErinnerungskarteVerknuepfungenPanel.Visibility = Visibility.Visible;
        }

        private List<Verknuepfung> ErmittleVerknuepfungen(Person basisPerson, Ereignis basisEreignis)
        {
            List<Verknuepfung> kandidaten = new List<Verknuepfung>();

            foreach (Person person in allePersonen)
            {
                SammleVerknuepfungsKandidaten(person, false, basisPerson, basisEreignis, kandidaten);
            }

            foreach (object element in ArchivListe.Items)
            {
                Person person = element as Person;

                if (person != null)
                {
                    SammleVerknuepfungsKandidaten(person, true, basisPerson, basisEreignis, kandidaten);
                }
            }

            return kandidaten
                .OrderBy(k => k.Prioritaet)
                .Take(VerknuepfungenMax)
                .ToList();
        }

        private void SammleVerknuepfungsKandidaten(Person kandidatPerson, bool istArchiviert, Person basisPerson, Ereignis basisEreignis, List<Verknuepfung> ergebnis)
        {
            if (kandidatPerson.Ereignisse == null)
            {
                return;
            }

            foreach (Ereignis kandidatEreignis in kandidatPerson.Ereignisse)
            {
                if (kandidatEreignis.Id == basisEreignis.Id)
                {
                    // nicht sich selbst als eigene Verknuepfung vorschlagen
                    continue;
                }

                int prioritaet = ErmittleVerknuepfungsprioritaet(basisPerson, basisEreignis, kandidatPerson, kandidatEreignis);

                if (prioritaet > 0)
                {
                    ergebnis.Add(new Verknuepfung
                    {
                        Grund = VerknuepfungsgrundText(prioritaet),
                        Prioritaet = prioritaet,
                        Person = kandidatPerson,
                        Ereignis = kandidatEreignis,
                        IstArchiviert = istArchiviert
                    });
                }
            }
        }

        // Prüft die Kategorien in der vom Architekten vorgegebenen
        // Rangfolge und gibt die erste zutreffende zurück (1 = höchste
        // Rangstufe). 0 bedeutet: keine Verbindung gefunden.
        private static int ErmittleVerknuepfungsprioritaet(Person basisPerson, Ereignis basisEreignis, Person kandidatPerson, Ereignis kandidatEreignis)
        {
            if (kandidatPerson.Id == basisPerson.Id)
            {
                return 1;
            }

            string basisBeziehung = basisPerson.Beziehung != null ? basisPerson.Beziehung.ToString() : null;
            string kandidatBeziehung = kandidatPerson.Beziehung != null ? kandidatPerson.Beziehung.ToString() : null;

            if (!string.IsNullOrWhiteSpace(basisBeziehung) && string.Equals(basisBeziehung, kandidatBeziehung, StringComparison.OrdinalIgnoreCase))
            {
                return 2;
            }

            if (basisEreignis.Stichwoerter != null && kandidatEreignis.Stichwoerter != null)
            {
                foreach (string wort in basisEreignis.Stichwoerter)
                {
                    if (kandidatEreignis.Stichwoerter.Any(k => string.Equals(k, wort, StringComparison.OrdinalIgnoreCase)))
                    {
                        return 3;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(basisEreignis.Jahreszeit) &&
                string.Equals(basisEreignis.Jahreszeit, kandidatEreignis.Jahreszeit, StringComparison.OrdinalIgnoreCase))
            {
                return 4;
            }

            DateTime basisDatum;
            DateTime kandidatDatum;

            if (VersucheDatumZuLesen(basisEreignis.Datum, out basisDatum) &&
                VersucheDatumZuLesen(kandidatEreignis.Datum, out kandidatDatum))
            {
                if (Math.Abs((basisDatum - kandidatDatum).TotalDays) <= 21)
                {
                    return 5;
                }
            }

            if (!string.IsNullOrWhiteSpace(basisEreignis.Ort) &&
                string.Equals(basisEreignis.Ort.Trim(), kandidatEreignis.Ort != null ? kandidatEreignis.Ort.Trim() : null, StringComparison.OrdinalIgnoreCase))
            {
                return 6;
            }

            return 0;
        }

        private static string VerknuepfungsgrundText(int prioritaet)
        {
            switch (prioritaet)
            {
                case 1: return "gleiche Person";
                case 2: return "gleiche Beziehung";
                case 3: return "gleiches Stichwort";
                case 4: return "gleiche Jahreszeit";
                case 5: return "ähnliche Zeit";
                case 6: return "gleicher Ort";
                default: return "";
            }
        }

        private void ErinnerungskarteVerknuepfungenListe_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Verknuepfung verknuepfung = ErinnerungskarteVerknuepfungenListe.SelectedItem as Verknuepfung;

            if (verknuepfung == null)
            {
                return;
            }

            if (verknuepfung.IstArchiviert)
            {
                bool ergebnis = James.FrageJaNein(James.FrageTrefferImArchiv(verknuepfung.Person.ToString()));

                if (ergebnis)
                {
                    HoleAusArchivZurueckAufSchreibtisch(verknuepfung.Person, verknuepfung.Ereignis);
                }
            }
            else
            {
                SucheTextBox.Text = "";

                HauptTabControl.SelectedIndex = 0;
                PersonenListe.SelectedItem = verknuepfung.Person;
                EreignisseListe.SelectedItem = verknuepfung.Ereignis;
            }

            // Karte auf die angeklickte Verknuepfung umschalten, damit man
            // durch das entstehende Erinnerungsnetz weiterklicken kann.
            ZeigeErinnerungskarte(verknuepfung.Person, verknuepfung.Ereignis);

            // Auswahl zurücksetzen, damit ein erneutes Anklicken desselben
            // Eintrags wieder ein SelectionChanged auslöst.
            ErinnerungskarteVerknuepfungenListe.SelectedItem = null;
        }
    }
}