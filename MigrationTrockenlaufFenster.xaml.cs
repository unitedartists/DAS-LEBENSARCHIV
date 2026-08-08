using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace DAS_LEBENSARCHIV
{
    // ============================================================
    // SANIERUNG BAUPHASE 2 - MIGRATIONS-TROCKENLAUF (08.08.)
    // ============================================================
    // Rein lesend: liest die bereits im Arbeitsspeicher geladenen
    // ECHTEN Daten (Personen/Ereignisse/Sammlungen, jeweils
    // Schreibtisch/Archiv/Papierkorb) und berechnet, was ein echter
    // Migrationslauf erzeugen würde - OHNE irgendetwas zu schreiben,
    // zu kopieren oder zu verändern. An keiner Stelle in dieser
    // Datei wird File.Copy, File.Delete, File.Move oder
    // File.WriteAllText aufgerufen - ausschließlich File.Exists zur
    // Prüfung, ob die referenzierten Dateien noch vorhanden sind.
    //
    // Bewusst OHNE Hash-Berechnung: bei rund 170.000 Dateien wäre
    // das für einen reinen Zähllauf zu aufwendig - der Hash wird
    // erst beim echten, später separat freizugebenden
    // Migrationslauf berechnet.
    public partial class MigrationTrockenlaufFenster : Window
    {
        private readonly IEnumerable personenSchreibtisch;
        private readonly IEnumerable personenArchiv;
        private readonly IEnumerable personenPapierkorb;
        private readonly List<Ereignis> freieEreignisse;
        private readonly List<Ereignis> freieEreignisseArchiv;
        private readonly List<Ereignis> freieEreignissePapierkorb;
        private readonly List<Sammlung> sammlungen;
        private readonly List<Sammlung> sammlungenArchiv;
        private readonly List<Sammlung> sammlungenPapierkorb;
        private readonly Func<Person, string> personErinnerungsOrdner;
        private readonly Func<Person, Ereignis, string> erinnerungsOrdnerFuer;
        private readonly Func<Sammlung, string> erinnerungsOrdnerFuerSammlung;

        public MigrationTrockenlaufFenster(
            IEnumerable personenSchreibtisch,
            IEnumerable personenArchiv,
            IEnumerable personenPapierkorb,
            List<Ereignis> freieEreignisse,
            List<Ereignis> freieEreignisseArchiv,
            List<Ereignis> freieEreignissePapierkorb,
            List<Sammlung> sammlungen,
            List<Sammlung> sammlungenArchiv,
            List<Sammlung> sammlungenPapierkorb,
            Func<Person, string> personErinnerungsOrdner,
            Func<Person, Ereignis, string> erinnerungsOrdnerFuer,
            Func<Sammlung, string> erinnerungsOrdnerFuerSammlung)
        {
            InitializeComponent();

            this.personenSchreibtisch = personenSchreibtisch;
            this.personenArchiv = personenArchiv;
            this.personenPapierkorb = personenPapierkorb;
            this.freieEreignisse = freieEreignisse;
            this.freieEreignisseArchiv = freieEreignisseArchiv;
            this.freieEreignissePapierkorb = freieEreignissePapierkorb;
            this.sammlungen = sammlungen;
            this.sammlungenArchiv = sammlungenArchiv;
            this.sammlungenPapierkorb = sammlungenPapierkorb;
            this.personErinnerungsOrdner = personErinnerungsOrdner;
            this.erinnerungsOrdnerFuer = erinnerungsOrdnerFuer;
            this.erinnerungsOrdnerFuerSammlung = erinnerungsOrdnerFuerSammlung;

            Loaded += MigrationTrockenlaufFenster_Loaded;
        }

        private async void MigrationTrockenlaufFenster_Loaded(object sender, RoutedEventArgs e)
        {
            StatusText.Text = "Trockenlauf läuft ...";

            IProgress<string> fortschritt = new Progress<string>(text => StatusText.Text = text);

            TrockenlaufErgebnis ergebnis = await Task.Run(() => FuehreTrockenlaufDurch(fortschritt));

            Fortschrittsleiste.IsIndeterminate = false;
            Fortschrittsleiste.Value = 100;
            StatusText.Text = "Trockenlauf abgeschlossen. Es wurde nichts verändert.";
            ErgebnisText.Text = ErstelleBericht(ergebnis);
        }

        private class TrockenlaufErgebnis
        {
            public int ZuordnungenPerson;
            public int ZuordnungenEreignis;
            public int ZuordnungenSammlung;
            public int FehlendeDateien;
            public readonly List<string> FehlendeDateiBeispiele = new List<string>();
        }

        private TrockenlaufErgebnis FuehreTrockenlaufDurch(IProgress<string> fortschritt)
        {
            TrockenlaufErgebnis ergebnis = new TrockenlaufErgebnis();
            int verarbeitetePersonen = 0;

            void PruefeDatei(string pfad, ZuordnungsZielTyp typ)
            {
                if (typ == ZuordnungsZielTyp.Person)
                {
                    ergebnis.ZuordnungenPerson++;
                }
                else if (typ == ZuordnungsZielTyp.Ereignis)
                {
                    ergebnis.ZuordnungenEreignis++;
                }
                else
                {
                    ergebnis.ZuordnungenSammlung++;
                }

                // Bewusst nur File.Exists - keine Schreib-, Kopier- oder
                // Löschoperation an dieser oder irgendeiner anderen Stelle
                // dieser Datei.
                if (!File.Exists(pfad))
                {
                    ergebnis.FehlendeDateien++;

                    if (ergebnis.FehlendeDateiBeispiele.Count < 50)
                    {
                        ergebnis.FehlendeDateiBeispiele.Add(pfad);
                    }
                }
            }

            void VerarbeiteEreignis(Person person, Ereignis ereignis)
            {
                string ordner = erinnerungsOrdnerFuer(person, ereignis);

                if (!string.IsNullOrEmpty(ereignis.EreignisFotoDateiname))
                {
                    PruefeDatei(Path.Combine(ordner, ereignis.EreignisFotoDateiname), ZuordnungsZielTyp.Ereignis);
                }

                if (ereignis.WeitereFotoDateinamen != null)
                {
                    foreach (string dateiname in ereignis.WeitereFotoDateinamen)
                    {
                        PruefeDatei(Path.Combine(ordner, dateiname), ZuordnungsZielTyp.Ereignis);
                    }
                }
            }

            void VerarbeitePersonenListe(IEnumerable personen, string beschriftung)
            {
                foreach (object eintrag in personen)
                {
                    Person person = eintrag as Person;

                    if (person == null)
                    {
                        continue;
                    }

                    if (person.ErinnerungsDateinamen != null)
                    {
                        string ordner = personErinnerungsOrdner(person);

                        foreach (string dateiname in person.ErinnerungsDateinamen)
                        {
                            PruefeDatei(Path.Combine(ordner, dateiname), ZuordnungsZielTyp.Person);
                        }
                    }

                    if (person.Ereignisse != null)
                    {
                        foreach (Ereignis ereignis in person.Ereignisse)
                        {
                            VerarbeiteEreignis(person, ereignis);
                        }
                    }

                    verarbeitetePersonen++;

                    if (verarbeitetePersonen % 200 == 0)
                    {
                        fortschritt.Report("Verarbeite " + beschriftung + " ... (" + verarbeitetePersonen + " Personen bisher)");
                    }
                }
            }

            void VerarbeiteFreieEreignisseListe(List<Ereignis> liste)
            {
                foreach (Ereignis ereignis in liste)
                {
                    VerarbeiteEreignis(null, ereignis);
                }
            }

            void VerarbeiteSammlungenListe(List<Sammlung> liste)
            {
                foreach (Sammlung sammlung in liste)
                {
                    string ordner = erinnerungsOrdnerFuerSammlung(sammlung);

                    if (!string.IsNullOrEmpty(sammlung.SammlungFotoDateiname))
                    {
                        PruefeDatei(Path.Combine(ordner, sammlung.SammlungFotoDateiname), ZuordnungsZielTyp.Sammlung);
                    }

                    if (sammlung.WeitereFotoDateinamen != null)
                    {
                        foreach (string dateiname in sammlung.WeitereFotoDateinamen)
                        {
                            PruefeDatei(Path.Combine(ordner, dateiname), ZuordnungsZielTyp.Sammlung);
                        }
                    }
                }
            }

            fortschritt.Report("Verarbeite Personen (Schreibtisch) ...");
            VerarbeitePersonenListe(personenSchreibtisch, "Personen (Schreibtisch)");

            fortschritt.Report("Verarbeite Personen (Archiv) ...");
            VerarbeitePersonenListe(personenArchiv, "Personen (Archiv)");

            fortschritt.Report("Verarbeite Personen (Papierkorb) ...");
            VerarbeitePersonenListe(personenPapierkorb, "Personen (Papierkorb)");

            fortschritt.Report("Verarbeite freie Ereignisse ...");
            VerarbeiteFreieEreignisseListe(freieEreignisse);
            VerarbeiteFreieEreignisseListe(freieEreignisseArchiv);
            VerarbeiteFreieEreignisseListe(freieEreignissePapierkorb);

            fortschritt.Report("Verarbeite Sammlungen ...");
            VerarbeiteSammlungenListe(sammlungen);
            VerarbeiteSammlungenListe(sammlungenArchiv);
            VerarbeiteSammlungenListe(sammlungenPapierkorb);

            return ergebnis;
        }

        private static string ErstelleBericht(TrockenlaufErgebnis ergebnis)
        {
            int gesamt = ergebnis.ZuordnungenPerson + ergebnis.ZuordnungenEreignis + ergebnis.ZuordnungenSammlung;

            StringBuilder text = new StringBuilder();

            text.AppendLine("ERGEBNIS DES TROCKENLAUFS - nichts wurde verändert, kopiert oder gespeichert.");
            text.AppendLine();
            text.AppendLine("Ein echter Migrationslauf würde nach heutigem Datenstand erzeugen:");
            text.AppendLine();
            text.AppendLine("  " + gesamt + " Erinnerungen");
            text.AppendLine("  (ohne Deduplizierung entspricht das exakt der heutigen Anzahl an Dateiverweisen -");
            text.AppendLine("   das ist gewollt, siehe Anschlussplan Punkt 6)");
            text.AppendLine();
            text.AppendLine("  " + gesamt + " Fundorte (je Erinnerung genau einer: der heutige Speicherort der Kopie)");
            text.AppendLine();
            text.AppendLine("  " + gesamt + " Zuordnungen, davon:");
            text.AppendLine("      " + ergebnis.ZuordnungenPerson + " direkt zu Personen (ohne Ereignis-Kontext)");
            text.AppendLine("      " + ergebnis.ZuordnungenEreignis + " zu Ereignissen");
            text.AppendLine("      " + ergebnis.ZuordnungenSammlung + " zu Sammlungen");
            text.AppendLine();

            if (ergebnis.FehlendeDateien == 0)
            {
                text.AppendLine("✓ Alle referenzierten Dateien wurden auf der Festplatte gefunden.");
            }
            else
            {
                text.AppendLine("⚠ " + ergebnis.FehlendeDateien + " referenzierte Datei(en) wurden NICHT gefunden.");
                text.AppendLine("   Das ist ein bereits heute bestehender Zustand, unabhängig von dieser Sanierung -");
                text.AppendLine("   diese Erinnerungen würden bei einer echten Migration ohne gültigen Fundort bleiben.");
                text.AppendLine("   Beispiele (max. 50 gezeigt):");
                text.AppendLine();

                foreach (string pfad in ergebnis.FehlendeDateiBeispiele)
                {
                    text.AppendLine("   - " + pfad);
                }
            }

            return text.ToString();
        }
    }
}
