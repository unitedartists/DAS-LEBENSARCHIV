using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace DAS_LEBENSARCHIV
{
    public partial class MainWindow : Window
    {
        // ============================================================
        // EREIGNISSE (Build 0.5) + EREIGNIS-FOTOS (Build 0.6)
        // ============================================================

        private void AktualisiereEreignisseAnzeige(Person person)
        {
            EreignisseListe.Items.Clear();
            EreignisAuswahlPanel.Visibility = Visibility.Collapsed;
            EreignisFotoBild.Source = null;

            if (person != null && person.Ereignisse != null)
            {
                foreach (Ereignis ereignis in person.Ereignisse)
                {
                    EreignisseListe.Items.Add(ereignis);
                }
            }
        }

        private void ZeigeEreignisFoto(Person person, Ereignis ereignis)
        {
            if (person != null && ereignis != null && ereignis.EreignisFotoDateiname != null)
            {
                string pfad = Path.Combine(ErinnerungsOrdnerFuer(person, ereignis), ereignis.EreignisFotoDateiname);

                if (File.Exists(pfad))
                {
                    BitmapImage bild = new BitmapImage();
                    bild.BeginInit();
                    bild.CacheOption = BitmapCacheOption.OnLoad;
                    bild.UriSource = new Uri(pfad);
                    bild.EndInit();

                    EreignisFotoBild.Source = bild;
                    return;
                }
            }

            EreignisFotoBild.Source = null;
        }

        private void EreignisseListe_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Ereignis ereignis = EreignisseListe.SelectedItem as Ereignis;
            Person person = PersonenListe.SelectedItem as Person;

            if (ereignis == null)
            {
                EreignisAuswahlPanel.Visibility = Visibility.Collapsed;
                EreignisFotoBild.Source = null;
                EreignisDetailsText.Text = "";
                BewertungFavoritCheckBox.IsChecked = false;
                BewertungWichtigCheckBox.IsChecked = false;
                BewertungFamiliengeschichteCheckBox.IsChecked = false;
                BewertungLebensbuchCheckBox.IsChecked = false;
                BewertungUeberpruefenCheckBox.IsChecked = false;
                BewertungPrivatCheckBox.IsChecked = false;
                BewertungUnbekanntCheckBox.IsChecked = false;
                ZeigeEreignisErinnerungenLink(null);
                SpeichereArbeitsstand();
                return;
            }

            EreignisAuswahlPanel.Visibility = Visibility.Visible;
            ZeigeEreignisFoto(person, ereignis);
            EreignisDetailsText.Text = ErstelleEreignisDetailsText(ereignis);
            ZeigeBewertungen(ereignis);
            ZeigeEreignisErinnerungenLink(ereignis);
            SpeichereArbeitsstand();
        }

        // ============================================================
        // BUILD 0.9: ERINNERUNGEN BEWERTEN
        // ============================================================

        private void ZeigeBewertungen(Ereignis ereignis)
        {
            List<string> bewertungen = ereignis.Bewertungen ?? new List<string>();

            BewertungFavoritCheckBox.IsChecked = bewertungen.Contains("Favorit");
            BewertungWichtigCheckBox.IsChecked = bewertungen.Contains("Besonders wichtig");
            BewertungFamiliengeschichteCheckBox.IsChecked = bewertungen.Contains("Familiengeschichte");
            BewertungLebensbuchCheckBox.IsChecked = bewertungen.Contains("Für das Lebensbuch geeignet");
            BewertungUeberpruefenCheckBox.IsChecked = bewertungen.Contains("Noch überprüfen");
            BewertungPrivatCheckBox.IsChecked = bewertungen.Contains("Privat");
            BewertungUnbekanntCheckBox.IsChecked = bewertungen.Contains("Unbekannt");
        }

        private void BewertungSpeichern_Click(object sender, RoutedEventArgs e)
        {
            Ereignis ereignis = EreignisseListe.SelectedItem as Ereignis;
            Person person = PersonenListe.SelectedItem as Person;

            if (ereignis == null)
            {
                James.Hinweis(James.BitteEreignisAuswaehlen);
                return;
            }

            List<string> neueBewertungen = new List<string>();

            if (BewertungFavoritCheckBox.IsChecked == true)
            {
                neueBewertungen.Add("Favorit");
            }

            if (BewertungWichtigCheckBox.IsChecked == true)
            {
                neueBewertungen.Add("Besonders wichtig");
            }

            if (BewertungFamiliengeschichteCheckBox.IsChecked == true)
            {
                neueBewertungen.Add("Familiengeschichte");
            }

            if (BewertungLebensbuchCheckBox.IsChecked == true)
            {
                neueBewertungen.Add("Für das Lebensbuch geeignet");
            }

            if (BewertungUeberpruefenCheckBox.IsChecked == true)
            {
                neueBewertungen.Add("Noch überprüfen");
            }

            if (BewertungPrivatCheckBox.IsChecked == true)
            {
                neueBewertungen.Add("Privat");
            }

            if (BewertungUnbekanntCheckBox.IsChecked == true)
            {
                neueBewertungen.Add("Unbekannt");
            }

            ereignis.Bewertungen = neueBewertungen;
            ereignis.ModifiedAt = DateTime.Now;

            if (person != null)
            {
                person.ModifiedAt = DateTime.Now;
            }

            SpeichereDaten();

            ZeigeStatusMeldung(James.BewertungGespeichert(ereignis.Titel));
        }

        private string ErstelleEreignisDetailsText(Ereignis ereignis)
        {
            List<string> zeilen = new List<string>();

            if (!string.IsNullOrWhiteSpace(ereignis.Beschreibung))
            {
                zeilen.Add(ereignis.Beschreibung);
            }

            if (!string.IsNullOrWhiteSpace(ereignis.Ort))
            {
                zeilen.Add("Ort: " + ereignis.Ort);
            }

            if (!string.IsNullOrWhiteSpace(ereignis.Jahreszeit))
            {
                zeilen.Add("Jahreszeit: " + ereignis.Jahreszeit);
            }

            if (ereignis.Stichwoerter != null && ereignis.Stichwoerter.Count > 0)
            {
                zeilen.Add("Stichwörter: " + string.Join(", ", ereignis.Stichwoerter));
            }

            if (!string.IsNullOrWhiteSpace(ereignis.Bemerkungen))
            {
                zeilen.Add("Bemerkungen: " + ereignis.Bemerkungen);
            }

            if (ereignis.Beteiligte != null && ereignis.Beteiligte.Count > 0)
            {
                List<string> beteiligteMitKennzeichen = ereignis.Beteiligte
                    .Select(name => allePersonen.Any(p => p.ToString().Equals(name, StringComparison.OrdinalIgnoreCase))
                        ? name + " ✓"
                        : name)
                    .ToList();

                zeilen.Add("Wer war noch dabei: " + string.Join(", ", beteiligteMitKennzeichen));
            }

            if (ereignis.WeitereFotoDateinamen != null && ereignis.WeitereFotoDateinamen.Count > 0)
            {
                zeilen.Add("+ " + ereignis.WeitereFotoDateinamen.Count + " weitere Erinnerungen zu diesem Ereignis");
            }

            return string.Join("\n", zeilen);
        }

        private void EreignisFotoHinzufuegen_Click(object sender, RoutedEventArgs e)
        {
            Person person = PersonenListe.SelectedItem as Person;
            Ereignis ereignis = EreignisseListe.SelectedItem as Ereignis;

            if (person == null || ereignis == null)
            {
                James.Hinweis(James.BittePersonUndEreignisAuswaehlen);
                return;
            }

            HauptTabControl.SelectedIndex = ArbeitsmappeTabIndex;
            James.Hinweis(James.ArbeitsmappeUmleitungEreignis(ereignis.Titel));
        }

        private void EreignisHinzufuegen_Click(object sender, RoutedEventArgs e)
        {
            Person person = PersonenListe.SelectedItem as Person;

            if (person == null)
            {
                James.Hinweis(James.BittePersonAuswaehlen);
                return;
            }

            EreignisTitelTextBox.Clear();
            EreignisBeschreibungTextBox.Clear();
            EreignisDatumTextBox.Clear();
            EreignisOrtTextBox.Clear();
            EreignisJahreszeitComboBox.SelectedIndex = 0;
            EreignisStichwoerterTextBox.Clear();
            EreignisBemerkungenTextBox.Clear();
            EreignisBeteiligteTextBox.Clear();

            EreignisFormularPanel.Visibility = Visibility.Visible;
            EreignisTitelTextBox.Focus();
        }

        private void EreignisSpeichern_Click(object sender, RoutedEventArgs e)
        {
            Person person = PersonenListe.SelectedItem as Person;

            if (person == null)
            {
                James.Hinweis(James.BittePersonAuswaehlen);
                return;
            }

            string titel = EreignisTitelTextBox.Text.Trim();

            if (titel == "")
            {
                James.Hinweis(James.BitteEreignisTitelEingeben);
                return;
            }

            string jahreszeit = "";
            ComboBoxItem ausgewaehlteJahreszeit = EreignisJahreszeitComboBox.SelectedItem as ComboBoxItem;

            if (ausgewaehlteJahreszeit != null)
            {
                jahreszeit = ausgewaehlteJahreszeit.Content.ToString();
            }

            List<string> stichwoerter = EreignisStichwoerterTextBox.Text
                .Split(',')
                .Select(teil => teil.Trim())
                .Where(teil => teil != "")
                .ToList();

            List<string> beteiligte = EreignisBeteiligteTextBox.Text
                .Split(',')
                .Select(teil => teil.Trim())
                .Where(teil => teil != "")
                .ToList();

            Ereignis neuesEreignis = new Ereignis
            {
                Titel = titel,
                Beschreibung = EreignisBeschreibungTextBox.Text.Trim(),
                Datum = EreignisDatumTextBox.Text.Trim(),
                Ort = EreignisOrtTextBox.Text.Trim(),
                Jahreszeit = jahreszeit,
                Stichwoerter = stichwoerter,
                Bemerkungen = EreignisBemerkungenTextBox.Text.Trim(),
                Beteiligte = beteiligte
            };

            if (person.Ereignisse == null)
            {
                person.Ereignisse = new List<Ereignis>();
            }

            person.Ereignisse.Add(neuesEreignis);
            person.ModifiedAt = DateTime.Now;

            AktualisiereEreignisseAnzeige(person);
            ZeigePersonErinnerungenLink(person);

            SpeichereDaten();

            EreignisFormularPanel.Visibility = Visibility.Collapsed;

            EreignisseListe.SelectedItem = neuesEreignis;

            ZeigeStatusMeldung(James.EreignisZugeordnet(neuesEreignis.Titel, person.ToString()));
        }
    }
}
