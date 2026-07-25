using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DAS_LEBENSARCHIV
{
    public partial class MainWindow : Window
    {
        private void ArbeitsmappeMitEreignisVerbinden_Click(object sender, RoutedEventArgs e)
        {
            if (arbeitsmappeAusgewaehlt.Count == 0)
            {
                return;
            }

            List<Person> alleAuswaehlbarenPersonen = allePersonen
                .Concat(ArchivListe.Items.Cast<Person>())
                .ToList();

            ArbeitsmappePersonComboBox.ItemsSource = alleAuswaehlbarenPersonen;
            ArbeitsmappePersonComboBox.SelectedIndex = -1;
            ArbeitsmappeEreignisComboBox.ItemsSource = null;

            VersteckeAlleArbeitsmappenPanels();
            ArbeitsmappeEreignisAuswahlPanel.Visibility = Visibility.Visible;
        }

        private void ArbeitsmappePersonComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Person person = ArbeitsmappePersonComboBox.SelectedItem as Person;
            ArbeitsmappeEreignisComboBox.ItemsSource = person != null ? person.Ereignisse : null;

            if (person != null)
            {
                int anzahlEreignisse = person.Ereignisse != null ? person.Ereignisse.Count : 0;
                ArbeitsmappeStatusText.Text = James.DiagnosePersonAusgewaehltFuerEreignis(person.ToString(), anzahlEreignisse);
            }
        }

        private void ArbeitsmappeEreignisBestaetigen_Click(object sender, RoutedEventArgs e)
        {
            Person person = ArbeitsmappePersonComboBox.SelectedItem as Person;
            Ereignis ereignis = ArbeitsmappeEreignisComboBox.SelectedItem as Ereignis;

            if (person == null || ereignis == null)
            {
                James.Hinweis(James.BittePersonUndEreignisAuswaehlen);
                return;
            }

            List<string> pfade = arbeitsmappeAusgewaehlt.ToList();

            VerknuepfeArbeitsmappenDateienMitEreignis(person, ereignis, pfade);

            ArbeitsmappeEreignisAuswahlPanel.Visibility = Visibility.Collapsed;
            AktualisiereArbeitsmappe();
        }

        private int VerbindeDateienMitEreignis(Ereignis ereignis, List<string> pfade, string zielOrdner)
        {
            int verbunden = 0;

            Directory.CreateDirectory(zielOrdner);

            foreach (string pfad in pfade)
            {
                GefundeneDatei datei = arbeitsmappeAlleDateien.FirstOrDefault(d => d.VollstaendigerPfad == pfad);

                if (datei == null || datei.Dateityp != "Bilder" || !File.Exists(datei.VollstaendigerPfad))
                {
                    continue;
                }

                string dateiendung = Path.GetExtension(datei.VollstaendigerPfad);
                string neuerDateiname = Guid.NewGuid().ToString() + dateiendung;
                string zielPfad = Path.Combine(zielOrdner, neuerDateiname);

                File.Copy(datei.VollstaendigerPfad, zielPfad, true);

                if (string.IsNullOrEmpty(ereignis.EreignisFotoDateiname))
                {
                    ereignis.EreignisFotoDateiname = neuerDateiname;
                }
                else
                {
                    if (ereignis.WeitereFotoDateinamen == null)
                    {
                        ereignis.WeitereFotoDateinamen = new List<string>();
                    }

                    ereignis.WeitereFotoDateinamen.Add(neuerDateiname);
                }

                arbeitsmappeAusgewaehlt.Remove(pfad);
                arbeitsmappeBereitsZugeordnet.Add(pfad);
                verbunden++;
            }

            if (verbunden > 0)
            {
                ereignis.ModifiedAt = DateTime.Now;
            }

            return verbunden;
        }

        private void VerknuepfeArbeitsmappenDateienMitEreignis(Person person, Ereignis ereignis, List<string> pfade)
        {
            if (person == null || ereignis == null || pfade == null || pfade.Count == 0)
            {
                return;
            }

            try
            {
                int verbunden = VerbindeDateienMitEreignis(ereignis, pfade, ErinnerungsOrdnerFuer(person, ereignis));

                if (verbunden > 0)
                {
                    person.ModifiedAt = DateTime.Now;
                    SpeichereDaten();
                    SpeichereArbeitsmappeZugeordnet();
                }

                if (verbunden == 0)
                {
                    James.Hinweis(James.ArbeitsmappeNurBilder);
                }
                else
                {
                    int gesamtErinnerungen = ZaehleErinnerungenDerPerson(person);

                    ArbeitsmappeStatusText.Text = verbunden == 1
                        ? James.ArbeitsmappeVerbunden(ereignis.Titel, person.ToString(), gesamtErinnerungen)
                        : James.ArbeitsmappeVerbundenMehrere(verbunden, ereignis.Titel, person.ToString(), gesamtErinnerungen);

                    ZeigeArbeitsmappeEreignisOeffnenButton(person, ereignis);
                }

                if (verbunden > 0)
                {
                    AktualisiereErinnerungskarteFallsBetroffen(person, ereignis);
                    AktualisiereSchreibtischFallsBetroffen(person);
                }
            }
            catch (Exception ex)
            {
                James.Problem(James.FehlerBeimSpeichernFoto(ex.Message));
            }
        }

        private void ArbeitsmappeNeuesEreignisAnlegen_Click(object sender, RoutedEventArgs e)
        {
            if (arbeitsmappeAusgewaehlt.Count == 0)
            {
                return;
            }

            if (allePersonen.Count == 0)
            {
                James.Hinweis(James.ArbeitsmappeBittePersonZuerst);
                return;
            }

            ArbeitsmappeNeuesEreignisPersonComboBox.ItemsSource = allePersonen;
            ArbeitsmappeNeuesEreignisPersonComboBox.SelectedIndex = -1;

            VersteckeAlleArbeitsmappenPanels();
            ArbeitsmappeNeuesEreignisPersonPanel.Visibility = Visibility.Visible;
        }

        private void ArbeitsmappeNeuesEreignisWeiter_Click(object sender, RoutedEventArgs e)
        {
            Person person = ArbeitsmappeNeuesEreignisPersonComboBox.SelectedItem as Person;

            if (person == null)
            {
                James.Hinweis(James.ArbeitsmappeBitteEreignisPersonAuswaehlen);
                return;
            }

            arbeitsmappeNeuesEreignisPerson = person;

            ArbeitsmappeEreignisTitelTextBox.Clear();
            ArbeitsmappeEreignisBeschreibungTextBox.Clear();
            ArbeitsmappeEreignisDatumTextBox.Clear();
            ArbeitsmappeEreignisOrtTextBox.Clear();
            ArbeitsmappeEreignisJahreszeitComboBox.SelectedIndex = 0;
            ArbeitsmappeEreignisStichwoerterTextBox.Clear();
            ArbeitsmappeEreignisBemerkungenTextBox.Clear();
            ArbeitsmappeEreignisBeteiligteTextBox.Clear();

            VersteckeAlleArbeitsmappenPanels();
            ArbeitsmappeNeuesEreignisFormularPanel.Visibility = Visibility.Visible;
            ArbeitsmappeEreignisTitelTextBox.Focus();
        }

        private void ArbeitsmappeEreignisSpeichern_Click(object sender, RoutedEventArgs e)
        {
            Person person = arbeitsmappeNeuesEreignisPerson;

            if (person == null)
            {
                James.Hinweis(James.ArbeitsmappeBitteEreignisPersonAuswaehlen);
                return;
            }

            string titel = ArbeitsmappeEreignisTitelTextBox.Text.Trim();

            if (titel == "")
            {
                James.Hinweis(James.BitteEreignisTitelEingeben);
                return;
            }

            string jahreszeit = "";
            ComboBoxItem ausgewaehlteJahreszeit = ArbeitsmappeEreignisJahreszeitComboBox.SelectedItem as ComboBoxItem;

            if (ausgewaehlteJahreszeit != null)
            {
                jahreszeit = ausgewaehlteJahreszeit.Content.ToString();
            }

            List<string> stichwoerter = ArbeitsmappeEreignisStichwoerterTextBox.Text
                .Split(',')
                .Select(teil => teil.Trim())
                .Where(teil => teil != "")
                .ToList();

            List<string> beteiligte = ArbeitsmappeEreignisBeteiligteTextBox.Text
                .Split(',')
                .Select(teil => teil.Trim())
                .Where(teil => teil != "")
                .ToList();

            Ereignis neuesEreignis = new Ereignis
            {
                Titel = titel,
                Beschreibung = ArbeitsmappeEreignisBeschreibungTextBox.Text.Trim(),
                Datum = ArbeitsmappeEreignisDatumTextBox.Text.Trim(),
                Ort = ArbeitsmappeEreignisOrtTextBox.Text.Trim(),
                Jahreszeit = jahreszeit,
                Stichwoerter = stichwoerter,
                Bemerkungen = ArbeitsmappeEreignisBemerkungenTextBox.Text.Trim(),
                Beteiligte = beteiligte
            };

            if (person.Ereignisse == null)
            {
                person.Ereignisse = new List<Ereignis>();
            }

            person.Ereignisse.Add(neuesEreignis);
            person.ModifiedAt = DateTime.Now;

            SpeichereDaten();

            List<string> pfade = arbeitsmappeAusgewaehlt.ToList();
            int vorherAusgewaehlt = arbeitsmappeAusgewaehlt.Count;

            VerknuepfeArbeitsmappenDateienMitEreignis(person, neuesEreignis, pfade);

            bool wurdeFotoVerbunden = arbeitsmappeAusgewaehlt.Count < vorherAusgewaehlt;

            if (!wurdeFotoVerbunden)
            {
                int gesamtErinnerungen = ZaehleErinnerungenDerPerson(person);
                ArbeitsmappeStatusText.Text = James.ArbeitsmappeEreignisAngelegtUndVerbunden(neuesEreignis.Titel, person.ToString(), gesamtErinnerungen);
            }

            ZeigeArbeitsmappeEreignisOeffnenButton(person, neuesEreignis);

            arbeitsmappeNeuesEreignisPerson = null;

            ArbeitsmappeNeuesEreignisFormularPanel.Visibility = Visibility.Collapsed;

            AktualisiereArbeitsmappe();
        }

        private void ArbeitsmappeNeuePersonAnlegen_Click(object sender, RoutedEventArgs e)
        {
            if (arbeitsmappeAusgewaehlt.Count == 0)
            {
                return;
            }

            ArbeitsmappeNeuePersonVornameTextBox.Clear();
            ArbeitsmappeNeuePersonNachnameTextBox.Clear();
            ArbeitsmappeNeuePersonGeburtTextBox.Clear();
            ArbeitsmappeNeuePersonOrtTextBox.Clear();
            ArbeitsmappeNeuePersonBeziehungComboBox.Text = "";

            VersteckeAlleArbeitsmappenPanels();
            ArbeitsmappeNeuePersonPanel.Visibility = Visibility.Visible;
            ArbeitsmappeNeuePersonVornameTextBox.Focus();
        }

        private void ArbeitsmappeNeuePersonSpeichern_Click(object sender, RoutedEventArgs e)
        {
            string vorname = ArbeitsmappeNeuePersonVornameTextBox.Text.Trim();
            string nachname = ArbeitsmappeNeuePersonNachnameTextBox.Text.Trim();

            if (vorname == "" && nachname == "")
            {
                James.Hinweis(James.BitteErstNamenEingeben);
                return;
            }

            string beziehungstext = ArbeitsmappeNeuePersonBeziehungComboBox.Text != null
                ? ArbeitsmappeNeuePersonBeziehungComboBox.Text.Trim()
                : "";

            Person neuePerson = new Person
            {
                Vorname = vorname,
                Nachname = nachname,
                Geburt = ArbeitsmappeNeuePersonGeburtTextBox.Text.Trim(),
                Ort = ArbeitsmappeNeuePersonOrtTextBox.Text.Trim(),
                Beziehung = beziehungstext == "" ? null : new Beziehung { Rolle = beziehungstext }
            };

            allePersonen.Add(neuePerson);

            SortiereAllePersonen();
            AktualisierePersonenAnzeige();

            SpeichereDaten();

            List<string> pfade = arbeitsmappeAusgewaehlt.ToList();

            VerknuepfeArbeitsmappenDateienMitPerson(neuePerson, pfade);

            ArbeitsmappeNeuePersonPanel.Visibility = Visibility.Collapsed;

            AktualisiereArbeitsmappe();
        }
        private void VersteckeAlleArbeitsmappenPanels()
        {
            ArbeitsmappeEreignisAuswahlPanel.Visibility = Visibility.Collapsed;
            ArbeitsmappePersonAuswahlPanel.Visibility = Visibility.Collapsed;
            ArbeitsmappeNeuesEreignisPersonPanel.Visibility = Visibility.Collapsed;
            ArbeitsmappeNeuesEreignisFormularPanel.Visibility = Visibility.Collapsed;
            ArbeitsmappeNeuePersonPanel.Visibility = Visibility.Collapsed;
            ArbeitsmappeNeuesFreiesEreignisPanel.Visibility = Visibility.Collapsed;
            ArbeitsmappeFreiesEreignisAuswahlPanel.Visibility = Visibility.Collapsed;
        }

        private void ZeigeArbeitsmappeEreignisOeffnenButton(Person person, Ereignis ereignis)
        {
            arbeitsmappeLetztesEreignisPerson = person;
            arbeitsmappeLetztesEreignis = ereignis;
            ArbeitsmappeEreignisOeffnenButton.Visibility = Visibility.Visible;
        }

        private void ArbeitsmappeEreignisOeffnen_Click(object sender, RoutedEventArgs e)
        {
            if (arbeitsmappeLetztesEreignisPerson == null || arbeitsmappeLetztesEreignis == null)
            {
                return;
            }

            SucheTextBox.Text = "";

            HauptTabControl.SelectedIndex = 0;
            PersonenListe.SelectedItem = arbeitsmappeLetztesEreignisPerson;
            EreignisseListe.SelectedItem = arbeitsmappeLetztesEreignis;

            ArbeitsmappeEreignisOeffnenButton.Visibility = Visibility.Collapsed;
        }

        private void VerknuepfeArbeitsmappenDateienMitPerson(Person person, List<string> pfade)
        {
            if (person == null || pfade == null || pfade.Count == 0)
            {
                return;
            }

            int verbunden = 0;

            try
            {
                string zielOrdner = PersonErinnerungsOrdner(person);
                Directory.CreateDirectory(zielOrdner);

                if (person.ErinnerungsDateinamen == null)
                {
                    person.ErinnerungsDateinamen = new List<string>();
                }

                foreach (string pfad in pfade)
                {
                    GefundeneDatei datei = arbeitsmappeAlleDateien.FirstOrDefault(d => d.VollstaendigerPfad == pfad);

                    if (datei == null || datei.Dateityp != "Bilder" || !File.Exists(datei.VollstaendigerPfad))
                    {
                        continue;
                    }

                    string dateiendung = Path.GetExtension(datei.VollstaendigerPfad);
                    string neuerDateiname = Guid.NewGuid().ToString() + dateiendung;
                    string zielPfad = Path.Combine(zielOrdner, neuerDateiname);

                    File.Copy(datei.VollstaendigerPfad, zielPfad, true);

                    person.ErinnerungsDateinamen.Add(neuerDateiname);

                    if (string.IsNullOrEmpty(person.TitelbildDateiname))
                    {
                        person.TitelbildDateiname = neuerDateiname;
                    }

                    arbeitsmappeAusgewaehlt.Remove(pfad);
                    arbeitsmappeBereitsZugeordnet.Add(pfad);
                    verbunden++;
                }

                if (verbunden == 0)
                {
                    James.Hinweis(James.ArbeitsmappeNurBilder);
                    return;
                }

                person.ModifiedAt = DateTime.Now;

                SpeichereDaten();
                SpeichereArbeitsmappeZugeordnet();

                int gesamtErinnerungen = ZaehleErinnerungenDerPerson(person);

                ArbeitsmappeStatusText.Text = verbunden == 1
                    ? James.ArbeitsmappeErinnerungZugeordnet(person.ToString(), gesamtErinnerungen)
                    : James.ArbeitsmappeErinnerungenZugeordnetMehrere(verbunden, person.ToString(), gesamtErinnerungen);

                AktualisiereSchreibtischFallsBetroffen(person);
                AktualisiereErinnerungskarteFallsBetroffen(person, null);
            }
            catch (Exception ex)
            {
                James.Problem(James.FehlerBeimSpeichernFoto(ex.Message));
            }
        }

        private void ArbeitsmappePersonZuordnen_Click(object sender, RoutedEventArgs e)
        {
            if (arbeitsmappeAusgewaehlt.Count == 0)
            {
                return;
            }

            List<Person> alleAuswaehlbarenPersonen = allePersonen
                .Concat(ArchivListe.Items.Cast<Person>())
                .ToList();

            ArbeitsmappeTitelbildPersonComboBox.ItemsSource = alleAuswaehlbarenPersonen;
            ArbeitsmappeTitelbildPersonComboBox.SelectedIndex = -1;

            VersteckeAlleArbeitsmappenPanels();
            ArbeitsmappePersonAuswahlPanel.Visibility = Visibility.Visible;
        }

        private void ArbeitsmappeTitelbildPersonComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Person person = ArbeitsmappeTitelbildPersonComboBox.SelectedItem as Person;

            if (person != null)
            {
                int anzahlVorher = ZaehleErinnerungenDerPerson(person);
                ArbeitsmappeStatusText.Text = James.DiagnosePersonAusgewaehltFuerErinnerungen(person.ToString(), anzahlVorher);
            }
        }

        private void ArbeitsmappeTitelbildBestaetigen_Click(object sender, RoutedEventArgs e)
        {
            Person person = ArbeitsmappeTitelbildPersonComboBox.SelectedItem as Person;

            if (person == null)
            {
                James.Hinweis(James.BittePersonAuswaehlen);
                return;
            }

            List<string> pfade = arbeitsmappeAusgewaehlt.ToList();

            VerknuepfeArbeitsmappenDateienMitPerson(person, pfade);

            int anzahlNachher = ZaehleErinnerungenDerPerson(person);
            ArbeitsmappeStatusText.Text = James.DiagnoseNachZuordnung(person.ToString(), anzahlNachher);

            ArbeitsmappePersonAuswahlPanel.Visibility = Visibility.Collapsed;
            AktualisiereArbeitsmappe();
        }

        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && ArbeitsmappeGrossansichtPanel.Visibility == Visibility.Visible)
            {
                ArbeitsmappeGrossansichtSchliessen_Click(sender, e);
            }
        }
    }
}
