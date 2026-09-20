using System;
using System.Globalization;
using System.IO;

namespace Gaia.Core.Services.Diagnostics
{
    /// <summary>
    ///     Writes what a monitor recorded out as text, so a decision that already happened can be read after the run and
    ///     one run compared against another.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The format is one line per thing recorded, the kind first and the fields separated by tabs, so a grep picks
    ///         out one option's history and two dumps of two runs diff line for line. Columns are not padded, since padding
    ///         would tie every line's bytes to the widest name and renaming one option would rewrite the whole file. Every
    ///         line ends with a line feed alone, the way <see cref="Specifications.XmlSpecificationWriter" /> ends its lines,
    ///         so a dump written on one platform diffs against one written on another. A name holding a tab is the one thing
    ///         the columns cannot carry.
    ///     </para>
    ///     <para>
    ///         Where the text goes is the host's. A <see cref="TextWriter" /> is an abstraction over a destination rather
    ///         than a file, so the library still opens nothing and a host writes to the file, console or buffer it already
    ///         owns, while a spec writes into a StringWriter and reads the text back.
    ///         <see cref="Specifications.XmlSpecificationWriter" /> returns a string instead because a specification document
    ///         is small and is compared byte for byte, where a history is a couple of thousand lines at its default capacities
    ///         and tens of thousands at a larger one.
    ///     </para>
    ///     <para>
    ///         When a dump is taken matters more than where it goes. A history holds
    ///         <see cref="DecisionHistory.CycleCapacity" /> cycles and then overwrites its oldest, 256 of them by default,
    ///         which is about four seconds at sixty cycles a second, so the dump worth having is the one taken as a warning
    ///         fires rather than when someone reaches for it. A warning arrives at the host's <see cref="Log.ILogSink" />,
    ///         which is host code that already owns the destination, so a host that wants this calls the writer from its own
    ///         sink.
    ///     </para>
    ///     <para>
    ///         Floats are written to nine significant digits, which is what reads back as the same float on every runtime
    ///         this assembly loads on. Everywhere else the library prints a float the way the invariant culture would, which
    ///         is shorter to read. A dump is compared against another run, so a rounded number here would hide the difference
    ///         it was taken to find.
    ///     </para>
    /// </remarks>
    public sealed class DecisionHistoryWriter
    {
        /// <summary>Names the format a dump is in, so a reader can refuse one it does not know.</summary>
        private const string FormatName = "gaia decision history 1";

        /// <summary>
        ///     Writes every reasoner the brain monitor watches, the root first and then in tree order, under a line naming
        ///     the brain.
        /// </summary>
        public void Write(BrainMonitor brainMonitor, TextWriter writer)
        {
            WriteLegend(writer);
            writer.Write(FormattableString.Invariant(
                $"brain\t{brainMonitor.LogChannel.Text}\t{brainMonitor.CycleIndex}\n"));

            foreach (ReasonerMonitor reasonerMonitor in brainMonitor.ReasonerMonitors)
            {
                WriteReasoner(writer, reasonerMonitor.Path, reasonerMonitor.History);
            }
        }

        /// <summary>Writes one reasoner's history, under a line carrying the path that says where in the tree it sits.</summary>
        public void Write(ReasonerMonitor reasonerMonitor, TextWriter writer)
        {
            WriteLegend(writer);
            WriteReasoner(writer, reasonerMonitor.Path, reasonerMonitor.History);
        }

        /// <summary>Writes one history on its own, under a reasoner line whose path is empty.</summary>
        /// <remarks>A history built by hand belongs to no tree, so there is no path for its line to carry.</remarks>
        public void Write(DecisionHistory history, TextWriter writer)
        {
            WriteLegend(writer);
            WriteReasoner(writer, string.Empty, history);
        }

        /// <summary>Writes the format's name and the fields of each line kind, so a dump says what its columns are.</summary>
        private static void WriteLegend(TextWriter writer)
        {
            writer.Write($"# {FormatName}\n");
            writer.Write("# brain\tchannel\tcycles\n");
            writer.Write("# reasoner\tpath\tcycles\tcycleCapacity\toptionCapacity\n");
            writer.Write("# cycle\tindex\troll\tselected\trecords\n");
            writer.Write("# record\toption\trank\tweight\tpassedScreening\n");
        }

        /// <summary>Writes a history's cycles under a line of its own, oldest first so the file reads forward in time.</summary>
        /// <remarks>
        ///     A cycle that selected nothing writes an empty name, which is what <see cref="DecisionCycle.SelectedName" />
        ///     holds for one.
        /// </remarks>
        private static void WriteReasoner(TextWriter writer, string path, DecisionHistory history)
        {
            writer.Write(FormattableString.Invariant(
                $"reasoner\t{path}\t{history.CycleCount}\t{history.CycleCapacity}\t{history.OptionCapacity}\n"));

            for (int age = history.CycleCount - 1; age >= 0; age--)
            {
                DecisionCycle cycle = history.GetCycle(age);
                string selected = cycle.SelectedName.Text;

                writer.Write(FormattableString.Invariant(
                    $"cycle\t{cycle.CycleIndex}\t{Number(cycle.Roll)}\t{selected}\t{cycle.RecordCount}\n"));

                for (int optionIndex = 0; optionIndex < cycle.RecordCount; optionIndex++)
                {
                    DecisionRecord record = history.GetRecord(age, optionIndex);
                    string option = record.OptionName.Text;
                    string passedScreening = record.PassedScreening ? "true" : "false";

                    writer.Write(FormattableString.Invariant(
                        $"record\t{option}\t{Number(record.Rank)}\t{Number(record.Weight)}\t{passedScreening}\n"));
                }
            }
        }

        /// <summary>A float as it reads back, nine significant digits in the invariant culture.</summary>
        private static string Number(float value)
        {
            return value.ToString("G9", CultureInfo.InvariantCulture);
        }
    }
}
