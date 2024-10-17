using System;
using System.Linq;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Synthesis;
using Mutagen.Bethesda.Skyrim;
using System.Threading.Tasks;
using System.IO;
using Mutagen.Bethesda.Plugins;
using System.Collections.Generic;
using DynamicData;
using Noggog;
using System.Collections;


namespace SpellTomePriceFixPatcher
{
    public class Program
    {
        public static Task<int> Main(string[] args)
        {
            return SynthesisPipeline.Instance
                .AddPatch<ISkyrimMod, ISkyrimModGetter>(RunPatch)
                .SetTypicalOpen(GameRelease.SkyrimSE, "spelltome_yeeter.esp")
                .Run(args);
        }

        public static void RunPatch(IPatcherState<ISkyrimMod, ISkyrimModGetter> state)
        {
            var whitelisted_mods = new HashSet<ModKey>();
            if (state.TryRetrieveConfigFile("whitelist.txt", out var textFile))
            {
                Console.WriteLine("*** DETECTED WHITELIST ***");
                foreach (string line in File.ReadAllLines(textFile))
                {
                    Console.WriteLine("mod tomes allowed: text " + line);
                    whitelisted_mods.Add(ModKey.FromNameAndExtension(line));
                }
                Console.WriteLine("*************************");
            }

            foreach (var placedobjectgetter in state.LoadOrder.PriorityOrder.PlacedObject().WinningContextOverrides(state.LinkCache))
            {
                //Find all placed tomes
                //If already disabled, skip
                if (whitelisted_mods.Contains(placedobjectgetter.ModKey)) continue;
                if (placedobjectgetter.Record.MajorRecordFlagsRaw == 0x0000_0800) continue;
                if (!placedobjectgetter.Record.Base.TryResolve<IBookGetter>(state.LinkCache, out var placedObjectBase)) continue;

                // if its a spell tome
                if (placedObjectBase.Teaches is not IBookSpellGetter) continue;

                // disable it
                IPlacedObject modifiedObject = placedobjectgetter.GetOrAddAsOverride(state.PatchMod);
                modifiedObject.MajorRecordFlagsRaw |= 0x0000_0800;
            }

            // Find all tomes in merchant lists
            foreach (var leveledList in state.LoadOrder.PriorityOrder.LeveledItem().WinningOverrides())
            {
                //If already disabled, skip
                if (leveledList.MajorRecordFlagsRaw == 0x0000_0800) continue;
                if (leveledList.Entries == null) continue;
                if (leveledList.EditorID == null) continue;

                if (whitelisted_mods.Contains(leveledList.FormKey.ModKey)) continue;

                if (leveledList.Entries.Any(e => e.Data?.Reference.TryResolve<IBookGetter>(state.LinkCache) != null))
                {
                    var modifiedList = state.PatchMod.LeveledItems.GetOrAddAsOverride(leveledList);
                    modifiedList.Entries ??= new();

                    modifiedList.Entries.SetTo(modifiedList.Entries
                        .ToArray()
                        .Where(e => e.Data?.Reference.TryResolve<IBookGetter>(state.LinkCache) == null));
                }
            }
        }
    }

}