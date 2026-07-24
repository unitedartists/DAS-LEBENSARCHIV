using System;
using System.Collections.Generic;
using System.Linq;
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
        // ============================================================
        // BUILD 5.0: ERINNERUNGEN ÜBER PERSONEN ODER EREIGNISSE
        // ============================================================

        private void ArbeitsmappeNeuesFreiesEreignisAnlegen_Click(object sender, RoutedEventArgs e)
        {
            if (arbeitsmappeAusgewaehlt.Count == 0)
            {
                return;
            }

            FreiesEreignisTitelTextBox.Clear();

            VersteckeAlleArbeitsmappenPanels();
            ArbeitsmappeNeuesFreiesEreignisPanel.Visibility = Visibility.Visible;
            FreiesEreignisTitelTextBox.Focus();
        }

        private void FreiesEreignisSpeichernUndZuordnen_Click(object sender, RoutedEventArgs e)
        {
            string titel = FreiesEreignisTitelTextBox.Text.Trim();

            if (titel == "")
            {
                James.Hinweis(James.BitteEreignisTitelEingeben);
                return;
            }

            Ereignis bestehendesEreignis = freieEreignisse
                .Concat(freieEreignisseArchiv)
                .FirstOrDefault(ereignis => string.Equals((ereignis.Titel ?? "").Trim(), titel, StringComparison.OrdinalIgnoreCase));

            Ereignis zielEreignis;

            if (bestehendesEreignis != null && James.FrageJaNein(James.FrageEreignisBereitsVorhanden(titel), James.TitelEntscheidung))
            {
                zielEreignis = bestehendesEreignis;
            }
            else
            {
                zielEreignis = new Ereignis { Titel = titel };
                freieEreignisse.Add(zielEreignis);
            }

            SpeichereDaten();
            AktualisiereFreieEreignisseAnzeige();

            List<string> pfade = arbeitsmappeAusgewaehlt.ToList();
            VerknuepfeArbeitsmappenDateienMitFreiemEreignis(zielEreignis, pfade);

            ArbeitsmappeNeuesFreiesEreignisPanel.Visibility = Visibility.Collapsed;
            AktualisiereArbeitsmappe();
        }

        private void ArbeitsmappeFreiesEreignisZuordnen_Click(object sender, RoutedEventArgs e)
        {
            if (arbeitsmappeAusgewaehlt.Count == 0)
            {
                return;
            }

            if (freieEreignisse.Count == 0 && freieEreignisseArchiv.Count == 0)
            {
                James.Hinweis(James.BitteErstFreiesEreignisAnlegen);
                return;
            }

            List<Ereignis> alleAuswaehlbaren = freieEreignisse.Concat(freieEreignisseArchiv).ToList();

            FreiesEreignisComboBox.ItemsSource = alleAuswaehlbaren;
            FreiesEreignisComboBox.SelectedIndex = -1;

            VersteckeAlleArbeitsmappenPanels();
            ArbeitsmappeFreiesEreignisAuswahlPanel.Visibility = Visibility.Visible;
        }

        private void FreiesEreignisBestaetigen_Click(object sender, RoutedEventArgs e)
        {
            Ereignis ereignis = FreiesEreignisComboBox.SelectedItem as Ereignis;

            if (ereignis == null)
            {
                James.Hinweis(James.BitteEreignisAuswaehlen);
                return;
            }

            List<string> pfade = arbeitsmappeAusgewaehlt.ToList();
            VerknuepfeArbeitsmappenDateienMitFreiemEreignis(ereignis, pfade);

            ArbeitsmappeFreiesEreignisAuswahlPanel.Visibility = Visibility.Collapsed;
            AktualisiereArbeitsmappe();
        }

        private void VerknuepfeArbeitsmappenDateienMitFreiemEreignis(Ereignis ereignis, List<string> pfade)
        {
            if (ereignis == null || pfade == null || pfade.Count == 0)
            {
                return;
            }

            try
            {
                int verbunden = VerbindeDateienMitEreignis(ereignis, pfade, ErinnerungsOrdnerFuer(null, ereignis));

                if (verbunden > 0)
                {
                    SpeichereDaten();
                    SpeichereArbeitsmappeZugeordnet();
                    AktualisiereFreieEreignisseAnzeige();
                }

                if (verbunden == 0)
                {
                    James.Hinweis(James.ArbeitsmappeNurBilder);
                }
                else if (verbunden == 1)
                {
                    ArbeitsmappeStatusText.Text = James.FreiesEreignisVerbunden(ereignis.Titel);
                }
                else
                {
                    ArbeitsmappeStatusText.Text = James.FreiesEreignisVerbundenMehrere(verbunden, ereignis.Titel);
                }
            }
            catch (Exception ex)
            {
                James.Problem(James.FehlerBeimSpeichernFoto(ex.Message));
            }
        }

        private void AktualisiereFreieEreignisseAnzeige()
        {
            object ausgewaehltArbeitsmappe = FreieEreignisseListe.SelectedItem;
            object ausgewaehltSchreibtisch = EreignisListeSchreibtisch.SelectedItem;
            object ausgewaehltArchiv = ArchivEreignisseListe.SelectedItem;
            object ausgewaehltPapierkorb = FreieEreignissePapierkorbListe.SelectedItem;

            FreieEreignisseListe.ItemsSource = null;
            FreieEreignisseListe.ItemsSource = freieEreignisse;

            EreignisListeSchreibtisch.ItemsSource = null;
            EreignisListeSchreibtisch.ItemsSource = freieEreignisse;

            ArchivEreignisseListe.ItemsSource = null;
            ArchivEreignisseListe.ItemsSource = freieEreignisseArchiv;

            FreieEreignissePapierkorbListe.ItemsSource = null;
            FreieEreignissePapierkorbListe.ItemsSource = freieEreignissePapierkorb;

            if (ausgewaehltArbeitsmappe != null && freieEreignisse.Contains(ausgewaehltArbeitsmappe))
            {
                FreieEreignisseListe.SelectedItem = ausgewaehltArbeitsmappe;
            }

            if (ausgewaehltSchreibtisch != null && freieEreignisse.Contains(ausgewaehltSchreibtisch))
            {
                EreignisListeSchreibtisch.SelectedItem = ausgewaehltSchreibtisch;
            }

            if (ausgewaehltArchiv != null && freieEreignisseArchiv.Contains(ausgewaehltArchiv))
            {
                ArchivEreignisseListe.SelectedItem = ausgewaehltArchiv;
            }

            if (ausgewaehltPapierkorb != null && freieEreignissePapierkorb.Contains(ausgewaehltPapierkorb))
            {
                FreieEreignissePapierkorbListe.SelectedItem = ausgewaehltPapierkorb;
            }
        }

        private void FreieEreignisseListe_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            bool istAusgewaehlt = FreieEreignisseListe.SelectedItem != null;

            FreiesEreignisErinnerungenAnsehenButton.IsEnabled = istAusgewaehlt;
            FreiesEreignisArchivierenButton.IsEnabled = istAusgewaehlt;
            FreiesEreignisInPapierkorbButton.IsEnabled = istAusgewaehlt;
        }

        private void FreiesEreignisInPapierkorb_Click(object sender, RoutedEventArgs e)
        {
            List<Ereignis> ausgewaehlteEreignisse = FreieEreignisseListe.SelectedItems.Cast<Ereignis>().ToList();

            if (ausgewaehlteEreignisse.Count == 0)
            {
                James.Hinweis(James.BitteFreieEreignisseAuswaehlen);
                return;
            }

            string frage = ausgewaehlteEreignisse.Count == 1
                ? James.FrageInPapierkorbEinzeln(ausgewaehlteEreignisse[0].Titel)
                : James.FrageInPapierkorbMehrere(ausgewaehlteEreignisse.Count);

            bool ergebnis = James.FrageJaNein(frage, James.TitelEntscheidung, MessageBoxImage.Warning);

            if (!ergebnis)
            {
                return;
            }

            foreach (Ereignis ereignis in ausgewaehlteEreignisse)
            {
                freieEreignisse.Remove(ereignis);
                freieEreignissePapierkorb.Add(ereignis);
            }

            SpeichereDaten();
            AktualisiereFreieEreignisseAnzeige();

            if (ausgewaehlteEreignisse.Count == 1)
            {
                ArbeitsmappeStatusText.Text = James.InPapierkorbGelegtEinzeln(ausgewaehlteEreignisse[0].Titel);
            }
            else
            {
                ArbeitsmappeStatusText.Text = James.InPapierkorbGelegtMehrere(ausgewaehlteEreignisse.Count);
            }
        }

        private void FreiesEreignisErinnerungenAnsehen_Click(object sender, RoutedEventArgs e)
        {
            Ereignis ereignis = FreieEreignisseListe.SelectedItem as Ereignis;

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

        private void FreiesEreignisArchivieren_Click(object sender, RoutedEventArgs e)
        {
            Ereignis ereignis = FreieEreignisseListe.SelectedItem as Ereignis;

            if (ereignis == null)
            {
                return;
            }

            freieEreignisse.Remove(ereignis);
            freieEreignisseArchiv.Add(ereignis);

            SpeichereDaten();
            AktualisiereFreieEreignisseAnzeige();

            ArbeitsmappeStatusText.Text = James.FreiesEreignisArchiviert(ereignis.Titel);
        }

        private void ArchivEreignisseListe_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ArchivEreignisAktionPanel.Visibility = Visibility.Collapsed;
        }

        private void ArchivEreignisAktion_Click(object sender, RoutedEventArgs e)
        {
            if (ArchivEreignisseListe.SelectedItem == null)
            {
                James.Hinweis(James.BitteFreieEreignisseAuswaehlen);
                return;
            }

            ArchivEreignisAktionPanel.Visibility = ArchivEreignisAktionPanel.Visibility == Visibility.Visible
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        private void ArchivEreignisErinnerungenAnsehen_Click(object sender, RoutedEventArgs e)
        {
            Ereignis ereignis = ArchivEreignisseListe.SelectedItem as Ereignis;

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

        private void ArchivEreignisWiederherstellen_Click(object sender, RoutedEventArgs e)
        {
            Ereignis ereignis = ArchivEreignisseListe.SelectedItem as Ereignis;

            if (ereignis == null)
            {
                return;
            }

            freieEreignisseArchiv.Remove(ereignis);
            freieEreignisse.Add(ereignis);

            SpeichereDaten();
            AktualisiereFreieEreignisseAnzeige();
        }

        private void ArchivEreignisInPapierkorb_Click(object sender, RoutedEventArgs e)
        {
            List<Ereignis> ausgewaehlteEreignisse = ArchivEreignisseListe.SelectedItems.Cast<Ereignis>().ToList();

            if (ausgewaehlteEreignisse.Count == 0)
            {
                James.Hinweis(James.BitteFreieEreignisseAuswaehlen);
                return;
            }

            string frage = ausgewaehlteEreignisse.Count == 1
                ? James.FrageInPapierkorbEinzeln(ausgewaehlteEreignisse[0].Titel)
                : James.FrageInPapierkorbMehrere(ausgewaehlteEreignisse.Count);

            bool ergebnis = James.FrageJaNein(frage, James.TitelEntscheidung, MessageBoxImage.Warning);

            if (!ergebnis)
            {
                return;
            }

            foreach (Ereignis ereignis in ausgewaehlteEreignisse)
            {
                freieEreignisseArchiv.Remove(ereignis);
                freieEreignissePapierkorb.Add(ereignis);
            }

            SpeichereDaten();
            AktualisiereFreieEreignisseAnzeige();
        }

        private void FreieEreignissePapierkorbListe_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            bool istAusgewaehlt = FreieEreignissePapierkorbListe.SelectedItem != null;

            FreiesEreignisWiederherstellenButton.IsEnabled = istAusgewaehlt;
            FreiesEreignisEndgueltigLoeschenButton.IsEnabled = istAusgewaehlt;
        }

        private void FreiesEreignisWiederherstellen_Click(object sender, RoutedEventArgs e)
        {
            List<Ereignis> ausgewaehlteEreignisse = FreieEreignissePapierkorbListe.SelectedItems.Cast<Ereignis>().ToList();

            if (ausgewaehlteEreignisse.Count == 0)
            {
                James.Hinweis(James.BitteEreignisPapierkorbAuswaehlen);
                return;
            }

            foreach (Ereignis ereignis in ausgewaehlteEreignisse)
            {
                freieEreignissePapierkorb.Remove(ereignis);
                freieEreignisse.Add(ereignis);
            }

            SpeichereDaten();
            AktualisiereFreieEreignisseAnzeige();

            if (ausgewaehlteEreignisse.Count == 1)
            {
                James.Hinweis(James.WiederhergestelltEinzeln(ausgewaehlteEreignisse[0].Titel), James.TitelWiederhergestellt);
            }
            else
            {
                James.Hinweis(James.WiederhergestelltMehrere(ausgewaehlteEreignisse.Count), James.TitelWiederhergestellt);
            }
        }

        private void FreiesEreignisEndgueltigLoeschen_Click(object sender, RoutedEventArgs e)
        {
            List<Ereignis> ausgewaehlteEreignisse = FreieEreignissePapierkorbListe.SelectedItems.Cast<Ereignis>().ToList();

            if (ausgewaehlteEreignisse.Count == 0)
            {
                James.Hinweis(James.BitteEreignisPapierkorbAuswaehlen);
                return;
            }

            string frage = ausgewaehlteEreignisse.Count == 1
                ? James.FrageEndgueltigLoeschenEinzeln(ausgewaehlteEreignisse[0].Titel)
                : James.FrageEndgueltigLoeschenMehrere(ausgewaehlteEreignisse.Count);

            bool ergebnis = James.FrageJaNein(frage, James.TitelEndgueltigeEntscheidung, MessageBoxImage.Warning);

            if (ergebnis)
            {
                foreach (Ereignis ereignis in ausgewaehlteEreignisse)
                {
                    freieEreignissePapierkorb.Remove(ereignis);
                }

                SpeichereDaten();
                AktualisiereFreieEreignisseAnzeige();
            }
        }
    }
}