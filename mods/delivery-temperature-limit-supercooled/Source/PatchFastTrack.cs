using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

// FastTrack mod replaces some of the game code, patch it too if present.
namespace DeliveryTemperatureLimit
{
    public class ChoreComparator_Patch
    {
        public static void Patch( Harmony harmony )
        {
            MethodInfo info = AccessTools.Method(
                Type.GetType( "PeterHan.FastTrack.GamePatches.ChoreComparator, FastTrack" ),
                "CheckFetchChore" );
            if( info != null )
                harmony.Patch( info, transpiler: new HarmonyMethod(
                    typeof( ChoreComparator_Patch ).GetMethod( nameof( CheckFetchChore ))));
        }

        public static IEnumerable<CodeInstruction> CheckFetchChore(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            bool found = false;
            int pickupableLoad = -1;
            for( int i = 0; i < codes.Count; ++i )
            {
//                Debug.Log("T:" + i + ":" + codes[i].opcode + "::" + (codes[i].operand != null ? codes[i].operand.ToString() : codes[i].operand));
                if( codes[ i ].opcode == OpCodes.Ldfld
                    && codes[ i ].operand.ToString().StartsWith( "Pickupable pickupable" )
                    && i + 2 < codes.Count
                    && codes[ i + 1 ].IsStloc()
                    && codes[ i + 2 ].IsLdloc())
                {
                    pickupableLoad = i + 2;
                }
                // The function has code:
                // if ( ... && FastCheckPreconditions(chore, transport))
                // Change to:
                // if (... && CheckFetchChore_Hook( chore, pickupable ) && FastCheckPreconditions(...))
                // Note that the original code is '(c1 && c2) || (c3 && c4))', so the evaluation
                // of the condition is a bit more complex.
                if( pickupableLoad != -1
                    && codes[ i ].opcode == OpCodes.Ldarg_0
                    && i + 4 < codes.Count
                    && codes[ i + 1 ].IsLdarg()
                    && codes[ i + 2 ].opcode == OpCodes.Ldsfld
                    && codes[ i + 2 ].operand.ToString() == "ChoreType transport"
                    && codes[ i + 3 ].opcode == OpCodes.Call
                    && codes[ i + 3 ].operand.ToString() == "Boolean FastCheckPreconditions(Chore, ChoreType)"
                    && codes[ i + 4 ].opcode == OpCodes.Brfalse_S )
                {
                    CodeInstruction loadChore = codes[ i + 1 ].Clone();
                    CodeInstruction brfalse = codes[ i + 4 ].Clone();
                    // The original code has a label at the first instruction (because of the more complex
                    // condition), this code should be included, so reuse the first instruction even
                    // though it's useless, and create a duplicate of it at the end to start the original code.
                    codes.Insert( i + 1, new CodeInstruction( OpCodes.Pop )); // drop the useless 'this'
                    codes.Insert( i + 2, loadChore ); // load 'chore'
                    codes.Insert( i + 3, codes[ pickupableLoad ].Clone()); // load 'pickupable'
                    codes.Insert( i + 4, new CodeInstruction( OpCodes.Call,
                        typeof( ChoreComparator_Patch ).GetMethod( nameof( CheckFetchChore_Hook ))));
                    codes.Insert( i + 5, brfalse ); // if false
                    codes.Insert( i + 6, new CodeInstruction( OpCodes.Ldarg_0 )); // the original first instruction
                    found = true;
                    break;
                }
            }
            if(!found)
                Debug.LogWarning("DeliveryTemperatureLimit: Failed to patch FastTrack ChoreComparator:CheckFetchChore()");
            return codes;
        }

        public static bool CheckFetchChore_Hook( FetchChore chore, Pickupable pickupable )
        {
            TemperatureLimit limit = TemperatureLimit.Get( chore.destination?.gameObject );
            if( limit == null || limit.IsDisabled())
                return true;
            if( pickupable?.PrimaryElement != null )
                return limit.AllowedByTemperature( pickupable.PrimaryElement.Temperature );
            return true;
        }
    }

    public class FetchManagerFastUpdate_PickupTagDict_Patch
    {
        public static void Patch( Harmony harmony )
        {
            MethodInfo info = AccessTools.Method(
                Type.GetType( "PeterHan.FastTrack.GamePatches.FetchManagerFastUpdate+PickupTagDict, FastTrack" ),
                "AddItem" );
            if( info != null )
                harmony.Patch( info, transpiler: new HarmonyMethod(
                    typeof( FetchManagerFastUpdate_PickupTagDict_Patch ).GetMethod( nameof( AddItem ))));
        }

        public static IEnumerable<CodeInstruction> AddItem(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            bool found = false;
            for( int i = 0; i < codes.Count; ++i )
            {
//                Debug.Log("T:" + i + ":" + codes[i].opcode + "::" + (codes[i].operand != null ? codes[i].operand.ToString() : codes[i].operand));
                // The function has code:
                // var key = new PickupTagKey(hash, target.KPrefabID);
                // Change to:
                // var key = new PickupTagKey(AddItem_Hook(hash, target), target.KPrefabID);
                if( codes[ i ].opcode == OpCodes.Ldloca_S && codes[ i ].operand.ToString().StartsWith(
                        "PeterHan.FastTrack.GamePatches.FetchManagerFastUpdate+PickupTagKey" )
                    && i + 4 < codes.Count
                    && codes[ i + 1 ].IsLdloc()
                    && codes[ i + 2 ].IsLdloc()
                    && codes[ i + 3 ].opcode == OpCodes.Ldfld
                    && codes[ i + 3 ].operand.ToString() == "KPrefabID KPrefabID"
                    && codes[ i + 4 ].opcode == OpCodes.Call
                    && codes[ i + 4 ].operand.ToString() == "Void .ctor(Int32, KPrefabID)" )
                {
                    codes.Insert( i + 2, codes[ i + 2 ].Clone()); // load 'target'
                    codes.Insert( i + 3, new CodeInstruction( OpCodes.Call,
                        typeof( FetchManagerFastUpdate_PickupTagDict_Patch ).GetMethod( nameof( AddItem_Hook ))));
                    found = true;
                    break;
                }
            }
            if(!found)
                Debug.LogWarning("DeliveryTemperatureLimit: Failed to patch FastTrack FetchManagerFastUpdate.PickupTagDict.AddItem()");
            return codes;
        }

        public static int AddItem_Hook( int hash, Pickupable pickupable )
        {
            if( pickupable?.PrimaryElement == null )
                return hash;
            // AddItem() uses a Dictionary with only tagBitsHash as the key, so it merges together pickupables
            // that belong into different temperature index groups. Add the temperature index to the hash to make
            // the dictionary key distinct. The key is not used for anything else than dictionary access, so this
            // should be ok.
            int num = hash;
            TemperatureLimit.TemperatureIndexData data = TemperatureLimit.getTemperatureIndexData();
            int index = data.TemperatureIndex( pickupable.PrimaryElement.Temperature );
            // The tagBitsHash value originally comes from Tag.GetHashCode(), and the Tag class uses Hash.SDBMLower().
            // This line is basically the line that computes Hash.SDBMLower(), so the temperature index should act
            // as another "character". Hopefully using the same hash code makes it extremely unlikely that
            // adding the temperature index would lead to collisions.
            num = index + (num << 6) + (num << 16) - num;
            return num;
        }
    }
}
