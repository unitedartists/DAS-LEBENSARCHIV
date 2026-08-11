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
        private void ZeigeFoto(Person person)
        {
            if (person != null && person.TitelbildDateiname != null)
            {
                string pfad = Path.Combine(PersonErinnerungsOrdner(person), person.TitelbildDateiname);

                if (File.Exists(pfad))
                {
                    BitmapImage bild = new BitmapImage();
                    bild.BeginInit();
                    bild.CacheOption = BitmapCacheOption.OnLoad;
                    bild.UriSource = new Uri(pfad);
                    bild.EndInit();

                    PersonFotoBild.Source = bild;
                    return;
                }
            }

            PersonFotoBild.Source = null;
        }

        // ============================================================
        // BUILD 1.2: BEZIEHUNGEN VERSTEHEN
        // ============================================================

        private void ZeigeBeziehung(Person person)
        {
            BeziehungRolleComboBox.Text = person != null && person.Beziehung != null
                ? person.Beziehung.ToString()
                : "";
        }

        private Beziehung ErstelleBeziehungAusEingabe()
        {
            string text = BeziehungRolleComboBox.Text != null ? BeziehungRolleComboBox.Text.Trim() : "";

            if (text == "")
            {
                return null;
            }

            return new Beziehung
            {
                Rolle = text,
                EigeneBezeichnung = null
            };
        }

        private void PersonenListe_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Person person = PersonenListe.SelectedItem as Person;
            aktuellBearbeitetePerson = person;

            if (person != null)
            {
                VornameTextBox.Text = person.Vorname;
                NachnameTextBox.Text = person.Nachname;
                GeburtTextBox.Text = person.Geburt;
                OrtTextBox.Text = person.Ort;
            }
            else
            {
                VornameTextBox.Clear();
                NachnameTextBox.Clear();
                GeburtTextBox.Clear();
                OrtTextBox.Clear();
            }

            ZeigeBeziehung(person);
            ZeigeFoto(person);
            AktualisiereEreignisseAnzeige(person);
            ZeigePersonErinnerungenLink(person);
            EreignisFormularPanel.Visibility = Visibility.Collapsed;
            SpeichereArbeitsstand();
        }

        // ============================================================
        // TÜV-REPARATUR TEIL B (08.08.): VEREINHEITLICHTES ARCHIV
        // ============================================================
        // A's Grundsatz "Gleiche Situation = gleiche Bedienung": eine
        // gemeinsame Aktionsleiste für Person/Ereignis/Sammlung statt drei
        // fast identischer, getrennter Bedienkonzepte (der bisherige
        // "Was möchten wir tun?"-Aufklapp-Button entfällt dadurch). James
        // muss dabei intern immer eindeutig wissen, welcher Typ UND welches
        // konkrete Element gerade gemeint ist (A's Punkt 5) - deshalb leert
        // eine Auswahl in einer der drei Listen automatisch die Auswahl in
        // den beiden anderen (siehe ArchivListe_SelectionChanged unten und
        // die Pendants ArchivEreignisseListe_SelectionChanged/
        // ArchivSammlungenListe_SelectionChanged in
        // MainWindow.BesondereEreignisse.cs/MainWindow.Sammlungen.cs).
        private enum ArchivTyp { Keine, Person, Ereignis, Sammlung }

        private ArchivTyp ErmittleAktuellenArchivTyp()
        {
            if (ArchivListe.SelectedItems.Count > 0)
            {
                return ArchivTyp.Person;
            }

            if (ArchivEreignisseListe.SelectedItems.Count > 0)
            {
                return ArchivTyp.Ereignis;
            }

            if (ArchivSammlungenListe.SelectedItems.Count > 0)
            {
                return ArchivTyp.Sammlung;
            }

            return ArchivTyp.Keine;
        }

        // Aktualisiert die gemeinsame Aktionsleiste (Beschriftung + welche
        // Buttons aktiv sind), passend zur aktuellen Auswahl. "Ansehen" und
        // "Zuordnen" ergeben nur bei genau EINEM ausgewählten Element Sinn
        // (die Erinnerungsansicht bzw. die Umleitungs-Nachricht beziehen
        // sich immer auf ein konkretes Element) - "Zurück auf den
        // Schreibtisch" und "In den Papierkorb legen" funktionieren dagegen
        // auch mit mehreren gleichzeitig ausgewählten Elementen.
        private void AktualisiereArchivAuswahl()
        {
            ArchivTyp typ = ErmittleAktuellenArchivTyp();
            int anzahl = 0;
            string text = "Bitte links eine Person, ein Ereignis oder eine Sammlung auswählen.";

            switch (typ)
            {
                case ArchivTyp.Person:
                    List<Person> personen = ArchivListe.SelectedItems.Cast<Person>().ToList();
                    anzahl = personen.Count;
                    text = anzahl == 1 ? "Ausgewählt: " + personen[0].ToString() : anzahl + " Personen ausgewählt";
                    break;

                case ArchivTyp.Ereignis:
                    List<Ereignis> ereignisse = ArchivEreignisseListe.SelectedItems.Cast<Ereignis>().ToList();
                    anzahl = ereignisse.Count;
                    text = anzahl == 1 ? "Ausgewählt: " + ereignisse[0].Titel : anzahl + " Ereignisse ausgewählt";
                    break;

                case ArchivTyp.Sammlung:
                    List<Sammlung> sammlungenAuswahl = ArchivSammlungenListe.SelectedItems.Cast<Sammlung>().ToList();
                    anzahl = sammlungenAuswahl.Count;
                    text = anzahl == 1 ? "Ausgewählt: " + sammlungenAuswahl[0].Titel : anzahl + " Sammlungen ausgewählt";
                    break;
            }

            ArchivAuswahlText.Text = text;

            ArchivAnsehenButton.IsEnabled = anzahl == 1;
            ArchivZuordnenButton.IsEnabled = anzahl == 1;
            ArchivZurueckButton.IsEnabled = anzahl >= 1;
            ArchivInPapierkorbButton.IsEnabled = anzahl >= 1;
        }

        private void ArchivListe_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ArchivListe.SelectedItems.Count > 0)
            {
                ArchivEreignisseListe.SelectedItem = null;
                ArchivSammlungenListe.SelectedItem = null;
            }

            AktualisiereArchivAuswahl();
        }

        private void ArchivPersonErinnerungenAnsehen_Click(object sender, RoutedEventArgs e)
        {
            Person person = ArchivListe.SelectedItem as Person;

            if (person == null)
            {
                return;
            }

            List<ErinnerungsInfo> erinnerungenListe = SammelErinnerungenFuerPerson(person);

            if (erinnerungenListe.Count == 0)
            {
                return;
            }

            ErinnerungenFenster fenster = new ErinnerungenFenster(James.ErinnerungenFensterTitelPerson(person.ToString()), erinnerungenListe, LiesVisuelleMerkmale, SpeichereVisuelleMerkmale, ZaehleVorkommenVisuellesMerkmal, ErstelleEntferneAusPersonCallback(person), SendeMarkierteZurArbeitsmappe);
            fenster.Owner = this;
            fenster.Show();
        }

        // A/Opa-INTEGRATIONSAUFTRAG (11.08.), Punkt 3+4: "Zuordnen" öffnet
        // jetzt direkt den Arbeitsmotor mit dieser Person vorausgewählt
        // (Weg C) - dort sieht Opa sofort die bereits zugeordneten
        // Erinnerungen (inkl. Entfernen in den Zuordnungs-Papierkorb) und
        // kann über Suche weitere zuordnen, alles ohne physische Kopie.
        // Ersetzt den bisherigen Redirect-Hinweis zur alten Arbeitsmappe.
        private void ArchivFotoHinzufuegen_Click(object sender, RoutedEventArgs e)
        {
            Person person = ArchivListe.SelectedItem as Person;

            if (person == null)
            {
                return;
            }

            OeffneArbeitsmotorFuerZiel(ZuordnungsZielTyp.Person, person.Id);
        }

        // ============================================================
        // GEMEINSAME AKTIONSLEISTE - DISPATCHER
        // ============================================================
        // Diese vier Methoden hängen an den vier Buttons der neuen
        // gemeinsamen Aktionsleiste (siehe MainWindow.xaml, Archiv-Tab) und
        // leiten je nach aktuellem ArchivTyp an die bestehende, bewährte
        // typ-spezifische Logik weiter - keine neue Parallellogik.

        private void ArchivAnsehen_Click(object sender, RoutedEventArgs e)
        {
            switch (ErmittleAktuellenArchivTyp())
            {
                case ArchivTyp.Person:
                    ArchivPersonErinnerungenAnsehen_Click(sender, e);
                    break;
                case ArchivTyp.Ereignis:
                    ArchivEreignisErinnerungenAnsehen_Click(sender, e);
                    break;
                case ArchivTyp.Sammlung:
                    ArchivSammlungErinnerungenAnsehen_Click(sender, e);
                    break;
            }
        }

        // Teil B, Punkt 3: "Zuordnen" verweist für alle drei Typen
        // einheitlich auf die Arbeitsmappe - kein eigener
        // Zuordnungsmechanismus im Archiv (Prinzip, das bei Person schon
        // bestand, jetzt auch für Ereignis/Sammlung).
        private void ArchivZuordnen_Click(object sender, RoutedEventArgs e)
        {
            switch (ErmittleAktuellenArchivTyp())
            {
                case ArchivTyp.Person:
                    ArchivFotoHinzufuegen_Click(sender, e);
                    break;
                case ArchivTyp.Ereignis:
                    ArchivEreignisZuordnen_Click(sender, e);
                    break;
                case ArchivTyp.Sammlung:
                    ArchivSammlungZuordnen_Click(sender, e);
                    break;
            }
        }

        private void ArchivZurueck_Click(object sender, RoutedEventArgs e)
        {
            switch (ErmittleAktuellenArchivTyp())
            {
                case ArchivTyp.Person:
                    List<Person> personen = ArchivListe.SelectedItems.Cast<Person>().ToList();

                    if (personen.Count == 1)
                    {
                        HoleAusArchivZurueckAufSchreibtisch(personen[0], null);
                    }
                    else if (personen.Count > 1)
                    {
                        foreach (Person person in personen)
                        {
                            ArchivListe.Items.Remove(person);
                            allePersonen.Add(person);
                        }

                        SortiereAllePersonen();
                        AktualisierePersonenAnzeige();
                        SpeichereDaten();

                        ZeigeStatusMeldung(personen.Count + " Personen sind zurück auf Ihrem Schreibtisch.");
                    }
                    break;

                case ArchivTyp.Ereignis:
                    ArchivEreignisWiederherstellen_Click(sender, e);
                    break;

                case ArchivTyp.Sammlung:
                    ArchivSammlungWiederherstellen_Click(sender, e);
                    break;
            }
        }

        // BUGFIX (TÜV-Reparatur 07.08., Priorität 1) + TEIL B (08.08.): jetzt
        // mehrfachauswahlfähig wie die Pendants bei Ereignis/Sammlung
        // (ArchivListe hat jetzt ebenfalls SelectionMode="Extended"), mit
        // namentlicher Bestätigung bei mehreren markierten Personen.
        private void ArchivInPapierkorb_Click(object sender, RoutedEventArgs e)
        {
            switch (ErmittleAktuellenArchivTyp())
            {
                case ArchivTyp.Person:
                    List<Person> personen = ArchivListe.SelectedItems.Cast<Person>().ToList();

                    if (personen.Count == 0)
                    {
                        return;
                    }

                    string frage = personen.Count == 1
                        ? James.FrageInPapierkorbEinzeln(personen[0].ToString())
                        : James.FrageInPapierkorbMehrere(personen.Select(p => p.ToString()).ToList());

                    bool ergebnis = James.FrageJaNein(frage, James.TitelEntscheidung, MessageBoxImage.Warning);

                    if (!ergebnis)
                    {
                        return;
                    }

                    foreach (Person person in personen)
                    {
                        ArchivListe.Items.Remove(person);
                        PapierkorbListe.Items.Add(person);
                    }

                    SpeichereDaten();

                    ZeigeStatusMeldung(personen.Count == 1
                        ? James.InPapierkorbGelegtEinzeln(personen[0].ToString())
                        : James.InPapierkorbGelegtMehrere(personen.Count));
                    break;

                case ArchivTyp.Ereignis:
                    ArchivEreignisInPapierkorb_Click(sender, e);
                    break;

                case ArchivTyp.Sammlung:
                    ArchivSammlungInPapierkorb_Click(sender, e);
                    break;
            }
        }

        private void HoleAusArchivZurueckAufSchreibtisch(Person person, Ereignis auszuwaehlendesEreignis)
        {
            ArchivListe.Items.Remove(person);
            allePersonen.Add(person);

            SortiereAllePersonen();
            AktualisierePersonenAnzeige();

            SpeichereDaten();

            HauptTabControl.SelectedIndex = 0;

            StartseiteBereich.Visibility = Visibility.Collapsed;
            EreignisBereich.Visibility = Visibility.Collapsed;
            EreignismappeBereich.Visibility = Visibility.Collapsed;
            PersonenFormularBereich.Visibility = Visibility.Visible;
            PersonenListeBereich.Visibility = Visibility.Visible;

            PersonenListe.SelectedItem = person;

            VornameTextBox.Text = person.Vorname;
            NachnameTextBox.Text = person.Nachname;
            GeburtTextBox.Text = person.Geburt;
            OrtTextBox.Text = person.Ort;

            aktuellBearbeitetePerson = person;

            ZeigeBeziehung(person);
            ZeigeFoto(person);
            AktualisiereEreignisseAnzeige(person);
            ZeigePersonErinnerungenLink(person);

            if (auszuwaehlendesEreignis != null)
            {
                EreignisseListe.SelectedItem = auszuwaehlendesEreignis;
            }

            ZeigeStatusMeldung(James.ZurueckAufSchreibtisch(person.ToString()));
        }

        private void FotoHinzufuegen_Click(object sender, RoutedEventArgs e)
        {
            Person person = PersonenListe.SelectedItem as Person;

            if (person == null)
            {
                James.Hinweis(James.BittePersonAuswaehlen);
                return;
            }

            HauptTabControl.SelectedIndex = ArbeitsmappeTabIndex;
            James.Hinweis(James.ArbeitsmappeUmleitungPerson(person.ToString()));
        }

        private void SortiereAllePersonen()
        {
            allePersonen = allePersonen
                .OrderBy(p => p.Nachname ?? "")
                .ThenBy(p => p.Vorname ?? "")
                .ToList();
        }

        private void AktualisierePersonenAnzeige()
        {
            Person vorherAusgewaehlt = PersonenListe.SelectedItem as Person;
            string suchtext = SucheTextBox == null ? "" : SucheTextBox.Text.Trim().ToLower();

            IEnumerable<Person> gefiltert = allePersonen;

            if (suchtext != "")
            {
                gefiltert = allePersonen.Where(p =>
                    (p.Vorname != null && p.Vorname.ToLower().Contains(suchtext)) ||
                    (p.Nachname != null && p.Nachname.ToLower().Contains(suchtext)));
            }

            PersonenListe.Items.Clear();

            foreach (Person person in gefiltert)
            {
                PersonenListe.Items.Add(person);
            }

            if (vorherAusgewaehlt != null && PersonenListe.Items.Contains(vorherAusgewaehlt))
            {
                PersonenListe.SelectedItem = vorherAusgewaehlt;
            }

            AnzahlText.Text = allePersonen.Count + " Erinnerungen";
        }

        private void Suche_TextChanged(object sender, TextChangedEventArgs e)
        {
            AktualisierePersonenAnzeige();
        }

        private void Speichern_Click(object sender, RoutedEventArgs e)
        {
            string vorname = VornameTextBox.Text.Trim();
            string nachname = NachnameTextBox.Text.Trim();

            if (vorname == "" && nachname == "")
            {
                James.Hinweis(James.BitteErstNamenEingeben);

                VornameTextBox.Focus();
                return;
            }

            Person gespeichertePerson;

            if (aktuellBearbeitetePerson != null)
            {
                aktuellBearbeitetePerson.Vorname = vorname;
                aktuellBearbeitetePerson.Nachname = nachname;
                aktuellBearbeitetePerson.Geburt = GeburtTextBox.Text.Trim();
                aktuellBearbeitetePerson.Ort = OrtTextBox.Text.Trim();
                aktuellBearbeitetePerson.Beziehung = ErstelleBeziehungAusEingabe();
                aktuellBearbeitetePerson.ModifiedAt = DateTime.Now;

                SortiereAllePersonen();
                AktualisierePersonenAnzeige();

                SpeichereDaten();

                ZeigeStatusMeldung(James.ErinnerungAktualisiert(aktuellBearbeitetePerson.ToString()));

                gespeichertePerson = aktuellBearbeitetePerson;
            }
            else
            {
                Person neuePerson = new Person
                {
                    Vorname = vorname,
                    Nachname = nachname,
                    Geburt = GeburtTextBox.Text.Trim(),
                    Ort = OrtTextBox.Text.Trim(),
                    Beziehung = ErstelleBeziehungAusEingabe()
                };

                allePersonen.Add(neuePerson);

                SortiereAllePersonen();
                AktualisierePersonenAnzeige();

                SpeichereDaten();

                ZeigeStatusMeldung(James.ErinnerungGespeichert(neuePerson.ToString()));

                gespeichertePerson = neuePerson;
            }

            aktuellBearbeitetePerson = null;

            VornameTextBox.Clear();
            NachnameTextBox.Clear();
            GeburtTextBox.Clear();
            OrtTextBox.Clear();
            BeziehungRolleComboBox.Text = "";

            if (PersonenListe.SelectedItem != null)
            {
                PersonenListe.SelectedItem = null;
            }

            VornameTextBox.Focus();
        }

        // BUGFIX (TÜV-Reparatur 07.08., Priorität 1): PersonenListe hat
        // SelectionMode="Extended" - bei mehreren markierten Personen
        // nennt die Bestätigung jetzt namentlich, WER betroffen ist,
        // statt nur eine Zahl zu zeigen.
        private void Loeschen_Click(object sender, RoutedEventArgs e)
        {
            List<Person> ausgewaehltePersonen = PersonenListe.SelectedItems.Cast<Person>().ToList();

            if (ausgewaehltePersonen.Count == 0)
            {
                James.Hinweis(James.BittePersonenAuswaehlen);

                return;
            }

            string frage;

            if (ausgewaehltePersonen.Count == 1)
            {
                frage = James.FrageInPapierkorbEinzeln(ausgewaehltePersonen[0].ToString());
            }
            else
            {
                frage = James.FrageInPapierkorbMehrere(ausgewaehltePersonen.Select(p => p.ToString()).ToList());
            }

            bool ergebnis = James.FrageJaNein(frage, James.TitelEntscheidung, MessageBoxImage.Warning);

            if (ergebnis)
            {
                foreach (Person person in ausgewaehltePersonen)
                {
                    allePersonen.Remove(person);
                    PapierkorbListe.Items.Add(person);

                    if (aktuellBearbeitetePerson == person)
                    {
                        aktuellBearbeitetePerson = null;
                    }
                }

                AktualisierePersonenAnzeige();
                SpeichereDaten();

                VornameTextBox.Clear();
                NachnameTextBox.Clear();
                GeburtTextBox.Clear();
                OrtTextBox.Clear();
                BeziehungRolleComboBox.Text = "";

                PersonFotoBild.Source = null;
                EreignisseListe.Items.Clear();
                EreignisAuswahlPanel.Visibility = Visibility.Collapsed;
                EreignisFotoBild.Source = null;

                if (ausgewaehltePersonen.Count == 1)
                {
                    ZeigeStatusMeldung(James.InPapierkorbGelegtEinzeln(ausgewaehltePersonen[0].ToString()));
                }
                else
                {
                    ZeigeStatusMeldung(James.InPapierkorbGelegtMehrere(ausgewaehltePersonen.Count));
                }
            }
        }

        private void Bearbeiten_Click(object sender, RoutedEventArgs e)
        {
            if (PersonenListe.SelectedItem == null)
            {
                James.Hinweis(James.BittePersonAuswaehlen);

                return;
            }

            Person person = PersonenListe.SelectedItem as Person;

            if (person == null)
            {
                return;
            }

            VornameTextBox.Text = person.Vorname;
            NachnameTextBox.Text = person.Nachname;
            GeburtTextBox.Text = person.Geburt;
            OrtTextBox.Text = person.Ort;

            aktuellBearbeitetePerson = person;

            ZeigeBeziehung(person);

            VornameTextBox.Focus();
        }

        private void Archivieren_Click(object sender, RoutedEventArgs e)
        {
            List<Person> ausgewaehltePersonen = PersonenListe.SelectedItems.Cast<Person>().ToList();

            if (ausgewaehltePersonen.Count == 0)
            {
                James.Hinweis(James.BittePersonenAuswaehlen);

                return;
            }

            foreach (Person person in ausgewaehltePersonen)
            {
                allePersonen.Remove(person);
                ArchivListe.Items.Add(person);

                if (aktuellBearbeitetePerson == person)
                {
                    aktuellBearbeitetePerson = null;
                }
            }

            AktualisierePersonenAnzeige();
            SpeichereDaten();

            VornameTextBox.Clear();
            NachnameTextBox.Clear();
            GeburtTextBox.Clear();
            OrtTextBox.Clear();
            BeziehungRolleComboBox.Text = "";

            PersonFotoBild.Source = null;
            EreignisseListe.Items.Clear();
            EreignisAuswahlPanel.Visibility = Visibility.Collapsed;
            EreignisFotoBild.Source = null;

            if (ausgewaehltePersonen.Count == 1)
            {
                ZeigeStatusMeldung(James.ImArchivAngekommen(ausgewaehltePersonen[0].ToString()));
            }
            else
            {
                ZeigeStatusMeldung(James.ImArchivAngekommenMehrere(ausgewaehltePersonen.Count));
            }
        }

        private void Wiederherstellen_Click(object sender, RoutedEventArgs e)
        {
            List<Person> ausgewaehltePersonen = PapierkorbListe.SelectedItems.Cast<Person>().ToList();

            if (ausgewaehltePersonen.Count == 0)
            {
                James.Hinweis(James.BittePapierkorbAuswaehlen);

                return;
            }

            foreach (Person person in ausgewaehltePersonen)
            {
                PapierkorbListe.Items.Remove(person);
                allePersonen.Add(person);
            }

            SortiereAllePersonen();
            AktualisierePersonenAnzeige();

            SpeichereDaten();

            if (ausgewaehltePersonen.Count == 1)
            {
                Person wiederhergestelltePerson = ausgewaehltePersonen[0];

                HauptTabControl.SelectedIndex = 0;

                StartseiteBereich.Visibility = Visibility.Collapsed;
                EreignisBereich.Visibility = Visibility.Collapsed;
                EreignismappeBereich.Visibility = Visibility.Collapsed;
                PersonenFormularBereich.Visibility = Visibility.Visible;
                PersonenListeBereich.Visibility = Visibility.Visible;

                PersonenListe.SelectedItem = wiederhergestelltePerson;

                VornameTextBox.Text = wiederhergestelltePerson.Vorname;
                NachnameTextBox.Text = wiederhergestelltePerson.Nachname;
                GeburtTextBox.Text = wiederhergestelltePerson.Geburt;
                OrtTextBox.Text = wiederhergestelltePerson.Ort;

                aktuellBearbeitetePerson = wiederhergestelltePerson;

                ZeigeBeziehung(wiederhergestelltePerson);
                ZeigeFoto(wiederhergestelltePerson);
                AktualisiereEreignisseAnzeige(wiederhergestelltePerson);
                ZeigePersonErinnerungenLink(wiederhergestelltePerson);

                James.Hinweis(James.WiederhergestelltEinzeln(wiederhergestelltePerson.ToString()), James.TitelWiederhergestellt);
            }
            else
            {
                James.Hinweis(James.WiederhergestelltMehrere(ausgewaehltePersonen.Count), James.TitelWiederhergestellt);
            }
        }

        // BUGFIX (TÜV-Reparatur 07.08., Priorität 1): PapierkorbListe hat
        // SelectionMode="Extended" - bei mehreren markierten Personen zeigt
        // die endgültige Löschbestätigung jetzt namentlich, WER betroffen
        // ist, statt nur eine Zahl. Gerade hier (unwiderrufliches Löschen)
        // ist das besonders wichtig.
        private void EndgueltigLoeschen_Click(object sender, RoutedEventArgs e)
        {
            List<Person> ausgewaehltePersonen = PapierkorbListe.SelectedItems.Cast<Person>().ToList();

            if (ausgewaehltePersonen.Count == 0)
            {
                James.Hinweis(James.BittePapierkorbAuswaehlen);

                return;
            }

            string frage;

            if (ausgewaehltePersonen.Count == 1)
            {
                frage = James.FrageEndgueltigLoeschenEinzeln(ausgewaehltePersonen[0].ToString());
            }
            else
            {
                frage = James.FrageEndgueltigLoeschenMehrere(ausgewaehltePersonen.Select(p => p.ToString()).ToList());
            }

            bool ergebnis = James.FrageJaNein(frage, James.TitelEndgueltigeEntscheidung, MessageBoxImage.Warning);

            if (ergebnis)
            {
                foreach (Person person in ausgewaehltePersonen)
                {
                    PapierkorbListe.Items.Remove(person);
                }

                SpeichereDaten();
            }
        }
    }
}
