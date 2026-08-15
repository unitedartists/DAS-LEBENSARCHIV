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
        // ============================================================
        // NEUE FUNKTION (Generaltest 2, Wunsch von Oma+Opa): SAMMLUNG
        // ============================================================
        // Dritte Schublade neben Person und besonderem Ereignis, analog
        // zur bewährten Ereignis-Logik aufgebaut (siehe
        // MainWindow.BesondereEreignisse.cs), aber bewusst ohne Datum/
        // Ort/Jahreszeit, da eine Sammlung rein thematisch ist.

        private static string ErinnerungsOrdnerFuerSammlung(Sammlung sammlung)
        {
            return Path.Combine(ErinnerungenOrdnerPfad, "Sammlungen", sammlung.Id.ToString());
        }

        // ============================================================
        // TÜV-REPARATUR (07.08.), FREIGEGEBENE ERWEITERUNG: PAPIERKORB-
        // KONTEXT-REGEL
        // ============================================================
        // Entfernt eine einzelne Erinnerung (per Pfad) NUR aus dieser
        // Sammlung - die physische Datei und andere Zuordnungen dieser
        // Erinnerung (Person, Ereignis, andere Sammlungen) bleiben dabei
        // unangetastet (Opas Grundsatzentscheidung vom 07.08.). War es
        // das Haupt-Foto der Sammlung, rückt das nächste aus
        // WeitereFotoDateinamen nach. Gibt zurück, ob die Erinnerung in
        // dieser Sammlung gefunden und entfernt wurde.
        private bool EntferneErinnerungAusSammlung(Sammlung sammlung, string pfad)
        {
            string dateiname = Path.GetFileName(pfad);

            if (sammlung.SammlungFotoDateiname == dateiname)
            {
                if (sammlung.WeitereFotoDateinamen != null && sammlung.WeitereFotoDateinamen.Count > 0)
                {
                    sammlung.SammlungFotoDateiname = sammlung.WeitereFotoDateinamen[0];
                    sammlung.WeitereFotoDateinamen.RemoveAt(0);
                }
                else
                {
                    sammlung.SammlungFotoDateiname = null;
                }

                sammlung.ModifiedAt = DateTime.Now;
                return true;
            }

            if (sammlung.WeitereFotoDateinamen != null && sammlung.WeitereFotoDateinamen.Remove(dateiname))
            {
                sammlung.ModifiedAt = DateTime.Now;
                return true;
            }

            return false;
        }

        // Baut den Papierkorb-Kontext-Callback für eine bestimmte Sammlung -
        // wird an ErinnerungenFenster übergeben und kennt per Closure genau
        // diese eine Sammlung, aus der heraus das Fenster geöffnet wurde.
        //
        // A/Opa-REPARATURAUFTRAG (11.08.), PROBLEM 3: Findet die alte,
        // dateiname-basierte Logik nichts (z.B. weil die Erinnerung nur
        // ueber die neue Lese-Bruecke/Zuordnung sichtbar ist), wird
        // zusaetzlich im neuen Zuordnungsmodell nachgesehen, bevor die
        // Aktion als "nichts gefunden" gilt (siehe
        // VersucheAusNeuemModellEntfernen in MainWindow.
        // ErinnerungsmodellZustand.cs) - dasselbe Muster wie beim
        // Personen-/Ereignis-Pendant in MainWindow.Erinnerungskarte.cs.
        private Func<string, bool> ErstelleEntferneAusSammlungCallback(Sammlung sammlung)
        {
            return pfad =>
            {
                bool entfernt = EntferneErinnerungAusSammlung(sammlung, pfad);

                if (entfernt)
                {
                    SpeichereDaten();
                    AktualisiereSammlungenAnzeige();
                    return true;
                }

                bool imNeuenModellEntfernt = VersucheAusNeuemModellEntfernen(ZuordnungsZielTyp.Sammlung, sammlung.Id, pfad);

                if (imNeuenModellEntfernt)
                {
                    AktualisiereSammlungenAnzeige();
                }

                return imNeuenModellEntfernt;
            };
        }

        private void SammlungenListeSchreibtisch_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Sammlung sammlung = SammlungenListeSchreibtisch.SelectedItem as Sammlung;

            aktuellBearbeiteteSammlung = sammlung;

            if (sammlung == null)
            {
                SammlungTitelSchreibtischTextBox.Clear();
                SammlungErinnerungenLinkText.Visibility = Visibility.Collapsed;
                SammlungSchreibtischArchivierenButton.Visibility = Visibility.Collapsed;
                return;
            }

            SammlungTitelSchreibtischTextBox.Text = sammlung.Titel;

            // BUGFIX (11.08., Großtest-Befund, Testpunkt G): zählte bisher
            // nur die alten Foto-Felder - jetzt dieselbe zentrale Quelle
            // wie "Erinnerungen ansehen" (inkl. Lese-Brücke zum neuen Modell).
            int anzahl = SammelErinnerungenFuerSammlung(sammlung).Count;

            SammlungErinnerungenLinkText.Text = James.PersonErinnerungenLink(anzahl);
            SammlungErinnerungenLinkText.Tag = anzahl > 0 ? sammlung : null;
            SammlungErinnerungenLinkText.Visibility = Visibility.Visible;

            SammlungSchreibtischArchivierenButton.Visibility = Visibility.Visible;
        }

        private void SammlungSchreibtischSpeichern_Click(object sender, RoutedEventArgs e)
        {
            string titel = SammlungTitelSchreibtischTextBox.Text.Trim();

            if (titel == "")
            {
                James.Hinweis(James.BitteEreignisTitelEingeben);
                return;
            }

            if (aktuellBearbeiteteSammlung != null)
            {
                aktuellBearbeiteteSammlung.Titel = titel;
                aktuellBearbeiteteSammlung.ModifiedAt = DateTime.Now;

                SpeichereDaten();

                ZeigeStatusMeldung("„" + titel + "\u201c wurde aktualisiert.");

                aktuellBearbeiteteSammlung = null;
                SammlungTitelSchreibtischTextBox.Clear();
                SammlungErinnerungenLinkText.Visibility = Visibility.Collapsed;
                SammlungSchreibtischArchivierenButton.Visibility = Visibility.Collapsed;

                if (SammlungenListeSchreibtisch.SelectedItem != null)
                {
                    SammlungenListeSchreibtisch.SelectedItem = null;
                }

                AktualisiereSammlungenAnzeige();
                SammlungTitelSchreibtischTextBox.Focus();
            }
            else
            {
                Sammlung neueSammlung = new Sammlung { Titel = titel };

                sammlungen.Add(neueSammlung);

                SpeichereDaten();
                AktualisiereSammlungenAnzeige();

                SammlungTitelSchreibtischTextBox.Clear();

                ZeigeStatusMeldung("„" + titel + "\u201c wurde angelegt.");
            }
        }

        private void SammlungSchreibtischArchivieren_Click(object sender, RoutedEventArgs e)
        {
            Sammlung sammlung = SammlungenListeSchreibtisch.SelectedItem as Sammlung;

            if (sammlung == null)
            {
                return;
            }

            sammlungen.Remove(sammlung);
            sammlungenArchiv.Add(sammlung);

            SpeichereDaten();

            aktuellBearbeiteteSammlung = null;
            SammlungTitelSchreibtischTextBox.Clear();
            SammlungErinnerungenLinkText.Visibility = Visibility.Collapsed;
            SammlungSchreibtischArchivierenButton.Visibility = Visibility.Collapsed;

            AktualisiereSammlungenAnzeige();

            ZeigeStatusMeldung("„" + sammlung.Titel + "\u201c wurde archiviert.");
        }

        private void SammlungErinnerungenLinkText_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            Sammlung sammlung = SammlungErinnerungenLinkText.Tag as Sammlung;

            if (sammlung == null)
            {
                return;
            }

            List<ErinnerungsInfo> erinnerungenListe = SammelErinnerungenFuerSammlung(sammlung);

            if (erinnerungenListe.Count == 0)
            {
                return;
            }

            ErinnerungenFenster fenster = new ErinnerungenFenster(James.ErinnerungenFensterTitelEreignis(sammlung.Titel), erinnerungenListe, LiesVisuelleMerkmale, SpeichereVisuelleMerkmale, ZaehleVorkommenVisuellesMerkmal, ErstelleEntferneAusSammlungCallback(sammlung), SendeMarkierteZurArbeitsmappe);
            fenster.Owner = this;
            fenster.Show();
        }

        private List<ErinnerungsInfo> SammelErinnerungenFuerSammlung(Sammlung sammlung)
        {
            List<ErinnerungsInfo> erinnerungenListe = new List<ErinnerungsInfo>();
            string ordner = ErinnerungsOrdnerFuerSammlung(sammlung);

            List<string> alleDateinamen = new List<string>();

            if (!string.IsNullOrEmpty(sammlung.SammlungFotoDateiname))
            {
                alleDateinamen.Add(sammlung.SammlungFotoDateiname);
            }

            if (sammlung.WeitereFotoDateinamen != null)
            {
                alleDateinamen.AddRange(sammlung.WeitereFotoDateinamen);
            }

            foreach (string dateiname in alleDateinamen)
            {
                erinnerungenListe.Add(new ErinnerungsInfo
                {
                    Pfad = Path.Combine(ordner, dateiname),
                    Titel = sammlung.Titel
                });
            }

            // A/Opa-INTEGRATIONSAUFTRAG (11.08.), Option B "Lese-Brücke".
            ErgaenzeUmNeuesModell(erinnerungenListe, ZuordnungsZielTyp.Sammlung, sammlung.Id);

            return erinnerungenListe;
        }

        private void AktualisiereSammlungenAnzeige()
        {
            object ausgewaehltSchreibtisch = SammlungenListeSchreibtisch.SelectedItem;
            object ausgewaehltArbeitsmappe = FreieSammlungenListe.SelectedItem;
            object ausgewaehltArchiv = ArchivSammlungenListe.SelectedItem;
            object ausgewaehltPapierkorb = SammlungenPapierkorbListe.SelectedItem;

            SammlungenListeSchreibtisch.ItemsSource = null;
            SammlungenListeSchreibtisch.ItemsSource = sammlungen;

            FreieSammlungenListe.ItemsSource = null;
            FreieSammlungenListe.ItemsSource = sammlungen;

            ArchivSammlungenListe.ItemsSource = null;
            ArchivSammlungenListe.ItemsSource = sammlungenArchiv;

            SammlungenPapierkorbListe.ItemsSource = null;
            SammlungenPapierkorbListe.ItemsSource = sammlungenPapierkorb;

            if (ausgewaehltSchreibtisch != null && sammlungen.Contains(ausgewaehltSchreibtisch))
            {
                SammlungenListeSchreibtisch.SelectedItem = ausgewaehltSchreibtisch;
            }

            if (ausgewaehltArbeitsmappe != null && sammlungen.Contains(ausgewaehltArbeitsmappe))
            {
                FreieSammlungenListe.SelectedItem = ausgewaehltArbeitsmappe;
            }

            if (ausgewaehltArchiv != null && sammlungenArchiv.Contains(ausgewaehltArchiv))
            {
                ArchivSammlungenListe.SelectedItem = ausgewaehltArchiv;
            }

            if (ausgewaehltPapierkorb != null && sammlungenPapierkorb.Contains(ausgewaehltPapierkorb))
            {
                SammlungenPapierkorbListe.SelectedItem = ausgewaehltPapierkorb;
            }
        }

        // ============================================================
        // NEUE FUNKTION: SAMMLUNG IN DER ARBEITSMAPPE
        // ============================================================

        private int VerbindeDateienMitSammlung(Sammlung sammlung, List<string> pfade, string zielOrdner)
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

                if (string.IsNullOrEmpty(sammlung.SammlungFotoDateiname))
                {
                    sammlung.SammlungFotoDateiname = neuerDateiname;
                }
                else
                {
                    if (sammlung.WeitereFotoDateinamen == null)
                    {
                        sammlung.WeitereFotoDateinamen = new List<string>();
                    }

                    sammlung.WeitereFotoDateinamen.Add(neuerDateiname);
                }

                arbeitsmappeBereitsZugeordnet.Add(pfad);
                verbunden++;
            }

            if (verbunden > 0)
            {
                sammlung.ModifiedAt = DateTime.Now;
            }

            return verbunden;
        }

        private void VerknuepfeArbeitsmappenDateienMitSammlung(Sammlung sammlung, List<string> pfade)
        {
            if (sammlung == null || pfade == null || pfade.Count == 0)
            {
                return;
            }

            try
            {
                int verbunden = VerbindeDateienMitSammlung(sammlung, pfade, ErinnerungsOrdnerFuerSammlung(sammlung));

                if (verbunden > 0)
                {
                    SpeichereDaten();
                    SpeichereArbeitsmappeZugeordnet();
                    AktualisiereSammlungenAnzeige();
                }

                if (verbunden == 0)
                {
                    James.Hinweis(James.ArbeitsmappeNurBilder);
                }
                else if (verbunden == 1)
                {
                    ArbeitsmappeStatusText.Text = "Verbunden mit \u201e" + sammlung.Titel + "\u201c.";
                }
                else
                {
                    ArbeitsmappeStatusText.Text = verbunden + " Erinnerungen verbunden mit \u201e" + sammlung.Titel + "\u201c.";
                }
            }
            catch (Exception ex)
            {
                James.Problem(James.FehlerBeimSpeichernFoto(ex.Message));
            }
        }

        private void ArbeitsmappeNeueSammlungAnlegen_Click(object sender, RoutedEventArgs e)
        {
            if (arbeitsmappeAusgewaehlt.Count == 0)
            {
                return;
            }

            SammlungTitelTextBox.Clear();

            VersteckeAlleArbeitsmappenPanels();
            ArbeitsmappeNeueSammlungPanel.Visibility = Visibility.Visible;
            SammlungTitelTextBox.Focus();
        }

        private void SammlungSpeichernUndZuordnen_Click(object sender, RoutedEventArgs e)
        {
            string titel = SammlungTitelTextBox.Text.Trim();

            if (titel == "")
            {
                James.Hinweis(James.BitteEreignisTitelEingeben);
                return;
            }

            Sammlung bestehendeSammlung = sammlungen
                .Concat(sammlungenArchiv)
                .FirstOrDefault(s => string.Equals((s.Titel ?? "").Trim(), titel, StringComparison.OrdinalIgnoreCase));

            Sammlung zielSammlung;

            if (bestehendeSammlung != null && James.FrageJaNein(James.FrageEreignisBereitsVorhanden(titel), James.TitelEntscheidung))
            {
                zielSammlung = bestehendeSammlung;
            }
            else
            {
                zielSammlung = new Sammlung { Titel = titel };
                sammlungen.Add(zielSammlung);
            }

            SpeichereDaten();
            AktualisiereSammlungenAnzeige();

            List<string> pfade = arbeitsmappeAusgewaehlt.ToList();
            VerknuepfeArbeitsmappenDateienMitSammlung(zielSammlung, pfade);

            ArbeitsmappeNeueSammlungPanel.Visibility = Visibility.Collapsed;
            AktualisiereArbeitsmappe();
        }

        private void ArbeitsmappeSammlungZuordnen_Click(object sender, RoutedEventArgs e)
        {
            if (arbeitsmappeAusgewaehlt.Count == 0)
            {
                return;
            }

            if (sammlungen.Count == 0)
            {
                James.Hinweis(James.BitteErstFreiesEreignisAnlegen);
                return;
            }

            AktualisiereSammlungenAnzeige();

            VersteckeAlleArbeitsmappenPanels();
            ArbeitsmappeSammlungAuswahlPanel.Visibility = Visibility.Visible;
        }

        private void ArbeitsmappeSammlungAbbrechen_Click(object sender, RoutedEventArgs e)
        {
            ArbeitsmappeSammlungAuswahlPanel.Visibility = Visibility.Collapsed;
        }

        private void SammlungBestaetigen_Click(object sender, RoutedEventArgs e)
        {
            List<Sammlung> ausgewaehlteSammlungen = FreieSammlungenListe.SelectedItems.Cast<Sammlung>().ToList();

            if (ausgewaehlteSammlungen.Count == 0)
            {
                James.Hinweis(James.BitteEreignisAuswaehlen);
                return;
            }

            List<string> pfade = arbeitsmappeAusgewaehlt.ToList();

            foreach (Sammlung sammlung in ausgewaehlteSammlungen)
            {
                List<string> pfadeKopie = new List<string>(pfade);
                VerknuepfeArbeitsmappenDateienMitSammlung(sammlung, pfadeKopie);
            }

            ArbeitsmappeStatusText.Text = "Zugeordnet an " + ausgewaehlteSammlungen.Count + " Sammlung(en).";

            AktualisiereArbeitsmappe();
        }

        private void FreieSammlungenListe_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            bool istAusgewaehlt = FreieSammlungenListe.SelectedItem != null;

            SammlungZuordnenBestaetigenButton.IsEnabled = istAusgewaehlt;
            SammlungErinnerungenAnsehenButton.IsEnabled = istAusgewaehlt;
            SammlungArchivierenButton.IsEnabled = istAusgewaehlt;
            SammlungInPapierkorbButton.IsEnabled = istAusgewaehlt;
        }

        private void SammlungErinnerungenAnsehen_Click(object sender, RoutedEventArgs e)
        {
            Sammlung sammlung = FreieSammlungenListe.SelectedItem as Sammlung;

            if (sammlung == null)
            {
                return;
            }

            List<ErinnerungsInfo> erinnerungenListe = SammelErinnerungenFuerSammlung(sammlung);

            if (erinnerungenListe.Count == 0)
            {
                return;
            }

            ErinnerungenFenster fenster = new ErinnerungenFenster(James.ErinnerungenFensterTitelEreignis(sammlung.Titel), erinnerungenListe, LiesVisuelleMerkmale, SpeichereVisuelleMerkmale, ZaehleVorkommenVisuellesMerkmal, ErstelleEntferneAusSammlungCallback(sammlung), SendeMarkierteZurArbeitsmappe);
            fenster.Owner = this;
            fenster.Show();
        }

        private void SammlungArchivieren_Click(object sender, RoutedEventArgs e)
        {
            List<Sammlung> ausgewaehlteSammlungen = FreieSammlungenListe.SelectedItems.Cast<Sammlung>().ToList();

            if (ausgewaehlteSammlungen.Count == 0)
            {
                return;
            }

            foreach (Sammlung sammlung in ausgewaehlteSammlungen)
            {
                sammlungen.Remove(sammlung);
                sammlungenArchiv.Add(sammlung);
            }

            SpeichereDaten();
            AktualisiereSammlungenAnzeige();

            ArbeitsmappeStatusText.Text = ausgewaehlteSammlungen.Count == 1
                ? "\u201e" + ausgewaehlteSammlungen[0].Titel + "\u201c wurde archiviert."
                : ausgewaehlteSammlungen.Count + " Sammlungen archiviert.";
        }

        // BUGFIX (TÜV-Reparatur 07.08., Priorität 1): FreieSammlungenListe
        // hat SelectionMode="Extended" - Bestätigung nennt jetzt
        // namentlich, welche Sammlungen betroffen sind.
        private void SammlungInPapierkorb_Click(object sender, RoutedEventArgs e)
        {
            List<Sammlung> ausgewaehlteSammlungen = FreieSammlungenListe.SelectedItems.Cast<Sammlung>().ToList();

            if (ausgewaehlteSammlungen.Count == 0)
            {
                return;
            }

            string frage = ausgewaehlteSammlungen.Count == 1
                ? James.FrageInPapierkorbEinzeln(ausgewaehlteSammlungen[0].Titel)
                : James.FrageInPapierkorbMehrere(ausgewaehlteSammlungen.Select(x => x.Titel).ToList());

            bool ergebnis = James.FrageJaNein(frage, James.TitelEntscheidung, MessageBoxImage.Warning);

            if (!ergebnis)
            {
                return;
            }

            foreach (Sammlung sammlung in ausgewaehlteSammlungen)
            {
                sammlungen.Remove(sammlung);
                sammlungenPapierkorb.Add(sammlung);
            }

            SpeichereDaten();
            AktualisiereSammlungenAnzeige();

            ArbeitsmappeStatusText.Text = ausgewaehlteSammlungen.Count == 1
                ? "\u201e" + ausgewaehlteSammlungen[0].Titel + "\u201c liegt jetzt im Papierkorb."
                : ausgewaehlteSammlungen.Count + " Sammlungen liegen jetzt im Papierkorb.";
        }

        // ============================================================
        // NEUE FUNKTION: SAMMLUNG IM ARCHIV
        // ============================================================

        // TÜV-Reparatur Teil B (08.08.): Teil der vereinheitlichten
        // Archiv-Aktionsleiste (siehe MainWindow.Personen.cs) - eine
        // Auswahl hier leert automatisch die Auswahl in den beiden
        // anderen Archiv-Listen, damit immer eindeutig ist, worauf eine
        // Aktion wirkt.
        private void ArchivSammlungenListe_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ArchivSammlungenListe.SelectedItems.Count > 0)
            {
                ArchivListe.SelectedItem = null;
                ArchivEreignisseListe.SelectedItem = null;
            }

            AktualisiereArchivAuswahl();
        }

        private void ArchivSammlungErinnerungenAnsehen_Click(object sender, RoutedEventArgs e)
        {
            Sammlung sammlung = ArchivSammlungenListe.SelectedItem as Sammlung;

            if (sammlung == null)
            {
                return;
            }

            List<ErinnerungsInfo> erinnerungenListe = SammelErinnerungenFuerSammlung(sammlung);

            if (erinnerungenListe.Count == 0)
            {
                return;
            }

            ErinnerungenFenster fenster = new ErinnerungenFenster(James.ErinnerungenFensterTitelEreignis(sammlung.Titel), erinnerungenListe, LiesVisuelleMerkmale, SpeichereVisuelleMerkmale, ZaehleVorkommenVisuellesMerkmal, ErstelleEntferneAusSammlungCallback(sammlung), SendeMarkierteZurArbeitsmappe);
            fenster.Owner = this;
            fenster.Show();
        }

        // A/Opa-INTEGRATIONSAUFTRAG (11.08.), Punkt 3+4: öffnet jetzt den
        // Arbeitsmotor mit dieser Sammlung vorausgewählt (Weg C) - keine
        // physische Kopie mehr, ersetzt den bisherigen Redirect-Hinweis.
        private void ArchivSammlungZuordnen_Click(object sender, RoutedEventArgs e)
        {
            Sammlung sammlung = ArchivSammlungenListe.SelectedItem as Sammlung;

            if (sammlung == null)
            {
                return;
            }

            OeffneArbeitsmotorFuerZiel(ZuordnungsZielTyp.Sammlung, sammlung.Id);
        }

        // TÜV-Reparatur Teil B (08.08.): jetzt mehrfachauswahlfähig, analog
        // zu ArchivSammlungInPapierkorb_Click.
        private void ArchivSammlungWiederherstellen_Click(object sender, RoutedEventArgs e)
        {
            List<Sammlung> ausgewaehlteSammlungen = ArchivSammlungenListe.SelectedItems.Cast<Sammlung>().ToList();

            if (ausgewaehlteSammlungen.Count == 0)
            {
                return;
            }

            foreach (Sammlung sammlung in ausgewaehlteSammlungen)
            {
                sammlungenArchiv.Remove(sammlung);
                sammlungen.Add(sammlung);
            }

            SpeichereDaten();
            AktualisiereSammlungenAnzeige();

            ZeigeStatusMeldung(ausgewaehlteSammlungen.Count == 1
                ? "\u201e" + ausgewaehlteSammlungen[0].Titel + "\u201c ist zurück auf dem Schreibtisch."
                : ausgewaehlteSammlungen.Count + " Sammlungen sind zurück auf dem Schreibtisch.");
        }

        // BUGFIX (TÜV-Reparatur 07.08., Priorität 1): ArchivSammlungenListe
        // hat SelectionMode="Extended" - dieselbe Risikostelle wie bei
        // ArchivEreignisseListe. Bestätigung nennt jetzt namentlich, welche
        // Sammlungen betroffen sind.
        private void ArchivSammlungInPapierkorb_Click(object sender, RoutedEventArgs e)
        {
            List<Sammlung> ausgewaehlteSammlungen = ArchivSammlungenListe.SelectedItems.Cast<Sammlung>().ToList();

            if (ausgewaehlteSammlungen.Count == 0)
            {
                James.Hinweis(James.BitteFreieEreignisseAuswaehlen);
                return;
            }

            string frage = ausgewaehlteSammlungen.Count == 1
                ? James.FrageInPapierkorbEinzeln(ausgewaehlteSammlungen[0].Titel)
                : James.FrageInPapierkorbMehrere(ausgewaehlteSammlungen.Select(x => x.Titel).ToList());

            bool ergebnis = James.FrageJaNein(frage, James.TitelEntscheidung, MessageBoxImage.Warning);

            if (!ergebnis)
            {
                return;
            }

            foreach (Sammlung sammlung in ausgewaehlteSammlungen)
            {
                sammlungenArchiv.Remove(sammlung);
                sammlungenPapierkorb.Add(sammlung);
            }

            SpeichereDaten();
            AktualisiereSammlungenAnzeige();
        }

        // ============================================================
        // NEUE FUNKTION: SAMMLUNG IM PAPIERKORB
        // ============================================================

        private void SammlungenPapierkorbListe_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            bool istAusgewaehlt = SammlungenPapierkorbListe.SelectedItem != null;

            SammlungWiederherstellenButton.IsEnabled = istAusgewaehlt;
            SammlungEndgueltigLoeschenButton.IsEnabled = istAusgewaehlt;
        }

        private void SammlungWiederherstellen_Click(object sender, RoutedEventArgs e)
        {
            List<Sammlung> ausgewaehlteSammlungen = SammlungenPapierkorbListe.SelectedItems.Cast<Sammlung>().ToList();

            if (ausgewaehlteSammlungen.Count == 0)
            {
                James.Hinweis(James.BitteEreignisPapierkorbAuswaehlen);
                return;
            }

            foreach (Sammlung sammlung in ausgewaehlteSammlungen)
            {
                sammlungenPapierkorb.Remove(sammlung);
                sammlungen.Add(sammlung);
            }

            SpeichereDaten();
            AktualisiereSammlungenAnzeige();

            if (ausgewaehlteSammlungen.Count == 1)
            {
                James.Hinweis(James.WiederhergestelltEinzeln(ausgewaehlteSammlungen[0].Titel), James.TitelWiederhergestellt);
            }
            else
            {
                James.Hinweis(James.WiederhergestelltMehrere(ausgewaehlteSammlungen.Count), James.TitelWiederhergestellt);
            }
        }

        // BUGFIX (TÜV-Reparatur 07.08., Priorität 1): SammlungenPapierkorbListe
        // hat SelectionMode="Extended" - gerade beim endgültigen,
        // unwiderruflichen Löschen besonders wichtig.
        private void SammlungEndgueltigLoeschen_Click(object sender, RoutedEventArgs e)
        {
            List<Sammlung> ausgewaehlteSammlungen = SammlungenPapierkorbListe.SelectedItems.Cast<Sammlung>().ToList();

            if (ausgewaehlteSammlungen.Count == 0)
            {
                James.Hinweis(James.BitteEreignisPapierkorbAuswaehlen);
                return;
            }

            string frage = ausgewaehlteSammlungen.Count == 1
                ? James.FrageEndgueltigLoeschenEinzeln(ausgewaehlteSammlungen[0].Titel)
                : James.FrageEndgueltigLoeschenMehrere(ausgewaehlteSammlungen.Select(x => x.Titel).ToList());

            bool ergebnis = James.FrageJaNein(frage, James.TitelEndgueltigeEntscheidung, MessageBoxImage.Warning);

            if (ergebnis)
            {
                foreach (Sammlung sammlung in ausgewaehlteSammlungen)
                {
                    sammlungenPapierkorb.Remove(sammlung);
                }

                SpeichereDaten();
                AktualisiereSammlungenAnzeige();
            }
        }
    }
}
