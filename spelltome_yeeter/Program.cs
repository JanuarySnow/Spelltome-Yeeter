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
                bool to_override = false;
                List<int> index_remove = new List<int>();
                FormKey lvl_key = leveledList.FormKey;
                FormLink<ILeveledItemGetter> my_lvl_link = new FormLink<ILeveledItemGetter>(lvl_key);
                // check if this leveled list is in the mod whitelist
                ModKey current_mod = lvl_key.ModKey;
                if (current_mod != null)
                {
                    if (whitelisted_mods.Contains(current_mod))
                    {
                        break;
                    }
                }
                for (var i = 0; i < leveledList.Entries.Count; i++)
                {

                    var entry = leveledList.Entries[i];
                    if (entry.Data == null)
                    {
                        continue;
                    }
                    else
                    {
                        if (state.LinkCache.TryResolve<IBookGetter>(entry.Data.Reference.FormKey,
                                                    out var resolved))
                        {
                            if (resolved.Teaches is IBookSpellGetter teachedSpell)
                            {
                                to_override = true;
                                index_remove.Add(i);
                            }
                            //detected at least one spell tome entry
                        }
                    }
                }
                if (to_override)
                {
                    var modifiedList = state.PatchMod.LeveledItems.GetOrAddAsOverride(leveledList);
                    if (modifiedList != null && modifiedList.Entries != null)
                    {
                        for (int i = modifiedList.Entries.Count - 1; i >= 0; i--)
                        {
                            if (index_remove.Contains(i)) // Condition to remove even numbers, for example
                            {
                                modifiedList.Entries.RemoveAt(i);
                            }

                        }

                    }
                }
            }
        }
    }
}