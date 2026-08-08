using System.Windows;

namespace DAS_LEBENSARCHIV
{
    // ============================================================
    // SANIERUNG BAUPHASE 2 - MIGRATIONS-TROCKENLAUF (08.08.)
    // ============================================================
    // Bewusst als eigene, neue Datei angelegt (wie schon bei
    // Bauphase 1) - jede bestehende Datei bleibt dadurch
    // unangetastet. Übergibt dem Trockenlauf-Fenster ausschließlich
    // die bereits im Speicher geladenen echten Daten sowie die
    // bestehenden, bewährten Pfad-Hilfsmethoden (per Delegate) -
    // keine eigene, parallele Pfadlogik.
    public partial class MainWindow
    {
        private void MigrationTrockenlaufOeffnen_Click(object sender, RoutedEventArgs e)
        {
            MigrationTrockenlaufFenster fenster = new MigrationTrockenlaufFenster(
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
