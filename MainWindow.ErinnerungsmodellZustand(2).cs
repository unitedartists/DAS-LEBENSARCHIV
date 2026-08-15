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

        // A/Opa-BAUAUFTRAG "JAMES-EINZUG" (12.08.), Punkt 9: Signatur auf
        // das zentrale SortierModus-Enum umgestellt statt eines einfachen
        // Ja/Nein - deckt jetzt alle 4 geforderten Sortierungen ab, in
        // genau dieser einen Methode, keine zweite Sortierlogik irgendwo
        // sonst. AM, Arbeitsmotor und die neue James-Suche rufen alle
        // dieselbe Methode auf.
        private List<Erinnerung> ZentraleErinnerungsSuche(string suchtext, SortierModus sortierung)
        {
            LadeErinnerungsmodellFallsNoetig();

            string normalisiert = (suchtext ?? "").Trim().ToLowerInvariant();

            IEnumerable<Erinnerung> treffer = erinnerungsmodellErinnerungen;

            if (normalisiert != "")
            {
                List<SehgedaechtnisEintrag> sehgedaechtnis = LadeSehgedaechtnis();
                treffer = treffer.Where(er => ErinnerungPasstZurZentralenSuche(er, normalisiert, sehgedaechtnis));
            }

            switch (sortierung)
            {
                case SortierModus.AlphabetischAufsteigend:
                    return treffer.OrderBy(er => er.Fundorte.Count > 0 ? Path.GetFileName(er.Fundorte[0].Pfad) : "", StringComparer.OrdinalIgnoreCase).ToList();

                case SortierModus.AlphabetischAbsteigend:
                    return treffer.OrderByDescending(er => er.Fundorte.Count > 0 ? Path.GetFileName(er.Fundorte[0].Pfad) : "", StringComparer.OrdinalIgnoreCase).ToList();

                case SortierModus.DatumAeltesteZuerst:
                    // A/Opa-REPARATURAUFTRAG (11.08.), PROBLEM 1: echter
                    // Importzeitpunkt vorrangig, Fallback fuer Altbestand
                    // ohne ImportiertAm bleibt bestehen.
                    return treffer.OrderBy(er => er.ImportiertAm ?? er.Erstellungsdatum ?? er.CreatedAt).ToList();

                default:
                    return treffer.OrderByDescending(er => er.ImportiertAm ?? er.Erstellungsdatum ?? er.CreatedAt).ToList();
            }
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
            SortierModus sortierung = alphabetisch ? SortierModus.AlphabetischAufsteigend : SortierModus.DatumNeuesteZuerst;

            // Nutzer-Feedback (11.08.): neu importierte Dateien sollen IMMER
            // sichtbar sein, ohne erst gezielt danach suchen zu muessen.
            // Vorher wurden bereits in der Arbeitsauswahl befindliche
            // Erinnerungen hier komplett ausgeblendet - unnoetig streng,
            // da FuegeZurArbeitsauswahlHinzu ein erneutes Hinzufuegen
            // ohnehin folgenlos abfaengt (kein Duplikat moeglich). Solche
            // Eintraege bleiben jetzt sichtbar, nur mit einem Hinweis
            // markiert.
            HashSet<Guid> bereitsAusgewaehlt = amArbeitsauswahl.Select(a => a.ErinnerungId).ToHashSet();

            List<Erinnerung> treffer = ZentraleErinnerungsSuche(suchtext, sortierung).ToList();

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

        // A/Opa-BAUAUFTRAG "JAMES-EINZUG" (12.08.), Punkt 5: Kernlogik des
        // Zuordnens, bisher nur inline im AM-Button-Handler - jetzt eine
        // zentrale, wiederverwendbare Methode (kein Kopieren des Handlers
        // fuer die neue James-Suche noetig). Erzeugt ausschliesslich neue
        // Zuordnungs-Datensaetze - NIE eine physische Dateikopie.
        //
        // A/Opa-DIAGNOSE-PLUS-SCHUTZAUFTRAG (13.08.), code-bestaetigte
        // Root Cause der 7 gefundenen Abweichungen ("9 gespeichert, nur 6
        // angezeigt"): diese Methode war die EINZIGE Stelle im gesamten
        // Bestand, die neue Zuordnung-Objekte erzeugt (per grep bestaetigt),
        // und pruefte bisher NICHT, ob dieselbe Erinnerung demselben Ziel
        // bereits zugeordnet war. Jetzt: Schutzregel - die Kombination
        // Erinnerung+ZielTyp+ZielId darf nur einmal existieren. Bereits
        // zugeordnete Erinnerungen werden uebersprungen (gezaehlt, aber
        // kein zweiter Datensatz angelegt). WICHTIG: bestehende, VOR dieser
        // Aenderung bereits entstandene Doppelzuordnungen werden hier NICHT
        // angefasst/bereinigt - das ist ausdruecklich ein separater, noch
        // nicht erteilter Auftrag (A/Opa-Entscheidung 13.08., Punkt 3+4).
        private bool FuehreZuordnungDurch(List<Guid> erinnerungIds, ZuordnungsZielTyp zielTyp, Guid zielId, string zielBezeichnung, out int anzahlBereitsVorhanden)
        {
            LadeErinnerungsmodellFallsNoetig();

            anzahlBereitsVorhanden = 0;

            foreach (Guid erinnerungId in erinnerungIds)
            {
                bool bereitsVorhanden = erinnerungsmodellZuordnungen.Any(z =>
                    z.ErinnerungId == erinnerungId && z.ZielTyp == zielTyp && z.ZielId == zielId);

                if (bereitsVorhanden)
                {
                    anzahlBereitsVorhanden++;
                    continue;
                }

                erinnerungsmodellZuordnungen.Add(new Zuordnung
                {
                    ErinnerungId = erinnerungId,
                    ZielTyp = zielTyp,
                    ZielId = zielId,
                    ZielBezeichnung = zielBezeichnung
                });
            }

            return SpeichereErinnerungsmodell();
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

            List<Guid> erinnerungIds = markiert.Select(eintrag => eintrag.ErinnerungId).ToList();

            bool gespeichertVerifiziert = FuehreZuordnungDurch(erinnerungIds, zielTyp, zielId, zielBezeichnung, out int anzahlBereitsVorhanden);

            foreach (ArbeitsauswahlEintrag eintrag in markiert)
            {
                amArbeitsauswahl.Remove(eintrag);
            }

            // A/Opa-SCHUTZAUFTRAG (13.08.): Ruecklmeldung, wenn Erinnerungen
            // uebersprungen wurden, weil sie diesem Ziel bereits zugeordnet
            // waren - statt stillschweigend nichts zu tun (A's Vorschlag:
            // "Diese Erinnerung ist bereits der Sammlung X zugeordnet").
            int anzahlNeu = markiert.Count - anzahlBereitsVorhanden;

            AktualisiereAmArbeitsauswahlAnzeige();

            string hinweisBereitsVorhanden = anzahlBereitsVorhanden > 0
                ? " (" + anzahlBereitsVorhanden + " war(en) \"" + zielBezeichnung + "\" bereits zugeordnet und wurde(n) übersprungen.)"
                : "";

            AmStatusText.Text = gespeichertVerifiziert
                ? "✓ " + anzahlNeu + " markierte Erinnerung(en) zu \"" + zielBezeichnung + "\" neu zugeordnet und gespeichert." + hinweisBereitsVorhanden
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

        // ============================================================
        // A/Opa-BAUAUFTRAG "JAMES-EINZUG" (12.08.), PUNKT 7
        // ============================================================
        // Der Zuordnungs-Papierkorb war bisher nur im Arbeitsmotor-Fenster
        // sichtbar (siehe Test 3/4 vom 11.08. - keine Bug, aber schlecht
        // auffindbar). Jetzt zusaetzlich im normalen Papierkorb-Tab als
        // eigener, klar beschrifteter Bereich "Gelöste Zuordnungen" -
        // bewusst sprachlich von den drei bestehenden Objekt-Papierkorb-
        // Listen (ganze Person/Ereignis/Sammlung) unterschieden, damit die
        // zwei verschiedenen Ebenen (Objekt vs. einzelne Verknuepfung)
        // fuer den Nutzer nicht verschwimmen. Wiederverwendet ausschliesslich
        // die bereits bestehenden, getesteten Methoden - keine zweite
        // Papierkorb-Logik.
        private void AktualisiereZuordnungsPapierkorbAnzeige()
        {
            LadeErinnerungsmodellFallsNoetig();

            if (JamesGeloesteZuordnungenListe == null)
            {
                return;
            }

            JamesGeloesteZuordnungenListe.Items.Clear();

            foreach (Zuordnung zuordnung in erinnerungsmodellZuordnungenPapierkorb)
            {
                Erinnerung erinnerung = erinnerungsmodellErinnerungen.FirstOrDefault(er => er.Id == zuordnung.ErinnerungId);

                string dateiname = erinnerung != null && erinnerung.Fundorte.Count > 0
                    ? Path.GetFileName(erinnerung.Fundorte[0].Pfad)
                    : "(Erinnerung nicht mehr auffindbar)";

                Border kachel = erinnerung != null
                    ? ErstelleErinnerungsKachel(erinnerung, dateiname + "\nwar: " + zuordnung.ZielTyp + ": " + zuordnung.ZielBezeichnung)
                    : new Border { Child = new TextBlock { Text = dateiname + "\nwar: " + zuordnung.ZielTyp + ": " + zuordnung.ZielBezeichnung, Margin = new Thickness(6) } };

                kachel.Tag = zuordnung;
                JamesGeloesteZuordnungenListe.Items.Add(kachel);
            }

            JamesGeloesteZuordnungenAnzahlText.Text = erinnerungsmodellZuordnungenPapierkorb.Count == 0
                ? "Keine gelösten Zuordnungen."
                : erinnerungsmodellZuordnungenPapierkorb.Count + " gelöste Zuordnung(en):";

            JamesZuordnungWiederherstellenButton.IsEnabled = false;
            JamesZuordnungEndgueltigLoeschenButton.IsEnabled = false;
        }

        private void JamesGeloesteZuordnungenListe_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            int anzahl = JamesGeloesteZuordnungenListe.SelectedItems.Count;
            JamesZuordnungWiederherstellenButton.IsEnabled = anzahl > 0;
            JamesZuordnungEndgueltigLoeschenButton.IsEnabled = anzahl > 0;
        }

        private void JamesZuordnungWiederherstellen_Click(object sender, RoutedEventArgs e)
        {
            List<Zuordnung> ausgewaehlt = JamesGeloesteZuordnungenListe.SelectedItems
                .Cast<Border>()
                .Select(b => b.Tag as Zuordnung)
                .Where(z => z != null)
                .ToList();

            if (ausgewaehlt.Count == 0)
            {
                return;
            }

            foreach (Zuordnung zuordnung in ausgewaehlt)
            {
                WiederherstelleZuordnung(zuordnung);
            }

            AktualisiereZuordnungsPapierkorbAnzeige();

            ZeigeStatusMeldung(ausgewaehlt.Count == 1
                ? "1 Zuordnung wurde wiederhergestellt."
                : ausgewaehlt.Count + " Zuordnungen wurden wiederhergestellt.");
        }

        private void JamesZuordnungEndgueltigLoeschen_Click(object sender, RoutedEventArgs e)
        {
            List<Zuordnung> ausgewaehlt = JamesGeloesteZuordnungenListe.SelectedItems
                .Cast<Border>()
                .Select(b => b.Tag as Zuordnung)
                .Where(z => z != null)
                .ToList();

            if (ausgewaehlt.Count == 0)
            {
                return;
            }

            bool ergebnis = James.FrageJaNein(
                ausgewaehlt.Count + " gelöste Zuordnung(en) endgültig entfernen?\n\n" +
                "Das betrifft ausschließlich diese Zuordnungs-Datensätze - die Erinnerung(en) selbst und ihre physischen Dateien bleiben davon vollständig unberührt.",
                James.TitelEndgueltigeEntscheidung, MessageBoxImage.Warning);

            if (!ergebnis)
            {
                return;
            }

            foreach (Zuordnung zuordnung in ausgewaehlt)
            {
                LoescheZuordnungEndgueltig(zuordnung);
            }

            AktualisiereZuordnungsPapierkorbAnzeige();
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
        // A/Opa-DIAGNOSEAUFTRAG "FEHLENDE FUNDORTE" (13.08.)
        // ============================================================
        // AUSDRUECKLICH REIN LESEND: prueft nur File.Exists (und einen
        // kurzen Lesezugriff zur Unterscheidung "fehlt" vs. "vorhanden,
        // aber nicht lesbar"), veraendert an keiner Stelle Dateien,
        // Zuordnungen, den Papierkorb oder fuehrt irgendeine Migration
        // durch. Soll klaeren, ob "20 zugeordnet, nur 3 angezeigt" bzw.
        // "neu zugeordnet, am Ziel nicht sichtbar" tatsaechlich auf fehlende
        // Fundorte zurueckzufuehren sind - keine Reparatur, nur Befund.

        // Prueft einen einzelnen Fundort-Pfad und unterscheidet die von
        // A verlangten drei Faelle. Reiner Lesezugriff - FileStream wird
        // sofort wieder geschlossen, nichts wird geschrieben.
        private static string PruefeFundortStatus(string pfad)
        {
            if (string.IsNullOrWhiteSpace(pfad))
            {
                return "Pfad ungültig (leer)";
            }

            string vollPfad;

            try
            {
                vollPfad = Path.GetFullPath(pfad);
            }
            catch
            {
                return "Pfad ungültig";
            }

            if (!File.Exists(vollPfad))
            {
                return "Datei nicht vorhanden";
            }

            try
            {
                using (FileStream stream = File.OpenRead(vollPfad))
                {
                    // Nur oeffnen+sofort schliessen, um "vorhanden, aber
                    // nicht lesbar" (z.B. Berechtigung, gesperrt) von
                    // echtem Fehlen zu unterscheiden. Kein Schreibzugriff.
                }

                return "Vorhanden";
            }
            catch
            {
                return "Datei vorhanden, aber nicht lesbar";
            }
        }

        private class FundortDiagnoseZeile
        {
            public Guid ErinnerungId;
            public ZuordnungsZielTyp ZielTyp;
            public string ZielBezeichnung;
            public string FundortDetails;
        }

        private string DiagnoseFehlendeFundorte()
        {
            LadeErinnerungsmodellFallsNoetig();

            int erinnerungenGesamt = erinnerungsmodellErinnerungen.Count;
            int erinnerungenMitFundort = 0;
            int erinnerungenOhneFundort = 0;

            Dictionary<Guid, bool> erinnerungHatVorhandenenFundort = new Dictionary<Guid, bool>();
            Dictionary<Guid, string> erinnerungFundortDetails = new Dictionary<Guid, string>();

            foreach (Erinnerung erinnerung in erinnerungsmodellErinnerungen)
            {
                List<string> details = new List<string>();
                bool hatVorhandenen = false;

                if (erinnerung.Fundorte != null)
                {
                    foreach (Fundort fundort in erinnerung.Fundorte)
                    {
                        string status = PruefeFundortStatus(fundort.Pfad);
                        details.Add((fundort.Pfad ?? "(leer)") + " -> " + status);

                        if (status == "Vorhanden")
                        {
                            hatVorhandenen = true;
                        }
                    }
                }
                else
                {
                    details.Add("(keine Fundorte gespeichert)");
                }

                erinnerungHatVorhandenenFundort[erinnerung.Id] = hatVorhandenen;
                erinnerungFundortDetails[erinnerung.Id] = string.Join("; ", details);

                if (hatVorhandenen)
                {
                    erinnerungenMitFundort++;
                }
                else
                {
                    erinnerungenOhneFundort++;
                }
            }

            int zuordnungenGesamt = erinnerungsmodellZuordnungen.Count;
            int zuordnungenOhneFundort = 0;

            Dictionary<ZuordnungsZielTyp, int> betroffenProZielTyp = new Dictionary<ZuordnungsZielTyp, int>
            {
                { ZuordnungsZielTyp.Person, 0 },
                { ZuordnungsZielTyp.Ereignis, 0 },
                { ZuordnungsZielTyp.Sammlung, 0 }
            };

            List<FundortDiagnoseZeile> problematisch = new List<FundortDiagnoseZeile>();

            foreach (Zuordnung zuordnung in erinnerungsmodellZuordnungen)
            {
                bool hatVorhandenen = erinnerungHatVorhandenenFundort.TryGetValue(zuordnung.ErinnerungId, out bool wert) && wert;

                if (hatVorhandenen)
                {
                    continue;
                }

                zuordnungenOhneFundort++;
                betroffenProZielTyp[zuordnung.ZielTyp]++;

                problematisch.Add(new FundortDiagnoseZeile
                {
                    ErinnerungId = zuordnung.ErinnerungId,
                    ZielTyp = zuordnung.ZielTyp,
                    ZielBezeichnung = zuordnung.ZielBezeichnung,
                    FundortDetails = erinnerungFundortDetails.TryGetValue(zuordnung.ErinnerungId, out string d) ? d : "(unbekannt)"
                });
            }

            // A's Zusatzfrage: treten dieselben fehlenden Fundorte
            // gleichzeitig bei mehreren Zieltypen derselben Erinnerung auf?
            List<Guid> erinnerungenMitMehrerenZieltypenBetroffen = problematisch
                .GroupBy(z => z.ErinnerungId)
                .Where(g => g.Select(z => z.ZielTyp).Distinct().Count() > 1)
                .Select(g => g.Key)
                .ToList();

            // Bericht als eigene, reine Text-Datei schreiben (nur
            // erzeugt/geschrieben - keine bestehende Datei wird
            // veraendert), damit die komplette Detailliste einsehbar
            // ist, auch wenn sie fuer eine einzelne Meldung zu lang waere.
            string berichtPfad = Path.Combine(OrdnerPfad, "diagnose_fehlende_fundorte_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt");

            System.Text.StringBuilder bericht = new System.Text.StringBuilder();
            bericht.AppendLine("DIAGNOSE: Fehlende Fundorte (rein lesend, " + DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss") + ")");
            bericht.AppendLine();
            bericht.AppendLine("Erinnerungen insgesamt: " + erinnerungenGesamt);
            bericht.AppendLine("Erinnerungen mit mindestens einem vorhandenen Fundort: " + erinnerungenMitFundort);
            bericht.AppendLine("Erinnerungen OHNE vorhandenen Fundort: " + erinnerungenOhneFundort);
            bericht.AppendLine();
            bericht.AppendLine("Zuordnungen insgesamt: " + zuordnungenGesamt);
            bericht.AppendLine("Zuordnungen, deren Erinnerung keinen vorhandenen Fundort mehr hat: " + zuordnungenOhneFundort);
            bericht.AppendLine("  davon Person: " + betroffenProZielTyp[ZuordnungsZielTyp.Person]);
            bericht.AppendLine("  davon Ereignis: " + betroffenProZielTyp[ZuordnungsZielTyp.Ereignis]);
            bericht.AppendLine("  davon Sammlung: " + betroffenProZielTyp[ZuordnungsZielTyp.Sammlung]);
            bericht.AppendLine();
            bericht.AppendLine("Erinnerungen, die GLEICHZEITIG bei mehreren Zieltypen betroffen sind: " + erinnerungenMitMehrerenZieltypenBetroffen.Count);
            bericht.AppendLine();
            bericht.AppendLine("=== EINZELFAELLE ===");

            foreach (FundortDiagnoseZeile zeile in problematisch.OrderBy(z => z.ErinnerungId))
            {
                bericht.AppendLine();
                bericht.AppendLine("ErinnerungId: " + zeile.ErinnerungId);
                bericht.AppendLine("Zuordnung: " + zeile.ZielTyp + " -> " + zeile.ZielBezeichnung);
                bericht.AppendLine("Fundort(e): " + zeile.FundortDetails);
            }

            try
            {
                File.WriteAllText(berichtPfad, bericht.ToString());
            }
            catch
            {
                // Bericht-Datei konnte nicht geschrieben werden - Diagnose
                // trotzdem als Kurzfassung zurueckgeben, nichts Kritisches.
            }

            return "Diagnose abgeschlossen (rein lesend, nichts verändert).\n\n" +
                erinnerungenGesamt + " Erinnerung(en) insgesamt, davon " + erinnerungenOhneFundort + " ohne vorhandenen Fundort.\n" +
                zuordnungenGesamt + " Zuordnung(en) insgesamt, davon " + zuordnungenOhneFundort + " betroffen " +
                "(Person: " + betroffenProZielTyp[ZuordnungsZielTyp.Person] + ", Ereignis: " + betroffenProZielTyp[ZuordnungsZielTyp.Ereignis] + ", Sammlung: " + betroffenProZielTyp[ZuordnungsZielTyp.Sammlung] + ").\n\n" +
                erinnerungenMitMehrerenZieltypenBetroffen.Count + " Erinnerung(en) sind gleichzeitig bei mehreren Zieltypen betroffen.\n\n" +
                "Vollständige Einzelfall-Liste gespeichert unter:\n" + berichtPfad;
        }

        private void DiagnoseFehlendeFundorte_Click(object sender, RoutedEventArgs e)
        {
            string ergebnis = DiagnoseFehlendeFundorte();
            James.Hinweis(ergebnis, "Diagnose: Fehlende Fundorte");
        }

        // ============================================================
        // A/Opa-DIAGNOSEAUFTRAG "ZUORDNUNGS-KETTE" (13.08.)
        // ============================================================
        // AUSDRUECKLICH REIN LESEND, KEIN FIX. Verfolgt fuer JEDES Ziel
        // (Person/Ereignis/Sammlung), zu dem mindestens eine Zuordnung
        // existiert, genau die von A vorgegebene Kette:
        //   1) im Datenbestand gespeicherte Zuordnungen (roh)
        //   2) davon eindeutige ErinnerungIds (Duplikate in den
        //      Zuordnungen selbst wuerden hier auffallen)
        //   3) davon tatsaechlich noch existierende Erinnerung-Objekte
        //      (verwaiste Zuordnungen auf geloeschte Erinnerungen wuerden
        //      hier auffallen)
        //   4) davon nach der Pfad-Deduplizierung von ErgaenzeUmNeuesModell
        //      uebrig bleibende (mehrere Erinnerungen mit demselben Fundort-
        //      Pfad wuerden sich hier gegenseitig verdraengen)
        //   5) davon tatsaechlich als Bild ladbar (ErinnerungenFenster.
        //      ZeigeUebersicht versucht JEDE Datei als Bild zu laden,
        //      unabhaengig vom MedienTyp - ein PDF/Dokument/Video wuerde
        //      hier durchfallen und in der Anzeige fehlen)
        // Meldet nur die Ziele, bei denen Schritt 1 != Schritt 5 ist -
        // genau dort verschwindet etwas zwischen Speicherung und Anzeige.
        private class ZuordnungsKettenBefund
        {
            public ZuordnungsZielTyp ZielTyp;
            public string ZielBezeichnung;
            public int SchrittGespeichert;
            public int SchrittEindeutigeIds;
            public int SchrittExistierendeErinnerungen;
            public int SchrittNachPfadDeduplizierung;
            public int SchrittTatsaechlichAlsBildLadbar;
            public List<string> Details = new List<string>();
        }

        private string DiagnoseZuordnungsKette()
        {
            LadeErinnerungsmodellFallsNoetig();

            // Alle Ziele ermitteln, zu denen mindestens eine Zuordnung
            // existiert - kein Raten, welche Sammlung/Person betroffen ist.
            List<(ZuordnungsZielTyp ZielTyp, Guid ZielId, string ZielBezeichnung)> alleZiele = erinnerungsmodellZuordnungen
                .Select(z => (z.ZielTyp, z.ZielId, z.ZielBezeichnung))
                .Distinct()
                .ToList();

            List<ZuordnungsKettenBefund> befunde = new List<ZuordnungsKettenBefund>();

            foreach ((ZuordnungsZielTyp zielTyp, Guid zielId, string zielBezeichnung) in alleZiele)
            {
                ZuordnungsKettenBefund befund = new ZuordnungsKettenBefund
                {
                    ZielTyp = zielTyp,
                    ZielBezeichnung = zielBezeichnung
                };

                // Schritt 1: roh gespeicherte Zuordnungen fuer dieses Ziel
                List<Zuordnung> zuordnungenFuerZiel = erinnerungsmodellZuordnungen
                    .Where(z => z.ZielTyp == zielTyp && z.ZielId == zielId)
                    .ToList();
                befund.SchrittGespeichert = zuordnungenFuerZiel.Count;

                // Schritt 2: eindeutige ErinnerungIds darunter
                List<Guid> eindeutigeIds = zuordnungenFuerZiel.Select(z => z.ErinnerungId).Distinct().ToList();
                befund.SchrittEindeutigeIds = eindeutigeIds.Count;

                if (eindeutigeIds.Count != zuordnungenFuerZiel.Count)
                {
                    befund.Details.Add("Es gibt mehrfache Zuordnungs-Datensätze zur selben Erinnerung (" + zuordnungenFuerZiel.Count + " Zuordnungen, aber nur " + eindeutigeIds.Count + " verschiedene Erinnerungen).");
                }

                // Schritt 3: davon tatsaechlich noch existierende Erinnerungen
                List<Erinnerung> existierendeErinnerungen = erinnerungsmodellErinnerungen
                    .Where(er => eindeutigeIds.Contains(er.Id))
                    .ToList();
                befund.SchrittExistierendeErinnerungen = existierendeErinnerungen.Count;

                if (existierendeErinnerungen.Count != eindeutigeIds.Count)
                {
                    int verwaist = eindeutigeIds.Count - existierendeErinnerungen.Count;
                    befund.Details.Add(verwaist + " Zuordnung(en) verweisen auf eine ErinnerungId, die im Erinnerungsbestand nicht mehr existiert (verwaiste Zuordnung).");
                }

                // Schritt 4: Pfad-Deduplizierung wie in ErgaenzeUmNeuesModell -
                // simuliert exakt dieselbe Logik (bekanntePfade-HashSet,
                // case-insensitive), aber rein lesend, nichts wird der
                // echten Anzeige hinzugefuegt.
                HashSet<string> bekanntePfadeSimuliert = new HashSet<string>();
                List<Erinnerung> nachPfadDeduplizierung = new List<Erinnerung>();
                Dictionary<string, List<Guid>> pfadKollisionen = new Dictionary<string, List<Guid>>();

                foreach (Erinnerung erinnerung in existierendeErinnerungen)
                {
                    string pfad = erinnerung.Fundorte?
                        .Select(f => f.Pfad)
                        .FirstOrDefault(p => !string.IsNullOrEmpty(p) && File.Exists(p));

                    if (pfad == null)
                    {
                        befund.Details.Add("Erinnerung " + erinnerung.Id + " hat keinen gueltigen, existierenden Fundort-Pfad (widerspricht der vorigen Fundort-Diagnose - bitte melden).");
                        continue;
                    }

                    string pfadKlein = pfad.ToLowerInvariant();

                    if (!pfadKollisionen.ContainsKey(pfadKlein))
                    {
                        pfadKollisionen[pfadKlein] = new List<Guid>();
                    }
                    pfadKollisionen[pfadKlein].Add(erinnerung.Id);

                    if (bekanntePfadeSimuliert.Contains(pfadKlein))
                    {
                        continue;
                    }

                    bekanntePfadeSimuliert.Add(pfadKlein);
                    nachPfadDeduplizierung.Add(erinnerung);
                }

                befund.SchrittNachPfadDeduplizierung = nachPfadDeduplizierung.Count;

                foreach (KeyValuePair<string, List<Guid>> kollision in pfadKollisionen.Where(k => k.Value.Count > 1))
                {
                    befund.Details.Add("Mehrere Erinnerungen zeigen auf denselben Fundort-Pfad (" + kollision.Key + "): " + string.Join(", ", kollision.Value) + " - nur die erste zaehlt in der Anzeige, der Rest wird durch die Pfad-Deduplizierung verdraengt.");
                }

                // Schritt 5: tatsaechliche Bild-Ladbarkeit wie in
                // ErinnerungenFenster.ZeigeUebersicht (versucht jede Datei
                // als Bild zu laden, unabhaengig vom MedienTyp).
                int alsBildLadbar = 0;

                foreach (Erinnerung erinnerung in nachPfadDeduplizierung)
                {
                    string pfad = erinnerung.Fundorte
                        .Select(f => f.Pfad)
                        .FirstOrDefault(p => !string.IsNullOrEmpty(p) && File.Exists(p));

                    try
                    {
                        BitmapImage testbild = new BitmapImage();
                        testbild.BeginInit();
                        testbild.CacheOption = BitmapCacheOption.OnLoad;
                        testbild.DecodePixelWidth = 200;
                        testbild.UriSource = new Uri(pfad);
                        testbild.EndInit();

                        alsBildLadbar++;
                    }
                    catch
                    {
                        befund.Details.Add("Erinnerung " + erinnerung.Id + " (MedienTyp: " + erinnerung.MedienTyp + ", Pfad: " + pfad + ") lässt sich NICHT als Bild laden - würde in \"Erinnerungen ansehen\" unsichtbar bleiben, egal was sonst stimmt.");
                    }
                }

                befund.SchrittTatsaechlichAlsBildLadbar = alsBildLadbar;

                befunde.Add(befund);
            }

            // Bericht schreiben - volle Liste aller Ziele, damit auch
            // unauffaellige Ziele zur Kontrolle einsehbar sind.
            string berichtPfad = Path.Combine(OrdnerPfad, "diagnose_zuordnungskette_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt");

            System.Text.StringBuilder bericht = new System.Text.StringBuilder();
            bericht.AppendLine("DIAGNOSE: Zuordnungs-Kette (rein lesend, " + DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss") + ")");
            bericht.AppendLine("Kette je Ziel: 1) gespeichert -> 2) eindeutige Erinnerungen -> 3) davon existierend -> 4) davon nach Pfad-Deduplizierung -> 5) davon tatsächlich als Bild ladbar (= das, was am Ende in \"Erinnerungen ansehen\" erscheinen würde)");
            bericht.AppendLine();

            List<ZuordnungsKettenBefund> mitAbweichung = befunde.Where(b => b.SchrittGespeichert != b.SchrittTatsaechlichAlsBildLadbar).ToList();

            // A/Opa-SCHUTZAUFTRAG (13.08.), Punkt 3: Gesamtzahl der BEREITS
            // BESTEHENDEN Doppelzuordnungen (nur zaehlen und berichten -
            // ausdruecklich KEINE automatische Bereinigung, siehe
            // Schutzregel in FuehreZuordnungDurch, die nur kuenftige neue
            // Duplikate verhindert, nicht rueckwirkend aufraeumt).
            int gesamtDoppelzuordnungen = befunde.Sum(b => b.SchrittGespeichert - b.SchrittEindeutigeIds);

            bericht.AppendLine(alleZiele.Count + " Ziel(e) insgesamt geprüft, " + mitAbweichung.Count + " mit Abweichung zwischen gespeichert und tatsächlich anzeigbar.");
            bericht.AppendLine("Insgesamt " + gesamtDoppelzuordnungen + " bestehende Doppelzuordnung(en) über alle Ziele hinweg (nur gezählt, NICHT bereinigt).");
            bericht.AppendLine();
            bericht.AppendLine("=== ZIELE MIT ABWEICHUNG ===");

            foreach (ZuordnungsKettenBefund b in mitAbweichung.OrderByDescending(x => x.SchrittGespeichert - x.SchrittTatsaechlichAlsBildLadbar))
            {
                bericht.AppendLine();
                bericht.AppendLine(b.ZielTyp + ": " + b.ZielBezeichnung);
                bericht.AppendLine("  1) gespeichert: " + b.SchrittGespeichert);
                bericht.AppendLine("  2) eindeutige Erinnerungen: " + b.SchrittEindeutigeIds);
                bericht.AppendLine("  3) davon existierend: " + b.SchrittExistierendeErinnerungen);
                bericht.AppendLine("  4) davon nach Pfad-Deduplizierung: " + b.SchrittNachPfadDeduplizierung);
                bericht.AppendLine("  5) davon tatsächlich als Bild ladbar: " + b.SchrittTatsaechlichAlsBildLadbar);

                foreach (string detail in b.Details)
                {
                    bericht.AppendLine("  - " + detail);
                }
            }

            bericht.AppendLine();
            bericht.AppendLine("=== ALLE ZIELE (zur Kontrolle) ===");

            foreach (ZuordnungsKettenBefund b in befunde.OrderBy(x => x.ZielTyp).ThenBy(x => x.ZielBezeichnung))
            {
                bericht.AppendLine(b.ZielTyp + ": " + b.ZielBezeichnung + " - gespeichert " + b.SchrittGespeichert + ", angezeigt " + b.SchrittTatsaechlichAlsBildLadbar);
            }

            try
            {
                File.WriteAllText(berichtPfad, bericht.ToString());
            }
            catch
            {
            }

            if (mitAbweichung.Count == 0)
            {
                return "Diagnose abgeschlossen (rein lesend, nichts verändert).\n\n" +
                    alleZiele.Count + " Ziel(e) geprüft - bei KEINEM gibt es eine Abweichung zwischen gespeicherten und tatsächlich anzeigbaren Zuordnungen.\n\n" +
                    "Vollständiger Bericht gespeichert unter:\n" + berichtPfad;
            }

            ZuordnungsKettenBefund groesste = mitAbweichung.OrderByDescending(b => b.SchrittGespeichert - b.SchrittTatsaechlichAlsBildLadbar).First();

            return "Diagnose abgeschlossen (rein lesend, nichts verändert).\n\n" +
                alleZiele.Count + " Ziel(e) geprüft, " + mitAbweichung.Count + " mit Abweichung.\n" +
                "Insgesamt " + gesamtDoppelzuordnungen + " bestehende Doppelzuordnung(en) über alle Ziele hinweg (nur gezählt, nicht bereinigt).\n\n" +
                "Größte Abweichung: " + groesste.ZielTyp + " \"" + groesste.ZielBezeichnung + "\" - " +
                groesste.SchrittGespeichert + " gespeichert, aber nur " + groesste.SchrittTatsaechlichAlsBildLadbar + " tatsächlich anzeigbar.\n\n" +
                "Vollständiger Bericht (alle betroffenen Ziele mit genauer Kette + Einzelfall-Details) gespeichert unter:\n" + berichtPfad;
        }

        private void DiagnoseZuordnungsKette_Click(object sender, RoutedEventArgs e)
        {
            string ergebnis = DiagnoseZuordnungsKette();
            James.Hinweis(ergebnis, "Diagnose: Zuordnungs-Kette");
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
