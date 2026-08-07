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

        private void ArchivListe_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Person person = ArchivListe.SelectedItem as Person;
            ZeigeArchivFoto(person);
            ArchivAktionPanel.Visibility = Visibility.Collapsed;
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

            ErinnerungenFenster fenster = new ErinnerungenFenster(James.ErinnerungenFensterTitelPerson(person.ToString()), erinnerungenListe, LiesVisuelleMerkmale, SpeichereVisuelleMerkmale, ZaehleVorkommenVisuellesMerkmal);
            fenster.Owner = this;
            fenster.Show();
        }

        private void ZeigeArchivFoto(Person person)
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

                    ArchivFotoBild.Source = bild;
                    return;
                }
            }

            ArchivFotoBild.Source = null;
        }

        private void ArchivAktion_Click(object sender, RoutedEventArgs e)
        {
            if (ArchivListe.SelectedItem == null)
            {
                James.Hinweis(James.BitteArchivPersonAuswaehlen);
                return;
            }

            if (ArchivAktionPanel.Visibility == Visibility.Visible)
            {
                ArchivAktionPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                ArchivAktionPanel.Visibility = Visibility.Visible;
            }
        }

        private void ArchivFotoHinzufuegen_Click(object sender, RoutedEventArgs e)
        {
            Person person = ArchivListe.SelectedItem as Person;

            if (person == null)
            {
                return;
            }

            HauptTabControl.SelectedIndex = ArbeitsmappeTabIndex;
            James.Hinweis(James.ArbeitsmappeUmleitungPerson(person.ToString()));
        }

        private void ArchivZurueck_Click(object sender, RoutedEventArgs e)
        {
            Person person = ArchivListe.SelectedItem as Person;

            if (person == null)
            {
                return;
            }

            HoleAusArchivZurueckAufSchreibtisch(person, null);
        }

        private void HoleAusArchivZurueckAufSchreibtisch(Person person, Ereignis auszuwaehlendesEreignis)
        {
            ArchivListe.Items.Remove(person);
            allePersonen.Add(person);

            SortiereAllePersonen();
            AktualisierePersonenAnzeige();

            SpeichereDaten();

            ArchivAktionPanel.Visibility = Visibility.Collapsed;
            ArchivFotoBild.Source = null;

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

        // Etappe B.1b, Punkt 4: ArchivListe ist NICHT im Extended-Auswahlmodus
        // (siehe MainWindow.xaml) - hier ist immer nur genau eine Person
        // gemeint, daher weiterhin die einfache, einzelne Bestätigung.
        private void ArchivPersonInPapierkorb_Click(object sender, RoutedEventArgs e)
        {
            Person person = ArchivListe.SelectedItem as Person;

            if (person == null)
            {
                return;
            }

            bool ergebnis = James.FrageJaNein(James.FrageInPapierkorbEinzeln(person.ToString()), James.TitelEntscheidung, MessageBoxImage.Warning);

            if (ergebnis)
            {
                ArchivListe.Items.Remove(person);
                PapierkorbListe.Items.Add(person);
                SpeichereDaten();

                ArchivAktionPanel.Visibility = Visibility.Collapsed;
                ArchivFotoBild.Source = null;
            }
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
