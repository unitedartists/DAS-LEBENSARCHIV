using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
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
        private List<Zuordnung> erinnerungsmodellZuordnungenPapierkorb;
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
            erinnerungsmodellZuordnungenPapierkorb = new List<Zuordnung>();

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

                        if (daten.ZuordnungenPapierkorb != null)
                        {
                            erinnerungsmodellZuordnungenPapierkorb = daten.ZuordnungenPapierkorb;
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
                Width = 155,
                Height = 70,
                Margin = new Thickness(3),
                BorderBrush = Brushes.LightGray,
                BorderThickness = new Thickness(1),
                Background = Brushes.WhiteSmoke,
                Tag = erinnerung
            };

            StackPanel inhalt = new StackPanel { Orientation = Orientation.Horizontal };

            Border bildRahmen = new Border
            {
                Width = 54,
                Height = 54,
                Margin = new Thickness(3),
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
                MaxWidth = 90,
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
        // ARBEITSMOTOR (09.08.): TESTIMPORT
        // ============================================================
        // Kopierender, additiver Vorgang (A's Regel) - Originaldateien
        // werden nie verschoben, gelöscht, umbenannt oder überschrieben.
        // Dopplungsschutz über den Hash: wird derselbe Ordner erneut
        // importiert, erkennt James bereits vorhandene Dateien anhand
        // ihres Hashes und überspringt sie - reiner Import-Schutz,
        // KEINE Zusammenführung/Deduplizierung bestehender Erinnerungen
        // (die bleibt eine spätere, eigens freizugebende Entscheidung).
        private class TestimportErgebnis
        {
            public int Importiert;
            public int UebersprungenDuplikat;
            public int UebersprungenTyp;
        }

        private static readonly string[] BildEndungen = { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff", ".heic", ".webp" };
        private static readonly string[] PdfEndungen = { ".pdf" };
        private static readonly string[] DokumentEndungen = { ".doc", ".docx", ".odt", ".rtf", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".md" };
        private static readonly string[] VideoEndungen = { ".mp4", ".mov", ".avi", ".mkv", ".wmv", ".m4v" };

        // ARBEITSMOTOR, Punkt 9+10: einheitliche Medienlogik - erkennt
        // den Medientyp rein anhand der Dateiendung, bewusst ohne
        // typspezifische Sonderbehandlung (OCR, Videoanalyse folgen
        // erst später, falls gewünscht).
        private static MedienTyp? ErmittleMedienTyp(string dateiendung)
        {
            string endung = (dateiendung ?? "").ToLowerInvariant();

            if (BildEndungen.Contains(endung)) { return MedienTyp.Bild; }
            if (PdfEndungen.Contains(endung)) { return MedienTyp.Pdf; }
            if (DokumentEndungen.Contains(endung)) { return MedienTyp.Dokument; }
            if (VideoEndungen.Contains(endung)) { return MedienTyp.Video; }

            return null;
        }

        private static DateTime? SicheresDateiDatum(string pfad)
        {
            try
            {
                return File.GetLastWriteTime(pfad);
            }
            catch
            {
                return null;
            }
        }

        private TestimportErgebnis TestimportDurchfuehren(List<string> quellDateien)
        {
            LadeErinnerungsmodellFallsNoetig();

            TestimportErgebnis ergebnis = new TestimportErgebnis();

            string zielOrdner = Path.Combine(OrdnerPfad, "Testimport");
            Directory.CreateDirectory(zielOrdner);

            HashSet<string> bekannteHashes = erinnerungsmodellErinnerungen
                .Where(er => !string.IsNullOrEmpty(er.Hashwert))
                .Select(er => er.Hashwert)
                .ToHashSet();

            foreach (string quellDatei in quellDateien)
            {
                MedienTyp? typ = ErmittleMedienTyp(Path.GetExtension(quellDatei));

                if (typ == null)
                {
                    ergebnis.UebersprungenTyp++;
                    continue;
                }

                string hash;

                try
                {
                    using (SHA256 sha256 = SHA256.Create())
                    using (FileStream stream = File.OpenRead(quellDatei))
                    {
                        hash = Convert.ToHexString(sha256.ComputeHash(stream));
                    }
                }
                catch
                {
                    continue;
                }

                if (bekannteHashes.Contains(hash))
                {
                    ergebnis.UebersprungenDuplikat++;
                    continue;
                }

                // Kopierender Vorgang - die Originaldatei am gewählten
                // Ort bleibt vollständig unangetastet.
                string neuerDateiname = Guid.NewGuid() + Path.GetExtension(quellDatei);
                string zielPfad = Path.Combine(zielOrdner, neuerDateiname);
                File.Copy(quellDatei, zielPfad, overwrite: false);

                Erinnerung erinnerung = new Erinnerung
                {
                    Hashwert = hash,
                    MedienTyp = typ.Value,
                    Erstellungsdatum = SicheresDateiDatum(quellDatei)
                };
                erinnerung.Fundorte.Add(new Fundort { Pfad = zielPfad, FundortRolle = "Testimport-Kopie" });

                erinnerungsmodellErinnerungen.Add(erinnerung);
                bekannteHashes.Add(hash);
                ergebnis.Importiert++;
            }

            if (ergebnis.Importiert > 0)
            {
                SpeichereErinnerungsmodell();
            }

            return ergebnis;
        }

        // Gemeinsame Vorschau+Bestätigung+Ausführung für beide Einstiege
        // (Einzeldateien und ganzer Ordner) - eine Stelle statt zwei fast
        // gleicher Abläufe (A/Opa-Prinzip "keine Nebenbaustellen, vorhandenes
        // wiederverwenden").
        private void BestaetigeUndFuehreTestimportAus(List<string> dateien, string quellBeschreibung)
        {
            if (dateien == null || dateien.Count == 0)
            {
                James.Hinweis("Keine Datei(en) ausgewählt.");
                return;
            }

            int unterstuetzt = dateien.Count(d => ErmittleMedienTyp(Path.GetExtension(d)) != null);

            bool bestaetigt = James.FrageJaNein(
                quellBeschreibung + "\n\n" +
                dateien.Count + " Datei(en) zum Import ausgewählt, davon " + unterstuetzt + " unterstützte Bild-/PDF-/Dokument-/Videodateien.\n\n" +
                "Diese werden KOPIERT (das Original bleibt unverändert) und als zusätzlicher Testbestand aufgenommen. Bereits identische, schon importierte Dateien werden automatisch übersprungen.\n\n" +
                "Import jetzt starten?",
                James.TitelEntscheidung);

            if (!bestaetigt)
            {
                return;
            }

            TestimportErgebnis testergebnis = TestimportDurchfuehren(dateien);

            James.Hinweis(
                testergebnis.Importiert + " neue Erinnerung(en) importiert.\n" +
                testergebnis.UebersprungenDuplikat + " bereits vorhanden (übersprungen).\n" +
                testergebnis.UebersprungenTyp + " nicht unterstützte Datei(en) übersprungen.");
        }

        // A/Opa-FOLGEAUFTRAG (10.08.), Punkt 2A: gezielt einzelne Dateien
        // wählen, statt zwangsläufig einen ganzen Ordner zu importieren.
        private void TestimportDateienWaehlenUndAusfuehren()
        {
            LadeErinnerungsmodellFallsNoetig();

            Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Einzelne Datei(en) für Testimport wählen",
                Multiselect = true,
                Filter = "Unterstützte Dateien|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tif;*.tiff;*.heic;*.webp;*.pdf;*.doc;*.docx;*.odt;*.rtf;*.xls;*.xlsx;*.ppt;*.pptx;*.txt;*.md;*.mp4;*.mov;*.avi;*.mkv;*.wmv;*.m4v|Alle Dateien (*.*)|*.*"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            BestaetigeUndFuehreTestimportAus(dialog.FileNames.ToList(), "Einzeln ausgewählte Datei(en).");
        }

        // A/Opa-FOLGEAUFTRAG (10.08.), Punkt 2B: bisherige Ordner-Funktion
        // bleibt vollständig erhalten - jetzt über dieselbe zentrale
        // Bestätigungs-/Ausführungslogik wie der Einzeldatei-Import.
        private void TestimportOrdnerWaehlenUndAusfuehren()
        {
            LadeErinnerungsmodellFallsNoetig();

            Microsoft.Win32.OpenFolderDialog dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Ordner für Testimport wählen"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            string ordner = dialog.FolderName;

            List<string> dateien = Directory.Exists(ordner)
                ? Directory.GetFiles(ordner, "*", SearchOption.AllDirectories).ToList()
                : new List<string>();

            BestaetigeUndFuehreTestimportAus(dateien, "Ordner: " + ordner);
        }

        // A/Opa-FOLGEAUFTRAG (10.08.), Punkt 3: dieselben Import-Methoden,
        // jetzt auch direkt aus der AM erreichbar - klar getrennt von der
        // Suche nach bereits vorhandenen Erinnerungen (siehe MainWindow.xaml,
        // eigener Abschnitt "Neue Bilder/Ordner importieren"). Nach dem
        // Import wird die Direktsuche aktualisiert, damit neu importierte
        // Erinnerungen sofort auffindbar sind.
        private void AmTestimportDatei_Click(object sender, RoutedEventArgs e)
        {
            TestimportDateienWaehlenUndAusfuehren();
            AktualisiereAmDirekteAuswahlListe();
        }

        private void AmTestimportOrdner_Click(object sender, RoutedEventArgs e)
        {
            TestimportOrdnerWaehlenUndAusfuehren();
            AktualisiereAmDirekteAuswahlListe();
        }

        // ============================================================
        // BETRACHTER + ARBEITSMOTOR ÖFFNEN
        // ============================================================
        private void ErinnerungsmodellBetrachterOeffnen_Click(object sender, RoutedEventArgs e)
        {
            LadeErinnerungsmodellFallsNoetig();

            ErinnerungsmodellBetrachterFenster fenster = new ErinnerungsmodellBetrachterFenster(
                erinnerungsmodellErinnerungen,
                erinnerungsmodellZuordnungen,
                erinnerungsmodellZuordnungenPapierkorb,
                allePersonen,
                ArchivListe.Items.OfType<Person>().ToList(),
                freieEreignisse,
                freieEreignisseArchiv,
                sammlungen,
                sammlungenArchiv,
                LiesVisuelleMerkmale,
                TestimportDateienWaehlenUndAusfuehren,
                TestimportOrdnerWaehlenUndAusfuehren,
                PruefeSehzentrumBestand,
                EntferneZuordnungenInPapierkorb,
                WiederherstelleZuordnung,
                LoescheZuordnungEndgueltig,
                ids => SendeErinnerungsIdsZurArbeitsmappe(ids, "Suche"),
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

            // Nach jeder Aktualisierung ist nichts markiert - Buttons, die
            // eine Markierung erfordern, starten deaktiviert (Sicherheitsfix,
            // siehe AmArbeitsauswahlListe_SelectionChanged).
            AmZuordnenBestaetigenButton.IsEnabled = false;
            AmAusgewaehlteEntfernenButton.IsEnabled = false;
            AmMarkierungsHinweisText.Text = istAusgewaehlt
                ? "Bitte oben markieren, welche Erinnerung(en) diese Aktion betreffen soll (Strg-/Umschalt-Klick für mehrere)."
                : "";

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

            bool zielVorhanden = AmZielObjektComboBox.Items.Count > 0;

            // Der "Neue Zuordnung anlegen"-Button hängt jetzt von der
            // MARKIERUNG in AmArbeitsauswahlListe ab (Sicherheitsfix, siehe
            // AmArbeitsauswahlListe_SelectionChanged) - hier nur zusätzlich
            // deaktivieren, wenn gar kein Ziel wählbar ist.
            if (!zielVorhanden)
            {
                AmZuordnenBestaetigenButton.IsEnabled = false;
            }

            if (zielVorhanden)
            {
                AmZielObjektComboBox.SelectedIndex = 0;
            }
        }

        // A/Opa-INTEGRATIONSAUFTRAG (10.08.), Punkt 7 "wichtigster Bugfix":
        // Der Großtest zeigte, dass "Neue Zuordnung anlegen" bisher auf
        // die GESAMTE Arbeitsauswahl wirkte, unabhängig davon, was
        // markiert war. Das ist behoben: die Methode wirkt jetzt
        // AUSSCHLIESSLICH auf die in AmArbeitsauswahlListe markierten
        // Einträge. Ist nichts markiert, wird KEINE Aktion durchgeführt,
        // stattdessen eine klare, verständliche Meldung gezeigt.
        private List<ArbeitsauswahlEintrag> ErmittleMarkierteArbeitsauswahl()
        {
            return AmArbeitsauswahlListe.SelectedItems
                .Cast<Border>()
                .Select(b => b.Tag as Erinnerung)
                .Where(er => er != null)
                .Select(er => amArbeitsauswahl.FirstOrDefault(a => a.ErinnerungId == er.Id))
                .Where(a => a != null)
                .ToList();
        }

        private void AmArbeitsauswahlListe_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            int markiert = AmArbeitsauswahlListe.SelectedItems.Count;

            AmZuordnenBestaetigenButton.IsEnabled = markiert > 0 && AmZielObjektComboBox.Items.Count > 0;
            AmAusgewaehlteEntfernenButton.IsEnabled = markiert > 0;

            AmMarkierungsHinweisText.Text = markiert == 0
                ? "Bitte oben markieren, welche Erinnerung(en) diese Aktion betreffen soll (Strg-/Umschalt-Klick für mehrere)."
                : markiert + " von " + amArbeitsauswahl.Count + " markiert - nur diese werden bei \"Neue Zuordnung anlegen\" oder \"Entfernen\" betroffen.";
        }

        private void AmZuordnenBestaetigen_Click(object sender, RoutedEventArgs e)
        {
            List<ArbeitsauswahlEintrag> markiert = ErmittleMarkierteArbeitsauswahl();

            if (markiert.Count == 0)
            {
                James.Hinweis("Bitte zuerst markieren, welche Erinnerung(en) neu zugeordnet werden sollen (in der Liste oben Strg-/Umschalt-Klick). Ohne Markierung wird nichts verändert.");
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
                "Genau " + markiert.Count + " markierte Erinnerung(en) neu zuordnen zu \"" + zielBezeichnung + "\"?\n\n" +
                "Nicht markierte Erinnerungen in der Arbeitsauswahl bleiben unverändert. Bisherige Zuordnungen der markierten Erinnerungen bleiben zusätzlich bestehen.",
                James.TitelEntscheidung);

            if (!ergebnis)
            {
                return;
            }

            foreach (ArbeitsauswahlEintrag eintrag in markiert)
            {
                erinnerungsmodellZuordnungen.Add(new Zuordnung
                {
                    ErinnerungId = eintrag.ErinnerungId,
                    ZielTyp = zielTyp,
                    ZielId = zielId,
                    ZielBezeichnung = zielBezeichnung
                });

                // Nur die markierten Einträge verlassen die Arbeitsauswahl -
                // unmarkierte bleiben unangetastet stehen.
                amArbeitsauswahl.Remove(eintrag);
            }

            int anzahlNeu = markiert.Count;

            bool gespeichertVerifiziert = SpeichereErinnerungsmodell();

            AktualisiereAmArbeitsauswahlAnzeige();

            AmStatusText.Text = gespeichertVerifiziert
                ? "✓ " + anzahlNeu + " markierte Erinnerung(en) zu \"" + zielBezeichnung + "\" neu zugeordnet und gespeichert."
                : "⚠ Zuordnung angelegt, aber Speichern konnte nicht verifiziert werden - bitte prüfen.";
        }

        // Entfernt NUR die markierten Erinnerungen aus der aktuellen
        // Arbeitsauswahl (nicht aus irgendeiner Zuordnung!) - für den
        // Fall, dass sich eine alte, nicht mehr gewünschte Erinnerung
        // in die Auswahl "verirrt" hat.
        private void AmAusgewaehlteEntfernen_Click(object sender, RoutedEventArgs e)
        {
            List<ArbeitsauswahlEintrag> markiert = ErmittleMarkierteArbeitsauswahl();

            if (markiert.Count == 0)
            {
                return;
            }

            foreach (ArbeitsauswahlEintrag eintrag in markiert)
            {
                amArbeitsauswahl.Remove(eintrag);
            }

            AktualisiereAmArbeitsauswahlAnzeige();
            AmStatusText.Text = markiert.Count + " Erinnerung(en) aus der Arbeitsauswahl entfernt (nicht zugeordnet, nichts gelöscht).";
        }

        private void AmArbeitsauswahlLeeren_Click(object sender, RoutedEventArgs e)
        {
            amArbeitsauswahl.Clear();
            AktualisiereAmArbeitsauswahlAnzeige();
            AmStatusText.Text = "Arbeitsauswahl geleert - es wurde nichts zugeordnet.";
        }

        // ============================================================
        // ZUORDNUNGS-PAPIERKORB (10.08., A/Opa-Integrationsauftrag Punkt 12+13)
        // ============================================================
        // Verschiebt Zuordnungen statt sie zu löschen - betrifft NIE die
        // Erinnerung selbst oder ihre Fundorte, nur den Zuordnungs-
        // Datensatz. Physische Originaldateien werden hier nie berührt.
        private void EntferneZuordnungenInPapierkorb(List<Guid> erinnerungIds, ZuordnungsZielTyp zielTyp, Guid zielId)
        {
            LadeErinnerungsmodellFallsNoetig();

            List<Zuordnung> betroffene = erinnerungsmodellZuordnungen
                .Where(z => z.ZielTyp == zielTyp && z.ZielId == zielId && erinnerungIds.Contains(z.ErinnerungId))
                .ToList();

            foreach (Zuordnung zuordnung in betroffene)
            {
                erinnerungsmodellZuordnungen.Remove(zuordnung);
                erinnerungsmodellZuordnungenPapierkorb.Add(zuordnung);
            }

            SpeichereErinnerungsmodell();
        }

        private void WiederherstelleZuordnung(Zuordnung zuordnung)
        {
            LadeErinnerungsmodellFallsNoetig();

            if (!erinnerungsmodellZuordnungenPapierkorb.Remove(zuordnung))
            {
                return;
            }

            erinnerungsmodellZuordnungen.Add(zuordnung);
            SpeichereErinnerungsmodell();
        }

        private void LoescheZuordnungEndgueltig(Zuordnung zuordnung)
        {
            LadeErinnerungsmodellFallsNoetig();

            erinnerungsmodellZuordnungenPapierkorb.Remove(zuordnung);
            SpeichereErinnerungsmodell();
        }

        // ============================================================
        // SEHZENTRUM-DATENBESTAND-DIAGNOSE (10.08., Sanierungsplan Punkt 4)
        // ============================================================
        // Rein lesend: zählt, für wie viele der migrierten/importierten
        // Erinnerungen tatsächlich ein Sehzentrum-Eintrag existiert -
        // klärt, ob "Suche Hund" mangels Daten nichts findet, oder ob
        // trotz vorhandener Daten kein Treffer entsteht.
        private string PruefeSehzentrumBestand()
        {
            LadeErinnerungsmodellFallsNoetig();

            int mitWissen = 0;
            int gesamtMerkmale = 0;

            foreach (Erinnerung erinnerung in erinnerungsmodellErinnerungen)
            {
                if (erinnerung.Fundorte == null || erinnerung.Fundorte.Count == 0)
                {
                    continue;
                }

                string dateiname = Path.GetFileName(erinnerung.Fundorte[0].Pfad);
                List<VisuellesMerkmal> merkmale = LiesVisuelleMerkmale(dateiname);

                if (merkmale != null && merkmale.Count > 0)
                {
                    mitWissen++;
                    gesamtMerkmale += merkmale.Count;
                }
            }

            return mitWissen + " von " + erinnerungsmodellErinnerungen.Count + " Erinnerung(en) haben bereits Sehzentrum-Wissen (" + gesamtMerkmale + " Merkmal(e) insgesamt).\n\n" +
                (mitWissen == 0
                    ? "Das erklärt vermutlich, warum eine Suche wie \"Hund\" noch nichts findet - James hat für den aktuellen Bestand einfach noch nichts gelernt, nicht weil die Suche fehlerhaft wäre."
                    : "Eine Suche nach einem tatsächlich gelernten Merkmal sollte jetzt Treffer liefern.");
        }
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
