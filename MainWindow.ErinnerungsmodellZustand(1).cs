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
    public partial class MainWindow
    {
        private List<Erinnerung> erinnerungsmodellErinnerungen;
        private List<Zuordnung> erinnerungsmodellZuordnungen;
        private List<Zuordnung> erinnerungsmodellZuordnungenPapierkorb;
        private bool erinnerungsmodellGeladen;

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

        private List<Erinnerung> ErmittleErinnerungenFuerZiel(ZuordnungsZielTyp zielTyp, Guid zielId)
        {
            LadeErinnerungsmodellFallsNoetig();

            List<Guid> ids = erinnerungsmodellZuordnungen
                .Where(z => z.ZielTyp == zielTyp && z.ZielId == zielId)
                .Select(z => z.ErinnerungId)
                .Distinct()
                .ToList();

            return erinnerungsmodellErinnerungen.Where(er => ids.Contains(er.Id)).ToList();
        }

        private void ErgaenzeUmNeuesModell(List<ErinnerungsInfo> bestehend, ZuordnungsZielTyp zielTyp, Guid zielId)
        {
            HashSet<string> bekanntePfade = bestehend
                .Where(info => !string.IsNullOrEmpty(info.Pfad))
                .Select(info => info.Pfad.ToLowerInvariant())
                .ToHashSet();

            foreach (Erinnerung erinnerung in ErmittleErinnerungenFuerZiel(zielTyp, zielId))
            {
                string pfad = erinnerung.Fundorte?
                    .Select(f => f.Pfad)
                    .FirstOrDefault(p => !string.IsNullOrEmpty(p) && File.Exists(p));

                if (pfad == null || bekanntePfade.Contains(pfad.ToLowerInvariant()))
                {
                    continue;
                }

                bestehend.Add(new ErinnerungsInfo
                {
                    Pfad = pfad,
                    Titel = "(neu zugeordnet)"
                });

                bekanntePfade.Add(pfad.ToLowerInvariant());
            }
        }

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

        private void AmDirekteSucheTextBox_TextChanged(object sender, RoutedEventArgs e)
        {
            AktualisiereAmDirekteAuswahlListe();
        }

        // A/Opa-ENTSCHEIDUNG (11.08.): "Zwei Karteikästen"-Befund - James
        // lernte im Sehzentrum (hash-basiert, sehgedaechtnis.json), die
        // Suche fragte bisher aber das aeltere, dateiname-basierte
        // Erinnerungsgedaechtnis (LiesVisuelleMerkmale) ab, das davon nie
        // etwas erfuhr. A's Entscheidung: Sehzentrum wird die EINZIGE
        // Quelle fuer Bildwissen in der Suche. Das alte Erinnerungs-
        // gedaechtnis bleibt unveraendert liegen (kein Loeschen, keine
        // Migration jetzt) - wird hier schlicht nicht mehr angefragt, um
        // keine zwei parallelen Wissenssysteme gleichzeitig zu durchsuchen.
        // Verknuepfung ueber den Hashwert - denselben Schluessel, den auch
        // SehgedaechtnisEintrag selbst verwendet.
        private bool ErinnerungPasstZurZentralenSuche(Erinnerung erinnerung, string suchtext, List<SehgedaechtnisEintrag> sehgedaechtnis)
        {
            if (erinnerung.Fundorte != null && erinnerung.Fundorte.Any(f => (f.Pfad ?? "").ToLowerInvariant().Contains(suchtext)))
            {
                return true;
            }

            if (erinnerungsmodellZuordnungen
                .Where(z => z.ErinnerungId == erinnerung.Id)
                .Any(z => !string.IsNullOrEmpty(z.ZielBezeichnung) && z.ZielBezeichnung.ToLowerInvariant().Contains(suchtext)))
            {
                return true;
            }

            if (!string.IsNullOrEmpty(erinnerung.Hashwert))
            {
                SehgedaechtnisEintrag eintrag = sehgedaechtnis.FirstOrDefault(s => s.Hashwert == erinnerung.Hashwert);

                if (eintrag != null && eintrag.BestaetigteStichwoerter != null
                    && eintrag.BestaetigteStichwoerter.Any(b => (b ?? "").ToLowerInvariant().Contains(suchtext)))
                {
                    return true;
                }
            }

            return false;
        }

        private List<Erinnerung> ZentraleErinnerungsSuche(string suchtext, bool zentraleAlphabetisch)
        {
            LadeErinnerungsmodellFallsNoetig();

            string normalisiert = (suchtext ?? "").Trim().ToLowerInvariant();

            IEnumerable<Erinnerung> treffer = erinnerungsmodellErinnerungen;

            if (normalisiert != "")
            {
                List<SehgedaechtnisEintrag> sehgedaechtnis = LadeSehgedaechtnis();
                treffer = treffer.Where(er => ErinnerungPasstZurZentralenSuche(er, normalisiert, sehgedaechtnis));
            }

            return zentraleAlphabetisch
                ? treffer.OrderBy(er => er.Fundorte.Count > 0 ? Path.GetFileName(er.Fundorte[0].Pfad) : "", StringComparer.OrdinalIgnoreCase).ToList()
                // A/Opa-REPARATURAUFTRAG (11.08.), PROBLEM 1: "neueste
                // zuerst" verwendet jetzt vorrangig den echten Import-
                // zeitpunkt statt (wie zuvor) das Erstellungsdatum der
                // Originaldatei - bestehende Erinnerungen ohne
                // ImportiertAm funktionieren unveraendert weiter ueber
                // den bisherigen Fallback.
                : treffer.OrderByDescending(er => er.ImportiertAm ?? er.Erstellungsdatum ?? er.CreatedAt).ToList();
        }

        private void AktualisiereAmDirekteAuswahlListe()
        {
            LadeErinnerungsmodellFallsNoetig();

            if (AmDirekteSucheTextBox == null || AmDirekteAuswahlListe == null)
            {
                return;
            }

            string suchtext = AmDirekteSucheTextBox.Text ?? "";

            ComboBoxItem sortItem = AmSortierungComboBox?.SelectedItem as ComboBoxItem;
            bool alphabetisch = sortItem != null && sortItem.Content.ToString().StartsWith("Alphabetisch");

            // Nutzer-Feedback (11.08.): neu importierte Dateien sollen IMMER
            // sichtbar sein, ohne erst gezielt danach suchen zu muessen.
            // Vorher wurden bereits in der Arbeitsauswahl befindliche
            // Erinnerungen hier komplett ausgeblendet - unnoetig streng,
            // da FuegeZurArbeitsauswahlHinzu ein erneutes Hinzufuegen
            // ohnehin folgenlos abfaengt (kein Duplikat moeglich). Solche
            // Eintraege bleiben jetzt sichtbar, nur mit einem Hinweis
            // markiert.
            HashSet<Guid> bereitsAusgewaehlt = amArbeitsauswahl.Select(a => a.ErinnerungId).ToHashSet();

            List<Erinnerung> treffer = ZentraleErinnerungsSuche(suchtext, alphabetisch).ToList();

            AmDirekteAuswahlListe.Items.Clear();

            foreach (Erinnerung erinnerung in treffer)
            {
                string dateiname = erinnerung.Fundorte.Count > 0 ? Path.GetFileName(erinnerung.Fundorte[0].Pfad) : erinnerung.Id.ToString();

                string beschriftung = bereitsAusgewaehlt.Contains(erinnerung.Id)
                    ? dateiname + "\n(bereits ausgewählt)"
                    : dateiname;

                AmDirekteAuswahlListe.Items.Add(ErstelleErinnerungsKachel(erinnerung, beschriftung));
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

        private void SendeErinnerungsIdsZurArbeitsmappe(List<Guid> erinnerungIds, string herkunft)
        {
            foreach (Guid id in erinnerungIds)
            {
                FuegeZurArbeitsauswahlHinzu(id, herkunft);
            }

            HauptTabControl.SelectedIndex = ArbeitsmappeTabIndex;
            AktualisiereAmArbeitsauswahlAnzeige();
        }

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

                // A/Opa-ENTSCHEIDUNG (11.08.), Folgefund zum "Zwei Karteikaesten"-
                // Bug: der Testimport berechnete den Hashwert bisher selbst
                // (SHA256 + Convert.ToHexString), waehrend die bereits
                // bestehende, im Projekt etablierte Methode BerechneHashwert
                // (Werkzeuge.cs, auch vom Sehzentrum und vom "Computer
                // kennenlernen"-Rundgang genutzt) SHA256 + Convert.
                // ToBase64String verwendet. Gleicher Algorithmus, aber zwei
                // unterschiedliche Textformate - dieselbe Datei bekam so
                // NIE denselben Hashwert, die Sehzentrum-Verknuepfung lief
                // dadurch komplett ins Leere. Fix: keine zweite Hash-
                // Berechnung mehr, stattdessen dieselbe geteilte Methode.
                string hash;

                try
                {
                    hash = BerechneHashwert(quellDatei);
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

                string neuerDateiname = Guid.NewGuid() + Path.GetExtension(quellDatei);
                string zielPfad = Path.Combine(zielOrdner, neuerDateiname);
                File.Copy(quellDatei, zielPfad, overwrite: false);

                Erinnerung erinnerung = new Erinnerung
                {
                    Hashwert = hash,
                    MedienTyp = typ.Value,
                    Erstellungsdatum = SicheresDateiDatum(quellDatei),
                    // A/Opa-REPARATURAUFTRAG (11.08.), PROBLEM 1: echter
                    // Importzeitpunkt, getrennt vom (moeglicherweise sehr
                    // alten) Erstellungsdatum der Originaldatei - macht
                    // "neueste zuerst" endlich zuverlaessig.
                    ImportiertAm = DateTime.Now
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

        private void ErinnerungsmodellBetrachterOeffnen_Click(object sender, RoutedEventArgs e)
        {
            OeffneArbeitsmotor(null, null);
        }

        private void OeffneArbeitsmotorFuerZiel(ZuordnungsZielTyp zielTyp, Guid zielId)
        {
            OeffneArbeitsmotor(zielTyp, zielId);
        }

        private void OeffneArbeitsmotor(ZuordnungsZielTyp? vorausgewaehlterZielTyp, Guid? vorausgewaehlteZielId)
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
                ZentraleErinnerungsSuche,
                TestimportDateienWaehlenUndAusfuehren,
                TestimportOrdnerWaehlenUndAusfuehren,
                PruefeSehzentrumBestand,
                EntferneZuordnungenInPapierkorb,
                WiederherstelleZuordnung,
                LoescheZuordnungEndgueltig,
                ids => SendeErinnerungsIdsZurArbeitsmappe(ids, "Suche"),
                ids => SendeErinnerungsIdsZurArbeitsmappe(ids, "Ziel"),
                vorausgewaehlterZielTyp,
                vorausgewaehlteZielId);

            fenster.Owner = this;
            fenster.ShowDialog();
        }

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

            if (!zielVorhanden)
            {
                AmZuordnenBestaetigenButton.IsEnabled = false;
            }

            if (zielVorhanden)
            {
                AmZielObjektComboBox.SelectedIndex = 0;
            }
        }

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

                amArbeitsauswahl.Remove(eintrag);
            }

            int anzahlNeu = markiert.Count;

            bool gespeichertVerifiziert = SpeichereErinnerungsmodell();

            AktualisiereAmArbeitsauswahlAnzeige();

            AmStatusText.Text = gespeichertVerifiziert
                ? "✓ " + anzahlNeu + " markierte Erinnerung(en) zu \"" + zielBezeichnung + "\" neu zugeordnet und gespeichert."
                : "⚠ Zuordnung angelegt, aber Speichern konnte nicht verifiziert werden - bitte prüfen.";
        }

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
        // A/Opa-REPARATURAUFTRAG (11.08.), PROBLEM 3
        // ============================================================
        // Wird von den alten Papierkorb-Kontext-Callbacks (MainWindow.
        // Erinnerungskarte.cs, MainWindow.Sammlungen.cs, MainWindow.
        // BesondereEreignisse.cs) NUR dann aufgerufen, wenn die jeweilige
        // alte Entfernen-Logik nichts gefunden hat (keine stillschweigend
        // wirkungslose Aktion mehr). Sucht die Erinnerung ueber den
        // Dateipfad, prueft ob dafuer tatsaechlich eine aktive Zuordnung
        // zu genau diesem Ziel existiert, und verschiebt in diesem Fall
        // NUR diese eine Zuordnung in den Zuordnungs-Papierkorb (bereits
        // bestehende, getestete EntferneZuordnungenInPapierkorb wird
        // wiederverwendet - keine zweite Papierkorb-Logik). Erinnerung
        // selbst, andere Zuordnungen und die Originaldatei bleiben
        // unangetastet. Gibt zurueck, ob wirklich etwas entfernt wurde.
        private bool VersucheAusNeuemModellEntfernen(ZuordnungsZielTyp zielTyp, Guid zielId, string pfad)
        {
            LadeErinnerungsmodellFallsNoetig();

            Erinnerung erinnerung = erinnerungsmodellErinnerungen.FirstOrDefault(er =>
                er.Fundorte != null && er.Fundorte.Any(f => string.Equals(f.Pfad, pfad, StringComparison.OrdinalIgnoreCase)));

            if (erinnerung == null)
            {
                return false;
            }

            bool vorhanden = erinnerungsmodellZuordnungen.Any(z =>
                z.ErinnerungId == erinnerung.Id && z.ZielTyp == zielTyp && z.ZielId == zielId);

            if (!vorhanden)
            {
                return false;
            }

            EntferneZuordnungenInPapierkorb(new List<Guid> { erinnerung.Id }, zielTyp, zielId);

            return true;
        }

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

        // A/Opa-ENTSCHEIDUNG (11.08.): auf Sehzentrum umgestellt, wie die
        // Suche selbst - dieselbe Quelle, dieselbe Logik ueberall.
        private string PruefeSehzentrumBestand()
        {
            LadeErinnerungsmodellFallsNoetig();

            List<SehgedaechtnisEintrag> sehgedaechtnis = LadeSehgedaechtnis();

            int mitWissen = 0;
            int gesamtBegriffe = 0;

            foreach (Erinnerung erinnerung in erinnerungsmodellErinnerungen)
            {
                if (string.IsNullOrEmpty(erinnerung.Hashwert))
                {
                    continue;
                }

                SehgedaechtnisEintrag eintrag = sehgedaechtnis.FirstOrDefault(s => s.Hashwert == erinnerung.Hashwert);

                if (eintrag != null && eintrag.BestaetigteStichwoerter != null && eintrag.BestaetigteStichwoerter.Count > 0)
                {
                    mitWissen++;
                    gesamtBegriffe += eintrag.BestaetigteStichwoerter.Count;
                }
            }

            return mitWissen + " von " + erinnerungsmodellErinnerungen.Count + " Erinnerung(en) haben bereits Sehzentrum-Wissen (" + gesamtBegriffe + " Begriff(e) insgesamt).\n\n" +
                (mitWissen == 0
                    ? "Das erklärt vermutlich, warum eine Suche wie \"Hund\" noch nichts findet - James hat für den aktuellen Bestand einfach noch nichts gelernt, nicht weil die Suche fehlerhaft wäre."
                    : "Eine Suche nach einem tatsächlich gelernten Begriff sollte jetzt Treffer liefern.");
        }

        // ============================================================
        // A/Opa-REPARATURAUFTRAG (11.08.), PROBLEM 2: HASHWERT-REPARATURLAUF
        // ============================================================
        // Berechnet den Hashwert JEDER bestehenden Erinnerung mit der
        // einheitlichen BerechneHashwert-Methode (Werkzeuge.cs) neu -
        // behebt den fruehren Base64/Hex-Formatunterschied fuer bereits
        // vor dem Fix importierte Erinnerungen. Liest dafuer nur die
        // vorhandene Originaldatei ein (kein Schreiben, kein Verschieben,
        // kein Loeschen) und ueberschreibt ausschliesslich das Hashwert-
        // Feld im Verwaltungsbestand (erinnerungsmodell.json). Das
        // Sehzentrum-Wissen (sehgedaechtnis.json) wird hier nirgends
        // angefasst - es wird nur ueber den jetzt korrekten Hashwert
        // wieder auffindbar. Vor dem eigentlichen Lauf wird zur
        // Sicherheit eine zeitgestempelte Kopie der aktuellen
        // erinnerungsmodell.json angelegt.
        private string HashwertReparaturlaufDurchfuehren()
        {
            LadeErinnerungsmodellFallsNoetig();

            string sicherungsPfad = Path.Combine(
                OrdnerPfad,
                "erinnerungsmodell_sicherung_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".json");

            try
            {
                if (File.Exists(ErinnerungsmodellDateiPfad))
                {
                    File.Copy(ErinnerungsmodellDateiPfad, sicherungsPfad, overwrite: false);
                }
            }
            catch (Exception ex)
            {
                return "⚠ Sicherung konnte nicht erstellt werden, Reparaturlauf wurde deshalb NICHT gestartet: " + ex.Message;
            }

            int geprueft = 0;
            int geaendert = 0;
            int nichtLesbar = 0;

            foreach (Erinnerung erinnerung in erinnerungsmodellErinnerungen)
            {
                string pfad = erinnerung.Fundorte?
                    .Select(f => f.Pfad)
                    .FirstOrDefault(p => !string.IsNullOrEmpty(p) && File.Exists(p));

                if (pfad == null)
                {
                    continue;
                }

                geprueft++;

                try
                {
                    string neuerHashwert = BerechneHashwert(pfad);

                    if (neuerHashwert != erinnerung.Hashwert)
                    {
                        erinnerung.Hashwert = neuerHashwert;
                        geaendert++;
                    }
                }
                catch
                {
                    nichtLesbar++;
                }
            }

            bool gespeichertVerifiziert = SpeichereErinnerungsmodell();

            // A's Punkt 6: nach dem Lauf pruefen, ob Erinnerung->Hash->
            // Sehgedaechtnis->Suchbegriff jetzt tatsaechlich zusammenpasst -
            // nutzt die bereits bestehende, bewaehrte Diagnose-Methode.
            string sehzentrumPruefung = PruefeSehzentrumBestand();

            return "Hashwert-Reparaturlauf abgeschlossen.\n\n" +
                "Sicherung vor dem Lauf erstellt unter:\n" + sicherungsPfad + "\n\n" +
                geprueft + " Erinnerung(en) mit vorhandener Datei geprüft, " + geaendert + " Hashwert(e) korrigiert" +
                (nichtLesbar > 0 ? ", " + nichtLesbar + " Datei(en) nicht lesbar (übersprungen, nichts verändert)" : "") + ".\n\n" +
                (gespeichertVerifiziert ? "✓ Ergebnis gespeichert und verifiziert.\n\n" : "⚠ Speichern konnte nicht verifiziert werden - bitte prüfen.\n\n") +
                sehzentrumPruefung;
        }

        private void HashwertReparaturlauf_Click(object sender, RoutedEventArgs e)
        {
            bool bestaetigt = James.FrageJaNein(
                "Der Hashwert-Reparaturlauf berechnet den Hashwert aller bestehenden Erinnerungen mit der jetzt einheitlichen Methode neu (behebt den fruehren Formatunterschied zum Sehzentrum).\n\n" +
                "Es wird vorher automatisch eine Sicherung erstellt. Originaldateien werden dabei nicht gelesen zum Verändern, nur zum Berechnen des Fingerabdrucks - nichts wird gelöscht oder verschoben.\n\n" +
                "Jetzt starten?",
                James.TitelEntscheidung);

            if (!bestaetigt)
            {
                return;
            }

            string ergebnis = HashwertReparaturlaufDurchfuehren();
            James.Hinweis(ergebnis, "Hashwert-Reparaturlauf");
        }

        private bool SpeichereErinnerungsmodell()
        {
            try
            {
                ArchivErinnerungsDaten daten = new ArchivErinnerungsDaten
                {
                    Erinnerungen = erinnerungsmodellErinnerungen,
                    Zuordnungen = erinnerungsmodellZuordnungen,
                    ZuordnungenPapierkorb = erinnerungsmodellZuordnungenPapierkorb
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
