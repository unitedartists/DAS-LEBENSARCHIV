using System.Windows;

namespace DAS_LEBENSARCHIV
{
    // ============================================================
    // SANIERUNG BAUPHASE 3 - SICHERUNG + ECHTER MIGRATIONSLAUF (08.08.)
    // ============================================================
    // Bewusst als eigene, neue Datei angelegt (wie schon bei
    // Bauphase 1 und 2) - jede bestehende Datei bleibt unangetastet.
    // Übergibt OrdnerPfad/DateiPfad sowie dieselben bereits geladenen
    // echten Daten und bewährten Pfad-Hilfsmethoden wie der
    // Trockenlauf (Bauphase 2) - keine parallele, eigene Pfadlogik.
    public partial class MainWindow
    {
        private void MigrationDurchfuehrenOeffnen_Click(object sender, RoutedEventArgs e)
        {
            MigrationDurchfuehrenFenster fenster = new MigrationDurchfuehrenFenster(
                OrdnerPfad,
                DateiPfad,
                allePersonen,
                ArchivListe.Items,
                PapierkorbListe.Items,
                freieEreignisse,
                freieEreignisseArchiv,
                freieEreignissePapierkorb,
                sammlungen,
                sammlungenArchiv,
                sammlungenPapierkorb,
                PersonErinnerungsOrdner,
                ErinnerungsOrdnerFuer,
                ErinnerungsOrdnerFuerSammlung);

            fenster.Owner = this;
            fenster.ShowDialog();
        }
    }
}
