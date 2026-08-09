using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DAS_LEBENSARCHIV
{
    // ============================================================
    // SANIERUNG BAUPHASE 4 (08.08.) + AM-SCHREIBTISCH (09.08., beide Runden)
    // ============================================================
    // A's Vorgabe: das neue Erinnerungsmodell wird zunächst LESEND an
    // die Arbeitsmappe angeschlossen - keine alte Struktur wird
    // entfernt oder ersetzt. personen.json wird an keiner Stelle in
    // dieser Datei beschrieben; geschrieben wird ausschließlich die
    // bereits in Bauphase 3 angelegte, separate Datei
    // erinnerungsmodell.json (beim Bestätigen einer neuen Zuordnung).
    // Keine physische Foto-Datei wird kopiert oder verschoben.
    //
    // Drei gleichberechtigte, mischbare Wege in dieselbe Arbeitsauswahl:
    //   WEG A - aus Archiv/Schreibtisch markieren -> "Zuordnen"
    //           (SendeMarkierteZurArbeitsmappe).
    //   WEG B - direkt in der AM suchen und hinzufügen
    //           (AmDirekteAuswahlHinzufuegen_Click).
    //   WEG C - von einem Ziel (Person/Ereignis/Sammlung) aus die dort
    //           bereits (im NEUEN Modell) zugeordneten Erinnerungen
    //           markieren und zur AM schicken (SendeErinnerungsIdsZur
    //           Arbeitsmappe, aufgerufen aus ErinnerungsmodellBetrachterFenster).
    // Alle drei befüllen dieselbe Liste, mit Dopplungsschutz über
    // ErinnerungId. Die Herkunft jeder Erinnerung bleibt sichtbar.
    public partial class MainWindow
    {
        private List<Erinnerung> erinnerungsmodellErinnerungen;
        private List<Zuordnung> erinnerungsmodellZuordnungen;
        private bool erinnerungsmodellGeladen;

        // Rein session-interne Hilfsklasse (NICHT Teil des abgenommenen
        // Kernmodells in ErinnerungsModell.cs) - merkt sich zusätzlich zur
        // Erinnerungs-Id, auf welchem der drei Wege sie in die
        // Arbeitsauswahl kam.
        private class ArbeitsauswahlEintrag
        {
            public Guid ErinnerungId;
            public string Herkunft;
        }

        private readonly List<ArbeitsauswahlEintrag> amArbeitsauswahl = new List<ArbeitsauswahlEintrag>();

        private string ErinnerungsmodellDateiPfad => Path.Combine(OrdnerPfad, "erinnerungsmodell.json");

        private void LadeErinnerungsmodellFallsNoetig()
        {
            if (erinnerungsmodellGeladen)
            {
                return;
            }

            erinnerungsmodellErinnerungen = new List<Erinnerung>();
            erinnerungsmodellZuordnungen = new List<Zuordnung>();

            try
            {
                if (File.Exists(ErinnerungsmodellDateiPfad))
                {
                    string json = File.ReadAllText(ErinnerungsmodellDateiPfad);
                    ArchivErinnerungsDaten daten = JsonSerializer.Deserialize<ArchivErinnerungsDaten>(json);

                    if (daten != null)
                    {
                        if (daten.Erinnerungen != null)
                        {
                            erinnerungsmodellErinnerungen = daten.Erinnerungen;
                        }

                        if (daten.Zuordnungen != null)
                        {
                            erinnerungsmodellZuordnungen = daten.Zuordnungen;
                        }
                    }
                }
            }
            catch
            {
                // Noch keine Migration durchgeführt oder Datei nicht lesbar -
                // dann bleibt die Arbeitsauswahl-Funktion einfach leer nutzbar.
            }

            erinnerungsmodellGeladen = true;
        }

        private void FuegeZurArbeitsauswahlHinzu(Guid erinnerungId, string herkunft)
        {
            if (amArbeitsauswahl.Any(a => a.ErinnerungId == erinnerungId))
            {
                return;
            }

            amArbeitsauswahl.Add(new ArbeitsauswahlEintrag { ErinnerungId = erinnerungId, Herkunft = herkunft });
        }

        // ============================================================
        // GEMEINSAME BILDVORSCHAU (09.08., Punkt 2 des AM-Auftrags)
        // ============================================================
        // Baut eine kleine Kachel (Miniaturbild + Text) nach demselben
        // bewährten Muster wie an anderen Stellen im Projekt (z.B.
        // ErinnerungenFenster.RenderMiniaturen) - keine neue Bildtechnik,
        // keine physische Kopie, greift nur lesend auf einen vorhandenen
        // Fundort zu.
        private static Border ErstelleErinnerungsKachel(Erinnerung erinnerung, string beschriftung)
        {
            string pfad = erinnerung.Fundorte != null && erinnerung.Fundorte.Count > 0 ? erinnerung.Fundorte[0].Pfad : null;

            Border rahmen = new Border
            {
                Width = 190,
                Height = 70,
                Margin = new Thickness(4),
                BorderBrush = Brushes.LightGray,
                BorderThickness = new Thickness(1),
                Background = Brushes.WhiteSmoke,
                Tag = erinnerung
            };

            StackPanel inhalt = new StackPanel { Orientation = Orientation.Horizontal };

            Border bildRahmen = new Border
            {
                Width = 60,
                Height = 60,
                Margin = new Thickness(4),
                Background = Brushes.White
            };

            if (!string.IsNullOrEmpty(pfad) && File.Exists(pfad))
            {
                try
                {
                    BitmapImage bild = new BitmapImage();
                    bild.BeginInit();
                    bild.CacheOption = BitmapCacheOption.OnLoad;
                    bild.DecodePixelWidth = 100;
                    bild.UriSource = new Uri(pfad);
                    bild.EndInit();

                    bildRahmen.Child = new Image { Source = bild, Stretch = Stretch.Uniform };
                }
                catch
                {
                    bildRahmen.Child = new TextBlock { Text = "🖼️", FontSize = 24, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                }
            }
            else
            {
                bildRahmen.Child = new TextBlock { Text = "⚠️", FontSize = 20, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, ToolTip = "Fundort nicht gefunden" };
            }

            inhalt.Children.Add(bildRahmen);

            inhalt.Children.Add(new TextBlock
            {
                Text = beschriftung,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 110,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 11
            });

            rahmen.Child = inhalt;

            return rahmen;
        }

        // ============================================================
        // WEG A (08.08., unverändert erhalten): aus Archiv/Schreibtisch
        // ============================================================
        private void SendeMarkierteZurArbeitsmappe(List<string> pfade)
        {
            LadeErinnerungsmodellFallsNoetig();

            int gefunden = 0;
            int nichtGefunden = 0;

            foreach (string pfad in pfade)
            {
                Erinnerung erinnerung = erinnerungsmodellErinnerungen.FirstOrDefault(er =>
                    er.Fundorte != null && er.Fundorte.Any(f => string.Equals(f.Pfad, pfad, StringComparison.OrdinalIgnoreCase)));

                if (erinnerung == null)
                {
                    nichtGefunden++;
                    continue;
                }

                FuegeZurArbeitsauswahlHinzu(erinnerung.Id, "Archiv");
                gefunden++;
            }

            HauptTabControl.SelectedIndex = ArbeitsmappeTabIndex;

            AktualisiereAmArbeitsauswahlAnzeige();

            if (nichtGefunden > 0)
            {
                James.Hinweis(gefunden + " Erinnerung(en) wurden in die Arbeitsauswahl übernommen. " + nichtGefunden +
                    " Erinnerung(en) sind noch nicht Teil des neuen Modells (noch nicht migriert) und konnten deshalb nicht übernommen werden.");
            }
        }

        // ============================================================
        // WEG B (09.08.): direkt in der AM auswählen
        // ============================================================
        private void AmDirekteSucheTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            AktualisiereAmDirekteAuswahlListe();
        }

        private void AktualisiereAmDirekteAuswahlListe()
        {
            LadeErinnerungsmodellFallsNoetig();

            if (AmDirekteSucheTextBox == null || AmDirekteAuswahlListe == null)
            {
                return;
            }

            string suchtext = (AmDirekteSucheTextBox.Text ?? "").Trim().ToLowerInvariant();

            HashSet<Guid> bereitsAusgewaehlt = amArbeitsauswahl.Select(a => a.ErinnerungId).ToHashSet();

            List<Erinnerung> treffer = erinnerungsmodellErinnerungen
                .Where(er => !bereitsAusgewaehlt.Contains(er.Id))
                .Where(er => suchtext == "" || (er.Fundorte != null && er.Fundorte.Any(f => f.Pfad.ToLowerInvariant().Contains(suchtext))))
                .ToList();

            AmDirekteAuswahlListe.Items.Clear();

            foreach (Erinnerung erinnerung in treffer)
            {
                string dateiname = erinnerung.Fundorte.Count > 0 ? Path.GetFileName(erinnerung.Fundorte[0].Pfad) : erinnerung.Id.ToString();
                AmDirekteAuswahlListe.Items.Add(ErstelleErinnerungsKachel(erinnerung, dateiname));
            }
        }

        private void AmDirekteAuswahlListe_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            AmDirekteAuswahlHinzufuegenButton.IsEnabled = AmDirekteAuswahlListe.SelectedItems.Count > 0;
        }

        private void AmDirekteAuswahlHinzufuegen_Click(object sender, RoutedEventArgs e)
        {
            if (AmDirekteAuswahlListe.SelectedItems.Count == 0)
            {
                return;
            }

            foreach (Border kachel in AmDirekteAuswahlListe.SelectedItems.Cast<Border>().ToList())
            {
                if (kachel.Tag is Erinnerung erinnerung)
                {
                    FuegeZurArbeitsauswahlHinzu(erinnerung.Id, "AM");
                }
            }

            AktualisiereAmArbeitsauswahlAnzeige();
        }

        // ============================================================
        // WEG C (09.08.): von einem Ziel aus zur AM
        // ============================================================
        // Wird als Action<List<Guid>> an ErinnerungsmodellBetrachterFenster
        // übergeben - übernimmt die dort markierten, bereits im neuen
        // Modell zugeordneten Erinnerungen direkt per Id, ohne dass das
        // Betrachter-Fenster die Arbeitsauswahl-Interna kennen muss.
        private void SendeErinnerungsIdsZurArbeitsmappe(List<Guid> erinnerungIds, string herkunft)
        {
            foreach (Guid id in erinnerungIds)
            {
                FuegeZurArbeitsauswahlHinzu(id, herkunft);
            }

            HauptTabControl.SelectedIndex = ArbeitsmappeTabIndex;
            AktualisiereAmArbeitsauswahlAnzeige();
        }

        // ============================================================
        // BETRACHTER ÖFFNEN
        // ============================================================
        private void ErinnerungsmodellBetrachterOeffnen_Click(object sender, RoutedEventArgs e)
        {
            LadeErinnerungsmodellFallsNoetig();

            ErinnerungsmodellBetrachterFenster fenster = new ErinnerungsmodellBetrachterFenster(
                erinnerungsmodellErinnerungen,
                erinnerungsmodellZuordnungen,
                allePersonen,
                ArchivListe.Items.OfType<Person>().ToList(),
                freieEreignisse,
                freieEreignisseArchiv,
                sammlungen,
                sammlungenArchiv,
                ids => SendeErinnerungsIdsZurArbeitsmappe(ids, "Ziel"));

            fenster.Owner = this;
            fenster.ShowDialog();
        }

        // ============================================================
        // GEMEINSAME ANZEIGE + ZUORDNUNG (alle drei Wege zusammen)
        // ============================================================
        private void AktualisiereAmArbeitsauswahlAnzeige()
        {
            LadeErinnerungsmodellFallsNoetig();

            List<(ArbeitsauswahlEintrag Eintrag, Erinnerung Erinnerung)> ausgewaehlt = amArbeitsauswahl
                .Select(a => (a, erinnerungsmodellErinnerungen.FirstOrDefault(er => er.Id == a.ErinnerungId)))
                .Where(paar => paar.Item2 != null)
                .ToList();

            AmArbeitsauswahlText.Text = ausgewaehlt.Count == 0
                ? "Noch keine Erinnerungen in der Arbeitsauswahl."
                : ausgewaehlt.Count + " Erinnerung(en) zur Neuzuordnung markiert:";

            AmArbeitsauswahlListe.Items.Clear();

            foreach ((ArbeitsauswahlEintrag Eintrag, Erinnerung Erinnerung) paar in ausgewaehlt)
            {
                string dateiname = paar.Erinnerung.Fundorte.Count > 0 ? Path.GetFileName(paar.Erinnerung.Fundorte[0].Pfad) : paar.Erinnerung.Id.ToString();
                AmArbeitsauswahlListe.Items.Add(ErstelleErinnerungsKachel(paar.Erinnerung, "[" + paar.Eintrag.Herkunft + "]\n" + dateiname));
            }

            bool istAusgewaehlt = ausgewaehlt.Count > 0;

            AmArbeitsauswahlLeerenButton.IsEnabled = istAusgewaehlt;

            AktualisiereAmDirekteAuswahlListe();
            AktualisiereAmZielAuswahl();
        }

        private void AmZielTypComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            AktualisiereAmZielAuswahl();
        }

        private void AktualisiereAmZielAuswahl()
        {
            if (AmZielTypComboBox == null || AmZielObjektComboBox == null)
            {
                return;
            }

            ComboBoxItem ausgewaehlterTyp = AmZielTypComboBox.SelectedItem as ComboBoxItem;
            string typText = ausgewaehlterTyp != null ? ausgewaehlterTyp.Content.ToString() : "Person";

            AmZielObjektComboBox.ItemsSource = null;

            if (typText == "Ereignis")
            {
                AmZielObjektComboBox.ItemsSource = freieEreignisse
                    .Concat(freieEreignisseArchiv)
                    .ToList();
            }
            else if (typText == "Sammlung")
            {
                AmZielObjektComboBox.ItemsSource = sammlungen
                    .Concat(sammlungenArchiv)
                    .ToList();
            }
            else
            {
                AmZielObjektComboBox.ItemsSource = allePersonen
                    .Concat(ArchivListe.Items.OfType<Person>())
                    .ToList();
            }

            bool arbeitsauswahlVorhanden = amArbeitsauswahl.Count > 0;
            bool zielVorhanden = AmZielObjektComboBox.Items.Count > 0;

            AmZuordnenBestaetigenButton.IsEnabled = arbeitsauswahlVorhanden && zielVorhanden;

            if (zielVorhanden)
            {
                AmZielObjektComboBox.SelectedIndex = 0;
            }
        }

        private void AmZuordnenBestaetigen_Click(object sender, RoutedEventArgs e)
        {
            if (amArbeitsauswahl.Count == 0)
            {
                return;
            }

            ComboBoxItem ausgewaehlterTyp = AmZielTypComboBox.SelectedItem as ComboBoxItem;
            string typText = ausgewaehlterTyp != null ? ausgewaehlterTyp.Content.ToString() : "Person";

            ZuordnungsZielTyp zielTyp;
            Guid zielId;
            string zielBezeichnung;

            if (typText == "Ereignis")
            {
                Ereignis ereignis = AmZielObjektComboBox.SelectedItem as Ereignis;
                if (ereignis == null) { return; }
                zielTyp = ZuordnungsZielTyp.Ereignis;
                zielId = ereignis.Id;
                zielBezeichnung = ereignis.Titel;
            }
            else if (typText == "Sammlung")
            {
                Sammlung sammlung = AmZielObjektComboBox.SelectedItem as Sammlung;
                if (sammlung == null) { return; }
                zielTyp = ZuordnungsZielTyp.Sammlung;
                zielId = sammlung.Id;
                zielBezeichnung = sammlung.Titel;
            }
            else
            {
                Person person = AmZielObjektComboBox.SelectedItem as Person;
                if (person == null) { return; }
                zielTyp = ZuordnungsZielTyp.Person;
                zielId = person.Id;
                zielBezeichnung = person.ToString();
            }

            bool ergebnis = James.FrageJaNein(
                amArbeitsauswahl.Count + " Erinnerung(en) neu zuordnen zu \"" + zielBezeichnung + "\"?\n\n" +
                "Bisherige Zuordnungen dieser Erinnerungen bleiben dabei zusätzlich bestehen (Testphase).",
                James.TitelEntscheidung);

            if (!ergebnis)
            {
                return;
            }

            foreach (ArbeitsauswahlEintrag eintrag in amArbeitsauswahl)
            {
                erinnerungsmodellZuordnungen.Add(new Zuordnung
                {
                    ErinnerungId = eintrag.ErinnerungId,
                    ZielTyp = zielTyp,
                    ZielId = zielId,
                    ZielBezeichnung = zielBezeichnung
                });
            }

            int anzahlNeu = amArbeitsauswahl.Count;

            bool gespeichertVerifiziert = SpeichereErinnerungsmodell();

            amArbeitsauswahl.Clear();
            AktualisiereAmArbeitsauswahlAnzeige();

            AmStatusText.Text = gespeichertVerifiziert
                ? "✓ " + anzahlNeu + " neue Zuordnung(en) zu \"" + zielBezeichnung + "\" angelegt und gespeichert."
                : "⚠ Zuordnung angelegt, aber Speichern konnte nicht verifiziert werden - bitte prüfen.";
        }

        private void AmArbeitsauswahlLeeren_Click(object sender, RoutedEventArgs e)
        {
            amArbeitsauswahl.Clear();
            AktualisiereAmArbeitsauswahlAnzeige();
            AmStatusText.Text = "Arbeitsauswahl geleert - es wurde nichts zugeordnet.";
        }

        // Schreibt AUSSCHLIESSLICH erinnerungsmodell.json (niemals
        // personen.json), danach Rückeinlese-Verifikation wie bereits in
        // Bauphase 3 - gleiches Sicherheitsprinzip.
        private bool SpeichereErinnerungsmodell()
        {
            try
            {
                ArchivErinnerungsDaten daten = new ArchivErinnerungsDaten
                {
                    Erinnerungen = erinnerungsmodellErinnerungen,
                    Zuordnungen = erinnerungsmodellZuordnungen
                };

                JsonSerializerOptions optionen = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(daten, optionen);

                File.WriteAllText(ErinnerungsmodellDateiPfad, json);

                string rueckgelesen = File.ReadAllText(ErinnerungsmodellDateiPfad);
                ArchivErinnerungsDaten kontrolle = JsonSerializer.Deserialize<ArchivErinnerungsDaten>(rueckgelesen);

                return kontrolle != null
                    && kontrolle.Erinnerungen != null
                    && kontrolle.Zuordnungen != null
                    && kontrolle.Erinnerungen.Count == erinnerungsmodellErinnerungen.Count
                    && kontrolle.Zuordnungen.Count == erinnerungsmodellZuordnungen.Count;
            }
            catch (Exception ex)
            {
                James.Problem("Das neue Erinnerungsmodell konnte nicht gespeichert werden: " + ex.Message + "\n\npersonen.json ist davon nicht betroffen.");
                return false;
            }
        }
    }
}
