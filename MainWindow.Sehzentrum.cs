using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace DAS_LEBENSARCHIV
{
    public partial class MainWindow
    {
        // ============================================================
        // SPRINT C, ETAPPE 1a (04.08.): SEHZENTRUM-FUNDAMENT
        // ============================================================
        private const string SehzentrumModellversion = "clip-vision-v1";

        private static string SehzentrumOrdnerPfad => Path.Combine(OrdnerPfad, "Sehzentrum");

        private static string SehzentrumModellPfad => Path.Combine(SehzentrumOrdnerPfad, "Modell", "clip-vision.onnx");

        private static string SehgedaechtnisPfad => Path.Combine(SehzentrumOrdnerPfad, "sehgedaechtnis.json");

        private List<SehgedaechtnisEintrag> LadeSehgedaechtnis()
        {
            List<SehgedaechtnisEintrag> eintraege;

            try
            {
                if (File.Exists(SehgedaechtnisPfad))
                {
                    string json = File.ReadAllText(SehgedaechtnisPfad);
                    List<SehgedaechtnisEintrag> geladen = JsonSerializer.Deserialize<List<SehgedaechtnisEintrag>>(json);
                    eintraege = geladen ?? new List<SehgedaechtnisEintrag>();
                }
                else
                {
                    eintraege = new List<SehgedaechtnisEintrag>();
                }
            }
            catch
            {
                // Sehgedächtnis nicht lesbar - dann eben mit einem leeren
                // Gedächtnis neu beginnen, statt James zum Absturz zu bringen.
                eintraege = new List<SehgedaechtnisEintrag>();
            }

            if (MigriereSehgedaechtnis(eintraege))
            {
                SpeichereSehgedaechtnis(eintraege);
            }

            return eintraege;
        }

        // Baukasten-Umbau (05.08., A's Architekturentscheidung, Punkt 14):
        // übernimmt Testdaten aus der Zeit vor dem Baukastenprinzip
        // (einzelne BestaetigteKategorie / JamesVermutungKategorie) sauber
        // ins neue Listenmodell und leert danach die alten Felder. Läuft
        // automatisch beim ersten Laden nach dem Update, unsichtbar für
        // den Benutzer. Gibt true zurück, wenn etwas migriert wurde (dann
        // muss gespeichert werden).
        private bool MigriereSehgedaechtnis(List<SehgedaechtnisEintrag> eintraege)
        {
            bool geaendert = false;

            foreach (SehgedaechtnisEintrag eintrag in eintraege)
            {
                if (eintrag.BestaetigteStichwoerter == null)
                {
                    eintrag.BestaetigteStichwoerter = new List<string>();
                }

                if (eintrag.BestaetigtNichtVorhanden == null)
                {
                    eintrag.BestaetigtNichtVorhanden = new List<string>();
                }

                if (eintrag.JamesVermutungen == null)
                {
                    eintrag.JamesVermutungen = new List<VermuteterBegriff>();
                }

                if (!string.IsNullOrWhiteSpace(eintrag.BestaetigteKategorie)
                    && !eintrag.BestaetigteStichwoerter.Contains(eintrag.BestaetigteKategorie))
                {
                    eintrag.BestaetigteStichwoerter.Add(eintrag.BestaetigteKategorie);
                    eintrag.BestaetigteKategorie = null;
                    geaendert = true;
                }

                if (!string.IsNullOrWhiteSpace(eintrag.JamesVermutungKategorie)
                    && eintrag.JamesVermutungKategorie != "Unbekannt"
                    && !eintrag.JamesVermutungen.Any(v => v.Begriff == eintrag.JamesVermutungKategorie))
                {
                    eintrag.JamesVermutungen.Add(new VermuteterBegriff
                    {
                        Begriff = eintrag.JamesVermutungKategorie,
                        SicherheitProzent = eintrag.JamesVermutungSicherheit
                    });

                    eintrag.JamesVermutungKategorie = null;
                    eintrag.JamesVermutungSicherheit = 0;
                    geaendert = true;
                }
            }

            return geaendert;
        }

        private void SpeichereSehgedaechtnis(List<SehgedaechtnisEintrag> eintraege)
        {
            Directory.CreateDirectory(SehzentrumOrdnerPfad);

            JsonSerializerOptions optionen = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            string json = JsonSerializer.Serialize(eintraege, optionen);
            File.WriteAllText(SehgedaechtnisPfad, json);
        }

        private float[] BerechneBildEinbettung(string bildPfad)
        {
            if (!File.Exists(SehzentrumModellPfad))
            {
                throw new InvalidOperationException(
                    "Das Sehzentrum-Modell wurde noch nicht heruntergeladen. Erwarteter Ort:\n" + SehzentrumModellPfad);
            }

            float[] eingabeDaten = LadeBildAlsEingabeTensor(bildPfad, out int breite, out int hoehe);

            using InferenceSession sitzung = new InferenceSession(SehzentrumModellPfad);

            DenseTensor<float> eingabeTensor = new DenseTensor<float>(eingabeDaten, new[] { 1, 3, hoehe, breite });

            string eingabeName = sitzung.InputMetadata.Keys.First();

            List<NamedOnnxValue> eingaben = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(eingabeName, eingabeTensor)
            };

            using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> ergebnisse = sitzung.Run(eingaben);

            return ergebnisse.First().AsEnumerable<float>().ToArray();
        }

        private float[] LadeBildAlsEingabeTensor(string bildPfad, out int breite, out int hoehe)
        {
            breite = 224;
            hoehe = 224;

            BitmapImage bild = new BitmapImage();
            bild.BeginInit();
            bild.UriSource = new Uri(bildPfad);
            bild.DecodePixelWidth = breite;
            bild.DecodePixelHeight = hoehe;
            bild.CacheOption = BitmapCacheOption.OnLoad;
            bild.EndInit();
            bild.Freeze();

            FormatConvertedBitmap umgewandelt = new FormatConvertedBitmap(bild, PixelFormats.Rgb24, null, 0);

            int stride = breite * 3;
            byte[] pixel = new byte[hoehe * stride];
            umgewandelt.CopyPixels(pixel, stride, 0);

            float[] mittelwert = { 0.48145466f, 0.4578275f, 0.40821073f };
            float[] streuung = { 0.26862954f, 0.26130258f, 0.27577711f };

            float[] tensor = new float[3 * hoehe * breite];

            for (int y = 0; y < hoehe; y++)
            {
                for (int x = 0; x < breite; x++)
                {
                    int pixelIndex = y * stride + x * 3;

                    for (int kanal = 0; kanal < 3; kanal++)
                    {
                        float wert = pixel[pixelIndex + kanal] / 255f;
                        wert = (wert - mittelwert[kanal]) / streuung[kanal];

                        tensor[kanal * hoehe * breite + y * breite + x] = wert;
                    }
                }
            }

            return tensor;
        }

        // Zeigt das gerade analysierte Bild groß in Werkzeuge an, solange
        // James daran arbeitet - reiner Komfort, beeinträchtigt keine
        // anderen Bereiche, da der Rahmen nur bei Bedarf sichtbar wird
        // (Visibility="Collapsed" im Ruhezustand). Wenn die Analyse über
        // die Arbeitsmappe angestoßen wurde, ist Werkzeuge evtl. gar nicht
        // sichtbar - das ist unschädlich, die Vorschau steht trotzdem
        // bereit, sobald man dorthin wechselt.
        private void ZeigeSehzentrumBildVorschau(string bildPfad)
        {
            try
            {
                BitmapImage bild = new BitmapImage();
                bild.BeginInit();
                bild.CacheOption = BitmapCacheOption.OnLoad;
                bild.UriSource = new Uri(bildPfad);
                bild.EndInit();

                SehzentrumBildVorschauBild.Source = bild;
                SehzentrumBildVorschauBorder.Visibility = Visibility.Visible;
            }
            catch
            {
                // Vorschau ist nur ein Komfort - falls sie aus irgendeinem
                // Grund nicht angezeigt werden kann, läuft die eigentliche
                // Analyse trotzdem ungestört weiter.
            }
        }

        private void VerstecktSehzentrumBildVorschau()
        {
            SehzentrumBildVorschauBorder.Visibility = Visibility.Collapsed;
            SehzentrumBildVorschauBild.Source = null;
        }

        private void SehzentrumTesten_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Testbild für das Sehzentrum wählen",
                Filter = "Bilder (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp|Alle Dateien (*.*)|*.*"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            ZeigeSehzentrumBildVorschau(dialog.FileName);

            try
            {
                string hashwert = BerechneHashwert(dialog.FileName);

                List<SehgedaechtnisEintrag> sehgedaechtnis = LadeSehgedaechtnis();
                SehgedaechtnisEintrag vorhanden = sehgedaechtnis.FirstOrDefault(x => x.Hashwert == hashwert && x.Modellversion == SehzentrumModellversion);

                if (vorhanden != null)
                {
                    James.Hinweis("Dieses Bild kennt James bereits (analysiert am " + vorhanden.AnalysiertAm.ToString("dd.MM.yyyy HH:mm") +
                        ") - keine erneute Analyse nötig. Einbettung hat " + vorhanden.BildEinbettung.Length + " Werte.");
                    return;
                }

                float[] einbettung = BerechneBildEinbettung(dialog.FileName);

                sehgedaechtnis.Add(new SehgedaechtnisEintrag
                {
                    Hashwert = hashwert,
                    BildEinbettung = einbettung,
                    AnalysiertAm = DateTime.Now,
                    Modellversion = SehzentrumModellversion
                });

                SpeichereSehgedaechtnis(sehgedaechtnis);

                string beispielWerte = string.Join(", ", einbettung.Take(5).Select(w => w.ToString("F3")));

                James.Hinweis("Erfolgreich analysiert und im Sehgedächtnis gespeichert.\n\n" +
                    "Einbettung hat " + einbettung.Length + " Werte, die ersten 5 davon: " + beispielWerte);
            }
            catch (Exception ex)
            {
                James.Problem("Das Sehzentrum konnte das Bild nicht verarbeiten: " + ex.Message);
            }
            finally
            {
                VerstecktSehzentrumBildVorschau();
            }
        }

        // ============================================================
        // SPRINT C, ETAPPE 1b-BAUKASTEN (05.08.): MULTI-LABEL-STICHWÖRTER
        // ============================================================
        // Architekturentscheidung A: "1 Bild = viele kleine beschreibende
        // Bausteine" statt "1 Bild = 1 Schublade". Ein Bild kann beliebig
        // viele Stichwörter bekommen (Mensch, Hund, Garten, ...). James
        // lernt weiterhin an Beispielen (kein Text-CLIP, kein Tokenizer
        // nötig): pro Stichwort wird der Durchschnitt aller bestätigten
        // Beispiel-Einbettungen mit dem neuen Bild verglichen.

        private static readonly string[] SehzentrumBasisstichwoerter =
        {
            "Mensch", "Hund", "Katze", "Gebäude", "Auto", "Berg", "See", "Blume"
        };

        // Unterhalb dieser Kosinus-Ähnlichkeit schlägt James ein Stichwort
        // nicht automatisch als vorausgewählt vor - es bleibt aber in der
        // Liste sichtbar, falls der Benutzer es trotzdem ankreuzen möchte.
        private const float SehzentrumMindestSicherheit = 0.75f;

        private static string KategorieReferenzenPfad => Path.Combine(SehzentrumOrdnerPfad, "kategorien.json");

        private static string WoerterbuchPfad => Path.Combine(SehzentrumOrdnerPfad, "woerterbuch.json");

        private List<KategorieReferenz> LadeKategorieReferenzen()
        {
            try
            {
                if (File.Exists(KategorieReferenzenPfad))
                {
                    string json = File.ReadAllText(KategorieReferenzenPfad);
                    List<KategorieReferenz> geladen = JsonSerializer.Deserialize<List<KategorieReferenz>>(json);

                    if (geladen != null)
                    {
                        return geladen;
                    }
                }
            }
            catch
            {
            }

            return new List<KategorieReferenz>();
        }

        private void SpeichereKategorieReferenzen(List<KategorieReferenz> referenzen)
        {
            Directory.CreateDirectory(SehzentrumOrdnerPfad);

            JsonSerializerOptions optionen = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            string json = JsonSerializer.Serialize(referenzen, optionen);
            File.WriteAllText(KategorieReferenzenPfad, json);
        }

        // Persönliches Bild-Wörterbuch (A's Punkt 12): zentrale Liste
        // aller bekannten Stichwörter, beginnt mit den Basisstichwörtern
        // und wächst automatisch mit jedem neuen, vom Benutzer bestätigten
        // Begriff. Liegt unter H:\...\BUTLER JAMES\Sehzentrum, nie unter
        // AppData.
        private List<string> LadeWoerterbuch()
        {
            List<string> woerterbuch;

            try
            {
                if (File.Exists(WoerterbuchPfad))
                {
                    string json = File.ReadAllText(WoerterbuchPfad);
                    List<string> geladen = JsonSerializer.Deserialize<List<string>>(json);
                    woerterbuch = geladen ?? new List<string>();
                }
                else
                {
                    woerterbuch = new List<string>();
                }
            }
            catch
            {
                woerterbuch = new List<string>();
            }

            bool geaendert = false;

            foreach (string basisbegriff in SehzentrumBasisstichwoerter)
            {
                if (!woerterbuch.Any(w => string.Equals(w, basisbegriff, StringComparison.OrdinalIgnoreCase)))
                {
                    woerterbuch.Add(basisbegriff);
                    geaendert = true;
                }
            }

            if (geaendert)
            {
                woerterbuch = woerterbuch.OrderBy(w => w).ToList();
                SpeichereWoerterbuch(woerterbuch);
            }

            return woerterbuch;
        }

        private void SpeichereWoerterbuch(List<string> woerterbuch)
        {
            Directory.CreateDirectory(SehzentrumOrdnerPfad);

            JsonSerializerOptions optionen = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            string json = JsonSerializer.Serialize(woerterbuch.OrderBy(w => w).ToList(), optionen);
            File.WriteAllText(WoerterbuchPfad, json);
        }

        private static float KosinusAehnlichkeit(float[] a, float[] b)
        {
            float punktprodukt = 0f;
            float normA = 0f;
            float normB = 0f;

            for (int i = 0; i < a.Length; i++)
            {
                punktprodukt += a[i] * b[i];
                normA += a[i] * a[i];
                normB += b[i] * b[i];
            }

            if (normA == 0 || normB == 0)
            {
                return 0f;
            }

            return punktprodukt / (float)(Math.Sqrt(normA) * Math.Sqrt(normB));
        }

        private static float[] Durchschnittseinbettung(List<float[]> beispiele, int laenge)
        {
            float[] durchschnitt = new float[laenge];

            foreach (float[] beispiel in beispiele)
            {
                for (int i = 0; i < laenge; i++)
                {
                    durchschnitt[i] += beispiel[i];
                }
            }

            for (int i = 0; i < laenge; i++)
            {
                durchschnitt[i] /= beispiele.Count;
            }

            return durchschnitt;
        }

        // Vergleicht eine Bild-Einbettung mit dem Durchschnitt aller
        // bestätigten Beispiele jedes bekannten Stichworts. Liefert alle
        // Stichwörter mit Ähnlichkeit > 0, absteigend sortiert - nicht nur
        // das eine "beste" wie vor dem Baukasten-Umbau, da ein Bild ja
        // mehrere Stichwörter gleichzeitig haben kann.
        private List<VermuteterBegriff> ErmittleVermutungen(float[] einbettung, List<KategorieReferenz> referenzen)
        {
            List<VermuteterBegriff> ergebnis = new List<VermuteterBegriff>();

            foreach (KategorieReferenz referenz in referenzen)
            {
                if (referenz.BestaetigteEinbettungen == null || referenz.BestaetigteEinbettungen.Count == 0)
                {
                    continue;
                }

                float[] durchschnitt = Durchschnittseinbettung(referenz.BestaetigteEinbettungen, einbettung.Length);
                float aehnlichkeit = KosinusAehnlichkeit(einbettung, durchschnitt);

                if (aehnlichkeit <= 0)
                {
                    continue;
                }

                ergebnis.Add(new VermuteterBegriff
                {
                    Begriff = referenz.Kategorie,
                    SicherheitProzent = (int)Math.Round(aehnlichkeit * 100)
                });
            }

            return ergebnis.OrderByDescending(v => v.SicherheitProzent).ToList();
        }

        private void BestaetigeStichwort(List<KategorieReferenz> referenzen, string stichwort, float[] einbettung)
        {
            KategorieReferenz referenz = referenzen.FirstOrDefault(x => string.Equals(x.Kategorie, stichwort, StringComparison.OrdinalIgnoreCase));

            if (referenz == null)
            {
                referenz = new KategorieReferenz { Kategorie = stichwort };
                referenzen.Add(referenz);
            }

            referenz.BestaetigteEinbettungen.Add(einbettung);
        }

        // Mehrfachauswahl-Fenster (A's Punkt 4), komplett im Code aufgebaut
        // (keine eigene XAML-Datei nötig): zeigt alle bekannten Stichwörter
        // als Häkchen an (mit James' Vermutungsprozent, falls vorhanden),
        // vorausgewählt sind bereits bestätigte Stichwörter sowie sichere
        // neue Vermutungen. Zusätzlich ein freies Eingabefeld für neue
        // Begriffe (kommagetrennt) - "wie das Beschriften eines Fotos mit
        // kleinen Merkzetteln".
        private List<string> SehzentrumStichwoerterAuswaehlen(List<string> woerterbuch, List<VermuteterBegriff> vermutungen, List<string> bereitsBestaetigt)
        {
            Window fenster = new Window
            {
                Title = "Welche Stichwörter passen zu diesem Bild?",
                Width = 340,
                Height = 480,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize
            };

            DockPanel wurzel = new DockPanel { Margin = new Thickness(12) };

            TextBlock ueberschrift = new TextBlock
            {
                Text = "Häkchen setzen für alles, was auf dem Bild zu sehen ist:",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            };
            DockPanel.SetDock(ueberschrift, Dock.Top);
            wurzel.Children.Add(ueberschrift);

            StackPanel untererBereich = new StackPanel();
            DockPanel.SetDock(untererBereich, Dock.Bottom);

            TextBlock freitextLabel = new TextBlock
            {
                Text = "Weitere Begriffe (mit Komma getrennt):",
                Margin = new Thickness(0, 10, 0, 4)
            };
            untererBereich.Children.Add(freitextLabel);

            TextBox freitextBox = new TextBox { Padding = new Thickness(4) };
            untererBereich.Children.Add(freitextBox);

            Button okButton = new Button
            {
                Content = "Bestätigen",
                Margin = new Thickness(0, 10, 0, 0),
                Padding = new Thickness(0, 4, 0, 4),
                IsDefault = true
            };
            untererBereich.Children.Add(okButton);

            wurzel.Children.Add(untererBereich);

            ScrollViewer scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            StackPanel checkboxPanel = new StackPanel();

            List<string> anzeigeListe = woerterbuch
                .OrderByDescending(w => vermutungen.Any(v => string.Equals(v.Begriff, w, StringComparison.OrdinalIgnoreCase)))
                .ThenBy(w => w)
                .ToList();

            List<CheckBox> checkboxen = new List<CheckBox>();

            foreach (string begriff in anzeigeListe)
            {
                VermuteterBegriff vermutung = vermutungen.FirstOrDefault(v => string.Equals(v.Begriff, begriff, StringComparison.OrdinalIgnoreCase));

                string beschriftung = vermutung != null
                    ? begriff + " (" + vermutung.SicherheitProzent + "%)"
                    : begriff;

                CheckBox checkbox = new CheckBox
                {
                    Content = beschriftung,
                    Tag = begriff,
                    Margin = new Thickness(2, 3, 2, 3)
                };

                bool schonBestaetigt = bereitsBestaetigt.Any(b => string.Equals(b, begriff, StringComparison.OrdinalIgnoreCase));
                bool sichereVermutung = vermutung != null && vermutung.SicherheitProzent >= (int)(SehzentrumMindestSicherheit * 100);

                checkbox.IsChecked = schonBestaetigt || sichereVermutung;

                checkboxen.Add(checkbox);
                checkboxPanel.Children.Add(checkbox);
            }

            scroll.Content = checkboxPanel;
            wurzel.Children.Add(scroll);

            fenster.Content = wurzel;

            List<string> ergebnis = new List<string>();

            okButton.Click += (s, e) =>
            {
                ergebnis.Clear();

                foreach (CheckBox checkbox in checkboxen)
                {
                    if (checkbox.IsChecked == true)
                    {
                        ergebnis.Add((string)checkbox.Tag);
                    }
                }

                if (!string.IsNullOrWhiteSpace(freitextBox.Text))
                {
                    // Bewusst auch bei Leerzeichen aufteilen (nicht nur
                    // Komma/Semikolon): "baum winter" soll als zwei
                    // einzelne Begriffe erkannt werden, nicht als ein neuer
                    // zusammengesetzter Begriff "baum winter" - sonst wird
                    // das Wörterbuch schnell unübersichtlich.
                    foreach (string teil in freitextBox.Text.Split(new[] { ',', ';', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        string bereinigt = teil.Trim();

                        if (bereinigt.Length > 0 && !ergebnis.Any(x => string.Equals(x, bereinigt, StringComparison.OrdinalIgnoreCase)))
                        {
                            ergebnis.Add(bereinigt);
                        }
                    }
                }

                fenster.DialogResult = true;
            };

            fenster.ShowDialog();

            return ergebnis;
        }

        // Testfunktion für das Baukasten-Modell (weiterhin über den
        // bestehenden Button "Kategorie testen..." erreichbar - Klick-
        // Handler-Name bewusst unverändert, damit an der XAML nichts
        // angepasst werden muss): Bild wählen, Einbettung berechnen (oder
        // aus dem Sehgedächtnis wiederverwenden), Stichwörter vermuten,
        // Benutzer wählt per Mehrfachauswahl die passenden Stichwörter
        // (inkl. freier Ergänzung), Ergebnis dauerhaft merken - sowohl im
        // Sehgedächtnis des Bildes als auch im persönlichen Wörterbuch und
        // in den Stichwort-Referenzen (fürs Lernen).
        private void SehzentrumKategorieTesten_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Bild zur Stichwort-Zuordnung wählen",
                Filter = "Bilder (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp|Alle Dateien (*.*)|*.*"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            SehzentrumBildKategorisieren(dialog.FileName);
        }

        // Kernlogik ausgelagert (05.08.), damit sie sowohl vom
        // Werkzeuge-Button (ein einzelnes, per Dialog gewähltes Bild) als
        // auch von der Arbeitsmappe aus (mehrere markierte Bilder
        // nacheinander) genutzt werden kann - siehe
        // ArbeitsmappeJamesLernt_Click in MainWindow.Arbeitsmappe.cs.
        private void SehzentrumBildKategorisieren(string bildPfad)
        {
            ZeigeSehzentrumBildVorschau(bildPfad);

            try
            {
                string hashwert = BerechneHashwert(bildPfad);

                List<SehgedaechtnisEintrag> sehgedaechtnis = LadeSehgedaechtnis();
                SehgedaechtnisEintrag eintrag = sehgedaechtnis.FirstOrDefault(x => x.Hashwert == hashwert && x.Modellversion == SehzentrumModellversion);

                float[] einbettung;

                if (eintrag != null)
                {
                    einbettung = eintrag.BildEinbettung;
                }
                else
                {
                    einbettung = BerechneBildEinbettung(bildPfad);

                    eintrag = new SehgedaechtnisEintrag
                    {
                        Hashwert = hashwert,
                        BildEinbettung = einbettung,
                        AnalysiertAm = DateTime.Now,
                        Modellversion = SehzentrumModellversion
                    };

                    sehgedaechtnis.Add(eintrag);
                }

                List<KategorieReferenz> referenzen = LadeKategorieReferenzen();
                List<string> woerterbuch = LadeWoerterbuch();

                List<VermuteterBegriff> vermutungen = ErmittleVermutungen(einbettung, referenzen);

                eintrag.JamesVermutungen = vermutungen;
                SpeichereSehgedaechtnis(sehgedaechtnis);

                // Bestätigungsschleife (05.08., Wunsch des Nutzers): vor dem
                // tatsächlichen Speichern zeigt James die gewählten
                // Stichwörter zur Kontrolle. Bei "Nein" geht's zurück zum
                // Auswahlfenster (mit der bisherigen Auswahl vorausgewählt,
                // nichts geht verloren) statt sofort etwas Falsches zu
                // lernen.
                List<string> vorauswahl = eintrag.BestaetigteStichwoerter;
                List<string> gewaehlteStichwoerter;

                while (true)
                {
                    gewaehlteStichwoerter = SehzentrumStichwoerterAuswaehlen(woerterbuch, vermutungen, vorauswahl);

                    if (gewaehlteStichwoerter.Count == 0)
                    {
                        James.Hinweis("Keine Stichwörter ausgewählt - James hat sich trotzdem gemerkt, welches Bild das war, aber noch nichts bestätigt.");
                        return;
                    }

                    MessageBoxResult bestaetigung = MessageBox.Show(
                        "James merkt sich für dieses Bild:\n\n" + string.Join(", ", gewaehlteStichwoerter) + "\n\nPasst das so?",
                        "Bitte kurz bestätigen",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (bestaetigung == MessageBoxResult.Yes)
                    {
                        break;
                    }

                    // "Nein" gewählt - zurück zur Auswahl, diesmal mit der
                    // gerade getroffenen (falschen/unvollständigen) Auswahl
                    // vorausgewählt, damit nur noch korrigiert werden muss.
                    vorauswahl = gewaehlteStichwoerter;
                }

                eintrag.BestaetigteStichwoerter = gewaehlteStichwoerter;

                bool woerterbuchGeaendert = false;

                foreach (string stichwort in gewaehlteStichwoerter)
                {
                    BestaetigeStichwort(referenzen, stichwort, einbettung);

                    if (!woerterbuch.Any(w => string.Equals(w, stichwort, StringComparison.OrdinalIgnoreCase)))
                    {
                        woerterbuch.Add(stichwort);
                        woerterbuchGeaendert = true;
                    }
                }

                SpeichereKategorieReferenzen(referenzen);
                SpeichereSehgedaechtnis(sehgedaechtnis);

                if (woerterbuchGeaendert)
                {
                    SpeichereWoerterbuch(woerterbuch);
                }

                James.Hinweis("Danke - James merkt sich für dieses Bild: " + string.Join(", ", gewaehlteStichwoerter) + ".");
            }
            catch (Exception ex)
            {
                James.Problem("Das Sehzentrum konnte das Bild nicht verarbeiten: " + ex.Message);
            }
            finally
            {
                VerstecktSehzentrumBildVorschau();
            }
        }
    }
}
