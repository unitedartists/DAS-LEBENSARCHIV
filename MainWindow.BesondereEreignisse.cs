using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DAS_LEBENSARCHIV
{
    public partial class MainWindow : Window
    {
        // ============================================================
        // BUILD 5.1: EREIGNISVERWALTUNG
        // ============================================================

        private void EreignisListeSchreibtisch_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Ereignis ereignis = EreignisListeSchreibtisch.SelectedItem as Ereignis;

            aktuellBearbeitetesFreiesEreignis = ereignis;

            if (ereignis == null)
            {
                EreignisTitelSchreibtischTextBox.Clear();
                FreiesEreignisErinnerungenLinkText.Visibility = Visibility.Collapsed;
                EreignisSchreibtischArchivierenButton.Visibility = Visibility.Collapsed;
                return;
            }

            EreignisTitelSchreibtischTextBox.Text = ereignis.Titel;

            int anzahl = (!string.IsNullOrEmpty(ereignis.EreignisFotoDateiname) ? 1 : 0)
                + (ereignis.WeitereFotoDateinamen != null ? ereignis.WeitereFotoDateinamen.Count : 0);

            FreiesEreignisErinnerungenLinkText.Text = James.PersonErinnerungenLink(anzahl);
            FreiesEreignisErinnerungenLinkText.Tag = anzahl > 0 ? ereignis : null;
            FreiesEreignisErinnerungenLinkText.Visibility = Visibility.Visible;

            EreignisSchreibtischArchivierenButton.Visibility = Visibility.Visible;
        }

        private void EreignisSchreibtischSpeichern_Click(object sender, RoutedEventArgs e)
        {
            string titel = EreignisTitelSchreibtischTextBox.Text.Trim();

            if (titel == "")
            {
                James.Hinweis(James.BitteEreignisTitelEingeben);
                return;
            }

            if (aktuellBearbeitetesFreiesEreignis != null)
            {
                aktuellBearbeitetesFreiesEreignis.Titel = titel;
                aktuellBearbeitetesFreiesEreignis.ModifiedAt = DateTime.Now;

                SpeichereDaten();

                ZeigeStatusMeldung(James.FreiesEreignisAktualisiert(titel));

                aktuellBearbeitetesFreiesEreignis = null;
                EreignisTitelSchreibtischTextBox.Clear();
                FreiesEreignisErinnerungenLinkText.Visibility = Visibility.Collapsed;
                EreignisSchreibtischArchivierenButton.Visibility = Visibility.Collapsed;

                if (EreignisListeSchreibtisch.SelectedItem != null)
                {
                    EreignisListeSchreibtisch.SelectedItem = null;
                }

                AktualisiereFreieEreignisseAnzeige();
                EreignisTitelSchreibtischTextBox.Focus();
            }
            else
            {
                Ereignis neuesEreignis = new Ereignis { Titel = titel };

                freieEreignisse.Add(neuesEreignis);

                SpeichereDaten();
                AktualisiereFreieEreignisseAnzeige();

                EreignisTitelSchreibtischTextBox.Clear();

                ZeigeEreignismappe(neuesEreignis);
            }
        }

        // ============================================================
        // BUILD 6.0: DIE EREIGNISMAPPE
        // ============================================================

        private Ereignis aktuelleEreignismappe = null;

        private void ZeigeEreignismappe(Ereignis ereignis)
        {
            aktuelleEreignismappe = ereignis;

            EreignisBereich.Visibility = Visibility.Collapsed;
            EreignismappeBereich.Visibility = Visibility.Visible;

            EreignismappeTitelText.Text = James.EreignismappeAngelegt(ereignis.Titel);
        }

        private void EreignismappeBilder_Click(object sender, RoutedEventArgs e)
        {
            if (aktuelleEreignismappe == null)
            {
                return;
            }

            string ereignisTitel = aktuelleEreignismappe.Titel;

            EreignismappeBereich.Visibility = Visibility.Collapsed;

            HauptTabControl.SelectedIndex = ArbeitsmappeTabIndex;

            James.Hinweis(James.EreignismappeBilderHinweis(ereignisTitel));
        }

        private void EreignismappeZurueck_Click(object sender, MouseButtonEventArgs e)
        {
            EreignismappeBereich.Visibility = Visibility.Collapsed;
            EreignisBereich.Visibility = Visibility.Visible;
        }

        private void FreiesEreignisErinnerungenLinkText_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            Ereignis ereignis = FreiesEreignisErinnerungenLinkText.Tag as Ereignis;

            if (ereignis == null)
            {
                return;
            }

            List<ErinnerungsInfo> erinnerungenListe = SammelErinnerungenFuerEreignis(null, ereignis);

            if (erinnerungenListe.Count == 0)
            {
                return;
            }

            ErinnerungenFenster fenster = new ErinnerungenFenster(James.ErinnerungenFensterTitelEreignis(ereignis.Titel), erinnerungenListe, LiesVisuelleMerkmale, SpeichereVisuelleMerkmale, ZaehleVorkommenVisuellesMerkmal);
            fenster.Owner = this;
            fenster.Show();
        }

        private void EreignisSchreibtischArchivieren_Click(object sender, RoutedEventArgs e)
        {
            Ereignis ereignis = EreignisListeSchreibtisch.SelectedItem as Ereignis;

            if (ereignis == null)
            {
                return;
            }

            freieEreignisse.Remove(ereignis);
            freieEreignisseArchiv.Add(ereignis);

            SpeichereDaten();

            aktuellBearbeitetesFreiesEreignis = null;
            EreignisTitelSchreibtischTextBox.Clear();
            FreiesEreignisErinnerungenLinkText.Visibility = Visibility.Collapsed;
            EreignisSchreibtischArchivierenButton.Visibility = Visibility.Collapsed;

            AktualisiereFreieEreignisseAnzeige();

            ZeigeStatusMeldung(James.FreiesEreignisArchiviert(ereignis.Titel));
        }
    }
}
