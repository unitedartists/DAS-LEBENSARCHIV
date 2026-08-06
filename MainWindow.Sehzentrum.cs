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


        // ============================================================
        // OPTIMIERUNGSRUNDE (06.08.): STAPELFÄHIGE ERKENNUNG
        // ============================================================
        // A's wichtigster Punkt: "James merkt sich..." wird zu "James
        // erkennt auf diesem Bild/diesen Bildern...". EIN Codeweg für
        // 1 bis N Bilder gleichzeitig - kein Bild wird mehr einzeln
        // nacheinander abgefragt. Für jedes bekannte Stichwort wird
        // gezählt, bei wie vielen der ausgewählten Bilder es zutrifft
        // ("Traktor: 4 von 4"); nur bei kompletter Übereinstimmung ist
        // das Häkchen vorausgewählt. Ein unsicherer Begriff wird NIE
        // automatisch auf alle Bilder übertragen - der Benutzer
        // entscheidet das bewusst. Die Auswahl wird bei jedem Aufruf
        // komplett neu aus den tatsächlichen Daten DIESES Aufrufs
        // aufgebaut (nichts von einer vorherigen Bearbeitung bleibt
        // hängen).

        private class SehzentrumBildKontext
        {
            public string BildPfad;
            public SehgedaechtnisEintrag Eintrag;
            public float[] Einbettung;
            public List<VermuteterBegriff> Vermutungen;
        }

        // Bereitet 1..N Bilder auf (Einbettung berechnen/wiederverwenden,
        // Vermutungen ermitteln), zeigt die Stapel-Auswahl und speichert
        // am Ende das gemeinsam bestätigte Wissen für jedes Bild einzeln -
        // ohne dabei bereits vorhandene, individuelle Bestätigungen der
        // Bilder zu löschen.
        private void SehzentrumStapelErkennen(List<string> bildPfade)
        {
            if (bildPfade == null || bildPfade.Count == 0)
            {
                return;
            }

            if (bildPfade.Count == 1)
            {
                ZeigeSehzentrumBildVorschau(bildPfade[0]);
            }

            try
            {
                List<SehgedaechtnisEintrag> sehgedaechtnis = LadeSehgedaechtnis();
                List<KategorieReferenz> referenzen = LadeKategorieReferenzen();
                List<string> woerterbuch = LadeWoerterbuch();

                List<SehzentrumBildKontext> kontexte = new List<SehzentrumBildKontext>();

                foreach (string bildPfad in bildPfade)
                {
                    try
                    {
                        string hashwert = BerechneHashwert(bildPfad);
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

                        if (eintrag.BestaetigteStichwoerter == null)
                        {
                            eintrag.BestaetigteStichwoerter = new List<string>();
                        }

                        List<VermuteterBegriff> vermutungen = ErmittleVermutungen(einbettung, referenzen);
                        eintrag.JamesVermutungen = vermutungen;

                        kontexte.Add(new SehzentrumBildKontext
                        {
                            BildPfad = bildPfad,
                            Eintrag = eintrag,
                            Einbettung = einbettung,
                            Vermutungen = vermutungen
                        });
                    }
                    catch (Exception ex)
                    {
                        James.Problem("Bild konnte nicht analysiert werden (" + Path.GetFileName(bildPfad) + "): " + ex.Message);
                    }
                }

                if (kontexte.Count == 0)
                {
                    return;
                }

                SpeichereSehgedaechtnis(sehgedaechtnis);

                // Bestätigungsschleife wie bisher: bei "Nein" zurück zur
                // Auswahl statt sofort etwas Falsches zu lernen.
                List<string> vorauswahl = new List<string>();
                List<string> gemeinsamBestaetigt;

                while (true)
                {
                    gemeinsamBestaetigt = SehzentrumStapelAuswaehlen(woerterbuch, kontexte, vorauswahl);

                    if (gemeinsamBestaetigt.Count == 0)
                    {
                        James.Hinweis("Keine Stichwörter bestätigt - James hat sich die Bilder trotzdem gemerkt, aber noch nichts gespeichert.");
                        return;
                    }

                    string zusammenfassung = kontexte.Count == 1
                        ? "James merkt sich für dieses Bild:\n\n" + string.Join(", ", gemeinsamBestaetigt)
                        : "James merkt sich für alle " + kontexte.Count + " ausgewählten Bilder gemeinsam:\n\n" + string.Join(", ", gemeinsamBestaetigt);

                    MessageBoxResult bestaetigung = MessageBox.Show(
                        zusammenfassung + "\n\nPasst das so?",
                        "Bitte kurz bestätigen",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (bestaetigung == MessageBoxResult.Yes)
                    {
                        break;
                    }

                    vorauswahl = gemeinsamBestaetigt;
                }

                bool woerterbuchGeaendert = false;

                foreach (SehzentrumBildKontext kontext in kontexte)
                {
                    foreach (string begriff in gemeinsamBestaetigt)
                    {
                        if (!kontext.Eintrag.BestaetigteStichwoerter.Any(x => string.Equals(x, begriff, StringComparison.OrdinalIgnoreCase)))
                        {
                            kontext.Eintrag.BestaetigteStichwoerter.Add(begriff);
                        }

                        BestaetigeStichwort(referenzen, begriff, kontext.Einbettung);
                    }
                }

                foreach (string begriff in gemeinsamBestaetigt)
                {
                    if (!woerterbuch.Any(w => string.Equals(w, begriff, StringComparison.OrdinalIgnoreCase)))
                    {
                        woerterbuch.Add(begriff);
                        woerterbuchGeaendert = true;
                    }
                }

                SpeichereKategorieReferenzen(referenzen);
                SpeichereSehgedaechtnis(sehgedaechtnis);

                if (woerterbuchGeaendert)
                {
                    SpeichereWoerterbuch(woerterbuch);
                }

                James.Hinweis(kontexte.Count == 1
                    ? "Danke - James merkt sich für dieses Bild: " + string.Join(", ", gemeinsamBestaetigt) + "."
                    : "Danke - James merkt sich für alle " + kontexte.Count + " Bilder: " + string.Join(", ", gemeinsamBestaetigt) + ".");
            }
            finally
            {
                if (bildPfade.Count == 1)
                {
                    VerstecktSehzentrumBildVorschau();
                }
            }
        }

        // Mehrfachauswahl-Fenster, stapelfähig: bei mehreren Bildern zeigt
        // jedes bekannte Stichwort seine Trefferzahl ("4 von 4"). Nur bei
        // vollständiger Übereinstimmung ist es vorausgewählt - ein
        // Begriff, der nur auf einem Teil der Bilder zutrifft, wird NIE
        // automatisch für alle übernommen, kann aber bewusst angehakt
        // werden ("Sammelbestätigung"). Wörterbuch/Liste ist rein
        // alphabetisch sortiert. Neue Begriffe im Freitextfeld brauchen
        // bei mehreren Bildern eine eigene Sammelbestätigung.
        private List<string> SehzentrumStapelAuswaehlen(List<string> woerterbuch, List<SehzentrumBildKontext> kontexte, List<string> vorausgewaehlteBegriffe)
        {
            int anzahlBilder = kontexte.Count;
            int mindestSicherheitProzent = (int)(SehzentrumMindestSicherheit * 100);

            // Begriffsliste für diesen Aufruf komplett frisch aufbauen -
            // Wörterbuch plus alle aktuellen Vermutungen dieser Bilder,
            // rein alphabetisch, nichts aus einer vorherigen Bearbeitung.
            HashSet<string> alleBegriffe = new HashSet<string>(woerterbuch, StringComparer.OrdinalIgnoreCase);

            foreach (SehzentrumBildKontext kontext in kontexte)
            {
                foreach (VermuteterBegriff vermutung in kontext.Vermutungen)
                {
                    alleBegriffe.Add(vermutung.Begriff);
                }
            }

            List<string> begriffsListe = alleBegriffe.OrderBy(b => b, StringComparer.OrdinalIgnoreCase).ToList();

            Window fenster = new Window
            {
                Title = anzahlBilder == 1
                    ? "Was erkennt James auf diesem Bild?"
                    : "Was erkennt James auf diesen " + anzahlBilder + " Bildern?",
                Width = 360,
                Height = 500,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize
            };

            DockPanel wurzel = new DockPanel { Margin = new Thickness(12) };

            TextBlock ueberschrift = new TextBlock
            {
                Text = anzahlBilder == 1
                    ? "Häkchen setzen für alles, was auf dem Bild zu sehen ist:"
                    : "Häkchen setzen für alles, was auf ALLEN " + anzahlBilder + " Bildern zu sehen ist (bei X von " + anzahlBilder + " selbst entscheiden):",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            };
            DockPanel.SetDock(ueberschrift, Dock.Top);
            wurzel.Children.Add(ueberschrift);

            StackPanel untererBereich = new StackPanel();
            DockPanel.SetDock(untererBereich, Dock.Bottom);

            TextBlock freitextLabel = new TextBlock
            {
                Text = "Weitere Begriffe (mit Komma oder Leerzeichen getrennt):",
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

            List<CheckBox> checkboxen = new List<CheckBox>();

            foreach (string begriff in begriffsListe)
            {
                int treffer = kontexte.Count(k =>
                    k.Eintrag.BestaetigteStichwoerter.Any(b => string.Equals(b, begriff, StringComparison.OrdinalIgnoreCase)) ||
                    k.Vermutungen.Any(v => string.Equals(v.Begriff, begriff, StringComparison.OrdinalIgnoreCase) && v.SicherheitProzent >= mindestSicherheitProzent));

                string beschriftung;

                if (anzahlBilder == 1)
                {
                    VermuteterBegriff vermutung = kontexte[0].Vermutungen.FirstOrDefault(v => string.Equals(v.Begriff, begriff, StringComparison.OrdinalIgnoreCase));
                    beschriftung = vermutung != null ? begriff + " (" + vermutung.SicherheitProzent + "%)" : begriff;
                }
                else
                {
                    beschriftung = begriff + " (" + treffer + " von " + anzahlBilder + ")";
                }

                CheckBox checkbox = new CheckBox
                {
                    Content = beschriftung,
                    Tag = begriff,
                    Margin = new Thickness(2, 3, 2, 3)
                };

                bool vorausgewaehlt = vorausgewaehlteBegriffe.Any(b => string.Equals(b, begriff, StringComparison.OrdinalIgnoreCase));

                // Nur bei VOLLER Übereinstimmung (alle Bilder) automatisch
                // ankreuzen - ein Begriff, der nur auf einem Teil zutrifft,
                // wird nie stillschweigend übernommen.
                checkbox.IsChecked = vorausgewaehlt || treffer == anzahlBilder;

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

                List<string> neueFreitextBegriffe = new List<string>();

                if (!string.IsNullOrWhiteSpace(freitextBox.Text))
                {
                    foreach (string teil in freitextBox.Text.Split(new[] { ',', ';', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        string bereinigt = teil.Trim();

                        if (bereinigt.Length > 0
                            && !ergebnis.Any(x => string.Equals(x, bereinigt, StringComparison.OrdinalIgnoreCase))
                            && !neueFreitextBegriffe.Any(x => string.Equals(x, bereinigt, StringComparison.OrdinalIgnoreCase)))
                        {
                            neueFreitextBegriffe.Add(bereinigt);
                        }
                    }
                }

                if (neueFreitextBegriffe.Count > 0)
                {
                    // Bei mehreren Bildern braucht ein neuer, frei
                    // eingegebener Begriff eine eigene Sammelbestätigung -
                    // keine stillschweigende Massenänderung.
                    bool uebernehmen = true;

                    if (anzahlBilder > 1)
                    {
                        string frage = neueFreitextBegriffe.Count == 1
                            ? "'" + neueFreitextBegriffe[0] + "' zu allen " + anzahlBilder + " markierten Erinnerungen hinzufügen?"
                            : string.Join(", ", neueFreitextBegriffe.Select(b => "'" + b + "'")) + " zu allen " + anzahlBilder + " markierten Erinnerungen hinzufügen?";

                        uebernehmen = MessageBox.Show(frage, "Neue Begriffe bestätigen", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
                    }

                    if (uebernehmen)
                    {
                        ergebnis.AddRange(neueFreitextBegriffe);
                    }
                }

                fenster.DialogResult = true;
            };

            fenster.ShowDialog();

            return ergebnis;
        }

        // Weiterhin über den Werkzeuge-Button "Kategorie testen..."
        // erreichbar (Entwicklungswerkzeug, siehe A's Punkt 4 - fliegt erst
        // nach erfolgreicher Integration aus der normalen Oberfläche).
        // Nutzt jetzt denselben Stapelweg wie die Arbeitsmappe, nur mit
        // genau einem Bild.
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

            SehzentrumStapelErkennen(new List<string> { dialog.FileName });
        }
    }
}
