using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace DAS_LEBENSARCHIV
{
    // ============================================================
    // SANIERUNG BAUPHASE 3 - SICHERUNG + ECHTER MIGRATIONSLAUF (08.08.)
    // ============================================================
    // A's Sicherheitsauflage: Schritt 2 (Migration) ist erst möglich,
    // nachdem Schritt 1 (Sicherungskopie) erfolgreich erstellt UND
    // verifiziert wurde (Datei vorhanden, lesbar). personen.json wird
    // an KEINER Stelle in dieser Datei beschrieben - ausschließlich
    // gelesen (zum Kopieren) bzw. gar nicht angefasst. Es entsteht
    // ausschließlich eine NEUE Datei (erinnerungsmodell.json). Keine
    // physische Foto-Datei wird kopiert, verschoben oder verändert.
    public partial class MigrationDurchfuehrenFenster : Window
    {
        private readonly string ordnerPfad;
        private readonly string dateiPfad;
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

        private string sicherungsPfad;
        private bool sicherungVerifiziert;

        public MigrationDurchfuehrenFenster(
            string ordnerPfad,
            string dateiPfad,
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

            this.ordnerPfad = ordnerPfad;
            this.dateiPfad = dateiPfad;
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
        }

        // ============================================================
        // SCHRITT 1: SICHERUNGSKOPIE + VERIFIKATION
        // ============================================================
        private void SicherungErstellen_Click(object sender, RoutedEventArgs e)
        {
            bool ergebnis = James.FrageJaNein(
                "Es wird zuerst eine Sicherungskopie von personen.json erstellt, danach das neue Erinnerungsmodell in einer eigenen, neuen Datei angelegt.\n\n" +
                "personen.json selbst bleibt dabei unverändert, es wird keine Foto-Datei kopiert oder verschoben.\n\n" +
                "Sicherung jetzt erstellen?",
                James.TitelEntscheidung);

            if (!ergebnis)
            {
                return;
            }

            string zeitstempel = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            sicherungsPfad = Path.Combine(ordnerPfad, "personen_sicherung_vor_migration_" + zeitstempel + ".json");

            SicherungPfadText.Text = "Sicherungsziel: " + sicherungsPfad;

            try
            {
                // Bewusst overwrite:false - eine bereits vorhandene Sicherung
                // mit demselben Namen (praktisch ausgeschlossen, da
                // sekundengenauer Zeitstempel) würde NIE stillschweigend
                // überschrieben.
                File.Copy(dateiPfad, sicherungsPfad, overwrite: false);

                bool vorhanden = File.Exists(sicherungsPfad);
                long groesse = 0;
                bool lesbar = false;

                if (vorhanden)
                {
                    groesse = new FileInfo(sicherungsPfad).Length;

                    try
                    {
                        string inhalt = File.ReadAllText(sicherungsPfad);
                        lesbar = inhalt.Length > 0;
                    }
                    catch
                    {
                        lesbar = false;
                    }
                }

                sicherungVerifiziert = vorhanden && lesbar && groesse > 0;

                SicherungStatusText.Text = sicherungVerifiziert
                    ? "✓ Sicherung erstellt und verifiziert (" + groesse + " Bytes, vorhanden und lesbar)."
                    : "✗ Sicherung konnte nicht verifiziert werden - Migration bleibt gesperrt.";

                MigrationStartenButton.IsEnabled = sicherungVerifiziert;
                SicherungErstellenButton.IsEnabled = false;
            }
            catch (Exception ex)
            {
                SicherungStatusText.Text = "✗ Sicherung fehlgeschlagen: " + ex.Message + " - Migration bleibt gesperrt.";
                sicherungVerifiziert = false;
            }
        }

        // ============================================================
        // SCHRITT 2: ECHTER MIGRATIONSLAUF (schreibend, aber nur in
        // eine NEUE Datei - personen.json bleibt unangetastet)
        // ============================================================
        private async void MigrationStarten_Click(object sender, RoutedEventArgs e)
        {
            if (!sicherungVerifiziert)
            {
                return;
            }

            MigrationStartenButton.IsEnabled = false;
            Fortschrittsleiste.IsIndeterminate = true;
            StatusText.Text = "Migration läuft ...";

            IProgress<string> fortschritt = new Progress<string>(text => StatusText.Text = text);

            MigrationsErgebnis ergebnis = await Task.Run(() => FuehreMigrationDurch(fortschritt));

            string neueDateiPfad = Path.Combine(ordnerPfad, "erinnerungsmodell.json");

            ArchivErinnerungsDaten daten = new ArchivErinnerungsDaten
            {
                Erinnerungen = ergebnis.Erinnerungen,
                Zuordnungen = ergebnis.Zuordnungen
            };

            long neueGroesse = 0;
            bool geschriebenVerifiziert = false;
            string fehlerText = null;

            try
            {
                JsonSerializerOptions optionen = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(daten, optionen);

                File.WriteAllText(neueDateiPfad, json);

                if (File.Exists(neueDateiPfad))
                {
                    neueGroesse = new FileInfo(neueDateiPfad).Length;

                    string rueckgelesen = File.ReadAllText(neueDateiPfad);
                    ArchivErinnerungsDaten kontrolle = JsonSerializer.Deserialize<ArchivErinnerungsDaten>(rueckgelesen);

                    geschriebenVerifiziert = kontrolle != null
                        && kontrolle.Erinnerungen != null
                        && kontrolle.Zuordnungen != null
                        && kontrolle.Erinnerungen.Count == ergebnis.Erinnerungen.Count
                        && kontrolle.Zuordnungen.Count == ergebnis.Zuordnungen.Count;
                }
            }
            catch (Exception ex)
            {
                fehlerText = ex.Message;
            }

            Fortschrittsleiste.IsIndeterminate = false;
            Fortschrittsleiste.Value = 100;
            StatusText.Text = fehlerText == null ? "Migration abgeschlossen." : "Migration mit Fehler beendet.";

            ErgebnisText.Text = ErstelleBericht(ergebnis, sicherungsPfad, neueDateiPfad, neueGroesse, geschriebenVerifiziert, fehlerText);
        }

        private class MigrationsErgebnis
        {
            public readonly List<Erinnerung> Erinnerungen = new List<Erinnerung>();
            public readonly List<Zuordnung> Zuordnungen = new List<Zuordnung>();
            public int UebersprungenFehlend;
        }

        private MigrationsErgebnis FuehreMigrationDurch(IProgress<string> fortschritt)
        {
            MigrationsErgebnis ergebnis = new MigrationsErgebnis();
            int verarbeitet = 0;

            void ErzeugeErinnerung(string pfad, ZuordnungsZielTyp typ, Guid zielId, string zielBezeichnung)
            {
                if (!File.Exists(pfad))
                {
                    ergebnis.UebersprungenFehlend++;
                    return;
                }

                string hash;

                using (SHA256 sha256 = SHA256.Create())
                using (FileStream stream = File.OpenRead(pfad))
                {
                    hash = Convert.ToHexString(sha256.ComputeHash(stream));
                }

                Erinnerung erinnerung = new Erinnerung { Hashwert = hash };
                erinnerung.Fundorte.Add(new Fundort { Pfad = pfad });

                ergebnis.Erinnerungen.Add(erinnerung);

                ergebnis.Zuordnungen.Add(new Zuordnung
                {
                    ErinnerungId = erinnerung.Id,
                    ZielTyp = typ,
                    ZielId = zielId,
                    ZielBezeichnung = zielBezeichnung
                });

                verarbeitet++;

                if (verarbeitet % 20 == 0)
                {
                    fortschritt.Report("Migriere ... (" + verarbeitet + " bisher)");
                }
            }

            void VerarbeiteEreignis(Person person, Ereignis ereignis)
            {
                string ordner = erinnerungsOrdnerFuer(person, ereignis);

                if (!string.IsNullOrEmpty(ereignis.EreignisFotoDateiname))
                {
                    ErzeugeErinnerung(Path.Combine(ordner, ereignis.EreignisFotoDateiname), ZuordnungsZielTyp.Ereignis, ereignis.Id, ereignis.Titel);
                }

                if (ereignis.WeitereFotoDateinamen != null)
                {
                    foreach (string dateiname in ereignis.WeitereFotoDateinamen)
                    {
                        ErzeugeErinnerung(Path.Combine(ordner, dateiname), ZuordnungsZielTyp.Ereignis, ereignis.Id, ereignis.Titel);
                    }
                }
            }

            void VerarbeitePersonenListe(IEnumerable personen)
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
                            ErzeugeErinnerung(Path.Combine(ordner, dateiname), ZuordnungsZielTyp.Person, person.Id, person.ToString());
                        }
                    }

                    if (person.Ereignisse != null)
                    {
                        foreach (Ereignis ereignis in person.Ereignisse)
                        {
                            VerarbeiteEreignis(person, ereignis);
                        }
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
                        ErzeugeErinnerung(Path.Combine(ordner, sammlung.SammlungFotoDateiname), ZuordnungsZielTyp.Sammlung, sammlung.Id, sammlung.Titel);
                    }

                    if (sammlung.WeitereFotoDateinamen != null)
                    {
                        foreach (string dateiname in sammlung.WeitereFotoDateinamen)
                        {
                            ErzeugeErinnerung(Path.Combine(ordner, dateiname), ZuordnungsZielTyp.Sammlung, sammlung.Id, sammlung.Titel);
                        }
                    }
                }
            }

            fortschritt.Report("Migriere Personen (Schreibtisch) ...");
            VerarbeitePersonenListe(personenSchreibtisch);

            fortschritt.Report("Migriere Personen (Archiv) ...");
            VerarbeitePersonenListe(personenArchiv);

            fortschritt.Report("Migriere Personen (Papierkorb) ...");
            VerarbeitePersonenListe(personenPapierkorb);

            fortschritt.Report("Migriere freie Ereignisse ...");
            VerarbeiteFreieEreignisseListe(freieEreignisse);
            VerarbeiteFreieEreignisseListe(freieEreignisseArchiv);
            VerarbeiteFreieEreignisseListe(freieEreignissePapierkorb);

            fortschritt.Report("Migriere Sammlungen ...");
            VerarbeiteSammlungenListe(sammlungen);
            VerarbeiteSammlungenListe(sammlungenArchiv);
            VerarbeiteSammlungenListe(sammlungenPapierkorb);

            return ergebnis;
        }

        private static string ErstelleBericht(MigrationsErgebnis ergebnis, string sicherungsPfad, string neueDateiPfad, long neueGroesse, bool geschriebenVerifiziert, string fehlerText)
        {
            int gesamt = ergebnis.Erinnerungen.Count;
            int person = ergebnis.Zuordnungen.Count(z => z.ZielTyp == ZuordnungsZielTyp.Person);
            int ereignis = ergebnis.Zuordnungen.Count(z => z.ZielTyp == ZuordnungsZielTyp.Ereignis);
            int sammlung = ergebnis.Zuordnungen.Count(z => z.ZielTyp == ZuordnungsZielTyp.Sammlung);

            StringBuilder text = new StringBuilder();

            text.AppendLine("MIGRATIONSBERICHT");
            text.AppendLine();
            text.AppendLine("Sicherungskopie: " + sicherungsPfad);
            text.AppendLine("  -> erstellt und verifiziert (vorhanden, lesbar)");
            text.AppendLine();

            if (fehlerText != null)
            {
                text.AppendLine("✗ FEHLER beim Speichern des neuen Modells: " + fehlerText);
                text.AppendLine("  personen.json wurde davon NICHT berührt.");
                return text.ToString();
            }

            text.AppendLine("Neu erzeugt und gespeichert unter: " + neueDateiPfad);
            text.AppendLine("  " + neueGroesse + " Bytes, " + (geschriebenVerifiziert
                ? "durch Rückeinlesen verifiziert (Anzahl stimmt überein)."
                : "KONNTE NICHT VOLLSTÄNDIG VERIFIZIERT WERDEN - bitte manuell prüfen!"));
            text.AppendLine();
            text.AppendLine(gesamt + " Erinnerungen erzeugt");
            text.AppendLine(gesamt + " Fundorte (je Erinnerung genau einer)");
            text.AppendLine(ergebnis.Zuordnungen.Count + " Zuordnungen, davon:");
            text.AppendLine("    " + person + " zu Personen");
            text.AppendLine("    " + ereignis + " zu Ereignissen");
            text.AppendLine("    " + sammlung + " zu Sammlungen");

            if (ergebnis.UebersprungenFehlend > 0)
            {
                text.AppendLine();
                text.AppendLine("⚠ " + ergebnis.UebersprungenFehlend + " Datei(en) waren nicht mehr vorhanden und wurden übersprungen.");
            }

            text.AppendLine();
            text.AppendLine("WICHTIG: personen.json wurde an keiner Stelle verändert. Keine alte Struktur wurde gelöscht oder umgestellt. Keine physische Foto-Datei wurde kopiert oder verschoben - jeder Fundort verweist nur auf den bereits bestehenden Speicherort.");

            return text.ToString();
        }
    }
}
